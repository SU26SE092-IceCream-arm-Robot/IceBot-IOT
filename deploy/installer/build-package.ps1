param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = "",
    [string]$DotNetFrameworkInstaller = "",
    [string]$NetBirdInstaller = ""
)

$ErrorActionPreference = "Stop"
$scriptDirectory = $PSScriptRoot
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory "..\.."))
$solution = Join-Path $repositoryRoot "code\IceBot-IOT.sln"
$appOutput = Join-Path $repositoryRoot "code\src\IceBot\bin\$Configuration\net472"
$setupProject = Join-Path $repositoryRoot "code\src\IceBot.Setup\IceBot.Setup.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\installer\IceBot-$Runtime"
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
if (-not $OutputDirectory.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay inside $artifactsRoot"
}

dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "IceBot build failed." }

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null

dotnet publish $setupProject -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Setup publish failed." }

$payload = Join-Path $OutputDirectory "payload"
New-Item -ItemType Directory -Path $payload | Out-Null
Copy-Item -Path (Join-Path $appOutput "*") -Destination $payload -Recurse -Force

$prerequisites = Join-Path $OutputDirectory "prerequisites"
if (-not [string]::IsNullOrWhiteSpace($DotNetFrameworkInstaller)) {
    New-Item -ItemType Directory -Path $prerequisites -Force | Out-Null
    Copy-Item -LiteralPath $DotNetFrameworkInstaller -Destination $prerequisites -Force
}
if (-not [string]::IsNullOrWhiteSpace($NetBirdInstaller)) {
    New-Item -ItemType Directory -Path $prerequisites -Force | Out-Null
    Copy-Item -LiteralPath $NetBirdInstaller -Destination $prerequisites -Force
}

Write-Host "[OK] Installer package: $OutputDirectory"
Write-Host "Distribute the entire directory, then run Setup.exe as Administrator."
