using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(o => o.ServiceName = "SafeWeb Gateway");
builder.WebHost.UseUrls("http://127.0.0.1:8787");
builder.Services.AddSingleton<RuleStore>();
builder.Services.AddSingleton<DomainFilter>();
builder.Services.AddSingleton<HotspotNetwork>();
builder.Services.AddHostedService<DnsProxy>();
// Register WinDivertGateway as a concrete singleton as well as a hosted service so API endpoints can resolve it.
builder.Services.AddSingleton<WinDivertGateway>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WinDivertGateway>());
var app = builder.Build();

var store = app.Services.GetRequiredService<RuleStore>();
await store.InitializeAsync();

app.MapGet("/api/status", (HotspotNetwork net, [FromServices] WinDivertGateway gw) => Results.Ok(new
{
    service = "running",
    mode = store.Mode,
    version = store.Version,
    cloud = false,
    dnsPort = store.DnsPort,
    enforcement = gw.Status,
    hotspot = net.GetStatus(),
    blockQuic = gw.BlockQuic
}));
app.MapGet("/api/rules", async () => Results.Ok(await store.GetRulesAsync()));
app.MapPost("/api/rules", async (RuleInput input) => Results.Ok(await store.UpsertAsync(input)));
app.MapDelete("/api/rules/{id:int}", async (int id) => Results.Ok(await store.DeleteAsync(id)));
app.MapPost("/api/mode", async (ModeInput input) => { await store.SetModeAsync(input.Mode); return Results.Ok(new { mode = store.Mode }); });
app.MapGet("/api/activity", async () => Results.Ok(await store.GetActivityAsync(500)));
app.MapPost("/api/test", (DomainInput input, [FromServices] DomainFilter filter) => Results.Ok(filter.Decide(input.Domain)));
app.MapGet("/api/network", (HotspotNetwork net) => Results.Ok(net.GetStatus()));
app.MapPost("/api/network/quic", (QuicInput input, [FromServices] WinDivertGateway gw) => { gw.BlockQuic = input.Block; return Results.Ok(new { blockQuic = gw.BlockQuic }); });
app.MapGet("/", async context => { context.Response.ContentType = "text/html; charset=utf-8"; await context.Response.SendFileAsync(Path.Combine(AppContext.BaseDirectory, "web", "index.html")); });
app.Run();

public record RuleInput(int? Id, string Domain, string Action, bool Enabled = true, string Scope = "global", string? DeviceKey = null);
public record ModeInput(string Mode);
public record DomainInput(string Domain);
public record QuicInput(bool Block);
public record Rule(int Id, string Domain, string Action, bool Enabled, string Scope, string? DeviceKey, string UpdatedUtc);
public record Activity(string Timestamp, string Device, string Domain, string Action, string Reason, int Count);
public record Decision(string Domain, string Action, string? MatchedRule, string Reason);

public sealed class RuleStore
{
    private readonly string _db;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string Mode { get; private set; } = "blocklist";
    public long Version { get; private set; }
    public int DnsPort { get; private set; } = 5353;

