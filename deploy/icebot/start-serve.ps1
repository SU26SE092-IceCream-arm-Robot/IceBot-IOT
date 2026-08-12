# Start IceBot in visible serve mode. Keep this window open to see status and logs.

$ErrorActionPreference = "Stop"
$exe = Join-Path $PSScriptRoot "..\..\code\src\IceBot\bin\Release\net472\IceBot.exe"

if (-not (Test-Path $exe)) {
    Write-Host "Building IceBot..."
    dotnet build (Join-Path $PSScriptRoot "..\..\code\IceBot-IOT.sln") -c Release
}

# Optional env vars — set before run or in System Environment
# $env:ICEBOT_NETBIRD_SETUP_KEY = "your-netbird-setup-key"
# $env:ICEBOT_PUBLIC_URL = "https://ice-shop.example.com"
# $env:ICEBOT_API_KEY = "your-shared-secret"

& $exe serve
