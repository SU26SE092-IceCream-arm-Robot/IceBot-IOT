# IceBot ice-cream machine driver

External `IMachineTrigger` plugin for the STM32F103 RS485 controller documented in
`context/protocols/Ice Cream Machine Serial Communication Protocol.md`.

Default trigger cycle: status precheck, UP at 20% for 3 seconds, STOP, DOWN at 20% for 1 second,
STOP, and final Standby verification. Any failure attempts an additional STOP before surfacing the
error to the workflow.

Build the installable package:

```powershell
.\driver-sdk\IceBot.Driver.IceCream\build-package.ps1
```

Install `DRIVER-DLL\IceCream` under `%ProgramData%\IceBot\drivers\IceCream` and configure the
`ice_cream` machine type to the STM32 USB-RS485 COM port.