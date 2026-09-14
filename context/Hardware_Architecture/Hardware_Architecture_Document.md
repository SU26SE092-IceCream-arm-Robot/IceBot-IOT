## 1.2 Hardware Architecture

Phần này trình bày kiến trúc phần cứng của module tự động nhả kem và cách module kết nối với Edge Computer trong hệ thống kiosk. Kiến trúc phần cứng được chia thành năm nhóm chính: **Control Unit**, **Communication Unit**, **Power Supply Unit**, **Actuator Unit** và **Limit Sensor Unit**.

### Hardware Interconnection

**[Chèn Hình 1.2.1 tại đây]**

**Figure 1.2.1 – Hardware Interconnection Diagram of the Automatic Ice Cream Dispensing Module**

**Hình 1.2.1** minh họa sơ đồ kết nối tổng thể giữa các thành phần phần cứng. Edge Computer giao tiếp với hệ thống thông qua **USB-to-RS485 Converter**. Bộ chuyển đổi này chuyển dữ liệu từ giao diện USB của Edge Computer thành tín hiệu RS485.

Tín hiệu RS485 được truyền qua đường truyền vi sai A/B đến **TTL-to-RS485 Converter Module (MAX485)**. Module thực hiện chuyển đổi hai chiều giữa tín hiệu vi sai RS485 và tín hiệu nối tiếp mức logic phía TTL, kết nối với STM32 qua USART1 ở chế độ UART.

Chân **PA4/A4** của STM32 nối chung đến **DE/RE** (cho phép nhận ở mức LOW) của MAX485 để điều khiển hướng giao tiếp RS485 half-duplex. Khi PA4 ở mức HIGH, module chuyển sang chế độ truyền; khi PA4 ở mức LOW, module chuyển sang chế độ nhận.

STM32 cung cấp tín hiệu PWM thông qua các chân PA8 và PB1. Các tín hiệu PWM được truyền qua hai bộ **PC817 Optocoupler** trước khi đến **BTS7960 Motor Driver Module**. BTS7960 sử dụng các tín hiệu này để điều khiển hoạt động của động cơ **JGB37-520 Geared DC Motor**.

Động cơ JGB37-520 được kết nối với **Lead Screw Mechanism**. Cơ cấu vít me chuyển đổi chuyển động quay của động cơ thành chuyển động tịnh tiến, qua đó thực hiện thao tác đóng và mở cơ cấu nhả kem.

Hai công tắc hành trình **KW12-071** cung cấp tín hiệu giới hạn hành trình về STM32: **Upper Limit Switch** nối với **PB0/B0**, còn **Lower Limit Switch** nối với **PB10/B10**. Trong sơ đồ, mỗi GPIO nối qua điện trở nối tiếp R đến chân COM của công tắc tương ứng; chân NO nối về GND phía STM32. Hai ngõ vào sử dụng điện trở kéo lên nội của STM32. Khi công tắc được nhấn, tiếp điểm COM–NO đóng để kéo ngõ vào xuống mức LOW (active-low). Hai công tắc xác định vị trí giới hạn trên và dưới của cơ cấu.

STM32 sử dụng các tín hiệu giới hạn này để điều khiển **BTS7960** theo từng chiều. Khi **Upper Limit Switch** tác động, chuyển động lên bị dừng và không được phép tiếp tục; khi **Lower Limit Switch** tác động, chuyển động xuống bị dừng và không được phép tiếp tục. Chuyển động theo chiều ngược vẫn được phép để đưa cơ cấu ra khỏi vị trí giới hạn, miễn là công tắc giới hạn của chiều đó không tác động. Khi cả hai công tắc cùng tác động, cả hai chiều chuyển động đều bị chặn.

Về cấp nguồn, **24V Switching Power Supply** chuyển đổi điện áp 220V AC thành 24V DC để cấp cho ngõ nguồn động cơ của BTS7960 và động cơ. **LM2596 Step-Down Voltage Regulator** chuyển đổi nguồn 24V thành một đường 5V riêng để cấp cho phần logic của BTS7960 và phía ngõ ra của các bộ PC817. **5V Power Adapter** cấp nguồn cho cả STM32 và **TTL-to-RS485 Converter Module (MAX485)**. Trong cấu hình phần cứng đang sử dụng, bo STM32 và module MAX485 được cấp nguồn 5V, còn đường tín hiệu nối tiếp giữa hai bo sử dụng mức logic 3.3V theo xác nhận về module thực tế. Các bộ PC817 tạo cách ly điện giữa mass phía STM32/mạch điều khiển và các tín hiệu điều khiển phía mạch công suất.

### Detailed Electrical Schematic

**[Chèn Hình 1.2.2 tại đây]**

**Figure 1.2.2 – Detailed Electrical Schematic of the Automatic Ice Cream Dispensing Module**

