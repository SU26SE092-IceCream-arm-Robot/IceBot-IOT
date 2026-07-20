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
│       ├── Program.cs              # Entry point mỏng: parse args, giao cho Cli/
│       ├── Api/BeApi.cs            # Client gọi BE lấy Lua (hiện là mock)
│       ├── Cli/ConsoleMenu.cs      # Toàn bộ UI console: menu, serve/test/test-machine mode
│       ├── Config/                 # AppConfig, SiteConfigStore, SiteSettings, ConfigSetupWizard
│       ├── Machines/                       # Định danh + điều khiển serial cho máy ngoại vi (Path A)
│       │   ├── IMachineModule.cs           #   interface MỌI máy phải implement (định danh + vị trí)
│       │   ├── IMachineTrigger.cs          #   interface tuỳ chọn: máy có cổng COM thật mới cần
│       │   ├── IMachineDiagnostics.cs      #   interface tuỳ chọn: query trạng thái cho menu test
│       │   ├── SerialFrameCodec.cs         #   hạ tầng dùng chung: đóng/mở khung, checksum
│       │   ├── MachineRegistry.cs          #   nơi ĐĂNG KÝ module — 1 dòng / máy mới
│       │   └── CupDropping/                #   1 "module" hoàn chỉnh cho 1 máy, 1 thư mục/máy
│       │       ├── CupDroppingMachineModule.cs   # implement IMachineTrigger (+ IMachineDiagnostics)
│       │       ├── CupDroppingMachineClient.cs   # giao thức serial thô (SerialPort)
│       │       └── CupMachineStatus.cs
│       ├── Networking/LocalApiServer.cs  # HTTP API nội bộ (ingress cho Cloudflare Tunnel)
│       ├── Robot/FairinoLuaExecutor.cs   # Upload/chạy .lua + MoveToTeachingPoint (Home) trên Fairino
│       └── Workflow/               # WorkflowProvisioner, WorkflowRunner, WorkflowQueueBuilder
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
| 6 | Test máy ngoại vi (serial) — chọn 1 máy trong danh sách đã đăng ký, query trạng thái / trigger |
| 7 | Thoát |

CLI tương ứng:

| Lệnh | Mục đích |
|------|----------|
| `IceBot.exe` | Mở menu tương tác |
| `IceBot.exe setup` | Wizard cấu hình → `config/icebot.site.env` |
| `IceBot.exe provision` | Tải Lua từ BE (mock) → `workflow/` |
| `IceBot.exe serve` | Chạy HTTP API nội bộ trên cổng `5080` |
| `IceBot.exe test` | Chạy workflow test trên tay máy |
| `IceBot.exe test-machine` | Menu test máy ngoại vi qua serial |

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

**Mọi file `.lua` đều gắn với 1 định danh máy** — không có chuyện 1 bước không thuộc về máy nào. Mỗi máy là **1 module tự khép kín**; thêm máy mới vào hệ thống = thêm 1 module, không phải sửa rải rác nhiều nơi.

### Kiến trúc module

```
IMachineModule (interface, Machines/IMachineModule.cs) — MỌI máy đều implement cái này
  ├─ MachineType : id ổn định (vd "cup_dropping")
  ├─ DisplayName  : tên hiển thị (vd "May tha coc")
  ├─ Position      : vị trí vật lý trên dây chuyền (số nhỏ hơn = đứng trước)
  └─ StepNames     : những bước (.lua) thuộc về máy này (vd ["cup_s"])

IMachineTrigger : IMachineModule (interface tuỳ chọn, Machines/IMachineTrigger.cs)
  └─ Trigger(comPort) : chỉ máy nào có giao thức serial thật (đấu cổng COM vào PC) mới cần
                         implement thêm cái này — gọi ngay sau khi tay máy chạy xong bước đó.
                         Máy thuần di chuyển tay máy (không có phần cứng serial riêng) chỉ cần
                         implement IMachineModule, KHÔNG cần Trigger.

IMachineDiagnostics (interface tuỳ chọn, Machines/IMachineDiagnostics.cs)
  └─ GetStatusText(comPort) : cho phép menu test hiện thêm lựa chọn "Query trạng thái"

MachineRegistry.Modules (Machines/MachineRegistry.cs)
  └─ danh sách MỌI máy đã đăng ký (kể cả máy không có Trigger) — nơi DUY NHẤT cần thêm 1 dòng khi có máy mới
```

