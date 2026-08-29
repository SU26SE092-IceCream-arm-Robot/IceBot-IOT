# IceBot-IOT Unit Test Report

- Generated: 2026-08-29
- Configuration: Release
- Framework: xUnit / .NET Framework 4.7.2
- Total: 123
- Passed: 123
- Failed: 0
- Skipped: 0
- Evidence: `harness/IceBot.Harness.Tests/TestResults/PB10ConfigTests.trx`

## Command

```powershell
dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj --configuration Release --no-restore
```

## Scope

The run covers deterministic application, configuration-menu, command-validation, workflow, persistence, simulator, and peripheral-driver tests. Live Backend, NetBird, Fairino hardware, STM32 hardware, and physical serial ports are outside this unit-test run.

## Summary by suite

| Suite | Function | Total | Passed | Failed |
|---|---|---:|---:|---:|
| AuthenticationAndConnectivityTests | Authentication and NetBird validation | 4 | 4 | 0 |
| ConfigSetupWizardTests | Configuration identity preservation | 2 | 2 | 0 |
| ConfigurationMenuTests | Init configuration menu navigation | 1 | 1 | 0 |
| EdgeClientCertificateProvisionerTests | mTLS client certificate, DPAPI migration and Schannel handshake | 4 | 4 | 0 |
| EdgeDeploymentApiTests | Deployment command and acknowledgement contract | 5 | 5 | 0 |
| EdgeOrderExecutionQueueTests | Durable single-session production queue and simulated lifecycle | 8 | 8 | 0 |
| EdgeOrderInboxTests | mTLS ExecuteOrder validation | 13 | 13 | 0 |
| ExecutionEndpointRegistrationTests | Kiosk and execution endpoint contracts | 13 | 13 | 0 |
| FullEdgeConfigurationInstallerTests | Verified Lua release installation and ordered deployment reports | 4 | 4 | 0 |
| IceCreamDriverTests | Ice-cream STM32 driver protocol | 7 | 7 | 0 |
| IceCreamFirmwareContractTests | STM32 actuator direction, PB0/PB10 EXTI and limit safety contract | 3 | 3 | 0 |
| MachinePluginLoaderTests | Peripheral driver plugin loading | 12 | 12 | 0 |
| OrderRequestTests | Legacy local payload rejection | 3 | 3 | 0 |
| PeripheralDeviceRegistrationTests | Peripheral device registration | 3 | 3 | 0 |
| ProductionReportOutboxTests | Production report outbox | 1 | 1 | 0 |
| RobotDeviceDiscoveryTests | Robot hardware discovery | 2 | 2 | 0 |
| SimulatedRobotWorkflowExecutorTests | Simulated robot and peripheral execution | 4 | 4 | 0 |
| SiteSettingsTests | Site settings, setup, verified Fairino readiness safety/capability dispatch, active-Lua peripheral discovery and idempotent hardware snapshots | 12 | 12 | 0 |
| WorkflowExecutionPlanTests | Typed Lua workflow plan and machine-type aliases | 20 | 20 | 0 |

## Detailed results

### Authentication and NetBird validation

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.AuthenticationAndConnectivityTests.Login_RejectsMissingCredentialsWithoutCallingBackend(account: "", password: "password") | Passed | 00:00:00.2680000 |
| IceBot.Harness.Tests.AuthenticationAndConnectivityTests.Login_RejectsMissingCredentialsWithoutCallingBackend(account: "store", password: "") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.AuthenticationAndConnectivityTests.NetBirdRunUp_RejectsMissingSetupKeyWithoutStartingProcess | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.AuthenticationAndConnectivityTests.Refresh_RejectsMissingTokenWithoutCallingBackend | Passed | 00:00:00.0010000 |

### Configuration identity preservation

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.ConfigSetupWizardTests.PreserveBackendDeviceIdentities_CopiesKioskAndDeviceMappings | Passed | 00:00:00.0020000 |
| IceBot.Harness.Tests.ConfigSetupWizardTests.PreserveBackendDeviceIdentities_CreatesIndependentDictionary | Passed | 00:00:00.0010000 |

