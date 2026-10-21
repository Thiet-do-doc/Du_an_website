# Auth

TV1 chủ trì, TV2 dự phòng. Đã có Identity/PostgreSQL, đăng ký/xác nhận email,
đăng nhập, JWT, refresh rotation, logout, forgot/reset password, CurrentUser,
User/Admin policy, trạng thái tài khoản, rate limit, CSRF và giao diện React.

Hướng dẫn chạy, Postman, cấu hình email/production và hợp đồng TV4/TV5:
[docs/AUTH_TESTING.md](../../../../docs/AUTH_TESTING.md).

ChatHub và SafetyAdmin nghiệp vụ chưa có trong repo; Auth cung cấp IAccountAccess,
AccountHubFilter và registry cho module đó tích hợp. Quyền match/block/audit do
module sở hữu thực hiện. RefreshTokens và Identity do Auth quản lý.
