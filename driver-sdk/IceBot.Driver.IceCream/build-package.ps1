param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$projectDirectory = $PSScriptRoot
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory "..\.."))
$project = Join-Path $projectDirectory "IceBot.Driver.IceCream.csproj"
$outputDirectory = Join-Path $repositoryRoot "DRIVER-DLL\IceCream"
$assemblyName = "IceBot.Driver.IceCream.dll"

dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Ice-cream driver build failed." }

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$builtAssembly = Join-Path $projectDirectory "bin\$Configuration\net472\$assemblyName"
$packagedAssembly = Join-Path $outputDirectory $assemblyName
Copy-Item -LiteralPath $builtAssembly -Destination $packagedAssembly -Force

$sha256 = (Get-FileHash -LiteralPath $packagedAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    schemaVersion = 1
    machineType = "ice_cream"
    assembly = $assemblyName
    entryType = "IceBot.Driver.IceCream.IceCreamDriver"
    driverVersion = "1.0.0"
    sha256 = $sha256
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory "driver.json") -Encoding UTF8
Write-Host "[OK] Ice-cream driver package: $outputDirectory"