    public RuleStore()
    {
        _db = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SafeWeb", "safeweb.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_db)!);
    }

    SqliteConnection C() => new($"Data Source={_db};Cache=Shared");

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await using var c = C(); await c.OpenAsync();
            var cmd = c.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS settings(k TEXT PRIMARY KEY,v TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS rules(id INTEGER PRIMARY KEY AUTOINCREMENT,domain TEXT NOT NULL UNIQUE,action TEXT NOT NULL,enabled INTEGER NOT NULL,scope TEXT NOT NULL,device_key TEXT,updated_utc TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS ix_rules_domain ON rules(domain);
CREATE TABLE IF NOT EXISTS activity(id INTEGER PRIMARY KEY AUTOINCREMENT,ts_utc TEXT NOT NULL,device TEXT NOT NULL,domain TEXT NOT NULL,action TEXT NOT NULL,rule TEXT,reason TEXT,count INTEGER NOT NULL DEFAULT 1,last_seen_utc TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS ix_activity_domain ON activity(domain,action,last_seen_utc);";
            await cmd.ExecuteNonQueryAsync();
            Mode = await GetSetting(c, "mode") ?? "blocklist";
            Version = long.TryParse(await GetSetting(c, "version"), out var v) ? v : 0;
            DnsPort = int.TryParse(await GetSetting(c, "dnsPort"), out var p) ? p : 55353;
        }
        finally { _gate.Release(); }
    }

    static async Task<string?> GetSetting(SqliteConnection c, string k)
    {
        var q = c.CreateCommand(); q.CommandText = "SELECT v FROM settings WHERE k=$k"; q.Parameters.AddWithValue("$k", k);
        return await q.ExecuteScalarAsync() as string;
    }

    async Task Bump(SqliteConnection c)
    {
        Version++;
        var q = c.CreateCommand(); q.CommandText = "INSERT INTO settings(k,v) VALUES('version',$v) ON CONFLICT(k) DO UPDATE SET v=$v";
        q.Parameters.AddWithValue("$v", Version.ToString()); await q.ExecuteNonQueryAsync();
    }

    public async Task SetModeAsync(string mode)
    {
        mode = mode.Trim().ToLowerInvariant();
        if (mode is not ("blocklist" or "allowlist")) throw new ArgumentException("Mode must be blocklist or allowlist");
        await _gate.WaitAsync();
        try
        {
            await using var c = C(); await c.OpenAsync();
            var q = c.CreateCommand(); q.CommandText = "INSERT INTO settings(k,v) VALUES('mode',$v) ON CONFLICT(k) DO UPDATE SET v=$v";
            q.Parameters.AddWithValue("$v", mode); await q.ExecuteNonQueryAsync(); Mode = mode; await Bump(c);
        }
        finally { _gate.Release(); }
    }

    public async Task<object> UpsertAsync(RuleInput x)
    {
        var d = DomainFilter.Normalize(x.Domain); var a = x.Action.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(d) || d.Contains(' ') || d.Contains('/') || d.Contains(':')) throw new ArgumentException("Invalid domain");
        if (a is not ("ALLOW" or "BLOCK")) throw new ArgumentException("Action must be ALLOW or BLOCK");
        await _gate.WaitAsync();
        try
        {
            await using var c = C(); await c.OpenAsync();
            var q = c.CreateCommand();
            if (x.Id.HasValue)
            {
                q.CommandText = "UPDATE rules SET domain=$d,action=$a,enabled=$e,scope=$s,device_key=$k,updated_utc=$t WHERE id=$id";
                q.Parameters.AddWithValue("$id", x.Id.Value);
            }
            else q.CommandText = "INSERT INTO rules(domain,action,enabled,scope,device_key,updated_utc) VALUES($d,$a,$e,$s,$k,$t) RETURNING id";
            q.Parameters.AddWithValue("$d", d); q.Parameters.AddWithValue("$a", a); q.Parameters.AddWithValue("$e", x.Enabled ? 1 : 0);
            q.Parameters.AddWithValue("$s", x.Scope); q.Parameters.AddWithValue("$k", (object?)x.DeviceKey ?? DBNull.Value); q.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
            var id = x.Id ?? Convert.ToInt32(await q.ExecuteScalarAsync()); await Bump(c);
            return new { id, domain = d, action = a, enabled = x.Enabled };
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await _gate.WaitAsync();
        try
        {
            await using var c = C(); await c.OpenAsync(); var q = c.CreateCommand(); q.CommandText = "DELETE FROM rules WHERE id=$id"; q.Parameters.AddWithValue("$id", id);
            var n = await q.ExecuteNonQueryAsync(); if (n > 0) await Bump(c); return n > 0;
        }
        finally { _gate.Release(); }
    }

    public async Task<List<Rule>> GetRulesAsync()
    {
        var list = new List<Rule>(); await using var c = C(); await c.OpenAsync(); var q = c.CreateCommand();
        q.CommandText = "SELECT id,domain,action,enabled,scope,device_key,updated_utc FROM rules ORDER BY domain";
        await using var r = await q.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3) != 0, r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6)));
        return list;
    }

    public async Task LogAsync(string device, string domain, string action, string reason)
    {
        await using var c = C(); await c.OpenAsync();
        var now = DateTime.UtcNow; var since = now.AddMinutes(-2);
        var q = c.CreateCommand(); q.CommandText = "SELECT id FROM activity WHERE device=$dev AND domain=$d AND action=$a AND last_seen_utc >= $s ORDER BY id DESC LIMIT 1";
        q.Parameters.AddWithValue("$dev", device); q.Parameters.AddWithValue("$d", domain); q.Parameters.AddWithValue("$a", action); q.Parameters.AddWithValue("$s", since.ToString("O"));
        var existing = await q.ExecuteScalarAsync();
        if (existing is not null)
        {
            var u = c.CreateCommand(); u.CommandText = "UPDATE activity SET count=count+1,last_seen_utc=$n,ts_utc=$n,reason=$r WHERE id=$id";
            u.Parameters.AddWithValue("$n", now.ToString("O")); u.Parameters.AddWithValue("$r", reason); u.Parameters.AddWithValue("$id", Convert.ToInt64(existing)); await u.ExecuteNonQueryAsync();
        }
        else
        {
            var i = c.CreateCommand(); i.CommandText = "INSERT INTO activity(ts_utc,device,domain,action,rule,reason,count,last_seen_utc) VALUES($t,$dev,$d,$a,$d,$r,1,$t)";
            i.Parameters.AddWithValue("$t", now.ToString("O")); i.Parameters.AddWithValue("$dev", device); i.Parameters.AddWithValue("$d", domain); i.Parameters.AddWithValue("$a", action); i.Parameters.AddWithValue("$r", reason); await i.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<Activity>> GetActivityAsync(int limit)
    {
        var l = new List<Activity>(); await using var c = C(); await c.OpenAsync(); var q = c.CreateCommand();
        q.CommandText = "SELECT ts_utc,device,domain,action,reason,count FROM activity ORDER BY id DESC LIMIT $n"; q.Parameters.AddWithValue("$n", limit);
        await using var r = await q.ExecuteReaderAsync(); while (await r.ReadAsync()) l.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt32(5))); return l;
    }
}

