# IceBot-IOT Project Context

Last reviewed: 2026-09-07

## Purpose

IceBot-IOT is the store-side Edge runtime for an IceBot kiosk. It pulls Cloud commands over mTLS, installs verified Lua artifacts, executes ordered workflows on a robot, and reports durable execution evidence to IceBot-Backend.

The current demo uses one Fairino FR5 arm. Device identity and hardware reporting remain explicit so later versions can support other models or multiple robot devices without changing endpoint provisioning.

The runtime home identity is `IceBot_Home`. Fairino controller Web 3.7.7 may return `143` or `-4` for its named-point query, so `FairinoLuaExecutor` contains the verified controller coordinates as a narrowly scoped fallback for that exact identity. Workflows move home before and after execution.

## Current Scope

Implemented:

- Windows installation through `Setup.exe`.
- Technician initialization through `InitIceBot.exe`.
- Production runtime through `IceBot.exe`.
- Operator login for management actions.
- Kiosk and Full Edge execution-endpoint registration.
- Client certificate creation and mTLS provisioning.
- Heartbeat, robot-device snapshot, and readiness uplink.
- Deployment pull, bundle verification, activation, and durable reports.
- Durable `ExecuteOrder` pull, inbox, serialized execution, and report outbox.
- Real Fairino and explicit simulated robot executors.
- Optional external peripheral drivers loaded as validated plugins.
- Built-in Ice Cream machine driver with directional UP/DOWN limit-controlled operation.
- Optional simulated inventory observations for Development.
- Cloud-to-Edge production order delivery through outbound mTLS polling, validated in the integrated hardware deployment.

Not complete or intentionally out of scope:

- Physical sensor adapter integration.
- Multi-robot production orchestration.
- Automatic certification of Lua behavior or safety.
- Cloud-to-Edge inbound order delivery.


## Canonical Runtime Flow

```text
Customer checkout/payment in Backend
  -> Backend creates ExecuteOrder for Kiosk + ExecutionEndpoint + active release
  -> Edge pulls commands over outbound mTLS
  -> Edge validates identity, expiry, release, hardware envelope and checksums
  -> Edge persists immutable receipt and durable production job
  -> Edge ACKs Accepted
  -> one worker executes one production unit at a time
  -> Edge persists and reports Accepted/Running/terminal evidence
  -> Backend applies terminal evidence and completes the order
```

Cloud-to-Edge order delivery is implemented through Edge-initiated outbound mTLS polling. There is no canonical inbound `POST /api/orders` flow. `LocalApiServer`, `OrderRequest`, and the old `OrderQueue` were removed. Do not recreate them as a second production lifecycle.

## Installation and Startup

The canonical lifecycle is:

```text
Setup.exe -> InitIceBot.exe -> IceBot.exe
```

### Setup.exe

Owns machine installation only: prerequisites, NetBird, immutable application files, empty mutable directories, ACLs, shared drivers, and shortcuts. Packaging excludes build-machine `config`, certificates, data, drivers, and downloaded workflows; installation/upgrade is rejected while IceBot or InitIceBot is running. Bundled peripheral packages are installed under a canonical directory named by manifest `machineType`; upgrades remove legacy directories that declare the same `machineType`, while unrelated third-party packages are preserved. Setup does not log in, register a kiosk/endpoint, provision mTLS, or start production.

### InitIceBot.exe

Owns technician-authorized initialization:

1. Persist the physical Kiosk Code.
2. Connect NetBird.
3. Confirm robot identity/profile and configure one COM port per installed peripheral trigger.
4. Resolve or register the kiosk.
5. Resolve or create the Full Edge execution endpoint.
6. Create/reuse the DPAPI-protected PFX and provision its fingerprint.
7. Activate the kiosk when allowed.
8. Send mTLS heartbeat and robot-device snapshot.
The technician configuration UI is task-based:

```text
Configuration
  -> First-time Edge setup / resume
  -> Connectivity: Backend URL, NetBird, mTLS checks and certificate status
  -> Devices: robot profile, peripheral registration, COM mapping and hardware report
  -> Configuration status
  -> Advanced recovery: deployment sync, hardware/report retry, heartbeat/readiness
```

Manual deployment synchronization and report flushing are recovery/diagnostic actions. They do not replace the unattended production command receiver and durable retry loops. Setup completion requires Kiosk, endpoint/runtime identities, HTTPS Backend URL, NetBird, an existing client PFX, and robot identity/profile. Production readiness additionally requires an active deployment/release/checksum and an existing active workflow directory. Backend-issued identities are not edited in normal settings menus.

