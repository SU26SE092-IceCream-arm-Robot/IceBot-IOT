param(
    [Parameter(Mandatory = $true)][string]$TrxPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$catalog = [ordered]@{
    AuthenticationAndConnectivityTests=@('AUTH-NET','Authentication and NetBird validation')
    ConfigSetupWizardTests=@('CONFIG-ID','Configuration identity preservation')
    SiteSettingsTests=@('SITE-CFG','Site settings and device mapping')
    EdgeClientCertificateProvisionerTests=@('MTLS-CERT','mTLS client certificate')
    ExecutionEndpointRegistrationTests=@('ENDPOINT','Kiosk and execution endpoint contracts')
    PeripheralDeviceRegistrationTests=@('DEVICE','Peripheral device registration contract')
    EdgeDeploymentApiTests=@('DEPLOY-API','Full Edge deployment command contract')
    FullEdgeConfigurationInstallerTests=@('LUA-INSTALL','Verified Lua bundle installation')
    MachinePluginLoaderTests=@('DRIVER-DLL','Peripheral driver plugin loading')
    IceCreamDriverTests=@('ICE-CREAM-BLL','Ice-cream STM32 driver protocol and safe cycle')
    OrderRequestTests=@('LOCAL-ORDER','Legacy local Order contract')
    EdgeOrderInboxTests=@('MTLS-ORDER','mTLS ExecuteOrder validation')
    EdgeOrderExecutionQueueTests=@('ORDER-QUEUE','Durable production queue')
    ProductionReportOutboxTests=@('REPORT-OUT','Production report outbox')
    WorkflowExecutionPlanTests=@('WORKFLOW-PLAN','Lua composition and Edge instruction dispatch')
}
function Test-Type([string]$name) {
    if ($name -match 'Limit|Four|Ten|Boundary') { return 'Boundary' }
    if ($name -match 'Reject|Invalid|Missing|Wrong|Expired|Tampered|Unsafe|Error|Empty|Without|NonPositive') { return 'Abnormal' }
    return 'Normal'
}
function Expected-Result([string]$name) {
    if ($name -match 'Reject') { return 'Invalid or unsafe input is rejected as expected.' }
    if ($name -match 'Accept') { return 'Valid input is accepted and required data is retained.' }
    if ($name -match 'Return') { return 'The expected value or identifier is returned.' }
    if ($name -match 'Create') { return 'The expected durable data is created without duplication.' }
    if ($name -match 'Preserve') { return 'The existing identity is preserved safely.' }
    return 'All automated assertions pass.'
}
[xml]$document = Get-Content -LiteralPath $TrxPath -Raw
$tests = @($document.TestRun.Results.UnitTestResult | ForEach-Object {
    $full = [string]$_.testName; $parts = $full.Split('.'); $class = $parts[3]
    if (-not $catalog.Contains($class)) { throw "Missing test-document mapping for $class" }
    [pscustomobject]@{ Class=$class; Code=$catalog[$class][0]; Function=$catalog[$class][1]; Scenario=$full.Substring($full.LastIndexOf('.')+1).Replace('_',' '); Expected=Expected-Result $full; Type=Test-Type $full; Result=[string]$_.outcome; Duration=[string]$_.duration }
})
$passed=@($tests|Where-Object Result -eq 'Passed').Count; $failed=@($tests|Where-Object Result -eq 'Failed').Count; $skipped=$tests.Count-$passed-$failed
$out=[Collections.Generic.List[string]]::new()
$out.Add('# IceBot-IOT Test Document'); $out.Add('')
$out.Add("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$out.Add("- Framework: xUnit / .NET Framework 4.7.2")
$out.Add("- Total: $($tests.Count)"); $out.Add("- Passed: $passed"); $out.Add("- Failed: $failed"); $out.Add("- Skipped: $skipped"); $out.Add('')
$out.Add('## Scope'); $out.Add(''); $out.Add('This document summarizes deterministic unit tests for the current IceBot Edge application. Live BE, NetBird, robot, serial ports, and peripheral hardware are outside this unit-test run.'); $out.Add('')
$out.Add('## Summary by function'); $out.Add(''); $out.Add('| Code | Function | Total | Passed | Failed |'); $out.Add('|---|---|---:|---:|---:|')
foreach($group in ($tests|Group-Object Class)){$first=$group.Group[0];$out.Add("| $($first.Code) | $($first.Function) | $($group.Count) | $(@($group.Group|Where-Object Result -eq 'Passed').Count) | $(@($group.Group|Where-Object Result -eq 'Failed').Count) |")}; $out.Add('')
foreach($group in ($tests|Group-Object Class)){$first=$group.Group[0];$out.Add("## $($first.Code) - $($first.Function)");$out.Add('');$out.Add('| ID | Scenario | Expected result | Type | Result | Duration |');$out.Add('|---|---|---|---|---|---|');for($i=0;$i-lt $group.Count;$i++){$t=$group.Group[$i];$id="$($first.Code)-TC$('{0:00}' -f ($i+1))";$scenario=$t.Scenario.Replace('|','\|');$expected=$t.Expected.Replace('|','\|');$out.Add("| $id | $scenario | $expected | $($t.Type) | $($t.Result) | $($t.Duration) |")};$out.Add('')}
$out.Add('## Result');$out.Add('');if($failed-eq 0){$out.Add("All $passed tests passed.")}else{$out.Add("$failed test(s) failed. Review the TRX before release.")}
[IO.File]::WriteAllLines($OutputPath,$out,[Text.UTF8Encoding]::new($false))
"Generated $OutputPath with $($tests.Count) test cases."