### Init configuration menu navigation

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.ConfigurationMenuTests.ConfigMenu_ShowsFiveTaskBasedGroupsAndReturns | Passed | 00:00:00.0030000 |

### mTLS client certificate

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.EdgeClientCertificateProvisionerTests.Ensure_CreatesReusableClientPfxAndSha256Fingerprint | Passed | 00:00:00.4180000 |
| IceBot.Harness.Tests.EdgeClientCertificateProvisionerTests.Ensure_MigratesPasswordlessPfxToDpapiProtectedPassword | Passed | 00:00:00.1610000 |
| IceBot.Harness.Tests.EdgeClientCertificateProvisionerTests.Ensure_RejectsExistingCertificateWithoutPrivateKey | Passed | 00:00:00.1410000 |
| IceBot.Harness.Tests.EdgeClientCertificateProvisionerTests.LoadForMtls_ProvidesPrivateKeyUsableByWindowsSchannel | Passed | 00:00:01.2740000 |

### Deployment command contract

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.EdgeDeploymentApiTests.BuildAcknowledgementBody_IncludesPhysicalEvidenceOnlyForRejectedOrder | Passed | 00:00:00.0020000 |
| IceBot.Harness.Tests.EdgeDeploymentApiTests.ParseFullEdgeDeployment_AcceptsCompletePayload | Passed | 00:00:01.0290000 |
| IceBot.Harness.Tests.EdgeDeploymentApiTests.ParseFullEdgeDeployment_RejectsIncompletePayload(json: "{}") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeDeploymentApiTests.ParseFullEdgeDeployment_RejectsIncompletePayload(json: "null") | Passed | 00:00:00.0020000 |
| IceBot.Harness.Tests.EdgeDeploymentApiTests.ParseFullEdgeDeployment_RejectsWrongCommandType | Passed | 00:00:00.0010000 |

### Durable single-session production queue

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.ExecuteOrderLifecycle_ValidatesPersistsExecutesAndCompletesInOrder | Passed | 00:00:00.1000000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.NextRunnable_StopsQueueWhenAnyJobRequiresManualIntervention | Passed | 00:00:00.0490000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.RecoverAwaitingAcknowledgements_ReplaysAckAndActivatesDurableJob | Passed | 00:00:00.0690000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.RecoverInterruptedJobs_MarksRunningUnitForManualInterventionAndReportsOnce | Passed | 00:00:00.0560000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.TryAdmit_AllowsOnlyOneCustomerSessionUntilCompletion | Passed | 00:00:00.0220000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.TryAdmit_CreatesOneDurableJobPerUnitWithConsecutiveUnitNumbers | Passed | 00:00:00.0800000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.TryAdmit_IsIdempotentByCommandId | Passed | 00:00:00.0210000 |
| IceBot.Harness.Tests.EdgeOrderExecutionQueueTests.ValidateForThisEdge_RejectsAnotherKiosk | Passed | 00:00:00.0010000 |

### mTLS ExecuteOrder validation

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.EdgeOrderInboxTests.OrderedArtifacts_UsesBindingOrderThenRunOrder | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.TryStore_DeduplicatesByCommandId | Passed | 00:00:00.0380000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.Validate_AcceptsAllBackendSupportedSchemaVersions(schemaVersion: 3) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.Validate_AcceptsAllBackendSupportedSchemaVersions(schemaVersion: 4) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.Validate_AcceptsAllBackendSupportedSchemaVersions(schemaVersion: 5) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.Validate_AcceptsBackendSchema5ExecuteOrder | Passed | 00:00:00.0040000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.Validate_RejectsNonPositiveQuantity | Passed | 00:00:01.0590000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.ValidateForThisEdge_AcceptsMatchingIdentityReleaseAndLuaChecksum | Passed | 00:00:00.0350000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.ValidateForThisEdge_RejectsAnotherEndpoint | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.ValidateForThisEdge_RejectsExpiredCommand | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.ValidateForThisEdge_RejectsInactiveRelease | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.ValidateForThisEdge_RejectsInstalledLuaWithWrongChecksum | Passed | 00:00:00.0070000 |
| IceBot.Harness.Tests.EdgeOrderInboxTests.ValidateForThisEdge_RejectsMoreThanFourUnits | Passed | 00:00:00.0020000 |

