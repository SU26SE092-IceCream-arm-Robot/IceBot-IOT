# IceBot-IOT Test Document

## InitIceBot local diagnostics — 2026-09-08

- Scope: offline entry before login; read-only application events and per-unit attempt history; date/order filtering; pagination and filtered text export; malformed/partial record tolerance without changing production state.
- Targeted: 1 passed, 0 failed, 0 skipped. Command: `dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj -c Release --no-restore -m:1 -nr:false --filter FullyQualifiedName~LocalDiagnosticsTests --logger "trx;LogFileName=LocalDiagnosticsRegression.trx"`.
- Full harness: 131 passed, 0 failed, 0 skipped. Command: `dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj -c Release --no-restore -m:1 -nr:false --logger "trx;LogFileName=LocalDiagnosticsFull.trx"`.
- Evidence: `harness/IceBot.Harness.Tests/TestResults/LocalDiagnosticsRegression.trx` and `harness/IceBot.Harness.Tests/TestResults/LocalDiagnosticsFull.trx`.
- Release solution build succeeded with zero warnings/errors using `dotnet build code/IceBot-IOT.sln -c Release --no-restore -m:1 -nr:false`.
- Tested against the existing workspace; no live Backend or hardware actions were performed.

## Edge restart recovery — 2026-09-08

- Scope: operator-launched restart remakes only interrupted Running units; preserves Completed units; retains local attempt history; validates stored Lua before execution; guards concurrent runtime instances; records session/crash diagnostics; recovers pending completion reports with an identical envelope.
- Targeted tests: 13 passed, 0 failed, 0 skipped. Command: `dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj -c Release --no-restore -m:1 -nr:false --filter "FullyQualifiedName~EdgeOrderExecutionQueueTests|FullyQualifiedName~RuntimeSessionTests" --logger "trx;LogFileName=RestartRecoveryRegression.trx"`.
- Full harness: 130 passed, 0 failed, 0 skipped. Command: `dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj -c Release --no-restore -m:1 -nr:false --logger "trx;LogFileName=RestartRecoveryFull.trx"`.
- Evidence: `harness/IceBot.Harness.Tests/TestResults/RestartRecoveryRegression.trx` and `harness/IceBot.Harness.Tests/TestResults/RestartRecoveryFull.trx`.
- Release solution build: `dotnet build code/IceBot-IOT.sln -c Release --no-restore -m:1 -nr:false` succeeded, zero warnings/errors.
- Initial sandbox run could not perform File.Replace in temporary test directories; verification above ran outside the sandbox and passed. Tests simulate interruption through persisted state and injected report failure; no power was cut and no physical device was commanded.
- Validation uses the current workspace, which already contains unrelated uncommitted runtime/SDK/plugin changes. No clean-checkout or live Backend acceptance test was performed. Backend source was not modified.

## STM32 limit EXTI verification — 2026-09-07

- Scope: handle both PB0 upper and PB10 lower in the GPIO EXTI callback; preserve matching-direction stopping/rejection and opposite-direction release.
- Firmware tests: 5 total, 5 passed, 0 failed, 0 skipped. Command: `dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~IceCreamFirmwareContractTests --logger "trx;LogFileName=LimitExtiRegression.trx"`.
- Full harness: 125 total, 125 passed, 0 failed, 0 skipped. Command: `dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=LimitExtiFull.trx"`.
- Evidence: `harness/IceBot.Harness.Tests/TestResults/LimitExtiRegression.trx` and `harness/IceBot.Harness.Tests/TestResults/LimitExtiFull.trx`.
- Release build: `cmake --build --preset Release` succeeded; Flash 8256 bytes, RAM 1896 bytes.
- STM32CubeProgrammer flashed `firmware/ice-cream-controller/build/Release/ice-cream-controller.elf` through ST-Link `37FF71064E573436E04B1343`; reported `Download verified successfully` and performed MCU software reset.
- Subsequent manual session: GPIO reads confirmed HIGH/LOW/HIGH on release/press/release for both limits; the operator confirmed successful physical UP/DOWN operation and real-hardware testing after exercising the switches. STOP/Standby replies were verified. These are manual observations, separate from the automated test counts.
- A rejected command with the matching switch continuously held was not captured. Touching an exposed NC terminal caused a reported stop; the cause was not isolated. See `context/PROJECT_CONTEXT.md` for the corrected COM-to-GPIO / NO-to-GND wiring and evidence limits.

