$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run as Administrator.' }
$exe = Join-Path $root 'src\SafeWebGateway\bin\Release\net8.0-windows\SafeWebGateway.exe'
if (-not (Test-Path $exe)) { throw 'Build first with .\scripts\build.ps1' }
& sc.exe stop 'SafeWeb Gateway' 2>$null | Out-Null
& sc.exe delete 'SafeWeb Gateway' 2>$null | Out-Null
& sc.exe create 'SafeWeb Gateway' binPath= "`"$exe`"" start= auto DisplayName= "SafeWeb Gateway" | Out-Null
& sc.exe start 'SafeWeb Gateway' | Out-Null
Write-Host 'SafeWeb Gateway service installed and started.'