### Kiosk and execution endpoint contracts

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.BuildEndpointCode_IsStableAndBackendSafe | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.NormalizeKioskCode_PreservesPrintedCodeAndUppercasesIt | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.NormalizeKioskCode_RejectsValuesUnsafeForBackendOrLocalConfig(input: "") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.NormalizeKioskCode_RejectsValuesUnsafeForBackendOrLocalConfig(input: "A") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.NormalizeKioskCode_RejectsValuesUnsafeForBackendOrLocalConfig(input: "BAD=CODE") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.ParseCreateResponse_ReturnsExecutionEndpointId | Passed | 00:00:00.6950000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.ParseCreateResponse_UsesBackendError | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.ParseKioskCreateResponse_ReturnsBackendKioskId | Passed | 00:00:00.0130000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.ParseKioskManagementResponse_RequiresActiveOperationalKioskData | Passed | 00:00:00.0040000 |
| IceBot.Harness.Tests.ExecutionEndpointRegistrationTests.ParseManagementResponse_ReturnsActiveProfileIdentity | Passed | 00:00:00.0040000 |

### Verified Lua release installation

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.FullEdgeConfigurationInstallerTests.DeploymentReportOutbox_OrdersInstalledBeforeActiveByDurableSequence | Passed | 00:00:00.1360000 |
| IceBot.Harness.Tests.FullEdgeConfigurationInstallerTests.InstallVerifiedBundle_InstallsGuidLuaAndManifest | Passed | 00:00:00.1200000 |
| IceBot.Harness.Tests.FullEdgeConfigurationInstallerTests.InstallVerifiedBundle_RejectsArtifactChecksumMismatchWithoutActivatingLua | Passed | 00:00:00.0100000 |
| IceBot.Harness.Tests.FullEdgeConfigurationInstallerTests.InstallVerifiedBundle_RejectsUnexpectedArchiveEntry | Passed | 00:00:00.0040000 |

### Ice-cream STM32 driver protocol

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.IceCreamDriverTests.Driver_ExposesCanonicalMachineIdentity | Passed | 00:00:00.0080000 |
| IceBot.Harness.Tests.IceCreamDriverTests.SerialFrameCodec_BuildsVerifiedControllerFrames | Passed | 00:00:00.0260000 |
| IceBot.Harness.Tests.IceCreamDriverTests.SerialFrameCodec_RejectsCorruptChecksum | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.IceCreamDriverTests.Trigger_RejectedUpAttemptsEmergencyStop | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.IceCreamDriverTests.Trigger_RejectsFaultBeforeMotorMotion | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.IceCreamDriverTests.Trigger_DownRunsUntilLowerLimitOnly | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.IceCreamDriverTests.Trigger_UpRunsUntilUpperLimitOnly | Passed | 00:00:00.3150000 |

### Peripheral driver plugin loading

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.MachinePluginLoaderTests.DriverDirectory_UsesSharedProgramDataLocation | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.Load_MissingDirectoryIsEmptyAndSafe | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.Load_PackagedCupDroppingDriver_IsValidAndLoadable | Passed | 00:00:00.0040000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.Load_PackagedIceCreamDriver_IsValidAndLoadable | Passed | 00:00:00.0040000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.Load_RejectsTamperedDllBySha256 | Passed | 00:00:00.2140000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.Load_ValidDllPackageCreatesDriver | Passed | 00:00:00.4000000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.ValidateManifest_AcceptsSchemaOneContract | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.ValidateManifest_RejectsAssemblyTraversal | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.ValidateManifest_RejectsUnsupportedOrUnsafeContracts(schema: 1, machine: "machine", assembly: "Driver.dll", entryType: "Type", version: "1", sha: "not-a-checksum") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.ValidateManifest_RejectsUnsupportedOrUnsafeContracts(schema: 1, machine: "machine", assembly: "Driver.exe", entryType: "Type", version: "1", sha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"···) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.ValidateManifest_RejectsUnsupportedOrUnsafeContracts(schema: 2, machine: "machine", assembly: "Driver.dll", entryType: "Type", version: "1", sha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"···) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.MachinePluginLoaderTests.ValidateModule_RejectsMachineTypeUnsafeForConfigSerialization | Passed | 00:00:00.0010000 |

