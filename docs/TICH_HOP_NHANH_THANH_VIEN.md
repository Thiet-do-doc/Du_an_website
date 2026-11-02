> Cập nhật 26/10/2026: các module đã được nối trong bản tích hợp. Xem [INTEGRATION_STATUS.md](INTEGRATION_STATUS.md) cho schema và hành vi hiện hành. Nội dung bàn giao bên dưới được giữ để đối chiếu lịch sử.

# Đưa module của từng thành viên vào nền chung của Thiết

Nền chung gồm bộ khung 357db46, Auth b3e85c4 và phần Auth hoàn chỉnh từ máy Thiết.
Giữ lịch sử main và PR #2; thay các bản project/Auth ở gốc bằng module Auth trong
backend/DatingApp.Api. Các nhánh cũ vẫn giữ nguyên để đối chiếu và khôi phục.

Tài liệu này là quy trình bàn giao. Chỉ chạy các lệnh tạo nhánh từ origin/main
SAU KHI PR nền Thiết đã được merge vào main. Không merge nguyên các nhánh cũ chỉ
để hết conflict; mỗi người chuyển module của mình sang nhánh mới và sửa theo
hợp đồng dưới đây. Những phần code chưa push trên máy thành viên chưa nằm trong
origin; phải lưu, kiểm tra secret và commit/push vào nhánh riêng trước khi chuyển.

## 1. Nền dùng chung

| Thành phần | Quy ước phải giữ |
|---|---|
| Backend chạy | backend/DatingApp.Api/DatingApp.Api.csproj |
| Frontend chạy | frontend/src/main.tsx → AuthProvider → AppRouter |
| User | ApplicationUser : IdentityUser<Guid>; database UUID |
| Lấy tài khoản | ICurrentUser.UserId hoặc claims đã được xác thực, không tin ID client tự khai |
| Xác thực | JWT bearer; refresh cookie HttpOnly của Auth; không thêm Auth cookie độc lập |
| API riêng tư | [Authorize(Policy = "User")]; admin dùng policy "Admin" |
| Khóa tài khoản | IAccountAccess.SetActiveAsync(Guid, bool, CancellationToken) |
| Frontend HTTP | authenticatedFetch<T> trong features/auth/api.ts |
| Phiên frontend | useAuth / AuthProvider; không lưu token vào localStorage |
| EF | Một AppDbContext và một snapshot tại Infrastructure/Migrations |

auth/api.ts hỗ trợ refresh và thêm Bearer cho request. Ví dụ trong một feature:

```ts
import { authenticatedFetch } from '../auth/api';
// Chỉnh đường dẫn import tương ứng với thư mục file hiện tại.
const result = await authenticatedFetch('/api/v1/matches');
await authenticatedFetch('/api/v1/example', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ value: 'example' }),
});
```

SignalR cần accessTokenFactory lấy token của phiên Auth hiện hành, cập nhật sau
refresh và kiểm tra reconnect; không dùng endpoint cấp token demo. Giữ cấu hình
JWT đọc access_token cho /hubs và AccountHubFilter trong Program.cs.

## 2. Bắt đầu ở một thư mục mới để giữ nguyên code cũ

Chạy từ thư mục cha bạn chọn. Tên thư mục đích phải chưa tồn tại:

```powershell
git clone https://github.com/Thiet-do-doc/Du_an_website.git Du_an_website-main-moi
cd Du_an_website-main-moi
git fetch origin
git status
```

Sau đó chọn DUY NHẤT khối lệnh dành cho module của mình bên dưới. `git restore
--source` ở đây chỉ đưa những đường dẫn được liệt kê vào nhánh mới; nhánh cũ và
lịch sử gốc không thay đổi. Vì chuyển chọn lọc source không tạo merge parent với
nhánh cũ, PR phải ghi rõ tên nhánh và commit nguồn để truy nguyên đóng góp.

Không sao chép nguyên Program.cs, AppDbContext, App.tsx, csproj, package.json,
lockfile hoặc model snapshot từ nhánh cũ. Thêm từng đăng ký, DbSet, route và package
cần thiết vào bản main. Các file chung vẫn được sửa trong PR khi cần, nhưng phải
review phần thay đổi; CODEOWNERS yêu cầu review không tự động giải quyết conflict.

## 3. Thiên và DiscoveryMatching

```powershell
git switch -c feature/thien-discovery-v2 origin/main
git restore --source=origin/thien -- backend/DatingApp.Api/Modules/DiscoveryMatching frontend/src/features/discovery-matching tests/DatingApp.Api.Tests/DiscoveryMatchingAuthorizationTests.cs tests/DatingApp.Api.Tests/DiscoveryMatchingModelTests.cs
```

Sau khi lấy file, còn phải sửa và kết nối, chưa thể push rồi coi là hoàn thành:

- Thêm DbSet UserLikes/Passes/Matches vào AppDbContext hiện có; giữ Identity và
  RefreshTokens. Các IEntityTypeConfiguration được cấu hình tự động từ assembly.
