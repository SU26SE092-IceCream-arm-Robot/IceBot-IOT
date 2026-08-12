# IceBot-IOT

Ứng dụng Edge điều phối hệ thống bán kem tự động gồm máy tính Edge tại kiosk, tay máy Fairino FR5 và các máy ngoại vi. Trong dự án này, **Kiosk và máy Edge là cùng một máy vật lý**.

IceBot nhận Order từ Backend, lưu và điều phối workflow, gửi từng file Lua cho bộ điều khiển Fairino, sau đó gọi driver của máy ngoại vi tương ứng. Thứ tự bước sản xuất do Backend quyết định; Edge không tự sắp xếp lại.

## Trạng thái quan trọng

- `Setup.exe` là bootstrapper cài đặt: kiểm tra .NET Framework, cài NetBird, copy ứng dụng, tạo thư mục/quyền và shortcut.
- `IceBot.exe` là runtime sản xuất: tự mở server, kết nối NetBird và bắt đầu pull Order từ BE bằng mTLS. Không cần đăng nhập tài khoản cửa hàng để chạy.
- `InitIceBot.exe` dành cho kỹ thuật viên: đăng nhập, khởi tạo Edge, cấu hình, đăng ký máy ngoại vi và kiểm tra phần cứng.
- Đăng nhập thật với BE, đăng ký Kiosk/Execution Endpoint, cấp mTLS, kích hoạt Kiosk và heartbeat đã được triển khai.
- mTLS `ExecuteOrder` hiện mới **xác thực, lưu bền vững vào inbox và ACK `Received`**; chưa chuyển Order đó thành job điều khiển robot.
- API cũ `POST /api/orders` có thể nhận danh sách tên file Lua và đưa vào hàng đợi chạy robot.
- Đồng bộ Full Edge deployment đã có code, nhưng đang **tạm hoãn** vì BE chưa có Lua production/deployment để tải xuống.
- Gửi trạng thái hoàn thành/thất bại của quá trình sản xuất về BE chưa được triển khai.

## Kiến trúc

Dự án dùng **Modular Monolith theo nhóm chức năng**. Toàn bộ runtime vẫn được triển khai thành một ứng dụng Edge, nhưng code được chia theo trách nhiệm:

```text
Setup.exe → InitIceBot.exe → IceBot.exe
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
- Local durable inbox để chống lưu trùng Order mTLS theo `CommandId`.

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
│   │   │   ├── Machines/               module và driver tích hợp sẵn
│   │   │   ├── Networking/             local HTTP API
│   │   │   ├── Robot/                  Fairino Lua executor
│   │   │   └── Workflow/
│   │   │       ├── Orders/             receiver, inbox và queue
│   │   │       └── Provisioning/       cài Full Edge bundle
│   │   ├── IceBot.Driver.Abstractions/ contract công khai cho plugin
│   │   ├── InitIceBot/                  entry point công cụ kỹ thuật
│   │   └── IceBot.Setup/                bootstrapper tạo Setup.exe
│   ├── test-workflow/                   Lua mẫu để test robot
│   └── workflow/                        Lua production, site-local/gitignored
├── driver-sdk/                          hướng dẫn và template driver
├── harness/                             test tự động
├── context/PROJECT_CONTEXT.md           nguồn sự thật chi tiết của dự án
├── deploy/installer/                    script đóng gói Setup + payload
├── deploy/cloudflare/, deploy/duckdns/  legacy, không dùng trong flow mới
├── docs/                                tài liệu giao thức phần cứng
└── firmware/                            firmware liên quan
```

## Yêu cầu

- Windows 10/11.
- Máy build cần .NET SDK hỗ trợ `.NET Framework 4.7.2` và `.NET 8`.
- Máy Edge không cần SDK; `Setup.exe` tự kiểm tra .NET Framework runtime và cài NetBird.
- Edge và Fairino FR5 cùng LAN; IP mặc định của Fairino là `192.168.58.2`.
- USB-RS485/cổng COM và driver tương ứng cho các máy ngoại vi.
- Tài khoản cửa hàng do BE cấp và Kiosk Code riêng được in trên vỏ máy.

## Build và chạy

```powershell
dotnet build code/IceBot-IOT.sln
```

Sau khi build Debug:

```powershell
# Runtime sản xuất: server + nhận Order
code/src/IceBot/bin/Debug/net472/IceBot.exe

# Công cụ cấu hình và test dành cho kỹ thuật viên
code/src/IceBot/bin/Debug/net472/InitIceBot.exe
```

`IceBot.exe serve` là alias tường minh của chế độ runtime. Hai file EXE phải nằm cùng thư mục để dùng chung `config/`, `certificates/`, `drivers/`, `workflow/`, `test-workflow/` và `data/`.