Endpoint identity provisioning must not depend on hardware compatibility. An endpoint may authenticate before its first hardware report.

### IceBot.exe

Owns unattended production:

- reconnect NetBird;
- start the mTLS command receiver;
- start the serialized execution worker;
- retry durable deployment and production reports;
- send operational uplinks;
- continue without a human JWT.

Operator tokens are only for explicit human management actions. Automatic traffic uses the execution endpoint's mTLS identity.

## Identity and Network

The Kiosk and Edge PC are currently one physical installation, but identities remain separate:

- `KioskId`: commercial/operational kiosk.
- `ExecutionEndpointId`: command-delivery endpoint.
- `FullEdgeRuntimeId`: stable runtime profile.
- client PFX: machine credential bound to the endpoint.

Cloud communication is initiated by Edge over HTTPS/mTLS. NetBird provides private connectivity; it is not an IceBot business API.

| Setting | Meaning |
|---|---|
| `BE_API_URL` / `ICEBOT_BE_API_URL` | Backend HTTPS base URL |
| `NETBIRD_SETUP_KEY` | NetBird enrollment key |
| `KIOSK_CODE` | Physical kiosk code |
| `KIOSK_ID` | Backend kiosk identity |
| `EXECUTION_ENDPOINT_ID` | Edge command endpoint |
| `FULL_EDGE_RUNTIME_ID` | Stable runtime identity |
| `EXECUTION_CLIENT_CERT_PATH` | Local PFX path |
| `ICEBOT_EXECUTION_CLIENT_CERT_PASSWORD` | Optional PFX password override; otherwise a random password is protected by Windows DPAPI |
| PFX key storage | `UserKeySet | Exportable` for .NET Framework/Windows Schannel client authentication; never `EphemeralKeySet` |
| `ROBOT_IP` / `ICEBOT_ROBOT_IP` | Robot IP; default `192.168.58.2` |

If a public proxy does not forward client certificates, use a private HTTPS Backend URL reachable through NetBird.

## Robot Hardware Reporting

Hardware ownership:

- Edge discovers or reads actual robot configuration.
- Edge reports robot devices after mTLS authentication.
- Backend stores the reported snapshot for compatibility decisions.
- Operators do not configure legacy `supported-robot-targets` during provisioning.

Current provider: `ConfiguredRobotDeviceDiscovery`.

Current single-device defaults:

```text
SourceDeviceKey: arm-primary
RuntimeTargetCode: FAIRINO_LUA_V1
MachineModelCode: FR5
```

These are demo defaults, not registration constants. A later provider can report FR3, CR5, another runtime, or multiple devices without changing provisioning.

The snapshot revision and `observedAt` change together when the device signature changes. Retries of unchanged content reuse both values so Backend treats reconnect delivery as idempotent instead of rejecting the same revision with different content. Hardware report answers what exists; readiness answers whether production can run now.

## Lua and Compatibility Boundary

Lua is a behavioral black box:

- Cloud does not parse or certify behavior.
- Cloud does not prove Lua matches Recipe quantities, toppings, or safe motion.
- The uploader remains responsible for program content.

Cloud and Edge still enforce a declared envelope:

- artifact runtime target and machine model;
- Edge-reported runtime/model;
- artifact identity, presence, size, and SHA-256.

This proves declared routing compatibility and byte integrity only. It does not prove that metadata truthfully describes Lua behavior. Deployment installation and order acceptance reject unknown or incompatible reported hardware; Edge is the final gate before loading artifacts.

## Deployment Flow

```text
Published release
  -> deployment command for ExecutionEndpoint
  -> Edge pulls over mTLS
  -> validate endpoint/release/payload/hardware envelope
  -> download presigned bundle
  -> verify size and SHA-256
  -> stage and atomically activate workflow
  -> persist active deployment/release/checksum
  -> durable Installed and Active reports
```

`FullEdgeConfigurationInstaller` owns installation. `DeploymentReportOutbox` prevents successful local installation from losing Cloud evidence during network failure. Deployment acknowledgements omit `physicalOutputMayHaveOccurred`; Backend permits that evidence only for a rejected `ExecuteOrder`, where Edge reports `false` before any production starts. Deployment outbox delivery is ordered by durable `SequenceNumber`, preserving the required `Installed` then `Active` transition even though filenames sort differently.

