# 301 Cup-Dropping Machine Serial Communication Protocol V0.0.3

| Version Number | Description | Modifier | Revision Date |
| --- | --- | --- | --- |
| V0.0.1 | Initial Version | Syx | 2022/6/6 |
| V0.0.2 | Added Lower Computer Version Number Reading | Syx | 2023/12/12 |
| V0.0.3 | 1. Modified some text content<br>2. Added an example for each instruction | Syx | 2025/2/11 |

----------- Shanghai Baolutong Coffee Machine Co., Ltd. ----------------

## I. Communication Mode

Serial Port (UART)

Hardware Physical Port: RS232 or RS485

1 start bit, 8 data bits, 1 stop bit, no parity bit, 115200 baud rate

Data String Format:"Command Code" + "Length Code" + "Instruction Code" + "Data 1"…+ "Data n" + "Checksum Code" + "End Code"

| Command Code | Length Code | Instruction Code | Checksum Code | End Code |
| --- | --- | --- | --- | --- |

Communication is carried out in the way of the host querying the slave.The mainboard of the cup - dropping machine is the slave, and the user - controlled device is the host. The session process is as shown in the following figure:



Note: After the host sends an instruction in Step 1, if it does not receive a reply from the slave within 1 second, it will resend. If there is no reply after 3 resends, a communication error will occur.

## II. Instruction Code

0x55 indicates querying slave data.0xAA indicates setting slave data.For querying or setting specific data, refer to the command code section.

## III. Length Code

The length code is equal to the sum of the number of all 8 - bit bytes in the data string.For example:For "Command Code" + "Length Code" + "Instruction Code" + "Data 1" + "Checksum Code" + "End Code", the length code value is 6.For "Command Code" + "Length Code" + "Instruction Code" + "Data 1" + "Data 2" + "Checksum Code" + "End Code", the length code value is 7.

## IV. Checksum Code

The checksum code value is equal to the lower 8 bits of the sum of all data except the "Checksum Code" and "End Code".

## V. End Code

The end code is fixed as the hexadecimal value "0xFF".For end - code judgment, when "0xFF" is received, query the "Length Code". Combine it with the current number of received bytes. If the number of received bytes is equal to the value of the "Length Code", the reception is complete.

## VI. Command Code

| Slave Status Query | 0x01 |
| --- | --- |
| Slave Parameter Query and Setting | 0x02 |
| Shutdown (Not Available Yet) | 0x03 |
| Dispense Beverage | 0x04 |
| Ruying - Specific | 0x05 |

### Slave Status Query (0x01)

Host queries the slave status: (Host → Slave)

| 0x01 | Length Code | 0x55 | Checksum Code | End Code |
| --- | --- | --- | --- | --- |

Slave replies to the host: (Host ← Slave)

| 0x01 | Length Code | 0x55 | Data 1 | Data 2 | Checksum Code | End Code |
| --- | --- | --- | --- | --- | --- | --- |

| Data 1 |  |
| --- | --- |
| Bit0 | 1: No Cup<br>0: Normal |
| Bit1 | 1: Cup Not Taken Away<br>0: Normal |
| Bit2 | 1: Function Drawer Pulled Out<br>0: Normal |
| Bit3 | 1: Motor Failure (Cup - dropping Motor or Cup - barrel Rotating Motor)<br>0: Normal |
| Bit4 | 1: Robot Arm in Place (Custom - made version for some customers)<br>0: Normal |
| Bit5 |  |
| Bit6 |  |
| Bit7 |  |

| Data 2 | System Operating Status |
| --- | --- |
|  | 0: Standby State, allowing the APP to send a cup - taking command;<br>1: Cup - dropping in progress;<br>2: Fault; |

Example: The communication screenshot is as follows:



### Slave Parameter Query and Setting (0x02)

Host queries slave parameters: (Host → Slave)

| 0x02 | Length Code | 0x55 | Checksum Code | End Code |
| --- | --- | --- | --- | --- |

Slave replies to the host: (Host ← Slave)

