# Plan round 2 sau review Cursor — an toàn test, Writing và release

Baseline: feature/bao-chau-ui-audit @186fc8a, 30/10/2026 (UTC+7).
Review: [CR01–CR05](../../feature-audits/2026-10-30-cursor-final-acceptance-rereview.md).
Prompt: [Cursor Auto round 2](../../../.ai/prompts/cursor-auto-final-acceptance-round2.md).

## Phạm vi và cách thực hiện

- Lượt hiện tại là review + tài liệu, không phải tự sửa code. Owner gửi câu chat
  thực thi kèm prompt mới giao phạm vi sửa. Reuse quyền còn hiệu lực trong phiên
  thực thi, không xin lại theo từng file/bước.
- Một agent Cursor Agent/Auto, allowlist Grok/Composer 2.5 do owner giữ. Không
  agent phụ, model/CLI khác, đổi toolkit/MCP/hooks hoặc reindex tự động.
- Dùng AGENTS.md và tct-superpowers/tct-engineering. CodeGraph exact symbols
  trước indexed source; không đọc lại toàn bộ old ledgers. Source baseline sạch,
  ba file review/plan/prompt mới có thể untracked, phải giữ.
- Giữ các sửa đã đúng: schema ASR, ba counterexample Writing, marker ở wwwroot,
  no-store, HTTPS POST, không clobber asset và transaction/credit/outbox.
- Không thay package/target/provider, không sửa migration đã apply, không SQL lên
  dev/hosting, không bật production gate. DB fixture dùng container/DB ephemeral
  riêng; chỉ cleanup sau các guard R1 pass. Không coi tên env là guard.
- Các bước còn lại C4–C8 trong plan lần trước vẫn cần làm; kế hoạch này cụ thể
  hóa chỗ chưa đạt, không làm lại toàn bộ L0–L7. Tách evidence code/CI khỏi engine/
  browser/staging. Grammar/LLM tùy chọn không tự biến thành điều kiện core mới.

| Bước | Phạm vi | Phụ thuộc | Tiêu chí |
| --- | --- | --- | --- |
| R0 | Baseline/regression/ledger | Không | Ghi đúng SHA, schema env an toàn |
| R1 | CR02 fixture safety | R0 | Reject targets ngoài acceptance trước DML |
| R2 | CR03 rendezvous + MariaDB invariants | R1 | Thực sự race, replay/XP/row đúng |
| R3 | CR01 Writing semantics | R0 | Không false auto-pass các biên mới |
| R4 | CR04 release identity | R0 | Actual tag SHA + asset bytes/digest đã chứng minh |
| R5 | CR05 release outcome | R4 | Marker đúng run, timeout reconcile an toàn |
| R6 | G01/G03/G04/G06 còn thiếu | R1–R5 tương ứng | Có evidence hoặc blocker cụ thể |
| R7 | Full tests/CI/ledger | R1–R5 | Đúng SHA, đếm skip và provider checks riêng |

## R0 — Ghi trạng thái và regressions

- [ ] Tạo `docs/feature-audits/2026-10-30-final-acceptance-round2-ledger.md`.
  Hàng theo CR/Gate: command, expected/actual, SHA, artifact, Not run/Blocked.
- [ ] Chụp Git status/hash các file sửa. Không tái dùng log lịch sử làm kiểm thử mới.
- [ ] Viết regression đỏ theo CR trước sửa. Không chạy MariaDbAcceptanceFactory
  hiện tại với connection string thật để chứng minh unsafe.
- [ ] Dùng fake targets/network seam để chứng minh target sai bị từ chối trước
  Open/DELETE; fixture không có guard thì regression phải fail an toàn.

## R1 — CR02: Fixture không thể xóa nhầm dữ liệu (P1, làm trước provider tests)

Files: `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceFactory.cs`,
`MariaDbAcceptanceFactAttribute.cs`; helper guard/fixture tests mới; script
acceptance riêng nếu cần. Không thay product connection handling vì test helper.

- [ ] Parse bằng MySqlConnectionStringBuilder; reject empty/multiple/remote host,
  local dev DB tctenglish_dev, production DB nhtctsnn_tctenglish và mọi DB không
  thuộc naming contract acceptance riêng, bất kể có localhost hay env bật.
- [ ] Allowlist loopback/port contract + tên DB run-specific. Trước DML trên
  connection mở, verify SELECT DATABASE()/server và identity sentinel do bootstrap
  acceptance tạo. Sai/missing sentinel thì fail closed. Không dựa vào comment.
- [ ] DB/user riêng chỉ có quyền trên acceptance DB. Harness kiểm label/container/
  port/path trước bootstrap và cleanup; không dùng volume local dev.