## Previous full report

- Generated: 2026-08-29 17:55:46
- Framework: xUnit / .NET Framework 4.7.2
- Total: 123
- Passed: 123
- Failed: 0
- Skipped: 0

## Scope

This document summarizes deterministic unit tests for the current IceBot Edge application. Live BE, NetBird, robot, serial ports, and peripheral hardware are outside this unit-test run.

## Summary by function

| Code | Function | Total | Passed | Failed |
|---|---|---:|---:|---:|
| CONFIG-MENU | InitIceBot configuration menu navigation | 1 | 1 | 0 |
| ENDPOINT | Kiosk and execution endpoint contracts | 13 | 13 | 0 |
| CONFIG-ID | Configuration identity preservation | 2 | 2 | 0 |
| AUTH-NET | Authentication and NetBird validation | 4 | 4 | 0 |
| WORKFLOW-PLAN | Lua composition and Edge instruction dispatch | 20 | 20 | 0 |
| ICE-CREAM-BLL | Ice-cream STM32 driver protocol and safe cycle | 7 | 7 | 0 |
| ICE-FIRMWARE | STM32 actuator direction and limit-switch contract | 3 | 3 | 0 |
| MTLS-ORDER | mTLS ExecuteOrder validation | 13 | 13 | 0 |
| SITE-CFG | Site settings and device mapping | 14 | 14 | 0 |
| DRIVER-DLL | Peripheral driver plugin loading | 12 | 12 | 0 |
| LOCAL-ORDER | Legacy local Order contract | 3 | 3 | 0 |
| ORDER-QUEUE | Durable production queue | 8 | 8 | 0 |
| LUA-INSTALL | Verified Lua bundle installation | 4 | 4 | 0 |
| DEPLOY-API | Full Edge deployment command contract | 5 | 5 | 0 |
| DEVICE | Peripheral device registration contract | 3 | 3 | 0 |
| SIM-ROBOT | Simulated robot and peripheral execution | 4 | 4 | 0 |
| ROBOT-DISCOVERY | Robot hardware discovery | 2 | 2 | 0 |
| REPORT-OUT | Production report outbox | 1 | 1 | 0 |
| MTLS-CERT | mTLS client certificate | 4 | 4 | 0 |

## CONFIG-MENU - InitIceBot configuration menu navigation

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| CONFIG-MENU-TC01 | ConfigMenu ShowsFiveTaskBasedGroupsAndReturns | The expected value or identifier is returned. | Normal | Passed | 00:00:00.0030000 |