### Legacy local payload rejection

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.OrderRequestTests.Validate_RejectsLegacyLocalApiPayloads(json: "{\"orderId\":\"ORD-1\",\"steps\":[\"one.lua\"]}") | Passed | 00:00:00.0040000 |
| IceBot.Harness.Tests.OrderRequestTests.Validate_RejectsLegacyLocalApiPayloads(json: "{}") | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.OrderRequestTests.Validate_RejectsLegacyLocalApiPayloads(json: "not-json") | Passed | 00:00:00.6180000 |

### Peripheral device registration

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.PeripheralDeviceRegistrationTests.MachineDeviceIds_RoundTripByStableMachineType | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.PeripheralDeviceRegistrationTests.ParseRegistrationResponse_ReturnsBackendDeviceId | Passed | 00:00:00.0150000 |
| IceBot.Harness.Tests.PeripheralDeviceRegistrationTests.ParseRegistrationResponse_UsesBackendErrorMessage | Passed | 00:00:00.6200000 |

### Production report outbox

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.ProductionReportOutboxTests.Enqueue_PersistsCompletePerUnitReportWithStableSequence | Passed | 00:00:00.4750000 |

### Robot hardware discovery

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.RobotDeviceDiscoveryTests.ConfiguredDiscovery_RejectsIncompleteHardwareConfiguration | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.RobotDeviceDiscoveryTests.ConfiguredDiscovery_UsesConfiguredProfileAndOptionalBackendDeviceMapping | Passed | 00:00:00.3010000 |

### Simulated robot executor

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.SimulatedRobotWorkflowExecutorTests.Factory_UsesSimulatorOnlyWhenExplicitlyConfigured | Passed | 00:00:00.8080000 |
| IceBot.Harness.Tests.SimulatedRobotWorkflowExecutorTests.Simulator_FailsAtConfiguredStep | Passed | 00:00:00.1620000 |
| IceBot.Harness.Tests.SimulatedRobotWorkflowExecutorTests.Simulator_RunsOpaqueExistingLuaWithoutContactingRobot | Passed | 00:00:00.1700000 |
| IceBot.Harness.Tests.SimulatedRobotWorkflowExecutorTests.Simulator_AlsoSimulatesPeripheralsAndReportsNoPhysicalOutput | Passed | 00:00:00.0010000 |

### Site settings and device mapping

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.SiteSettingsTests.DeviceIdSerialization_IgnoresInvalidEntriesAndIsCaseInsensitive | Passed | 00:00:00.0400000 |
| IceBot.Harness.Tests.SiteSettingsTests.DeviceIdSerialization_SkipsEmptyIdsAndMachineTypes | Passed | 00:00:00.0020000 |
| IceBot.Harness.Tests.SiteSettingsTests.IsConfigured_RequiresBothNetBirdKeyAndPublicUrl | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.SiteSettingsTests.MachineLookups_AreCaseInsensitiveAndUnknownReturnsEmpty | Passed | 00:00:00.0130000 |
| IceBot.Harness.Tests.SiteSettingsTests.SetupCompletion_IsIndependentFromDeploymentReadiness | Passed | 00:00:00.4010000 |
| IceBot.Harness.Tests.SiteSettingsTests.ReportedDevicesSnapshot_ReusesRevisionAndObservedAtForSameContent | Passed | 00:00:00.0270000 |
| IceBot.Harness.Tests.SiteSettingsTests.ReportedDevicesSnapshot_IncrementsVersionWhenContentChanges | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.SiteSettingsTests.ReadinessSafetyProfile_UsesSafeForSimulationOrVerifiedPhysicalTelemetry | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.SiteSettingsTests.ReadinessCapabilityProfile_PhysicalModeReportsRobotOnlyWhenSafetyIsVerified | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.SiteSettingsTests.ActiveWorkflowPeripheralDiscovery_UsesTriggerDeviceMachineTypesInsteadOfArtifactFileNames | Passed | 00:00:00.0010000 |

