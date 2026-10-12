# Đưa bộ khung lên repository đã có

## Cách nên dùng để giữ lịch sử repository
1. Mở PowerShell trong thư mục muốn lưu project, ví dụ `C:\Users\<ten>\source`.
2. Clone repository của nhóm (thay URL mẫu bằng URL thật):
```powershell
git clone https://github.com/TEN-TAI-KHOAN/TEN-REPO.git
cd TEN-REPO
```
3. Giải nén ZIP. Copy **nội dung bên trong thư mục DatingApp** vào thư mục repo vừa clone.
   Bao gồm `.github`, `.config`, `.gitignore`, `.editorconfig`. Không copy thêm lớp
   `DatingApp` bên ngoài. Không đụng vào `.git` của repo đã clone.
   Nếu repo đã có code khác, kiểm tra từng file trùng tên trước khi thay thế.
   Nếu chỉ có README cũ, giữ lại nội dung cần thiết rồi bổ sung README của bộ khung.
4. Tạo nhánh và kiểm tra:
```powershell
git switch -c chore/project-scaffold
git status
git add .
git diff --cached --stat
git diff --cached
```
5. Chỉ khi danh sách đúng và không có secret:
```powershell
git commit -m "chore: add shared project scaffold"
git push -u origin chore/project-scaffold
```
6. Trên GitHub mở Pull Request vào nhánh chính của repo, nhờ một bạn review rồi merge.
   Repository hoàn toàn rỗng có thể chưa mở được PR do chưa có nhánh chính;
   trong trường hợp đó, lần khởi tạo đầu có thể dùng:
```powershell
git branch -M main
git push -u origin main
```
   Chỉ dùng bước này khi repo thật sự chưa có nhánh chính. Không force push.

## Khi thành viên nhận việc
Sau khi bộ khung đã merge:
```powershell
git switch main
git pull --ff-only
git switch -c feat/auth-register
```
Thay `main` bằng tên nhánh chính nếu repo đang dùng tên khác.
Chỉ commit phần của mình, tạo PR nhỏ; không đưa `.env`, `node_modules`, `bin`, `obj` lên Git.
README, migration, lockfile và tài liệu dùng chung nên được commit để ai clone cũng chạy được.
Không đưa chính file ZIP lên repository: GitHub cần các file source đã giải nén.