## ENDPOINT - Kiosk and execution endpoint contracts

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| ENDPOINT-TC01 | NormalizeKioskCode RejectsValuesUnsafeForBackendOrLocalConfig(input: "BAD=CODE") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| ENDPOINT-TC02 | NormalizeKioskCode RejectsValuesUnsafeForBackendOrLocalConfig(input: "A") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| ENDPOINT-TC03 | SelectUnambiguousProvisioningFullEdgeEndpoint RejectsAmbiguousProvisioningEndpoints | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0020000 |
| ENDPOINT-TC04 | ParseKioskCreateResponse ReturnsBackendKioskId | The expected value or identifier is returned. | Normal | Passed | 00:00:00.0050000 |
| ENDPOINT-TC05 | ParseCreateResponse UsesBackendError | The expected durable data is created without duplication. | Abnormal | Passed | 00:00:00.0010000 |
| ENDPOINT-TC06 | ParseManagementResponse ReturnsActiveProfileIdentity | The expected value or identifier is returned. | Normal | Passed | 00:00:00.0230000 |
| ENDPOINT-TC07 | ParseKioskManagementResponse RequiresActiveOperationalKioskData | All automated assertions pass. | Normal | Passed | 00:00:00.0030000 |
| ENDPOINT-TC08 | BuildEndpointCode IsStableAndBackendSafe | All automated assertions pass. | Normal | Passed | 00:00:00.0020000 |
| ENDPOINT-TC09 | NormalizeKioskCode RejectsValuesUnsafeForBackendOrLocalConfig(input: "") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| ENDPOINT-TC10 | SelectUnambiguousProvisioningFullEdgeEndpoint ReusesSingleSeededEndpoint | All automated assertions pass. | Normal | Passed | 00:00:00.0080000 |
| ENDPOINT-TC11 | NormalizeKioskCode PreservesPrintedCodeAndUppercasesIt | The existing identity is preserved safely. | Normal | Passed | 00:00:00.0010000 |
| ENDPOINT-TC12 | ParseCreateResponse ReturnsExecutionEndpointId | The expected value or identifier is returned. | Normal | Passed | 00:00:00.0110000 |
| ENDPOINT-TC13 | SelectUnambiguousProvisioningFullEdgeEndpoint IgnoresOtherProfilesAndStates | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |

## CONFIG-ID - Configuration identity preservation

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| CONFIG-ID-TC01 | PreserveBackendDeviceIdentities CopiesKioskAndDeviceMappings | The existing identity is preserved safely. | Normal | Passed | 00:00:00.0100000 |
| CONFIG-ID-TC02 | PreserveBackendDeviceIdentities CreatesIndependentDictionary | The expected durable data is created without duplication. | Normal | Passed | 00:00:00.0090000 |

## AUTH-NET - Authentication and NetBird validation

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| AUTH-NET-TC01 | Refresh RejectsMissingTokenWithoutCallingBackend | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| AUTH-NET-TC02 | NetBirdRunUp RejectsMissingSetupKeyWithoutStartingProcess | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| AUTH-NET-TC03 | Login RejectsMissingCredentialsWithoutCallingBackend(account: "store", password: "") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| AUTH-NET-TC04 | Login RejectsMissingCredentialsWithoutCallingBackend(account: "", password: "password") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |

## WORKFLOW-PLAN - Lua composition and Edge instruction dispatch

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| WORKFLOW-PLAN-TC01 | HomeFallback RejectsUnexpectedPointOrError(pointName: "icebot home", errorCode: 143) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC02 | HomeFallback AppliesOnlyToVerifiedPointOnController37(errorCode: -4) | All automated assertions pass. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC03 | execute(\"bad\")") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC04 | HomeFallback RejectsUnexpectedPointOrError(pointName: "IceBot Home", errorCode: -2) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC05 | Compose PreservesBackendArtifactOrder | The existing identity is preserved safely. | Normal | Passed | 00:00:00.0270000 |
| WORKFLOW-PLAN-TC06 | MachineTypeCanonicalizer NormalizesKnownAliasAndWhitespace(input: " bt cup l90 ", expected: "bt cup l90") | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC07 | Parse FairinoStudioOutputBuildsTypedExecutionPlan | All automated assertions pass. | Normal | Passed | 00:00:00.3420000 |
| WORKFLOW-PLAN-TC08 | Parse RejectsUnsafeOrUnboundedWorkflow(lua: "MoveJ({1,2,3,4,5,6}, 0, 0, 0, 30, -1, -1)") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC09 | Parse CycleLoopExpandsToFiniteInstructions | All automated assertions pass. | Normal | Passed | 00:00:00.0090000 |
| WORKFLOW-PLAN-TC10 | HomeFallback RejectsUnexpectedPointOrError(pointName: "Other Home", errorCode: 143) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC11 | Compose RejectsArtifactPathTraversal | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC12 | clock() -"···) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC13 | RuntimeHomePoint UsesVerifiedFallbackIdentity | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC14 | MachineTypeCanonicalizer NormalizesKnownAliasAndWhitespace(input: "icemachine", expected: "ice cream") | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC15 | HomeFallback RejectsUnexpectedPointOrError(pointName: "IceBot Home", errorCode: 142) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC16 | HomeFallback AppliesOnlyToVerifiedPointOnController37(errorCode: 143) | All automated assertions pass. | Abnormal | Passed | 00:00:00.0130000 |
| WORKFLOW-PLAN-TC17 | Execute DispatchesEveryInstructionInOrder | All automated assertions pass. | Normal | Passed | 00:00:00.0050000 |
| WORKFLOW-PLAN-TC18 | Parse RejectsUnsafeOrUnboundedWorkflow(lua: "SetToolDO(2, 1, 0, 0)") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0020000 |
| WORKFLOW-PLAN-TC19 | MachineTypeCanonicalizer NormalizesKnownAliasAndWhitespace(input: "ICE cream", expected: "ICE cream") | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| WORKFLOW-PLAN-TC20 | Parse RejectsUnsafeOrUnboundedWorkflow(lua: "SetDO(16, 1, 0)") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |

