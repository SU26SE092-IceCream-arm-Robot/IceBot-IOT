# IceBot-IOT

Ứng dụng Edge điều phối hệ thống bán kem tự động gồm máy tính Edge tại kiosk, tay máy Fairino FR5 và các máy ngoại vi. Trong dự án này, **Kiosk và máy Edge là cùng một máy vật lý**.

IceBot nhận Order từ Backend, lưu và điều phối workflow, gửi từng file Lua cho bộ điều khiển Fairino, sau đó gọi driver của máy ngoại vi tương ứng. Thứ tự bước sản xuất do Backend quyết định; Edge không tự sắp xếp lại.

## Trạng thái quan trọng

- `IceBot-Setup.exe` là bootstrapper cài đặt: kiểm tra .NET Framework, cài NetBird, copy ứng dụng, tạo thư mục/quyền và shortcut.
- `IceBot.exe` là runtime sản xuất: tự mở server, kết nối NetBird và bắt đầu pull Order từ BE bằng mTLS. Không cần đăng nhập tài khoản cửa hàng để chạy.
- `InitIceBot.exe` dành cho kỹ thuật viên: đăng nhập, khởi tạo Edge, cấu hình, đăng ký máy ngoại vi và kiểm tra phần cứng.
- Đăng nhập thật với BE, đăng ký Kiosk/Execution Endpoint, cấp mTLS, kích hoạt Kiosk và heartbeat đã được triển khai.
- mTLS `ExecuteOrder` đã có queue bền vững, kiểm tra đúng Kiosk/Endpoint/release/Lua, ACK `Accepted`, chạy lần lượt từng cây và gửi trạng thái về BE.
- API local cũ `POST /api/orders` đã bị loại bỏ; production chỉ có một lifecycle chuẩn là outbound mTLS command pull.
- Đồng bộ Full Edge deployment tải bundle phát hành từ Backend, kiểm tra checksum rồi stage/activate trước khi nhận order. Runtime cũng pull `DeployConfiguration` cùng với các command khác; kỹ thuật viên không chép Lua thủ công vào thư mục workflow.
- Trạng thái từng cây (`Accepted`, `Running`, `Completed`, `Failed`, `RequiresManualIntervention`) được lưu vào outbox bền vững và gửi về BE qua mTLS.

## Kiến trúc

Dự án dùng **Modular Monolith theo nhóm chức năng**. Toàn bộ runtime vẫn được triển khai thành một ứng dụng Edge, nhưng code được chia theo trách nhiệm:

```text
IceBot-Setup.exe → InitIceBot.exe → IceBot.exe
        │
        ├── Api             đăng nhập, API quản trị và mTLS
        ├── Config          kết nối, khởi tạo và lưu cấu hình
        ├── Workflow        nhận Order, provisioning và thực thi
        ├── Machines        registry + driver máy ngoại vi
        ├── Robot           giao tiếp Fairino
        └── Networking      HTTP server cục bộ
```

Các pattern chính:

- Plugin Architecture cho driver DLL của máy ngoại vi.
- Registry Pattern để ánh xạ `MachineType` và Lua step tới driver.
- Producer–Consumer Queue để chỉ một worker điều khiển robot tại một thời điểm.
- Wizard/Orchestrator cho quy trình khởi tạo Edge.
- Durable inbox + execution queue/outbox để chống trùng Order và khôi phục an toàn theo `CommandId`.

## Cấu trúc repository

