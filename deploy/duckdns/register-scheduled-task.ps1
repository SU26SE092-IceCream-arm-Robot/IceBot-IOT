# Creates Windows Task Scheduler job to update DuckDNS every 5 minutes.

$ErrorActionPreference = "Stop"
$taskName = "IceBot-DuckDNS-Update"
$scriptPath = Join-Path $PSScriptRoot "update-duckdns.ps1"
$tr = "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`""

schtasks /Create /TN $taskName /TR $tr /SC MINUTE /MO 5 /F | Out-Null
Write-Host "[TaskScheduler] Registered '$taskName' (every 5 minutes)"
