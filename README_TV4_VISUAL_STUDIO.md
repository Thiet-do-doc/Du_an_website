# HƯỚNG DẪN TV4 — chạy ngay trên Visual Studio (Windows)

Đây là **bản bổ sung vào repository Du_an_website-main của nhóm** chứ không phải một project riêng. Trang chủ môi trường được giữ nguyên; chức năng TV4 truy cập ở `http://localhost:5173/?view=tv4`.

## 1. Phạm vi code hiện có (T4.1–T4.5)

- **T4.1**: entity và EF configuration cho `Conversations`, `Messages`, `Notifications`; API lịch sử có cursor (SQL keyset pagination) và phân trang thông báo.
- **T4.2**: `ChatHub` có `[Authorize]`, kiểm tra thành viên, match, chặn và trạng thái user tại thao tác gửi. Demo permissions trong `DemoChatPermissions`.
- **T4.3**: lưu DB trước khi phát `MessageCreated`; `clientMessageId` + unique index chống gửi trùng; trả xác nhận cho người gửi.
- **T4.4**: React chat, responsive, lịch sử cũ, trạng thái gửi/thất bại/thử lại, tự nối lại và lấy tin bị lỡ bằng `afterId`.
- **T4.5**: thông báo match/tin mới (lưu DB), đánh dấu đã đọc, `NotificationCreated` realtime.

### TRẠNG THÁI QUAN TRỌNG
- **Đã viết** mã nguồn module, giao diện React, rule tests và smoke test để bạn chạy thử.
- **Chưa kiểm chứng bằng cách thực thi**: backend build/test (môi trường tạo gói không có .NET SDK); frontend build (npm registry không tải được). Bạn phải chạy lệnh build/test bên dưới để xác nhận kết quả.
- **Chưa có migration chính thức** trong gói vì nhóm phải tạo/review migration trên shared AppDbContext bằng `dotnet ef` một lần, tránh xung đột với TV1.
- **Chưa ghép production** với Auth (TV1), Matching (TV3), Block (TV5): hiện dùng ba bảng `Demo*` chỉ để chạy thử trên máy local. Không triển khai bản này cho người dùng thật.
- **package-lock.json chưa cập nhật cho @microsoft/signalr** do môi trường đóng gói không truy cập được npm; `npm install` lần đầu sẽ cập nhật lock, hãy commit phần thay đổi này cùng PR. Đừng dùng `npm ci` trước khi lock được cập nhật.

## 2. Giải nén và mở đúng dự án

1. Giải nén ZIP vào thư mục riêng, ví dụ `C:\Users\ADMIN\source\Du_an_website-main`.
2. Mở **Visual Studio 2022/2026 hỗ trợ .NET 10** → **Open a project or solution** → chọn `DatingApp.slnx`. Nếu phiên bản Visual Studio cũ không đọc `.slnx`, mở trực tiếp `backend/DatingApp.Api/DatingApp.Api.csproj`.
3. Đảm bảo đã cài **.NET 10 SDK**, Node.js 24 LTS, Git, PostgreSQL 17 (Docker hoặc native). Mở **View → Terminal**, chạy:

```powershell
dotnet --version
node -v
npm -v
git --version
```

## 3. PostgreSQL 17 (Docker)

Ở thư mục gốc chứa `compose.yml`:

```powershell
Copy-Item .env.example .env
```

Mở `.env` và **tự chọn** `POSTGRES_PASSWORD` khác mật khẩu tài khoản cá nhân. Chạy:

```powershell
docker compose up -d postgres
docker compose ps
```

Database `datingapp`, host `127.0.0.1`, port **5433**, user `datingapp`. Nếu máy đã cài PostgreSQL native, dùng pgAdmin và cập nhật user/port (thường 5432). Không commit `.env`.

## 4. Cấu hình API và migration

**Tại thư mục gốc** (không phải thư mục frontend), thay `MAT_KHAU_DATABASE_LOCAL` bằng mật khẩu bạn đã chọn, sau đó chạy:

```powershell
dotnet restore DatingApp.slnx
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=datingapp;Username=datingapp;Password=MAT_KHAU_DATABASE_LOCAL" --project backend/DatingApp.Api
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef migrations add InitialTV4 --project backend/DatingApp.Api --output-dir Infrastructure/Migrations
dotnet ef database update --project backend/DatingApp.Api
dotnet run --project backend/DatingApp.Api --launch-profile http
```

Quan trọng: **chỉ tạo migration khi TV1 đã chốt quyền sở hữu migration**. Nếu nhóm đã có migration, bỏ `migrations add` và xem lại model diff trước khi update. Ở local demo với schema rỗng hiện tại, tạo `InitialTV4` một lần rồi chạy update. Không dùng `EnsureCreated`.

