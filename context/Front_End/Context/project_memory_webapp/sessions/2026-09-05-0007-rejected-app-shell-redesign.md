# Session: Rejected App Shell Redesign

## Date
2026-09-05 00:07 +07:00

## Summary

Hạng mục 14 được audit và thử triển khai App Shell mới, nhưng thiết kế bị người dùng từ chối vì mang vẻ AI-template, thiếu hiện đại và không cải thiện UX thực chất. Theo yêu cầu người dùng, toàn bộ thay đổi của lần thử đã được hủy để phiên sau bắt đầu lại từ đầu.

## What changed

- Đã rollback mọi thay đổi thử nghiệm trong dashboard layout và sidebar.
- Đã xóa các component mobile navigation, topbar, breadcrumbs và test mới tạo.
- Không thay đổi hoặc xóa hai API snapshots staged có từ trước.
- Cập nhật memory/handoff để ghi nhận hướng thiết kế bị từ chối và nguyên nhân.

## Files touched

- `.project-memory/active-context.md`
- `.project-memory/handoff.md`
- `.project-memory/sessions/2026-09-05-0007-rejected-app-shell-redesign.md`

Mã nguồn ứng dụng đã trở về trạng thái trước lần thử Hạng mục 14.

## Decisions made

- Hạng mục 14 chưa hoàn tất và không có baseline thiết kế được chấp nhận.
- Phiên mới phải làm discovery và visual direction trước khi code.
- Thiết kế phải được đánh giá end-to-end trên một màn hình nghiệp vụ, không chỉ ở lớp App Shell.
- Technical correctness và visual/UX acceptance là hai cổng nghiệm thu độc lập.

## Bugs / risks / unresolved issues

- Thiết kế bị từ chối dùng dark navy, cyan/neon, glow, gradient, decorative grid, badge/slogan kỹ thuật và active indicator phô trương; tổng thể trông như AI-template.
- Shell mới không đồng bộ với các module cũ, đặc biệt trang Maintenance.
- Navigation vẫn dài và không được ưu tiên theo workflow.
- Header, breadcrumb, account và alert có sự trùng lặp.
- Maintenance vẫn có KPI card quá lớn, filter container dư thừa, mật độ thấp và empty state chiếm quá nhiều không gian.
- Chưa có visual direction thay thế được người dùng duyệt.

## Commands / checks run

```txt
git restore --worktree -- src/app/(dashboard)/layout.tsx src/components/shared/app-sidebar.tsx
PASS

git diff --exit-code -- src/app/(dashboard)/layout.tsx src/components/shared/app-sidebar.tsx
PASS - no diff

git status --short
A  openapi.json
A  schema.graphql
```

## Next recommended steps

1. Audit jobs-to-be-done và task frequency của navigation.
2. Chọn một module pilot end-to-end; Maintenance là ứng viên tốt vì có phản hồi trực quan cụ thể.
3. Đề xuất 2-3 visual directions bằng wireframe hoặc mockup đủ rõ để người dùng chọn.
4. Chỉ sau khi duyệt mới lập Patch Contract và triển khai.

## Notes for future agents

- Không lặp lại hướng “futuristic operational dashboard” bằng dark sidebar, cyan glow và decorative tech labels.
- Không tuyên bố redesign hoàn tất nếu chỉ thay shell trong khi phần nội dung module vẫn giữ nguyên visual/UX cũ.
- Không dùng test/build pass thay cho visual judgment.
- Không lưu secrets hoặc dữ liệu đăng nhập trong memory.
