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
- [Deploy (NetBird)](#deploy-netbird)
- [Xử lý sự cố thường gặp](#xử-lý-sự-cố-thường-gặp)
- [Trạng thái triển khai](#trạng-thái-triển-khai)

---

## Kiến trúc tổng quan

```
Cloud BE (đơn hàng/thanh toán)
        │ POST /api/orders  (qua NetBird)
        ▼
IceBot.exe (robot controller, PC tại cửa hàng)
        │
        ▼
Với mỗi bước trong hàng đợi (WorkflowRunner):
  1. Chạy file .lua của bước đó trên tay máy Fairino (LAN 192.168.58.2) — nạp vào, chạy hết
     từ trên xuống dưới, tay máy di chuyển tới vị trí của bước này (không quay về vị trí
     ban đầu giữa các bước — chỉ quay về Home lúc khởi động/reset hoặc sau khi xong 1 sản phẩm)
  2. Nếu bước này có máy ngoại vi gắn kèm (MachineRegistry), NGAY SAU KHI tay máy chạy xong
     bước 1 (đã vào đúng vị trí) → IceBot mở cổng COM, gửi khung lệnh qua RS485
     (System.IO.Ports) cho đúng máy đó — mọi máy ngoại vi đều bắt buộc có đầu RS485 riêng,
     không còn cách kích tín hiệu nào khác (không dùng SetDO/DO 24V qua Fairino nữa)
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
│       ├── Machines/                       # Định danh + điều khiển RS485 cho máy ngoại vi
│       │   ├── IMachineModule.cs           #   interface MỌI máy phải implement (định danh)
│       │   ├── IMachineTrigger.cs          #   interface tuỳ chọn: máy có cổng COM thật mới cần
│       │   ├── IMachineDiagnostics.cs      #   interface tuỳ chọn: query trạng thái cho menu test
│       │   ├── SerialFrameCodec.cs         #   hạ tầng dùng chung: đóng/mở khung, checksum
│       │   ├── MachineRegistry.cs          #   nơi ĐĂNG KÝ module — 1 dòng / máy mới
│       │   └── CupDropping/                #   1 "module" hoàn chỉnh cho 1 máy, 1 thư mục/máy
│       │       ├── CupDroppingMachineModule.cs   # implement IMachineTrigger (+ IMachineDiagnostics)
│       │       ├── CupDroppingMachineClient.cs   # giao thức serial thô (SerialPort)
│       │       └── CupMachineStatus.cs
│       ├── Networking/LocalApiServer.cs  # HTTP API nội bộ (ingress cho NetBird)
│       ├── Robot/FairinoLuaExecutor.cs   # Upload/chạy .lua + MoveToTeachingPoint (Home) trên Fairino
│       └── Workflow/               # WorkflowProvisioner, WorkflowRunner, OrderRequest, OrderQueue
├── workflow/                       # File .lua theo từng bước (gitignored, tải từ BE)
├── test-workflow/                  # File .lua mẫu để test tay robot (KHÔNG phải từ BE, xem README trong đó)
├── deploy/                         # Script cài đặt ingress (DuckDNS+Cloudflare cũ — cho NetBird xem ghi chú Deploy)
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

# Chạy trực tiếp exe vừa build (server + nhận order)
code/src/IceBot/bin/Debug/net472/IceBot.exe

# Mở menu quản trị
code/src/IceBot/bin/Debug/net472/IceBot.exe menu

# Hoặc build bản Release rồi chạy kèm lệnh CLI
dotnet build code/IceBot-IOT.sln -c Release
code/src/IceBot/bin/Release/net472/IceBot.exe serve
```

> Vì `IceBot` target `net472` (.NET Framework, không phải .NET Core/5+), chạy trực tiếp file `.exe` — không dùng `dotnet run`.

## Menu & CLI

Menu quản trị khi chạy `IceBot.exe menu` — menu chính chỉ có 4 dòng; chọn `1` hoặc `2` mở submenu riêng (đánh số lại từ 1 trong submenu đó, không phải mã ghép kiểu `1.1`); `0` luôn là "quay lại" (trong submenu) hoặc "thoát" (menu chính):

```
Menu chinh                       Chon 1 -> CAU HINH                          Chon 2 -> TEST MAY
1. Cau hinh                      1. Cau hinh NetBird                         1. Test tay Robot
2. Test may                      2. Cau hinh he thong (API key, robot IP,    2. Test ket noi may ngoai vi (Serial)
3. Chay he thong                    tai khoan, cong COM)                     0. Quay lai
0. Thoat                         3. Xem cau hinh hien tai
                                  4. Tai file Lua tu BE (mock)
                                  0. Quay lai
```

| Menu | Mục | Chức năng |
|------|-----|-----------|
| Chính | 1 | Cấu hình — mở submenu |
| Chính | 2 | Test máy — mở submenu |
| Chính | 3 | Chạy hệ thống — nhận đơn từ BE (`serve` mode, cổng 5080) |
| Chính | 0 | Thoát |
| Cấu hình | 1 | **Cấu hình NetBird** — chỉ 2 thứ NetBird thực sự cần: setup key + Public URL |
| Cấu hình | 2 | **Cấu hình hệ thống** — mọi thứ còn lại: API key chia sẻ với BE, IP robot, tài khoản cửa hàng, cổng COM máy ngoại vi |
| Cấu hình | 3 | Xem cấu hình hiện tại |
| Cấu hình | 4 | Tải file Lua từ BE (hiện là mock `BeApi.GetLua`) — **ghi nhớ định danh máy đã nhập**, xem bên dưới |
| Cấu hình | 0 | Quay lại menu chính |
| Test máy | 1 | **Test tay Robot** — 2 bước: (1) test kết nối tay máy, (2) nạp + chạy 1 file `.lua` mẫu (xem bên dưới) |
| Test máy | 2 | **Test kết nối máy ngoại vi (Serial)** — test kết nối cho toàn bộ máy ngoại vi cửa hàng này đã khai báo (xem bên dưới) |
| Test máy | 0 | Quay lại menu chính |

### Test máy > 1 — Test tay Robot

Chỉ test tay máy, tách làm 2 bước độc lập, không đụng gì tới máy ngoại vi:

1. **Test kết nối** — `FairinoLuaExecutor.Connect()` (RPC), in `Tay may (IP): connect` hoặc `disconnect (ly do)`.
2. **Nạp file lua mẫu và chạy** — file **tách biệt hoàn toàn** với `workflow/` (nơi chỉ chứa file BE gửi về). Đặt file mẫu tại `test-workflow/robot_test.lua` (xem `code/test-workflow/README.md`). Nếu file chưa có, hoặc bước 1 thất bại, bước này tự bỏ qua kèm thông báo rõ ràng — không lỗi giả.

### Test máy > 2 — Test kết nối máy ngoại vi (Serial)

Test kết nối cho **đúng những máy ngoại vi cửa hàng này thật sự có** — không phải mọi máy từng được code trong `MachineRegistry`. Cơ chế:

- Khi nhập định danh máy ở **Cấu hình > 4** để tải file Lua từ BE (vd `cup_s, ice_chocolate_s`), Edge **tự lưu lại** các định danh này (`SiteSettings.ProvisionedSteps`, dựa trên tên file BE thực sự trả về — kể cả khi nhập bundle như `FR5`/`full`).
- Test máy > 2 lặp qua đúng danh sách đã lưu đó, map ngược về máy qua `MachineRegistry.TryGetModule`, rồi gọi `TestConnection(comPort)` — dùng chung 1 cơ chế cho mọi máy có `IMachineTrigger`, không cần biết máy đó cụ thể là gì.
- Định danh không map được máy nào có RS485 (vd `deliver_tray`, `lay_coc` — thuần di chuyển tay máy) thì tự bỏ qua, không hiện ra.

Kết quả in theo đúng format:

```
cup_s : connect
ice_chocolate_s : disconnect (chua cau hinh cong COM)
```

Không có mục "đăng nhập lại" ở đâu trong menu — đăng nhập là **cổng bắt buộc trước khi menu hiện ra** (xem bên dưới), không phải 1 lựa chọn giữa các mục khác. Muốn đăng nhập lại giữa phiên (không chặn), dùng CLI `IceBot.exe login`.

CLI tương ứng:

| Lệnh | Mục đích |
|------|----------|
| `IceBot.exe` | Mặc định chạy server và bộ nhận order ngay lập tức |
| `IceBot.exe menu` | Mở menu quản trị để cấu hình, đăng ký và test máy |
| `IceBot.exe setup` | Chạy cả 2 wizard (NetBird + hệ thống) liền nhau → `config/icebot.site.env` |
| `IceBot.exe login` | Đăng nhập tài khoản cửa hàng → lưu key BE trả về |
| `IceBot.exe provision` | Tải Lua từ BE (mock) → `workflow/` (+ ghi nhớ định danh máy) |
| `IceBot.exe serve` | Chạy HTTP API nội bộ trên cổng `5080` |
| `IceBot.exe test` | Test tay Robot (kết nối + chạy file mẫu) |
| `IceBot.exe test-machine` | Test kết nối máy ngoại vi (Serial) |

### Đăng nhập cửa hàng (`IceBot.exe login`)

Mỗi cửa hàng có **1 tài khoản riêng do BE cấp**. Đăng nhập chỉ bắt buộc cho thao tác quản trị dùng JWT như đăng ký máy ngoại vi. Server nhận order dùng mTLS nên vẫn khởi động và bán hàng nếu login lỗi:

```
Khoi dong IceBot.exe / IceBot.exe serve
    → khoi dong NetBird + server + bo nhan order mTLS, khong doi login

Chon dang ky may ngoai vi (hoac IceBot.exe register-device)
    → luc nay moi refresh token hoac yeu cau login
    → login that bai chi chan thao tac dang ky, khong chan server nhan order
```

`IceBot.exe login` cho phép đăng nhập lại thủ công. Khi đăng ký máy, Edge thử refresh access token một lần trước khi yêu cầu đăng nhập lại. Operator token chỉ dùng cho API quản trị; mTLS certificate là danh tính riêng của Edge cho nhận order, ACK và deployment.

## Cấu hình site

Cấu hình theo từng cửa hàng lưu tại `config/icebot.site.env` (gitignored, tạo qua Cấu hình > 1 / > 2 hoặc `IceBot.exe setup`):

| Biến | Ý nghĩa |
|------|---------|
| `NETBIRD_SETUP_KEY` | Key thiết lập NetBird — NetBird dùng key này để nhận diện cửa hàng và mở đường vào Edge (thay cho DuckDNS + Cloudflare Tunnel cũ) |
| `PUBLIC_URL` | URL công khai để BE gọi vào IceBot (do NetBird cấp) |
| `BE_API_URL` | Base URL của BE (dự phòng, chưa dùng — đang mock) |
| `API_KEY` | Secret chia sẻ với BE, gửi qua header `X-Api-Key` (chiều BE → IceBot) |
| `ROBOT_IP` | IP control box Fairino (mặc định `192.168.58.2`) |
| `STORE_ACCOUNT` / `STORE_PASSWORD` | Tài khoản cửa hàng do BE cấp — dùng để đăng nhập (bắt buộc lúc khởi động, hoặc `IceBot.exe login` để đăng nhập lại thủ công) |
| `BE_SESSION_KEY` | Key BE trả về sau khi đăng nhập thành công — dùng cho các request IceBot → BE sau này (chiều ngược `API_KEY`) |
| `MACHINE_PORTS` | Cổng COM theo từng loại máy ngoại vi, dạng `cup_dropping:COM3,...` |
| `PROVISIONED_STEPS` | Định danh bước (.lua, không đuôi) đã tải qua Cấu hình > 4, tích luỹ dần — dùng để biết Test máy > 2 cần kiểm tra kết nối cho máy nào |

## Điều khiển máy trong hệ thống

**Mọi file `.lua` đều gắn với 1 định danh máy** — không có chuyện 1 bước không thuộc về máy nào. Mỗi máy là **1 module tự khép kín**; thêm máy mới vào hệ thống = thêm 1 module, không phải sửa rải rác nhiều nơi.

> **Business rule:** mọi máy ngoại vi (dispenser, feeder, máy thả cốc...) đều bắt buộc có **đầu RS485** và **driver điều khiển riêng** (host-side client) — trigger **chỉ qua RS485**, không còn cách nào khác. Không còn khái niệm kích tín hiệu qua `SetDO`/DO 24V của Fairino control box nữa. Ngoại lệ duy nhất: 1 "máy" thuần di chuyển tay máy, không có phần cứng riêng nào để điều khiển (vd trạm đặt khay) — trường hợp này không cần `IMachineTrigger`, vì bản thân nó không phải là 1 thiết bị ngoại vi cần trigger.

### Kiến trúc module

```
IMachineModule (interface, Machines/IMachineModule.cs) — MỌI máy đều implement cái này
  ├─ MachineType : id ổn định (vd "cup_dropping")
  ├─ DisplayName  : tên hiển thị (vd "May tha coc")
  └─ StepNames     : những bước (.lua) thuộc về máy này (vd ["cup_s"])

IMachineTrigger : IMachineModule (interface tuỳ chọn, Machines/IMachineTrigger.cs)
  ├─ Trigger(comPort) : chỉ máy nào có giao thức serial thật (đấu cổng COM vào PC) mới cần
  │                      implement thêm cái này — gọi ngay sau khi tay máy chạy xong bước đó.
  │                      Máy thuần di chuyển tay máy (không có phần cứng serial riêng) chỉ cần
  │                      implement IMachineModule, KHÔNG cần Trigger.
  └─ TestConnection(comPort) : BẮT BUỘC cùng với Trigger — mở cổng COM + gửi lệnh query thật,
                         ném lỗi nếu không có phản hồi. Dùng bởi Test máy > 3 "Kiểm tra kết nối"
                         — tái sử dụng cho MỌI máy có IMachineTrigger, không cần code riêng.

IMachineDiagnostics (interface tuỳ chọn, Machines/IMachineDiagnostics.cs)
  └─ GetStatusText(comPort) : tra ve trang thai may dang text de doc — hien khong co muc menu
                         nao goi truc tiep (menu da don gian hoa chi con test ket noi), nhung
                         van implement san de dung sau (vd goi tu code, hoac 1 menu rieng sau nay)

MachineRegistry.Modules (Machines/MachineRegistry.cs)
  └─ danh sách MỌI máy đã đăng ký (kể cả máy không có Trigger) — nơi DUY NHẤT cần thêm 1 dòng khi có máy mới
```

`WorkflowRunner` (bắn tín hiệu), `ConfigSetupWizard` (hỏi cổng COM), và Test máy > 2 "Test kết nối máy ngoại vi" chỉ đọc các máy implement thêm `IMachineTrigger` — vì chỉ những máy đó mới thật sự cần cổng COM.

### Thêm 1 máy mới — chỉ cần

1. Tạo thư mục `Machines/<TenMay>/`.
2. Máy có phần cứng riêng cần điều khiển (dispenser, feeder...) → **bắt buộc** có đầu RS485: viết `<TenMay>Client.cs` (giao thức serial thô qua RS485, giống `CupDropping/CupDroppingMachineClient.cs` hoặc `IceCream/IceCreamMachineClient.cs` — tái dùng `SerialFrameCodec` nếu cùng khuôn khung lệnh) + `<TenMay>Module.cs` implement `IMachineTrigger` (bắt buộc có cả `Trigger` và `TestConnection`) và `IMachineDiagnostics` nếu có lệnh query trạng thái.
   Máy chỉ thuần di chuyển tay máy, không có phần cứng riêng nào để điều khiển (vd trạm đặt khay): chỉ cần viết `<TenMay>Module.cs` implement `IMachineModule` (không cần `Trigger`).
3. Thêm đúng **1 dòng** vào `MachineRegistry.Modules`:
   ```csharp
   public static readonly IReadOnlyList<IMachineModule> Modules = new IMachineModule[]
   {
       new CupDroppingMachineModule(),
       new TenMayModule(),   // ← thêm dòng này
   };
   ```

Xong — nếu máy có `IMachineTrigger` thì `ConfigSetupWizard` (Cấu hình > 2) tự hỏi thêm cổng COM, `WorkflowRunner` tự bắn tín hiệu đúng bước, Test máy > 2 tự kiểm tra kết nối được (miễn định danh bước của máy đó đã được ghi nhận qua Cấu hình > 4). Không cần sửa `WorkflowRunner.cs`, `ConfigSetupWizard.cs`, hay `ConsoleMenu.cs`.

Máy ngoại vi chưa có driver RS485 (vd topping hiện tại) thì **chưa được coi là đã tích hợp** — vẫn cần đăng ký 1 `IMachineModule` để bước `.lua` của nó có định danh, nhưng phải bổ sung `<TenMay>Client.cs` + `IMachineTrigger` theo đúng rule trên trước khi máy đó thật sự trigger được.

### Thứ tự chạy các bước do BE quyết định

BE gửi kèm trong đơn hàng luôn cả **danh sách file `.lua` và đúng thứ tự chạy** (xem `POST /api/orders` bên dưới) — IceBot **không** tự sắp xếp lại thứ tự máy/bước nữa (không còn khái niệm "vị trí trên dây chuyền" trong code). IceBot chỉ kiểm tra từng file có tồn tại trong `workflow/` rồi chạy đúng theo thứ tự BE gửi qua `WorkflowRunner.RunQueue`.

## Lua workflow scripts

- Mỗi file `.lua` trong `workflow/` là **một bước tay máy**, viết theo quy ước: 1 điểm bắt đầu → 1 điểm kết thúc + `WaitMs` nếu cần chờ — **không** quay lại điểm bắt đầu ở cuối file, và **không** tự kích máy ngoại vi bên trong file (việc đó do IceBot làm qua RS485 sau khi file chạy xong). IceBot không quan tâm tên/note các điểm bên trong file, chỉ nạp và chạy hết nội dung **từ trên xuống dưới** (`FairinoLuaExecutor.RunScript`).
- Được tải về qua Cấu hình > 4 / `IceBot.exe provision` (hiện gọi `BeApi.GetLua` — đang là mock, chưa nối BE thật).
- **Nối nhiều bước liên tục** (không cần merge gì thêm): vì mỗi file chỉ có 1 đoạn đường (không round-trip), chạy tuần tự từng file là tay máy đã tự đi liên tục — điểm kết thúc file trước = điểm bắt đầu thực tế của file sau.
- **Điểm Home (`robot_home`)**: tay máy có 1 **teaching point tên `robot_home` lưu sẵn trong bộ điều khiển robot** (qua app Fairino) — **không** phải file `.lua`. `WorkflowRunner.RunQueue` gọi `FairinoLuaExecutor.MoveToTeachingPoint("robot_home")` (đọc điểm trực tiếp từ controller bằng `GetRobotTeachingPoint` rồi `MoveJ`) **tự động ở đầu** (sau khi kết nối = "vừa bật/reset") **và ở cuối** (sau khi xong toàn bộ hàng đợi = "xong 1 sản phẩm") mỗi lần chạy. Giữa các bước trong 1 sản phẩm thì **không** quay về Home. Đây là hành vi có sẵn trong code, không cần cấu hình gì thêm.
  - Nếu điểm được lưu dưới tên khác, đổi hằng số `HomeTeachingPoint` trong `WorkflowRunner.cs`.
- ⚠️ File mẫu `workflow/lay_coc.lua` hiện có trong repo là **script demo/test** (do FaiRobot Studio sinh, có đi khứ hồi A→B→A) — **không** phải khuôn mẫu cho file bước sản xuất thật, đừng copy cấu trúc round-trip của nó.

## Deploy (NetBird)

Ingress cloud → Edge giờ dùng **[NetBird](https://netbird.io)** thay cho DuckDNS + Cloudflare Tunnel — đây là NetBird CLI/mesh network thật, không phải API riêng của dự án. Ở Cấu hình > 1, chỉ cần nhập setup key (vd `7980A958-3B57-42E1-93EE-3DE008A9AD10`) — IceBot tự lo phần còn lại (`Config/NetBirdSetup.cs`):

1. **Chưa cài NetBird trên máy này (lần đầu chạy IceBot trên 1 Edge PC mới)** → tự động chạy `winget install --id Netbird.Netbird --silent` để cài, không cần thao tác tay.
2. Sau đó (hoặc nếu đã cài sẵn) → tự chạy `netbird up --setup-key <key>` để kết nối.
3. Việc kiểm tra/kết nối này **chạy lại mỗi lần mở IceBot** (menu tương tác lẫn `serve`), ngay sau bước đăng nhập — không chỉ lúc nhập key ở wizard, phòng trường hợp máy Edge bị cài lại/mất NetBird sau này.

Yêu cầu: máy Edge cần có `winget` (có sẵn trên Windows 10/11 bản mới) và kết nối internet ở lần cài đầu tiên; việc cài driver mạng ảo của NetBird cần quyền Administrator — nếu IceBot không chạy quyền admin, bước cài/kết nối sẽ báo lỗi rõ ràng (không treo máy, có timeout) nhưng **không chặn** các phần khác của app.

| Script | Vai trò |
|--------|---------|
| `deploy/icebot/start-serve.ps1` | Build (nếu cần) rồi chạy `IceBot.exe serve` |

⚠️ `deploy/duckdns/` và `deploy/cloudflare/` vẫn còn trong repo nhưng **đã lỗi thời** — thuộc stack ingress cũ, chưa có script tương ứng cho NetBird (đang chờ xác định cơ chế deploy thật của NetBird).

## Xử lý sự cố thường gặp

| Triệu chứng | Nguyên nhân thường gặp |
|-------------|-------------------------|
| `RPC failed with error code ...` khi test robot | Sai `ROBOT_IP`, tay máy chưa bật, hoặc PC không cùng LAN `192.168.58.x` với control box |
| Test máy > 2 / `test-machine` báo "Chua cau hinh cong COM" | Chưa nhập cổng COM cho máy đó ở Cấu hình > 2 |
| `Cup-dropping machine communication error: no valid reply after 3 resend(s)` | Sai cổng COM, sai baud rate/đấu dây RS232-RS485, hoặc máy thả cốc chưa cấp nguồn |
| `Checksum mismatch` / `Length mismatch` từ máy thả cốc | Nhiễu đường truyền hoặc đấu sai chân TX/RX — kiểm tra cách ly & dây tín hiệu |

## Trạng thái triển khai

| Hạng mục | Trạng thái |
|----------|------------|
| Menu + CLI | ✅ Xong |
| Config wizard — tách riêng "Cấu hình NetBird" (setup key, public URL) và "Cấu hình hệ thống" (API key, robot IP, tài khoản cửa hàng, cổng COM) | ✅ Xong |
| Tự cài NetBird qua winget nếu máy chưa có + tự `netbird up` mỗi lần mở app (`Config/NetBirdSetup.cs`) | ✅ Xong |
| Đăng nhập cửa hàng — `BeApi.Login` (mock) → lưu `BE_SESSION_KEY`, bắt buộc trước khi vào menu/serve, `IceBot.exe login` để đăng nhập lại thủ công | ✅ Xong (mock; key chưa được đính kèm vào request thật nào vì chưa có request Edge → BE thật) |
| `WorkflowRunner` — chạy tuần tự từng bước, mỗi file `.lua` chạy trọn vẹn (nối liền tự nhiên, xem Lua workflow scripts) | ✅ Xong |
| Kiến trúc module máy ngoại vi (`IMachineModule` + `MachineRegistry.Modules`) — thêm máy = thêm 1 module | ✅ Xong |
| Máy thả cốc — module + client serial (`Machines/CupDropping/`) | ✅ Xong |
| Máy làm kem — module + client serial RS485 (`Machines/IceCream/`) | ✅ Xong |
| Test máy > 1 "Test tay Robot" — test kết nối + nạp/chạy 1 file `.lua` mẫu từ `test-workflow/` (tách biệt `workflow/`) | ✅ Xong (file mẫu do người dùng tự cung cấp, chưa có sẵn trong repo) |
| Test máy > 2 "Test kết nối máy ngoại vi" — dùng `SiteSettings.ProvisionedSteps` (ghi nhớ từ Cấu hình > 4) để biết cần test máy nào, gọi chung `IMachineTrigger.TestConnection` cho mọi máy | ✅ Xong |
| Tự quay về Home (`robot_home`) ở đầu + cuối mỗi lần chạy hàng đợi | ✅ Xong (đọc teaching point `robot_home` từ controller qua SDK — cần đã lưu điểm này trên robot) |
| Kết nối BE thật (`BeApi`) | ❌ Chưa (đang mock — cả `GetLua` lẫn `Login`) |
| `POST /api/orders` → chạy thực tế | ✅ Xong — BE gửi kèm `steps` (tên file + đúng thứ tự chạy), IceBot kiểm tra file tồn tại rồi chạy qua `OrderQueue` → `WorkflowRunner.RunQueue` (chạy tuần tự, không chặn HTTP thread) |
| POST trạng thái đơn hàng (done/failed) ngược về BE | ❌ Chưa |

Xem thêm chi tiết giao thức máy thả cốc tại [`docs/301 Cup-Dropping Machine Serial Communication Protocol V0.0.3.md`](docs/301%20Cup-Dropping%20Machine%20Serial%20Communication%20Protocol%20V0.0.3.md).