Hình 1.2.2 trình bày sơ đồ điện chi tiết của mạch điều khiển module tự động nhả kem, gồm STM32 Blue Pill, TTL-to-RS485 Converter Module (MAX485), hai PC817 Optocoupler, BTS7960 Motor Driver Module, LM2596 Step-Down Voltage Regulator, hai công tắc hành trình KW12-071 và các điện trở 330 Ω, 4.7 kΩ. Các nhãn tín hiệu cùng tên thể hiện những điểm được nối điện với nhau.

PA9 và PA10 của STM32 lần lượt nối với DI và RO của MAX485; PA4 nối chung với DE và /RE để chọn hướng truyền hoặc nhận. PA8 và PB1 nối đến chân 1 của hai PC817, mỗi chân 2 nối qua điện trở 330 Ω về GND_ADAPTER. Ở phía ngõ ra, chân 4 của mỗi PC817 nối nguồn 5 V từ LM2596; chân 3 lần lượt nối RPWM và LPWM của BTS7960, đồng thời nối qua điện trở kéo xuống 4.7 kΩ về OUT−. Các chân VCC, L_EN và R_EN của BTS7960 nối nguồn 5 V này.

PB0 mang nhãn UPPER_LIMIT và PB10 mang nhãn LOWER_LIMIT. Mỗi đường tín hiệu nối qua điện trở nối tiếp 4.7 kΩ đến COM của công tắc tương ứng; NO nối GND_ADAPTER và NC để hở. Hai ngõ vào sử dụng điện trở kéo lên nội của STM32 để tạo tín hiệu giới hạn active-low. Điện trở nối tiếp tại công tắc khác với điện trở kéo xuống ở phía ngõ ra PC817.

Nguồn +5V_ADAPTER và GND_ADAPTER cấp cho phía STM32/MAX485 và làm tham chiếu cho hai công tắc. Nguồn +24V cấp cho B+ của BTS7960 và IN+ của LM2596; B− và IN− nối GND phía công suất. OUT+ của LM2596 tạo đường 5 V cho phía BTS7960/PC817, còn OUT− nối GND logic của BTS7960. Các nhãn nguồn phân biệt miền điều khiển và miền công suất.

Các kết nối ngoài không được vẽ lại trong Hình 1.2.2: chân A/B của MAX485 kết nối với USB-to-RS485 Converter, còn M+/M− của BTS7960 kết nối với động cơ JGB37-520, như thể hiện trong Hình 1.2.1. Nguồn 5 V adapter và nguồn 24 V được biểu diễn bằng các nhãn nguồn tương ứng.

### 1.2.1 Control Unit

**[Chèn bảng Control Unit tại đây]**

### 1.2.2 Communication Unit

**[Chèn bảng Communication Unit tại đây]**

### 1.2.3 Power Supply Unit

**[Chèn bảng Power Supply Unit tại đây]**

### 1.2.4 Actuator Unit

**[Chèn bảng Actuator Unit tại đây]**

### 1.2.5 Limit Sensor Unit

**[Chèn bảng Limit Sensor Unit tại đây]**

### 1.2.6 Communication Standardization and Extensibility

Mạch điều khiển được trình bày trong tài liệu là một giải pháp custom được phát triển để chuyển đổi máy kem từ cơ chế vận hành thủ công sang cơ chế tự động. Thiết kế này là một phiên bản triển khai tham khảo cho máy kem cụ thể và không phải là cấu hình phần cứng bắt buộc đối với tất cả các thiết bị được tích hợp vào hệ thống kiosk.

Hệ thống sử dụng RS485 làm giao diện vật lý chung cho các máy ngoại vi được Edge điều khiển trực tiếp. Mỗi thiết bị cần có giao diện RS485 tương thích và có thể sử dụng mạch điều khiển, nguồn điện, cảm biến và cơ cấu chấp hành riêng. Các yêu cầu về giao thức và tích hợp phần mềm được trình bày trong tài liệu tương ứng.

Việc sử dụng RS485 làm giao diện giao tiếp tiêu chuẩn giúp thống nhất phương thức kết nối giữa Edge Computer và các thiết bị ngoại vi. Edge Computer giao tiếp với bus RS485 thông qua USB-to-RS485 Converter, trong khi các bộ điều khiển như STM32 kết nối qua giao tiếp UART phía logic của TTL-to-RS485 Converter Module (MAX485). Thiết kế này giúp giảm sự khác biệt giữa các loại máy, đơn giản hóa quá trình tích hợp và hỗ trợ khả năng mở rộng của hệ thống.

Do đó, các sơ đồ trong phần này mô tả một trường hợp triển khai cụ thể cho máy kem, đồng thời cung cấp cơ sở tham khảo cho quá trình tích hợp các thiết bị ngoại vi khác vào hệ thống kiosk.
