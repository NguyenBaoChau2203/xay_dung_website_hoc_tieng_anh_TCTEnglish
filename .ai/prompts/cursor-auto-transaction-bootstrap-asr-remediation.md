# Cursor Auto — transaction, bootstrap và ASR sau review31/10

Dùng một Cursor Agent, Auto trong allowlist Grok/Composer2.5 của owner. Không
agent phụ hoặc AI CLI khác. Reuse AGENTS.md/.ai/README.md, tct-engineering và
tct-superpowers; CodeGraph trước source indexed, không rebuild index tự động.

Đọc review `docs/feature-audits/2026-10-31-cursor-sentinel-acceptance-review.md`,
rồi scope/T0 và bước đang chạy trong
`docs/superpowers/plans/2026-10-31-transaction-bootstrap-asr-remediation-plan.md`.
Baseline `69c6815`, branch `feature/bao-chau-ui-audit`; giữ thay đổi của owner.

Ba finding cụ thể:

1. SA01/P1: CompletionCreditService retry1213 trên transaction do Speaking/
   Writing caller mở; Goals cũng swallow1213 rồi increment. Retry phải ở chủ
   unit hoặc propagate an toàn. Không retry/query trên transaction đã rollback.
2. SA02/P2: bootstrap-mariadb-acceptance.ps1 DROP sentinel rồi INSERT run mới,
   không exact-match run/actual port. Sửa script vận hành và negative tests;
   helper C# xanh không đủ. Existing foreign identity phải giữ nguyên.
3. SA03/P2: `[BLANK_AUDIO]` của engine được chọn thành Ok. Marker-only phải
   NoSpeech; giữ speech thật/mixed và invalid-schema/error behavior khác nhau.

Làm T0 red trước → T1/T2/T3 → T4 acceptance thực → T5 verify/ledger. Probe của
review trong `.tmp/sentinel-rereview/` chỉ là gợi ý nếu còn, không phải dependency
của tests bàn giao. Không coi full suite xanh là đã che phủ ba nhánh mới.

G03 chỉ dual-credit chạy MariaDB; notification/import matrix còn SQLite. G01 chỉ
silence HTTP200, chưa app E2E. G04 chưa login sâu25 view; tạo bcrypt account
synthetic local thay vì yêu cầu account thật. G06 chưa vượt backup-client guard;
chuẩn bị client/harness local có provenance, không cần Plesk thật. Ghi đúng
partial/blocked nếu thực sự không thể chạy. Không đóng checkbox vượt evidence.

Phạm vi sửa/commit/push/CI theo chat thực thi owner. Cho phép setup local đã giao:
Docker loopback/DB synthetic riêng, ASR English<=500MiB có pin/hash/license, app
5110 riêng, local backup client/harness có provenance. Không dùng DB/volume dev,
hosting, real credentials/PII hoặc paid API. Không đổi app package/target/provider,
Program/appsettings/csproj ngoài quyền cụ thể, không sửa migrations đã apply.
Không merge master, đổi Plesk, bật gate/deploy production. Không bypass tool policy.

Khi được giao thực thi, làm hết phần độc lập còn được phép trong cùng lượt. Không
dừng sau ba unit fixes nếu provider/browser/staging còn làm được. Chỉ hỏi đầu vào
thật sự thiếu; không xin lại quyền local chung. Build/full/relevant script tests,
provider assertions, cập nhật ledger đúng SHA/pass/fail/skip/blocked. Nếu được giao,
commit theo nhóm, push feature và kiểm CI đúng SHA; báo release riêng.

## Chat copy

```text
Dùng Cursor Agent, Auto trong allowlist Grok/Composer 2.5, một agent duy nhất.
Đọc .ai/prompts/cursor-auto-transaction-bootstrap-asr-remediation.md và review docs/feature-audits/2026-10-31-cursor-sentinel-acceptance-review.md. Tôi giao thực thi T0–T5 trong docs/superpowers/plans/2026-10-31-transaction-bootstrap-asr-remediation-plan.md.
Sửa SA01–SA03 bằng regression đỏ trước; làm tiếp acceptance local đã giao, tạo user bcrypt synthetic, chuẩn bị client/harness staging có provenance. Cho phép Docker/DB riêng và ASR English <=500 MiB có pin/hash/license theo plan; giữ DB/volume dev và hosting nguyên vẹn.
Khi checks pass, commit theo nhóm, push feature và chạy CI đúng SHA. Không merge, đổi Plesk, bật gate hoặc deploy production; không bypass tool policy. Báo actual evidence/provider và blockers, không đánh Done bằng SQLite/curl/302 thay acceptance thật. Không dừng sau một fix hoặc hỏi lại quyền local vừa giao.
```
