# Active Context

## Last updated
2026-09-07 00:00 +07:00

## Current focus
- Dashboard SystemAdmin redesign đã được triển khai và phát hành trên nhánh `refactorUI`.

## Current status
- Commit `f35d759f66f597fa83a907e09473beae65977be6` đã push thành công lên `origin/refactorUI`.
- Khối “Cần xử lý ngay” dùng dữ liệu Dashboard GraphQL thật; frontend chỉ tổng hợp ưu tiên, mô tả và deep-link.
- Vòng đời kiosk và trạng thái kết nối được hiển thị thành hai nhóm độc lập.
- OrgAdmin và các vai trò khác vẫn giữ nhánh dashboard vận hành cũ.
- Worktree sạch; 367 tests, TypeScript, ESLint, architecture check và production build đều pass.

## Important decisions
- Chỉ thay đổi presentation/composition cho SystemAdmin; giữ API, payload, RBAC, routes, CRUD và realtime semantics.
- `Maintenance` là trạng thái vận hành, không thuộc vòng đời kiosk.
- Không tạo dữ liệu xu hướng giả hoặc endpoint mới.
- `openapi.json` và `schema.graphql` tiếp tục local-only.

## Open issues / risks
- Live authenticated browser visual QA cho dashboard chưa được thực hiện trong phiên do browser connector không khả dụng; user đã yêu cầu commit/push.
- SignalR status trên dashboard phản ánh trạng thái kết nối; lỗi WebSocket/negotiation vẫn cần môi trường backend thật để chẩn đoán nếu xuất hiện.
- Redesign các màn authenticated khác vẫn còn trong roadmap.

## Next steps
- Nếu tiếp tục UI work, nghiệm thu dashboard trên localhost bằng tài khoản SystemAdmin và OrgAdmin.
- Tiếp tục checklist redesign theo từng workflow, chờ user duyệt từng hạng mục.
- Smoke-test các workflow backend còn thiếu bằng dữ liệu authenticated thật.

## Source of truth warning
Memory is advisory. Current repository files are the source of truth.
