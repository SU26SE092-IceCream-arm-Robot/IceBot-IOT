# Technical Decisions

### 2026-07-29 Di chuyển Cấu hình Hệ thống vào Native Menu Bar & Tối giản Header
- **Context**: Thanh Header bị quá tải thông tin và nhiều dải nút điều khiển/ngôn ngữ/tên dự án rời rạc, làm giảm tính thẩm mỹ của phần mềm Desktop 3D chuyên nghiệp.
- **Decision**: Di chuyển cấu hình chọn Ngôn ngữ, Hiện/ẩn thanh công cụ nhanh và Hiện/ẩn Hitbox va chạm lên Native Menu Bar của Electron thông qua cơ chế đồng bộ IPC 2 chiều. Chuyển ô đặt tên dự án thành Click-to-edit phẳng đặt bên trái cạnh Logo.
- **Rationale**: Tận dụng tối đa giao diện gốc của hệ điều hành mang lại trải nghiệm Desktop thuần túy, sạch sẽ, giải phóng không gian Header, tăng độ hoàn thiện tương tự VS Code hay Photoshop.
- **Consequences**: Cần xử lý đồng bộ IPC chặt chẽ để đảm bảo dấu tích (check/radio) trên Menu Bar luôn khớp với Zustand store, và tự động rebuild Menu khi đổi ngôn ngữ.

### 2026-07-29 Chỉ dẫn Đồ họa Khớp Xoay bằng Vòng Khuyên ôm sát & Phát sáng Emissive
- **Context**: Người dùng khó nhận biết trục xoay và hướng xoay của các khớp robot FR5, cũng như không biết khớp nào hỗ trợ di chuyển FK khi nhìn vào mô hình 3D.
- **Decision**: Sinh 6 vòng khuyên 3D mỏng (2-3mm) ôm khít sát vỏ khớp của robot, xoay theo trục `joint.axis` bằng Quaternion. Cho phép các khớp phát sáng nhẹ màu vàng hổ phách lúc rảnh, đổi sang xanh lá khi chọn và đỏ khi chạm giới hạn.
- **Rationale**: Tạo chỉ dẫn trực quan cao cấp, hỗ trợ người dùng định hình không gian xoay nhanh chóng trước khi kéo thả mà không thay đổi bất kỳ logic IK/FK hay chuyển động thực tế của robot.
- **Consequences**: Phải căn chỉnh bán kính vòng khuyên tối ưu sát vỏ (9.2cm -> 4.5cm) để tránh đè lấp mesh hoặc quá to gây mất thẩm mỹ.

### 2026-07-29 Loại trừ Vòng Khuyên Chỉ dẫn ra khỏi tính toán va chạm OBB
- **Context**: Các vòng khuyên chỉ dẫn gắn vào khớp robot bị thuật toán tính toán hitbox (DFS của `computeLinkOBB`) gom nhầm vào giới hạn hình học của Link cha, làm phình to và biến dạng các hộp va chạm OBB khi xoay khớp.
- **Decision**: Gán thẻ định danh `ringMesh.userData.isJointHelper = true` khi khởi tạo và bỏ qua các node mang thẻ này khi thu thập meshes tính OBB.
- **Rationale**: Đảm bảo sự phân tách rõ ràng giữa mô hình đồ họa hỗ trợ người dùng (visual helper) và mô hình tính toán vật lý (collision geometry), giữ cho dữ liệu va chạm luôn chính xác theo thiết kế cơ học thực tế.
- **Consequences**: Không có ảnh hưởng tiêu cực nào đến chuyển động hay va chạm thực của robot.

### 2026-08-15 Định dạng Lưu trữ Container (.fairobot Bundle v1.2) Tự Đóng Gói Toàn Bộ Asset 3D
- **Context**: File dự án `.fairobot` trước đây chỉ lưu chuỗi đường dẫn tuyệt đối `filePath` trên máy cục bộ, dẫn đến việc khi di chuyển thư mục hoặc gửi file sang máy tính khác thì toàn bộ mô hình CAD 3D (.obj, .stl, .gltf) bị gãy liên kết và biến mất.
- **Decision**: Nâng cấp định dạng `.fairobot` thành Container Bundle chuẩn nén ZIP bằng `fflate`. Khi lưu, toàn bộ dữ liệu nhị phân của các mô hình 3D trong scene được đóng gói vào thư mục ảo `assets/` cùng với `project.json`. Khi mở, ứng dụng giải nén in-memory và tạo `Blob URL` nạp trực tiếp vào Three.js. Đồng thời duy trì tương thích ngược 100% với các file JSON cũ v1.0/v1.1.
- **Rationale**: Chuẩn hóa định dạng file theo mô hình chuẩn công nghiệp (tương tự `.blend` của Blender hoặc `.docx` của Word), biến dự án thành file độc lập, an toàn khi chia sẻ giữa các thành viên và thiết bị mà không làm tăng đáng kể dung lượng nhờ thuật toán nén Deflate.
- **Consequences**: Cần quản lý vòng đời `Blob URL` sạch sẽ và bổ sung IPC đọc/ghi file nhị phân trong Electron Main/Preload process.

