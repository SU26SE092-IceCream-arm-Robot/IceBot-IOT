# Plan sửa lỗi build IceBot-IOT (Cập nhật — Đối chiếu 2 repo)

**Ngày phân tích:** 2026-09-09  
**Branch:** `main` (commit `2ab356a`)  
**Repos đối chiếu:** `IceBot-IOT` ↔ `IceBot-Backend`  
**Tình trạng:** Solution build fail — `dotnet build .\code\IceBot-IOT.sln` exit code 1

---

## Tổng quan

Solution hiện tại **không build được** do 3 lỗi. Trong đó 2 lỗi hiện rõ trong log, 1 lỗi ẩn sẽ xuất hiện sau khi sửa 2 lỗi đầu. Cả 3 đều do **source code trên `main` chưa đầy đủ** hoặc đường dẫn không khớp với thực tế trên git.

| # | Lỗi | Mô tả ngắn | Ai sửa |
|---|---|---|---|
| 1 | Fairino SDK path sai | `.sln` và `.csproj` trỏ folder không tồn tại | Sửa 2 dòng trong repo |
| 2 | `IceBot.Driver.IceCream` thiếu | Toàn bộ project chưa được commit/push | Push source lên git |
| 3 | `IceBot.Workflow.Execution` thiếu | Namespace/folder chưa được commit/push | Push source lên git |

---

## Lỗi 1: Fairino SDK — Đường dẫn sai

### Error log
```
error MSB3202: The project file "...\code\lib\fairino-csharp-sdk-robot3.7.8\src\FRRobot\FRRobot.csproj" was not found.
```

### Nguyên nhân gốc (Git forensic)

Truy vết qua git history cho thấy **chính xác commit nào gây lỗi**:

| Commit | Ngày | Message | Path trong `.sln` |
|---|---|---|---|
| `cc4a35e` | 2026-06-22 | `feat: add IceBot C# console app and Fairino SDK` | ✅ `lib\fairino-csharp-sdk\src\FRRobot\FRRobot.csproj` |
| `65f5073` → `04ae89d` | — | (nhiều commit) | ✅ Không thay đổi, vẫn `fairino-csharp-sdk` |
| **`162d642`** | **2026-08-29** | **`docs: sync README with project context`** | ❌ **Đổi thành `fairino-csharp-sdk-robot3.7.8`** |

**Commit `162d642` (anh Kiệt — KietCT-0863)** đổi path từ `fairino-csharp-sdk` → `fairino-csharp-sdk-robot3.7.8` trong `.sln` và thêm `IceBot.csproj` với path mới. Nhưng **folder thực tế trên git vẫn là `fairino-csharp-sdk`** — không hề có commit nào rename folder.

**Giải thích:** Trên máy local của anh Kiệt, folder có thể đã được rename thành `fairino-csharp-sdk-robot3.7.8` (theo SDK tag `v1.1.0_robot3.7.8`). Nhưng vì `code/lib/fairino-csharp-sdk/` đã tồn tại trong git history từ commit đầu tiên `cc4a35e`, nên khi commit lại chỉ path reference bị thay đổi, còn folder trên git thì giữ nguyên tên cũ.

### Đối chiếu với Backend

Backend không tham chiếu trực tiếp đến Fairino SDK folder (Backend không build FRRobot.dll). Backend chỉ sử dụng **string constants** `FAIRINO_LUA_V1` và `FR5` làm runtime target/model codes — đây là contract codes, không phải SDK path.

```
code/lib/
  └── fairino-csharp-sdk/              ← folder THỰC TẾ trên git (từ commit cc4a35e)
        └── src/FRRobot/FRRobot.csproj   ✅

  └── fairino-csharp-sdk-robot3.7.8/   ← path ĐƯỢC THAM CHIẾU (từ commit 162d642)
                                          ❌ KHÔNG tồn tại, chưa bao giờ rename trên git
```

### Cách sửa

**File 1:** `code/IceBot-IOT.sln` — **dòng 8**

```diff
- Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FRRobot", "lib\fairino-csharp-sdk-robot3.7.8\src\FRRobot\FRRobot.csproj", "{A50C0309-4334-4BED-9372-BD8B7D4D4654}"
+ Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FRRobot", "lib\fairino-csharp-sdk\src\FRRobot\FRRobot.csproj", "{A50C0309-4334-4BED-9372-BD8B7D4D4654}"
```

**File 2:** `code/src/IceBot/IceBot.csproj` — **dòng 16**