### Typed Lua workflow plan

| Test | Result | Duration |
|---|---|---:|
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Compose_PreservesBackendArtifactOrder | Passed | 00:00:00.0240000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Compose_RejectsArtifactPathTraversal | Passed | 00:00:00.0030000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Execute_DispatchesEveryInstructionInOrder | Passed | 00:00:00.0040000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.HomeFallback_AppliesOnlyToVerifiedPointOnController37(errorCode: 143) | Passed | 00:00:00.0050000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.HomeFallback_AppliesOnlyToVerifiedPointOnController37(errorCode: -4) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.HomeFallback_RejectsUnexpectedPointOrError(pointName: "IceBot_Home", errorCode: 142) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.HomeFallback_RejectsUnexpectedPointOrError(pointName: "icebot_home", errorCode: 143) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.HomeFallback_RejectsUnexpectedPointOrError(pointName: "IceBot_Home", errorCode: -2) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.HomeFallback_RejectsUnexpectedPointOrError(pointName: "Other_Home", errorCode: 143) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_CycleLoopExpandsToFiniteInstructions | Passed | 00:00:00.0160000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_FairinoStudioOutputBuildsTypedExecutionPlan | Passed | 00:00:00.5420000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_RejectsUnsafeOrUnboundedWorkflow(lua: "local start_time = os.clock()\nwhile (os.clock() -"···) | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_RejectsUnsafeOrUnboundedWorkflow(lua: "MoveJ({1,2,3,4,5,6}, 0, 0, 0, 30, -1, -1)") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_RejectsUnsafeOrUnboundedWorkflow(lua: "os.execute(\"bad\")") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_RejectsUnsafeOrUnboundedWorkflow(lua: "SetDO(16, 1, 0)") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.Parse_RejectsUnsafeOrUnboundedWorkflow(lua: "SetToolDO(2, 1, 0, 0)") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.RuntimeHomePoint_UsesVerifiedFallbackIdentity | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.MachineTypeCanonicalizer_NormalizesKnownAliasAndWhitespace(input: "icemachine", expected: "ice_cream") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.MachineTypeCanonicalizer_NormalizesKnownAliasAndWhitespace(input: "ICE_cream", expected: "ICE_cream") | Passed | 00:00:00.0010000 |
| IceBot.Harness.Tests.WorkflowExecutionPlanTests.MachineTypeCanonicalizer_NormalizesKnownAliasAndWhitespace(input: " bt_cup_l90 ", expected: "bt_cup_l90") | Passed | 00:00:00.0010000 |

## Result

All 119 tests passed. No unit test was skipped or failed.

### STM32 actuator firmware contract

The harness validates the checked-in STM32 firmware source contract and the Edge plugin serial contract, including 0.1-second duration encoding. These tests do not energize the motor or replace physical hardware verification.

The protocol contract tests include 1.2-second (`12`) and 1.6-second (`16`) duration encodings. The latest run also validates PB0 upper-limit and PB10 lower-limit wiring contracts, 20% default PWM, directional UP/DOWN trigger routing, and limit-driven Edge behavior. Physical motor/limit-switch travel was not exercised in this host run.

| Test | Result |
|---|---|
| IceBot.Harness.Tests.IceCreamFirmwareContractTests.Firmware_MapsCommandsToMeasuredPhysicalDirections | Passed |
| IceBot.Harness.Tests.IceCreamFirmwareContractTests.Firmware_ConfiguresUpperLimitAsActiveLowExtiInput | Passed |
| IceBot.Harness.Tests.IceCreamFirmwareContractTests.Firmware_StopsAndRejectsPhysicalUpAtUpperLimitButKeepsDownAvailable | Passed |

Run evidence: `harness/IceBot.Harness.Tests/TestResults/FirmwareContractTests.trx`