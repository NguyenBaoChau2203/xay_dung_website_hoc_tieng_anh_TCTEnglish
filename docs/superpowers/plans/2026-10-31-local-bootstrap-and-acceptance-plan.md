# Plan local: bootstrap password và acceptance còn thiếu

Baseline `df251e6`, feature/bao-chau-ui-audit,31/10/2026.
[Review](../../feature-audits/2026-10-31-cursor-sa-rereview-and-release-status.md).
[Prompt](../../../.ai/prompts/cursor-auto-local-bootstrap-and-acceptance.md).

## Quyền và phạm vi

Owner yêu cầu làm ổn định local trước. Review hiện tại chỉ sửa tài liệu/quy định;
sửa code được giao qua chat thực thi. Một Cursor Agent Auto, allowlist
Grok/Composer2.5, không agent phụ. Không tự commit, push, tạo PR, dispatch CI,
merge, bật gate, đổi Plesk/deploy. Chỉ đọc remote khi cần xác minh; chưa được ghi.
Giữ workflow và production gatefalse, không cần disable CI để bảo vệ hosting.

Không làm lại SA01–SA03 đã xanh nếu không có regression mới. Không đổi app
packages/target/provider, Program/appsettings/csproj, migrations đã apply hoặc
auth/AI chung. Không SQL/copy dữ liệu vào hosting/dev, không dừng app/các workload
owner, không đụng volume dev. Chỉ DB/container synthetic riêng với ownership rõ.
Không có schema impact cho LB01; không tạo migration để sửa bootstrap.

## L0 — Regression LB01 trước sửa

- [ ] Ghi status/SHA, giữ các tài liệu/rule owner còn chưa commit.
- [ ] Trong `scripts/tests/test_bootstrap_mariadb_acceptance.py`, chạy actual
  PowerShell script qua fake docker với password có backslash, quote, backslash+
  quote, whitespace và Unicode. Kiểm generated SQL giữ đúng logical value;
  không viết expected theo literal lỗi hiện tại.
- [ ] Reuse SELECT HEX proof: `Aa\nBb` phải là41615C6E4262, hiện thành41610A4262.
  Regression red phải chứng minh script làm đổi bytes, không chỉ test helper độc
  lập không được đường tạo user gọi tới.
- [ ] Không in password thật/SQL backup vào stdout, Git, docs hoặc TRX. Fixture
  dùng synthetic value; logs/auth note trong ignored local output.

## L1 — Encode literal theo contract MariaDB

Files: `scripts/bootstrap-mariadb-acceptance.ps1`, script regression tests;
local disposable harness nếu cần. Không sửa deployment production vì LB01.

- [ ] Chọn một contract rõ: encoding/parameter phù hợp, hoặc session sql_mode
  được thiết lập cho **mỗi** invocation rồi escape đúng quote/backslash. Xem mode
  mặc định và NO_BACKSLASH_ESCAPES; không thay global sql_mode/server config.
- [ ] CREATE/GRANT và các SQL strings dùng cùng contract. Không dùng cách đổi
  password người gọi thành password khác để vượt test. Giữ byte/value tương ứng
  khi login bằng connection string/password gốc.
- [ ] Giữ exact run/port/DB/sentinel guard, reject-before-write, no overwrite và
  native exit code; các6 test hiện có phải giữ pass. Không để quote/cmd transport
  làm thay password trong trường hợp có ký tự đặc biệt.
- [ ] MariaDB10.5.23 disposable mới: bootstrap → login bằng password gốc → schema/
  sentinel verify → rerun cùng identity → reject foreign identity không writes.
  Nếu test nhiều sql_mode, chỉ session của harness được thay đổi. Cleanup theo
  exact ownership/run/container ID, không tìm theo prefix rồi delete.

## L2 — Nghiệm thu provider/ASR/UI còn thiếu

Làm các việc local đã được giao trong plan trước; không dùng scope này để gọi CI.

- [ ] G03: dùng MariaDB factory cho notification450 recipients/dedup/checkpoint/
  stale lease, import retry/fingerprint/hash/Premium/atomic publish, outbox race.
  Ghi provider cho từng case. Standalone dual-credit5 lần đã pass nhưng không
  thay real Speaking/Writing unit atomicity hoặc các case worker khác.
- [ ] G01: loopback binary/model hiện có (English<=500MiB pin/hash/license), audio
  synthetic/licensed. Test app WAV→assessment→receipt→progress→credit/reload và
  silence/wrong phrase/unavailable/cancel không thưởng; raw response + timings.
  Giữ ASR optional engines default-off theo phạm vi cũ, không paid API/PII.
- [ ] G04: synthetic bcrypt roles đã có; giữ app5110 riêng và DB riêng. Matrix25
  view AU01–06/CO01/SP01–03/WR01–03/GO01/AI01–02/AD01–09 từng dòng, desktop/mobile/
  keyboard/error/empty/loading/race. HTTP200/302 và login screenshot chưa đủ.
- [ ] Nếu host/tool policy cản một bước, ghi action/error chính xác, không bypass;
  tiếp tục việc độc lập. Không xin lại quyền local đã được giao.

## L3 — Windows staging local không cần quyền ghi Program Files

- [ ] Dùng MariaDB client official có hash/provenance đã nằm trong workspace.
  Dựng harness/profile cô lập để chỉ định client đó; không cài/giả Plesk trên máy,
  không sửa chuỗi trong bản script tạm rồi gọi là nghiệm thu production logic.
- [ ] Nếu cần thêm dependency seam vào deploy script, giữ default production và
  các guard; override test chỉ chấp nhận staging root/DB local owned rõ. Không
  cho test mode trỏ SiteRoot/DB/config hosting/dev. Test rejection của seam trước.
- [ ] Enabled backup→migration→copy→health/marker, inject backup/migration/copy/
  health failure, maintenance/concurrent/rerun. Restore dump vào DB mới kiểm
  counts/checksums/history; giữ uploads/private config. Không fake backup return0.
- [ ] Thiếu điều kiện thực sự thì giữ partial/blocked, ghi đúng đầu vào và làm
  tiếp các bước khác. Không thay việc chưa làm bằng tổng số unit tests.

## L4 — Verify local và dừng để owner review

- [ ] Build app + focused tests + script61 tests trở lên. Full suite trong bin
  chuẩn; chọn Release khi Debug bị app owner khóa, không stop app để build.
- [ ] Provider test executed trên DB riêng và không skip; tool/schema checks
  theo thay đổi. Nếu chỉnh guidance/tooling, chạy tooling validator + Python suite.
- [ ] Ledger ghi actual command/provider/pass/fail/skip/blocker/artifact/SHA cho
  từng gate. Review current diffs inline, chỉ sửa lỗi đã tái hiện trong scope.
- [ ] Báo local status cho owner, giữ working tree chưa commit/push. Không gọi
  GitHub workflow hoặc cập nhật ledger bằng run CI mới. Remote CI đã có chỉ được
  tham chiếu lịch sử, không phải quyền chạy tiếp.