## Order Execution and Recovery

`EdgeOrderCommandReceiver` polls Backend. `EdgeOrderInbox` validates and persists receipts by `CommandId`. `EdgeOrderExecutionQueue` creates durable jobs and serializes execution.

Invariants:

- maximum 4 production units per order;
- one worker drives the robot at a time;
- Backend supplies artifact order; Edge never reorders it;
- referenced Lua must exist and match checksum;
- duplicate delivery is idempotent by `CommandId`;
- durable jobs left at `AwaitingAck` replay the idempotent Backend ACK after restart before queue activation;
- after staff removes the interrupted product and checks the workcell, manually launching `IceBot.exe` authorizes restarting the interrupted `Running` unit from its beginning;
- persisted `Completed` units are never remade; physical completion without persisted `Completed` is treated as incomplete;
- each interrupted local attempt is retained on the unit with its start/detection time; retries keep the same cloud production identity and do not emit a terminal interruption report;
- existing `Failed` or legacy `RequiresManualIntervention` jobs remain blocked; this policy handles process interruption, not arbitrary terminal failures;
- report sequence numbers are persisted and monotonic.

`ProductionReportOutbox` persists evidence before delivery and retries Accepted, Running, Completed, Failed, and manual-intervention outcomes.

Startup recovery records interrupted attempts and selects the first remaining pending unit. For four units with the first two durably completed, it remakes unit three and then executes unit four. The operator must clear the failed product, verify robot/peripheral starting conditions and the existing Home movement path before manually launching the executable. Do not configure Windows startup, service recovery, or an external launcher to automatically restart production. There is no confirmation button or step-level resume.

Before a unit starts, Edge rechecks Lua checksums, composes the execution plan, probes Fairino safety telemetry and calls required peripheral drivers' read-only connection tests. These tests do not prove the physical workcell is clear or that a peripheral is in a safe starting position. A failed preflight leaves the unit pending and retries the check without physical execution. A global named mutex prevents concurrent `IceBot.exe` processes.

Job and production-outbox writes flush file contents to disk before replacement/publication. Completion stores the immutable report envelope with the job before publishing it to the outbox; interrupted publication replays the same event ID, sequence and payload. Local interruption history is stored in `data/order-jobs/*.json`. Session startup, clean shutdown and catchable crashes are logged in `data/logs/runtime-events.jsonl`; `session-state.txt` detects an unclean previous session, without asserting whether the cause was power loss or a process crash. No live hardware recovery validation is implied by unit tests.

## Robot Executors

`IRobotWorkflowExecutor` isolates orchestration from execution mode.

### Fairino

`FairinoLuaExecutor` connects to the configured robot, uploads and runs each Lua artifact, and waits for completion. `WorkflowRunner` executes Backend order and handles the home teaching point around each production unit. IceBot-IOT does not generate or rewrite production Lua.

### Simulated

```powershell
$env:ICEBOT_ROBOT_EXECUTION_MODE = "Simulated"
$env:ICEBOT_SIMULATED_STEP_DELAY_MS = "150"
$env:ICEBOT_SIMULATED_FAIL_STEP = "0"
```

Simulation exercises the real inbox, durable queue, state transitions, and outbox without a physical FR5. In this mode readiness reports an available `ROBOT_ARM` capability at `ARM_PRIMARY` and `safety=Safe`, so a production release requiring that capability is dispatched through the same Backend contract as the physical path. The runtime computes the execution mode once per readiness request and logs the exact `safety` and `mode` transmitted for diagnosis. In Fairino mode, every readiness probe opens a read-only SDK session and reports `Safe` plus `ROBOT_ARM` only when SDK communication is healthy, E-stop is clear, SI0/SI1 are clear, and both robot error codes are zero. Any failed telemetry read becomes `Unknown`; E-stop, safety-stop, or a robot error becomes `Unsafe`; neither state advertises a robot capability. Serial connection tests discover `TriggerDevice` machine types by parsing the active Lua files, because Full Edge artifact filenames are UUIDs rather than machine names; they call only the driver `TestConnection` method. `TriggerDevice` validates that a matching plugin exists but does not open the configured COM port or call the hardware driver in simulation; completed/failed evidence reports `physicalOutputMayHaveOccurred=false`. Production Lua may use the legacy `icemachine` identifier, which is canonicalized to the installed `ice_cream` driver. The automated lifecycle test validates checksum, immutable receipt, durable admission, ACK recovery, ordered plan execution, and `Accepted -> Running -> Completed` reports. It is Development/test evidence, not physical E2E proof.

