# Kế hoạch và biên bản kiểm thử TV4 (điền kết quả thật)

**Nguyên tắc:** bảng dưới là **kịch bản cần chạy**, không phải kết quả đã đạt. Chỉ điền PASS khi đã chạy trên máy thật. Test PostgreSQL; không coi mock in-memory là bằng chứng.

| ID | Nhiệm vụ | Thực hiện | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|---|
| CHAT-01 | T4.2/T4.4 | Mở A Chrome, B cửa sổ ẩn danh, A gửi "hello" | B thấy ngay không F5 | Chưa chạy |
| CHAT-02 | T4.1 | F5 hai phía sau CHAT-01 | Lịch sử vẫn còn | Chưa chạy |
| CHAT-03 | T4.1 | Gửi hơn 30 tin, bấm "Tải tin nhắn cũ" | Sắp xếp đúng, không thiếu/trùng | Chưa chạy |
| CHAT-04 | T4.2 | C đọc GET messages của A/B | API 404/403, không lộ nội dung | Chưa chạy |
| CHAT-05 | T4.2 | C gọi `JoinConversation` Hub của A/B | Bị từ chối | Chưa chạy |
| CHAT-06 | T4.2 | C gọi `SendMessage` vào A/B | Bị từ chối | Chưa chạy |
| CHAT-07 | T4.3 | Gửi 2 lần cùng clientMessageId + cùng nội dung | Một message và một notification | Chưa chạy |
| CHAT-08 | T4.3 | Dùng lại clientMessageId với nội dung khác | Báo xung đột, không lưu sai | Chưa chạy |
| CHAT-09 | T4.4 | Cho B Offline, A gửi, B Online | B nhận bù từ DB qua `afterId` | Chưa chạy |
| CHAT-10 | T4.2 | A chặn B khi B vẫn có Hub | Cả hai không gửi mới được | Chưa chạy |
| CHAT-11 | T4.2 | Bị khóa tài khoản, thử gọi Hub/tải API | Bị từ chối theo chính sách tích hợp | Chưa chạy |
| CHAT-12 | T4.2 | Unmatch A/B, thử gửi | Chỉ đọc, không gửi được | Chưa chạy |
| CHAT-13 | T4.5 | Có match mới, theo dõi MatchCreated | Có notification DB + realtime | Chưa chạy |
| CHAT-14 | T4.5 | B đánh dấu thông báo đã đọc | `readAt` được lưu, A/C không sửa của B | Chưa chạy |
| CHAT-15 | T4.4 | Màn hình rộng 360 px/desktop | Không tràn ngang; gửi được bằng bàn phím | Chưa chạy |
| CHAT-16 | T4.4 | Nội dung nhập có ký tự `<script>` | Chỉ hiển thị như văn bản, không chạy HTML | Chưa chạy |
| CHAT-17 | T4.3 | Gửi chuỗi rỗng, >2000 ký tự, clientMessageId rỗng | Từ chối, không lưu | Chưa chạy |
| CHAT-18 | T4.1 | Tắt/bật lại API và DB | Nội dung đã commit không mất | Chưa chạy |

## Chạy kiểm thử tự động

Tại thư mục `TV4_DatingApp` (PowerShell):

```powershell
# Unit tests (không cần PostgreSQL)
dotnet test tests/DatingApp.Tests/DatingApp.Tests.csproj

# Integration demo (cần backend Development + PostgreSQL + npm install)
cd frontend
npm run test:tv4
```

Script `test:smoke` kiểm tra A/B/C, SignalR, gửi trùng, thông báo, block, khóa và lấy tin sau offline. Nếu lỗi tại một bước, nó in `FAIL` để bạn chụp và sửa; không tự mặc định PASS.

## Mẫu ghi nhận kết quả khi demo

- Ngày kiểm thử: ______
- Người thực hiện: ______
- Nhánh/commit: ______
- Windows/CPU/RAM: ______
- .NET SDK/PostgreSQL/Node.js: ______
- Kết quả `dotnet test`: ______ tests passed, ______ failed
- Kết quả smoke test: PASS / FAIL (đính kèm log)
- Thử thời gian nhận tin: số lần ______, độ trễ từng lần ______ ms; trung bình ______ ms. Mục tiêu kế hoạch dưới 2 giây, **phải đo chứ không tự ghi đạt**.
- Lỗi còn tồn tại, cách tái hiện: ______
- Ảnh cần chụp: A gửi, B nhận; DB lưu message; C bị chặn; network offline/online; notification; UI 360 px; test console.

## Rủi ro cần giải quyết khi ghép nhóm

- `DemoChatPermissions` kiểm tra block/match trong bảng giả. Phải thay bằng service của TV3 và TV5.
- Race giữa hành động block/unmatch và gửi cần integration tests chạy song song với giao dịch của TV3/TV5.
- Khi khóa account đã mở Hub, phải từ chối mọi hub method và đường dữ liệu; TV1/TV5 cần chính sách ngắt phiên hoặc kiểm tra lại khi phát sự kiện.
- Tắt ứng dụng trong lúc đang gửi có thể dẫn tới client không nhận ACK nhưng DB đã lưu; retry cùng clientMessageId là cách lấy lại message cũ.
- Mục tiêu 20 phiên chat, 100 hồ sơ giả của kế hoạch nhóm là test tải chưa có sẵn trong bộ mẫu này; nếu giảng viên yêu cầu phải tự chạy và đo.
