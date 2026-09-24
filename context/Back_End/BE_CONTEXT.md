# IceBot Backend Project Context

Last reviewed: 2026-09-14

## Vai trò

IceBot Backend là backend ASP.NET Core cho hệ thống kiosk bán kem tự động nhiều cửa hàng. Backend là một modular monolith, chịu trách nhiệm quản lý tổ chức, cửa hàng, kiosk, tài khoản và phân quyền; danh mục bán hàng; đơn hàng và trạng thái fulfillment; thanh toán; tồn kho; cấu hình chương trình robot; đồng bộ với Edge; giám sát và thông báo vận hành.

## Kiến trúc triển khai

Mã nguồn nằm trong `src/WebAPI`, `src/Application`, `src/Domain` và `src/Infrastructure`. Chuỗi phụ thuộc chính là `WebAPI -> Infrastructure -> Application -> Domain`; WebAPI sở hữu HTTP/auth, Application điều phối use case và contract, Domain sở hữu quy tắc nghiệp vụ, Infrastructure sở hữu EF Core, adapter ngoài và background workers.

Backend được thiết kế để chạy như một deployable duy nhất. PostgreSQL là cơ sở dữ liệu nghiệp vụ thông qua EF Core. MinIO là object storage tương thích S3 cho artifact Lua và bundle triển khai. Các vị trí chạy cụ thể phụ thuộc cấu hình triển khai; sơ đồ hệ thống hiện mô tả PostgreSQL, MinIO và Backend trong K3s Cluster trên máy chủ production.

## Các boundary chính

- **Identity và tenancy:** quản lý organization, store, kiosk, nhân sự, vai trò và phạm vi truy cập.
- **Sales Catalog:** sản phẩm, biến thể, menu, option, giá và runtime menu snapshot.
- **Orders và fulfillment:** nhận đơn từ Kiosk App, kiểm tra khả dụng, lưu snapshot lựa chọn, theo dõi thanh toán và fulfillment theo từng order item/unit.
- **Payments:** tạo payment session, tiếp nhận webhook PayOS, xác thực và khử trùng lặp provider event, sau đó chuyển đơn sang `ReadyForFulfillment` khi thanh toán thành công.
- **Robot Configuration:** author/import artifact Lua, manifest, runtime/model/capability contract và release/bundle triển khai.
- **Inventory:** trạng thái dispenser/ingredient, quan sát sensor tùy chọn và các quy tắc sellability/readiness.
- **IoT/Edge Integration:** execution endpoint, mTLS identity, deployment sync, command pull/ack, execution reports, heartbeat, readiness, device snapshot, telemetry và MQTT wake-up.
- **Operations:** alert, maintenance, notification, SignalR projection và Firebase FCM cho thông báo phù hợp.

## Luồng checkout và sản xuất

```text
Kiosk App lấy runtime menu
  -> tạo order với Idempotency-Key
  -> tạo payment session PayOS
  -> PayOS gọi webhook HTTPS
  -> Backend xác thực callback và commit payment/order
  -> Backend tạo ExecuteOrder cho execution endpoint phù hợp
  -> Edge chủ động pull command qua HTTPS/mTLS
  -> Edge thực thi và gửi Accepted/Running/Completed/Failed evidence
  -> Backend cập nhật fulfillment và customer status
  -> Kiosk App polling hoặc nhận projection realtime
```

Kiosk App không điều khiển robot trực tiếp. Backend chọn đúng một execution endpoint có release/capability bao phủ các line sản xuất; nếu không có endpoint thì trì hoãn, nếu có nhiều endpoint phù hợp thì không tự chọn. Thanh toán được commit độc lập với việc Edge nhận lệnh.

## Giao tiếp frontend

Kiosk App dùng các runtime endpoint HTTPS để lấy menu, tạo order, tạo payment session và đọc trạng thái customer. Client device được xác thực bằng device JWT; order-specific calls dùng thêm order access token. Order creation luôn kiểm tra lại menu, giá, option, currency và khả dụng ở Cloud; tổng tiền do client gửi chỉ dùng để phát hiện chênh lệch.

Admin Web dùng HTTPS và các management API để quản lý organization/store/kiosk, catalog, inventory, robot artifact/release, vận hành và người dùng theo RBAC. Admin Web và Kiosk App không được xem là thành phần điều khiển phần cứng.

