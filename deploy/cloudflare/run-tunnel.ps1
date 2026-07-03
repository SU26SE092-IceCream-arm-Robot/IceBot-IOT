# Run Cloudflare Tunnel (foreground). For production, install as Windows Service instead.

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cf = & (Join-Path $scriptDir "get-cloudflared.ps1")
$config = Join-Path $scriptDir "config.yml"

if (-not (Test-Path $config)) {
    Write-Error "Missing $config — run setup-tunnel.ps1 first"
}

Write-Host "[Cloudflare] Starting tunnel with $config"
Write-Host "[Cloudflare] Ensure IceBot is running: IceBot.exe serve"
& $cf tunnel --config $config run