## ICE-CREAM-BLL - Ice-cream STM32 driver protocol and safe cycle

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| ICE-CREAM-BLL-TC01 | Trigger DownRunsUntilLowerLimitOnly | All automated assertions pass. | Boundary | Passed | 00:00:00.0030000 |
| ICE-CREAM-BLL-TC02 | Trigger RejectsFaultBeforeMotorMotion | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0030000 |
| ICE-CREAM-BLL-TC03 | SerialFrameCodec BuildsVerifiedControllerFrames | All automated assertions pass. | Normal | Passed | 00:00:00.0020000 |
| ICE-CREAM-BLL-TC04 | SerialFrameCodec RejectsCorruptChecksum | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.1200000 |
| ICE-CREAM-BLL-TC05 | Trigger UpRunsUntilUpperLimitOnly | All automated assertions pass. | Boundary | Passed | 00:00:00.0170000 |
| ICE-CREAM-BLL-TC06 | Trigger RejectedUpAttemptsEmergencyStop | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0060000 |
| ICE-CREAM-BLL-TC07 | Driver ExposesCanonicalMachineIdentity | All automated assertions pass. | Normal | Passed | 00:00:00.0050000 |

## ICE-FIRMWARE - STM32 actuator direction and limit-switch contract

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| ICE-FIRMWARE-TC01 | Firmware MapsCommandsToMeasuredPhysicalDirections | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| ICE-FIRMWARE-TC02 | Firmware StopsAndRejectsPhysicalUpAtUpperLimitButKeepsDownAvailable | Invalid or unsafe input is rejected as expected. | Boundary | Passed | 00:00:00.0050000 |
| ICE-FIRMWARE-TC03 | Firmware ConfiguresUpperLimitAsActiveLowExtiInput | All automated assertions pass. | Boundary | Passed | 00:00:00.0130000 |

