# Phân công TV4: từ tuần 1 tới tuần 8 và cách chia Issues

## Công việc chính

| Issue gợi ý | Dự kiến | Phụ thuộc | Người review |
|---|---|---|---|
| `T4-01-db-contract` | Chốt DTO Message/Notification, entity/index/migration | TV1 UserId, TV3 MatchId | TV1 + TV3 |
| `T4-02-history-api` | GET conversations + message history cursor | T4-01 | TV5 (kiểm tra quyền) |
| `T4-03-signalr-hub` | Hub JWT, join, send kiểm tra quyền | TV1 Auth, TV5 BlockPolicy | TV1 + TV5 |
| `T4-04-send-dedup` | Lưu trước phát, ACK, unique clientMessageId | T4-03 | TV3 |
| `T4-05-chat-ui` | React chat, trạng thái gửi/fail, responsive | DTO/API/Hub | TV2 |
| `T4-06-reconnect` | Rejoin, tải tin bằng afterId, retry | T4-05 | TV5 |
| `T4-07-notifications` | MatchCreated/MessageCreated, thông báo & read API | TV3 Match | TV3 + TV5 |
| `T4-08-tests-docs` | Unit/smoke, ERD module, ghi log/chụp ảnh | Tất cả | TV5 |

Mỗi Issue lý tưởng chia nhỏ 2–4 giờ; tối đa hai Issue đang xử lý; mở PR nhỏ, CI/test qua và review trước khi merge. Ước tính trong báo cáo TV4 dành tổng **80 giờ/8 tuần** (40 giờ chức năng, 20 giờ học chung, 12 giờ review/hỗ trợ, 4 giờ điều phối, 4 giờ dự phòng), chỉ là quy ước lập kế hoạch, không phải giờ đã làm.

## Lộ trình bám sát báo cáo

- **Tuần 1:** chốt message DTO, event name, UserId/CurrentUser với TV1 và TV3; tạo branch, thực hiện PR nhỏ.
- **Tuần 2:** dựng entity/history và thử SignalR Hub; ghép đôi TV3–TV4 để chốt `EnsureConversation(matchId)`.
- **Tuần 3:** kiểm tra quyền hội thoại và block với TV5; hoàn thành API history và DB.
- **Tuần 4:** tích hợp match→conversation→chat của TV3; A/B/C end-to-end; TV4 là người demo theo kế hoạch.
- **Tuần 5:** xử lý reconnect, gửi lặp, thông báo, quyền khóa tài khoản.
- **Tuần 6:** đổi người sửa lỗi; TV4 sửa một truy vấn discovery của TV3; thực hành deploy/log cùng nhóm.
- **Tuần 7:** đóng băng tính năng, chạy smoke, kiểm thử phân quyền và biên bản thật.
- **Tuần 8:** tag release, quay demo, hoàn thành README/API/doc/tests và tập trả lời vấn đáp.

## Hợp đồng phải xin từ nhóm

1. **TV1**: tên trường Claim chứa UserId, cách kiểm tra tài khoản bị khóa, email confirmed, refresh token, quyền truy cập Hub.
2. **TV3**: Match (2 ID, active/unmatched), event sau khi match đã commit, method `EnsureConversation`.
3. **TV5**: BlockPolicy API, blocked hai chiều, account suspended, thời điểm revoke quyền.
4. **TV2**: ProfileSummary tên/ảnh hiển thị cho danh sách chat, giao diện Bootstrap được nhóm chọn.

Không thể ghép hoàn thiện vào dự án nhóm khi chưa có các hợp đồng này và mã nguồn thật. Bộ ZIP có `Demo*` để làm bài được song song, nhưng các lớp ấy phải được bỏ hoặc thay trong PR nhóm.

## Hướng dẫn tạo nhánh PR trên GitHub

Nếu TV1 đã tạo repo chung, `git clone <link-repo-nhom>` một lần, cd vào folder repo chung, sau đó:

```powershell
git switch main
git pull --ff-only
git switch -c feat/T4-01-chat-database
# Copy những file thuộc module TV4, tích hợp DbContext có sẵn
git status
git add backend/DatingApp.Api/Modules/ChatNotifications
git commit -m "feat(tv4): chat entities, cursor API and Hub"
git push -u origin feat/T4-01-chat-database
```

Vào repository → **Compare & pull request** → mô tả task + screenshot/test + gửi TV1/TV3/TV5 review. Không push thẳng `main`, không commit `Jwt:Key`, `.env` thật hoặc mật khẩu token. Migration cần lên lịch một người tạo/merge tại một thời điểm.

## Bàn giao của TV4 cho nhóm

- Backend module `ChatNotifications` + API JSON + Hub event contracts.
- EF model/index + migration sinh từ DbContext chung sau khi được duyệt.
- React `features/chat` tích hợp router và AuthProvider của TV1.
- Hướng dẫn mở hai browser A/B, C kiểm tra chặn; kết quả test **thực tế**.
- PR/commit, ảnh demo, ghi chú known bugs, giải thích cách reconnect và chống trùng.
