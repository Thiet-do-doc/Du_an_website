# Ôn vấn đáp TV4 — Chat, thông báo, SignalR

**1. Em là thành viên mấy và làm gì?**
> Em là thành viên 4, phụ trách module ChatNotifications gồm cơ sở dữ liệu Conversations/Messages/Notifications, API lịch sử, SignalR gửi nhận realtime, giao diện React, reconnect và thông báo. Em cần phối hợp với TV1 về xác thực, TV3 về match, TV5 về block.

**2. Tại sao không chỉ dùng REST API để chat?**
> REST dùng tải danh sách và lịch sử. SignalR giữ kết nối realtime để server chủ động đẩy tin mới; giảm việc trình duyệt phải hỏi liên tục. Tin đã gửi vẫn phải lưu DB để không mất khi F5/mất mạng.

**3. SignalR khác WebSocket ở đâu?**
> SignalR là framework giao tiếp realtime, có Hub và cơ chế kết nối/reconnect; WebSocket là một transport. SignalR có thể dùng WebSocket hoặc fallback phù hợp.

**4. `ChatHub` là gì?**
> Lớp backend chứa các phương thức mà client gọi, ví dụ `JoinConversation` và `SendMessage`. Hub đọc UserId từ JWT đã xác minh và kiểm tra quyền trước khi thao tác.

**5. Vì sao C không đọc được chat của A và B?**
> Backend truy vấn conversation → match → 2 user; nếu UserId trong token không thuộc match thì từ chối GET và Hub. Không chỉ ẩn nút ở React vì client có thể tự sửa request.

**6. Nếu hai người chưa match thì có nhắn được không?**
> Không. Quy tắc bản đầu: hai bên like nhau mới tạo match và conversation. Tài khoản hợp lệ, match active và không bị block mới gửi tin được.

**7. Block rồi mà Hub chưa ngắt thì sao?**
> SignalR group không phải lớp bảo mật. Backend phải kiểm tra quyền lại trên mỗi lần `SendMessage` và cập nhật quyền nhận sự kiện. Khi tích hợp TV5 còn cần cơ chế invalidate connection và test tranh chấp block/gửi.

**8. `ClientMessageId` để làm gì?**
> Client tạo UUID trước lần gửi đầu. Nếu mất ACK và gửi lại, vẫn dùng UUID cũ. DB unique `(SenderId, ClientMessageId)` nên không tạo hai tin nhắn cùng một yêu cầu; service trả về message đã lưu.

**9. Tại sao phải lưu database trước rồi mới phát event?**
> Nếu phát trước mà DB lưu thất bại, người nhận thấy tin không có trong lịch sử. Ghi thành công và commit trước; nếu event bị mất, REST tải bù.

**10. Mất mạng rồi mở lại có mất tin không?**
> Không mất tin đã commit. Khi reconnect, client kết nối lại Hub, tham gia conversation khi được phép, lấy `afterId` của tin cuối đã có rồi truy vấn những tin mới hơn từ DB.

**11. Phân trang cursor khác `Skip/Take`?**
> Cursor nhớ cặp `(SentAt, Id)`, truy vấn tin trước/sau vị trí ấy. Cặp khóa giúp ổn định khi nhiều tin cùng thời điểm; không phải lấy cả lịch sử về máy. Notifications có thể dùng phân trang theo page như kế hoạch.

**12. Các bảng em thiết kế gồm gì?**
> Conversations có Id, MatchId unique. Messages có Id, ConversationId, SenderId, ClientMessageId, Content, SentAt; unique senderId+clientMessageId, index conversationId+sentAt+id. Notifications có Id, RecipientId, Type, TargetId, CreatedAt, ReadAt.

**13. Em dùng công nghệ nào?**
> React + TypeScript ở frontend, ASP.NET Core Web API .NET 10 C# ở backend, EF Core với PostgreSQL 17, SignalR realtime. Khi ghép code sẽ dùng Identity/JWT của TV1.

**14. JWT dùng ở đâu?**
> React gắn Bearer token khi gọi REST và dùng `accessTokenFactory` khi kết nối Hub. Server xác minh token rồi suy ra userId; không cho client gửi userId tùy ý để giả danh.

**15. Tại sao phải có DTO?**
> DTO định nghĩa dữ liệu JSON trao đổi; giới hạn trường được trả về và giúp React/C# thống nhất hợp đồng. Không trả hash mật khẩu hay dữ liệu riêng tư của người khác.

**16. Em làm giao diện chat như thế nào?**
> Chia layout: danh sách conversation, vùng tin nhắn và thông báo. Dùng state cho current conversation, messages, loading, draft, pending, error, connection status; CSS responsive cho mobile.

**17. Em xử lý tin lỗi/thử lại như thế nào?**
> Trước khi gửi tạo `clientMessageId`, hiển thị trạng thái sending. Khi server ACK thì cập nhật message; khi lỗi giữ nguyên id và hiện nút retry, nên retry không tạo duplicate.

**18. Đánh dấu đã đọc thông báo có phải đã đọc tin nhắn không?**
> Không. `Notifications.ReadAt` chỉ là trạng thái người dùng đã mở thông báo. Read receipt của từng Message là phần mở rộng, chưa thuộc P0.

**19. Khi hai người unmatch thì sao?**
> Không cho gửi mới. Theo chính sách trong báo cáo, giao diện hội thoại chỉ đọc, còn nội dung cũ được xử lý theo quyền đã thống nhất. Sau block có thể ẩn hội thoại theo chính sách của nhóm.

**20. Làm thế nào biết mình hoàn thành?**
> Chạy cùng lúc A/B không reload, F5 vẫn có lịch sử, C không thể đọc/gửi, block/lock có hiệu lực, reconnect tải bù, gửi lặp không trùng; test và code được review/merge vào nhánh chung. Không khẳng định PASS nếu chưa chạy thật.

**21. Nếu giảng viên chỉ vào đoạn này: `HasIndex(x => new { x.SenderId, x.ClientMessageId }).IsUnique()`?**
> Đây là unique index trong database để chống lưu cùng một lượt gửi hai lần, kể cả server nhận lại request sau timeout.

**22. Nếu giảng viên hỏi `await tx.CommitAsync()`?**
> Xác nhận giao dịch lưu Message và Notification. Chỉ sau khi commit mới phát `MessageCreated` để dữ liệu được đảm bảo tồn tại.

**23. Nếu giảng viên hỏi `OnConnectedAsync`?**
> Là hàm lifecycle của Hub chạy khi client kết nối; em kiểm tra tài khoản đang hoạt động. Tuy nhiên mỗi lần gửi vẫn phải kiểm tra quyền lại vì trạng thái có thể đổi sau đó.

**24. Nếu giảng viên hỏi `withAutomaticReconnect`?**
> Đây là cấu hình SignalR client cố kết nối lại khi mất mạng. Sau reconnect phải gọi lại JoinConversation và REST lấy tin bị lỡ; reconnect tự động một mình không khôi phục event đã mất.

**25. Nếu giảng viên hỏi em hỗ trợ phần nào ngoài TV4?**
> Theo kế hoạch, em là dự phòng của TV3 phần DiscoveryMatching: phải hiểu EnsureConversation, trạng thái match và sửa được ít nhất một truy vấn khám phá; em cũng tham gia review và deploy thử cùng nhóm.

**Lưu ý trả lời:** chỉ dùng từ "đã chạy", "đã kiểm thử", "đã triển khai" khi có screenshot/log/PR thật; hiện ZIP là bản mã nguồn tham khảo để bạn thực hành và tích hợp.
