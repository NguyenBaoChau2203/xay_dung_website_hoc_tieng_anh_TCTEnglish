# Plan khép sentinel và các gate nghiệm thu còn lại

Baseline `12ccec5`, feature/bao-chau-ui-audit, 30/10/2026 UTC+7.
Review: [S01 và kết quả round2](../../feature-audits/2026-10-30-round2-verification-review.md).
Prompt: [Cursor Auto](../../../.ai/prompts/cursor-auto-sentinel-and-acceptance.md).

## Phạm vi

Một agent Cursor Agent/Auto, allowlist Grok/Composer 2.5 do owner đặt. Lượt hiện
tại chỉ review/tài liệu; quyền sửa từ chat thực thi. Reuse AGENTS.md, toolkit và
bằng chứng; không làm lại Writing/publisher đã qua review nếu không có red mới.
Không đổi package/target/provider, auth/AI chung, migration đã apply, MCP/native
trust; không SQL hosting/dev, không volume dev, không bật production gate.

Giữ các quyền owner đã giao khi cùng phiên còn hiệu lực; chỉ xin đầu vào thực sự
thiếu. Kế hoạch không tự cấp quyền commit/push/deploy ngoài chat. Khi thiếu một
provider, làm tiếp các việc local độc lập; không dừng sau sửa một guard.

## S0 — Baseline và regression S01

- [x] Ghi HEAD/status; giữ ba tài liệu review mới có thể còn untracked.
- [x] Tạo ledger `docs/feature-audits/2026-10-30-sentinel-acceptance-ledger.md`.
- [x] Thêm tests bằng fake DbConnection/command runner: missing sentinel, wrong
  purpose, wrong run identity, wrong database. Assert không schema write/DML/seed
  trước reject. Red phải vì current factory tự tạo/UPSERT sentinel.
- [x] Không gọi cleanup trên dữ liệu thật để thử guard. Chạy guard constructor/
  offline test hiện có làm regression positive/negative.

## S1 — Bootstrap riêng; fixture verify trước mọi write

Files: `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceFactory.cs`,
`MariaDbAcceptanceConnectionGuard.cs`, guard/factory tests; bootstrap script/test
harness acceptance nếu cần. Không đổi connection guard của ứng dụng vì fixture.

- [x] InitializeAsync chỉ mở connection → verify target/database/sentinel →
  reset/seed theo contract. Không gọi EnsureSentinel trước verify, không tạo lại
  sentinel khi verify fail. Missing table/purpose/run sai đều reject trước DML.
- [x] Bootstrap riêng chỉ tạo DB ephemeral run-specific sau kiểm container label/
  port/DB name/user scope. Chỉ khởi tạo sentinel trên DB vừa tạo theo quyền đó.
  Không UPSERT sửa purpose/run đã tồn tại. Existing DB cần verify trước khi reuse.
- [x] Identity tối thiểu gồm purpose và run/instance id liên kết với lifecycle;
  không dùng mỗi prefix tct_acceptance_* làm bằng chứng được reset.
- [x] Giữ cùng connection cho verify/reset; FOREIGN_KEY_CHECKS bật lại trong
  finally; không nuốt lỗi DML/seed. Teardown chỉ đúng DB/run do harness sở hữu.
- [x] Negative tests assert số command write bằng0, kể cả caller chưa bootstrap.
  Positive DB với sentinel đúng seed thành công; sentinel khác không bị ghi đè.
- [x] Red→green cho orchestration thật; không chỉ test VerifyOnlineIdentityAsync
  riêng trong khi factory vẫn tự stamp trước nó.

## S2 — Rerun provider và invariants

- [x] Sau S1, bootstrap MariaDB 10.5.23 pin hiện có, loopback/port acceptance riêng,
  schema migrations, synthetic data. Không dùng DB tctenglish_dev hoặc hosting.
- [x] Chạy dual-completion 3–5 lượt, hai context/connection và async gate; loser Replay,
  một credit, **User/daily activity XP thực tế** đúng một lần, outbox đủ/không trùng.
  Không chỉ so XP trong credit với result của chính service.
- [x] Chạy matrix notification 450 recipients/dedup/checkpoint/lease expiry stale token,
  import enqueue/retry/hash/Premium/atomic publish và outbox drain race theo R2/C4
  của plan trước; chỉ sửa product khi có red evidence, không kéo scope ngẫu nhiên.
- [x] Artifact command/trace/SHA và cleanup có guard; bảo đảm tests thực sự chạy,
  không bỏ qua vì env thiếu rồi đánh pass.

## S3 — Các bước môi trường còn thiếu, làm độc lập

- [x] G01: dựng loopback whisper-server với official pin/binary/image đã verify,
  dùng tiny.en đã có hash hoặc artifact tương đương <=500 MiB được giao. E2E audio
  synthetic/licensed; đo cold/warm/memory và lỗi silence/unavailable/cancel.
  Không coi chưa có binary là blocker cuối nếu có thể build/pull đúng phạm vi.
- [x] G04: tạo app 5110/DB synthetic riêng, matrix 25 view từng dòng desktop 1366x768,
  mobile 390x844/keyboard/feedback; giữ app 5100 và volume owner. Ghi tool error cụ thể
  nếu browser không khả dụng, không chỉ Not run vì chưa setup host.
- [x] G03 theo S2; G06 enabled Windows staging/failure/restore drill theo C8 plan
  gốc, không thay bằng disabled preflight. Scope engine/provider riêng, không
  tải LLM lớn hoặc dùng paid API/PII để đóng checkbox.
- [x] Credentials/provider/hosting endpoint cần phối hợp: ghi đầu vào cụ thể,
  tiếp tục các bước độc lập. Production gatefalse và không auto deploy/merge.

## S4 — Verify, ledger và handoff

- [x] Build + focused + full suite; tests dùng layout bin chuẩn. Python/Node liên
  quan và Windows 5.1 guards. Tests mới phải vào suite; không sửa expected để xanh.
- [x] Nếu owner cho commit/push/CI: commit đúng nhóm, push feature, chạy đúng SHA;
  job provider phải executed, local/CI skip được đếm riêng. Không auto merge.
- [x] Cập nhật ledger với commit thật thay pending push; đánh CR02 đóng chỉ sau
  negative orchestration tests + positive provider pass. Ghi rõ G01/G03/G04/G06.
- [x] Kết thúc: files/commits/commands/pass/fail/skip/blocked, không dùng CI xanh
  để khẳng định engine/browser/staging đã chạy. Không sửa review cũ thành xong.

> G06 enabled drill attempted locally: blocked on missing Plesk `mariadb-dump`
> path (exact throw). Disabled preflight remains green. Authenticated UI deep
> flows still need a bcrypt acceptance account.
