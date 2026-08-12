# IceBot peripheral driver SDK

Copy `IceBot.Driver.Template`, implement the machine protocol, and build it for `net472`.
The public plugin class must implement `IMachineModule`; controllable machines normally implement
`IMachineTrigger`, and may implement `IMachineDiagnostics`.

Install one complete package on Edge:

```text
drivers/<driver-name>/
  driver.json
  Vendor.Driver.dll
```

`driver.json` format:

```json
{
  "schemaVersion": 1,
  "machineType": "vendor_cup_dropper",
  "assembly": "Vendor.Driver.dll",
  "entryType": "Vendor.Driver.CupDropperDriver",
  "driverVersion": "1.0.0",
  "sha256": "64-lowercase-hex-characters"
}
```

Generate the checksum in PowerShell:

```powershell
(Get-FileHash .\Vendor.Driver.dll -Algorithm SHA256).Hash.ToLowerInvariant()
```

Restart IceBot after installing or replacing a package. Install both the DLL and its matching
`driver.json`; a checksum mismatch is rejected. A plugin with the same `MachineType` replaces
the built-in driver, while a new `MachineType` adds another machine.
