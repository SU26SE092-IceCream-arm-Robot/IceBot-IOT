# IceBot cup-dropping driver

Plugin RS485 cho máy thả cốc. Driver này không được compile vào `IceBot.exe` và không được cài
mặc định. Build package rồi chủ động copy cả thư mục package vào Edge:

```text
<Edge install directory>\drivers\bt_cup_l90\
├── driver.json
└── IceBot.Driver.CupDropping.dll
```

Source target `net472` và chỉ phụ thuộc contract `IceBot.Driver.Abstractions` do IceBot cung cấp.
