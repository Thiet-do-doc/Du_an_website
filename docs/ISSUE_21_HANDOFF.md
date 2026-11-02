# Bàn giao #21 — Khuyến / nhánh khuyen

## Phạm vi và hợp đồng

Nhánh đã ghép `origin/main` để dùng Identity Guid, JWT và `ICurrentUser` của Thiết.
Không nhận ID người thao tác từ body/query. Mọi thao tác chỉ thuộc tài khoản trong JWT.

| API | Kết quả |
| --- | --- |
| GET /api/v1/blocks | 200, chỉ các block do chính mình tạo; `{ userId, blockedAt }[]` |
| POST /api/v1/blocks/{targetUserId} | 204, kể cả lặp hoặc đồng thời |
| DELETE /api/v1/blocks/{targetUserId} | 204, kể cả không còn block |

Cần `Authorization: Bearer <access token>`. 401 nếu chưa đăng nhập/phiên hết hiệu lực.
ID sai, Guid.Empty, tự chặn/tự bỏ chặn: 400. POST người không tồn tại: 404.
DELETE chỉ xóa chiều mình sở hữu; không xóa block chiều ngược lại.
UI thật tại `/blocks`, có danh sách, loading/error/empty, tải lại, chặn và bỏ chặn.
`BlockButton.tsx` có thể dùng trên hồ sơ/chat với `userId`, `blocked`, `onChanged`.

## Tích hợp cho Thiện/Mạnh/Võ

Inject `IBlockPolicy` (scoped), chữ ký:

```csharp
Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId,
    CancellationToken cancellationToken = default);
```

Ví dụ REST trước khi like, match, gửi tin hoặc tải nội dung hội thoại:

```csharp
if (currentUser.UserId is not Guid actorId) return Unauthorized();
if (!await blockPolicy.CanInteractAsync(actorId, targetUserId, cancellationToken))
    return Forbid();
```

Hub cần kiểm tra cả REST lẫn từng lần gửi tin/nhận nội dung, dùng user ID từ phiên
và kiểm tra tư cách thành viên hội thoại trước. Không lưu kết quả policy lâu dài.
A chặn B => A→B và B→A đều bị từ chối. A bỏ chặn nhưng B vẫn chặn A => vẫn từ chối.
Discovery lọc người bị chặn theo cả hai chiều trước phân trang; chat ẩn hội thoại
khỏi danh sách và từ chối tải/gửi theo ID trực tiếp. Không chỉ ẩn nút ở frontend.
Bỏ chặn không sửa, tái tạo hay khôi phục match. Việc hủy match cần service của
module Discovery/Matching và quyết định chung; API hiện chỉ thay đổi bảng block.

**Giới hạn:** chưa tuyên bố chặn xuyên toàn hệ thống. Chat/match còn phải ghép policy
vào module của người phụ trách; chưa có demo chat/discovery tích hợp trong PR này.

## Migration

Đã cập nhật từ main trước khi sinh migration. Thứ tự:
`InitialIdentity` → `AuthSessions` → `AddProfiles` → `SafetyBlocksAndModeration`.
Migration mới tạo `user_blocks` với khóa ghép, FK tới Identity, check cấm tự chặn;
đồng thời giữ các bảng report/admin có sẵn của nhánh. Không tạo lại Identity.
Loại migration SafetyAdmin cũ của nhánh vì nó tạo Identity string trùng với main Guid.
Nếu từng áp dụng migration cũ vào DB local, giữ backup và dùng DB dev mới theo chuỗi
chung; không áp migration mới trực tiếp lên schema Identity string cũ.
Không sửa migration đã chạy trên môi trường chung. Thiết cần review snapshot/thứ tự.

```powershell
dotnet tool restore
dotnet ef database update --project backend/DatingApp.Api/DatingApp.Api.csproj --startup-project backend/DatingApp.Api/DatingApp.Api.csproj
$env:AUTH_TEST_CONNECTION = '<connection string database test riêng đã migrate>'
dotnet test DatingApp.slnx
```

Tests dùng database test riêng, sinh user giả duy nhất, không chạy trên dữ liệu thật.
CI có PostgreSQL 17, tự áp migration rồi chạy tests. Các test PostgreSQL sẽ skip
khi không đặt AUTH_TEST_CONNECTION; build xanh một mình không thay thế bằng chứng DB.

## Cần nhóm xác nhận trước khi đóng issue

Thiết review PR/schema; Thiện/Mạnh xác nhận chữ ký policy, ghép discovery/chat và demo
hai chiều. Không đánh dấu issue hoàn thành chỉ vì code nhánh Khuyến đã build/test.
Xem `infra/OPERATIONS.md` cho cấu hình, volume, Nginx và kiểm tra secret.
