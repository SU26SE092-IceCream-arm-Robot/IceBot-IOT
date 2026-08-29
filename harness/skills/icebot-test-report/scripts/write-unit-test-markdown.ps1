param(
    [Parameter(Mandatory = $true)][string]$TrxPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = "Stop"
$catalog = [ordered]@{
    AuthenticationAndConnectivityTests = @("AUTH-NET", "Authentication and NetBird validation")
    ConfigSetupWizardTests = @("CONFIG-ID", "Configuration identity preservation")
    SiteSettingsTests = @("SITE-CFG", "Site settings and device mapping")
    EdgeClientCertificateProvisionerTests = @("MTLS-CERT", "mTLS client certificate")
    ExecutionEndpointRegistrationTests = @("ENDPOINT", "Kiosk and execution endpoint contracts")
    PeripheralDeviceRegistrationTests = @("DEVICE", "Peripheral device registration contract")
    EdgeDeploymentApiTests = @("DEPLOY-API", "Full Edge deployment command contract")
    FullEdgeConfigurationInstallerTests = @("LUA-INSTALL", "Verified Lua bundle installation")
    MachinePluginLoaderTests = @("DRIVER-DLL", "Peripheral driver plugin loading")
    OrderRequestTests = @("LOCAL-ORDER", "Legacy local Order contract")
    EdgeOrderInboxTests = @("MTLS-ORDER", "mTLS ExecuteOrder validation")
    EdgeOrderExecutionQueueTests = @("ORDER-QUEUE", "Durable production queue")
    ProductionReportOutboxTests = @("REPORT-OUT", "Production report outbox")
}

function Get-TestType([string]$name) {
    if ($name -match "Limit|Four|Ten|Boundary") { return "B (Boundary)" }
    if ($name -match "Reject|Invalid|Missing|Wrong|Expired|Tampered|Unsafe|Error|Empty|Without|NonPositive") { return "A (Abnormal)" }
    return "N (Normal)"
}

function Get-Expected([string]$name) {
    if ($name -match "Reject") { return "Input không hợp lệ hoặc không an toàn bị từ chối đúng lỗi mong đợi." }
    if ($name -match "Accept") { return "Input hợp lệ được chấp nhận và giữ đủ dữ liệu bắt buộc." }
    if ($name -match "Return") { return "Trả về đúng giá trị hoặc định danh mong đợi." }
    if ($name -match "Create") { return "Tạo đúng dữ liệu hoặc file bền vững và không tạo trùng." }
    if ($name -match "Preserve") { return "Giữ nguyên định danh đã tồn tại một cách an toàn." }
    return "Tất cả assertion tự động đều đạt."
}

[xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
$tests = @($trx.TestRun.Results.UnitTestResult | ForEach-Object {
    $fullName = [string]$_.testName
    $parts = $fullName.Split(".")
    $className = $parts[3]
    if (-not $catalog.Contains($className)) { throw "Thiếu mapping report cho test class: $className" }
    [pscustomobject]@{
        ClassName = $className
        Code = $catalog[$className][0]
        Function = $catalog[$className][1]
        Scenario = $fullName.Substring($fullName.LastIndexOf(".") + 1).Replace("_", " ")
        Expected = Get-Expected $fullName
        Type = Get-TestType $fullName
        Outcome = [string]$_.outcome
        Duration = [string]$_.duration
    }
})

$passed = @($tests | Where-Object Outcome -eq "Passed").Count
$failed = @($tests | Where-Object Outcome -eq "Failed").Count
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("# IceBot-IOT Unit Test Report")
$lines.Add("")
$lines.Add("- Ngày chạy: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("- Tổng test: $($tests.Count)")
$lines.Add("- Passed: $passed")
$lines.Add("- Failed: $failed")
$lines.Add("- Nguồn kết quả: `testing/results/$([IO.Path]::GetFileName($TrxPath))`")
$lines.Add("")
$lines.Add("## Cách điền vào Excel")
$lines.Add("")
$lines.Add("Mỗi mục Function bên dưới tương ứng một function sheet. Mỗi dòng trong bảng là một cột UTCID trong Excel.")
$lines.Add("")
$lines.Add("- Tạo cột theo `UTCID`.")
$lines.Add("- Thêm nội dung `Condition / Test scenario`, rồi đặt `O` tại giao điểm với UTCID tương ứng.")
$lines.Add("- Thêm nội dung `Confirm / Expected result`, rồi đặt `O` tại giao điểm với cùng UTCID.")
$lines.Add("- Điền Type, Passed/Failed và Executed Date theo bảng.")
$lines.Add("")

foreach ($group in ($tests | Group-Object ClassName)) {
    $first = $group.Group[0]
    $lines.Add("## $($first.Code) — $($first.Function)")
    $lines.Add("")
    $lines.Add("- Function Code: `UT-$($first.Code)`")
    $lines.Add("- Total Test Cases: $($group.Count)")
    $lines.Add("- Passed: $(@($group.Group | Where-Object Outcome -eq 'Passed').Count)")
    $lines.Add("- Failed: $(@($group.Group | Where-Object Outcome -eq 'Failed').Count)")
    $lines.Add("")
    $lines.Add("| UTCID | Condition / Test scenario | Đặt O ở Condition | Confirm / Expected result | Đặt O ở Confirm | Type | Result | Executed Date |")
    $lines.Add("|---|---|---:|---|---:|---|---|---|")
    for ($index = 0; $index -lt $group.Count; $index++) {
        $test = $group.Group[$index]
        $utcId = "UTCID$($index + 1)".PadLeft(7, "0")
        $result = if ($test.Outcome -eq "Passed") { "P" } else { "F" }
        $scenario = $test.Scenario.Replace("|", "\|")
        $expected = $test.Expected.Replace("|", "\|")
        $lines.Add("| $utcId | $scenario | O | $expected | O | $($test.Type) | $result | $(Get-Date -Format 'yyyy-MM-dd') |")
    }
    $lines.Add("")
}

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
[IO.File]::WriteAllLines($OutputPath, $lines, [Text.UTF8Encoding]::new($false))
Write-Output "Markdown report written: $OutputPath ($($tests.Count) tests, $passed passed, $failed failed)."
