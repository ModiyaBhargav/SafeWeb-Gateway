$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$native = Join-Path $root 'native'
New-Item -ItemType Directory -Force $native | Out-Null
$url = 'https://github.com/basil00/WinDivert/releases/download/v2.2.2/WinDivert-2.2.2-A.zip'
$tmp = Join-Path $env:TEMP 'WinDivert-2.2.2-A.zip'
Write-Host 'Downloading official WinDivert 2.2.2-A x64 package...'
Invoke-WebRequest -Uri $url -OutFile $tmp
$extract = Join-Path $env:TEMP 'SafeWeb-WinDivert'
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $tmp -DestinationPath $extract -Force
$pkg = Get-ChildItem $extract -Directory -Recurse | Where-Object { Test-Path (Join-Path $_.FullName 'WinDivert.dll') } | Select-Object -First 1
if (-not $pkg) { throw 'Could not find WinDivert x64 package after extraction.' }
Copy-Item (Join-Path $pkg.FullName 'WinDivert.dll') $native -Force
Copy-Item (Join-Path $pkg.FullName 'WinDivert64.sys') $native -Force
if (Test-Path (Join-Path $pkg.FullName '..\LICENSE')) { Copy-Item (Join-Path $pkg.FullName '..\LICENSE') (Join-Path $native 'WinDivert-LICENSE.txt') -Force }
Write-Host "Installed WinDivert runtime files into $native"
Write-Host 'Run build.ps1 after this script.'