### Tạo package cài đặt

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\installer\build-package.ps1
```

Package được tạo tại `artifacts/installer/IceBot-win-x64/`, gồm `Setup.exe` self-contained và thư mục `payload/`. Phải phân phối **cả thư mục**, không chỉ copy riêng `Setup.exe`.

Máy Windows đã có .NET Framework 4.7.2+ thì không cần bộ cài framework. Để tạo package offline đầy đủ, truyền thêm đường dẫn bộ cài .NET Framework và NetBird:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\deploy\installer\build-package.ps1 `
  -DotNetFrameworkInstaller "D:\Installers\ndp48-x86-x64-allos-enu.exe" `
  -NetBirdInstaller "D:\Installers\netbird-installer.msi"
```

## Lần đầu cài một Edge mới

Flow chuẩn:

```text
Setup.exe → InitIceBot.exe → IceBot.exe
```

### 1. `Setup.exe` — cài môi trường

Chạy bằng quyền Administrator. Setup sẽ:

1. Kiểm tra .NET Framework 4.7.2+; nếu thiếu, chạy bộ cài offline trong `prerequisites/`.
2. Cài NetBird từ installer offline; nếu không có thì dùng `winget`.
3. Mở hộp thoại để người dùng chọn thư mục cài đặt; mặc định là `C:\Program Files\IceBot`.
4. Tạo `config/`, `certificates/`, `drivers/`, `workflow/`, `test-workflow/`, `data/` và chỉ cấp quyền ghi cho tài khoản Windows đang cài đặt.
5. Tạo shortcut `IceBot` và `Init IceBot` trên Desktop/Start Menu.

Setup không đăng nhập, không nhận Kiosk Code/NetBird key, không đăng ký Edge và không tự chạy hệ thống bán hàng.
Nếu đóng hoặc hủy hộp thoại chọn thư mục, Setup dừng mà chưa thay đổi file. Khi triển khai tự động,
có thể bỏ qua hộp thoại bằng `Setup.exe --install-dir "D:\IceBot"`.

### 2. `InitIceBot.exe` — khởi tạo Edge

Kỹ thuật viên thực hiện:

1. Đăng nhập bằng tài khoản cửa hàng.
2. Chọn **Cấu hình → Khởi tạo Edge mới**.
3. Nhập **Kiosk Code in trên vỏ máy** nếu máy chưa lưu code.
4. Nhập NetBird setup key.

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

### `Setup.exe`

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

Chỉ hiển thị menu sau khi đăng nhập tài khoản cửa hàng thành công.

```text
1. Cau hinh
   1. Khoi tao Edge moi
   2. Cau hinh NetBird
   3. Cau hinh he thong
   4. Xem cau hinh hien tai
   5. Dong bo deployment Lua tu BE (mTLS)
   6. Dang ky may ngoai vi voi BE
   7. Danh sach may ngoai vi
2. Test may
   1. Test tay Robot
   2. Test ket noi may ngoai vi (Serial)
0. Thoat
```

Mục đồng bộ Lua được giữ lại nhưng chưa phải bước bắt buộc cho đến khi BE có Lua production.

## Cấu hình và danh tính

Cấu hình site nằm trong `config/icebot.site.env` cạnh file EXE và không được commit. Các giá trị quan trọng:

| Giá trị | Mục đích |
|---|---|
| `BE_API_URL` | Mặc định `https://api.icebot.io.vn` |
| `NETBIRD_SETUP_KEY` | Kết nối Edge vào mạng NetBird |
| `KIOSK_CODE` | Code vật lý do kỹ thuật viên nhập một lần |
| `KIOSK_ID` | ID do BE trả về, được tái sử dụng ở những lần sau |
| `EXECUTION_ENDPOINT_ID` | Danh tính endpoint nhận lệnh của chính Edge |
| `FULL_EDGE_RUNTIME_ID` | Runtime identity ổn định của Full Edge |
| `EXECUTION_CLIENT_CERT_PATH` | Đường dẫn PFX dùng cho mTLS |
| `MACHINE_PORTS` | Ánh xạ `MachineType:COM` cho máy ngoại vi |
| `MACHINE_DEVICE_IDS` | Ánh xạ `MachineType:DeviceId` do BE cấp |

Password PFX chỉ đọc từ biến môi trường `ICEBOT_EXECUTION_CLIENT_CERT_PASSWORD`, không lưu vào file cấu hình. Có thể override cấu hình bằng các biến môi trường mang tiền tố `ICEBOT_`.

URL public mặc định đủ cho login và API quản trị. Nếu reverse proxy không chuyển client certificate, `BE_API_URL` phải được đổi sang **private HTTPS URL của BE trên NetBird**. Không sử dụng HTTP cho mTLS.

## Nhận và xử lý Order

### Luồng mTLS từ BE — hiện tại

