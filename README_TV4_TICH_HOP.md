# TV4 trong bộ khung main — bản ZIP tích hợp

Bộ mã nguồn này dựa trên snapshot `main` `f4b61ab2de7ec2148445cc508b32b7f07ea72163` được gửi trong cuộc trò chuyện, sau đó áp dụng source TV4 có chọn lọc. Đây là ZIP source, không chứa `.git` hoặc thông tin tài khoản GitHub.

## Đã ghép vào bộ khung
- Giữ `ApplicationUser<Guid>`, JWT bearer, refresh/AuthProvider và `AccountHubFilter` hiện có.
- Thêm ChatNotifications entities, controllers, services, ChatHub, API, UI, tests và tài liệu.
- Đăng ký `/hubs/chat` và route đã xác thực `/chat` mà không thay trang chủ `App.tsx`.
- Thêm `@microsoft/signalr` `^10.0.11`, lockfile và scripts `test:tv4`, `test:tv4-load`.
- Không sao chép thư mục `Demo`, migration `InitialTV4` hoặc snapshot cũ từ nhánh demo.

## Trạng thái tích hợp còn chờ
1. Bản main ZIP hiện chưa có model/service Matching thật của TV3. DI cho `IChatMatchAccess` hiện dùng adapter fail-closed, không tạo match giả.
2. Bản main ZIP hiện chưa có BlockPolicy Guid của TV5. DI cho `IChatBlockPolicy` fail-closed.
3. Chưa tạo migration `AddChatNotifications`; phải chờ TV3 Match entity và TV1 thống nhất thứ tự migration, sau đó review SQL/snapshot rồi mới áp dụng vào database thử riêng.
4. TV5 cần nối `IChatReportTargetAccess` vào endpoint report `Message` và chạy test quyền.
5. Cần chạy các build/test thực tế trên Windows với .NET 10, Node 24 và PostgreSQL 17. Các test tải 20 phiên cần lưu JSON thật; không có số đo được giả định trong ZIP này.

## Cách dùng source ZIP
- Giải nén vào thư mục mới và mở folder `Du_an_website-main` bằng VS Code. Không giải nén đè lên thư mục code cũ.
- Cấu hình PostgreSQL/Auth theo `README.md` và `docs/AUTH_TESTING.md`.
- Chạy `npm install` trong `frontend` lần đầu để xác thực/cập nhật lockfile theo registry của máy trước khi dùng `npm ci`.
- Chạy `dotnet restore DatingApp.slnx`, `dotnet test DatingApp.slnx`, `npm run build` và `npm run test:tv4` sau khi các dependency thật đã được tích hợp.
- Nếu muốn làm tiếp bằng Git, clone repository chính thức riêng rồi tạo nhánh `feature/tv4-chat-v2` từ `origin/main`; copy các file thay đổi có chọn lọc, kiểm tra `git diff`. ZIP không mang lịch sử Git và không nên được upload trực tiếp làm commit.
