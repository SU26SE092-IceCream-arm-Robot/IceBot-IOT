# Debug Playbook

### [SYMPTOM] Dính phím di chuyển WASD khi cửa sổ ứng dụng mất focus
- **Symptom**: Người dùng nhấn giữ các phím di chuyển, sau đó click ra ngoài hoặc chuyển cửa sổ/mở devtools. Khi quay lại, camera hoặc robot vẫn tiếp tục di chuyển tự động dù người dùng không nhấn phím.
- **Root Cause**: Sự kiện `keyup` không được kích hoạt trên window khi ứng dụng bị mất tiêu điểm (blur). Do đó, các phím bị kẹt lại trong bộ nhớ đệm phím bấm `keysPressedRef.current` ở trạng thái đang nhấn.
- **Fix**: Lắng nghe sự kiện `blur` trên window (`handleBlur`). Khi sự kiện này kích hoạt, tự động xóa sạch (`clear()`) toàn bộ danh sách phím đang giữ trong `keysPressedRef`.
- **Verification**: Chạy ứng dụng, giữ phím di chuyển và bấm click chuột ra màn hình bên ngoài, xác minh camera/robot dừng di chuyển lập tức. Chạy typecheck và build thành công.
- **Date**: 2026-07-29
- **Tags**: `keyboard`, `sticky-keys`, `three-js`, `input-handling`

### [SYMPTOM] Hộp va chạm (Hitbox OBB) của các khớp liên kết bị phình to bất thường khi xoay robot
- **Symptom**: Khi di chuyển/xoay một khớp (ví dụ: khớp vai `j2`), hộp va chạm màu xanh lá/đỏ của link cha (`shoulder_link`) đột nhiên bị kéo giãn, phình to ra bao phủ cả khoảng không gian xung quanh khớp.
- **Root Cause**: Thuật toán DFS trong `computeLinkOBB` thu thập đệ quy tất cả các con có `isMesh = true` để tính kích thước hộp giới hạn. Do vòng khuyên chỉ dẫn (`ringMesh`) được add làm con của `joint` (thuộc link cha) và là một Mesh nên đã bị gom nhầm vào OBB, làm phình to kích thước khi khớp quay.
- **Fix**: Gán thẻ định danh `userData.isJointHelper = true` cho `ringMesh`, và bổ sung điều kiện dừng đệ quy trong `collectMeshes`: `if (node.userData.isJointHelper) return`.
- **Verification**: Bật chế độ hiển thị hitbox, di chuyển robot FK và kiểm chứng các hộp va chạm bao quanh ống robot giữ nguyên hình dạng vật lý thực, không bị biến dạng.
- **Date**: 2026-07-29
- **Tags**: `obb`, `hitbox`, `collision`, `mesh-traversal`

### [SYMPTOM] Mô hình 3D (CAD Assets) bị mất khi chuyển file dự án `.fairobot` sang máy khác
- **Symptom**: Người dùng lưu file `.fairobot` có chứa đầu gắp hoặc khay 3D, gửi file sang máy tính khác hoặc đổi tên thư mục chứa file CAD gốc thì khi mở lại dự án, toàn bộ mô hình 3D biến mất.
- **Root Cause**: File `.fairobot` trước đây chỉ lưu chuỗi đường dẫn cục bộ `filePath` (ví dụ `C:\Users\Admin\Downloads\gripper.obj`), khi sang máy khác không tìm thấy file tại đường dẫn này.
- **Fix**: Chuyển đổi định dạng file sang Container ZIP Bundle v1.2 sử dụng `fflate`. Khi lưu, đóng gói toàn bộ buffer nhị phân của các 3D mesh vào thư mục `assets/` bên trong file `.fairobot`. Khi mở, giải nén in-memory và tạo `Blob URL` nạp vào Three.js loader.
- **Verification**: Lưu dự án có chứa file `.obj` và `.stl`, xóa file CAD gốc trên đĩa cứng và mở lại file `.fairobot`. Xác minh mô hình 3D vẫn nạp và render đầy đủ 100%.
- **Date**: 2026-08-15
- **Tags**: `project-bundle`, `3d-assets`, `fflate`, `zip-container`, `blob-url`