```text
IceBot-IOT/
├── code/
│   ├── IceBot-IOT.sln
│   ├── lib/fairino-csharp-sdk/          Fairino C# SDK
│   ├── src/
│   │   ├── IceBot/                      runtime chính
│   │   │   ├── Api/
│   │   │   │   ├── Authentication/     login và token cửa hàng
│   │   │   │   ├── Management/         Kiosk, endpoint, thiết bị
│   │   │   │   └── IoT/                heartbeat/probe mTLS
│   │   │   ├── Cli/                    CMD và serve mode
│   │   │   ├── Config/
│   │   │   │   ├── Connectivity/       NetBird
│   │   │   │   ├── Setup/              wizard kỹ thuật viên
│   │   │   │   └── Storage/            cấu hình cục bộ
│   │   │   ├── Machines/               plugin loader/registry; không chứa driver thả cốc
│   │   │   ├── Networking/             local HTTP API
│   │   │   ├── Robot/                  Fairino Lua executor
│   │   │   └── Workflow/
│   │   │       ├── Orders/             receiver, inbox và queue
│   │   │       └── Provisioning/       cài Full Edge bundle
│   │   ├── IceBot.Driver.Abstractions/ contract công khai cho plugin
│   │   ├── InitIceBot/                  entry point công cụ kỹ thuật
│   │   └── IceBot.Setup/                bootstrapper tạo IceBot-Setup.exe
│   ├── test-workflow/                   Lua mẫu để test robot
│   └── workflow/                        Lua production, site-local/gitignored
├── driver-sdk/                          hướng dẫn và template driver
├── DRIVER-DLL/                          package driver build sẵn, được nhúng vào bộ cài Edge
├── harness/                             test tự động
├── context/                             context, architecture và test Lua
│   ├── PROJECT_CONTEXT.md                nguồn sự thật chi tiết của dự án
│   ├── GLOBAL_WORKING_CONTEXT.md        quy tắc làm việc chung
│   ├── lua-tests/                        Lua dùng cho kiểm thử
│   ├── protocols/                        protocol thiết bị
│   ├── Hardware_Architecture/            tài liệu kiến trúc phần cứng
│   └── System_Architecture/              tài liệu kiến trúc hệ thống
├── deploy/installer/                    script đóng gói Setup + payload
├── deploy/cloudflare/, deploy/duckdns/  legacy, không dùng trong flow mới
└── firmware/                            firmware liên quan
```

## Tài liệu tham chiếu

- Project context: `context/PROJECT_CONTEXT.md`
- Global working context: `context/GLOBAL_WORKING_CONTEXT.md`
- Protocols: `context/protocols/`
- Hardware Architecture: `context/Hardware_Architecture/`
- System Architecture: `context/System_Architecture/`
- Lua test files: `context/lua-tests/`

## Yêu cầu

- Windows 10/11.
- Máy build cần .NET SDK hỗ trợ `.NET Framework 4.7.2` và `.NET 8`.
- Máy Edge không cần SDK; `IceBot-Setup.exe` tự kiểm tra .NET Framework runtime và cài NetBird.
- Edge và Fairino FR5 cùng LAN; IP mặc định của Fairino là `192.168.58.2`.
- Cổng serial và driver tương ứng cho từng máy ngoại vi; transport có thể là RS232 hoặc RS485 theo protocol thiết bị.
- Tài khoản cửa hàng do BE cấp và Kiosk Code riêng được in trên vỏ máy.

## Build và chạy

```powershell
.\code\scripts\restore-fairino-sdk-dependencies.ps1
dotnet build code/IceBot-IOT.sln --configuration Release
```

Fairino C# SDK phụ thuộc assembly legacy `CookComputing.XmlRpcV2`. Package đã được lưu cùng
repository tại `code/lib/fairino-csharp-sdk-robot3.7.8/packages`; chạy script restore một lần sau clean clone
trước khi build với `--no-restore`.

Sau khi build Release:

```powershell
# Runtime sản xuất: server + nhận Order
code/src/IceBot/bin/Release/net472/IceBot.exe

# Công cụ cấu hình và test dành cho kỹ thuật viên
code/src/IceBot/bin/Release/net472/InitIceBot.exe
```

`IceBot.exe serve` là alias tường minh của chế độ runtime. Hai file EXE phải nằm cùng thư mục để
dùng chung `config/`, `certificates/`, `workflow/` và `data/`; các Lua kiểm thử nằm trong `context/lua-tests/`. Driver là ngoại lệ:
cả bản dev và production đều đọc từ `C:\ProgramData\IceBot\drivers`.