- Thêm DI cho DiscoveryService, IMatchService và TimeProvider; giữ toàn bộ Auth
  middleware/DI. Dùng tài khoản đã xác nhận email, đang hoạt động, đủ profile.
- Đổi client fetch sang authenticatedFetch để gửi JWT và phục hồi phiên.
- Đưa DiscoveryPage vào router/layout chung, không thay toàn bộ App.tsx.
- Phối hợp TV2 cung cấp IDiscoveryProfileQuery, Khuyến cung cấp IBlockLookup,
  TV4 cung cấp IMatchConversationBridge. Không đổi fallback thành cho phép mọi
  người để vượt test. Thiếu dependency thì giữ trạng thái chưa sẵn sàng.
- Không copy Infrastructure/Data/Migrations từ nhánh cũ. Tạo migration bổ sung
  theo mục 6 sau khi chốt model. Test hai like đồng thời trên PostgreSQL.

Nguồn lúc bàn giao: thien / aa3af84. Giữ ghi chú nguồn này trong PR.

## 4. TV4 và ChatNotifications

```powershell
git switch -c feature/tv4-chat-v2 origin/main
git restore --source=origin/feat/tv4-chat-notifications -- backend/DatingApp.Api/Modules/ChatNotifications frontend/src/features/chat-notifications tests/DatingApp.Api.Tests/TV4ChatRulesTests.cs
```

- Các file được copy còn phụ thuộc Demo*. Thay DemoChatPermissions bằng truy vấn
  tài khoản thật, match thật và block thật. Không copy Demo/ vào hệ thống chính.
- Thay foreign key DemoUser/DemoMatch ở ChatEntityConfiguration bằng
  ApplicationUser/Match đã thống nhất. Giữ unique MatchId và SenderId+ClientMessageId.
- Giữ AccountHubFilter; đăng ký ChatService, NotificationService, bridge và
  app.MapHub<ChatHub>("/hubs/chat") trong Program.cs hiện có.
- Adapter IMatchConversationBridge phải trả Guid như contract của Thiên;
  MatchChatBridge hiện trả Conversation. Lưu match/conversation trong transaction
  chung và chỉ phát realtime sau commit. Cần hỗ trợ GetConversationIds và Unmatch.
- Xóa hoặc tách TV4DemoPage khỏi luồng chính; ChatPage lấy phiên từ AuthProvider.
- Thêm @microsoft/signalr đúng phiên bản trong package.json của nhánh nguồn bằng
  npm install --save-exact ở frontend, giữ các dependency/scripts hiện tại và
  commit package-lock.json mới. Không chép đè lockfile cũ.
- Không copy migration InitialTV4; lập migration cho bảng thật theo mục 6.
- Thử A/B/C, block/unmatch/khóa khi Hub đang mở, refresh token, reconnect và retry.

Nguồn lúc bàn giao: feat/tv4-chat-notifications / 62d6226.

## 5. Khuyến, SafetyAdmin và vận hành

```powershell
git switch -c feature/khuyen-safety-v2 origin/main
git restore --source=origin/khuyen -- backend/DatingApp.Api/Modules/SafetyAdmin frontend/src/features/safety-admin
```

- Đổi các UserId và foreign key sang Guid, map UUID; không copy ApplicationUser,
  AuthController, Program.cs Auth cookie hoặc AccountStatusService của nhánh cũ.
- Sửa AdminModerationController gọi IAccountAccess.SetActiveAsync để thu hồi
  refresh/session và ngắt Hub; giữ audit. Chốt ranh giới transaction nếu audit
  cần atomic cùng thao tác khóa, tránh mở transaction lồng nhau.
- Chuyển IBlockPolicy/BlockPolicy và DTO sang ID thống nhất; cung cấp adapter
  IBlockLookup cho Thiên, tra block hai chiều cho TV4.
- Thêm DbSet/config/DI cần thiết, giữ Identity<Guid> và RefreshTokens hiện tại.
- Frontend dùng authenticatedFetch. UI ẩn chức năng admin với user thường nhưng
  quyền vẫn phải kiểm tra ở backend. Reports Message/Post hiện 501: nối kiểm tra
  quyền đối tượng từ Chat/Posts trước khi bật, không giả thành thành công.
- Không copy migration SafetyAdmin cũ vì nó tạo lại bảng AspNetUsers kiểu text.
- Tạo test block hai chiều, user thường bị cấm admin, report không hợp lệ và khóa
  thu hồi phiên; các test health cũ không đủ nghiệm thu Safety.

Deploy nên là PR riêng sau khi Auth/Safety ổn. Lấy chọn lọc Dockerfile, compose và
nginx từ nhánh khuyen, sửa cấu hình JWT, SMTP, HTTPS, DataProtection và WebSocket
theo Auth hiện tại. Không copy README hoặc .env thật đè nền chung.

Nguồn lúc bàn giao: khuyen / 132b3c9.

## 6. Võ, Profiles và migration