public sealed class DomainFilter
{
    private readonly RuleStore _store; public DomainFilter(RuleStore store) => _store = store;
    public static string Normalize(string d)
    {
        d = (d ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        if (d.Contains("://")) d = d[(d.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var slash = d.IndexOf('/'); if (slash >= 0) d = d[..slash];
        var colon = d.IndexOf(':'); if (colon >= 0) d = d[..colon];
        return d.Trim().TrimEnd('.');
    }
    public Decision Decide(string domain)
    {
        domain = Normalize(domain); var rules = _store.GetRulesAsync().GetAwaiter().GetResult().Where(r => r.Enabled && r.Scope == "global").ToList();
        Rule? best = null;
        foreach (var r in rules) if (domain == r.Domain || domain.EndsWith("." + r.Domain, StringComparison.Ordinal)) if (best == null || r.Domain.Length > best.Domain.Length) best = r;
        var action = best?.Action ?? (_store.Mode == "allowlist" ? "BLOCK" : "ALLOW"); return new(domain, action, best?.Domain, best == null ? "default" : $"matched {best.Domain}");
    }
    public async Task<Decision> DecideAndLog(string device, string domain) { var x = Decide(domain); await _store.LogAsync(device, x.Domain, x.Action, x.Reason); return x; }
}

public sealed class HotspotNetwork
{
    public HotspotStatus GetStatus()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var props = nic.GetIPProperties();
                foreach (var ua in props.UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork))
                {
                    var ip = ua.Address.ToString();
                    if (ip == "192.168.137.1")
                    {
                        var idx = props.GetIPv4Properties()?.Index ?? -1;
                        return new(true, nic.Name, nic.Description, ip, ua.PrefixLength, idx, "192.168.137.0/24");
                    }
                }
            }
            catch { }
        }
        return new(false, null, null, null, 0, -1, null);
    }
}

