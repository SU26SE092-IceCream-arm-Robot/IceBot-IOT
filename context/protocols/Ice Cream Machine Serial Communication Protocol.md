# Ice Cream Machine (Custom STM32 Controller) Serial Communication Protocol V0.1

| Version Number | Description | Modifier | Revision Date |
| --- | --- | --- | --- |
| V0.1 | Initial draft — dispense + status (low-stock/out-of-stock lamp tap) | | 2026-07-27 |
| V0.2 | Motor is bidirectional (up = dispense, down = cutoff) — replaced single Dispense command with explicit Motor Up / Motor Down / Stop; physical link is RS485, not USB-TTL | | 2026-07-28 |

Custom board: **STM32F103C8T6 (Blue Pill)**. IceBot (PC) is the host; the STM32 board is the
slave. Frame format and framing rules are intentionally identical to
`301 Cup-Dropping Machine Serial Communication Protocol V0.0.3` so the host side reuses
`IceBot.Machines.SerialFrameCodec` as-is.

## I. Communication Mode

Serial Port (UART) over **RS485** (2-wire, half-duplex) via an RS485 transceiver module
(MAX485/SP3485) between the STM32's USART1 and the PC COM port.

1 start bit, 8 data bits, 1 stop bit, no parity bit, 115200 baud rate.

Half-duplex note: the STM32 side must drive the transceiver's DE/RE line high only while
transmitting its reply, then drop it low immediately after so it can receive the host's next
request (STM32F103 has no hardware auto-DE; this is toggled in firmware around the UART send).

Data String Format: "Command Code" + "Length Code" + "Instruction Code" + "Data 1"…"Data n" + "Checksum Code" + "End Code"

Host queries/commands the slave; slave (STM32) always replies with a frame of the same shape.

Note: after the host sends an instruction, if no reply within 1 second it resends; after 3
resends with no reply, it's a communication error. (Matches `CupDroppingMachineClient` retry
policy — reuse the same host-side pattern.)

## II. Instruction Code

`0x55` = query slave data. `0xAA` = set slave data.

## III. Length Code

Sum of the number of all 8-bit bytes in the data string (same rule as the cup-dropping protocol).

## IV. Checksum Code

Lower 8 bits of the sum of all bytes except "Checksum Code" and "End Code".

## V. End Code

Fixed `0xFF`.

## VI. Command Code

| Command | Code |
| --- | --- |
| Slave Status Query | 0x01 |
| Motor Run UP (dispense) | 0x02 |
| Motor Run DOWN (cutoff) | 0x03 |
| Motor Stop | 0x04 |

### Slave Status Query (0x01)

Host queries the slave status: (Host → Slave)

| 0x01 | 0x05 | 0x55 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- |

Slave reply: (Slave → Host)

| 0x01 | 0x07 | 0x55 | Data 1 | Data 2 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- | --- | --- |

**Data 1** — status bitmask (sourced from tapping the machine's existing indicator-lamp wires
through opto-isolated inputs; see hardware note below):

| Bit | Meaning |
| --- | --- |
| 0 | Low stock (yellow lamp on — sap het kem) |
| 1 | Out of stock (red lamp on — het kem) |
| 2 | Motor fault |
| 3 | Dispensing (busy) |
| 4-7 | Reserved (0) |

**Data 2** — System State: `0x00` = Standby, `0x01` = Dispensing, `0x02` = Fault.

Example (idle, full stock): `01 07 55 00 00 5D FF`

### Motor Run UP (0x02) — dispense

Host commands the motor to run in the UP direction (RPWM on BTS7960): (Host → Slave)

| 0x02 | 0x07 | 0xAA | Data 1 | Data 2 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- | --- | --- |

- Data 1 = Speed (PWM duty, `0`–`100` %). `0` = use the default duty configured on the MCU.
- Data 2 = Duration in 0.1-second units (`0x00` = run continuously until a Stop command is received).

Slave reply: (Slave → Host)

| 0x02 | 0x06 | 0xAA | Data 1 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- | --- |

Data 1 = `0x01` accepted / `0x00` rejected (e.g. already running the other direction, or fault
— slave should refuse rather than reverse abruptly).

Example (run up, default speed, 3s): `02 07 AA 00 03 B6 FF`

### Motor Run DOWN (0x03) — cutoff

Host commands the motor to run in the DOWN direction (LPWM on BTS7960): (Host → Slave)

| 0x03 | 0x07 | 0xAA | Data 1 | Data 2 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- | --- | --- |

Same Data 1 (speed %) / Data 2 (duration in 0.1-second units) meaning as Motor Run UP.

Slave reply: (Slave → Host)

| 0x03 | 0x06 | 0xAA | Data 1 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- | --- |

Data 1 = `0x01` accepted / `0x00` rejected.

### Motor Stop (0x04)

Host commands an immediate stop (both RPWM and LPWM duty forced to 0, driver disabled):
(Host → Slave)

| 0x04 | 0x05 | 0xAA | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- |

Slave reply: (Slave → Host)

| 0x04 | 0x06 | 0xAA | Data 1 | Checksum Code | 0xFF |
| --- | --- | --- | --- | --- |

Data 1 = `0x01` OK / `0x00` failed.

**Firmware rule (safety-critical): RPWM and LPWM must never both be driven with duty > 0 at
the same time.** A Run UP command while DOWN is active (or vice versa) must be rejected
(Data 1 = `0x00`) unless the motor is first stopped.

## VII. Hardware note — lamp-tap sensing (low-stock / out-of-stock)

The machine is electromechanical and already has yellow (low-stock) / red (out-of-stock)
indicator lamps running on 12V or 24V DC. The STM32 does **not** read these lamps directly —
each lamp's two terminals feed one **optocoupler (PC817) input channel** (current-limiting
resistor sized for the lamp's actual DC voltage), and the optocoupler's output side drives a
3.3V-logic GPIO input on the STM32. This isolates the MCU from the machine's control voltage
and lets the STM32 just read "lamp on/off" as a clean digital signal (bit 0 / bit 1 above).

**Do not wire the lamp terminals directly into an STM32 GPIO — confirm the lamp's actual DC
voltage with a multimeter before sizing the resistor/optocoupler stage.**

## VIII. Hardware note — opto-isolated drive stage (STM32 → BTS7960)

The STM32's 3 control outputs (RPWM, LPWM, R_EN/L_EN — see `note.md` for the exact GPIO
mapping) do not wire directly into the BTS7960 driver. Each passes through its own
optocoupler stage first, isolating the STM32's 3.3V logic domain from the BTS7960/motor
supply domain. This is transparent to firmware **except**:

- **Polarity**: depending on how the optocoupler stage is wired (LED forward-biased on GPIO
  HIGH vs GPIO LOW), the signal may arrive inverted at the BTS7960 side. Verify with a
  multimeter which STM32 output level produces the intended BTS7960 input level before
  trusting the firmware's `ACTIVE_HIGH`/`ACTIVE_LOW` assumption (see `Drivers/motor_io.h`).
- **Switching speed**: standard phototransistor optocouplers (e.g. PC817) have slow turn-off
  (storage time), which can distort a fast PWM signal. Keep the motor PWM frequency low
  (firmware default: 1 kHz) unless the isolation stage uses a fast logic-gate optocoupler
  (e.g. TLP281, 6N137).
