# Posts frontend
`FeedPage.tsx` hiển thị bảng tin thành viên tại `/`, hỗ trợ bài có ảnh, cảm xúc, lượt thích, bình luận và xóa bài của chính mình.
Dùng `authenticatedFetch` chung; API thật `/api/v1/posts`.
Nút chia sẻ gửi hoặc sao chép đường dẫn `/?post={id}`. Người mở link cần đăng nhập và có quyền xem bài.
