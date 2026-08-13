# Tổng hợp unit test IceBot

Ngày chạy gần nhất: **2026-08-13**  
Framework: **xUnit / .NET Framework 4.7.2**  
Kết quả: **68 passed, 0 failed, 0 skipped**

## Ma trận kiểm thử

| Phần đã triển khai | Test file | Số test case | Nội dung chính | Kết quả |
|---|---|---:|---|---|
| Login và validation NetBird | `AuthenticationAndConnectivityTests.cs` | 4 | Không cho login/refresh thiếu dữ liệu; không chạy NetBird khi thiếu setup key | Pass |
| Giữ định danh BE khi cấu hình lại | `ConfigSetupWizardTests.cs` | 2 | Giữ KioskId/KioskCode/DeviceId; không dùng chung mutable dictionary | Pass |
| Cấu hình và ánh xạ thiết bị | `SiteSettingsTests.cs` | 4 | Parse/serialize DeviceId, lookup không phân biệt hoa thường, `IsConfigured` | Pass |
| Chứng chỉ client mTLS | `EdgeClientCertificateProvisionerTests.cs` | 2 | Tạo/tái sử dụng PFX; từ chối certificate không có private key | Pass |
| Kiosk và Execution Endpoint | `ExecutionEndpointRegistrationTests.cs` | 10 | Chuẩn hóa code, tạo endpoint code, parse success/error/status/profile identity | Pass |
| Đăng ký máy ngoại vi | `PeripheralDeviceRegistrationTests.cs` | 3 | Parse DeviceId, lỗi BE, round-trip MachineType–DeviceId | Pass |
| Contract deployment từ BE | `EdgeDeploymentApiTests.cs` | 4 | Parse payload hợp lệ; từ chối sai command type hoặc thiếu dữ liệu | Pass |
| Cài bundle Lua | `FullEdgeConfigurationInstallerTests.cs` | 3 | Cài GUID Lua + manifest; chặn sai SHA-256 và ZIP entry không an toàn | Pass |
| Plugin driver DLL | `MachinePluginLoaderTests.cs` | 11 | Vị trí driver, manifest, path traversal, checksum, DLL thật, package CupDropping, contract module | Pass |
| Local Order contract cũ | `OrderRequestTests.cs` | 5 | Parse, giữ thứ tự step, JSON không hợp lệ/thiếu dữ liệu, case-insensitive | Pass |
| ExecuteOrder mTLS và artifact | `EdgeOrderInboxTests.cs` | 13 | Schema 3/4/5, Kiosk/Endpoint/release/expiry, quantity, deduplicate inbox, thứ tự artifact, checksum Lua | Pass |
| Durable execution queue | `EdgeOrderExecutionQueueTests.cs` | 6 | Giới hạn 10 cây, sai Kiosk, idempotency, unit number, restart recovery và khóa queue an toàn | Pass |
| Production report outbox | `ProductionReportOutboxTests.cs` | 1 | Lưu đủ identity/provenance/status/sequence của report từng cây | Pass |
| **Tổng** | | **68** | | **Pass** |

## Những invariant an toàn đã được unit test

- Một Order không được vượt quá 4 cây.
- Queue không nhận vượt quá 10 cây đang chờ/chạy.
- Một `CommandId` không tạo hai durable job.
- Mỗi cây có `SourceProductionJobId` riêng và số unit liên tục.
- Edge từ chối Order của Kiosk khác.
- Lua chỉ được nhận khi tên artifact dạng GUID tồn tại và checksum khớp.
- Thứ tự chạy là `BindingOrder` rồi `RunOrder`.
- Bundle deployment không kích hoạt Lua nếu checksum sai hoặc ZIP có entry ngoài contract.
- Driver DLL bị sửa nội dung sẽ không được load.
- PFX dùng cho mTLS phải có private key.

## Lệnh đã xác nhận

```powershell
dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj --no-restore
```

Kết quả cảnh báo `NU1900` chỉ do môi trường chạy không truy cập được vulnerability feed của NuGet;
quá trình compile và toàn bộ 68 test vẫn thành công.
