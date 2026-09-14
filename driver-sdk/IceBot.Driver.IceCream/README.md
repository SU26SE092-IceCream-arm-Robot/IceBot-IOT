# IceBot ice-cream machine driver

External `IMachineTrigger` plugin for the STM32F103 RS485 controller documented in
`context/protocols/Ice Cream Machine Serial Communication Protocol.md`.

`Trigger(connectionName, command)` accepts separate `UP` and `DOWN` commands. After a status
precheck, the driver moves in the requested direction at 20% with duration 0 until the matching
limit returns the controller to Standby, then verifies the final status. It does not automatically
move in the opposite direction. Any failure attempts STOP before surfacing the error to the workflow.

Build the installable package:

```powershell
.\driver-sdk\IceBot.Driver.IceCream\build-package.ps1
```

Install `DRIVER-DLL\IceCream` under `%ProgramData%\IceBot\drivers\IceCream` and configure the
`ice_cream` machine type to the STM32 USB-RS485 COM port.
