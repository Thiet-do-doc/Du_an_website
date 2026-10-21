# DatingApp — bộ khung chung cho nhóm 5 người

Frontend **React + TypeScript + Vite**, backend **ASP.NET Core Web API (.NET 10)**,
database **PostgreSQL + EF Core**. Một repository và một backend chia module.

Nền chung dùng bộ khung và Auth của Thiết: `IdentityUser<Guid>`, JWT và refresh
token. Thành viên chuyển code từ các nhánh cũ theo
[hướng dẫn tích hợp từng nhánh](docs/TICH_HOP_NHANH_THANH_VIEN.md).
Chỉ chạy project trong `backend/DatingApp.Api` và `frontend`; các bản Auth/project
trùng ở gốc từ PR #2 đã được thay bằng Auth trong module, lịch sử Git vẫn giữ nguyên.

## Có sẵn và chưa có

**Có sẵn:** trang kiểm tra kết nối frontend/API/database; AppDbContext; cấu hình
PostgreSQL; cấu trúc module; OpenAPI JSON; CI build/test; kiểm thử hợp đồng health;
Compose chạy database local; hướng dẫn chạy và Git.

**Auth đã có:** Identity/PostgreSQL, register/confirm-email/login, JWT,
refresh/logout, forgot/reset-password, giao diện React và kiểm thử.
Xem [hướng dẫn Auth](docs/AUTH_TESTING.md) để chạy migration, lấy email local
và import Postman collection.

**TV4 đã được tích hợp ở mức source:** ChatHub, REST API hội thoại/lịch sử, thông báo, giao diện có xác thực tại `/chat`, chống gửi trùng và script kiểm thử. Phần nghiệp vụ Chat chưa được nghiệm thu toàn luồng: branch ZIP này chưa chứa Match model/service thật của TV3 hoặc BlockPolicy Guid của TV5 nên các API chat cố ý fail-closed cho đến khi adapter được tích hợp và migration được review. Không coi bản này là website hoàn chỉnh.

**Còn thiếu:** Profiles/upload, Matching thật, Block/report/admin thật và deploy production. Auth đã cung cấp policy và hợp đồng kiểm tra tài khoản cho các module đó.

## 1. Cài công cụ

- Git: https://git-scm.com/downloads
- VS Code: https://code.visualstudio.com/ — C# Dev Kit, ESLint, Prettier là extension có thể dùng.
- .NET **SDK 10 stable**: https://dotnet.microsoft.com/download/dotnet/10.0
- Node.js **24 LTS** cùng npm: https://nodejs.org/
- PostgreSQL **17** cài trực tiếp, hoặc Docker Desktop nếu WSL2 đã hoạt động.
- pgAdmin hoặc DBeaver để quản trị database; Postman để kiểm tra API.

Các gói project được pin/khóa trong csproj và package-lock.json. Cả nhóm cập nhật
phiên bản bằng PR chung sau khi kiểm tra tương thích, không nâng riêng từng máy.

## 2. Chạy nhanh frontend và API trước khi có database

Mở thư mục gốc chứa `DatingApp.slnx`, `frontend` và `backend` bằng VS Code.
Mọi lệnh sau chạy từ gốc project trừ khi có lệnh `cd`. Sau khi đăng nhập tài khoản Auth, route Chat TV4 đã tích hợp là `http://localhost:5173/chat`; route này cần database migration và adapter Match/Block thật trước khi nghiệp vụ chạy được.

**Terminal 1 — API**
```powershell
dotnet restore DatingApp.slnx
dotnet run --project backend/DatingApp.Api --launch-profile http
```
API: http://localhost:5080/api/v1/health

**Terminal 2 — frontend**
```powershell
cd frontend
npm ci
npm run dev
```
Mở http://localhost:5173 rồi bấm **Kiểm tra kết nối**.
Backend phải báo đang hoạt động; PostgreSQL báo chưa cấu hình là đúng ở bước này.
Nếu PowerShell chặn npm.ps1, thay `npm` bằng `npm.cmd`, không cần đổi policy hệ thống.

## 3. Kết nối PostgreSQL

Chọn **một** trong hai cách. Không cần cài cả hai.

### Cách A — Docker

Tại thư mục gốc, copy cấu hình mẫu:
```powershell
Copy-Item .env.example .env
```
Mở `.env`, điền mật khẩu local của bạn vào `POSTGRES_PASSWORD` rồi chạy:
```powershell
docker compose up -d postgres
docker compose ps
```
Thông số: host `127.0.0.1`, port **5433**, database `datingapp`, user `datingapp`.
Mật khẩu là giá trị bạn vừa đặt. `.env` chỉ được Compose đọc, ASP.NET không tự đọc file này.
Dữ liệu giữ trong volume. `docker compose down` dừng dịch vụ; không thêm `-v`
nếu cần giữ dữ liệu. Đổi POSTGRES_PASSWORD sau khi có volume không tự đổi mật khẩu DB.