## MTLS-ORDER - mTLS ExecuteOrder validation

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| MTLS-ORDER-TC01 | Validate AcceptsAllBackendSupportedSchemaVersions(schemaVersion: 5) | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.0090000 |
| MTLS-ORDER-TC02 | Validate AcceptsAllBackendSupportedSchemaVersions(schemaVersion: 3) | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.0010000 |
| MTLS-ORDER-TC03 | ValidateForThisEdge RejectsInstalledLuaWithWrongChecksum | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0100000 |
| MTLS-ORDER-TC04 | TryStore DeduplicatesByCommandId | All automated assertions pass. | Normal | Passed | 00:00:00.0760000 |
| MTLS-ORDER-TC05 | Validate RejectsNonPositiveQuantity | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0400000 |
| MTLS-ORDER-TC06 | ValidateForThisEdge AcceptsMatchingIdentityReleaseAndLuaChecksum | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.0440000 |
| MTLS-ORDER-TC07 | Validate AcceptsAllBackendSupportedSchemaVersions(schemaVersion: 4) | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.0010000 |
| MTLS-ORDER-TC08 | Validate AcceptsBackendSchema5ExecuteOrder | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.0010000 |
| MTLS-ORDER-TC09 | ValidateForThisEdge RejectsAnotherEndpoint | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| MTLS-ORDER-TC10 | OrderedArtifacts UsesBindingOrderThenRunOrder | All automated assertions pass. | Normal | Passed | 00:00:00.0030000 |
| MTLS-ORDER-TC11 | ValidateForThisEdge RejectsInactiveRelease | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0040000 |
| MTLS-ORDER-TC12 | ValidateForThisEdge RejectsExpiredCommand | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0070000 |
| MTLS-ORDER-TC13 | ValidateForThisEdge RejectsMoreThanFourUnits | Invalid or unsafe input is rejected as expected. | Boundary | Passed | 00:00:00.0030000 |

## SITE-CFG - Site settings and device mapping

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| SITE-CFG-TC01 | ReportedDevicesSnapshot ConflictRecoveryAlwaysCreatesNextRevision | The expected durable data is created without duplication. | Normal | Passed | 00:00:00.0200000 |
| SITE-CFG-TC02 | SetupCompletion IsIndependentFromDeploymentReadiness | All automated assertions pass. | Normal | Passed | 00:00:00.0720000 |
| SITE-CFG-TC03 | DeviceIdSerialization IgnoresInvalidEntriesAndIsCaseInsensitive | All automated assertions pass. | Abnormal | Passed | 00:00:00.0010000 |
| SITE-CFG-TC04 | ActiveWorkflowPeripheralDiscovery UsesTriggerDeviceMachineTypesInsteadOfArtifactFileNames | All automated assertions pass. | Normal | Passed | 00:00:00.4100000 |
| SITE-CFG-TC05 | IsConfigured RequiresBothNetBirdKeyAndPublicUrl | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| SITE-CFG-TC06 | ReadinessSafetyProfile UsesSafeForSimulationOrVerifiedPhysicalTelemetry | All automated assertions pass. | Normal | Passed | 00:00:00.0070000 |
| SITE-CFG-TC07 | ReportedDevicesSnapshot IncrementsVersionWhenContentChanges | All automated assertions pass. | Boundary | Passed | 00:00:00.0010000 |
| SITE-CFG-TC08 | MachineLookups AreCaseInsensitiveAndUnknownReturnsEmpty | The expected value or identifier is returned. | Abnormal | Passed | 00:00:00.0020000 |
| SITE-CFG-TC09 | ReadinessCapabilityProfile SimulatedModeReportsAvailableRobotArm | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| SITE-CFG-TC10 | DeviceIdSerialization SkipsEmptyIdsAndMachineTypes | All automated assertions pass. | Abnormal | Passed | 00:00:00.0010000 |
| SITE-CFG-TC11 | ReportedDevicesSnapshot NormalizesTimestampToPostgresMicrosecondPrecision | All automated assertions pass. | Normal | Passed | 00:00:00.0020000 |
| SITE-CFG-TC12 | ReportedDevicesSnapshot ReusesRevisionAndObservedAtForSameContent | All automated assertions pass. | Boundary | Passed | 00:00:00.0020000 |
| SITE-CFG-TC13 | ReadinessCapabilityProfile PhysicalModeDoesNotClaimUnverifiedCapability | All automated assertions pass. | Normal | Passed | 00:00:00.0030000 |
| SITE-CFG-TC14 | ReadinessCapabilityProfile PhysicalModeReportsRobotOnlyWhenSafetyIsVerified | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |

## DRIVER-DLL - Peripheral driver plugin loading

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| DRIVER-DLL-TC01 | Load RejectsTamperedDllBySha256 | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.3960000 |
| DRIVER-DLL-TC02 | Load ValidDllPackageCreatesDriver | The expected durable data is created without duplication. | Normal | Passed | 00:00:01.5340000 |
| DRIVER-DLL-TC03 | dll", entryType: "Type", version: "1", sha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"···) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC04 | Load PackagedCupDroppingDriver IsValidAndLoadable | All automated assertions pass. | Normal | Passed | 00:00:00.0030000 |
| DRIVER-DLL-TC05 | dll", entryType: "Type", version: "1", sha: "not-a-checksum") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC06 | Load MissingDirectoryIsEmptyAndSafe | All automated assertions pass. | Abnormal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC07 | ValidateManifest RejectsAssemblyTraversal | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC08 | exe", entryType: "Type", version: "1", sha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"···) | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC09 | DriverDirectory UsesSharedProgramDataLocation | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC10 | Load PackagedIceCreamDriver IsValidAndLoadable | All automated assertions pass. | Normal | Passed | 00:00:00.0060000 |
| DRIVER-DLL-TC11 | ValidateModule RejectsMachineTypeUnsafeForConfigSerialization | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| DRIVER-DLL-TC12 | ValidateManifest AcceptsSchemaOneContract | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.0010000 |

## LOCAL-ORDER - Legacy local Order contract

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| LOCAL-ORDER-TC01 | lua\"]}") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0110000 |
| LOCAL-ORDER-TC02 | Validate RejectsLegacyLocalApiPayloads(json: "{}") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| LOCAL-ORDER-TC03 | Validate RejectsLegacyLocalApiPayloads(json: "not-json") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.6340000 |

## ORDER-QUEUE - Durable production queue

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| ORDER-QUEUE-TC01 | TryAdmit CreatesOneDurableJobPerUnitWithConsecutiveUnitNumbers | The expected durable data is created without duplication. | Normal | Passed | 00:00:00.7760000 |
| ORDER-QUEUE-TC02 | TryAdmit AllowsOnlyOneCustomerSessionUntilCompletion | All automated assertions pass. | Normal | Passed | 00:00:00.0300000 |
| ORDER-QUEUE-TC03 | NextRunnable StopsQueueWhenAnyJobRequiresManualIntervention | All automated assertions pass. | Normal | Passed | 00:00:00.0560000 |
| ORDER-QUEUE-TC04 | RecoverInterruptedJobs MarksRunningUnitForManualInterventionAndReportsOnce | All automated assertions pass. | Normal | Passed | 00:00:00.1110000 |
| ORDER-QUEUE-TC05 | ExecuteOrderLifecycle ValidatesPersistsExecutesAndCompletesInOrder | All automated assertions pass. | Normal | Passed | 00:00:00.1930000 |
| ORDER-QUEUE-TC06 | ValidateForThisEdge RejectsAnotherKiosk | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0020000 |
| ORDER-QUEUE-TC07 | RecoverAwaitingAcknowledgements ReplaysAckAndActivatesDurableJob | All automated assertions pass. | Normal | Passed | 00:00:00.1370000 |
| ORDER-QUEUE-TC08 | TryAdmit IsIdempotentByCommandId | All automated assertions pass. | Boundary | Passed | 00:00:00.0300000 |

## LUA-INSTALL - Verified Lua bundle installation

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| LUA-INSTALL-TC01 | InstallVerifiedBundle RejectsArtifactChecksumMismatchWithoutActivatingLua | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0180000 |
| LUA-INSTALL-TC02 | InstallVerifiedBundle InstallsGuidLuaAndManifest | All automated assertions pass. | Normal | Passed | 00:00:00.6330000 |
| LUA-INSTALL-TC03 | InstallVerifiedBundle RejectsUnexpectedArchiveEntry | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0070000 |
| LUA-INSTALL-TC04 | DeploymentReportOutbox OrdersInstalledBeforeActiveByDurableSequence | All automated assertions pass. | Normal | Passed | 00:00:00.0800000 |