### Tạo package cài đặt

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\installer\build-package.ps1
```

File cài đặt duy nhất được tạo tại `artifacts/installer/IceBot-win-x64/IceBot-Setup.exe`. Runtime, Fairino SDK `robot3.7.8`, driver `bt_cup_l90` và driver máy kem đều được nhúng trong file; chỉ cần phân phối file EXE này.

Payload chỉ chứa runtime bất biến. Script đóng gói loại `config/`, `certificates/`, `data/`, `drivers/` và `workflow/` của máy build để không nhúng token, PFX, Order hay deployment cục bộ. Installer từ chối bundle chứa các thư mục mutable này.

Máy Windows đã có .NET Framework 4.7.2+ thì không cần bộ cài framework. Để tạo package offline đầy đủ, truyền thêm đường dẫn bộ cài .NET Framework và NetBird:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\installer\build-package.ps1 `
  -DotNetFrameworkInstaller "D:\Installers\ndp48-x86-x64-allos-enu.exe" `
  -NetBirdInstaller "D:\Installers\netbird-installer.msi"
```

## Lần đầu cài một Edge mới

Flow chuẩn:

```text
IceBot-Setup.exe → InitIceBot.exe → IceBot.exe
```

### 1. `IceBot-Setup.exe` — cài môi trường

Chạy bằng quyền Administrator. Setup sẽ:

1. Kiểm tra .NET Framework 4.7.2+; nếu thiếu, chạy bộ cài offline trong `prerequisites/`.
2. Cài NetBird từ installer offline; nếu không có thì dùng `winget`.
3. Mở hộp thoại để người dùng chọn thư mục cài đặt; mặc định là `C:\Program Files\IceBot`.
4. Xác minh SHA-256 của Fairino SDK và hai driver, tạo dữ liệu ứng dụng và cài driver vào kho dùng chung
   `C:\ProgramData\IceBot\drivers`; chỉ cấp quyền ghi cần thiết cho tài khoản Windows đang cài đặt.
   Thư mục đích dùng chính `machineType` trong manifest (`bt_cup_l90`, `ice_cream`); khi nâng cấp,
   Setup loại thư mục legacy trùng `machineType` để registry không phụ thuộc thứ tự duyệt filesystem.
5. Tạo shortcut `IceBot` và `Init IceBot` trên Desktop/Start Menu.
6. Từ chối cài/nâng cấp nếu `IceBot.exe` hoặc `InitIceBot.exe` còn chạy, tránh trộn binary cũ và mới.

Setup không đăng nhập, không nhận Kiosk Code/NetBird key, không đăng ký Edge và không tự chạy hệ thống bán hàng.
Nếu đóng hoặc hủy hộp thoại chọn thư mục, Setup dừng mà chưa thay đổi file. Khi triển khai tự động,
có thể bỏ qua hộp thoại bằng `IceBot-Setup.exe --install-dir "D:\IceBot"`. Dùng `IceBot-Setup.exe --validate-only` để chỉ xác minh bundle mà không cài đặt.

### 2. `InitIceBot.exe` — khởi tạo Edge

Kỹ thuật viên thực hiện:

1. Đăng nhập bằng tài khoản cửa hàng.
2. Chọn **Cấu hình → Thiết lập Edge lần đầu → Bắt đầu / tiếp tục thiết lập tự động**.
3. Nhập **Kiosk Code in trên vỏ máy** nếu máy chưa lưu code.
4. Nhập NetBird setup key.
5. Xác nhận Robot IP, hardware profile và nhập cổng COM riêng cho từng máy ngoại vi.

Các bước còn lại chạy tự động:

1. Kiểm tra NetBird đã được Setup cài và chạy `netbird up`.
2. Nếu máy đã lưu `KIOSK_ID`, tái sử dụng ID đó.
3. Nếu chưa có, tìm Kiosk theo đúng Kiosk Code; không tìm thấy thì tự đăng ký Kiosk dưới cửa hàng duy nhất mà tài khoản được truy cập.
4. Tìm hoặc tạo Full Edge Execution Endpoint với code `EDGE-{WINDOWS_MACHINE_NAME}`.
5. Lưu `KIOSK_ID` và `EXECUTION_ENDPOINT_ID` vào cấu hình cục bộ.
6. Tạo hoặc tái sử dụng certificate RSA-3072 tại `certificates/icebot-edge-client.pfx`.
7. Tạo `FULL_EDGE_RUNTIME_ID`, gửi fingerprint certificate để provision endpoint và kích hoạt Kiosk.
8. Gửi heartbeat mTLS thật để xác nhận Edge kết nối được BE.

