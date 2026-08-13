# IceBot testing

Thư mục này là điểm tổng hợp kiểm thử của IceBot-IOT.

- Unit test thực thi được đặt trong `harness/IceBot.Harness.Tests/` để giữ nguyên test harness của dự án.
- [UNIT_TEST_SUMMARY.md](UNIT_TEST_SUMMARY.md) liệt kê phạm vi và kết quả unit test.
- [INTEGRATION_TEST_CHECKLIST.md](INTEGRATION_TEST_CHECKLIST.md) liệt kê các phần bắt buộc cần BE, NetBird hoặc phần cứng thật.
- `run-unit-tests.ps1` chạy toàn bộ unit test và xuất file TRX vào `testing/results/`.

Chạy nhanh từ thư mục gốc:

```powershell
dotnet test harness/IceBot.Harness.Tests/IceBot.Harness.Tests.csproj
```

Chạy và lưu kết quả:

```powershell
powershell -ExecutionPolicy Bypass -File testing/run-unit-tests.ps1
```

Không commit nội dung `testing/results/`; đây là kết quả sinh ra trên từng máy/CI.
