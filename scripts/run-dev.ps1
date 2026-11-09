[CmdletBinding()]
param(
    [switch]$SkipWinDivertInstall,
    [switch]$SkipBuild,
    [switch]$SkipWinDivertTest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Test-Administrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Relaunch elevated automatically so the user does not need to open an
# Administrator PowerShell or change the execution policy manually.
if (-not (Test-Administrator)) {
    $elevatedArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
    if ($SkipWinDivertInstall) { $elevatedArgs += '-SkipWinDivertInstall' }
    if ($SkipBuild) { $elevatedArgs += '-SkipBuild' }
    if ($SkipWinDivertTest) { $elevatedArgs += '-SkipWinDivertTest' }
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $elevatedArgs
    exit 0
}

Set-Location $root

$native = Join-Path $root 'native'
$dll = Join-Path $native 'WinDivert.dll'
$sys = Join-Path $native 'WinDivert64.sys'

Write-Host ''
Write-Host '========================================='
Write-Host ' SafeWeb Gateway - Local Development' -ForegroundColor Cyan
Write-Host '========================================='
Write-Host "Project: $root"
Write-Host ''

# Install WinDivert only once. Later launches reuse the files already installed.
if (-not (Test-Path $dll) -or -not (Test-Path $sys)) {
    if ($SkipWinDivertInstall) {
        throw 'WinDivert runtime is missing and -SkipWinDivertInstall was specified.'
    }
    Write-Host '[1/4] WinDivert runtime not found. Installing it now...' -ForegroundColor Yellow
    & (Join-Path $root 'scripts\install-windivert.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'WinDivert installation failed.' }
} else {
    Write-Host '[1/4] WinDivert runtime: already installed.' -ForegroundColor Green
}

if (-not $SkipBuild) {
    Write-Host '[2/4] Building SafeWeb Gateway...' -ForegroundColor Cyan
    & (Join-Path $root 'scripts\build.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'SafeWeb build failed.' }
} else {
    Write-Host '[2/4] Build: skipped.' -ForegroundColor Yellow
}

if (-not $SkipWinDivertTest) {
    Write-Host '[3/4] Testing WinDivert filters...' -ForegroundColor Cyan
    & (Join-Path $root 'scripts\test-windivert.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'WinDivert diagnostics failed.' }
} else {
    Write-Host '[3/4] WinDivert test: skipped.' -ForegroundColor Yellow
}

$exe = Join-Path $root 'src\SafeWebGateway\bin\Release\net8.0-windows\SafeWebGateway.exe'
if (-not (Test-Path $exe)) {
    throw "SafeWeb executable was not produced: $exe"
}

Write-Host '[4/4] Starting SafeWeb Gateway...' -ForegroundColor Cyan
Write-Host "Dashboard: http://127.0.0.1:8787" -ForegroundColor Green
Write-Host 'Press Ctrl+C to stop SafeWeb.' -ForegroundColor DarkGray
Write-Host ''

# Run the compiled x64 executable directly. This avoids a second build and
# avoids asking the user to type a long dotnet command every time.
& $exe
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host ''
    Write-Host "SafeWeb Gateway exited with code $exitCode." -ForegroundColor Red
    Write-Host 'If Windows reports 0x800711C7 / Application Control blocked this file,' -ForegroundColor Yellow
    Write-Host 'the Windows App Control policy must authorize this development build.' -ForegroundColor Yellow
}

exit $exitCode