`WorkflowQueueBuilder` (sắp xếp bước) đọc `Position` từ **mọi** máy trong `MachineRegistry.Modules`. `WorkflowRunner` (bắn tín hiệu), `ConfigSetupWizard` (hỏi cổng COM), và menu 6 "Test máy ngoại vi" chỉ đọc các máy implement thêm `IMachineTrigger` (`MachineRegistry.Modules.OfType<IMachineTrigger>()`) — vì chỉ những máy đó mới thật sự cần cổng COM.

### Thêm 1 máy mới — chỉ cần

1. Tạo thư mục `Machines/<TenMay>/`.
2. Nếu máy có giao thức serial riêng (đấu cổng COM): viết `<TenMay>Client.cs` (giao thức thô, giống `CupDropping/CupDroppingMachineClient.cs` — tái dùng `SerialFrameCodec` nếu cùng khuôn khung lệnh) + `<TenMay>Module.cs` implement `IMachineTrigger` (và `IMachineDiagnostics` nếu có lệnh query trạng thái) — xem `Machines/CupDropping/CupDroppingMachineModule.cs` làm mẫu.
   Nếu máy chỉ thuần di chuyển tay máy (không có phần cứng serial riêng, vd trạm đặt khay): chỉ cần viết `<TenMay>Module.cs` implement `IMachineModule` (không cần `Trigger`).
3. Thêm đúng **1 dòng** vào `MachineRegistry.Modules`:
   ```csharp
   public static readonly IReadOnlyList<IMachineModule> Modules = new IMachineModule[]
   {
       new CupDroppingMachineModule(),
       new TenMayModule(),   // ← thêm dòng này
   };
   ```

Xong — `WorkflowQueueBuilder` tự biết vị trí của máy mới để sắp xếp; nếu máy có `IMachineTrigger` thì `ConfigSetupWizard` tự hỏi thêm cổng COM, `WorkflowRunner` tự bắn tín hiệu đúng bước, menu 6 tự liệt kê để test. Không cần sửa `WorkflowRunner.cs`, `ConfigSetupWizard.cs`, hay `ConsoleMenu.cs`.

Các máy chưa có giao thức serial riêng (máy kem, topping...) vẫn dùng đường cũ (Path B) bên trong `.lua`: `SetDO(...)` để kích 24V ra mạch ngoài → PCB tự chế → động cơ bước — vẫn cần đăng ký 1 `IMachineModule` (để có `Position`), chỉ là không cần `IMachineTrigger`.

### Sắp xếp thứ tự bước theo vị trí máy (`WorkflowQueueBuilder`)

Khi 1 đơn hàng cần nhiều bước (vd cốc + kem + topping + khay), các bước đó có thể được tập hợp **không theo thứ tự** (tùy nguồn dữ liệu đơn hàng). `code/src/IceBot/Workflow/WorkflowQueueBuilder.cs` sắp xếp lại chúng theo đúng thứ tự vật lý trên dây chuyền trước khi đưa vào `WorkflowRunner.RunQueue`:

```csharp
var ordered = WorkflowQueueBuilder.BuildQueue(new[] { "deliver_tray", "ice_chocolate_s", "cup_s" });
// → sắp theo Position của từng module đã đăng ký (cup_s trước, vì CupDroppingMachineModule.Position = 1)
WorkflowRunner.RunQueue(ordered, AppConfig.RobotIp);
```

- Thứ tự dựa vào `IMachineModule.Position` của máy gắn với bước đó (số nhỏ hơn = đứng trước trên dây chuyền). **Vì mọi file `.lua` đều gắn với 1 máy đã đăng ký trong `MachineRegistry`, mọi bước đều phải có `Position` xác định** — kể cả máy thuần di chuyển tay máy (vd trạm đặt khay) cũng cần đăng ký `IMachineModule` để có vị trí, dù không cần `IMachineTrigger`.
- Trường hợp 1 bước không tìm thấy máy nào trong `MachineRegistry` sẽ bị xếp xuống cuối — đây là **cơ chế phòng vệ**, báo hiệu thiếu đăng ký máy trong `MachineRegistry.Modules`, không phải trạng thái bình thường.
- Đây là bước **gộp hàng đợi**, tách biệt với việc **chạy** hàng đợi (`WorkflowRunner`) — dùng khi xây dựng phần "đơn hàng → danh sách bước" (vẫn đang TODO, xem Trạng thái triển khai).

