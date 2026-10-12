# Xử lý lỗi thường gặp

| Hiện tượng | Cách kiểm tra |
|---|---|
| `dotnet` không nhận | Cài .NET **SDK** 10, đóng/mở terminal, chạy `dotnet --info` |
| global.json yêu cầu SDK | Cần SDK stable 10.x từ 10.0.100 trở lên; không dùng riêng Runtime hoặc SDK 9 |
| `npm.ps1 cannot be loaded` | Dùng `npm.cmd ci`, `npm.cmd run dev` trong PowerShell hoặc dùng terminal CMD |
| Vite báo port 5173 đã dùng | Dừng terminal frontend cũ; không chạy hai bản cùng cổng |
| API lỗi cổng 5080 | Dừng API cũ; nếu đổi cổng, cập nhật frontend `.env.local` theo `.env.example` |
| Giao diện mở được nhưng backend lỗi | Chạy API terminal riêng và thử `http://localhost:5080/api/v1/health` |
| Database `not_configured` | Chưa chạy `dotnet user-secrets set`; không phải lỗi frontend |
| Database `unavailable` | Kiểm tra service/container, port 5433 hoặc 5432, database, user và password |
| Docker chưa chạy do WSL2 | Chạy native PostgreSQL như README để tiếp tục; sửa WSL2 riêng |
| Đổi mật khẩu trong .env nhưng DB vẫn dùng mật khẩu cũ | PostgreSQL khởi tạo user trên volume lần đầu; sửa .env không đổi user đã tồn tại. Đổi mật khẩu bằng quản trị DB, không xóa volume chứa dữ liệu |
| `/swagger` trả 404 | Bộ khung chỉ có `/openapi/v1.json`, chưa cài Swagger UI |
| `dotnet ef database update` chưa tạo bảng | Chưa có entity/migration ở bộ khung; TV1 chốt Identity trước |

Backend mặc định chạy local HTTP và lắng nghe localhost. Chưa phải môi trường deploy
public. HTTPS, cookie Secure, Nginx, quyền và giới hạn tần suất phải được hoàn thiện
trước khi đưa ứng dụng có dữ liệu người dùng lên mạng.
