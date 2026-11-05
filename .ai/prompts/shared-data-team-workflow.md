# Cập nhật code và dữ liệu chung bằng AI Agent

Đọc AGENTS.md và docs/operations/shared-data-workflow.md. Làm inline bằng một
agent; dùng source thực tế/CodeGraph, không reset database hoặc sửa ledger.

Khi tôi yêu cầu pull, dùng `pwsh -NoProfile -File scripts/pull-local.ps1` cho
origin/master; xử lý Git conflict theo quyền được cấp. Code đã lấy về nhưng sync
lỗi phải báo chưa xong, giữ dữ liệu và dừng chạy web đến khi giải quyết.

Khi tôi yêu cầu thêm/sửa nội dung dùng chung, xác định gốc allowlist và ID từ DB
local, kiểm `scripts/shared-data.ps1 -Action Status`, tự Export gói có chọn lọc
trong cùng phạm vi công việc. Giữ package/key đã có, xuất version tiếp theo,
review JSON/media/hash/dependencies; không sửa bản đã merge. Không xuất tài
khoản, key, mật khẩu, Goal/tiến độ/bài làm cá nhân hoặc nguyên SQL/volume.
Entity chưa hỗ trợ cần adapter có kiểu, bảo mật và regression trước khi export.

Validate/sync, chạy kiểm thử tính năng và kiểm tra diff trước khi đề nghị commit.
Chỉ commit/push/PR/merge khi được cho phép giai đoạn đó. Production vẫn OFF;
không tự Approve, mở gate hoặc đưa dữ liệu lên production.

Khi tôi yêu cầu chạy web local, dùng launcher chuẩn `scripts/start-local.ps1
-NoBrowser`, dùng key đã lưu và kiểm readiness/speech status. Không cài lại
Whisper. Báo kết quả code, data sync, tests và blocker bằng tiếng Việt.
