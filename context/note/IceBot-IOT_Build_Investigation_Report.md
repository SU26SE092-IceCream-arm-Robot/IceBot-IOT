# IceBot-IOT Build Investigation Report

**Repository:** `IceBot-IOT`  
**Branch inspected:** `main`  
**HEAD during investigation:** `828b023` — `fix: include Edge build dependencies`  
**Scope:** Source-code/repository issues discovered after cloning and building the repository.  
**Excluded:** Environment/setup issues such as .NET SDK, .NET Framework Targeting Pack, Visual Studio Build Tools, and MSBuild path/configuration.

---

## 1. Summary

After the required build environment was available, the repository restored successfully and several projects built successfully, including the legacy Fairino robot SDK project.

The remaining build failures are caused by inconsistencies in the checked-in source code on `main`, not by the local development environment.

The investigation identified two categories of source problems:

1. **Four referenced classes/helpers are used by production code and tests but have no definition in the current repository checkout.**
2. **`IceBot.Driver.IceCream` implements an `IMachineTrigger` method signature that is incompatible with the interface contract used by the rest of the project.**

The repository was confirmed to be clean and synchronized with `origin/main`, so these failures are reproducible from the current checked-in state rather than being caused by uncommitted local modifications.

---

## 2. Build Result After Environment Issues Were Removed

Command used:

```powershell
dotnet build .\code\IceBot-IOT.sln -c Release
```

Restore succeeded, and the following projects were observed building successfully:

```text
FRRobot
IceBot.Setup
IceBot.Driver.Abstractions
IceBot.Driver.Template
IceBot.Driver.CupDropping
```

The build then failed with **12 compiler errors**.

The errors consisted of:

```text
CS0535 - IceCreamDriver does not implement IMachineTrigger.Trigger(string)

CS0103 - MachineTypeCanonicalizer does not exist
CS0103 - ConsoleSecretReader does not exist
CS0103 - FairinoSafetyProbe does not exist
CS0103 - EdgeSetupReadiness does not exist
```

`EdgeSetupReadiness` produces several errors because it is referenced at multiple locations in `ConsoleMenu.cs`.

---

## 3. Repository State Was Verified

The repository was checked with:

```powershell
git status
git branch --show-current
git log -5 --oneline
git status --short
```

Observed state:

```text
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
```

Current HEAD:

```text
828b023 fix: include Edge build dependencies
```

Therefore, the failing source represents the current tracked `origin/main` state rather than local edits or an outdated local branch.

---

# 4. Missing Source Definitions

The compiler reported unresolved references to:

```text
MachineTypeCanonicalizer
ConsoleSecretReader
FairinoSafetyProbe
EdgeSetupReadiness
```

## 4.1 Definitions were searched directly

The following command searched for class declarations:

```powershell
git grep -n -E "class (MachineTypeCanonicalizer|ConsoleSecretReader|FairinoSafetyProbe|EdgeSetupReadiness)|static class (MachineTypeCanonicalizer|ConsoleSecretReader|FairinoSafetyProbe|EdgeSetupReadiness)"
```

**Result:** no matches.

This means no tracked C# source in the current checkout declares any of these four classes under those names.

A filename search also returned no matching files:

```powershell
Get-ChildItem -Recurse -File |
    Where-Object {
        $_.Name -match "MachineType|ConsoleSecret|FairinoSafety|EdgeSetup"
    } |
    Select-Object FullName
```

**Result:** no output.

---

## 4.2 References to the missing types do exist

The following command searched all tracked content for usages:

```powershell
git grep -n -E "MachineTypeCanonicalizer|ConsoleSecretReader|FairinoSafetyProbe|EdgeSetupReadiness"
```

It found production references including:

```text
code/src/IceBot/Machines/MachineRegistry.cs
    MachineTypeCanonicalizer.Canonicalize(machineType)

code/src/IceBot/Api/Authentication/StoreAuth.cs
    ConsoleSecretReader.Read(current)

code/src/IceBot/Config/Setup/ConfigSetupWizard.cs
    ConsoleSecretReader.Read(current)

code/src/IceBot/Workflow/Orders/OrderExecutionPreflight.cs
    FairinoSafetyProbe.Read(AppConfig.RobotIp)

code/src/IceBot/Cli/ConsoleMenu.cs
    EdgeSetupReadiness.IsHttpsUrl(...)
    EdgeSetupReadiness.IsSetupComplete(...)
    EdgeSetupReadiness.IsProductionReady(...)
```