```diff
-     <ProjectReference Include="..\..\lib\fairino-csharp-sdk-robot3.7.8\src\FRRobot\FRRobot.csproj" />
+     <ProjectReference Include="..\..\lib\fairino-csharp-sdk\src\FRRobot\FRRobot.csproj" />
```

> **Lưu ý:** Script `code/scripts/restore-fairino-sdk-dependencies.ps1` đã dùng đúng path `fairino-csharp-sdk` → không cần sửa script.

---

## Lỗi 2: IceBot.Driver.IceCream — Toàn bộ project thiếu

### Error log
```
error MSB3202: The project file "...\driver-sdk\IceBot.Driver.IceCream\IceBot.Driver.IceCream.csproj" was not found.
```

### Nguyên nhân

Folder `driver-sdk/IceBot.Driver.IceCream/` **không tồn tại** trên git và chưa bao giờ được commit. Git history cho `driver-sdk/` chỉ có:

```
1c1550e  feat: load peripheral drivers as DLL plugins     → Template
04ae89d  refactor: move cup dropper to dll driver          → CupDropping
```

Không có commit nào cho `IceBot.Driver.IceCream`. Project được thêm vào `.sln` tại commit `162d642` nhưng source code không đi kèm.

```
driver-sdk/
  ├── IceBot.Driver.CupDropping/    ✅ có (commit 04ae89d)
  ├── IceBot.Driver.Template/       ✅ có (commit 1c1550e)
  └── IceBot.Driver.IceCream/       ❌ THIẾU — chưa bao giờ commit
```

### Đối chiếu với Backend

Backend **sử dụng rộng rãi `ICE_CREAM` capability** — đây là contract code quan trọng trong hệ thống:

| Nơi sử dụng trong Backend | Mục đích |
|---|---|
| `EDGE_SYNC_TELEMETRY_CONTRACT.md` | Readiness capability: `"capabilityCode": "ICE_CREAM"` |
| `EdgeOperationalIntegrationTestBase.cs` | Test Edge capability: `CapabilityCode = "ICE_CREAM"` |
| `EdgeTelemetryIntegrationTests.cs` | Test readiness: `new ExecutionCapabilityInput("ICE_CREAM", ...)` |
| `RobotArtifactDeploymentAndExecutionIntegrationTests.cs` | Required capabilities: `'["CUP_DISPENSER","ICE_CREAM"]'` |
| `ProductionPackage*` tests | Product definition: `"ICE_CREAM"` |
| `ConfigurationReleaseRouteAuthoringTests.cs` | Route capability: `"ICE_CREAM"` |
| `Product.cs`, `CreateProductRequest.cs` | ProductType default: `"IceCream"` |

**Kết luận:** Backend đã hoàn chỉnh ICE_CREAM contract — deployment, execution, readiness, capability reporting đều expect ICE_CREAM. Nhưng IOT side chưa push source code của driver xử lý phần cứng ice cream machine. Test harness đã viết xong (`IceCreamDriverTests.cs` dùng `SerialFrameCodec`, `IceCreamDriver`, v.v.) nhưng source thiếu.

### Nơi tham chiếu đến project này (IOT)

| File | Dòng | Nội dung |
|---|---|---|
| `code/IceBot-IOT.sln` | 24 | `Project(...) = "IceBot.Driver.IceCream", "..\driver-sdk\IceBot.Driver.IceCream\IceBot.Driver.IceCream.csproj"` |
| `harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj` | 20 | `<ProjectReference Include="..\..\driver-sdk\IceBot.Driver.IceCream\IceBot.Driver.IceCream.csproj" />` |
| `harness/IceBot.Harness.Tests/IceCreamDriverTests.cs` | 3 | `using IceBot.Driver.IceCream;` |

### Test code đã sử dụng các class sau (từ `IceCreamDriverTests.cs`)

- `SerialFrameCodec` (static methods: `Build`, `TryValidate`)
- `IceCreamDriver`
- `IceCreamMachineStatus`
- `IceCreamMachineState` (enum, có giá trị `Standby`, `Fault`)
- `IIceCreamMachineClient` (interface)

### Cách sửa

**Commit và push toàn bộ folder `driver-sdk/IceBot.Driver.IceCream/`** bao gồm:
- `IceBot.Driver.IceCream.csproj`
- Tất cả file `.cs` chứa các class/interface/enum liệt kê ở trên
- Có thể tham khảo cấu trúc tương tự `IceBot.Driver.CupDropping/` (cùng target `net472`, reference đến `IceBot.Driver.Abstractions`)

---

## Lỗi 3 (ẩn): IceBot.Workflow.Execution — Namespace thiếu source

### Tại sao gọi là "lỗi ẩn"?

