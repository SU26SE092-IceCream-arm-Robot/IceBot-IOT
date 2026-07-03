# Install Cloudflare Tunnel as Windows Service (runs on boot).

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cf = & (Join-Path $scriptDir "get-cloudflared.ps1")
$config = Join-Path $scriptDir "config.yml"

if (-not (Test-Path $config)) {
    Write-Error "Missing $config — run setup-tunnel.ps1 first"
}

& $cf service install --config $config
Write-Host "[Cloudflare] Service installed. Start with: sc start cloudflared"
Write-Host "[Cloudflare] Or: net start cloudflared"