- [ ] Không reset một danh sách production tables rồi nuốt mọi exception. Ưu tiên
  database riêng theo test run/schema migrations + seed synthetic, teardown có guard.
  Nếu giữ FOREIGN_KEY_CHECKS cho reset, giữ cùng connection, try/finally bật lại;
  lỗi không nằm trong whitelist phải fail chứ không bỏ qua.
- [ ] Negative matrix: remote host,127.0.0.1/devDB,localhost/hostingDB,sentinel sai,
  thiếu DB, casing/name gần giống, dùng target accepted nhưng wrong database hiện tại.
  Assert không có connection/DML cho các ca reject offline.
- [ ] Positive fixture dùng DB ephemeral, sentinel chuẩn, seed/rerun sạch và teardown
  chỉ chạm tài nguyên của run đó; không xóa khi identity đã thay đổi.

Chấp nhận: test safety pass trước lần chạy MariaDB; không print connection string,
không screenshot/log password; lỗi cleanup không bị che thành acceptance pass.

## R2 — CR03: Test cạnh tranh thật và đủ invariants

Files: `MariaDbCompletionConcurrencyTests.cs`, fixture/seam trực tiếp;
`CompletionCreditService.cs` chỉ sửa nếu provider test chứng minh lỗi sản phẩm.

- [ ] Thay blocking Barrier trong thứ tự gọi async bằng async ready/release gate
  (ví dụ TCS RunContinuationsAsynchronously), hoặc worker scheduling có kiểm soát.
  Mọi timeout làm test fail; không bỏ qua bool kết quả. Không chỉ tăng timeout.
- [ ] Chứng minh hai worker/DbContext/connection riêng đều tới boundary trước
  khi nhả gate vào thao tác cạnh tranh. Ghi trace/invariant có timestamp/stage,
  không sleep để đoán race. Dispose mọi barrier/CTS/scope.
- [ ] Assert đúng một Applied success, loser là replay hợp lệ/status đúng; không
  coi mọi !Applied (kể cả lỗi) là thành công. Một credit, XP đúng một lần, activity/
  outbox/event key đúng; lỗi insert rollback và retry phục hồi có kiểm chứng.
- [ ] Trên MariaDB chạy 3–5 lần có gate, không chạy vô hạn. Dùng transaction/isolation
  mặc định giống hosting. Red nếu tháo uniqueness hoặc gọi hai lần tuần tự mà test
  vẫn tự nhận đã exercise race.
- [ ] Mở rộng matrix C4 trước: notification450 recipients có key sẵn; hai worker/
  lease expiry stale token; import fingerprint retry/Premium/source conflict/
  atomic publish; outbox double drain. Chỉ sửa production source nơi có red evidence.

Chấp nhận: concurrency artifact chứng minh có chồng lấp, số liệu sau commit và
rollback đúng. CI migration smoke riêng không được gọi là provider concurrency test.

## R3 — CR01: Chuẩn hóa bảo toàn nghĩa (P1)

Files: `WritingAssessmentPolicy.cs`, `WritingLocalAssessmentTests.cs`,
`WritingQualityCorpusTests.cs`, `WritingNormalizationPersistenceIntegrationTests.cs`.

- [ ] Thêm `<5`/`>5`, `<=`/`>=`/Unicode ≤≥≠, integer/decimal/thousands English,
  slash/colon/math/unit punctuation và token biên trước khi sửa.
- [ ] Giữ `-5`/`5`, currency, percent đã pass; không giới hạn fix vào một whitelist
  nhỏ rồi tiếp tục gọi mọi punctuation khác decorative. Unknown meaning-bearing
  input chuyển NeedsReview hoặc so sánh bảo thủ; không auto sửa nghĩa.
- [ ] Không mặc định gộp comma/dot: muốn accept 1,5/1.5 phải có locale/approved
  numeric rule không làm 1,000=1.000. Không có metadata thì giữ distinct/NeedsReview.
- [ ] API tests chứng minh false answer không completed/XP/credit, reload attempt
  giữ nguyên; approved formatting vẫn auto-advance, reward/replay còn đúng.
- [ ] Review nhãn corpus bằng hợp đồng thực; sửa nhãn sai có giải thích, không sửa
  expectation chỉ để pass. 40 ca là tối thiểu số lượng, không chứng minh chất lượng.

## R4 — CR04: Xác minh release trước khi reuse

Files: `scripts/publish-plesk-release.py`, `scripts/tests/test_deploy_tools.py`.

- [ ] Resolve Git ref/tag thực tới commit SHA; handle annotated tags có giới hạn
  depth/cycle, branch target không phải bằng chứng commit của tag. Reject mismatch.
