# SafetyAdmin
- Chủ trì: **TV5**. Dự phòng: **TV1**.
- Phạm vi: Block, report, quản trị, khóa tài khoản và audit.
- Bảng dự kiến: Blocks, Reports, ModerationActions.
- Frontend tương ứng: `frontend/src/features/safety-admin/`.

Đây là chỗ phát triển tiếp, chưa có endpoint nghiệp vụ.

## Đầu việc đầu tiên
1. Chốt request/response DTO và quy tắc quyền với thành viên liên quan.
2. Tạo entity, cấu hình EF Core và migration có review khi đã chốt schema.
3. Xây service xử lý nghiệp vụ; controller chỉ nhận, kiểm tra và trả HTTP.
4. Nối giao diện React với API thật, có loading/error/empty.
5. Thêm kiểm thử sai quyền và quy tắc quan trọng; gửi PR cho người khác review.

Không tạo bảng hoặc API trùng với module khác. Không phát triển trước Auth
bằng cách cho phép mọi người thao tác dữ liệu. Trao đổi API qua `Shared/Contracts`
và service nếu cần thay đổi dữ liệu thuộc module khác.