Tests also reference missing helpers, for example:

```text
harness/IceBot.Harness.Tests/SiteSettingsTests.cs
    EdgeSetupReadiness.IsSetupComplete(...)
    EdgeSetupReadiness.IsProductionReady(...)

harness/IceBot.Harness.Tests/WorkflowExecutionPlanTests.cs
    MachineTypeCanonicalizer.Canonicalize(...)
```

Therefore these names are not dead documentation references; they are dependencies of executable source and tests.

---

## 4.3 Git history shows usages were introduced without corresponding definitions in the inspected commits

History was searched with:

```powershell
git log --all -S"MachineTypeCanonicalizer" --oneline
git log --all -S"ConsoleSecretReader" --oneline
git log --all -S"FairinoSafetyProbe" --oneline
git log --all -S"EdgeSetupReadiness" --oneline
```

Relevant commits included:

```text
627a865 test: add harness and testing documentation
162d642 docs: sync README with project context
2687223 feat: recover interrupted production units on operator restart
```

Inspection of commit `162d642` showed code being changed from inline console input logic to:

```csharp
return ConsoleSecretReader.Read(current);
```

For example, `StoreAuth.cs` changed from:

```csharp
var input = Console.ReadLine()?.Trim() ?? string.Empty;
return string.IsNullOrEmpty(input) ? current : input;
```

to:

```csharp
return ConsoleSecretReader.Read(current);
```

However, the inspected commit file list did not contain a newly added `ConsoleSecretReader` source file.

Likewise, commit `627a865` added tests such as:

```csharp
Assert.Equal(expected, MachineTypeCanonicalizer.Canonicalize(input));
```

and the generated unit-test documentation reported these tests as passed, while the current tracked source contains no `MachineTypeCanonicalizer` definition.

This creates a mismatch between the checked-in implementation and the checked-in call sites/tests.

---

# 5. `IceCreamDriver` / `IMachineTrigger` Contract Mismatch

The other independent compiler failure is:

```text
CS0535:
'IceCreamDriver' does not implement interface member
'IMachineTrigger.Trigger(string)'
```

## 5.1 Current interface contract

`code/src/IceBot.Driver.Abstractions/IMachineTrigger.cs` contains:

```csharp
public interface IMachineTrigger : IMachineModule
{
    void Trigger(string connectionName);
    void TestConnection(string connectionName);
}
```

The contract therefore requires a trigger method with exactly one string parameter.

---

## 5.2 Existing drivers follow the one-parameter contract

This was verified with:

```powershell
git grep -n "void Trigger("
```

Observed implementations:

```text
IMachineTrigger.cs
    void Trigger(string connectionName);

IceBot.Driver.CupDropping/CupDroppingDriver.cs
    public void Trigger(string comPort)

IceBot.Driver.Template/TemplateMachineDriver.cs
    public void Trigger(string connectionName)

IceBot.Driver.IceCream/IceCreamDriver.cs
    public void Trigger(string comPort, string command)
```

`IceCreamDriver` is therefore the only inspected implementation using two parameters.

---

## 5.3 `IceCreamDriver` expects command-specific behavior

The current implementation contains:

```csharp
public void Trigger(string comPort, string command)
```

and branches on:

```text
UP
DOWN
```

The checked-in test suite confirms this behavior explicitly:

```csharp
driver.Trigger("COM7", "UP");
driver.Trigger("COM7", "DOWN");
```

The tests verify separate upward and downward operations.

Therefore, this is not only a compiler-signature typo inside the driver; its tests and implementation are both based on a two-argument, command-aware trigger API.

---

## 5.4 Core workflow uses a different trigger model

`WorkflowRunner` currently accepts a workflow-level command:

```csharp
TriggerDevice(string machineType, string command)
```

but validates only:

```text
ON
TRIGGER
```

and eventually calls the driver as:

```csharp
trigger.Trigger(
    SiteConfigStore.Load().GetMachinePort(trigger.MachineType)
);
```

The `command` value is not forwarded to `IMachineTrigger`.

Therefore there are two incompatible command models:

```text
Core workflow:
    TriggerDevice(machineType, "ON" / "TRIGGER")
        -> IMachineTrigger.Trigger(connectionName)

IceCream driver:
    Trigger(connectionName, "UP" / "DOWN")
```

