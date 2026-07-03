# Updates DuckDNS with the current public IP of this robot controller.
# Schedule via Windows Task Scheduler every 5 minutes.

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$envFile = Join-Path $scriptDir "duckdns.env"
if (-not (Test-Path $envFile)) {
    $appConfig = Join-Path (Split-Path -Parent (Split-Path -Parent $scriptDir)) "code\src\IceBot\bin\Release\net472\config\duckdns.env"
    if (Test-Path $appConfig) { $envFile = $appConfig }
}

if (-not (Test-Path $envFile)) {
    Write-Error "Missing $envFile — copy duckdns.env.example to duckdns.env"
}

Get-Content $envFile | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
        Set-Variable -Name $matches[1].Trim() -Value $matches[2].Trim() -Scope Script
    }
}

if (-not $DUCKDNS_SUBDOMAIN -or -not $DUCKDNS_TOKEN) {
    Write-Error "DUCKDNS_SUBDOMAIN and DUCKDNS_TOKEN must be set in duckdns.env"
}

$url = "https://www.duckdns.org/update?domains=$DUCKDNS_SUBDOMAIN&token=$DUCKDNS_TOKEN&ip="
$response = Invoke-WebRequest -Uri $url -UseBasicParsing

$body = $response.Content
if ($body -is [byte[]]) {
    $body = [System.Text.Encoding]::UTF8.GetString($body)
}
$body = "$body".Trim()

if ($body -eq "OK") {
    Write-Host "[DuckDNS] Updated $DUCKDNS_SUBDOMAIN.duckdns.org"
} else {
    Write-Error "[DuckDNS] Update failed: $body"
}