| 0x02 | Length Code | 0x55 | Data 1 | Data 2 | Data 3 | Data 4 | Data 5 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Data 6 | Data 7 | Data 8 | Data 9 | Data 10 | Data 11 | Data 12 | Data 13 |
| Checksum Code | End Code |  |  |  |  |  |  |

| Name | Value Range | Value Meaning |
| --- | --- | --- |
| Data 1 |  | Reserved |
| Data 2 |  |  |
| Data 3 |  | Reserved |
| Data 4 |  |  |
| Data 5 |  | Reserved |
| Data 6 |  |  |
| Data 7 | Alarm Sound Switch | 1: On, 0 or others: Off. |
| Data 8 |  | Reserved |
| Data 9 |  | Reserved |
| Data 10 |  | Reserved |
| Data 11 |  | Version Number |
| Data 12 |  | Version Number |
| Data 13 |  | Version Number |

Host sets slave parameters: (Host → Slave)Note: Parameter settings can only be carried out when the slave is in a non - fault state.

| 0x02 | Length Code | 0xAA | Data 1 | Data 2 | Data 3 | Data 4 | Data 5 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Data 6 | Data 7 | Data 8 | Data 9 | Data 10 | Checksum Code | End Code |  |
|  |  |  |  |  |  |  |  |

| Name | Value Range | Value Meaning |
| --- | --- | --- |
| Data 1 |  | Reserved |
| Data 2 |  |  |
| Data 3 |  | Reserved |
| Data 4 |  |  |
| Data 5 |  | Reserved |
| Data 6 |  |  |
| Data 7 | Alarm Sound Switch | 1: On, 0 or others: Off. |
| Data 8 |  | Reserved |
| Data 9 |  | Reserved |
| Data 10 |  | Reserved |
| Data 11 |  | Version Number |
| Data 12 |  | Version Number |
| Data 13 |  | Version Number |

Slave replies to the host: (Host ← Slave)

| 0x02 | Length Code | 0xAA | Data 1 | Checksum Code | End Code |  |  |
| --- | --- | --- | --- | --- | --- | --- | --- |

| Data 1 |  |
| --- | --- |
| 0x00 | Setting Failed |
| 0x01 | Setting Successful |

### Shutdown (0x03)

The host sends a shutdown instruction to the slave: (Host → Slave)Note: Shutdown can only be carried out when the slave is in a non - fault state.

| 0x03 | Length Code | 0xAA | Checksum Code | End Code |
| --- | --- | --- | --- | --- |

Slave replies to the host: (Host ← Slave)

| 0x03 | Length Code | 0xAA | Data 1 | Checksum Code | End Code |
| --- | --- | --- | --- | --- | --- |

| Data 1 |  |
| --- | --- |
| 0x00 | Setting Failed |
| 0x01 | Setting Successful |

### Dispense Beverage (0x04)

The host sends a beverage - dispensing instruction to the slave: (Host → Slave)

| 0x04 | Length Code | 0xAA | Beverage Number | Data 1 | Checksum Code | End Code |
| --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |

| Name | Value Range | Value Meaning |
| --- | --- | --- |
| Beverage Number | 1 | Drop one cup |
| Data 1 |  | Reserved |

Slave replies to the host: (Host ← Slave)

| 0x04 | Length Code | 0xAA | Beverage Number | Data 1 | Checksum Code | End Code |
| --- | --- | --- | --- | --- | --- | --- |

| Data 1 |  |
| --- | --- |
| 0x00 | Setting Failed |
| 0x01 | Setting Successful |

Example: Drop one cup: 04 07 aa 01 00 B6 ff

### Ruying - Specific (0x05)

The slave actively reports to the host: (Host ← Slave) (Interval: 300 milliseconds)

| 0x05 | Length Code | 0xAA | Data 1 | Checksum Code | End Code |
| --- | --- | --- | --- | --- | --- |

| Data 1 |  |
| --- | --- |
| 0x00 or others | Meaningless |
| 0x01 | Cup Not Taken Away |

