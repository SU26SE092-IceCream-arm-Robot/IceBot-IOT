# Cloudflare Tunnel setup (run once). Requires browser login to Cloudflare account.

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cf = & (Join-Path $scriptDir "get-cloudflared.ps1")
$cfDir = "C:\cloudflared"
$tunnelName = "icebot"
$configDest = Join-Path $scriptDir "config.yml"
$publicHost = "ice-shop-01.duckdns.org"

if (-not (Test-Path $cfDir)) {
    New-Item -ItemType Directory -Path $cfDir | Out-Null
}

$certPath = Join-Path $env:USERPROFILE ".cloudflared\cert.pem"
if (-not (Test-Path $certPath)) {
    Write-Host "[Cloudflare] Open the URL in browser if prompted, then authorize..."
    & $cf tunnel login
}

Write-Host "[Cloudflare] Creating tunnel '$tunnelName'..."
& $cf tunnel create $tunnelName 2>&1 | ForEach-Object { Write-Host $_ }

$tunnels = & $cf tunnel list --output json | ConvertFrom-Json
$tunnel = $tunnels | Where-Object { $_.name -eq $tunnelName } | Select-Object -First 1
if (-not $tunnel) {
    Write-Error "Tunnel '$tunnelName' not found after create."
}

$tunnelId = $tunnel.id
$credSource = Join-Path $env:USERPROFILE ".cloudflared\$tunnelId.json"
$credDest = Join-Path $cfDir "$tunnelId.json"
if (-not (Test-Path $credSource)) {
    Write-Error "Missing credentials: $credSource"
}
Copy-Item $credSource $credDest -Force

$config = @"
tunnel: $tunnelId
credentials-file: $credDest

ingress:
  - hostname: $publicHost
    service: http://localhost:5080
  - service: http_status:404
"@

Set-Content -Path $configDest -Value $config -Encoding UTF8
Write-Host "[Cloudflare] Wrote $configDest (tunnel id: $tunnelId)"
Write-Host ""
Write-Host "IMPORTANT: '$publicHost' must be a hostname in your Cloudflare account."
Write-Host "If you only use DuckDNS, add a domain to Cloudflare OR configure Public Hostname in Zero Trust dashboard."
Write-Host ""
Write-Host "Optional DNS route (domain on Cloudflare):"
Write-Host "  $cf tunnel route dns $tunnelName $publicHost"
Write-Host ""
Write-Host "Start tunnel: .\run-tunnel.ps1"
Write-Host "Start IceBot: ..\icebot\start-serve.ps1"