## Peripheral Machines and Inventory

Do not equate Recipe ingredients with Edge-controlled devices.

Current physical model:

- staff prepares and loads one ice-cream mixture into one machine compartment;
- the ice-cream machine mixes/produces independently;
- Edge primarily controls the robot arm;
- only peripherals physically connected to Edge use Edge plugin drivers; each such peripheral must provide a documented serial transport and device-control protocol; transport may be RS232 or RS485 according to the device protocol;
- optional sensors may report Cloud-owned dispenser state.

Therefore:

- three Recipe ingredients do not imply three devices;
- an independent machine is not an Edge-controlled peripheral;
- sensor topology is optional and must not block sensorless installations;
- no sensor means inventory is Unknown/manual, not automatically OutOfStock;
- simulated inventory emulates a sensor gateway only in Simulated mode;
- simulation references existing Backend `IngredientDispenserStateId` and `DeviceId`; Edge does not invent Cloud topology.

External peripheral packages live under:

```text
C:\ProgramData\IceBot\drivers\<driver-name>\
  driver.json
  Vendor.Driver.dll
```

`IMachineModule` provides identity and step names. `IMachineTrigger` is optional for hardware physically connected to this Edge. An Edge-controlled serial peripheral must provide a documented serial transport, a documented device-control protocol, and an Edge plugin driver. The transport may be RS232 or RS485 according to the device; device commands, frame formats, status codes, and checksums may be device-specific. Core contains no device-specific protocol.

## Readiness

Readiness is operational evidence, not hardware identity. It includes storage/local-state health, report backlog, robot activity/safety evidence, queue capacity, and active deployment state where available.

Simulation may explicitly report simulated safety. Physical mode must not claim safety merely because the process runs; it must instead receive a healthy Fairino SDK sample with E-stop, SI0/SI1, and robot errors all clear. Missing optional sensor topology is evaluated by Backend policy and must not invalidate endpoint identity by default.

## Repository Map

| Path | Responsibility |
|---|---|
| `code/src/IceBot/Api/Authentication/` | Human login and refresh |
| `code/src/IceBot/Api/Management/` | Technician setup APIs |
| `code/src/IceBot/Api/IoT/EdgeMtlsProbe.cs` | Heartbeat, hardware, readiness, simulated inventory |
| `code/src/IceBot/Api/EdgeDeploymentApi.cs` | mTLS pull, ACK and reports |
| `code/src/IceBot/Config/Setup/` | Initialization orchestration |
| `code/src/IceBot/Config/Storage/` | Site config and persisted counters |
| `code/src/IceBot/Robot/Hardware/` | Robot discovery/report model |
| `code/src/IceBot/Robot/` | Real and simulated executors |
| `code/src/IceBot/Workflow/Provisioning/` | Deployment install/report outbox |
| `code/src/IceBot/Workflow/Orders/` | Receiver, inbox, jobs, worker, report outbox |
| `code/src/IceBot/Machines/` | Driver plugin loader and registry |
| `code/src/IceBot.Driver.Abstractions/` | Public plugin contracts |
| `harness/IceBot.Harness.Tests/` | Automated Edge tests |

## Build and Verification

```powershell
.\code\scripts\restore-fairino-sdk-dependencies.ps1
dotnet build .\code\IceBot-IOT.sln -c Release --no-restore
dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj -c Release --no-restore
# Latest verified result: 130 passed, 0 failed, 0 skipped
```

Run:

```powershell
.\code\src\IceBot\bin\Debug\net472\IceBot.exe serve
```

A real Backend plus simulated robot validates the software path through payment, dispatch, execution transitions, and completion reports. It does not validate physical robot or sensor behavior.

## Ice-cream Machine Actuator and Limit Switches

The custom ice-cream actuator uses a 24 V, 100 RPM JGB37-520 geared DC motor connected to an 8 mm lead screw with a 2 mm lead per revolution.

The firmware has been corrected and flashed through ST-Link to the STM32F103C8T6. The actuator uses separate active-low limit inputs: `PB0` for the upper limit and `PB10` for the lower limit. Its current direction mapping matches the physical mechanism:

