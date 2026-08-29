# System Architecture Notes

## Edge & Physical Hardware cần sửa

- Đổi `.NET + Lua Edge Control Runtime` thành `IceBot Edge Runtime (.NET)`.
- Không thể hiện BLL, workflow, device driver hoặc STM32F103 trong system architecture vì đây là chi tiết triển khai nội bộ.
- Giữ Flutter Kiosk App và IceBot Edge Runtime trong cùng khối `On-site Kiosk PC / Windows`.
- Fairino FR5 kết nối với Edge qua `Ethernet / Fairino SDK-RPC`.
- Các máy ngoại vi được Edge điều khiển trực tiếp kết nối qua `RS485`. Máy mới muốn tích hợp phải có giao diện RS485 tương thích, tài liệu protocol điều khiển và `Edge plugin driver` tương ứng trong Edge. RS485 chỉ là lớp vật lý; protocol lệnh có thể khác nhau theo từng thiết bị.

### Plain text

```text
┌──────────────── On-site Kiosk PC / Windows ────────────────┐
│                                                            │
│  ┌──────────────────┐       ┌───────────────────────────┐  │
│  │ Flutter Kiosk App│       │ IceBot Edge Runtime       │  │
│  │                  │       │ (.NET)                    │  │
│  └────────┬─────────┘       └───────┬───────────┬───────┘  │
└───────────┼─────────────────────────┼───────────┼──────────┘
            │                         │           │
       HTTPS/REST               Ethernet/RPC    RS485
            │                         │           │
            ▼                         ▼           ▼
     Cloud Backend             Fairino FR5    RS485 Peripheral Machines
                                              ├─ Ice Cream Machine
                                              ├─ Coffee Machine
                                              └─ Cup Dispenser
```

## Simulation Tools cần sửa

- Tách Fairino Studio khỏi khối `Applications`; đặt trong khối riêng tên `Simulation Tools`.
- Fairino Studio dùng để tạo và xuất file `.lua`, không phải thành phần runtime của hệ thống production.
- Thể hiện luồng thủ công: `Fairino Studio -> Export .lua file -> Admin Web`.
- Dùng đường nét đứt cho luồng trên vì người dùng tự xuất và upload file, không có kết nối API trực tiếp giữa Fairino Studio và Admin Web.
- Không cần thêm mô tả chi tiết bên trong hộp Fairino Studio; tên đường truyền đã thể hiện mục đích của file Lua.

### Quan trọng
- không bao Simulation Tools và Admin Web trong cùng một hình chữ nhật

### Plain text

```text
┌────────── Simulation Tools ──────────┐
│                                      │
│        ┌──────────────────┐          │
│        │ Fairino Studio   │          │
│        └────────┬─────────┘          │
└─────────────────┼────────────────────┘
                  ┊
                  ┊ Export .lua file
                  ▼
              Admin Web
```

## Admin Web App cần sửa

- Admin Web được deploy trên `Vercel`; dùng khối `Vercel` làm deployment boundary và đặt `Admin Web (Next.js)` bên trong.
- Admin Web giao tiếp với Cloud Backend qua `HTTPS/REST` trên Internet.
- Không cần khối ngoài `Applications` nếu khối đó chỉ chứa Admin Web; deployment boundary `Vercel` đã đủ rõ.

### Plain text

```text

┌──────────────── Vercel ──────────────┐
│ Admin Web                            │
│ Next.js                              │
└────────────────┬─────────────────────┘
                 │
                 │ HTTPS/REST
                 ▼
            Cloud Backend
```

## BE cần sửa

- Giữ khối ngoài `VPS / Production Server` vì đây là máy chủ production được thuê để triển khai hệ thống.
- Giữ `K3s Cluster` bên trong VPS; đây là môi trường thực tế của production.
- Đặt cả ba thành phần `IceBot Backend`, `PostgreSQL` và `MinIO` bên trong `K3s Cluster`.
- Đổi tên backend thành `IceBot Backend (ASP.NET Core)`
- Ghi `PostgreSQL — Database` và `MinIO — Object Storage` để thể hiện đúng vai trò ở mức high-level.
- Edge giao tiếp với Cloud Backend API qua `HTTPS/mTLS`; khi nhận presigned URL, Edge tải workflow artifact trực tiếp từ MinIO qua `HTTPS GET`.

### Plain text

```text
┌──────────────────────┐                 ┌────────── VPS / Production Server ──────────┐
│ IceBot Edge Runtime  │                 │                                             │
│ .NET                 │                 │  ┌────────────── K3s Cluster ─────────────┐ │
│                      │ HTTPS/mTLS      │  │ ┌────────────────┐  ┌────────────────┐ │ │
│                      ├─────────────────────►│ IceBot Backend │─►│ PostgreSQL     │ │ │
│                      │                 │  │ │ ASP.NET Core   │  │ Database       │ │ │
│                      │                 │  │ └───────┬────────┘  └────────────────┘ │ │
│                      │                 │  │         │                              │ │
│                      │                 │  │         ▼                              │ │
│                      │ HTTPS GET       │  │ ┌────────────────┐                     │ │
│                      ├─────────────────────►│ MinIO          │                     │ │
└──────────────────────┘                 │  │ │ Object Storage │                     │ │
                                         │  │ └────────────────┘                     │ │
                                         │  └────────────────────────────────────────┘ │
                                         └─────────────────────────────────────────────┘
```

## External Services cần sửa

- Đổi nhãn Firebase từ `Auth / Validation / Push` thành `Authentication & FCM`.
- Thể hiện kết nối `IceBot Backend -> Firebase` qua `HTTPS`; không dùng từ `Validation` vì quá chung chung.
- Đổi hộp Firebase thành `Firebase — Authentication & FCM`; đường kết nối từ IceBot Backend chỉ ghi `HTTPS`.
end` cho `Webhook / HTTPS`.

### Plain text

```text
┌──────────────────────┐                                             ┌──────── External Services ───────┐
│ IceBot Backend       │                                             │                                  │
│ ASP.NET Core         │                                             │  ┌───────────────────────┐       │
│                      │  HTTPS                                      │  │ Firebase              │       │
│                      ├─────────────────────────────────────────────┼─►│ Authentication & FCM  │       │
│                      │                                             │  └───────────────────────┘       │
│                      │                                             │                                  │
│                      │  Payment API / HTTPS                        │  ┌─────────────────┐             │
│                      ├─────────────────────────────────────────────┼─►│ PayOS           │             │
│                      │  Webhook / HTTPS                            │  │ Payment Gateway │             │
│                      │◄────────────────────────────────────────────┼──┤                 │             │
└──────────────────────┘                                             │  └─────────────────┘             │
                                                                     └──────────────────────────────────┘
```

## DevOps Pipeline cần sửa
- bỏ phần DevOps Pipeline

## Quan trọng
- dữ liệu đi lên thì có đi về không? nếu có thì phải thêm 1 đường thẳng quay về nữa