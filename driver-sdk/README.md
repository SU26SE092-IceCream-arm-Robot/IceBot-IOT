# IceBot peripheral driver SDK

Copy `IceBot.Driver.Template`, implement the machine protocol, and build it for `net472`.
The public plugin class must implement `IMachineModule`; controllable machines normally implement
`IMachineTrigger`, and may implement `IMachineDiagnostics`.

Install one complete package into the shared Edge driver directory:

```text
C:\ProgramData\IceBot\drivers\<driver-name>\
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
`driver.json`; a checksum mismatch is rejected. The architecture is migrating every peripheral
machine to a plugin. IceBot core no longer provides a cup-dropping fallback; its ready-to-install
package is built into `DRIVER-DLL/CupDropping/`.
