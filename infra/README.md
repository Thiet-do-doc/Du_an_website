# Triển khai sau khi P0 chạy ổn
`compose.yml` ở thư mục gốc chỉ chạy PostgreSQL local, chưa phải cấu hình production.

TV5 + TV1 bổ sung ở đây: Dockerfile API/frontend, Nginx, HTTPS, secrets,
health check, backup và restore. Production không dùng Vite dev server.
Nginx sẽ phục vụ frontend tĩnh và proxy `/api`, `/hubs` đến API cùng origin.
Trước khi mở public: hoàn thành auth, quyền, rate limit, log không chứa secret
và kiểm thử nghiệp vụ. Chưa mở SignalR Hub trong bộ khung.