Nếu endpoint đã `Active`, InitIceBot yêu cầu đúng PFX hiện có; không tự tạo certificate mới vì fingerprint sẽ không khớp với BE.

### 3. `IceBot.exe` — vận hành bán hàng

Sau khi khởi tạo thành công, chạy `IceBot.exe`. Runtime không cài dependency và không yêu cầu tài khoản cửa hàng; nó chỉ kết nối lại NetBird, mở server và bắt đầu nhận Order.

## Trách nhiệm của từng chương trình

### `IceBot-Setup.exe`

- Chỉ cài môi trường và application payload.
- Có manifest yêu cầu quyền Administrator.
- Có thể chạy lại để nâng cấp file chương trình mà vẫn giữ `config`, certificate, workflow và dữ liệu runtime.
- Không chứa nghiệp vụ cửa hàng và không chạy server bán hàng.

### `IceBot.exe`

- Không yêu cầu login hoặc refresh JWT.
- Kiểm tra/kết nối NetBird nếu đã có setup key.
- Mở local API tại `http://localhost:5080/`.
- Bắt đầu pull `ExecuteOrder` từ BE mỗi 5 giây nếu đủ cấu hình mTLS.
- Hiển thị cửa sổ CMD, PID, URL, trạng thái API/order pull và heartbeat log mỗi 30 giây.
- Nhập `exit` để dừng bình thường; nhập `test` để chạy workflow test đã cấu hình.

Login thất bại trong `InitIceBot.exe` không làm dừng một `IceBot.exe` đang bán hàng.

### `InitIceBot.exe`

Chỉ hiển thị menu sau khi đăng nhập tài khoản cửa hàng thành công. Menu cấu hình được nhóm theo tác vụ để kỹ thuật viên không phải chọn giữa các thao tác trùng nhau:

```text
1. Cau hinh
   1. Thiet lap Edge lan dau
      1. Bat dau / tiep tuc thiet lap tu dong
      2. Kiem tra dieu kien va tien do thiet lap
   2. Cau hinh ket noi
      1. Backend API URL
      2. NetBird
      3. Kiem tra ket noi Backend qua mTLS
      4. Xem thong tin chung chi mTLS
   3. Cau hinh thiet bi
      1. Cau hinh Robot
      2. Quan ly may ngoai vi
         1. Danh sach thiet bi
         2. Dang ky thiet bi moi voi Backend
         3. Kiem tra ket noi Serial
      3. Cau hinh cong COM
      4. Bao cao lai hardware profile
   4. Xem trang thai cau hinh
   5. Cong cu nang cao
      1. Dong bo deployment ngay
      2. Gui lai hardware snapshot
      3. Gui lai report dang cho
      4. Gui heartbeat va readiness ngay
2. Test may
   1. Test tay Robot
   2. Test ket noi may ngoai vi (Serial)
0. Thoat
```

Mục đồng bộ Lua dùng để cài hoặc kiểm tra lại một bản phát hành cấu hình đã được Backend tạo. Trong vận hành bình thường, `IceBot.exe` cũng pull `DeployConfiguration` cùng với các command khác; kỹ thuật viên không chép Lua thủ công vào thư mục workflow.

Menu tách hai mức: `SETUP HOÀN TẤT` kiểm tra Kiosk, Execution Endpoint, Full Edge Runtime, Backend HTTPS, NetBird, PFX mTLS, Robot IP và hardware profile; `SẴN SÀNG SẢN XUẤT` yêu cầu thêm release/deployment cùng thư mục workflow active hợp lệ. Đồng bộ deployment thủ công và gửi lại report chỉ nằm trong **Công cụ nâng cao**; runtime production vẫn tự nhận command và retry report.
## Cấu hình và danh tính

Cấu hình site nằm trong `config/icebot.site.env` cạnh file EXE và không được commit. Các giá trị quan trọng:

| Giá trị | Mục đích |
|---|---|
| `BE_API_URL` | URL HTTPS của Backend mà runtime Edge dùng để pull command, report readiness và tải deployment metadata |
| `NETBIRD_SETUP_KEY` | Kết nối Edge vào mạng NetBird |
| `KIOSK_CODE` | Code vật lý do kỹ thuật viên nhập một lần |
| `KIOSK_ID` | ID do BE trả về, được tái sử dụng ở những lần sau |
| `EXECUTION_ENDPOINT_ID` | Danh tính endpoint nhận lệnh của chính Edge |
| `FULL_EDGE_RUNTIME_ID` | Runtime identity ổn định của Full Edge |
| `EXECUTION_CLIENT_CERT_PATH` | Đường dẫn PFX dùng cho mTLS |
| `MACHINE_PORTS` | Ánh xạ `MachineType:COM` cho máy ngoại vi |
| `MACHINE_DEVICE_IDS` | Ánh xạ `MachineType:DeviceId` do BE cấp |

PFX dùng mật khẩu từ `ICEBOT_EXECUTION_CLIENT_CERT_PASSWORD` nếu được cấp. Nếu không, Edge tạo mật khẩu ngẫu nhiên và lưu bản mã hóa bằng Windows DPAPI theo tài khoản vận hành tại file `.password.dpapi`; mật khẩu không được ghi vào site config. PFX passwordless cũ được mã hóa lại khi tái sử dụng. Khi dùng mTLS trên .NET Framework, PFX được load bằng Windows user key store (`UserKeySet | Exportable`) để Schannel có thể sử dụng private key; không dùng `EphemeralKeySet`.

`InitIceBot.exe` tạo PFX RSA-3072 cùng private key tại Edge (mặc định `certificates/icebot-edge-client.pfx`) và chỉ gửi SHA-256 fingerprint để Backend provision endpoint. Private key/PFX không rời Edge; Backend dùng fingerprint đã provision để xác thực request mTLS.

Runtime Full Edge phải dùng listener HTTPS mTLS riêng, ví dụ `https://edge-api.icebot.io.vn:8443`, được định tuyến qua NetBird trực tiếp đến Kestrel hoặc TLS passthrough. Không dùng HTTP và không dùng reverse proxy HTTP kết thúc TLS trên đường này, vì Kestrel sẽ không nhận được client certificate. URL public có thể dùng cho thao tác quản trị trước khi setup, nhưng không thay thế `BE_API_URL` của runtime sau khi endpoint đã provision.

Server certificate của URL Edge phải khớp hostname và được Windows trên Edge tin cậy. Object-storage `DownloadUrl` là kết nối HTTPS độc lập: endpoint đó cũng phải reachable và có certificate tin cậy từ máy Edge.

## Nhận và xử lý Order

### Luồng mTLS từ BE — hiện tại

```text
BE
  → ExecuteOrder command
  → Edge pull bằng certificate mTLS
  → kiểm tra schema 3/4/5, Kiosk, Endpoint, release và checksum Lua
  → lưu payload bất biến tại data/order-inbox/{CommandId}.json
  → tạo job từng cây tại data/order-jobs/{CommandId}.json
  → giới hạn 4 cây/Order và chỉ một phiên khách hàng chưa hoàn tất tại một thời điểm
  → ACK Accepted; nếu runtime dừng ở cửa sổ ACK, lần khởi động sau replay ACK từ durable job rồi kích hoạt queue
  → chạy tuần tự từng cây, mỗi cây là một workflow home-to-home
  → lưu tiến độ và gửi report qua data/report-outbox
```

Nếu Edge khởi động lại giữa lúc một cây đang chạy, hệ thống không tự làm lại cây đó mà chuyển sang `RequiresManualIntervention` và dừng các đơn sau để tránh bán trùng.

### API order local cũ

`POST /api/orders`, `OrderQueue` và lifecycle nhận order inbound đã bị loại bỏ. Runtime production chỉ nhận `ExecuteOrder` bằng outbound mTLS pull; không tạo lại local API như một lifecycle thứ hai.
## Lua và robot

Mỗi file `.lua` là một bước chuyển động của tay máy. Khi thực thi một sản phẩm:

