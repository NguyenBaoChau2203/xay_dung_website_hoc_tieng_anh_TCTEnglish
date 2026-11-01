# Plan 2 — review và hoàn thiện chạy local

Owner01/11/2026 cấp quyền review và sửa trực tiếp; muốn mọi AI Agent chạy web,
Whisper và test key local cùng nhau. Owner xác nhận dùng `OPENAI_API_KEY` cho
TCT English. Một agent thực thi, không commit/push/CI remote/PR/merge/deploy.
Giữ nguyên dirty diff Plan1/Plan2 và dữ liệu/workloads hiện có.

- [x] Preflight branch/diff/ledger; review provider Responses và media pipeline
  từ source CodeGraph, xác minh docs chính thức cho Luna low.
- [x] Xác định key riêng TCT đang trống; `OPENAI_API_KEY` có cấu hình. Chỉ kiểm
  presence/source, không in key; owner xác nhận được dùng nguồn này.
- [x] Viết regression đỏ→xanh cho nối key trong launcher: ưu tiên `AI__ApiKey`,
  User Secrets, rồi fallback `OPENAI_API_KEY`; không lưu/copy key vào repo/log,
  không mutate source env sau khi launcher kết thúc. Báo trạng thái đã cấu hình
  và source, không gọi cloud chỉ vì start web.
- [x] Kiểm các regression ứng dụng/provider/audio; sửa lỗi được tái hiện nếu có.
- [x] Cập nhật AGENTS và runbook chung cho Codex/Cursor/Claude Code/Antigravity:
  chạy `scripts/start-local.ps1 -NoBrowser`; xác nhận app/DB/speech/key-source.
  Cổng có app cũ không chứng minh config mới; không kill process ngoài ownership.
- [x] Build + full relevant tests/tooling + pending EF model check; chạy launcher
  thật, health và inference status, smoke OpenAI nhỏ với key owner cho phép.
  Không gọi paid API trong full suite/hook/startup hoặc in secret.
- [x] Ghi report và ledger: lỗi đã sửa, local checks, các acceptance chưa chạy;
  phân biệt local/CI/production. Không dừng ở plan sửa khác.

Kết quả: [review closeout](../../feature-audits/2026-11-01-openai-migration-review-closeout.md).
107 scripts pass,808 app pass/5skip,74 focused pass,9Nodepass,tooling/hooks/EF
pass. Key env ban đầu401; owner cho key thay thế, nhập masked vào User Secrets
và smoke2requests pass actual Luna low. Web mới5102/Whisper giữ chạy cho owner.
