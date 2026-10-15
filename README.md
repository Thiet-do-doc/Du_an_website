# Võ - T1.2: Identity + PostgreSQL Auth API starter

Bản khung cho phần đăng ký, kiểm tra đủ 18 tuổi, xác nhận email, đăng nhập, đăng xuất, quên mật khẩu và đặt lại mật khẩu.

## Lưu ý trước khi ghép vào repository nhóm
- Đây là starter độc lập, namespace `Vo_T1_2_AuthStarter`. Nếu repo đã có `Program.cs`, `DbContext` hoặc `ApplicationUser`, hãy ghép các phần tương ứng, không ghi đè mù quáng.
- Đăng nhập dùng cookie ASP.NET Core Identity để chạy thử T1.2. JWT/refresh-token thuộc T1.3 trong kế hoạch, chưa được triển khai trong gói này.
- `DevelopmentEmailSender` chỉ ghi nội dung email vào log để demo local. Không dùng trong production; thay bằng SMTP/provider thật.
- `appsettings.json` chỉ là cấu hình local mẫu. Không commit mật khẩu thật; nên chuyển connection string sang User Secrets hoặc biến môi trường.
- Cookie `Secure` cần HTTPS. Khi gọi từ React, dùng `credentials: 'include'` và cấu hình CSRF phù hợp trước khi triển khai.

## Chạy local
1. Cài .NET 10 SDK và PostgreSQL.
2. Sửa connection string local (không đẩy mật khẩu thật lên Git).
3. Trong terminal ở thư mục này:

```bash
dotnet restore
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations add InitialIdentity
dotnet ef database update
dotnet run
```

4. Mở Swagger URL được in ra terminal. Health check: `/api/v1/health`.
5. Sau khi đăng ký, xem terminal backend để lấy link xác nhận email demo. Liên kết sẽ gọi API trên `App:PublicBaseUrl`; hãy chỉnh URL cho đúng địa chỉ HTTPS đang chạy.

## API
- `POST /api/v1/auth/register` — `{ "email":"test@example.com", "password":"StrongPass123!", "dateOfBirth":"2000-01-01" }`
- `GET /api/v1/auth/confirm-email?userId=...&token=...`
- `POST /api/v1/auth/login` — `{ "email":"test@example.com", "password":"StrongPass123!" }`
- `POST /api/v1/auth/logout`
- `POST /api/v1/auth/forgot-password` — `{ "email":"test@example.com" }`
- `POST /api/v1/auth/reset-password` — `{ "email":"test@example.com", "token":"...", "newPassword":"NewStrongPass123!" }`

## Trước khi tạo PR
- [ ] Chạy `dotnet build` và sửa mọi lỗi.
- [ ] Kiểm thử email trùng, ngày sinh chưa đủ 18, mật khẩu yếu, email chưa xác nhận, đăng nhập sai 5 lần.
- [ ] Thử confirm/reset token hết hạn hoặc sai.
- [ ] Không commit `bin/`, `obj/`, mật khẩu, token hoặc email thật.
- [ ] Nếu repo nhóm đã có cấu trúc `backend/DatingApp.Api/`, chuyển code vào cấu trúc module nhóm đã chốt và nhờ TV1 review.
