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
code/src/IceBot/bin/Debug/net472/IceBot.exe        # default: serve mode
code/src/IceBot/bin/Debug/net472/IceBot.exe menu   # interactive administration menu
code/src/IceBot/bin/Debug/net472/IceBot.exe serve  # local HTTP API on :5080 (headless)
dotnet test harness/IceBot.Harness.Tests           # unit tests for pure logic (xunit, net472)
```

- Real robot/serial runs need hardware (arm @ `192.168.58.2`, COM ports). Without it you can
  still: build, `dotnet test`, and `serve` (`GET /health`, `POST /api/orders`).
- The interactive menu (`IceBot.exe menu`) calls `Console.Clear()` — wrapped in `SafeClear()` so piped/redirected runs
  don't crash. Drive the menu by piping choices to `IceBot.exe menu`.
  (Login is required first — pipe account/password lines before menu choices when scripting.)

## Key invariants (don't break these)

- **Every `.lua` step belongs to exactly one machine identifier** (`MachineRegistry`). No
  step is machine-less.
- **`IMachineModule`** = every machine (identity: `MachineType`, `DisplayName`,
  `StepNames`). **`IMachineTrigger : IMachineModule`** = only machines wired over serial
  (adds `Trigger(comPort)` **and** `TestConnection(comPort)` — both mandatory on the interface).
  `TestConnection` opens the port and does a real query round-trip, throwing on failure; it
  backs the "Test may > 2 Test ket noi may ngoai vi (Serial)" menu item, which loops
  `SiteSettings.ProvisionedSteps` (see "Provisioned steps" below), resolves each via
  `MachineRegistry.TryGetModule`, and calls `TestConnection` generically — a newly added machine
  gets connection-checking for free once it's provisioned. Adding a machine = new module under
  `Machines/<Name>/` + one line in `MachineRegistry.Modules`; nothing else changes.
- **Business rule: every peripheral machine triggers over RS485, and only RS485.** No DO/24V
  trigger fired from the Fairino control box — that path is gone. Any new machine needs its own
  RS485 head + a host-side `<Name>Client.cs` driver + `<Name>Module.cs` implementing
  `IMachineTrigger` (see `Machines/CupDropping/` or `Machines/IceCream/`). A machine with no
  separate hardware to control (pure arm motion, e.g. a tray-placement position) is exempt —
  it implements only `IMachineModule`, no `IMachineTrigger`.
- **`robot_home` is a teaching point saved on the robot controller (Fairino app), NOT a `.lua`
  file.** `FairinoLuaExecutor.MoveToTeachingPoint` reads it via SDK `GetRobotTeachingPoint` +
  `MoveJ`. `WorkflowRunner` returns there at the start and end of every queue run.
- **Peripheral trigger fires AFTER the step's `.lua` runs** (arm moves into position first,
  then IceBot sends the RS485 command). They are not alternatives.
- Each production `.lua` is a simple start→end path (no round-trip), so chaining files
  back-to-back is naturally continuous — no merge step needed. `workflow/lay_coc.lua` is a
  round-trip DEMO, not a template.
- **`SerialFrameCodec` checksum/framing is safety-critical** — a wrong byte silently corrupts a
  hardware command. Covered by harness tests against the documented example
  (`04 07 aa 01 00 B6 ff`). Run `dotnet test` after touching it.

## Order → robot wiring

`POST /api/orders` (`Networking/LocalApiServer.cs`) is wired end-to-end. **BE decides which
`.lua` files an order needs AND the exact order to run them in** — it sends both in the
request body (`OrderRequest`: `orderId` + `steps` = ordered list of `.lua` file names).
IceBot does **not** map order contents (flavor/topping/qty) to step names and does **not**
reorder `steps` — there is no machine-position/ordering logic left in IceBot at all (order
is already correct as sent). IceBot's job is only: validate every named file exists in the
local `workflow/` folder (400 `missing_lua_file` if not), then hand `steps` as-is to
`OrderQueue.Enqueue` → a single background worker thread calls `WorkflowRunner.RunQueue` (kept
off the HTTP thread, and serialized — the arm can't run two orders at once). Still open:
POSTing completion/failure status back to BE.

## Store login

Each store has its own BE account (`STORE_ACCOUNT`/`STORE_PASSWORD` in `config/icebot.site.env`,
set via 1.2 or entered inline at the login gate). "1.2" here means: main menu item 1 ("Cau
hinh"), then item 2 in that submenu ("Cau hinh he thong") — each submenu renumbers from 1 on
screen (no literal "1.2" keystroke; see `Cli/ConsoleMenu.cs`). Note this is a **different**
submenu item than NetBird's own config (1.1, "Cau hinh NetBird") — `ConfigSetupWizard` is split
into `RunNetBird()` (NetBird setup key + Public URL only) and `RunSystemSettings()` (API key,
robot IP, store account/password, COM ports) precisely so these two concerns don't live in one
combined prompt anymore.
**Login is mandatory to reach the menu or `serve` mode** — `ConsoleMenu.Run()` and
`ConsoleMenu.RunServeMode()` both call `StoreAuth.RequireLogin()` first thing on startup, which
**loops, blocking, until `BeApi.Login` succeeds** (mock — `Api/BeApi.cs`); prompts inline for
account/password if not already configured (1.2 is not a prerequisite), typing `exit` quits the
app instead of retrying.
`RequireLogin()` only actually gates once per process (`StoreAuth._loggedInThisRun`) — entering
the menu logs in once, and picking "3. Chay he thong" from inside the menu does not ask again;
running `IceBot.exe serve` directly (a fresh process, no menu) still gates normally. On
success the key is saved as `BE_SESSION_KEY` (`Api/StoreAuth.cs`, read back via
`AppConfig.BeSessionKey`). `IceBot.exe login` (CLI-only — not a menu item) is a separate
**non-blocking, single-attempt** re-login for mid-session use (e.g. after changing the account
in the config wizard) — it does not gate anything. Token expiry/refresh (access+refresh token) is an explicit TODO, not handled yet;
a successful mock login is currently sufficient. `BE_SESSION_KEY` is the **opposite direction**
from `API_KEY`: `API_KEY` authenticates inbound BE→Edge requests (checked in
`LocalApiServer.Authorize`); `BE_SESSION_KEY` is what Edge will attach to outbound Edge→BE
requests once `BeApi` talks to a real BE over HTTP — no such outbound call exists yet, so
nothing consumes the key today beyond storing it. Changing `STORE_ACCOUNT`/`STORE_PASSWORD` in
the wizard clears the stale key.

## Provisioned steps & robot test sample

`WorkflowProvisioner.FetchAndSave` records every `.lua` file name BE actually returned (step
name = file name minus extension) into `SiteSettings.ProvisionedSteps` (persisted,
`PROVISIONED_STEPS` in `config/icebot.site.env`, deduped, accumulates across runs — even when
the operator types a bundle keyword like `FR5`/`full`, since it's the *resolved* file list that
gets saved, not the raw typed model string). This is the list "Test may > 2 Test ket noi may
ngoai vi" iterates — each entry resolved via `MachineRegistry.TryGetModule` down to an
`IMachineTrigger`, so the connection check only covers machines this specific store actually
provisioned, not every machine type ever coded into `MachineRegistry.Modules`.

"Test may > 1 Test tay Robot" is arm-only, two independent steps: (1) `FairinoLuaExecutor.Connect`
plain OK/fail, (2) load+run a sample `.lua` from `test-workflow/` (constant
`AppConfig.TestSampleScriptName` = `robot_test.lua`) via `WorkflowRunner.RunQueue`'s 3-arg
overload (explicit directory). **`test-workflow/` is deliberately separate from `workflow/`** —
`workflow/` only ever holds BE-downloaded files (gitignored); `test-workflow/` is a small
tracked-in-git folder for a fixed sample script the user supplies, used only for exercising the
arm/upload/run pipeline, independent of any real order content. Missing sample file → the step
is skipped with a message, not an error.

## Ingress: NetBird (replaced DuckDNS + Cloudflare Tunnel)

The store no longer uses DuckDNS + Cloudflare Tunnel for cloud→edge ingress — **NetBird**
(the real https://netbird.io CLI/product — mind the spelling, not "NextBird") replaces both
(dynamic DNS *and* the tunnel itself, a WireGuard-based mesh network). IceBot's side of this is
a single secret: `NetBirdSetupKey` (`NETBIRD_SETUP_KEY` in `config/icebot.site.env`, prompted as
"NetBird setup key" in menu Cau hinh > 1). Unlike `BeApi` (mock, no real HTTP yet), NetBird is a
real installed CLI and IceBot **actively shells out to it** via `Config/NetBirdSetup.cs`:
- `NetBirdSetup.RunUp(setupKey, out message)` resolves the `netbird` executable
  (`ResolveExecutable()` — bare `"netbird"` if it resolves via this process's PATH, else checks
  the known install path `%ProgramFiles%\Netbird\netbird.exe` directly; this second check
  matters because a process that outlives an installer never sees the installer's PATH update).
  **If NetBird isn't found on this machine at all, it's installed automatically** via
  `winget install --id Netbird.Netbird --silent --accept-package-agreements
  --accept-source-agreements` (3 min timeout) before continuing — this is the "may Edge tu dong
  cai NetBird" behavior; the operator never installs it or runs `netbird up` by hand. Then it
  runs `netbird up --setup-key <key>` (1 min timeout) as a child process and surfaces NetBird's
  own stdout/stderr as the result message.
- Called from **two places**, both non-blocking (report `[OK]`/`[WARN]`/`[ERROR]`, never abort):
  `ConfigSetupWizard.RunNetBird()` (right after the key prompt, only if the key is new/changed), and
  `ConsoleMenu.EnsureNetBirdConnected()` (called at the top of both `ConsoleMenu.Run()` and
  `ConsoleMenu.RunServeMode()`, right after `StoreAuth.RequireLogin()`, whenever a setup key is
  already saved) — the second one is what makes a *fresh* Edge PC image work: the key was
  provisioned before, NetBird wasn't installed yet, first `IceBot.exe` launch installs it with
  no wizard re-entry needed.
- Process timeouts exist specifically because `winget`/driver install can trigger a UAC
  elevation prompt that a non-interactive process can never satisfy — instead of hanging
  forever, `RunProcess` kills the child after the timeout and reports "may dang cho quyen admin
  (UAC), chay IceBot voi quyen Administrator".
`SiteSettings.IsConfigured` now checks `NetBirdSetupKey` + `PublicUrl` (previously
`DuckDnsSubdomain`/`DuckDnsToken`/`PublicUrl`). All DuckDNS/Cloudflare-specific fields
(`DuckDnsSubdomain`, `DuckDnsToken`, `TunnelName`, `DuckDnsDomain`, the synced `duckdns.env`
file) are gone from `SiteSettings`/`SiteConfigStore`/`AppConfig` — do not reintroduce them.
**Not yet touched:** `deploy/duckdns/`, `deploy/cloudflare/`, and `deploy/icebot/start-serve.ps1`
still reference the old stack; they're stale until someone writes the NetBird equivalent.

## Conventions

- Console output is in Vietnamese (no diacritics), matching existing code.
- **Commit + push after every change, without waiting to be asked each time.** Repo:
  `SU26SE092-IceCream-arm-Robot/IceBot-IOT`, branch `main` (push straight to `main`, no PR flow
  in use here). Still stage deliberately (no blanket `git add -A`) and skip anything that looks
  like a secret, a build artifact, or unrelated pre-existing untracked content the user hasn't
  confirmed — ask about those rather than silently including or silently dropping them.
