$ErrorActionPreference = 'SilentlyContinue'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run as Administrator.' }
sc.exe stop 'SafeWeb Gateway' | Out-Null
sc.exe delete 'SafeWeb Gateway' | Out-Null
Write-Host 'SafeWeb Gateway service removed.'