Nhánh (vo) hiện có AuthStarter, không có module Profiles hoàn chỉnh. Các chức năng
Auth của nhánh này được nền Thiết thay thế. Không merge nguyên (vo) và không đưa
Program.cs/App.tsx/csproj/Controllers ở gốc vào repo. Nhánh và commit 932b614,
4f728dd vẫn được giữ để đối chiếu, không xóa đóng góp lịch sử.

Nếu Võ được nhóm giao Profiles, tạo nhánh sau (nếu phân công khác thì dùng tên
module đã thống nhất):

```powershell
git switch -c feature/vo-profiles-v2 origin/main
```

Làm trong Modules/Profiles và features/profiles; dùng ApplicationUser<Guid> hiện
có, không tạo hệ đăng nhập thứ hai. Cần Profile/Photo/Interest, API sửa hồ sơ, upload
ảnh và ProfileSummary cho Thiên. Nếu có code Profile chưa push, chuyển có chọn lọc
vào hai thư mục trên và ghi nguồn, không copy nguyên một project khác.

Migration chung: Thiết điều phối thứ tự, mỗi lượt một PR. Nền có InitialIdentity
và AuthSessions. Trước khi thay lịch sử migration, xác nhận có DB chung cần giữ
dữ liệu không. Nếu có, sao lưu và lập kế hoạch upgrade/convert ID; không xóa DB,
không sửa migration đã áp dụng. Nếu chỉ có DB dev bỏ được, dùng DB tích hợp MỚI
và tạo migration bổ sung từ model nền; không trỏ vào DB đang dùng.

Lệnh mẫu sau khi model và thứ tự đã được thống nhất, cấu hình DB test riêng:

```powershell
dotnet tool restore
# Thay AddModuleTables bằng tên module thực tế, không tạo lại InitialIdentity.
dotnet ef migrations add AddModuleTables --project backend/DatingApp.Api --output-dir Infrastructure/Migrations
dotnet ef migrations script --project backend/DatingApp.Api --idempotent --output migration-review.sql
# Review SQL, đặc biệt DROP/ALTER và việc tạo lại AspNetUsers, trước database update.
```

## 7. Kiểm tra và gửi PR

Tất cả lệnh dưới đây chạy từ gốc clone mới, sau khi đã sửa các mục dành cho module:

```powershell
powershell -File scripts/Test-RepositoryLayout.ps1
dotnet build DatingApp.slnx --configuration Release
dotnet test DatingApp.slnx --configuration Release
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend test
git status
git diff --check
git diff
```

Dùng Node 24 theo .nvmrc. Nếu test báo skipped thì phải đọc lý do; Auth cần
AUTH_TEST_CONNECTION trên DB test đã migration để chạy 8 test PostgreSQL. Không
được báo tất cả đạt khi chỉ chạy test không cần DB. Thêm test nghiệp vụ module.

Stage đúng các file đã kiểm tra, ví dụ cho Thiên (thêm migration và tài liệu cụ
thể nếu PR có chúng; không dùng đường dẫn mẫu như một lệnh nguyên xi):

```powershell
git add -- backend/DatingApp.Api/Modules/DiscoveryMatching frontend/src/features/discovery-matching
git add -- backend/DatingApp.Api/Infrastructure/Data/AppDbContext.cs backend/DatingApp.Api/Program.cs frontend/src/app
git add -- tests/DatingApp.Api.Tests/DiscoveryMatchingAuthorizationTests.cs tests/DatingApp.Api.Tests/DiscoveryMatchingModelTests.cs
git diff --cached --stat
git diff --cached
git commit -m "feat(discovery): integrate with shared UUID and JWT foundation"
git push -u origin HEAD
```

Mở PR base main, mô tả nhánh/commit nguồn, file chung đã đổi, migration, dependency
còn thiếu và test thực tế. Không ghi hoàn thành nếu vẫn dùng demo/fallback.

Khi main thay đổi, ở nhánh module và sau khi đã commit/sao lưu công việc:

```powershell
git fetch origin
git merge --no-ff --no-commit origin/main
git diff --name-only --diff-filter=U
# Nếu có conflict: sửa từng vùng theo nghiệp vụ, git add file đã sửa, chạy lại test.
# Nếu merge đang mở và chưa xử lý được: git merge --abort.
# Nếu đã xử lý và kiểm tra đạt: git commit, rồi git push.
```

Không cần force push. Không dùng Accept All Current/Incoming cho Program,
DbContext, router, package hoặc snapshot. Build xanh chỉ là điều kiện đầu; phải
demo bằng tài khoản thật của nền Thiết và PostgreSQL test.

## 8. Quyền review trên main

CODEOWNERS chỉ định Thiết review Auth và file dùng chung; CI kiểm tra project
trùng ở gốc và snapshot EF sai vị trí. Để bắt buộc review/CI, chủ repo cần bật
rule cho main trên GitHub: yêu cầu PR, required checks, review thích hợp và chặn
force push. CODEOWNERS tự nó không cấm push; không tự ý hạ rule để merge nhanh.
PR do Thiết tạo cần một thành viên khác review nếu rule yêu cầu; tác giả không
thể tự phê duyệt PR của mình.
