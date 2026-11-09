# SafeWeb Gateway Local v4

A local-only Windows Mobile Hotspot filtering gateway. **No Netlify, no Supabase, no browser extension.**

## What changed in v4

- Keeps Windows Internet Connection Sharing (ICS) / Mobile Hotspot untouched. It does **not** try to bind port 53.
- Detects the Windows hotspot gateway `192.168.137.1` and its interface index automatically.
- Adds WinDivert packet interception around ICS.
- Transparently intercepts hotspot-client IPv4 UDP DNS requests sent to port 53.
- Applies the same local blocklist/allowlist rules to DNS and logs client IPs.
- For allowed DNS, forwards the query to 1.1.1.1 or 8.8.8.8 and sends the response back to the client.
- Inspects IPv4 TCP 443 ClientHello traffic for TLS SNI and blocks matching domains without a browser extension.
- Blocks UDP 443/QUIC by default so browsers fall back to TCP where SNI can be inspected. This can be disabled in the dashboard/API.
- Keeps the local SQLite database and localhost dashboard.
- Runs x64 and requires Administrator because WinDivert uses a signed Windows packet-filter driver.

WinDivert provides user-mode packet capture/drop/re-injection and a forwarded-packet layer; its official documentation also describes IPv6 support and the LGPLv3 licensing. This project currently uses the IPv4 enforcement path first. WinDivert documentation: https://github.com/basil00/WinDivert/blob/master/doc/windivert.html

## IMPORTANT

Do **not** stop `SharedAccess`, disable Windows Mobile Hotspot, or manually change the ICS DNS port. Windows ICS owns UDP/TCP 53 on the hotspot gateway. SafeWeb now sits in front of that path using WinDivert.

This package is an enforcement test build. IPv4 DNS and IPv4 HTTPS SNI are the first transparent interception paths. IPv6, TCP DNS, encrypted client hello (ECH), arbitrary DoH endpoints, and applications that use direct IPs require additional policy layers before calling this a complete enterprise-grade filter.

## Install / run locally

For normal local development, you no longer need to manually change the execution policy, install WinDivert, build, run the WinDivert test, and type the long `dotnet` command.

### One-click start

Double-click:

```text
Start-SafeWeb.cmd
```

The launcher automatically starts elevated when required, uses a process-only PowerShell execution-policy bypass, installs the official WinDivert runtime only when it is missing, builds the x64 Release build, runs the WinDivert diagnostics, and starts the compiled `SafeWebGateway.exe`.

The dashboard is available at `http://127.0.0.1:8787`.

### PowerShell start

You can also run:

```powershell
.\scripts\run-dev.ps1
```

The script elevates itself automatically if necessary.

WinDivert and the NuGet restore are cached locally, so they are not downloaded/restored on every launch unless the required files are missing.

## Hotspot diagnostic

Run:

```powershell
.\scripts\test-hotspot.ps1
```

It should find `192.168.137.1`, show the hotspot adapter index, list neighbors, and show that Windows `SharedAccess` owns port 53.

## First real test

Add a block rule:

```powershell
Invoke-RestMethod http://127.0.0.1:8787/api/rules -Method Post -ContentType 'application/json' -Body '{"domain":"instagram.com","action":"BLOCK"}'
```

Then use a phone connected to the Windows hotspot and browse to `https://instagram.com`.

Expected behavior:

- the phone remains connected to the hotspot;
- DNS for `instagram.com` is intercepted locally and blocked;
- if the browser already knows an IP, the IPv4 TCP ClientHello SNI path should still block the HTTPS connection;
- activity appears in the SafeWeb dashboard with the client IP.

Remove the rule after the test if required:

```powershell
Invoke-RestMethod http://127.0.0.1:8787/api/rules | ConvertTo-Json
Invoke-RestMethod http://127.0.0.1:8787/api/activity | ConvertTo-Json
```

## Architecture

```text
Internet
   |
Laptop Wi-Fi/Ethernet
   |
Windows Mobile Hotspot / ICS
   |
192.168.137.0/24
   |
+-----------------------------+
| WinDivert on laptop         |
|   |                         |
|   +-- UDP/53 -> SafeWeb DNS |
|   +-- TCP/443 -> SNI rules  |
|   +-- UDP/443 -> QUIC block |
+-----------------------------+
   |
Phone / tablet / other client
```

Port 53 remains owned by Windows ICS. SafeWeb does not replace DHCP or the ICS gateway.

## Service

After successful testing:

```powershell
.\scripts\install-service.ps1
```

Remove it with:

```powershell
.\scripts\uninstall-service.ps1
```

## Security / limitations

- WinDivert is a kernel-assisted packet interception component; only use the official signed package and verify its license/source before redistribution.
- Current enforcement filters IPv4 hotspot traffic. IPv6 traffic is not yet transparently filtered by this v4 build, so IPv6 must be disabled on the hotspot path or handled by a later IPv6 layer before claiming full bypass resistance.
- DNS-over-TLS (853), DNS-over-HTTPS, ECH, VPNs, and direct-IP applications can bypass hostname-only filtering unless separate policies are added.
- Blocking UDP 443 is a compatibility tradeoff. It is enabled by default because it makes normal browser HTTPS SNI inspection much more reliable.
- Fail-closed protection is not enabled by default. If WinDivert fails, Windows ICS continues to provide normal Internet access rather than being intentionally broken.


## v4.2 build correction

This release corrects the WinDivert 2.2 `WINDIVERT_ADDRESS` bit-field mapping and invalid-handle detection. The WinDivert outbound flag is bit 17 in the 64-bit flag field; using bit 9 would produce incorrect reinjection metadata. The application also treats `INVALID_HANDLE_VALUE` correctly.

## v4.3 fixes
- Uses WinDivert-supported filter syntax (`ip`, `udp.DstPort`, `tcp.DstPort`) instead of the invalid `ifIdx`/`ipv4` aliases.
- Hotspot interface filtering is performed in user mode from the detected `192.168.137.0/24` subnet.
- The local diagnostic DNS proxy moved to `127.0.0.1:55353` so it does not collide with Windows/Bonjour/mDNS listeners on 5353.
- ASP.NET service dependencies are explicitly marked with `[FromServices]` to avoid Minimal API body inference errors.
- Added `scripts/test-windivert.ps1` to validate the exact filters before starting the gateway.