public record HotspotStatus(bool Detected, string? InterfaceName, string? Description, string? Gateway, int PrefixLength, int InterfaceIndex, string? Subnet);

public sealed class DnsProxy : BackgroundService
{
    private readonly DomainFilter _filter; private readonly RuleStore _store; private readonly ILogger<DnsProxy> _log; private UdpClient? _udp;
    private static readonly IPEndPoint[] Upstreams = { new(IPAddress.Parse("1.1.1.1"), 53), new(IPAddress.Parse("8.8.8.8"), 53) };
    public DnsProxy(DomainFilter filter, RuleStore store, ILogger<DnsProxy> log) { _filter = filter; _store = store; _log = log; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, _store.DnsPort));
            _log.LogInformation("Local SafeWeb DNS proxy listening on 127.0.0.1:{Port}. Windows ICS keeps port 53.", _store.DnsPort);
            while (!stoppingToken.IsCancellationRequested) { var r = await _udp.ReceiveAsync(stoppingToken); _ = Task.Run(() => Handle(r), stoppingToken); }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception e) { _log.LogError(e, "DNS proxy stopped"); }
    }
    async Task Handle(UdpReceiveResult req)
    {
        try
        {
            var name = ParseQuestion(req.Buffer, out var id, out var qEnd); var decision = await _filter.DecideAndLog("local-dns", name);
            byte[] response = decision.Action == "BLOCK" ? BuildNxDomain(req.Buffer, id, qEnd) : await ForwardAsync(req.Buffer);
            await _udp!.SendAsync(response, response.Length, req.RemoteEndPoint);
        }
        catch (Exception e) { _log.LogDebug(e, "Local DNS request error"); }
    }
    static async Task<byte[]> ForwardAsync(byte[] query)
    {
        foreach (var ep in Upstreams)
        {
            try { using var u = new UdpClient(); await u.SendAsync(query, query.Length, ep); var r = await u.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2)); return r.Buffer; } catch { }
        }
        return BuildServFail(query);
    }
    static string ParseQuestion(byte[] b, out ushort id, out int qEnd)
    {
        if (b.Length < 17) throw new InvalidDataException("DNS packet too short"); id = (ushort)((b[0] << 8) | b[1]); int p = 12; var parts = new List<string>();
        while (p < b.Length && b[p] != 0) { int n = b[p++]; if (n == 0 || n > 63 || p + n > b.Length) throw new InvalidDataException("Invalid DNS label"); parts.Add(Encoding.ASCII.GetString(b, p, n)); p += n; }
        if (p >= b.Length || p + 5 > b.Length) throw new InvalidDataException("Invalid DNS question"); p++; qEnd = p + 4; return string.Join('.', parts);
    }
    static byte[] BuildNxDomain(byte[] q, ushort id, int qEnd) { var b = new byte[qEnd]; Buffer.BlockCopy(q, 0, b, 0, qEnd); b[0] = (byte)(id >> 8); b[1] = (byte)id; b[2] = 0x81; b[3] = 0x83; b[4] = 0; b[5] = 1; for (int i = 6; i < 12; i++) b[i] = 0; return b; }
    static byte[] BuildServFail(byte[] q) { var len = Math.Min(q.Length, 512); var b = new byte[len]; Buffer.BlockCopy(q, 0, b, 0, len); b[2] = 0x81; b[3] = 0x82; return b; }
}

public sealed class WinDivertGateway : BackgroundService
{
    private readonly HotspotNetwork _net; private readonly DomainFilter _filter; private readonly RuleStore _store; private readonly ILogger<WinDivertGateway> _log;
    private IntPtr _dnsHandle = IntPtr.Zero, _webHandle = IntPtr.Zero;
    private readonly ConcurrentDictionary<FlowKey, DateTime> _blockedFlows = new();
    private readonly ConcurrentDictionary<FlowKey, StringBuilder> _tlsBuffers = new();
    public string Status { get; private set; } = "waiting";
    public bool BlockQuic { get; set; } = true;

