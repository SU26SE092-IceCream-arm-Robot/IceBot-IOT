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
$cupDriverBuild = Join-Path $repositoryRoot "driver-sdk\IceBot.Driver.CupDropping\build-package.ps1"
$iceCreamDriverBuild = Join-Path $repositoryRoot "driver-sdk\IceBot.Driver.IceCream\build-package.ps1"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\installer\IceBot-$Runtime"
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
if (-not $OutputDirectory.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay inside $artifactsRoot"
}

$workingDirectory = Join-Path $artifactsRoot "installer\.build-$Runtime"
if (Test-Path -LiteralPath $workingDirectory) {
    Remove-Item -LiteralPath $workingDirectory -Recurse -Force
}

try {
    dotnet build $solution -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "IceBot build failed." }

    & $cupDriverBuild -Configuration $Configuration
    & $iceCreamDriverBuild -Configuration $Configuration

    if (Test-Path -LiteralPath $OutputDirectory) {
        Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null

    $bundleRoot = Join-Path $workingDirectory "bundle"
    $payload = Join-Path $bundleRoot "payload"
    $drivers = Join-Path $bundleRoot "drivers"
    $prerequisites = Join-Path $bundleRoot "prerequisites"
    $publishDirectory = Join-Path $workingDirectory "publish"
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    New-Item -ItemType Directory -Path $drivers -Force | Out-Null
    $mutablePayloadRoots = @("config", "certificates", "data", "drivers", "workflow")
    Get-ChildItem -LiteralPath $appOutput | Where-Object {
        $mutablePayloadRoots -notcontains $_.Name
    } | Copy-Item -Destination $payload -Recurse -Force
    foreach ($mutableRoot in $mutablePayloadRoots) {
        if (Test-Path -LiteralPath (Join-Path $payload $mutableRoot)) {
            throw "Installer payload must not contain mutable local state: $mutableRoot"
        }
    }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "DRIVER-DLL\CupDropping") -Destination $drivers -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "DRIVER-DLL\IceCream") -Destination $drivers -Recurse -Force

    $requiredPayloadFiles = @(
        "IceBot.exe",
        "InitIceBot.exe",
        "libfairino.dll",
        "CookComputing.XmlRpcV2.dll",
        "IceBot.Driver.Abstractions.dll"
    )
    foreach ($file in $requiredPayloadFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $payload $file))) {
            throw "Release payload is missing $file."
        }
    }

    $fairinoDll = Join-Path $payload "libfairino.dll"
    $installerManifest = [ordered]@{
        schemaVersion = 1
        fairinoSdk = "robot3.7.8"
        libfairinoSha256 = (Get-FileHash -LiteralPath $fairinoDll -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $installerManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundleRoot "installer-manifest.json") -Encoding UTF8

    if (-not [string]::IsNullOrWhiteSpace($DotNetFrameworkInstaller)) {
        New-Item -ItemType Directory -Path $prerequisites -Force | Out-Null
        Copy-Item -LiteralPath $DotNetFrameworkInstaller -Destination $prerequisites -Force
    }
    if (-not [string]::IsNullOrWhiteSpace($NetBirdInstaller)) {
        New-Item -ItemType Directory -Path $prerequisites -Force | Out-Null
        Copy-Item -LiteralPath $NetBirdInstaller -Destination $prerequisites -Force
    }

    $bundleArchive = Join-Path $workingDirectory "install-bundle.zip"
    Compress-Archive -Path (Join-Path $bundleRoot "*") -DestinationPath $bundleArchive -CompressionLevel Optimal

    dotnet publish $setupProject -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
        -p:InstallerBundleArchive=$bundleArchive -o $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw "Setup publish failed." }

    $publishedSetup = Join-Path $publishDirectory "IceBot-Setup.exe"
    if (-not (Test-Path -LiteralPath $publishedSetup)) {
        throw "Published IceBot-Setup.exe was not found."
    }
    Copy-Item -LiteralPath $publishedSetup -Destination (Join-Path $OutputDirectory "IceBot-Setup.exe") -Force

    Write-Host "[OK] Single-file installer: $(Join-Path $OutputDirectory 'IceBot-Setup.exe')"
    Write-Host "Run IceBot-Setup.exe as Administrator."
}
finally {
    if (Test-Path -LiteralPath $workingDirectory) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force
    }
}