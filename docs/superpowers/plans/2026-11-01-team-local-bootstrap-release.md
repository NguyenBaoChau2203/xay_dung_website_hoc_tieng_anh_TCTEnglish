# Team local bootstrap, Whisper/OpenAI và merge giữ deploy off

Owner01/11/2026 cho phép setup, đổi tên nhánh không có codex/, commit/push/PR/
merge master. **Không production deploy**; giữ PLESK_DEPLOYMENT_ENABLED=false.
Một agent làm trực tiếp, giữ dirty Plan1/Plan2 và workload/data hiện có.

- [x] Inventory diff/launcher/compose/seeders; remote master vẫn03c9c5c; repo gatefalse,
  production environment không có variable override. Auth GitHub sẵn sàng.
- [x] Tạo cold-bootstrap dữ liệu mẫu/admin local riêng: chỉ Development, DB
  loopback tctenglish_dev; chỉ khởi tạo khi chưa có user, không đổi dữ liệu cũ,
  không seed hosting/CI thông thường. Random passwords chỉ ignored access.json.
- [x] Prerequisite doctor + hướng dẫn Windows x64/một lệnh, giải thích Git không
  chia sẻ Docker volume/UserSecrets/SQL/key. Optional private backup cho data thật.
  AI Agent đọc shared rules, dùng launcher để có DB/Whisper/web/key đúng nguồn.
- [x] TDD meaningful guards/idempotency/credential-file preservation; verify cold disposable MariaDB,
  rerun/data preservation, build/relevant full suites/tooling/EF. Không reset DB dev.
- [ ] Đổi nhánh `feature/local-docker-whisper-openai`; review/staged credential
  scan, chia commits Plan1/Plan2/team docs, bỏ ngoài phạm vi reportBTH2.
- [ ] Push/PR master; CI pass rồi merge theo quyền owner. Kiểm master CI và
  release job skipped/gatefalse; không bật Plesk hoặc chạy webhook.
- [ ] Đồng bộ workspace master; báo PR/merge/CI thực tế và đoạn chat gửi nhóm.

Skill áp dụng: tct-superpowers, tct-engineering, tct-tooling-review,
tct-data-seeder và tct-commit-message. Các checklist cũ ghi local-only là lịch sử;
quyền owner mới nhất cho phép Git stages ở trên, vẫn cấm production deploy.
