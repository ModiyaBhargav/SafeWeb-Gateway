$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$native = Join-Path $root 'native'
$dll = Join-Path $native 'WinDivert.dll'
$sys = Join-Path $native 'WinDivert64.sys'
if (-not (Test-Path $dll) -or -not (Test-Path $sys)) {
    throw "WinDivert runtime is missing. Run .\scripts\install-windivert.ps1 first."
}

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SafeWebWinDivertTest {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool SetDllDirectory(string path);
    [DllImport("WinDivert.dll", CallingConvention=CallingConvention.Cdecl, SetLastError=true)]
    public static extern IntPtr WinDivertOpen(string filter, int layer, short priority, ulong flags);
    [DllImport("WinDivert.dll", CallingConvention=CallingConvention.Cdecl, SetLastError=true)]
    public static extern bool WinDivertClose(IntPtr handle);
}
'@

if (-not [SafeWebWinDivertTest]::SetDllDirectory($native)) { throw 'Could not set native DLL search path.' }

$filters = @(
    @{ Name='DNS inbound to ICS'; Filter='inbound and !impostor and ip and udp and udp.DstPort == 53'; Layer=0 },
    @{ Name='Forwarded HTTPS/QUIC'; Filter='ip and !impostor and (tcp.DstPort == 443 or udp.DstPort == 443)'; Layer=1 }
)

Write-Host '=== SafeWeb WinDivert filter diagnostics ==='
foreach ($x in $filters) {
    $h = [SafeWebWinDivertTest]::WinDivertOpen($x.Filter, $x.Layer, 30000, 1)
    if ($h -eq [IntPtr]::Zero -or $h -eq [IntPtr]::new(-1)) {
        $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        Write-Host "FAIL: $($x.Name) -> WinDivertOpen error $err" -ForegroundColor Red
    } else {
        [void][SafeWebWinDivertTest]::WinDivertClose($h)
        Write-Host "PASS: $($x.Name)" -ForegroundColor Green
        Write-Host "      $($x.Filter)"
    }
}