    public WinDivertGateway(HotspotNetwork net, DomainFilter filter, RuleStore store, ILogger<WinDivertGateway> log) { _net = net; _filter = filter; _store = store; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var hs = _net.GetStatus();
            if (!hs.Detected) { Status = "waiting-for-windows-hotspot"; await Task.Delay(3000, stoppingToken); continue; }
            if (!WinDivertNative.Available)
            {
                Status = "WinDivert-missing"; _log.LogWarning("WinDivert64.sys/WinDivert.dll not found. Run scripts\\install-windivert.ps1."); await Task.Delay(5000, stoppingToken); continue;
            }
            try
            {
                StartHandles(hs.InterfaceIndex);
                Status = $"active-if{hs.InterfaceIndex}";
                _log.LogInformation("SafeWeb packet enforcement active on hotspot interface {Index} ({Name})", hs.InterfaceIndex, hs.InterfaceName);
                var t1 = Task.Run(() => ReceiveDns(stoppingToken), stoppingToken);
                var t2 = Task.Run(() => ReceiveWeb(stoppingToken), stoppingToken);
                await Task.WhenAll(t1, t2);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Status = "error"; _log.LogError(ex, "WinDivert enforcement stopped; retrying"); CloseHandles(); await Task.Delay(3000, stoppingToken);
            }
            finally { CloseHandles(); }
        }
    }

    void StartHandles(int ifIndex)
    {
        var dnsFilter = "inbound and !impostor and ip and udp and udp.DstPort == 53";
        var webFilter = "ip and !impostor and (tcp.DstPort == 443 or udp.DstPort == 443)";
        _dnsHandle = WinDivertNative.Open(dnsFilter, 0, 30000, 0);
        _webHandle = WinDivertNative.Open(webFilter, 1, 30000, 0);
        if (WinDivertNative.IsInvalid(_dnsHandle) || WinDivertNative.IsInvalid(_webHandle))
        {
            var error = Marshal.GetLastWin32Error();
            CloseHandles();
            throw new InvalidOperationException($"WinDivertOpen failed: {error}. Run SafeWeb Gateway as Administrator and make sure WinDivert64.sys is beside WinDivert.dll.");
        }
    }

    void ReceiveDns(CancellationToken token)
    {
        var packet = new byte[65535]; var addr = WinDivertAddress.Create();
        while (!token.IsCancellationRequested)
        {
            if (!WinDivertNative.Recv(_dnsHandle, packet, out var len, ref addr)) continue;
            var raw = packet.AsSpan(0, (int)len).ToArray();
            if (!PacketParser.TryParseIpv4Udp(raw, out var ip, out var udp, out var dns)) { Send(_dnsHandle, raw, addr); continue; }
            _ = Task.Run(async () => await HandleDnsPacket(raw, ip, udp, dns, addr), token);
        }
    }

    async Task HandleDnsPacket(byte[] raw, Ipv4Info ip, UdpInfo udp, ReadOnlyMemory<byte> dns, WinDivertAddress addr)
    {
        try
        {
            var name = DnsWire.ParseName(dns.Span); var device = ip.Source.ToString(); var decision = await _filter.DecideAndLog(device, name);
            byte[] response;
            if (decision.Action == "BLOCK") response = DnsWire.NxDomain(dns.ToArray());
            else response = await DnsWire.Forward(dns.ToArray());
            var packet = PacketBuilder.UdpIpv4(ip.Destination, ip.Source, 53, udp.SourcePort, response);
            addr.SetOutbound(true);
            Send(_dnsHandle, packet, addr);
        }
        catch (Exception ex) { _log.LogDebug(ex, "Hotspot DNS handling failed"); }
    }

    void ReceiveWeb(CancellationToken token)
    {
        var packet = new byte[65535]; var addr = WinDivertAddress.Create();
        while (!token.IsCancellationRequested)
        {
            if (!WinDivertNative.Recv(_webHandle, packet, out var len, ref addr)) continue;
            var raw = packet.AsSpan(0, (int)len).ToArray();
            if (PacketParser.TryParseIpv4Tcp(raw, out var ip, out var tcp, out var payload))
            {
                var key = FlowKey.From(ip.Source, ip.Destination, tcp.SourcePort, tcp.DestinationPort, 6);
                if (_blockedFlows.ContainsKey(key)) continue;
                if (ip.Source.ToString().StartsWith("192.168.137.", StringComparison.Ordinal) && payload.Length > 0)
                {
                    var buf = _tlsBuffers.GetOrAdd(key, _ => new StringBuilder());
                    lock (buf)
                    {
                        if (buf.Length < 20000) buf.Append(Encoding.Latin1.GetString(payload.Span));
                    }
                    var bytes = Encoding.Latin1.GetBytes(buf.ToString());
                    if (TlsSni.TryGetSni(bytes, out var sni))
                    {
                        var decision = _filter.DecideAndLog(ip.Source.ToString(), sni).GetAwaiter().GetResult();
                        if (decision.Action == "BLOCK")
                        {
                            _blockedFlows[key] = DateTime.UtcNow; _tlsBuffers.TryRemove(key, out _); _log.LogInformation("Blocked HTTPS SNI {Domain} from {Device}", sni, ip.Source); continue;
                        }
                        _tlsBuffers.TryRemove(key, out _);
                    }
                }
                Send(_webHandle, raw, addr);
            }
            else if (PacketParser.TryParseIpv4Udp(raw, out var uip, out var udp, out _))
            {
                if (BlockQuic && uip.Source.ToString().StartsWith("192.168.137.", StringComparison.Ordinal)) { _log.LogInformation("Blocked QUIC/UDP443 from {Device}", uip.Source); continue; }
                Send(_webHandle, raw, addr);
            }
            else Send(_webHandle, raw, addr);
        }
    }

    void Send(IntPtr handle, byte[] packet, WinDivertAddress addr)
    {
        if (!WinDivertNative.Send(handle, packet, ref addr)) _log.LogDebug("WinDivertSend failed: {Err}", Marshal.GetLastWin32Error());
    }
    void CloseHandles()
    {
        if (!WinDivertNative.IsInvalid(_dnsHandle)) { WinDivertNative.Close(_dnsHandle); _dnsHandle = IntPtr.Zero; }
        if (!WinDivertNative.IsInvalid(_webHandle)) { WinDivertNative.Close(_webHandle); _webHandle = IntPtr.Zero; }
        _blockedFlows.Clear(); _tlsBuffers.Clear();
    }
}

