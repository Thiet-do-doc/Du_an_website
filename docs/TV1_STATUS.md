# Trạng thái phần việc TV1

Đối chiếu mục 11 trong báo cáo phân công. Đây là trạng thái code và bằng chứng
kiểm thử local, không thay thế nghiệm thu của nhóm.

| Mã | Phần đã triển khai | Phụ thuộc / việc nghiệm thu còn lại |
|---|---|---|
| T1.1 | Bộ khung, health, README, API docs, Problem Details, CI build/test và PostgreSQL test job | CI GitHub chạy sau PR; chốt DTO với nhóm và review |
| T1.2 | Register, tuổi, email confirmation/resend, login, logout, forgot/reset; Identity/PostgreSQL migration; email adapter; các form React | SMTP production cần cấu hình dịch vụ thật |
| T1.3 | JWT 10 phút; refresh 7 ngày có hash/rotation/replay revocation; CurrentUser; User/Admin policy; CSRF/Secure cookie; tests quyền/token | Cấp Admin chỉ bằng lệnh vận hành; chưa chạy cấp quyền trên tài khoản thật |
| T1.4 | AuthProvider, route bảo vệ, khôi phục phiên, lỗi form, logout giữa các tab, refresh một lần | Chưa QA trực quan responsive/bàn phím vì không có browser kết nối |
| T1.5 | IAccountAccess kiểm tra phiên/trạng thái; khóa/thu hồi; Hub filter và registry; tests API/Hub filter | ChatHub và SafetyAdmin chưa được TV4/TV5 triển khai để nghiệm thu xuyên module |

Các việc học/phối hợp trong báo cáo vẫn cần con người thực hiện: hướng dẫn TV2,
review chéo, cải tiến UI Profiles khi module có code, deploy cùng TV5, demo nhánh
chung. Không sửa thay hoặc tạo mới nghiệp vụ của TV2/TV4/TV5 trong lần này.

## Các nhóm file thay đổi

- backend/DatingApp.Api/Program.cs, appsettings.json: DI, policies, JWT events,
  antiforgery, rate limit, DataProtection, SMTP config và lệnh grant-admin.
- Modules/Auth/Controllers/AuthController.cs: các endpoint tài khoản và kiểm tra quyền.
- Modules/Auth/DTOs/: LoginRequest/LoginResponse và AccountRequests.
- Modules/Auth/Entities/: ApplicationUser.IsActive và RefreshToken.
- Modules/Auth/Services/: AuthService, JwtSettings, CurrentUser, AccountAccess,
  AccountHubFilter, AuthEmailSender, AuthEmailExceptionHandler, ValidateAuthCsrfAttribute.
- Infrastructure/Data/AppDbContext.cs; migration AuthSessions và snapshot: PostgreSQL schema bổ sung.
- frontend/src/features/auth/: API client, AuthProvider, form/trang tài khoản.
- frontend/src/app/AppRouter.tsx, App.tsx, styles.css; main.tsx và index.html: routing,
  links, form layout và referrer policy.
- tests/DatingApp.Api.Tests/: JWT, PostgreSQL flow, Hub/CurrentUser tests.
- frontend/tests/auth-api.test.mjs và package.json: tests client bằng Node.
- .github/workflows/ci.yml: frontend tests và PostgreSQL integration tests.
- .gitignore: bỏ qua artifact/mailbox local.
- README.md, docs/API_CONTRACT.md, module README, docs/AUTH_TESTING.md,
  docs/Auth.postman_collection.json: hướng dẫn và hợp đồng bàn giao.

JwtBearer package và các DTO Login đã có thay đổi ở workspace từ phần việc trước;
không đổi PostgreSQL provider hoặc migration InitialIdentity.

## Bằng chứng

- dotnet build Release: 0 errors, 0 warnings.
- npm run build: đạt TypeScript và Vite build.
- Backend tests: có JWT, PostgreSQL thật, CSRF/rate limit, email transport recovery,
  khóa/role, reset, refresh concurrent/replay/expiry, logout, Hub filter và CurrentUser.
- Frontend tests: refresh đồng thời, không lặp refresh, lưu access token trong bộ nhớ,
  logout và dữ liệu lỗi form.
- API thật với email pickup: register 200; confirm-email 200; login 200; me 200;
  logout 204; JWT cũ sau logout 401.

Hướng dẫn chạy, thiết lập SMTP và kiểm thử: [AUTH_TESTING.md](AUTH_TESTING.md).