1. Kết nối Fairino tại `192.168.58.2`.
2. Đi tới teaching point `IceBot_Home` lưu trong Fairino controller.
3. Với từng bước: `LuaUpload → ProgramLoad → ProgramRun` và chờ hoàn thành.
4. Nếu module của bước implements `IMachineTrigger`, gọi driver RS485 ngay sau khi tay máy đến vị trí.
5. Sau toàn bộ workflow, quay lại `IceBot_Home`.

`IceBot_Home` không phải file Lua. Lua production nằm trong `workflow/` tại máy Edge và không được commit.

Trên controller Web 3.7.7, nếu XML-RPC trả `143`/`-4` dù điểm `IceBot_Home` đã được xác minh, runtime dùng bộ tọa độ fallback đã kiểm thử và chỉ áp dụng cho đúng tên điểm này.

### Đồng bộ Lua

Code provisioning hỗ trợ pull `DeployConfiguration` bằng mTLS, tải ZIP từ object storage, kiểm tra kích thước/SHA-256 và chỉ cài bundle hợp lệ. MinIO production dùng `https://artifacts.internal.icebot.io.vn`; Edge đã tải và xác minh bundle thật thành công. ACK deployment không gửi `physicalOutputMayHaveOccurred`; trường này chỉ được gửi với giá trị `false` khi Edge từ chối `ExecuteOrder` trước sản xuất. Report deployment được flush theo `SequenceNumber` để luôn gửi `Installed` trước `Active`.
Order mTLS dùng trực tiếp `{RobotArtifactId}.lua` theo contract deployment mới.

## Máy ngoại vi và plugin driver

Máy ngoại vi được Edge điều khiển trực tiếp phải có serial transport và protocol điều khiển được mô tả, cùng Edge plugin driver tương ứng; transport có thể là RS232 hoặc RS485 theo từng thiết bị. Lua chỉ đưa tay máy tới vị trí; tín hiệu vận hành thiết bị được gửi từ plugin DLL sau khi Lua hoàn tất. RS485 là lớp vật lý; protocol lệnh và frame có thể riêng theo từng thiết bị.

Core `code/src/IceBot/Machines/` hiện chỉ còn plugin loader và registry. Không có code giao thức
hay driver thiết bị cụ thể nào được compile vào `IceBot.exe`. Nếu thư mục
`C:\ProgramData\IceBot\drivers` trống,
`MachineRegistry.Modules` cũng trống.

Driver máy thả cốc đã được tách hoàn toàn khỏi `IceBot.exe`. Package build sẵn nằm tại:

```text
DRIVER-DLL/CupDropping/
├── driver.json
└── IceBot.Driver.CupDropping.dll
```

`IceBot-Setup.exe` tự xác minh rồi cài package máy thả cốc vào
`C:\ProgramData\IceBot\drivers\bt_cup_l90\` và package máy kem vào
`C:\ProgramData\IceBot\drivers\ice_cream\`. Khi cài lại/nâng cấp, Setup dùng `machineType` làm tên
thư mục chuẩn và dọn các thư mục legacy có cùng `machineType`; plugin bên thứ ba khác không bị xóa.

Để thêm hoặc thay máy mà không sửa source IceBot, tạo plugin target `net472` dựa trên `IceBot.Driver.Abstractions`, sau đó cài:

```text
C:\ProgramData\IceBot\drivers\<driver-name>\
├── driver.json
└── Vendor.Driver.dll
```

Driver phải có public entry type, constructor không tham số và implement `IMachineModule`; thiết bị cần giao tiếp serial implement thêm `IMachineTrigger`. `driver.json` chứa schema, `machineType`, tên DLL, entry type, version và SHA-256.

`MachineType` là định danh ổn định dùng để giữ ánh xạ COM/DeviceId khi thay DLL. Khởi động lại
IceBot sau khi cài hoặc thay plugin. Xem `driver-sdk/README.md`, template trong
`driver-sdk/IceBot.Driver.Template` và driver thật trong `driver-sdk/IceBot.Driver.CupDropping`.

Driver máy kem tích hợp cũ đã bị xóa khỏi core; plugin DLL máy kem hiện được build độc lập và
được nhúng/cài cùng `IceBot-Setup.exe` theo đúng contract trên.

Đăng ký máy với BE tại **InitIceBot → Cấu hình → Cấu hình thiết bị → Quản lý máy ngoại vi → Đăng ký thiết bị mới với Backend**. BE trả `DeviceId`; Edge lưu ánh xạ đó trong `MACHINE_DEVICE_IDS`. Menu **Danh sách máy ngoại vi** chỉ đọc dữ liệu cục bộ và hiển thị máy nào chưa đăng ký.

`MACHINE_DEVICE_IDS` chỉ dùng cho máy ngoại vi được Edge điều khiển trực tiếp, chẳng hạn máy thả cốc qua RS485. Nó không phải là danh sách toàn bộ thiết bị vật lý của kiosk.

Máy kem/dispenser có thể hoạt động độc lập và không kết nối Edge. Backend là nơi sở hữu topology của thiết bị đó:

```text
Kiosk
  -> Device: ICE_CREAM_DISPENSER_01
  -> IngredientDispenserState: từng bình/container nguyên liệu
