$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$app = Join-Path $root 'app'
$exe = Join-Path $app 'SafeWebGateway.exe'
$nativeDll = Join-Path $app 'WinDivert.dll'
$driver = Join-Path $app 'WinDivert64.sys'
$downloadUrl = 'https://github.com/basil00/WinDivert/releases/download/v2.2.2/WinDivert-2.2.2-A.zip'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $command = "& '$($PSCommandPath.Replace("'", "''"))'"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    Start-Process -FilePath 'powershell.exe' -ArgumentList "-NoProfile -EncodedCommand $encoded" -Verb RunAs
    exit
}

if (-not (Test-Path $exe)) { throw "SafeWeb executable not found: $exe" }
if (-not [Environment]::Is64BitOperatingSystem) { throw 'SafeWeb requires 64-bit Windows.' }

$runtimes = & dotnet --list-runtimes 2>$null
if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.NETCore.App 8\.') -or -not ($runtimes -match '^Microsoft.AspNetCore.App 8\.')) {
    Write-Host 'SafeWeb requires the .NET 8 and ASP.NET Core 8 x64 runtimes.' -ForegroundColor Yellow
    Write-Host 'Install the ASP.NET Core Runtime x64 from https://dotnet.microsoft.com/en-us/download/dotnet/8.0'
    throw 'Required .NET 8 runtime components are missing.'
}

if (-not (Test-Path $nativeDll) -or -not (Test-Path $driver)) {
    $zip = Join-Path $env:TEMP 'SafeWeb-WinDivert-2.2.2-A.zip'
    $extract = Join-Path $env:TEMP ('SafeWeb-WinDivert-' + [guid]::NewGuid().ToString('N'))
    Write-Host 'Downloading the official WinDivert 2.2.2-A x64 package...'
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $dll = Get-ChildItem -LiteralPath $extract -Filter 'WinDivert.dll' -File -Recurse | Select-Object -First 1
    if (-not $dll) { throw 'WinDivert.dll was not found in the official package.' }
    $sys = Join-Path $dll.DirectoryName 'WinDivert64.sys'
    if (-not (Test-Path $sys)) { throw 'WinDivert64.sys was not found beside WinDivert.dll.' }
    $signature = Get-AuthenticodeSignature -FilePath $sys
    if ($signature.Status -ne 'Valid') {
        throw "WinDivert driver signature is not valid (status: $($signature.Status)). The driver was not installed."
    }
    Copy-Item -LiteralPath $dll.FullName -Destination $nativeDll -Force
    Copy-Item -LiteralPath $sys -Destination $driver -Force
    $license = Get-ChildItem -LiteralPath $extract -Filter 'LICENSE*' -File -Recurse | Select-Object -First 1
    if ($license) { Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $root 'WinDivert-LICENSE.txt') -Force }
    Remove-Item -LiteralPath $extract -Recurse -Force
    Write-Host "Verified WinDivert driver signer: $($signature.SignerCertificate.Subject)"
}

Set-Location $app
Write-Host 'Starting SafeWeb Gateway...'
$process = Start-Process -FilePath $exe -WorkingDirectory $app -PassThru
Start-Sleep -Seconds 3
if ($process.HasExited) { throw "SafeWeb exited during startup with code $($process.ExitCode)." }
Start-Process 'http://127.0.0.1:8787'
Write-Host 'SafeWeb is running. Dashboard: http://127.0.0.1:8787'
Write-Host 'Keep this package folder intact. Close SafeWeb using its process window or Task Manager.'