- Firmware `UP` produces physical UP motion through `PB1/TIM3_CH4`.
- Firmware `DOWN` produces physical DOWN motion through `PA8/TIM1_CH1`.

The original reversed mapping was observed during the initial manual calibration and is historical information only.

The STM32 firmware uses a default PWM of 20% when a command sends speed `0`. Motor command duration is encoded in 0.1-second units: `12` means 1.2 seconds and `16` means 1.6 seconds. A duration of `0` means run until the matching limit switch. The Edge ice-cream driver sends duration `0` and waits for the machine to return to Standby after the matching limit is reached. An Edge `UP` trigger runs only physical UP until the upper limit; an Edge `DOWN` trigger runs only physical DOWN until the lower limit. The driver never performs an automatic opposite-direction movement. `context/lua-tests/real-demo-1408.lua` currently sends `TriggerDevice("ice_cream", "UP")`; a DOWN movement requires a separate explicit `TriggerDevice("ice_cream", "DOWN")`. Workflow execution is synchronous, so the next step starts only after the current machine trigger returns, followed by any explicit `WaitMs` in the workflow.

The manually calibrated physical travel from LOW to HIGH is approximately 8 seconds at 20% PWM, corresponding to an estimated 5.33 mm of linear travel. Time-based positioning is only an estimate and must not be treated as absolute position feedback.

A `KW12-071` mechanical limit switch is used for the physical upper limit, and the lower limit uses the same active-low topology on `PB10`:

- STM32 `PB0/B0` connects directly to the upper switch `COM`; STM32 `PB10` connects directly to the lower switch `COM`.
- Each switch `NO` connects to STM32 GND; `NC` is left unwired and insulated.
- Released means GPIO HIGH (inactive); pressed means GPIO LOW (active). Earlier notes saying NC-to-GND were incorrect for this active-low, press-to-stop setup.
- A `4.7 kΩ` pull-up resistor connects STM32 `3.3 V` to `PB0/B0`; a second `4.7 kΩ` pull-up connects `3.3 V` to `PB10`.
- The switch belongs entirely to the STM32 control power domain.
- It must not connect to the 24 V motor domain, BTS7960 ground, `A8`, or `B1`.

Firmware configures both `PB0/EXTI0` and `PB10/EXTI10` as pull-up GPIO inputs with interrupts on both edges. The GPIO callback handles both pins, and the main loop also polls both inputs. An active matching limit stops motion and rejects further commands in that direction; the opposite direction remains available to release the switch. Releasing a switch does not restart the motor automatically, but a new command in that direction is allowed. A limit event is not a permanent latched lockout.

The updated firmware is flashed to the STM32. The upper switch provides automatic stopping and physical-UP protection; the lower switch provides automatic stopping and physical-DOWN protection. Each opposite direction remains available to release the corresponding switch.

### Hardware verification on 2026-09-07

- The Release firmware was flashed through ST-Link and passed Flash readback verification before MCU reset.
- Direct GPIO reads confirmed both switches: released HIGH, held LOW, then HIGH after release.
- Manual RS485 tests used 115200 baud, 8N1, PWM 20%, and duration `0`. COM7 was the port on the test PC, not a deployment default. UP and DOWN require separate explicit commands; send STOP before changing direction.
- The operator confirmed successful physical UP/DOWN motion and successful real-hardware testing after exercising the limit switches. STOP replies and subsequent Standby status were verified over RS485. This is manual actuator validation, not an automated robot/order E2E test.
- During DOWN, ST-Link showed TIM1 and its channel output enabled, CCR1 = 199 with ARR = 999 (approximately 20% PWM), the UP channel at zero, and PB10 HIGH. Protocol Busy is derived from `currentDir`; it is not measured motor movement or driver feedback.
- A repeated command after pressing and releasing a switch was accepted, as intended. The conversation did not capture a rejected command while the matching switch was continuously held; do not describe that as physically verified.
- Touching the exposed NC terminal was reported to stop motion. The cause (input transient, reset, or contact issue) was not isolated, despite a reported 4.7 kOhm pull-up. Insulate unused terminals and test with the mechanical lever; do not claim this susceptibility has been fixed.

## Session Context Loading

At the beginning of every new session, load both context files before making changes:

1. `context/GLOBAL_WORKING_CONTEXT.md` for universal working rules.
2. `context/PROJECT_CONTEXT.md` for IceBot-IOT project-specific scope, architecture, hardware, and verification requirements.