Ở môi trường Development, nếu chưa cấu hình `Jwt:Key`, chương trình tạo khóa JWT ngẫu nhiên trong RAM khi khởi động; sau khi restart, đăng nhập giả lại từ đầu. Khi làm với TV1 và triển khai thật phải cấu hình khóa bí mật qua nơi quản lý secret.

Backend: `http://localhost:5080/api/v1/health`; DB readiness: `http://localhost:5080/api/v1/health/database`.

> Nếu app khởi động báo `relation "DemoUsers" does not exist`, là bạn **chưa áp dụng migration** vào đúng database. Nếu API kiểm tra kết nối được mà UI chưa chat, đừng nhầm health check với nghiệp vụ đã hoàn chỉnh.

## 5. Frontend trong Visual Studio Terminal

Mở terminal **thứ hai**, trở về thư mục gốc project, chạy:

```powershell
cd frontend
npm install
npm run build
npm run dev
```

Nếu PowerShell chặn `npm.ps1`, thay `npm` thành `npm.cmd`. Giao diện ở `http://localhost:5173` và bấm **Mở giao diện TV4**, hoặc mở trực tiếp `http://localhost:5173/?view=tv4`.

**Điểm khác so với bản ZIP mẫu cũ**: đúng port của repo nhóm là **5080/5433**, không phải 5062/5432. Proxy Vite đã có sẵn cả `/api` và `/hubs`.

## 6. Demo hai trình duyệt A/B/C

1. Chrome thường chọn A; cửa sổ ẩn danh chọn B. Match A/B được seed sẵn từ local demo; C chưa match.
2. A gửi tin; B nhận ngay không tải lại; B gửi lại; A nhận ngay.
3. F5 cả hai: tin vẫn còn vì DB đã lưu.
4. Network → Offline ở B, A gửi tin, B Online: reconnect/catch-up phải thấy tin đã lỡ.
5. A và B có thông báo match/tin mới; đánh dấu đọc và F5 xem trạng thái còn lưu.
6. Mở C, thử truy cập ID chat A/B bằng token C: API phải trả 404, Hub phải từ chối.
7. Thử gửi trùng cùng clientMessageId, block, unmatch và tài khoản bị khóa.

## 7. Build, test và PR

Backend tests từ root:

```powershell
dotnet build DatingApp.slnx
dotnet test DatingApp.slnx
```

Frontend compile và integration smoke (cần backend+DB đang chạy):

```powershell
cd frontend
npm run build
npm run test:tv4
```

`test:tv4` dùng Development endpoints và có thể thay đổi dữ liệu block/lock demo; chạy **chỉ trên database thử**. Chưa có kết quả đo độ trễ <2 giây; bạn cần tự đo và ghi máy/mạng/test case trong báo cáo.

Tạo nhánh `feat/T4-chat-notifications` trên repo chung, không push thẳng `main`:

```powershell
git switch main
git pull --ff-only
git switch -c feat/T4-chat-notifications
git add backend/DatingApp.Api frontend tests docs
git commit -m "feat(tv4): implement chat notifications and realtime demo"
git push -u origin feat/T4-chat-notifications
```

**Đề nghị review**: TV1 xem JWT/Hub; TV3 xem EnsureConversation + match transaction; TV5 xem block/unmatch; một người được phân công merge migration.

## 8. Sau khi TV1/TV3/TV5 hoàn tất

- TV1: thay token `GET /api/v1/dev/token/{alias}` bằng JWT đăng nhập chính thức; lấy userId từ claims, không nhận userId tùy ý từ client.
- TV3: thay `DemoMatch` bằng entity `Match`; gọi `MatchChatBridge.EnsureConversationAsync(matchId)` trong transaction tạo match, phát event **sau commit**.
- TV5: thay `DemoBlock` bằng `BlockPolicy` và trạng thái tài khoản thật; kiểm tra quyền **mỗi lần gửi**, không tin group SignalR.
- Gỡ **toàn bộ** `Demo/`, `DemoUsers`, `DemoMatches`, `DemoBlocks` và các route `/api/v1/dev/*` khỏi bản production; thay quan hệ khóa ngoại với entity thật và tạo migration được review.
- Chỉ kiểm thử được và ghi kết quả thực tế mới đánh dấu nhiệm vụ xong. Xem `docs/TV4_KIEM_THU.md`, `docs/TV4_VAN_DAP_TV4.md` và `docs/TV4_API_VA_TICH_HOP.md`.