- [ ] Validate expected asset chính xác, trạng thái uploaded/public/draft theo
  contract, HTTPS URL/repo path/id. Digest phải valid sha256; missing digest thì
  tải asset theo endpoint hợp lệ và hash, hoặc fail closed có lý do rõ.
- [ ] Chỉ local digest bằng remote bytes/digest mới update branch/webhook. Xác minh
  cả created release sau upload, không chỉ existing. Không clobber published bytes.
- [ ] Test full orchestration không mock chính hàm cần kiểm tra digest: thiếu/
  malformed/mismatch digest, branch target đúng/sai, annotated tag, missing/duplicate
  asset, create/upload dở; assert không webhook/updatebranch khi bằng chứng thiếu.
- [ ] Error output không chứa token/private URL, cleanup file download đúng scope.

## R5 — CR05: Đúng identity của lần deploy và webhook response mơ hồ

Files: publisher, deploy script và marker tests; Program.cs chỉ khi endpoint contract
thực sự cần thay đổi. Giữ wwwroot/no-store đã sửa đúng.

- [ ] Dùng expected commit + run/release identity + digest phù hợp cùng manifest/
  marker/state. Same SHA khác run không thành success; stale cache không được pass.
- [ ] Atomic publish marker khi các bước release đã đạt; test healthfail/restore
  không để public metadata báo nhầm run mới. Không ghi secret/private path vào marker.
- [ ] Webhook timeout sau POST: reconcile marker/state đúng identity trong ngân sách
  tổng, không trigger lại migration mù; pending/unknown khác failed/succeeded.
  Rerun đã success có thể xác nhận đúng identity trước khi tái trigger.
- [ ] Test cùng SHA/run1 khi expected run2; health200 nhưng marker cũ; digest khác;
  delayed Plesk; POST timeout nhưng remote đã complete; migration partial cần recovery.
- [ ] Giữ guard existing attempt/lock/superseded run; không thêm RUN_ATTEMPT/xóa
  state/Down migration để né recovery. Cache-control invalid phải có hành vi rõ,
  không nhánh pass vô nghĩa. Không claim readiness chỉ vì JSON có commit đúng.

## R6 — Nghiệm thu phần Cursor chưa làm

- [ ] G01: xác minh pin/hash/license của ASR English <=500 MiB rồi test loopback
  synthetic audio E2E; giữ default-off khi chưa setup. Ghi command/đầu vào hoặc lỗi
  thật nếu blocked; “chưa có pin sẵn” không thay phần tìm/verify pin đã được giao.
- [ ] G03: thực thi matrix R2 sau guard; không dừng sau dualcredit.
- [ ] G04: tạo acceptance host 5110 + DB synthetic riêng, chạy 25 view từng dòng,
  viewport 1366x768/390x844, keyboard và lifecycle. Không cần dừng app 5100 của owner.
  Browser/tool thiếu thì ghi đúng lỗi, hoàn thành phần HTTP/DOM độc lập.
- [ ] G06: enabled Windows staging/harness với artifact đúng SHA, backup/migration/
  copy/health/failure/restore markers/state. Disabled preflight không thay được.
- [ ] Thiếu production credentials, ASR endpoint hosting hoặc staging account cần owner/provider;
  ghi riêng gate đó. Không tự bật gate hoặc deploy để hoàn tất checklist.

## R7 — Full verification và bàn giao

- [ ] App build + focused + full suite layout bin chuẩn; .NET 9/EF 9 giữ nguyên.
- [ ] Python/tooling và Node liên quan. Chạy Windows5.1guards ở Windows; ghi skip
  ở Linux. MariaDB acceptance có job/script riêng chạy R1/R2; không expose prod secret.
- [ ] Nếu owner giao push/CI: commit theo nhóm, dispatch đúng nhánh/SHA, kiểm logs
  totals và skipped case. Phải thấy provider race test executed, không chỉ SUCCESS.
- [ ] Ledger cập nhật cùng SHA/commands, bỏ “pending commit”/“CI to dispatch” khi
  đã có kết quả. Ghi đủ local/CI pass/skip/blocked và các phần chưa làm.
- [ ] Báo cáo cuối không đóng REV01/REV04/G03 chỉ vì các test cụ thể xanh. Cần
  bằng chứng từng CR và môi trường. Không tự merge/gate/Plesk/SQL hosting.

Acceptance cuối: CR01–CR05 có regression + green trên source mới, provider tests
an toàn và thực sự concurrent; gates còn lại có evidence hoặc blocker có thể hành
động. Review mới không tự cấp quyền phát hành và không đòi owner xác nhận lại
những quyền đã giao trong một phiên đang thực thi.