The global context is shared across projects and must not be modified for project-specific notes. Project-specific updates belong in this file.
## Unit Test Location and Procedure

The project harness and test evidence are located here:

For actuator firmware direction, limit-switch, or Edge trigger changes, the contract tests must cover command direction mapping, PB0/PB10 active-low GPIO/EXTI configuration, upper/lower-limit stopping, matching-direction rejection, and opposite-direction release behavior.


- Test project: `harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj`.
- Firmware contract tests: `harness/IceBot.Harness.Tests/IceCreamFirmwareContractTests.cs`.
- Unit-test report: `testing/UNIT_TEST_REPORT.md`.
- Test result evidence: `harness/IceBot.Harness.Tests/TestResults/LimitExtiRegression.trx` and `harness/IceBot.Harness.Tests/TestResults/LimitExtiFull.trx` (local test artifacts).

When any project code changes, including Edge runtime, API, configuration/provisioning, deployment, order execution, robot workflow, peripheral driver, or STM32 firmware code changes:

1. Identify the affected unit-test suites and add or update tests for the changed behavior.
2. Run `dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj -c Release --no-restore`.
3. For actuator firmware changes, the contract tests must cover command direction mapping, PB0/PB10 active-low GPIO/EXTI configuration, upper/lower-limit stopping, matching-direction rejection, and opposite-direction release behavior.
4. Record total, passed, failed, skipped counts, scope, command, and evidence path in `testing/UNIT_TEST_REPORT.md`.
5. For Edge code changes, run the directly affected suites first (for example `EdgeOrderInboxTests`, `EdgeOrderExecutionQueueTests`, `EdgeDeploymentApiTests`, `SiteSettingsTests`, `WorkflowExecutionPlanTests`, `IceCreamDriverTests`, or the matching suite), then run the full harness before handoff.
6. Physical STM32, motor, limit-switch, ST-Link, and serial behavior still require cautious hardware verification; host unit tests do not replace it.

## Protocol Reference Documents

Detailed serial protocol definitions are maintained separately under `context/protocols/` and should not be duplicated in this project context:

- Ice Cream machine: `context/protocols/Ice Cream Machine Serial Communication Protocol.md`
- Cup-dropping machine: `context/protocols/301 Cup-Dropping Machine Serial Communication Protocol V0.0.3.md`

## Architecture Reference Documents

- Hardware Architecture: `context/Hardware_Architecture/Hardware_Architecture_Document.md`
- Hardware Architecture notes: `context/Hardware_Architecture/note.md`
- System Architecture: `context/System_Architecture/system architecture.md`
- System Architecture diagrams: `context/System_Architecture/System Architecture.jpg` and `context/System_Architecture/System Architecture_2.jpg`

Open the relevant protocol document when changing firmware, peripheral drivers, serial tests, or hardware wiring. Keep this context limited to project scope, implementation decisions, and verification rules.

## Lua Test Files

Project Lua files intended for testing are stored in:

`context/lua-tests/`

This directory currently contains test/demo variants such as `real-demo-1408.lua`, `pre-review.lua`, and machine-trigger scenarios. Use files in this directory as controlled workflow inputs for parser, plan-validation, Edge execution, and machine-trigger tests. Keep production workflow files separate from test variants. When a Lua test changes, run the directly affected workflow/Edge test suites and then the full harness; record the result in `testing/UNIT_TEST_REPORT.md`.

## Rules for Future Changes

- Keep one production lifecycle: outbound mTLS pull.
- Do not restore the inbound order API.
- Do not couple hardware reporting to identity provisioning.
- Do not hard-code FR5 in registration; keep it as a discovery/config default.
- Do not treat runtime/model declarations as Lua certification.
- Do not infer devices from Recipe ingredients.
- Keep sensor topology optional.
- Persist state/evidence before acknowledging irreversible work.
- Keep robot execution serial until multi-customer operation is explicitly designed.
- Keep README, this project context, unit tests, and `testing/UNIT_TEST_REPORT.md` synchronized with behavior changes.
- For interrupted production, staff must clear the product and check the workcell before manually restarting IceBot.exe; restart then authorizes remaking the incomplete unit. Never bypass this operating procedure through unattended process restart.

## Technology

- C# / .NET Framework 4.7.2 Edge runtime.
- .NET 8 Windows installer.
- Fairino C# SDK and controller Lua runtime.
- HTTPS/mTLS and NetBird.
- Local filesystem durable inbox/job/outbox persistence.
