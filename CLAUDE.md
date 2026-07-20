# CLAUDE.md — IceBot-IOT

> Agent context for anyone working on this repo. The team-facing overview is `README.md`;
> the deep architecture notes are `.env/PROJECT_CONTEXT.md` (gitignored, local only). Read those for detail.

## What this is

Control app for a Fairino **FR5** robot arm + peripheral machines (cup dropper, ice cream,
topping) that makes ice cream to order. Runs on a Windows edge PC at the store.
**C# / .NET Framework 4.7.2 console app** (`net472`, not .NET Core — run the `.exe`, not `dotnet run`).

## Build / run / test

```bash
dotnet build code/IceBot-IOT.sln -c Debug          # build (Fairino SDK builds too)
code/src/IceBot/bin/Debug/net472/IceBot.exe        # interactive menu
code/src/IceBot/bin/Debug/net472/IceBot.exe serve  # local HTTP API on :5080 (headless)
dotnet test harness/IceBot.Harness.Tests           # unit tests for pure logic (xunit, net472)
```

- Real robot/serial runs need hardware (arm @ `192.168.58.2`, COM ports). Without it you can
  still: build, `dotnet test`, and `serve` (`GET /health`, `POST /api/orders`).
- Interactive menu calls `Console.Clear()` — wrapped in `SafeClear()` so piped/redirected runs
  don't crash. Drive the menu by piping choices, e.g. `printf '2\n\n7\n' | IceBot.exe`.

## Key invariants (don't break these)

- **Every `.lua` step belongs to exactly one machine identifier** (`MachineRegistry`). No
  step is machine-less.
- **`IMachineModule`** = every machine (identity: `MachineType`, `DisplayName`, `Position`,
  `StepNames`). **`IMachineTrigger : IMachineModule`** = only machines wired over serial
  (adds `Trigger(comPort)`). Adding a machine = new module under `Machines/<Name>/` + one line
  in `MachineRegistry.Modules`; nothing else changes.
- **`robot_home` is a teaching point saved on the robot controller (Fairino app), NOT a `.lua`
  file.** `FairinoLuaExecutor.MoveToTeachingPoint` reads it via SDK `GetRobotTeachingPoint` +
  `MoveJ`. `WorkflowRunner` returns there at the start and end of every queue run.
- **Peripheral trigger fires AFTER the step's `.lua` runs** (arm moves into position first,
  then IceBot sends the serial command). They are not alternatives.
- Each production `.lua` is a simple start→end path (no round-trip), so chaining files
  back-to-back is naturally continuous — no merge step needed. `workflow/lay_coc.lua` is a
  round-trip DEMO, not a template.
- **`SerialFrameCodec` checksum/framing is safety-critical** — a wrong byte silently corrupts a
  hardware command. Covered by harness tests against the documented example
  (`04 07 aa 01 00 B6 ff`). Run `dotnet test` after touching it.

## Biggest open gap (see `.env/PROJECT_CONTEXT.md` → "mắt xích còn thiếu")

Sending an order does NOT run the robot yet. `POST /api/orders`
(`Networking/LocalApiServer.cs`, ~line 82) only logs the body and returns 202. Missing:
(1) order DTO, (2) order→step-name mapper, (3) wire it to
`WorkflowQueueBuilder.BuildQueue` → `WorkflowRunner.RunQueue`. The run pieces already work;
nothing connects an incoming order to them.

## Conventions

- Console output is in Vietnamese (no diacritics), matching existing code.
- Commit/push only when asked. Repo: `SU26SE092-IceCream-arm-Robot/IceBot-IOT`, branch `main`.
