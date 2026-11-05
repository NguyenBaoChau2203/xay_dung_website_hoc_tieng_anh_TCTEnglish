# Kiểm tra và thiết lập staging trên Plesk Nhân Hòa — 05/11/2026

Owner yêu cầu mở Chrome để đăng nhập, kiểm tra gói Sinh Viên N518 và nếu đủ
khả năng thì thiết lập staging. Quyền này áp dụng staging, không cho đổi website
chính, dùng dữ liệu production, bật gate release production hoặc mua dịch vụ.
Một agent; bảo toàn worktree hiện có và secret stores.

- [x] Mở Chrome/Plesk đúng hostname TLS, owner đăng nhập trực tiếp.
- [x] Đọc hạn mức/quyền thực tế: subdomain/site riêng, app pool ASP.NET Core9,
      DB/user MariaDB riêng, storage, DNS/SSL, backup và upload/private files.
- [x] Lưu kết quả khả năng đã xác minh, không ghi credential/session/token.
- [x] Nếu đủ quyền, tạo staging.tctenglish.site với root và app pool riêng;
      tạo DB/user staging riêng, giới hạn đúng DB; TLS và truy cập test riêng.
- [x] Cấu hình Staging, workers/SMTP/payment/OAuth/AI và migrations để không ảnh
      hưởng production. Secret chỉ private store; không log hoặc đưa vào Git.
- [x] Chuẩn bị build/migration đã kiểm, đối chiếu đích trước mọi lần ghi; dùng
      synthetic fixtures/accounts có mật khẩu ngẫu nhiên riêng. Không copy DB
      hoặc key/user data production, không đè local volume.
- [x] Kiểm readiness, hai tài khoản cùng học liệu nhưng progress riêng; backup/
      restore trên DB thử riêng khi có quyền và hạn mức. Ghi checks chưa chạy.
- [x] Cập nhật database-collaboration/onboarding và audit kết quả thực tế;
      không commit/push/PR/merge hoặc remote CI khi chưa có quyền giai đoạn đó.

Kết quả: [audit](../../feature-audits/2026-11-05-plesk-staging-setup.md),
[runbook](../../operations/staging-plesk-runbook.md). Hosting hiện tại đủ, không
mua thêm server. Readiness200,HTTPS hợp lệ,anonymous401;3roles và progress riêng
đạt. Backup/restore trên DB Docker mới +repeat schema giữ dữ liệu. Gatefalse.

- [ ] Nghiệm thu gia hạn HTTPS tự động khi root Basic Auth vẫn bật. Cấp chứng
      chỉ hiện tại đã đạt; trước kỳ gia hạn cần Nhân Hòa cho anonymous chỉ path
      ACME của staging, hoặc vận hành maintenance/reissue theo runbook. Không
      sửa challenge directory chung của server hoặc nới auth production.
- [ ] OpenAI/OAuth/SMTP/payment live cần cấu hình riêng khi owner giao tiếp;
      key local không được copy lên hosting và chưa paid/live test.

Nếu gói bị giới hạn, tiếp tục chuẩn bị các phần không phụ thuộc; báo đúng hạn
mức/thiếu quyền và phương án tiếp theo. Không lách quyền hoặc đổi cấu hình site
production để dùng tạm làm staging. Không báo staging sẵn sàng chỉ vì tạo được
subdomain/DB; cần app và kiểm chứng theo checklist trên.
