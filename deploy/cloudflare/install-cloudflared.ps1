# Download cloudflared for Windows amd64 into deploy/cloudflare/

$ErrorActionPreference = "Stop"
$dest = Join-Path $PSScriptRoot "cloudflared.exe"
$url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"

Write-Host "[Cloudflare] Downloading cloudflared..."
Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
& $dest version
Write-Host "[Cloudflare] Installed: $dest"
