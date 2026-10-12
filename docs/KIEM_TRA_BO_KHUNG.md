# Kết quả kiểm tra bộ khung

Ngày kiểm tra: 12/10/2026.

- Frontend: TypeScript typecheck và `npm run build` thành công.
- Backend: biên dịch .NET 10 và kiểm thử hợp đồng endpoint health.
- Chạy API thật: `/api/v1/health` trả 200.
- Khi chưa cấu hình PostgreSQL: `/api/v1/health/database` trả 503 với trạng thái `not_configured`.
- OpenAPI JSON có các đường dẫn health đúng.
- Vite phục vụ HTML và proxy `/api` tới backend hoạt động.
- `.gitignore` loại `.env`, `node_modules`, `bin`, `obj`; giữ cấu hình mẫu và lockfile.

Các bài test dùng probe giả có kiểm soát để kiểm tra HTTP 200/503. Chưa chạy kết nối
với một PostgreSQL thật trong môi trường kiểm tra này. Mỗi thành viên phải thực hiện
bước 3 trong README và xác nhận database báo `connected` trên máy mình.

Bộ khung chưa có chức năng tài khoản, hồ sơ, match, chat hoặc admin; không dùng
trạng thái health thành công làm bằng chứng các chức năng đó đã hoàn thành.
