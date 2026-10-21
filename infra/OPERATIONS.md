# Khung vận hành #21

## Biến cấu hình

Copy `.env.example` sang `.env` (đã ignore), tự điền secret. Compose đọc `.env`;
`dotnet run` trên host dùng user-secrets hoặc biến môi trường, không tự đọc `.env`.

| Compose | ASP.NET / mục đích |
| --- | --- |
| POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD | ConnectionStrings__DefaultConnection; API trong Compose dùng host postgres:5432 |
| POSTGRES_PORT | Port DB local, mặc định 5433, chỉ bind 127.0.0.1 |
| JWT_KEY | Jwt__Key, secret ngẫu nhiên ít nhất 32 byte |
| — | Jwt__Issuer=DatingApp.Api; Jwt__Audience=DatingApp.Client; Jwt__ExpireMinutes=10 |
| AUTH_FRONTEND_URL | Auth__FrontendUrl, URL HTTPS công khai, dùng link email |
| EMAIL_FROM | Email__From |
| SMTP_HOST, SMTP_PORT | Email__SmtpHost, Email__SmtpPort (587 mặc định, TLS) |
| SMTP_USERNAME, SMTP_PASSWORD | Email__Username, Email__Password |
| STAGING_PORT | Nginx bind 127.0.0.1:8080 mặc định |
| — | DataProtection__KeyPath=/var/lib/datingapp/keys; volume staging_auth_keys |

Development dùng Email:Delivery=Pickup theo README Auth; production bắt buộc SMTP.
Không đưa JWT key, mật khẩu, access/refresh token hoặc dữ liệu thật vào Git/log/ảnh demo.
Giá trị mật khẩu CI chỉ cho database dùng tạm của runner, không dùng cho môi trường khác.

## Chạy và lưu dữ liệu

Local: `docker compose up -d postgres`. Database volume `datingapp_postgres_data`.
Khung staging: `docker compose -f compose.staging.yml up -d --build` sau khi điền đủ .env.
Database dùng `staging_postgres_data`, không expose port; auth keys có volume riêng.
Áp migration bằng EF CLI có quyền truy cập database trước khi phục vụ request nghiệp vụ.
Không tự migrate mỗi API replica. Xem thứ tự migration trong docs/ISSUE_21_HANDOFF.md.
`docker compose down` giữ volume; `down -v` xóa dữ liệu, chỉ dùng DB test bỏ được.
Thay POSTGRES_PASSWORD trong .env không đổi password của database đã có volume.
Backup/restore phải được thử trên DB riêng trước khi dùng môi trường chung.

## Checklist Nginx / HTTPS

- Khung `infra/nginx.conf` phục vụ SPA, proxy /api và nâng cấp WebSocket /hubs.
- Giữ Authorization header, không cache response auth; không ghi token query vào access log Hub.
- Đặt HTTPS/TLS ở reverse proxy ngoài, certificate và chuyển HTTP sang HTTPS.
- Cookie refresh/CSRF production có Secure: browser phải truy cập HTTPS để đăng nhập.
- Truyền Host và X-Forwarded-Proto chính xác; chỉ tin proxy đã khai báo, không bật tin mọi IP.
- Chọn domain cùng origin cho UI/API để dùng cookie/CSRF; không mở CORS wildcard credentials.
- Giới hạn kích thước upload, timeout/WebSocket theo module Profiles/Chat; kiểm tra health API và DB.
- Không expose PostgreSQL/API ra Internet; frontend staging chỉ bind loopback để proxy ngoài truy cập.

Đây là khung cấu hình, chưa xác nhận VPS/TLS/SMTP/staging thực tế. Cần kiểm tra
`docker compose config --quiet`, build image, đăng nhập HTTPS và WebSocket trên môi trường triển khai.

## Rà secret trước PR

`git diff --check`; xem kỹ `git diff --cached`; `.env`, key/cert, uploads, DB, mail pickup
phải được ignore. Không dán connection string thật vào PR. Dùng secret scanning của nhóm
cho toàn bộ lịch sử Git; việc rà mã hiện tại không bảo đảm lịch sử chưa từng lộ secret.