## Lua workflow scripts

- Mỗi file `.lua` trong `workflow/` là **một bước tay máy**, viết theo quy ước: 1 điểm bắt đầu → 1 điểm kết thúc + một số hành động ở giữa (`WaitMs`, `SetDO`...) — **không** quay lại điểm bắt đầu ở cuối file. IceBot không quan tâm tên/note các điểm bên trong file, chỉ nạp và chạy hết nội dung **từ trên xuống dưới** (`FairinoLuaExecutor.RunScript`).
- Được tải về qua menu 3 / `IceBot.exe provision` (hiện gọi `BeApi.GetLua` — đang là mock, chưa nối BE thật).
- **Nối nhiều bước liên tục** (không cần merge gì thêm): vì mỗi file chỉ có 1 đoạn đường (không round-trip), chạy tuần tự từng file là tay máy đã tự đi liên tục — điểm kết thúc file trước = điểm bắt đầu thực tế của file sau.
- **Điểm Home (`robot_home`)**: tay máy có 1 **teaching point tên `robot_home` lưu sẵn trong bộ điều khiển robot** (qua app Fairino) — **không** phải file `.lua`. `WorkflowRunner.RunQueue` gọi `FairinoLuaExecutor.MoveToTeachingPoint("robot_home")` (đọc điểm trực tiếp từ controller bằng `GetRobotTeachingPoint` rồi `MoveJ`) **tự động ở đầu** (sau khi kết nối = "vừa bật/reset") **và ở cuối** (sau khi xong toàn bộ hàng đợi = "xong 1 sản phẩm") mỗi lần chạy. Giữa các bước trong 1 sản phẩm thì **không** quay về Home. Đây là hành vi có sẵn trong code, không cần cấu hình gì thêm.
  - Nếu điểm được lưu dưới tên khác, đổi hằng số `HomeTeachingPoint` trong `WorkflowRunner.cs`.
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
| Menu 6 / `test-machine` báo "Chua cau hinh cong COM" | Chưa nhập cổng COM cho máy đó ở menu 1 |
| `Cup-dropping machine communication error: no valid reply after 3 resend(s)` | Sai cổng COM, sai baud rate/đấu dây RS232-RS485, hoặc máy thả cốc chưa cấp nguồn |
| `Checksum mismatch` / `Length mismatch` từ máy thả cốc | Nhiễu đường truyền hoặc đấu sai chân TX/RX — kiểm tra cách ly & dây tín hiệu |

## Trạng thái triển khai

| Hạng mục | Trạng thái |
|----------|------------|
| Menu + CLI | ✅ Xong |
| Config wizard (DuckDNS, tunnel, robot IP, cổng COM máy ngoại vi) | ✅ Xong |
| `WorkflowRunner` — chạy tuần tự từng bước, mỗi file `.lua` chạy trọn vẹn (nối liền tự nhiên, xem Lua workflow scripts) | ✅ Xong |
| Kiến trúc module máy ngoại vi (`IMachineModule` + `MachineRegistry.Modules`) — thêm máy = thêm 1 module | ✅ Xong |
| Máy thả cốc — module + client serial (`Machines/CupDropping/`) | ✅ Xong |
| `WorkflowQueueBuilder` — sắp xếp bước theo `Position` của máy trước khi gộp thành 1 workflow | ✅ Xong (bước không gắn máy nào thì chưa có vị trí xác định, xem ghi chú ở mục Điều khiển máy) |
| Tự quay về Home (`robot_home`) ở đầu + cuối mỗi lần chạy hàng đợi | ✅ Xong (đọc teaching point `robot_home` từ controller qua SDK — cần đã lưu điểm này trên robot) |
| Kết nối BE thật (`BeApi`) | ❌ Chưa (đang mock) |
| Ánh xạ đơn hàng → danh sách bước (số lượng, vị/topping → queue) | ❌ Chưa |
| `POST /api/orders` → chạy thực tế | ❌ Chưa (mới log + trả 202) |

Xem thêm chi tiết giao thức máy thả cốc tại [`docs/301 Cup-Dropping Machine Serial Communication Protocol V0.0.3.md`](docs/301%20Cup-Dropping%20Machine%20Serial%20Communication%20Protocol%20V0.0.3.md).
