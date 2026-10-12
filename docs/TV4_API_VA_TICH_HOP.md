# Đặc tả API và hợp đồng tích hợp TV4

## A. HTTP REST API

Tất cả endpoint `/api/v1` dưới đây (trừ health) yêu cầu header `Authorization: Bearer <JWT>` do TV1 phát.

| Method | Endpoint | Chức năng | Trạng thái lỗi |
|---|---|---|---|
| GET | `/api/v1/conversations?page=1&pageSize=20` | Lấy hội thoại hiện có của chính mình | 401 |
| GET | `/api/v1/conversations/{id}/messages?limit=30` | 30 tin mới nhất, có `nextCursor` để đọc trang cũ | 401, 404 |
| GET | `/api/v1/conversations/{id}/messages?cursor=...` | Trang tin cũ theo `(SentAt, Id)` | 400, 401, 404 |
| GET | `/api/v1/conversations/{id}/messages?afterId={guid}` | Tin mới hơn tin cuối cùng mà client giữ | 400, 401, 404 |
| GET | `/api/v1/notifications?page=1&pageSize=20` | Danh sách thông báo của mình | 401 |
| PATCH | `/api/v1/notifications/{id}/read` | Đánh dấu chính thông báo của mình đã đọc | 401, 404 |
| GET | `/api/v1/health` | Kiểm tra backend có trả lời | – |

Danh sách giới hạn `pageSize` tối đa 50. Trang đầu của tin trả mảng `items` theo thứ tự **cũ đến mới**. `nextCursor` = trang tin cũ hơn. Nếu tải tin bị lỡ bằng `afterId`, lặp cho tới khi `nextAfterId` là `null`. Trường hợp đọc chat sau unmatch ở chế độ **chỉ đọc**, không cho gửi.

Ví dụ DTO `Message` (JSON camelCase):

```json
{
  "id": "77c4a6d8-b5e6-4c16-8d4d-433e4b9e753d",
  "conversationId": "383caad3-045d-4cf3-9927-20a9adfbfbd8",
  "senderId": "00000000-0000-4000-a000-00000000000a",
  "clientMessageId": "725d28a3-2a2b-4956-83f1-376d77ecee98",
  "content": "Xin chào, đây là dữ liệu demo",
  "sentAt": "2026-10-12T13:00:00+00:00"
}
```

Ví dụ `MessagesPage`:

```json
{
  "items": [/* MessageDto */],
  "nextCursor": null,
  "nextAfterId": null
}
```

Thông báo:

```json
{
  "id": "2ae698b7-1677-4639-aed8-ab59cc4b4eb2",
  "type": "newMessage",
  "targetId": "383caad3-045d-4cf3-9927-20a9adfbfbd8",
  "createdAt": "2026-10-12T13:00:00+00:00",
  "readAt": null
}
```

- `type=match`: `targetId` chính là MatchId.
- `type=newMessage`: `targetId` chính là ConversationId.
- **Đã đọc thông báo** khác hoàn toàn **đã đọc tin nhắn**. Message read receipt thuộc phần mở rộng.
- Không trả email, mật khẩu, ngày sinh hoặc nội dung chat cho tài khoản không có quyền.

## B. SignalR Hub

Địa chỉ: `GET/WS /hubs/chat`. Frontend gửi JWT qua `accessTokenFactory`. Chỉ lấy JWT ở query `access_token` cho Hub transport tương thích; không ghi token vào log. UserId lấy từ claim do backend xác minh, không lấy từ request body.

| Loại | Tên | Dữ liệu | Lưu ý |
|---|---|---|---|
| Client gọi | `JoinConversation(conversationId)` | UUID | Phải có quyền xem; C không vào group A/B |
| Client gọi | `LeaveConversation(conversationId)` | UUID | Rời group |
| Client gọi | `SendMessage(conversationId, content, clientMessageId)` | UUID, text, UUID | Trả về `MessageDto` để xác nhận gửi |
| Server phát | `MessageCreated` | `MessageDto` | Phát sau commit khi gửi mới |
| Server phát | `MatchCreated` | `{matchId}` | Sau match đã lưu và commit |
| Server phát | `NotificationCreated` | `NotificationDto` | Thông báo cập nhật realtime |

**Chống trùng:** nếu gửi lại cùng `(SenderId, ClientMessageId)` và cùng hội thoại/nội dung, trả bản ghi cũ, không tạo tin mới hay thông báo mới. Nếu reuse ID cho nội dung khác, trả lỗi xung đột.

**Phân quyền:** khi `JoinConversation`, đọc lịch sử và **mỗi lần SendMessage**, phải kiểm tra danh tính + đối tác + trạng thái match + block hai chiều + tài khoản còn hiệu lực. Chỉ `JoinConversation` thành công không đủ chứng minh quyền gửi sau này.

## C. Database bảng TV4 sở hữu

```
DemoMatch (do TV3 sở hữu sau merge)
  1 ─── 0..1 Conversation 1 ─── 0..n ChatMessage
                                SenderId ───> User (TV1)
User (TV1) 1 ─── 0..n AppNotification
```

`Conversations`: PK `Id`, FK `MatchId`, unique `MatchId`, `CreatedAt` UTC.

`Messages`: PK `Id`, FK `ConversationId`, FK `SenderId`, `ClientMessageId`, `Content`, `SentAt` UTC. Unique `(SenderId, ClientMessageId)`, index `(ConversationId, SentAt, Id)`.

`Notifications`: PK `Id`, FK `RecipientId`, `Type`, `TargetId`, `CreatedAt`, `ReadAt?`; index `(RecipientId, ReadAt)`.

`Demo*` bảng giữ quan hệ mẫu cho local; thay bằng bảng thật của nhóm khi merge.

## D. Giao tiếp bắt buộc với TV3 và TV5

**TV3** sử dụng `MatchChatBridge.EnsureConversationAsync(matchId)` trong transaction tạo match. Chỉ có **một** Conversation cho một match; hoàn thành transaction rồi mới `PublishMatchAsync()`. Để hiển thị thông báo, cần `QueueMatchNotifications` **trước commit** và `PublishMatchAsync` **sau commit**. Phát sự kiện mà chưa commit dễ dẫn đến UI có match nhưng DB chưa có.

**TV5** thay lớp `DemoChatPermissions` bằng adapter của `BlockPolicy` và phải kiểm tra block hai chiều trong truy vấn. Thỏa thuận cách vô hiệu hóa kết nối, truyền `PermissionsChanged` hoặc bắt client đồng bộ lại khi block. Tài khoản bị khóa không được cấp token mới cũng như không được gọi Hub/gửi dữ liệu bằng phiên cũ.

**TV1** cung cấp UserId UUID trong JWT và chính sách `IsAccountActive`, `EmailConfirmed`/`ProfileCompleted`. Token Hub chỉ là cơ chế nhận dạng; service của TV4 vẫn kiểm tra quyền truy cập trên từng đối tượng.

## E. Những điểm P1/P2 KHÔNG nằm trong yêu cầu MVP

- Presence/online status, typing indicator, read receipt của từng tin.
- Video call WebRTC, media, notification push hệ điều hành, gửi tệp trong chat.
- Redis backplane, phân tán nhiều backend.

Nếu thiếu thời gian, **không** ưu tiên các mục trên thay cho test phân quyền hoặc chống mất tin.
