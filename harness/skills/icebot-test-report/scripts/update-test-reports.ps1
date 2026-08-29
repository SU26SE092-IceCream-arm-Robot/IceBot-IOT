param(
    [string[]]$TestClasses = @(),
    [switch]$NoRestore
)

Write-Warning "update-test-reports.ps1 is deprecated; Excel reports are no longer generated. Running the Markdown test-document workflow."
& (Join-Path $PSScriptRoot 'run-tests-and-document.ps1') -TestClasses $TestClasses -NoRestore:$NoRestore
exit $LASTEXITCODE
