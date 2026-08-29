## 1.2 Hardware Architecture

Phần này trình bày kiến trúc phần cứng của module tự động nhả kem và cách module kết nối với Edge Computer trong hệ thống kiosk. Kiến trúc phần cứng được chia thành bốn nhóm chính: **Control Unit**, **Communication Unit**, **Power Supply Unit** và **Actuator Unit**.

### Hardware Interconnection

**[Chèn Hình 1.2.1 tại đây]**

**Figure 1.2.1 – Hardware Interconnection Diagram of the Automatic Ice Cream Dispensing Module**

**Hình 1.2.1** minh họa sơ đồ kết nối tổng thể giữa các thành phần phần cứng. Edge Computer giao tiếp với hệ thống thông qua **USB-to-RS485 Converter**. Bộ chuyển đổi này chuyển dữ liệu từ giao diện USB của Edge Computer thành tín hiệu RS485.

Tín hiệu RS485 được truyền qua đường truyền vi sai A/B đến **TTL-to-RS485 Converter Module (MAX485)**. Module MAX485 chuyển đổi tín hiệu RS485 thành tín hiệu UART để STM32 giao tiếp thông qua USART1. Ở phía phần mềm, Edge Computer mở giao tiếp dưới dạng cổng COM bằng `SerialPort`, trong khi firmware của STM32 xử lý việc truyền và nhận dữ liệu UART thông qua giao tiếp RS485.

Sau khi nhận được lệnh điều khiển, STM32 xử lý dữ liệu và phát tín hiệu PWM thông qua các chân PA8 và PB1. Các tín hiệu PWM được truyền qua hai bộ **PC817 Optocoupler** trước khi đến **BTS7960 Motor Driver Module**. BTS7960 sử dụng các tín hiệu này để điều khiển hoạt động của động cơ **JGB37-520 Geared DC Motor**.

Động cơ JGB37-520 được kết nối với **Lead Screw Mechanism**. Cơ cấu vít me chuyển đổi chuyển động quay của động cơ thành chuyển động tịnh tiến, qua đó thực hiện thao tác đóng và mở cơ cấu nhả kem.

Về cấp nguồn, **24V Switching Power Supply** chuyển đổi điện áp 220V AC thành 24V DC để cấp cho ngõ nguồn động cơ của BTS7960 và động cơ. **LM2596 Step-Down Voltage Regulator** chuyển đổi nguồn 24V thành một đường 5V riêng để cấp cho phần logic của BTS7960 và phía ngõ ra của các bộ PC817. **5V Power Adapter** cấp nguồn cho cả STM32 và **TTL-to-RS485 Converter Module (MAX485)**. Mặc dù STM32 và MAX485 đều được cấp nguồn 5V, các tín hiệu logic UART giữa hai mạch sử dụng mức logic 3.3V. Các bộ PC817 tạo cách ly điện giữa mass phía STM32/mạch điều khiển và các tín hiệu điều khiển phía mạch công suất.

### Detailed Electrical Schematic

**[Chèn Hình 1.2.2 tại đây]**

**Figure 1.2.2 – Detailed Electrical Schematic of the Automatic Ice Cream Dispensing Module**

**Hình 1.2.2** trình bày sơ đồ điện chi tiết của module tự động nhả kem. Sơ đồ thể hiện các kết nối giữa STM32, MAX485, hai PC817 Optocoupler, các điện trở 330 Ω và 4.7 kΩ, BTS7960 Motor Driver Module, LM2596 Step-Down Voltage Regulator, JGB37-520 Geared DC Motor và các nguồn điện 5V/24V.

### 1.2.1 Control Unit

**[Chèn bảng Control Unit tại đây]**

### 1.2.2 Communication Unit

**[Chèn bảng Communication Unit tại đây]**

### 1.2.3 Power Supply Unit

**[Chèn bảng Power Supply Unit tại đây]**

### 1.2.4 Actuator Unit

**[Chèn bảng Actuator Unit tại đây]**

### 1.2.5 Communication Standardization and Extensibility

Mạch điều khiển được trình bày trong tài liệu là một giải pháp custom được phát triển để chuyển đổi máy kem từ cơ chế vận hành thủ công sang cơ chế tự động. Thiết kế này là một phiên bản triển khai tham khảo cho máy kem cụ thể và không phải là cấu hình phần cứng bắt buộc đối với tất cả các thiết bị được tích hợp vào hệ thống kiosk.

Hệ thống được thiết kế theo hướng chuẩn hóa, trong đó các máy ngoại vi được Edge điều khiển trực tiếp sẽ được tích hợp thông qua giao diện RS485. Mỗi thiết bị loại này phải cung cấp giao diện RS485 tương thích, tài liệu protocol điều khiển và một **Edge plugin driver** tương ứng. Thiết bị có thể sử dụng mạch điều khiển và cơ cấu chấp hành riêng. RS485 chỉ định nghĩa lớp giao tiếp vật lý; bản thân RS485 không định nghĩa lệnh thiết bị, cấu trúc frame, mã trạng thái, quy tắc checksum hoặc hành vi riêng của từng máy.

Việc sử dụng RS485 làm giao diện giao tiếp tiêu chuẩn giúp thống nhất phương thức kết nối giữa Edge Computer và các thiết bị ngoại vi. Edge Computer giao tiếp với bus RS485 thông qua USB-to-RS485 Converter, trong khi các bộ điều khiển như STM32 sử dụng UART kết hợp với TTL-to-RS485 Converter Module (MAX485). Thiết kế này giúp giảm sự khác biệt giữa các loại máy, đơn giản hóa quá trình tích hợp và hỗ trợ khả năng mở rộng của hệ thống.

Các thiết bị ngoại vi có thể có thiết kế mạch điện, bộ điều khiển và cơ cấu chấp hành khác nhau. Tuy nhiên, mỗi thiết bị được Edge Computer điều khiển trực tiếp phải có giao diện RS485 tương thích, protocol điều khiển được mô tả và **Edge plugin driver** tương ứng. Edge plugin driver chịu trách nhiệm mở kết nối serial, chuyển đổi thao tác của Edge thành lệnh riêng của thiết bị, kiểm tra phản hồi và xử lý lỗi giao tiếp. Hệ thống không mặc định có một application protocol chung, trừ khi protocol đó được quy định rõ trong một đặc tả riêng.

Do đó, các sơ đồ trong phần này mô tả một trường hợp triển khai cụ thể cho máy kem, đồng thời cung cấp cơ sở tham khảo cho quá trình tích hợp các thiết bị ngoại vi khác vào hệ thống kiosk.