```

Edge chỉ report phần cứng robot mà nó thực sự điều khiển (`reported-devices`), readiness và execution. Khi có sensor cho máy kem, sensor gateway có thể gửi inventory observation vào các `IngredientDispenserStateId` đã được Backend cấu hình. Chế độ `Simulated` chỉ giả lập sensor gateway này cho Development; nó không khẳng định Edge điều khiển máy kem.

## Kiểm thử

```powershell
dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj --configuration Release --no-restore
```

Trong `ICEBOT_ROBOT_EXECUTION_MODE=Simulated`, Edge mô phỏng cả tay robot và lệnh `TriggerDevice`: vẫn kiểm tra plugin/machine type nhưng không mở COM và không gọi driver vật lý. Edge báo capability `ROBOT_ARM` tại workcell `ARM_PRIMARY` và `safety=Safe` để Backend dispatch cùng contract với production release; log readiness cũng in rõ `safety` và `mode` đã gửi. Menu test serial quét `TriggerDevice` trong Lua active (không dựa vào tên artifact UUID) rồi chỉ gọi `TestConnection` trên COM đã cấu hình. Alias Lua `icemachine` được ánh xạ về driver `ice_cream`; report hoàn tất/thất bại gửi `physicalOutputMayHaveOccurred=false`. Hardware snapshot không đổi sẽ tái sử dụng cùng `snapshotRevision` và `observedAt` để retry idempotent, tránh HTTP 409 từ Backend. Trong physical mode, mỗi readiness probe kết nối Fairino và chỉ báo `safety=Safe`/capability `ROBOT_ARM` sau khi SDK communication bình thường, E-stop bằng 0, SI0/SI1 bằng 0 và cả mã lỗi chính/phụ bằng 0. Lỗi đọc telemetry hoặc bất kỳ tín hiệu không an toàn nào sẽ báo `Unknown`/`Unsafe` và không công bố capability.

Lần xác minh gần nhất: **123/123 test passed**, gồm smoke test điều hướng menu Cấu hình của `InitIceBot`, capability/safety dispatch simulated và physical Fairino đã xác minh telemetry, discovery máy ngoại vi từ Lua active, simulator ngoại vi, alias machine type, hardware snapshot idempotency. Báo cáo chi tiết: [testing/UNIT_TEST_REPORT.md](testing/UNIT_TEST_REPORT.md).

## Các phần chưa hoàn thành

- Backpressure đầy đủ cho inbox: dung lượng đĩa, tuổi Order và telemetry.
- Kiểm tra nguyên liệu trước từng cây và chức năng pause/resume/reconcile cho kỹ thuật viên.
- Gửi telemetry trạng thái riêng của từng máy ngoại vi về BE.
- Xác thực deployment Lua với máy Fairino thật, gồm download endpoint, mTLS transport và activation report.

Chi tiết đầy đủ và các quyết định thiết kế nằm trong [context/PROJECT_CONTEXT.md](context/PROJECT_CONTEXT.md).
