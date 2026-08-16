[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$packageDirectory = Join-Path $PSScriptRoot '..\lib\fairino-csharp-sdk\packages\xmlrpcnet.3.0.0.266'
$packageDirectory = [System.IO.Path]::GetFullPath($packageDirectory)
$packageArchive = Join-Path $packageDirectory 'xmlrpcnet.3.0.0.266.nupkg'
$assemblyPath = Join-Path $packageDirectory 'lib\net20\CookComputing.XmlRpcV2.dll'

if (-not (Test-Path -LiteralPath $packageArchive -PathType Leaf)) {
    throw "Missing vendored Fairino SDK dependency package: $packageArchive"
}

if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    $tar = Get-Command tar -ErrorAction SilentlyContinue
    if ($null -eq $tar) {
        throw 'The Windows tar command is required to extract the vendored xmlrpcnet package.'
    }

    & $tar.Source -xf $packageArchive -C $packageDirectory 'lib/net20/CookComputing.XmlRpcV2.dll'
    if ($LASTEXITCODE -ne 0) {
        throw "Could not extract $packageArchive"
    }
}

if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "CookComputing.XmlRpcV2.dll was not found after extracting $packageArchive"
}

Write-Host "Fairino SDK dependency is ready: $assemblyPath"
