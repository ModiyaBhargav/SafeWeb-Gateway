$ErrorActionPreference = 'Stop'
Write-Host '=== SafeWeb Hotspot diagnostics ==='
Get-NetIPAddress -AddressFamily IPv4 |
  Where-Object { $_.IPAddress -eq '192.168.137.1' } |
  Format-Table IPAddress,PrefixLength,InterfaceIndex,InterfaceAlias -AutoSize
$ip = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -eq '192.168.137.1' } | Select-Object -First 1
if ($ip) {
  Write-Host "`n=== Adapter ==="
  Get-NetAdapter -InterfaceIndex $ip.InterfaceIndex | Format-Table Name,InterfaceDescription,Status,MacAddress -AutoSize
  Write-Host "`n=== Hotspot neighbors ==="
  Get-NetNeighbor -InterfaceIndex $ip.InterfaceIndex -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -like '192.168.137.*' } |
    Format-Table IPAddress,LinkLayerAddress,State -AutoSize
}
Write-Host "`n=== Port 53 owner ==="
netstat -ano | findstr ':53'
