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

## Hợp đồng T5

- `POST /api/v1/blocks/{targetUserId}` và `DELETE /api/v1/blocks/{targetUserId}`: cần đăng nhập; lặp lại là an toàn.
- `IBlockPolicy.CanInteractAsync(a, b)`: trả `false` nếu một trong hai phía đã chặn phía còn lại. TV3 gọi trước khi khám phá/tạo match; TV4 gọi trước gửi và nhận tin trên REST lẫn Hub. Bỏ chặn không tạo lại match.
- `POST /api/v1/reports`: body gồm `targetType` (`User`, `Message`, `Post`), `targetId`, `reason`, `description`; người gửi lấy từ phiên đăng nhập, không nhận `reporterUserId` từ client.
- `GET /api/v1/admin/reports`: chỉ role `Admin`; hỗ trợ `status`, `page`, `pageSize`. `GET/PATCH /api/v1/admin/reports/{id}` xử lý và ghi người xử lý, ghi chú, thời điểm vào `ModerationActions`.
- `PATCH /api/v1/admin/users/{id}/status`: chỉ role `Admin`; gọi `IAccountStatusService` của Auth để cập nhật `IsActive`, lockout và security stamp; ghi audit trong cùng request.
- Admin report DTO chỉ chứa metadata và ID đối tượng. Chưa có endpoint đọc nội dung chat. Không cấp quyền truy cập toàn bộ chat cho admin.

Migration `SafetyAdmin` tạo schema Identity cùng `Blocks`, `Reports`, `ModerationActions`. Hiện chỉ nhận report đối tượng User đã tồn tại; endpoint trả 501 cho Message/Post cho tới khi TV4/TV2 cung cấp validator kiểm tra ID và quyền của người gửi. TV1 cần cấu hình role `Admin` và thống nhất `IAccountStatusService` trong Auth trước khi dùng quyền quản trị trên môi trường chung.
