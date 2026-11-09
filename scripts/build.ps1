$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK not found. Install .NET 8 SDK first.'
}

if (-not (Test-Path .\native\WinDivert.dll) -or -not (Test-Path .\native\WinDivert64.sys)) {
    Write-Warning 'WinDivert runtime files are missing. The launcher will install them automatically on the first run.'
}

$project = '.\src\SafeWebGateway\SafeWebGateway.csproj'
$assets = '.\src\SafeWebGateway\obj\project.assets.json'

if (-not (Test-Path $assets)) {
    Write-Host 'Restoring NuGet packages...'
    dotnet restore $project
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
} else {
    Write-Host 'Restore: already available.' -ForegroundColor DarkGray
}

Write-Host 'Building x64 Release...'
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

Write-Host 'Build succeeded.' -ForegroundColor Green
