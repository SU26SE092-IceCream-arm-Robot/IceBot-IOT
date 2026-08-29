param([string[]]$TestClasses=@(),[switch]$NoRestore)
$ErrorActionPreference='Stop'
$skillRoot=Split-Path -Parent $PSScriptRoot
$repoRoot=(Resolve-Path (Join-Path $skillRoot '..\..\..')).Path
$project=Join-Path $repoRoot 'harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj'
$testing=Join-Path $repoRoot 'testing';$results=Join-Path $testing 'results';$trx=Join-Path $results 'IceBot.UnitTests.Fresh.trx';$report=Join-Path $testing 'UNIT_TEST_REPORT.md'
New-Item -ItemType Directory -Path $results -Force|Out-Null
function Run([string]$name,[string]$filter){$args=@('test',$project,'--logger',"trx;LogFileName=$name",'--results-directory',$results);if($NoRestore){$args+='--no-restore'};if($filter){$args+=@('--filter',$filter)};& dotnet @args;if($LASTEXITCODE-ne 0){throw "Tests failed: $name"}}
if($TestClasses.Count){$filters=$TestClasses|Where-Object{$_}|ForEach-Object{"FullyQualifiedName~.$_"};Run 'IceBot.RelatedTests.trx' ($filters-join '|')}
Run 'IceBot.UnitTests.Fresh.trx' ''
& (Join-Path $PSScriptRoot 'generate-test-document.ps1') -TrxPath $trx -OutputPath $report
if($LASTEXITCODE-ne 0){throw 'Cannot generate Markdown test document.'}