```text
BE
  → ExecuteOrder command
  → Edge pull bằng certificate mTLS
  → kiểm tra schema 4 và các định danh Order
  → lưu payload bất biến tại data/order-inbox/{CommandId}.json
  → chống trùng bằng CommandId
  → ACK Received
```

Luồng này hiện **chưa chạy robot** và chưa ACK `Accepted`. Phần tiếp theo cần chuyển inbox thành execution job bền vững, kiểm tra endpoint/kiosk/release, chạy từng đơn vị sản phẩm và gửi trạng thái về BE.

### Local HTTP API — luồng cũ

| Method | Path | Trạng thái |
|---|---|---|
| `GET` | `/health` | Đã triển khai |
| `POST` | `/api/orders` | Nhận `orderId` + danh sách `steps`, kiểm tra Lua và đưa vào worker |
| `POST` | `/api/provision` | Stub |

Với `/api/orders`, Edge chạy đúng thứ tự file mà BE gửi, không tự map sản phẩm hoặc sắp xếp lại. `OrderQueue` chỉ có một worker nên không có hai Order cùng điều khiển tay máy.

## Lua và robot

Mỗi file `.lua` là một bước chuyển động của tay máy. Khi thực thi một sản phẩm:

1. Kết nối Fairino tại `192.168.58.2`.
2. Đi tới teaching point `robot_home` lưu trong Fairino controller.
3. Với từng bước: `LuaUpload → ProgramLoad → ProgramRun` và chờ hoàn thành.
4. Nếu module của bước implements `IMachineTrigger`, gọi driver RS485 ngay sau khi tay máy đến vị trí.
5. Sau toàn bộ workflow, quay lại `robot_home`.

`robot_home` không phải file Lua. Lua production nằm trong `workflow/` tại máy Edge và không được commit.

### Đồng bộ Lua

Code provisioning hỗ trợ pull `DeployConfiguration` bằng mTLS, tải ZIP từ object storage, kiểm tra kích thước/SHA-256 và chỉ cài bundle hợp lệ. Tuy nhiên tính năng này hiện được **để lại, chưa đưa vào quy trình cài bắt buộc**, vì BE chưa có file Lua production.

Contract Order theo tên file Lua cũ và contract artifact ID/release manifest mới cũng chưa được nối hoàn chỉnh.

## Máy ngoại vi và plugin driver

Máy ngoại vi giao tiếp trực tiếp với Edge qua RS485. Lua chỉ đưa tay máy tới vị trí; tín hiệu vận hành thiết bị được gửi từ C# sau khi Lua hoàn tất.

Đang có driver tích hợp cho:

- Máy thả cốc.
- Máy kem dùng STM32.

Để thêm hoặc thay máy mà không sửa source IceBot, tạo plugin target `net472` dựa trên `IceBot.Driver.Abstractions`, sau đó cài:

```text
drivers/<driver-name>/
├── driver.json
└── Vendor.Driver.dll
```

Driver phải có public entry type, constructor không tham số và implement `IMachineModule`; thiết bị cần giao tiếp serial implement thêm `IMachineTrigger`. `driver.json` chứa schema, `machineType`, tên DLL, entry type, version và SHA-256.

Plugin trùng `MachineType` sẽ thay driver built-in nhưng giữ ánh xạ COM/DeviceId. Plugin có `MachineType` mới sẽ thêm một máy mới. Khởi động lại IceBot sau khi cài hoặc thay DLL. Xem `driver-sdk/README.md` và template trong `driver-sdk/IceBot.Driver.Template`.

Đăng ký máy với BE tại **InitIceBot → Cấu hình → Đăng ký máy ngoại vi với BE**. BE trả `DeviceId`; Edge lưu ánh xạ đó trong `MACHINE_DEVICE_IDS`. Menu **Danh sách máy ngoại vi** chỉ đọc dữ liệu cục bộ và hiển thị máy nào chưa đăng ký.

## Kiểm thử

```powershell
dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj
```

## Các phần chưa hoàn thành

- Chuyển Order mTLS trong durable inbox thành workflow thực thi robot.
- Kiểm tra chéo Kiosk/Execution Endpoint/release trước khi nhận Order.
- Backpressure đầy đủ cho inbox: dung lượng đĩa, tuổi Order và telemetry.
- Theo dõi tiến độ bền vững theo từng cây kem, pause/resume khi hết nguyên liệu.
- Gửi trạng thái running/completed/failed và sự cố máy ngoại vi về BE.
- Hoàn thiện contract artifact ID giữa deployment Lua mới và execution Order.
- Xác thực provisioning Lua thực tế sau khi BE có Lua production.

Chi tiết đầy đủ và các quyết định thiết kế nằm trong [context/PROJECT_CONTEXT.md](context/PROJECT_CONTEXT.md).
