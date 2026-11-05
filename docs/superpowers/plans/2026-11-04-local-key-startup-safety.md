# Local startup dùng key an toàn — 04/11/2026

Owner yêu cầu kiểm key vừa nhập và mỗi lần AI Agent mở web phải dùng key local.
Một agent; không commit/push/CI/merge/deploy nếu chưa được yêu cầu trong bước này.

Key mới đã qua GET metadata của hai model (200) và một text request rất nhỏ
(200/completed/OK,11 input +5 output tokens). Khi thử launcher, Update-LocalSecretStore
dùng File.Move(overwrite=true) trong thư mục EFS bị lỗi; destination secrets.json
không còn. Không thay bảo vệ/EFS/ACL hoặc dùng bản protected D thay cho active store.
Không tự chọn global key khác để che incident; cần owner nhập lại sau bản sửa.

- [x] Xác minh key có thật; chỉ log source/status, không giá trị key/error body.
- [x] Điều tra launcher và lỗi ghi secret; đối chiếu bug memory/source/API file.
- [x] Regression launcher: chạy được khi secret store cấm ghi; bytes/mtime giữ nguyên,
      DB/runtime flags vào child process và khôi phục cả khi lỗi/DatabaseOnly.
- [x] Bỏ ghi User Secrets lúc startup; giữ key tại store chuẩn. Key helper thay
      file có backup/khôi phục, không delete destination rồi move.
- [x] Cập nhật shared rules/onboarding và bug/known issue, không chứa key.
- [x] Chạy focused Python →tooling/native hooks/full scripts theo phạm vi.
- [x] Owner nhập lại key qua masked helper sau bản sửa; không gửi key vào chat.
      Ban đầu owner hoãn, sau đó đã nhập lại và yêu cầu restart. Không thử key
      global khác thay thế.
- [x] Launcher thật +/health/ready +speech status, mở browser cho non-AI.
      Ready200; follow-up restart đã nhận cả text/speech key từ User Secrets,
      file metadata không đổi. Không coi configured là paid live acceptance.
      Không gọi paid ASR nếu chưa opt-in riêng.