Lỗi này **chưa hiện trong error log** vì build fail ở bước restore (lỗi 1 và 2) trước khi compiler đến bước compile C#. Nhưng sau khi sửa lỗi 1 và 2, build **sẽ tiếp tục fail** vì thiếu namespace này.

### Nguyên nhân

4 file `.cs` import `using IceBot.Workflow.Execution;` nhưng **không tồn tại** file nào khai báo `namespace IceBot.Workflow.Execution` trên git.

| File sử dụng `using IceBot.Workflow.Execution;` | Dòng |
|---|---|
| `code/src/IceBot/Workflow/WorkflowRunner.cs` | 7 |
| `code/src/IceBot/Workflow/Orders/OrderExecutionPreflight.cs` | 8 |
| `code/src/IceBot/Robot/RobotWorkflowExecutors.cs` | 5 |
| `code/src/IceBot/Robot/FairinoLuaExecutor.cs` | 3 |

```
Workflow/
  ├── Orders/           ✅ có
  ├── Provisioning/     ✅ có
  ├── WorkflowRunner.cs ✅ có
  └── Execution/        ❌ THIẾU (hoặc file khác chứa namespace này)
```

### Đối chiếu với Backend

Backend có context `Domain.ProductionExecution` với `OrderExecutionRecord` và `ProductionExecutionRecord`. Edge command contract yêu cầu Edge report execution status (`Accepted`, `Running`, `Completed`, `Failed`). IOT cần có `Workflow.Execution` namespace chứa execution plan/model để:
- Parse ordered artifact list từ Backend command
- Tạo execution plan cho robot workflow
- Track execution state per production unit

Điều này khớp với `WorkflowRunner.cs` (line 7) đã import namespace này, và `FairinoLuaExecutor.cs` cần nó để biết plan chạy artifact nào.

### Cách sửa

**Commit và push source code chứa `namespace IceBot.Workflow.Execution`** — khả năng cao là folder `code/src/IceBot/Workflow/Execution/` chứa các class liên quan (như `WorkflowExecutionPlan`, etc.).

---

## Tóm tắt nguyên nhân đối chiếu 2 repo

```
Backend (hoàn chỉnh)                  IOT (thiếu source)
━━━━━━━━━━━━━━━━━━━                  ━━━━━━━━━━━━━━━━━━━
FAIRINO_LUA_V1 / FR5 constants  ←→   FRRobot SDK (có nhưng path sai)
ICE_CREAM capability contract   ←→   IceBot.Driver.IceCream (THIẾU hoàn toàn)
ProductionExecution domain      ←→   Workflow.Execution namespace (THIẾU hoàn toàn)
ExecuteOrder command delivery   ←→   Order receiver/inbox/queue (có đủ)
Deployment command delivery     ←→   Deployment install/report (có đủ)
Readiness/Heartbeat contract    ←→   EdgeMtlsProbe (có đủ)
```

**Kết luận:** Backend đã hoàn chỉnh toàn bộ contract (IoT endpoints, capability codes, command/report schema). IOT có phần lớn implementation nhưng **3 phần bị thiếu trên git** — tất cả đều tồn tại trên máy local dev gốc (vì tests đã viết xong, references đã khai báo, commit `162d642` đã thêm entries vào `.sln`).

---

## Checklist sửa lỗi

```
[ ] 1. Sửa path Fairino SDK trong code/IceBot-IOT.sln (dòng 8)
         Đổi fairino-csharp-sdk-robot3.7.8 → fairino-csharp-sdk
[ ] 2. Sửa path Fairino SDK trong code/src/IceBot/IceBot.csproj (dòng 16)
         Đổi fairino-csharp-sdk-robot3.7.8 → fairino-csharp-sdk
[ ] 3. Commit & push folder driver-sdk/IceBot.Driver.IceCream/ (toàn bộ source)
[ ] 4. Commit & push source namespace IceBot.Workflow.Execution (toàn bộ source)
[ ] 5. Verify build thành công (xem bên dưới)
```

---

## Verify sau khi sửa

Chạy 3 lệnh theo thứ tự:

```powershell
.\code\scripts\restore-fairino-sdk-dependencies.ps1
dotnet build .\code\IceBot-IOT.sln -c Release --no-restore
dotnet test .\harness\IceBot.Harness.Tests\IceBot.Harness.Tests.csproj -c Release --no-restore
```

**Kết quả mong đợi:**
- Build: 0 errors
- Test: 131 passed, 0 failed, 0 skipped
- Output: `IceBot.exe` và `InitIceBot.exe` xuất hiện trong `code/src/IceBot/bin/Release/net472/`
