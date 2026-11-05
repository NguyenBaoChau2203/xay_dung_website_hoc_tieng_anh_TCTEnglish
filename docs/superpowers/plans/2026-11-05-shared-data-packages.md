# Gói dữ liệu dùng chung và đồng bộ sau pull

Châu hủy chuyển Vercel/Supabase, giữ ASP.NET/MariaDB. Cho phép triển khai, commit,
push, PR, merge master, staging; production tiếp tục OFF. Một agent thực hiện.

- [x] Công cụ CLI validate/status/export/sync: registry allowlist dữ liệu chung,
      stable keys/FK mapping, version/hash, dependencies, owner aliases, conflict
      trước ghi và transaction. Không xuất Users/credentials/progress/chat/payment.
- [x] Ledger EF additive + migration/Designer/snapshot; không sửa migration cũ.
- [x] Tệp theo manifest/hash, kiểm path/link/loại tệp, immutable managed paths;
      không đưa runtime uploads hoặc backup vào Git. Nội dung public mới nằm trong
      data/shared; xuất media qua file có chọn lọc, không copy cả uploads.
- [x] Lệnh pull-local + post-merge khi hooks đã cài + launcher sync:
      Docker local cố định, không reset DB; không rewrite User Secrets/key; pull
      đã xong nhưng import lỗi phải báo rõ và dừng bàn giao, không rollback Git.
- [x] Artifact/CI validate và staging sync sau backup/migration. Production chỉ
      nhập danh sách package/hash đã duyệt trong manifest riêng, dưới gate OFF
      hiện tại và environment approval; không có đường tắt ghi production.
- [x] Quy tắc bốn Agent và docs/onboarding: Agent tự export gói khi thay đổi dữ
      liệu chung, pull luôn gọi sync và xác minh; không hứa Git hook trên clone
      chưa setup. Dữ liệu cá nhân vẫn riêng; mọi dữ liệu chung hỗ trợ theo registry.
- [x] Regression có ý nghĩa: tạo/áp lại/v2/local-edit conflict/dependency/ID map,
      tamper/version rewrite, asset traversal/hash/conflict, private data reject,
      rollback/FK bảo toàn lịch sử trên MariaDB10.5.23 disposable; không reset dev.
- [ ] Tooling checks bắt buộc khi đổi hooks/rules; build, pending-model, tests liên
      quan và CI thực tế. Review/security/secret scan; commit theo nhóm, PR, merge
      master, sync workspace; kiểm staging và production gate false.

Luồng: tác giả export từ Docker vào package công khai đã chọn → PR/code review →
merge → máy khác pull + sync (hoặc startup catch-up) → staging nhập cùng gói.
Production approval có manifest riêng, không tự coi package merge là duyệt prod.
Xóa nội dung đang có lịch sử/phụ thuộc không được cưỡng ép; thay đổi có xung đột
phải giữ dữ liệu cũ và yêu cầu giải quyết rõ ràng, không dùng --force mặc định.