## DEPLOY-API - Full Edge deployment command contract

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| DEPLOY-API-TC01 | ParseFullEdgeDeployment RejectsWrongCommandType | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0020000 |
| DEPLOY-API-TC02 | ParseFullEdgeDeployment AcceptsCompletePayload | Valid input is accepted and required data is retained. | Normal | Passed | 00:00:00.7120000 |
| DEPLOY-API-TC03 | ParseFullEdgeDeployment RejectsIncompletePayload(json: "{}") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0010000 |
| DEPLOY-API-TC04 | ParseFullEdgeDeployment RejectsIncompletePayload(json: "null") | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0020000 |
| DEPLOY-API-TC05 | BuildAcknowledgementBody IncludesPhysicalEvidenceOnlyForRejectedOrder | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0020000 |

## DEVICE - Peripheral device registration contract

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| DEVICE-TC01 | ParseRegistrationResponse UsesBackendErrorMessage | All automated assertions pass. | Abnormal | Passed | 00:00:00.6300000 |
| DEVICE-TC02 | MachineDeviceIds RoundTripByStableMachineType | All automated assertions pass. | Normal | Passed | 00:00:00.0030000 |
| DEVICE-TC03 | ParseRegistrationResponse ReturnsBackendDeviceId | The expected value or identifier is returned. | Normal | Passed | 00:00:00.0610000 |

## SIM-ROBOT - Simulated robot and peripheral execution

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| SIM-ROBOT-TC01 | Simulator AlsoSimulatesPeripheralsAndReportsNoPhysicalOutput | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| SIM-ROBOT-TC02 | Simulator FailsAtConfiguredStep | All automated assertions pass. | Normal | Passed | 00:00:00.1560000 |
| SIM-ROBOT-TC03 | Factory UsesSimulatorOnlyWhenExplicitlyConfigured | All automated assertions pass. | Normal | Passed | 00:00:00.8670000 |
| SIM-ROBOT-TC04 | Simulator RunsOpaqueExistingLuaWithoutContactingRobot | All automated assertions pass. | Abnormal | Passed | 00:00:00.1570000 |

## ROBOT-DISCOVERY - Robot hardware discovery

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| ROBOT-DISCOVERY-TC01 | ConfiguredDiscovery UsesConfiguredProfileAndOptionalBackendDeviceMapping | All automated assertions pass. | Normal | Passed | 00:00:00.0010000 |
| ROBOT-DISCOVERY-TC02 | ConfiguredDiscovery RejectsIncompleteHardwareConfiguration | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0040000 |

## REPORT-OUT - Production report outbox

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| REPORT-OUT-TC01 | Enqueue PersistsCompletePerUnitReportWithStableSequence | All automated assertions pass. | Normal | Passed | 00:00:00.7930000 |

## MTLS-CERT - mTLS client certificate

| ID | Scenario | Expected result | Type | Result | Duration |
|---|---|---|---|---|---|
| MTLS-CERT-TC01 | Ensure MigratesPasswordlessPfxToDpapiProtectedPassword | All automated assertions pass. | Normal | Passed | 00:00:00.1670000 |
| MTLS-CERT-TC02 | Ensure RejectsExistingCertificateWithoutPrivateKey | Invalid or unsafe input is rejected as expected. | Abnormal | Passed | 00:00:00.0930000 |
| MTLS-CERT-TC03 | Ensure CreatesReusableClientPfxAndSha256Fingerprint | The expected durable data is created without duplication. | Normal | Passed | 00:00:00.2820000 |
| MTLS-CERT-TC04 | LoadForMtls ProvidesPrivateKeyUsableByWindowsSchannel | All automated assertions pass. | Normal | Passed | 00:00:01.7970000 |

## Result

All 123 tests passed.
