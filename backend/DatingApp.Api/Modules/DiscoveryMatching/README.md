# DiscoveryMatching
- Chủ trì: **TV3**. Dự phòng: **TV4**.
- Chuyển chọn lọc từ `origin/thien@aa3af84` sang nền `origin/main@e381eb0`.
- Phạm vi: Khám phá, lọc, like hai chiều, match và bỏ match.
- Bảng dự kiến: UserLikes, Passes, Matches.
- Frontend tương ứng: `frontend/src/features/discovery-matching/`.

Module có các route/DTO/service và luồng gọi API ở frontend; các adapter thật của Auth,
Profiles, SafetyAdmin và ChatNotifications vẫn cần được nối để chạy trọn luồng.

## Đầu việc đầu tiên
1. Chốt request/response DTO và quy tắc quyền với thành viên liên quan.
2. Tạo entity, cấu hình EF Core và migration có review khi đã chốt schema.
3. Xây service xử lý nghiệp vụ; controller chỉ nhận, kiểm tra và trả HTTP.
4. Nối giao diện React với API thật, có loading/error/empty.
5. Thêm kiểm thử sai quyền và quy tắc quan trọng; gửi PR cho người khác review.

Không tạo bảng hoặc API trùng với module khác. Không phát triển trước Auth
bằng cách cho phép mọi người thao tác dữ liệu. Trao đổi API qua `Shared/Contracts`
và service nếu cần thay đổi dữ liệu thuộc module khác.

## Thiết kế phần lõi TV3

- `UserLikes` dùng khóa ghép `(FromUserId, ToUserId)`; `Passes` cũng dùng khóa ghép riêng. Constraint cấm tự like/tự bỏ qua. Like lặp là thao tác an toàn; like sau khi bỏ qua gỡ lượt pass trước đó.
- `Matches` lưu cặp UUID theo thứ tự tăng dần và có unique index trên cặp. `UnmatchedAt` giữ lịch sử; P0 không cho ghép lại sau khi hủy. Thời gian lưu UTC.
- `MatchService` chạy luồng hai like trong transaction Serializable. Khi hai lượt like đối diện đến đồng thời, unique constraint là lớp bảo vệ cuối; request thua thử lại và đọc kết quả đã commit. `EnsureConversationAsync(matchId)` trả về conversation UUID và chạy trong cùng scoped `AppDbContext`/transaction; phát sự kiện match chỉ sau commit. Danh sách match là cách đồng bộ lại nếu mất sự kiện.
- Bỏ match ghi `UnmatchedAt` ở backend; quyền gửi tin phải dựa vào trạng thái đó, không dựa vào trạng thái UI. `IMatchConversationBridge` là hợp đồng cần TV4 hiện thực cho conversation và thông báo.

Controller yêu cầu policy `User` và lấy UUID từ principal (`NameIdentifier` hoặc `sub`); client không truyền UserId người thực hiện. Auth và truy vấn Profiles thật đã đăng ký. Block lookup và conversation bridge hiện vẫn fail-closed với 503 cho tới khi TV5/TV4 bàn giao adapter.

Migration `AddDiscoveryMatching` tạo ba bảng, khóa cặp, ràng buộc cấm tự tương tác, index và khóa ngoại tới `AspNetUsers(Guid)`. Migration không tự chạy khi khởi động API; cần review và áp dụng theo lịch migration chung.

P1 Posts chưa nằm trong phần bàn giao này vì phân công yêu cầu chỉ bắt đầu sau khi P0 ổn định và cần tích hợp module Posts của TV2. Các adapter Profiles/Auth/TV4/TV5 cũng phải được đăng ký bởi module chủ quản; fallback hiện tại cố ý trả 503 khi thiếu adapter.