public readonly record struct FlowKey(IPAddress A, IPAddress B, ushort AP, ushort BP, byte Protocol)
{
    public static FlowKey From(IPAddress a, IPAddress b, ushort ap, ushort bp, byte protocol)
    {
        var sa = a.ToString(); var sb = b.ToString(); return string.CompareOrdinal(sa + ":" + ap, sb + ":" + bp) <= 0 ? new(a, b, ap, bp, protocol) : new(b, a, bp, ap, protocol);
    }
}

public static class TlsSni
{
    public static bool TryGetSni(byte[] data, out string sni)
    {
        sni = ""; if (data.Length < 5 || data[0] != 22) return false; int p = 5;
        while (p + 4 <= data.Length) { if (data[p] != 1) return false; int len = (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3]; if (p + 4 + len > data.Length) return false; var end = p + 4 + len; p += 4; if (p + 2 + 32 > end) return false; p += 2 + 32;
            if (p >= end) return false; int sidLen = data[p++]; p += sidLen; if (p + 2 > end) return false; int csLen = (data[p] << 8) | data[p + 1]; p += 2 + csLen; if (p >= end) return false; int compLen = data[p++]; p += compLen; if (p + 2 > end) return false; int extLen = (data[p] << 8) | data[p + 1]; p += 2; int extEnd = Math.Min(end, p + extLen);
            while (p + 4 <= extEnd) { int type = (data[p] << 8) | data[p + 1]; int elen = (data[p + 2] << 8) | data[p + 3]; p += 4; if (p + elen > extEnd) return false; if (type == 0 && elen >= 5) { int listLen = (data[p] << 8) | data[p + 1]; int q = p + 2; int listEnd = Math.Min(p + 2 + listLen, p + elen); while (q + 3 <= listEnd) { int nt = data[q++]; int nl = (data[q] << 8) | data[q + 1]; q += 2; if (q + nl > listEnd) return false; if (nt == 0) { sni = Encoding.ASCII.GetString(data, q, nl); return DomainFilter.Normalize(sni) == sni; } q += nl; } } p += elen; }
            return false;
        }
        return false;
    }
}

