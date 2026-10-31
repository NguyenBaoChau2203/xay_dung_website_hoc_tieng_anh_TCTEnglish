# Cursor Auto — sửa và nghiệm thu chỉ local

Một Cursor Agent/Auto, allowlist Grok/Composer2.5; không agent phụ. Reuse
AGENTS.md/.ai/README.md và tct-engineering/tct-superpowers. CodeGraph trước source
indexed, không tự rebuild index. Owner instruction31/10 supersedes quyền push/CI
trong prompts trước: **chỉ làm local trong task này**.

Đọc review `docs/feature-audits/2026-10-31-cursor-sa-rereview-and-release-status.md`,
rồi scope và bước active trong
`docs/superpowers/plans/2026-10-31-local-bootstrap-and-acceptance-plan.md`.
Baseline df251e6, branch feature/bao-chau-ui-audit. Giữ local rule/docs chưa commit.

SA01–SA03 đã qua34 focused tests và3 probe cũ; full tracked729/729 với provider
đã executed trên container riêng. Không sửa lại những phần đó nếu không có red mới.
LB01/P2 còn: Escape-SqlLiteral chỉ escape quote; `Aa\nBb` trong SQL bị MariaDB đổi
backslash+n thành newline. Bootstrap có thể báo thành công với password khác.

Thực thi L0 red actual-script regression → L1 encoding/login trên MariaDB
disposable → L2 provider/ASR/UI acceptance còn thiếu → L3 enabled Windows staging
local với client trong workspace → L4 verify và báo owner. Không chỉ sửa một
helper rồi đánh toàn bộ acceptance Done. Mỗi case cần provider/command/assertion/
artifact, giữ partial/blocked khi chưa thực thi được.

Cho phép setup local trong scope owner giao: Docker loopback/DB synthetic riêng,
user synthetic, ASR English<=500MiB có pin/hash/license hiện có, app5110 và local
backup client/harness có provenance. Không chạm DB/volume dev, hosting, real
password/audio/PII; không dừng app/workload owner. Không ghi Program Files để giả
Plesk; test seam phải có guard và giữ default production. Không app package/
target/provider/Program/appsettings/csproj hoặc migration đã apply ngoài quyền.
Không bypass tool policy.

**Không commit/push, tạo/cập nhật PR, dispatch CI, merge, đổi Plesk, bật gate hoặc
deploy.** Không tạo remote run để đóng ledger. Báo local results/diff và đợi owner
review. Chỉ hỏi đầu vào thật sự thiếu, không xin lại quyền local chung.

## Chat copy

```text
Dùng Cursor Agent, Auto trong allowlist Grok/Composer 2.5, một agent duy nhất.
Đọc .ai/prompts/cursor-auto-local-bootstrap-and-acceptance.md và review docs/feature-audits/2026-10-31-cursor-sa-rereview-and-release-status.md. Tôi giao thực thi L0–L4 trong docs/superpowers/plans/2026-10-31-local-bootstrap-and-acceptance-plan.md, CHỈ LOCAL.
Sửa LB01 bằng regression đỏ trước, kiểm password đúng bytes/login trên MariaDB synthetic riêng; làm tiếp provider/ASR/UI/staging local theo plan. Giữ DB/volume dev, hosting và app/workload hiện có. Cho phép Docker loopback/DB riêng và ASR English <=500 MiB có pin/hash/license trong scope.
Không tự commit/push, tạo PR, chạy CI từ xa, merge, đổi Plesk, bật gate hay deploy. Quyền remote trong prompt cũ không còn áp dụng. Khi kiểm tra xong hãy báo actual evidence/diff và blockers để tôi review, không đánh Done bằng SQLite/curl/302 thay nghiệm thu thật.
```
