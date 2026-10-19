# Authentication của thành viên 1

## Chạy local

Backend giữ ASP.NET Core Identity, EF Core/Npgsql và PostgreSQL. Branch triển khai:
`feature/auth-foundation`. Không reset database. Migration `AuthSessions` thêm
`AspNetUsers.IsActive` (mặc định true cho tài khoản cũ) và bảng `RefreshTokens`.
Migration này đã áp dụng trên máy hiện tại. Máy khác chạy từ gốc repo:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet tool restore
dotnet ef database update --project backend/DatingApp.Api
dotnet run --project backend/DatingApp.Api --launch-profile http
```

Trong terminal khác:

```powershell
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run dev
```

Backend: `http://localhost:5080`; frontend: `http://localhost:5173`.
Dừng phiên API cũ trước khi build lại nếu file bin bị khóa.
User Secrets đã có connection string và Jwt:Key trên máy hiện tại. Máy khác dùng
hướng dẫn kết nối PostgreSQL trong README và tạo khóa riêng:

```powershell
$bytes = New-Object byte[] 48
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($bytes)) --project backend/DatingApp.Api
```

## Luồng sử dụng

1. Mở `/register`: email, mật khẩu, nhập lại mật khẩu, ngày sinh đủ 18 tuổi.
2. Development lưu email `.eml` vào `.artifacts/mail` ở gốc repository, không gửi
   tới hộp thư bên ngoài. Mở `.eml` bằng ứng dụng email để lấy liên kết xác nhận.
   Nếu email gửi thất bại, tài khoản vẫn tồn tại; dùng `/resend-confirmation`.
3. Mở liên kết `/confirm-email?...`, bấm xác nhận. Liên kết sống 1 giờ và dùng một lần.
4. Mở `/login`. Tài khoản chưa xác nhận hoặc bị khóa không đăng nhập được.
5. Mở `/account`, tải lại trang: AuthProvider khôi phục phiên bằng refresh cookie.
6. `/forgot-password` gửi liên kết `/reset-password?...`. Reset thành công thu hồi
   mọi phiên của tài khoản; token reset cũ không sử dụng lại được.
7. Đăng xuất gọi máy chủ để thu hồi phiên trước khi xóa trạng thái UI. Các tab
   cùng origin nhận thông báo logout; trang được bảo vệ yêu cầu đăng nhập lại.

Nếu không có ứng dụng mở `.eml`, đọc nội dung email mới nhất bằng lệnh:

```powershell
powershell -File scripts/Read-DevMail.ps1
```

Lệnh chỉ đọc mailbox local và in liên kết. Không chia sẻ liên kết xác nhận/reset.

Không lưu access/refresh token vào localStorage. Access token sống 10 phút trong
bộ nhớ; refresh cookie HttpOnly sống tối đa 7 ngày cho một phiên, không kéo dài
hạn tuyệt đối khi rotation. Refresh token trong DB chỉ lưu SHA-256 hash, giá trị
ngẫu nhiên 48 byte; mỗi lần refresh đổi token. Tái sử dụng token đã đổi thu hồi
cả phiên. Các tab có Web Locks tuần tự hóa refresh; trình duyệt không hỗ trợ
Web Locks có thể phải đăng nhập lại khi hai tab cùng refresh một token.

## API và Postman

Import `docs/Auth.postman_collection.json`. Đặt `baseUrl`, `email`, `password`.
Collection tự lấy CSRF token cho login/refresh/logout và lưu JWT sau login/refresh.
Postman Cookie Jar phải bật. Không thêm Bearer header cho các endpoint cookie;
chỉ thêm cho `/me` và `/admin-check`. Liên kết email chứa `userId`, `token` hoặc
`email`, `token`; điền token đó vào biến collection để thử confirm/reset.

| Method | Endpoint /api/v1/auth/ | Body / điều kiện |
|---|---|---|
| GET | csrf | Trả request token; lưu cookie CSRF |
| POST | register | email, password, confirmPassword, dateOfBirth |
| POST | resend-confirmation | email; phản hồi chung tránh lộ tài khoản |
| POST | confirm-email | userId (UUID), token từ email |
| POST | login | email, password; X-CSRF-TOKEN + cookie CSRF |
| POST | refresh | Body rỗng; X-CSRF-TOKEN + cookie CSRF/refresh |
| POST | logout | Như refresh; 204 và xóa cookie |
| POST | forgot-password | email; phản hồi chung |
| POST | reset-password | email, token, password, confirmPassword |
| GET | me | Bearer; userId, email và roles hiện tại |
| GET | admin-check | Bearer của Admin; User thường nhận 403 |

JSON camelCase; UUID; thời gian UTC. Lỗi dùng Problem Details gồm title, status,
traceId; validation có errors. Login sai/trạng thái không phù hợp: 401; CSRF sai:
400; gửi email lỗi SMTP: 503 và hướng dẫn thử lại. Endpoint nhạy cảm giới hạn
30 request/phút/IP; sai mật khẩu 5 lần khóa đăng nhập 15 phút. Đằng sau reverse
proxy cần cấu hình forwarded headers chỉ từ proxy tin cậy để IP được chính xác.

