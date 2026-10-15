# SafetyAdmin
- Chủ trì: **TV5**. Dự phòng: **TV1**.
- Phạm vi: Block, report, quản trị, khóa tài khoản và audit.
- Bảng dự kiến: Blocks, Reports, ModerationActions.
- Frontend tương ứng: `frontend/src/features/safety-admin/`.

Đã có endpoint Block/Report/Admin, Identity và migration SafetyAdmin. Hợp đồng
hiện có nằm ở phần T5 bên dưới và `docs/API_CONTRACT.md`.

## Đầu việc tích hợp tiếp theo
1. TV1 xác nhận UserId, claim, cấp role Admin và service khóa/mở tài khoản hiện có.
2. TV3 chốt hủy match khi block; TV4 gọi `IBlockPolicy` trên REST/Hub và chỉ cho
   phép đọc đúng message đang bị report, không cấp quyền xem toàn bộ chat.
3. Chốt DTO/status với nhóm; bổ sung entity và migration có review khi cần.
4. Hoàn thiện UI Block và Report/Admin với loading/error/empty.
5. Kiểm tra sai quyền, audit và luồng end-to-end cùng nhóm; gửi PR review.

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