public static class DnsWire
{
    static readonly IPEndPoint[] Upstreams = { new(IPAddress.Parse("1.1.1.1"), 53), new(IPAddress.Parse("8.8.8.8"), 53) };
    public static string ParseName(ReadOnlySpan<byte> b) { if (b.Length < 13) throw new InvalidDataException(); int p = 12; var parts = new List<string>(); while (p < b.Length && b[p] != 0) { int n = b[p++]; if (n < 1 || n > 63 || p + n > b.Length) throw new InvalidDataException(); parts.Add(Encoding.ASCII.GetString(b.Slice(p, n))); p += n; } return DomainFilter.Normalize(string.Join('.', parts)); }
    public static async Task<byte[]> Forward(byte[] query) { foreach (var ep in Upstreams) { try { using var u = new UdpClient(); await u.SendAsync(query, query.Length, ep); return (await u.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer; } catch { } } return NxDomain(query, true); }
    public static byte[] NxDomain(byte[] q, bool servfail = false) { var b = new byte[q.Length]; Buffer.BlockCopy(q, 0, b, 0, q.Length); b[2] = 0x81; b[3] = (byte)(servfail ? 0x82 : 0x83); if (b.Length >= 12) { b[4] = 0; b[5] = 1; for (int i = 6; i < 12; i++) b[i] = 0; } return b; }
}

public readonly record struct Ipv4Info(IPAddress Source, IPAddress Destination);
public readonly record struct UdpInfo(ushort SourcePort, ushort DestinationPort, int HeaderOffset, int PayloadOffset);
public readonly record struct TcpInfo(ushort SourcePort, ushort DestinationPort, int HeaderOffset, int PayloadOffset);

public static class PacketParser
{
    public static bool TryParseIpv4Udp(byte[] p, out Ipv4Info ip, out UdpInfo udp, out ReadOnlyMemory<byte> payload)
    {
        ip = default; udp = default; payload = default; if (p.Length < 28 || (p[0] >> 4) != 4 || p[9] != 17) return false; int ihl = (p[0] & 15) * 4; if (ihl < 20 || p.Length < ihl + 8) return false;
        ip = new(new IPAddress(p.AsSpan(12, 4)), new IPAddress(p.AsSpan(16, 4))); ushort sp = (ushort)((p[ihl] << 8) | p[ihl + 1]); ushort dp = (ushort)((p[ihl + 2] << 8) | p[ihl + 3]); udp = new(sp, dp, ihl, ihl + 8); payload = p.AsMemory(ihl + 8); return true;
    }
    public static bool TryParseIpv4Tcp(byte[] p, out Ipv4Info ip, out TcpInfo tcp, out ReadOnlyMemory<byte> payload)
    {
        ip = default; tcp = default; payload = default; if (p.Length < 40 || (p[0] >> 4) != 4 || p[9] != 6) return false; int ihl = (p[0] & 15) * 4; if (ihl < 20 || p.Length < ihl + 20) return false;
        int t = ihl; ushort sp = (ushort)((p[t] << 8) | p[t + 1]); ushort dp = (ushort)((p[t + 2] << 8) | p[t + 3]); int thl = ((p[t + 12] >> 4) & 15) * 4; if (thl < 20 || p.Length < t + thl) return false;
        ip = new(new IPAddress(p.AsSpan(12, 4)), new IPAddress(p.AsSpan(16, 4))); tcp = new(sp, dp, t, t + thl); payload = p.AsMemory(t + thl); return true;
    }
}

public static class PacketBuilder
{
    public static byte[] UdpIpv4(IPAddress src, IPAddress dst, ushort srcPort, ushort dstPort, byte[] payload)
    {
        var p = new byte[20 + 8 + payload.Length]; p[0] = 0x45; p[8] = 64; p[9] = 17; Write16(p, 2, (ushort)p.Length); Buffer.BlockCopy(src.GetAddressBytes(), 0, p, 12, 4); Buffer.BlockCopy(dst.GetAddressBytes(), 0, p, 16, 4); Write16(p, 20, srcPort); Write16(p, 22, dstPort); Write16(p, 24, (ushort)(8 + payload.Length)); Buffer.BlockCopy(payload, 0, p, 28, payload.Length); Write16(p, 10, Checksum(p, 0, 20)); Write16(p, 26, UdpChecksum(p)); return p;
    }
    static void Write16(byte[] b, int o, ushort v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
    static ushort Checksum(byte[] b, int off, int len) { uint s = 0; for (int i = off; i < off + len; i += 2) s += (uint)((b[i] << 8) | (i + 1 < off + len ? b[i + 1] : 0)); while ((s >> 16) != 0) s = (s & 0xffff) + (s >> 16); return (ushort)~s; }
    static ushort UdpChecksum(byte[] b) { uint s = 0; for (int i = 12; i < 20; i += 2) s += (uint)((b[i] << 8) | b[i + 1]); s += 17; s += (uint)(b.Length - 20); for (int i = 20; i < b.Length; i += 2) s += (uint)((b[i] << 8) | (i + 1 < b.Length ? b[i + 1] : 0)); while ((s >> 16) != 0) s = (s & 0xffff) + (s >> 16); var x = (ushort)~s; return x == 0 ? (ushort)0xffff : x; }
}

public static class WinDivertNative
{
    public static bool Available => NativeLibrary.TryLoad("WinDivert.dll", out var h) && Release(h);
    static bool Release(IntPtr h) { NativeLibrary.Free(h); return true; }
    [DllImport("WinDivert.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)] static extern IntPtr WinDivertOpen([MarshalAs(UnmanagedType.LPStr)] string filter, int layer, short priority, ulong flags);
    [DllImport("WinDivert.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)] static extern bool WinDivertRecv(IntPtr handle, byte[] packet, uint packetLen, out uint recvLen, ref WinDivertAddress addr);
    [DllImport("WinDivert.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)] static extern bool WinDivertSend(IntPtr handle, byte[] packet, uint packetLen, out uint sendLen, ref WinDivertAddress addr);
    [DllImport("WinDivert.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)] static extern bool WinDivertClose(IntPtr handle);
    public static IntPtr Open(string filter, int layer, short priority, ulong flags) => WinDivertOpen(filter, layer, priority, flags);
    public static bool IsInvalid(IntPtr h) => h == IntPtr.Zero || h == new IntPtr(-1);
    public static bool Recv(IntPtr h, byte[] p, out uint len, ref WinDivertAddress a) => WinDivertRecv(h, p, (uint)p.Length, out len, ref a);
    public static bool Send(IntPtr h, byte[] p, ref WinDivertAddress a) => WinDivertSend(h, p, (uint)p.Length, out _, ref a);
    public static void Close(IntPtr h) { if (!IsInvalid(h)) WinDivertClose(h); }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WinDivertAddress
{
    // WINDIVERT_ADDRESS is 80 bytes on WinDivert 2.2:
    // INT64 Timestamp + UINT64 bit-field flags + 64-byte layer-specific union.
    public long Timestamp;
    public ulong Flags;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
    public byte[] Data;

    public static WinDivertAddress Create() => new() { Data = new byte[64] };

    // In the WinDivert 2.2 bit-field, Layer=bits 0-7, Event=8-15,
    // Sniffed=16, Outbound=17, Loopback=18, Impostor=19, ...
    public bool Outbound => (Flags & (1UL << 17)) != 0;
    public void SetOutbound(bool value)
    {
        if (value) Flags |= 1UL << 17;
        else Flags &= ~(1UL << 17);
    }
}