### Cách B — PostgreSQL cài trực tiếp

Cài PostgreSQL 17 từ https://www.postgresql.org/download/windows/ rồi mở pgAdmin.
Tạo database `datingapp`. Có thể dùng user local đã tạo khi cài PostgreSQL;
port thường là **5432**. Mỗi người dùng database local của mình.

### Cấu hình API bằng user-secrets

Dừng API bằng Ctrl+C. Chạy ở thư mục gốc, thay mật khẩu, user và port cho đúng:
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=datingapp;Username=datingapp;Password=THAY_MAT_KHAU_LOCAL;Timeout=4;Command Timeout=4" --project backend/DatingApp.Api
dotnet run --project backend/DatingApp.Api --launch-profile http
```
Với bản native, thường đổi `Port=5432` và `Username` theo tài khoản của bạn.
`THAY_MAT_KHAU_LOCAL` chỉ là chỗ điền, không phải mật khẩu có sẵn.
Không chụp hoặc chia sẻ terminal chứa mật khẩu; dùng mật khẩu dev riêng, không dùng lại
mật khẩu tài khoản cá nhân. User-secrets nằm ngoài repo nhưng không phải kho mã hóa.
Nếu mật khẩu có dấu `;`, dùng cú pháp trích dẫn của Npgsql cho giá trị password.

Quay lại React, bấm kiểm tra. Cả API và database phải báo thành công.
Endpoint database kiểm tra kết nối thật; không tạo bảng và không kiểm chứng migration.

## 4. Cấu trúc cần biết

| Thư mục/file | Chức năng |
|---|---|
| `frontend/src/app/` | Màn hình khung và CSS hiện tại; nơi nối router/auth sau này |
| `frontend/src/features/` | Auth, profiles, discovery-matching, chat-notifications, safety-admin, posts |
| `frontend/src/shared/` | HTTP helper và components/hooks dùng chung |
| `backend/DatingApp.Api/Modules/` | Các module nghiệp vụ và SystemHealth đang chạy |
| `backend/DatingApp.Api/Infrastructure/` | EF DbContext, probe PostgreSQL, nơi thêm media/email/migration |
| `backend/DatingApp.Api/Shared/` | Hợp đồng dùng chung |
| `tests/DatingApp.Api.Tests/` | xUnit kiểm tra hợp đồng endpoint health |
| `docs/` | Phân công, API, Git và xử lý lỗi |
| `infra/` | Ghi chú để bổ sung deployment sau |
| `.github/` | CI, template Issue và PR |
| `compose.yml` | Chạy PostgreSQL local, chưa chạy frontend/API |

`.gitkeep` giúp Git giữ thư mục chưa có code. Khi có file thật, có thể xóa `.gitkeep`.

## 5. Build và test trước khi gửi PR

Từ gốc:
```powershell
dotnet build DatingApp.slnx
dotnet test DatingApp.slnx
cd frontend
npm run build
```
CI chạy các kiểm tra tương tự khi push main hoặc mở PR.
Health tests dùng probe giả có kiểm soát để test HTTP 200/503.
AuthFlowTests dùng PostgreSQL thật khi đặt AUTH_TEST_CONNECTION; CI có PostgreSQL
test riêng. Xem docs/AUTH_TESTING.md để chạy các luồng tài khoản.

## 6. Công việc tiếp theo

Auth đã có trong nền chung. TV2 bổ sung Profiles/media; TV3, TV4 và TV5 cập nhật
module theo UUID, JWT và service tài khoản hiện tại. Các phần này cần PR và kiểm
thử riêng trước khi có thể chạy toàn luồng. Xem hướng dẫn tích hợp ở trên.

Local EF tool đã khai báo trong `.config/dotnet-tools.json`:
```powershell
dotnet tool restore
```
Chỉ sau khi đã thêm entity và chốt schema mới tạo migration:
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef migrations add InitialIdentity --project backend/DatingApp.Api --output-dir Infrastructure/Migrations
dotnet ef database update --project backend/DatingApp.Api
```
Không chạy tạo migration rỗng ngay khi nhận bộ khung. Phải có connection string
local; biến `ASPNETCORE_ENVIRONMENT` ở trên cho phép EF CLI nạp user-secrets.

## 7. Đẩy lên repository của nhóm

Đọc **[docs/GIT_WORKFLOW.md](docs/GIT_WORKFLOW.md)**. Nên clone repo đã có rồi copy
nội dung bộ khung vào repo đó, giữ `.git`, tạo nhánh và gửi PR. Không upload file ZIP.
Đọc **[docs/PHAN_CONG.md](docs/PHAN_CONG.md)** để nhận module và người review.

## Tài liệu kỹ thuật
- https://vite.dev/guide/
- https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0
- https://www.npgsql.org/efcore/
- https://learn.microsoft.com/dotnet/core/tools/global-json
