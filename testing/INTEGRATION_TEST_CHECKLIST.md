# Checklist integration và kiểm thử phần cứng

Các mục dưới đây không được gọi là unit test vì cần tiến trình bên ngoài, mạng, BE hoặc thiết bị thật.
Chúng cần được chạy khi môi trường staging/production đã có Lua và phần cứng.

## Setup.exe

- [ ] Cài mới trên Windows sạch và cho phép chọn thư mục cài.
- [ ] Cài/tìm đúng .NET Framework và NetBird.
- [ ] Tạo `IceBot.exe`, `InitIceBot.exe`, config/data/workflow và `C:\ProgramData\IceBot\drivers`.
- [ ] Chạy lại Setup để xác nhận hành vi upgrade/idempotent.

## InitIceBot.exe và BE management

- [ ] Login bằng tài khoản cửa hàng thật; kiểm tra refresh token sau HTTP 401.
- [ ] Nhập Kiosk Code in trên vỏ và tìm/tạo đúng KioskId.
- [ ] Tìm/tạo Full Edge Execution Endpoint và tái sử dụng ID sau khi chạy lại.
- [ ] Provision certificate mTLS, activate kiosk và gửi heartbeat thật.
- [ ] Đăng ký máy ngoại vi và xác nhận DeviceId hiển thị lại sau restart.

## NetBird và mTLS

- [ ] `netbird up` kết nối thành công bằng setup key thật.
- [ ] Edge gọi được private HTTPS URL của BE và certificate được BE chấp nhận.
- [ ] Pull `DeployConfiguration` và `ExecuteOrder` thật.
- [ ] ACK `Accepted`, `ExecutorBusy`, `Rejected` xuất hiện đúng trên BE.
- [ ] Report `Accepted/Running/Completed/Failed/RequiresManualIntervention` đến BE đúng thứ tự.
- [ ] Ngắt mạng tạm thời rồi kết nối lại; report trong outbox phải được gửi bù.

## Lua, robot và máy ngoại vi

- [ ] Tải bundle Lua production thật và đối chiếu release/checksum.
- [ ] Fairino kết nối, đi `robot_home`, upload/load/run Lua và trở lại home.
- [ ] Nhiều artifact chạy đúng `BindingOrder`/`RunOrder`.
- [ ] Plugin CupDropping giao tiếp đúng cổng COM/RS485 trên máy thật.
- [ ] Mỗi lần chỉ làm một cây; Order nhiều cây chạy nguyên workflow từ đầu cho từng cây.
- [ ] Cố ý gây lỗi robot/máy giữa một cây và xác nhận queue dừng an toàn.
- [ ] Tắt nguồn Edge khi unit đang `Running`; sau restart phải yêu cầu can thiệp thủ công, không tự làm lại.

## Phần chưa triển khai nên chưa thể kiểm thử hoàn chỉnh

- Kiểm tra tồn kho/nguyên liệu trước từng cây.
- Lệnh resume/reconcile sau khi kỹ thuật viên xử lý sự cố.
- Telemetry riêng của từng máy ngoại vi gửi về BE.
- Lua production thật trên BE tại thời điểm lập tài liệu này.
