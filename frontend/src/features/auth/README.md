# Auth frontend

AuthProvider giữ access token trong bộ nhớ, phục hồi phiên bằng refresh cookie,
tuần tự hóa refresh, hỗ trợ logout giữa các tab. ProtectedRoute bảo vệ trang
/account và /admin; backend vẫn kiểm tra quyền thật.

Form: /register, /login, /confirm-email, /resend-confirmation, /forgot-password,
/reset-password. Thông báo lỗi dữ liệu/CSRF/phiên hiển thị từ API Problem Details.
Không lưu token vào localStorage. Vite proxy /api tới backend local.
Xem docs/AUTH_TESTING.md và chạy npm test để kiểm tra logic client.
