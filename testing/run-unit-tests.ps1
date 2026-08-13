param(
    [switch]$NoRestore
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj"
$results = Join-Path $PSScriptRoot "results"
New-Item -ItemType Directory -Path $results -Force | Out-Null

$arguments = @(
    "test",
    $project,
    "--logger", "trx;LogFileName=IceBot.UnitTests.trx",
    "--results-directory", $results
)
if ($NoRestore) { $arguments += "--no-restore" }

& dotnet @arguments
exit $LASTEXITCODE
