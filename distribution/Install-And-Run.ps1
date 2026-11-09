$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$app = Join-Path $root 'app'
$exe = Join-Path $app 'SafeWebGateway.exe'
$nativeDll = Join-Path $app 'WinDivert.dll'
$driver = Join-Path $app 'WinDivert64.sys'
$windivertArchive = Join-Path $root 'WinDivert-2.2.2-A.zip'
$expectedArchiveSha256 = '63cb41763bb4b20f600b6de04e991a9c2be73279e317d4d82f237b150c5f3f15'
$officialReleaseUrl = 'https://github.com/basil00/WinDivert/releases/tag/v2.2.2'

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
    if (-not (Test-Path $windivertArchive)) {
        throw @"
WinDivert is not installed. Automatic downloading is intentionally disabled while the
official 2.2.2-A archive has an unresolved Microsoft Defender report.
Do not disable Defender, restore a quarantined file, or add an exclusion.
Only continue after the upstream/Microsoft review is resolved. If you have an independently
verified archive, place it here:
$windivertArchive
Official release information: $officialReleaseUrl
Expected SHA-256: $expectedArchiveSha256
"@
    }

    $actualArchiveSha256 = (Get-FileHash -LiteralPath $windivertArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualArchiveSha256 -ne $expectedArchiveSha256) {
        throw "WinDivert archive SHA-256 mismatch. Expected $expectedArchiveSha256; got $actualArchiveSha256. No files were extracted."
    }

    $extract = Join-Path $env:TEMP ('SafeWeb-WinDivert-' + [guid]::NewGuid().ToString('N'))
    try {
        Expand-Archive -LiteralPath $windivertArchive -DestinationPath $extract -Force
        $dll = Get-ChildItem -LiteralPath $extract -Filter 'WinDivert.dll' -File -Recurse | Select-Object -First 1
        if (-not $dll) { throw 'WinDivert.dll was not found in the verified archive.' }
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
        Write-Host "Verified archive SHA-256 and driver signer: $($signature.SignerCertificate.Subject)"
    }
    finally {
        if (Test-Path $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
    }
}

# Always validate the runtime files, including files that were already present.
if (-not (Test-Path -LiteralPath $nativeDll -PathType Leaf)) {
    throw "WinDivert.dll is missing: $nativeDll"
}
if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) {
    throw "WinDivert64.sys is missing: $driver"
}

$existingDriverSignature = Get-AuthenticodeSignature -FilePath $driver
if ($existingDriverSignature.Status -ne 'Valid') {
    throw "WinDivert64.sys signature validation failed (status: $($existingDriverSignature.Status)). SafeWeb will not start."
}
Write-Host "WinDivert driver signature verified: $($existingDriverSignature.SignerCertificate.Subject)"

Set-Location $app
Write-Host 'Starting SafeWeb Gateway...'
$process = Start-Process -FilePath $exe -WorkingDirectory $app -PassThru
Start-Sleep -Seconds 3
if ($process.HasExited) { throw "SafeWeb exited during startup with code $($process.ExitCode)." }
Start-Process 'http://127.0.0.1:8787'
Write-Host 'SafeWeb is running. Dashboard: http://127.0.0.1:8787'
Write-Host 'Keep this package folder intact. Close SafeWeb using its process window or Task Manager.'
