# Staging: tự động phát hành, AI và dữ liệu nhóm — 05/11/2026

Owner yêu cầu hoàn thiện staging để nhóm test sau khi làm xong; production giữ
deploy off. Một agent thực hiện, bảo toàn worktree. Không chuyển key từng được
cấp quyền local-only lên hosting. Commit/push/merge cần quyền cho đúng giai đoạn.

## Công việc và tiêu chí nghiệm thu

- [x] Kiểm tra phiên Plesk/Hostinger, cấu hình staging và release hiện tại; giữ
      production gate false. Không thay DNS/binding/DB production.
- [x] Chuẩn bị CI job staging sau build/test/provider checks thành công trên
      master, gate/Environment/webhook/nhánh manifest riêng. PR/feature push
      chỉ kiểm tra, không deploy. Production script/guard giữ riêng.
- [x] Script staging khóa run, đối chiếu site/root/DB/environment, private backup
      trước migrations, giữ config/keys/uploads/data; maintenance và failure
      recovery rõ ràng. SHA256 + expected marker + readiness qua HTTPS Basic Auth.
- [x] Regression cho sai môi trường/root/DB, artifact lỗi/secret, webhook, run
      idempotency và private credentials; preflight + diễn tập disposable local.
- [x] Chuẩn bị helper nhập key staging kín trên máy (không dùng local key store),
      cấu hình OpenAI text/speech trên Plesk; metadata trước paid smoke có giới hạn.
      Không ghi key vào argv/source/Git/artifact/log.
- [ ] Xử lý IIS anonymous exception chỉ ACME challenge, root vẫn401; kiểm token
      đường HTTP + HTTPS và cấp lại chứng chỉ trong điều kiện đó nếu hosting cho.
      Nếu bị giới hạn quyền, cung cấp yêu cầu hỗ trợ cụ thể; không nới auth cả site.
- [x] Nghiệm thu Admin xuất bản học liệu: hai tài khoản riêng cùng thấy bài;
      draft/private/progress vẫn theo người. Không tự nâng quyền tất cả thành Admin.
- [x] Cập nhật runbook/onboarding/audit: cách dùng hằng ngày, phần cần owner nhập,
      Git stage còn chờ và bằng chứng local/hosting/CI phân biệt rõ.

## QA inventory

Happy path: approved master CI → staging backup → migration → app → identity/ready;
Admin thêm bài published → hai người cùng thấy → progress riêng; text/audio dùng
key staging khi cấu hình. Off path: PR không deploy; prod gate false; wrong DB/root
rejected trước ghi; backup lỗi không migrate; migration lỗi giữ maintenance;
anonymous root401 nhưng ACME token200; redeploy giữ users/goals/học liệu/secret.

Owner đã xác nhận quyền commit/push/PR/merge và bật riêng staging sau khi xem phạm
vi file/checks. Bootstrap branch/Plesk private preflight và Environment đã chuẩn
bị. Theo dõi first real CI/staging release sau merge; production false. SSL
exception còn cần Nhân Hòa, nội dung hỗ trợ sẵn, không báo tự gia hạn đã đạt.