## Giao tiếp Edge

Edge dùng execution endpoint identity và HTTPS/mTLS để pull `DeployConfiguration`/`ExecuteOrder`, gửi ACK và execution reports. MQTT có thể gửi thông báo command-available hoặc telemetry theo contract; MQTT chỉ là wake-up/realtime transport, còn periodic authenticated pull và các endpoint HTTPS là cơ chế khôi phục khi broker hoặc kết nối bị gián đoạn. SignalR phục vụ projection tới giao diện người dùng, không điều khiển robot.

Backend kiểm tra endpoint identity, release/artifact compatibility, capability/readiness và các trạng thái admission trước khi dispatch. Execution reports được áp dụng theo production unit/order item, giữ bằng chứng immutable và hỗ trợ replay/retry theo contract. Backend không tự động replay accepted command hay tự động khởi động lại Lua sau restart; các trường hợp có khả năng output vật lý cần luồng incident/support.

## Lua và lưu trữ artifact

Fairino Studio hoặc công cụ authoring tạo chương trình Lua; Admin Web upload/import artifact. Backend lưu artifact/bundle trong MinIO và metadata, manifest, checksum, runtime target, model và capability contract trong PostgreSQL. Backend kiểm tra byte integrity và declared compatibility để định tuyến/deploy; các kiểm tra này không chứng minh nội dung Lua an toàn hay đúng recipe. Edge tải artifact khi đồng bộ deployment, lưu cục bộ và tạo execution plan khi chạy.

## Thanh toán và dịch vụ ngoài

PayOS được tích hợp qua HTTPS để tạo payment session và webhook kết quả. Backend xác minh tính xác thực callback, khử trùng lặp provider event và commit trạng thái trước khi gửi thông báo hoặc dispatch.

Firebase hỗ trợ xác thực và FCM theo các adapter hiện có. FCM là kênh thông báo, không phải nguồn sự thật cho session hay trạng thái nghiệp vụ. Sơ đồ còn thể hiện AI Service; theo kiến trúc đã thống nhất, Frontend gọi API AI bên ngoài để cung cấp chức năng chatbot. Không có bằng chứng Backend làm proxy hoặc tích hợp trực tiếp với API này trong mã nguồn đã khảo sát.

## Trạng thái Edge và inventory

Heartbeat phản ánh connectivity; lifecycle kiosk và connectivity là hai projection độc lập. Readiness là bằng chứng vận hành có thời hạn, gồm local persistence, storage/backlog, robot safety/activity và capability. Hardware snapshot là inventory thiết bị được Edge quan sát và không đồng nghĩa với chứng nhận hành vi Lua. Sensor inventory là tùy chọn; thiếu sensor không mặc nhiên có nghĩa `OutOfStock`.

## Phục hồi và quan sát

Backend dùng các event/report projection, idempotency và background reconciliation để xử lý retry, mất kết nối, webhook lặp và command/report đến trễ. Khi Edge hoặc robot restart trong lúc sản xuất, Cloud giữ bằng chứng và chuyển sự việc sang luồng incident/manual intervention theo physical-output evidence; Cloud không tự ý replay accepted command. SignalR phát các thay đổi đã commit đến các nhóm UI; alert nghiêm trọng có thể tạo thông báo Firebase.

## Nguồn sự thật và giới hạn

Context này được tổng hợp từ `ARCHITECTURE.md`, `docs/architecture`, `docs/flows`, `docs/iot`, `docs/api`, cấu hình WebAPI và mã nguồn đại diện trong Backend tại thời điểm review. Mã nguồn đã đối chiếu gồm `PlaceOrderCommandHandler`, `OrderExecutionDispatchPlanner`, `PayOsPaymentGateway`, `PayOsWebhookController`, `MinioArtifactObjectStorage`, các handler command pull/report và `AuthenticationExtensions`. Các chi tiết giao thức, field và trạng thái phải đối chiếu các contract sở hữu trong Backend khi cần triển khai. Không suy ra từ context này rằng AI Service, K3s topology ngoài sơ đồ, hoặc mọi chức năng frontend đều đã được xác minh bằng runtime production; riêng việc Frontend gọi API AI bên ngoài cho chatbot là thông tin kiến trúc do người dùng xác nhận.
