# DiscoveryMatching
- Chủ trì: **TV3**. Dự phòng: **TV4**.
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
- `Matches` lưu cặp UUID theo thứ tự tăng dần và có unique index trên cặp. `UnmatchedAt` giữ lịch sử, cho phép mở lại cùng cặp mà không tạo match trùng. Thời gian lưu UTC.
- `MatchService` chạy luồng hai like trong transaction Serializable. Khi hai lượt like đối diện đến đồng thời, unique constraint là lớp bảo vệ cuối; request thua thử lại và đọc kết quả đã commit. `EnsureConversationAsync(matchId)` trả về conversation UUID và chạy trong cùng scoped `AppDbContext`/transaction; phát sự kiện match chỉ sau commit. Danh sách match là cách đồng bộ lại nếu mất sự kiện.
- Bỏ match ghi `UnmatchedAt` ở backend; quyền gửi tin phải dựa vào trạng thái đó, không dựa vào trạng thái UI. `IMatchConversationBridge` là hợp đồng cần TV4 hiện thực cho conversation và thông báo.

Controller lấy UUID người dùng từ principal (`NameIdentifier` hoặc `sub`) và trả 401 nếu chưa xác thực. Do bộ khung chưa cài middleware Auth, chỉ request đã được một middleware xác thực đặt principal mới được xử lý; không có đường truyền UserId từ client. Khi DB có cấu hình, các adapter chưa được đăng ký trả 503 thay vì cho phép tương tác mà không kiểm tra profile/block/conversation.

Migration `20261015150402_DiscoveryMatchingInitial` đã được tạo cho ba bảng TV3 và script idempotent đã được sinh để rà soát. Trước khi áp dụng lên database dùng chung, cần đối chiếu schema UserId với TV1, thống nhất migration slot theo quy ước repo và tạo migration bổ sung cho foreign key nếu bảng người dùng đã sẵn sàng. Migration hiện không tự chạy khi khởi động API.

P1 Posts chưa nằm trong phần bàn giao này vì phân công yêu cầu chỉ bắt đầu sau khi P0 ổn định và cần tích hợp module Posts của TV2. Các adapter Profiles/Auth/TV4/TV5 cũng phải được đăng ký bởi module chủ quản; fallback hiện tại cố ý trả 503 khi thiếu adapter.
