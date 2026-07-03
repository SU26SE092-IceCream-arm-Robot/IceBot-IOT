# Returns path to cloudflared executable (bundled or PATH).

$ErrorActionPreference = "Stop"
$bundled = Join-Path $PSScriptRoot "cloudflared.exe"
if (Test-Path $bundled) {
    return $bundled
}

$cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
if ($cmd) {
    return $cmd.Path
}

throw "cloudflared not found. Run: deploy/cloudflare/install-cloudflared.ps1"
