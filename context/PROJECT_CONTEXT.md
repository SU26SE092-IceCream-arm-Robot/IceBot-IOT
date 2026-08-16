# IceBot-IOT Project Context

Last reviewed: 2026-08-16

## Purpose

IceBot-IOT is the store-side Edge runtime for an IceBot kiosk. It pulls Cloud commands over mTLS, installs verified Lua artifacts, executes ordered workflows on a robot, and reports durable execution evidence to IceBot-Backend.

The current demo uses one Fairino FR5 arm. Device identity and hardware reporting remain explicit so later versions can support other models or multiple robot devices without changing endpoint provisioning.

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
- Optional simulated inventory observations for Development.

Not complete or intentionally out of scope:

- Physical sensor adapter integration.
- Multi-robot production orchestration.
- Automatic certification of Lua behavior or safety.
- Cloud-to-Edge inbound order delivery.
- A built-in ice-cream-machine driver.

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

There is no canonical inbound `POST /api/orders` flow. `LocalApiServer`, `OrderRequest`, and the old `OrderQueue` were removed. Do not recreate them as a second production lifecycle.

## Installation and Startup

The canonical lifecycle is:

```text
Setup.exe -> InitIceBot.exe -> IceBot.exe
```

### Setup.exe

Owns machine installation only: prerequisites, NetBird, application files, mutable directories, ACLs, and shortcuts. It does not log in, register a kiosk/endpoint, provision mTLS, or start production.

### InitIceBot.exe

Owns technician-authorized initialization:

1. Persist the physical Kiosk Code.
2. Connect NetBird.
3. Resolve or register the kiosk.
4. Resolve or create the Full Edge execution endpoint.
5. Create/reuse the PFX and provision its fingerprint.
6. Activate the kiosk when allowed.
7. Send mTLS heartbeat and robot-device snapshot.

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
| `ICEBOT_EXECUTION_CLIENT_CERT_PASSWORD` | PFX password; environment-only |
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

The snapshot revision changes when the device signature changes. Hardware report answers what exists; readiness answers whether production can run now.

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

`FullEdgeConfigurationInstaller` owns installation. `DeploymentReportOutbox` prevents successful local installation from losing Cloud evidence during network failure.

## Order Execution and Recovery

`EdgeOrderCommandReceiver` polls Backend. `EdgeOrderInbox` validates and persists receipts by `CommandId`. `EdgeOrderExecutionQueue` creates durable jobs and serializes execution.

Invariants:

- maximum 4 production units per order;
- one worker drives the robot at a time;
- Backend supplies artifact order; Edge never reorders it;
- referenced Lua must exist and match checksum;
- duplicate delivery is idempotent by `CommandId`;
- interrupted `Running` work is not silently restarted;
- uncertain physical output requires manual intervention;
- report sequence numbers are persisted and monotonic.

`ProductionReportOutbox` persists evidence before delivery and retries Accepted, Running, Completed, Failed, and manual-intervention outcomes.

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

Simulation exercises the real inbox, durable queue, state transitions, and outbox without a physical FR5. It is Development/test evidence, not physical E2E proof.

## Peripheral Machines and Inventory

Do not equate Recipe ingredients with Edge-controlled devices.

Current physical model:

- staff prepares and loads one ice-cream mixture into one machine compartment;
- the ice-cream machine mixes/produces independently;
- Edge primarily controls the robot arm;
- only peripherals physically connected to Edge use plugin drivers;
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

`IMachineModule` provides identity and step names. `IMachineTrigger` is optional for hardware physically connected to this Edge. Core contains no device-specific protocol.

## Readiness

Readiness is operational evidence, not hardware identity. It includes storage/local-state health, report backlog, robot activity/safety evidence, queue capacity, and active deployment state where available.

Simulation may explicitly report simulated safety. Physical mode must not claim safety merely because the process runs. Missing optional sensor topology is evaluated by Backend policy and must not invalidate endpoint identity by default.

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
dotnet build .\code\IceBot-IOT.sln -c Debug --no-restore
dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj -c Debug --no-build
```

Run:

```powershell
.\code\src\IceBot\bin\Debug\net472\IceBot.exe serve
```

A real Backend plus simulated robot validates the software path through payment, dispatch, execution transitions, and completion reports. It does not validate physical robot or sensor behavior.

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
- Treat uncertain physical output as manual intervention, never blind retry.

## Technology

- C# / .NET Framework 4.7.2 Edge runtime.
- .NET 8 Windows installer.
- Fairino C# SDK and controller Lua runtime.
- HTTPS/mTLS and NetBird.
- Local filesystem durable inbox/job/outbox persistence.
