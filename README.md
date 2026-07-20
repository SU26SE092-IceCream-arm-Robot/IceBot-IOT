# IceBot-IOT

Ứng dụng điều khiển hệ thống robot pha chế/đóng gói kem tự động: tay máy **Fairino FR5** (6 bậc tự do) + các **máy ngoại vi** trong cell (máy thả cốc, máy kem, máy topping...). IceBot chạy trên một PC đặt tại cửa hàng ("robot controller"), nhận đơn hàng từ cloud và điều phối toàn bộ dây chuyền.

---

## Mục lục

- [Kiến trúc tổng quan](#kiến-trúc-tổng-quan)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Yêu cầu môi trường](#yêu-cầu-môi-trường)
- [Build & Run](#build--run)
- [Menu & CLI](#menu--cli)
- [Cấu hình site](#cấu-hình-site)
- [Điều khiển máy trong hệ thống](#điều-khiển-máy-trong-hệ-thống)
- [Lua workflow scripts](#lua-workflow-scripts)
- [Deploy (DuckDNS + Cloudflare Tunnel)](#deploy-duckdns--cloudflare-tunnel)
- [Xử lý sự cố thường gặp](#xử-lý-sự-cố-thường-gặp)
- [Trạng thái triển khai](#trạng-thái-triển-khai)

---

## Kiến trúc tổng quan

```
Cloud BE (đơn hàng/thanh toán)
        │ POST /api/orders  (qua DuckDNS + Cloudflare Tunnel)
        ▼
IceBot.exe (robot controller, PC tại cửa hàng)
        │
        ▼
Với mỗi bước trong hàng đợi (WorkflowRunner):
  1. Chạy file .lua của bước đó trên tay máy Fairino (LAN 192.168.58.2) — nạp vào, chạy hết
     từ trên xuống dưới, tay máy di chuyển tới vị trí của bước này (không quay về vị trí
     ban đầu giữa các bước — chỉ quay về Home lúc khởi động/reset hoặc sau khi xong 1 sản phẩm)
  2. Nếu bước này có máy ngoại vi gắn kèm (MachineRegistry), NGAY SAU KHI tay máy chạy xong
     bước 1 (đã vào đúng vị trí) → IceBot mở cổng COM, gửi tín hiệu riêng cho máy đó
       - Máy có giao thức serial riêng (vd. máy thả cốc) → gửi khung lệnh qua System.IO.Ports
       - Máy chỉ nhận bật/tắt (máy kem, topping...) → nằm trong chính file .lua của bước đó,
         dùng SetDO(...) kích 24V ra mạch ngoài (không cần bước 2 riêng)
```

Tín hiệu tới máy **luôn đi sau** khi tay máy đã chạy xong phần `.lua` của bước đó — không phải 2 việc tách rời. Xem `code/src/IceBot/Machines/` cho phần giao tiếp serial trực tiếp.

## Cấu trúc thư mục

```
IceBot-IOT/
├── code/
│   ├── IceBot-IOT.sln
│   ├── lib/fairino-csharp-sdk/     # Fairino Robot C# SDK (vendored)
│   └── src/IceBot/
│       ├── Program.cs              # Entry point, menu, CLI
│       ├── Api/BeApi.cs            # Client gọi BE lấy Lua (hiện là mock)
│       ├── Config/                 # AppConfig, SiteConfigStore, ConfigSetupWizard
│       ├── Machines/               # Giao tiếp serial trực tiếp với máy ngoại vi (Path A)
│       ├── Networking/LocalApiServer.cs  # HTTP API nội bộ (ingress cho Cloudflare Tunnel)
│       ├── Robot/FairinoLuaExecutor.cs   # Upload + chạy .lua trên Fairino
│       └── Workflow/               # WorkflowProvisioner, WorkflowRunner
├── workflow/                       # File .lua theo từng bước (gitignored, tải từ BE)
├── deploy/                         # Script cài đặt DuckDNS + Cloudflare Tunnel
└── docs/                           # Tài liệu giao thức phần cứng (vd. máy thả cốc)
```

## Yêu cầu môi trường

- **Windows** (dùng `System.IO.Ports` để giao tiếp COM, và SDK Fairino).
- [.NET SDK](https://dotnet.microsoft.com/) có hỗ trợ target `net472` (.NET Framework 4.7.2) — cài .NET Framework 4.7.2 Developer Pack nếu build báo thiếu targeting pack.
- Tay máy Fairino FR5 cùng LAN với PC (mặc định `192.168.58.2`).
- Cổng COM còn trống để đấu các máy ngoại vi có giao thức serial (vd. máy thả cốc).

## Build & Run

```powershell
# Build toàn bộ solution (IceBot + Fairino SDK)
dotnet build code/IceBot-IOT.sln -c Debug

# Chạy trực tiếp exe vừa build (menu tương tác)
code/src/IceBot/bin/Debug/net472/IceBot.exe

# Hoặc build bản Release rồi chạy kèm lệnh CLI
dotnet build code/IceBot-IOT.sln -c Release
code/src/IceBot/bin/Release/net472/IceBot.exe serve
```

> Vì `IceBot` target `net472` (.NET Framework, không phải .NET Core/5+), chạy trực tiếp file `.exe` — không dùng `dotnet run`.

## Menu & CLI

Menu tương tác khi chạy `IceBot.exe` không tham số:

| # | Chức năng |
|---|-----------|
| 1 | Cấu hình DuckDNS + Cloudflare Tunnel + IP robot + cổng COM máy ngoại vi |
| 2 | Xem cấu hình hiện tại |
| 3 | Tải file Lua từ BE (hiện là mock `BeApi.GetLua`) |
| 4 | Chạy server — nhận đơn từ BE (`serve` mode, cổng 5080) |
| 5 | Test robot — chạy file `.lua` test từ `workflow/` |
| 6 | Test máy thả cốc (serial) — query trạng thái / thả 1 cốc |
| 7 | Thoát |

CLI tương ứng:

| Lệnh | Mục đích |
|------|----------|
| `IceBot.exe` | Mở menu tương tác |
| `IceBot.exe setup` | Wizard cấu hình → `config/icebot.site.env` |
| `IceBot.exe provision` | Tải Lua từ BE (mock) → `workflow/` |
| `IceBot.exe serve` | Chạy HTTP API nội bộ trên cổng `5080` |
| `IceBot.exe test` | Chạy workflow test trên tay máy |
| `IceBot.exe test-cup` | Menu test máy thả cốc qua serial |

## Cấu hình site

Cấu hình theo từng cửa hàng lưu tại `config/icebot.site.env` (gitignored, tạo qua menu 1 hoặc `IceBot.exe setup`):

| Biến | Ý nghĩa |
|------|---------|
| `DUCKDNS_SUBDOMAIN` / `DUCKDNS_TOKEN` | DuckDNS domain cho tunnel |
| `TUNNEL_NAME` | Tên Cloudflare Tunnel |
| `PUBLIC_URL` | URL công khai để BE gọi vào IceBot |
| `BE_API_URL` | Base URL của BE (dự phòng, chưa dùng — đang mock) |
| `API_KEY` | Secret chia sẻ với BE, gửi qua header `X-Api-Key` |
| `ROBOT_IP` | IP control box Fairino (mặc định `192.168.58.2`) |
| `MACHINE_PORTS` | Cổng COM theo từng loại máy ngoại vi, dạng `cup_dropping:COM3,...` |

## Điều khiển máy trong hệ thống

- `code/src/IceBot/Machines/SerialFrameCodec.cs` — đóng/mở khung lệnh (checksum, length, end code `0xFF`) theo đúng `docs/301 Cup-Dropping Machine Serial Communication Protocol V0.0.3.md`.
- `code/src/IceBot/Machines/CupDroppingMachineClient.cs` — mở `SerialPort` (115200-8N1), gửi lệnh Query Status (`0x01`), Dispense (`0x04`), Shutdown (`0x03`); timeout 1s, tự gửi lại tối đa 3 lần trước khi báo lỗi giao tiếp (đúng đặc tả).
- `code/src/IceBot/Machines/MachineRegistry.cs` — ánh xạ tên bước trong hàng đợi (vd `cup_s`) sang loại máy ngoại vi (`cup_dropping`) **cần bắn tín hiệu thêm sau khi bước đó chạy xong**. **Thêm máy mới**: viết `<Machine>Client` mới trong `Machines/`, đăng ký bước tương ứng vào `MachineRegistry`, và thêm `MachinePorts` cho loại máy đó trong cấu hình site.
- `code/src/IceBot/Workflow/WorkflowRunner.cs` — với mỗi bước trong hàng đợi: **luôn** upload/chạy `.lua` của bước đó trên tay máy Fairino trước; chạy xong, nếu bước này khớp `MachineRegistry` thì gửi thêm lệnh serial trực tiếp tới máy tương ứng. File `.lua` không thể tự gửi được tín hiệu serial (Fairino Lua không có lệnh UART thô) nên tín hiệu thật luôn do IceBot (C#) gửi.

Các máy chưa có giao thức serial riêng (máy kem, topping...) vẫn dùng đường cũ: `.lua` gọi `SetDO(...)` để kích 24V ra mạch ngoài → PCB tự chế → động cơ bước.

## Lua workflow scripts

- Mỗi file `.lua` trong `workflow/` là **một bước tay máy**, viết theo quy ước: 1 điểm bắt đầu → 1 điểm kết thúc + một số hành động ở giữa (`WaitMs`, `SetDO`...) — **không** quay lại điểm bắt đầu ở cuối file. IceBot không quan tâm tên/note các điểm bên trong file, chỉ nạp và chạy hết nội dung **từ trên xuống dưới** (`FairinoLuaExecutor.RunScript`).
- Được tải về qua menu 3 / `IceBot.exe provision` (hiện gọi `BeApi.GetLua` — đang là mock, chưa nối BE thật).
- **Nối nhiều bước liên tục** (không cần merge gì thêm): vì mỗi file chỉ có 1 đoạn đường (không round-trip), chạy tuần tự từng file là tay máy đã tự đi liên tục — điểm kết thúc file trước = điểm bắt đầu thực tế của file sau. Tay máy **chỉ quay về Home** lúc khởi động/reset phần mềm, hoặc sau khi hoàn thành 1 sản phẩm (cần một bước `home.lua` chèn vào hàng đợi ở đúng điểm đó — phần ánh xạ đơn hàng → hàng đợi này vẫn đang TODO).
- ⚠️ File mẫu `workflow/lay_coc.lua` hiện có trong repo là **script demo/test** (do FaiRobot Studio sinh, có đi khứ hồi A→B→A) — **không** phải khuôn mẫu cho file bước sản xuất thật, đừng copy cấu trúc round-trip của nó.

## Deploy (DuckDNS + Cloudflare Tunnel)

Script trong `deploy/` dùng để mở đường cho BE trên cloud gọi vào IceBot (không cần port-forward router):

| Script | Vai trò |
|--------|---------|
| `deploy/duckdns/register-scheduled-task.ps1` | Đăng ký scheduled task tự cập nhật IP công khai lên DuckDNS |
| `deploy/cloudflare/setup-tunnel.ps1` | Tạo/khởi tạo Cloudflare Tunnel (chạy sau khi `cloudflared tunnel login`) |
| `deploy/icebot/start-serve.ps1` | Build (nếu cần) rồi chạy `IceBot.exe serve` |

## Xử lý sự cố thường gặp

| Triệu chứng | Nguyên nhân thường gặp |
|-------------|-------------------------|
| `RPC failed with error code ...` khi test robot | Sai `ROBOT_IP`, tay máy chưa bật, hoặc PC không cùng LAN `192.168.58.x` với control box |
| Menu 6 / `test-cup` báo "Chua cau hinh cong COM" | Chưa nhập cổng COM máy thả cốc ở menu 1 |
| `Cup-dropping machine communication error: no valid reply after 3 resend(s)` | Sai cổng COM, sai baud rate/đấu dây RS232-RS485, hoặc máy thả cốc chưa cấp nguồn |
| `Checksum mismatch` / `Length mismatch` từ máy thả cốc | Nhiễu đường truyền hoặc đấu sai chân TX/RX — kiểm tra cách ly & dây tín hiệu |

## Trạng thái triển khai

| Hạng mục | Trạng thái |
|----------|------------|
| Menu + CLI | ✅ Xong |
| Config wizard (DuckDNS, tunnel, robot IP, cổng COM máy ngoại vi) | ✅ Xong |
| `WorkflowRunner` — chạy tuần tự từng bước, mỗi file `.lua` chạy trọn vẹn (nối liền tự nhiên, xem Lua workflow scripts) | ✅ Xong |
| Máy thả cốc — giao tiếp serial trực tiếp sau khi tay máy vào vị trí (`IceBot.Machines`) | ✅ Xong |
| Kết nối BE thật (`BeApi`) | ❌ Chưa (đang mock) |
| Ánh xạ đơn hàng → danh sách bước (số lượng, vị/topping → queue) | ❌ Chưa |
| Bước quay về Home giữa các sản phẩm / lúc khởi động | ❌ Chưa (cần chèn `home.lua` vào queue ở đúng chỗ khi build queue) |
| `POST /api/orders` → chạy thực tế | ❌ Chưa (mới log + trả 202) |

Xem thêm chi tiết giao thức máy thả cốc tại [`docs/301 Cup-Dropping Machine Serial Communication Protocol V0.0.3.md`](docs/301%20Cup-Dropping%20Machine%20Serial%20Communication%20Protocol%20V0.0.3.md).