---

## 5.5 Git history confirms the abstraction was not previously two-argument

The full history of `IMachineTrigger.cs` was inspected:

```powershell
git log --all -p -- .\code\src\IceBot.Driver.Abstractions\IMachineTrigger.cs
```

The interface was introduced in commit:

```text
1c1550e feat: load peripheral drivers as DLL plugins
```

with:

```csharp
void Trigger(string connectionName);
void TestConnection(string connectionName);
```

A repository-wide history search was then performed:

```powershell
git rev-list --all |
ForEach-Object {
    git grep -n "Trigger(string connectionName, string command)" $_ 2>$null
}
```

**Result:** no matches.

A search for the IceCream signature:

```powershell
git rev-list --all |
ForEach-Object {
    git grep -n "Trigger(string comPort, string command)" $_ 2>$null
}
```

returned only:

```text
828b023:driver-sdk/IceBot.Driver.IceCream/IceCreamDriver.cs
```

This shows the two-parameter trigger signature first appears in the current IceCream-driver commit and is not an older version of the common abstraction.

---

# 6. Commit `828b023` Does Not Contain All Dependencies Required for a Successful Build

The current HEAD is:

```text
828b023 fix: include Edge build dependencies
```

Inspection:

```powershell
git show --stat 828b023
git show --name-status 828b023
```

shows that it added:

- Fairino `FRRobot` source/project files
- workflow execution files
- `IceBot.Driver.IceCream`
- IceCream protocol/client/codec files

Notably, this commit does **not** add definitions for:

```text
MachineTypeCanonicalizer
ConsoleSecretReader
FairinoSafetyProbe
EdgeSetupReadiness
```

It also adds `IceCreamDriver.cs` with the two-parameter trigger implementation while the repository's common `IMachineTrigger` abstraction remains one-parameter.

Thus, although the commit adds several previously absent Edge build dependencies, the resulting repository state is still not internally build-consistent.

---

# 7. Documentation/Test Inconsistency Around IceCream Trigger Semantics

There is also an internal inconsistency inside the IceCream feature itself.

`driver-sdk/IceBot.Driver.IceCream/README.md` describes a default trigger as a complete cycle:

```text
status precheck
-> UP
-> STOP
-> DOWN
-> STOP
-> final Standby verification
```

However:

- `IceCreamDriver.cs` exposes separate `"UP"` and `"DOWN"` commands.
- `IceCreamDriverTests.cs` tests `"UP"` and `"DOWN"` independently.
- Core `WorkflowRunner` accepts only `"ON"` and `"TRIGGER"`.

Therefore the checked-in README, driver implementation/tests, and Core workflow do not currently describe the same trigger contract.

---

# 8. Final Findings

The following issues are present in the cloned `main` branch after excluding all environment/toolchain problems:

1. **`MachineTypeCanonicalizer` is referenced but has no tracked source definition.**
2. **`ConsoleSecretReader` is referenced but has no tracked source definition.**
3. **`FairinoSafetyProbe` is referenced but has no tracked source definition.**
4. **`EdgeSetupReadiness` is referenced by production code and tests but has no tracked source definition.**
5. **`IceCreamDriver` does not satisfy the current `IMachineTrigger` interface.**
6. **The common interface requires `Trigger(string connectionName)`, while `IceCreamDriver` implements `Trigger(string comPort, string command)`.**
7. **Other inspected drivers (`CupDroppingDriver` and `TemplateMachineDriver`) follow the existing one-parameter interface.**
8. **Git history shows no prior two-parameter version of `IMachineTrigger`; the two-parameter signature appears only in the current IceCream-driver commit.**
9. **Core workflow accepts `ON`/`TRIGGER`, while the IceCream driver/tests use `UP`/`DOWN`.**
10. **The IceCream README describes a full trigger cycle, while the implementation/tests expose separate directional operations.**
11. **Commit `828b023` adds multiple Edge dependencies but does not include the four missing helper definitions required by current call sites.**
12. **The repository is clean and synchronized with `origin/main`, so these inconsistencies exist in the tracked branch state rather than being caused by local modifications.**

---

## Build-state conclusion

At the time of investigation, the repository could restore dependencies and compile several component projects, but the complete solution could not compile successfully because the checked-in source on `main` contains missing definitions and incompatible internal contracts.
