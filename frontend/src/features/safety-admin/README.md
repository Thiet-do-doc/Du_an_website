# SafetyAdmin frontend
Chủ trì TV5; người dự phòng TV1.
Block, report, quản trị, khóa tài khoản và audit.

- `pages/`: màn hình cấp route.
- `components/`: thành phần chỉ dùng trong module.
- `api/`: hàm gọi API; tái sử dụng client ở `shared/api/`.
- `types/`: TypeScript DTO tương ứng với backend.

Tuần 3 (#26): `components/ReportForm.tsx` và `api/reports.ts` dùng API thật; form được dùng tại /safety và ChatPage. Xem `docs/ISSUE_26_HANDOFF.md` ở gốc repo để biết props, DTO, kiểm thử và cách demo.