## Bàn giao TV4 và TV5

`ICurrentUser` cung cấp UserId, Email, IsAdmin từ claims của request. Chỉ tin
UserId từ dịch vụ này khi thao tác dữ liệu. Dùng `[Authorize(Policy = "User")]`
hoặc `[Authorize(Policy = "Admin")]`; User policy yêu cầu phiên hợp lệ, Admin
policy còn yêu cầu role Admin. Vai trò được lấy lại từ DB cho từng request.

TV5 gọi `IAccountAccess.SetActiveAsync(userId, false)` trong nghiệp vụ quản trị
đã kiểm tra quyền và ghi audit: đổi trạng thái/stamp, thu hồi refresh tokens,
ngắt kết nối SignalR đang đăng ký. Mở khóa không khôi phục phiên cũ. Không cập
nhật IsActive trực tiếp trong module khác. API khóa/audit thuộc SafetyAdmin,
không tạo API quản trị trùng ở Auth.

TV4 dùng Hub có `[Authorize(Policy = "User")]`, map đường dẫn dưới `/hubs`:

```csharp
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true);
```

`AccountHubFilter` đã đăng ký toàn cục: kiểm tra trạng thái, session, stamp và
hạn access token khi kết nối/mỗi invocation. Registry hỗ trợ ngắt kết nối khi
khóa/reset/logout. Query access_token chỉ nhận dưới /hubs để SignalR dùng.
TV4 vẫn phải kiểm tra match, block và quyền đọc/gửi ở từng nghiệp vụ; không log
query chứa access_token. Bộ registry hiện dành cho một backend như kế hoạch;
nhiều instance cần cơ chế phát sự kiện thu hồi giữa các instance.

Repo hiện chưa có ChatHub hoặc API SafetyAdmin thật. Hợp đồng/filter có kiểm thử,
nhưng nghiệm thu chat–khóa xuyên module chờ TV4/TV5 triển khai module của họ.

## Cấp Admin và production

Không có trường role trong Register và không có endpoint tự nâng quyền.
Chủ vận hành chạy lệnh có kiểm soát cho tài khoản đã xác nhận và đang hoạt động:

```powershell
dotnet run --project backend/DatingApp.Api --launch-profile http -- --grant-admin="admin@example.com"
```

Lệnh kết thúc sau khi cấp quyền, không tự tạo tài khoản hay mật khẩu admin.
Không chạy lệnh này cho tài khoản người dùng thông thường.

Production: cấu hình `Auth:FrontendUrl` HTTPS, `Email:SmtpHost`, `Email:SmtpPort`
(mặc định 587), `Email:From`, `Email:Username`, `Email:Password`, connection string
và Jwt:Key qua biến môi trường/secrets. SMTP dùng TLS. Cookie refresh/CSRF Secure,
SameSite Strict. Phục vụ frontend/API cùng origin qua proxy. Cấu hình
`DataProtection:KeyPath` tới volume bền vững, phân quyền và bảo vệ khóa; nếu mất
khóa thì CSRF/link xác nhận/reset cũ không dùng được. Không đưa secrets vào Git.

## Kiểm thử và phạm vi nghiệm thu

```powershell
dotnet build DatingApp.slnx --configuration Release
dotnet test DatingApp.slnx --configuration Release
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend test
```

Kiểm thử PostgreSQL thật cần AUTH_TEST_CONNECTION là connection string của DB
local/test đã áp dụng migrations. Test tạo tài khoản giả riêng, không xóa dữ liệu
có sẵn; không dùng database production. Nếu thiếu biến này, các PostgreSQL tests
được đánh dấu skipped, không được coi là bằng chứng DB thật trong CI.

```powershell
# Điền kết nối DB test riêng, không commit hoặc chia sẻ terminal chứa mật khẩu.
$env:AUTH_TEST_CONNECTION="Host=127.0.0.1;Port=5432;Database=datingapp_test;Username=...;Password=..."
dotnet test DatingApp.slnx --configuration Release
Remove-Item Env:AUTH_TEST_CONNECTION
```

Đã kiểm tra tự động: JWT; role; session; xác nhận/reset một lần và hết hạn;
refresh rotation/replay/đồng thời/hết hạn; logout; khóa tài khoản; tuổi/email;
CSRF; rate limit; Hub filter/registry; logic refresh/logout frontend.
UI build được, nhưng chưa kiểm tra trực quan trên trình duyệt vì môi trường
không có browser kết nối. SMTP production chưa gửi thử vì chưa có dịch vụ SMTP.

Các bước ngoài code vẫn cần nhóm thực hiện: PR/review của người khác, demo nhánh
chung/staging, cải tiến UI Profiles cùng TV2, hướng dẫn chéo và thực hành deploy.
Tài liệu này không đánh dấu các việc đó đã hoàn thành thay con người.

Tham khảo thiết kế: [JWT Bearer của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0),
[CSRF của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery).
