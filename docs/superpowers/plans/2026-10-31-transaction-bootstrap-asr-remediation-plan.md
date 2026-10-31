# Plan sửa transaction, bootstrap và ASR sau review Cursor

Ngày31/10/2026 UTC+7. Baseline `69c6815`, branch `feature/bao-chau-ui-audit`.
[Review SA01–SA03](../../feature-audits/2026-10-31-cursor-sentinel-acceptance-review.md).
[Prompt Cursor](../../../.ai/prompts/cursor-auto-transaction-bootstrap-asr-remediation.md).

## Scope và thẩm quyền

Lượt tạo plan chỉ review; sửa code lấy quyền từ chat thực thi owner. Một Cursor
Agent, Auto trong allowlist Grok/Composer2.5; không agent phụ. Giữ các quyền đã
giao trong cùng phiên. Không làm lại Writing normalization/publisher đã qua review
nếu không có regression mới. Không đổi package/target/provider, migrations đã
apply, auth/AI chung, native trust, Plesk/gate/production hoặc merge master.
Không copy DB local lên hosting, không dùng DB/volume dev cho acceptance.

Không cần migration cho ba finding này: chúng liên quan transaction, fixture và
adapter, không thay persisted model. Nếu phát sinh thay model, dừng mở rộng scope
và áp dụng tct-ef-migration/AGENTS.md; không sửa migration đã publish.

| Bước | Dependency | Điều kiện hoàn thành |
| --- | --- | --- |
| T0 | Không | Baseline + regression đỏ của3 finding |
| T1 | T0 | Transaction ownership/atomicity đúng, unit+provider evidence |
| T2 | T0 | PowerShell bootstrap không thể đổi identity DB khác |
| T3 | T0 | Silence marker không thành speech, engine contract evidence |
| T4 | T1/T2/T3 theo case | Nghiệm thu còn thiếu có actual/provider/artifact rõ |
| T5 | Các bước trên | Build/tests/CI/ledger đúng kết quả và blockers |

## T0 — Baseline và regression

- [ ] Ghi SHA/status; giữ thay đổi owner. Tạo ledger mới hoặc nối mục có ngày,
  không sửa review lịch sử thành đã luôn đúng. Mỗi case ghi provider, command,
  expected/actual, artifact và SHA. Xem probe ignored nếu còn nhưng tests bàn giao
  phải nằm trong repo và không phụ thuộc `.tmp` của người review.
- [ ] SA01: transaction do caller mở + lỗi1213 ở Goals/SaveChanges. Assert service
  tham gia không retry/query trên transaction chết; preserve exception và để owner
  retry cả unit. Thêm1205, cancel và exhausted-retry cases. Dùng fault injection
  xác định, không chỉ random concurrency hoặc sleep.
- [ ] SA02: thực thi script PowerShell thật với Docker runner giả. DB đã có
  sentinel foreign purpose/run, missing identity, label mismatch, port mismatch,
  native exit failure phải reject và không phát sinh write/false success.
- [ ] SA03: `[BLANK_AUDIO]` ở text/segments, empty text, noise marker hợp lệ,
  speech thật và mixed speech/marker. Assert status/transcript và assessment không
  cho commit khi không có lời nói; không dùng literal marker làm expected speech.

## T1 — SA01, P1: Retry ở chủ transaction

Files chính: `CompletionCreditService.cs`, `GoalsService.cs`, caller
`SpeakingController.cs`, `WritingService.cs`, regression tests liên quan.

- [ ] Xác định ranh giới unit: Speaking assessment consumption/progress/video
  completion/credit/Goals/outbox; Writing attempt/sentence/exercise progress/
  credit/Goals/outbox. Không retry chỉ phần đuôi sau khi phần đầu rollback.
- [ ] Khi credit service tham gia transaction, propagate contention để chủ unit
  rollback/dispose và retry toàn bộ với context/tracking state sạch. Khi service
  tự mở transaction, retry mỗi attempt bằng transaction mới và bounded policy.
  Có thể fail an toàn nếu chưa có whole-unit retry; không giả success/replay.
- [ ] Không swallow1213 ở Goals để increment tiếp trên transaction cũ. Phân biệt
  duplicate unique, deadlock và lock timeout; xét cấu hình rollback-on-timeout.
  Không che original exception bằng rollback/query trên transaction đã hoàn tất.
- [ ] Lỗi thật không bị đổi thành Replay. Replay phải dựa trên credit đã commit
  trong transaction/context hợp lệ. Cancellation thoát và cleanup đúng.
- [ ] MariaDB10.5.23 disposable: hai HTTP/real-service units Speaking và Writing,
  hai context/connections, fault/barrier kiểm soát. Assert persisted progress,
  receipt consumption, attempt/completion, daily XP/streak, một credit, outbox và
  reload; loser/retry không báo success với progress thiếu. Test standalone service
  vẫn cần nhưng không thay HTTP/caller transaction. Lặp race3–5 lượt sau fix.

## T2 — SA02, P2: Một contract cho bootstrap thực

Files: `scripts/bootstrap-mariadb-acceptance.ps1`, orchestration guard/harness và
script regression tests. Không chỉ sửa helper C# trong khi script vẫn DROP.

- [ ] Inspect container id/name, exact purpose/run labels, image/version, actual
  loopback binding3308–3399 và lifecycle ownership trước SQL. Validate/escape DB,
  user/password theo driver hoặc input contract; không log credential.
- [ ] Tạo mới phải xác nhận DB chưa tồn tại trong lifecycle đã sở hữu. DB đã có
  chỉ được reuse sau read-only identity verify. Không DROP/UPSERT sentinel để
  biến foreign/missing identity thành target được reset. Schema sentinel cũ cần
  xử lý bằng DB ephemeral mới, không tự sửa identity dữ liệu cũ.
- [ ] Check `$LASTEXITCODE` cho docker inspect/exec và client. Native fail phải
  terminate nonzero; không in Bootstrapped. Password chỉ dùng local private env/
  secret; không commit mặc định credential sử dụng lại cho đội.
- [ ] Negative actual-script tests, positive new DB, rerun same run và reject
  foreign run. Sau đó mới bootstrap provider fixture. Cleanup chỉ đúng owned
  resource; không đoán path/container theo prefix rồi delete.

## T3 — SA03, P2: Contract engine qua adapter và assessment

Files: `WhisperHttpTranscriber.cs`, speech/assessment tests và ASR evidence notes.

- [ ] Dựa response của binary/model đã pin: marker-only không phải lời nói;
  text/segments sau chuẩn hóa đều rỗng thì NoSpeech. Không chỉ dựa một ngưỡng
  no_speech_prob áp dụng mù cho mọi câu; giữ speech thật và mixed input hợp lệ.
- [ ] Giữ invalid-schema/error khác silence. Đảm bảo limiter/timeout/cancel vẫn
  nhả slot và không đổi các lỗi HTTP thành transcript giả.
- [ ] Khi host cho phép, dùng loopback engine sẵn có, model<=500MiB đã có hash,
  WAV synthetic/licensed. Lưu raw response đã loại PII và replay fixture đúng
  bytes/schema. Đo cold/warm/memory; silence phải qua adapter/assessment thật.
- [ ] WAV đúng câu→assessment→receipt→progress→credit/reload; wrong sentence,
  silence, unavailable/timeout/cancel không thưởng. Không chỉ curl HTTP200.
- [ ] Nếu auto-review chặn khởi chạy engine, ghi action/lý do; không bypass bằng
  wrapper/tiến trình khác. Vẫn hoàn thành unit/contract tests và các việc độc lập.

## T4 — Khép acceptance theo bằng chứng, không chỉ checkbox

- [ ] G03: notification450 recipients/dedup/checkpoint/stale lease, import
  fingerprint/retry/hash/Premium/atomic publish, outbox competing drain phải chạy
  MariaDB thật. Các test còn dùng TestWebApplicationFactory là SQLite; ghi riêng
  và port cần thiết sang fixture provider mà không đụng DB dev/hosting.
- [ ] G04: tạo bcrypt user/role synthetic **tại local acceptance**; không cần xin
  tài khoản thật của owner. Chạy25 view gốc AU01–06/CO01/SP01–03/WR01–03/GO01/
  AI01–02/AD01–09 desktop/mobile, keyboard và loading/empty/error/race states.
  HTTP302 chưa login không chứng minh UI authenticated đã đạt.
- [ ] G06: tìm client backup local hoặc chuẩn bị client Windows official có
  pin/hash/license, harness an toàn cho Windows staging. Không cần cài Plesk thật
  chỉ để có mariadb-dump. Không fake dump/return0. Dùng production logic để test
  enabled backup→migration→copy→readiness và lỗi/maintenance/marker/concurrent/
  rerun; restore vào DB riêng kiểm dữ liệu/history. Nếu thiếu tool/quyền thật,
  ghi blocked thay Done, tiếp tục phần độc lập.
- [ ] Giữ app5100/volume owner; acceptance riêng5110/DB riêng. Docker daemon hiện
  chưa chạy không phải bằng chứng provider đã fail hay pass; ghi môi trường thực.
  Không dừng workload khác để setup. Không bật production gate.

## T5 — Verify và handoff

- [ ] Build app + focused regressions + full suite. Bảo đảm review-only targets
  không còn inject vào bin chuẩn; các regression bàn giao có trong tracked suite.
- [ ] Python/Windows5.1 script tests; tooling validator và Python suite nếu cần.
  `has-pending-model-changes` dùng repo-local EF tool; không auto migrate hosting.
- [ ] Mỗi gate Done phải có command/provider/actual assertions/artifact/SHA;
  missing input, blocked, skipped, partial ghi riêng. Không gọi ASR E2E vì curl,
  provider matrix vì SQLite xanh hoặc UI25 vì login screenshot.
- [ ] Nếu chat giao commit/push/CI: commit theo nhóm transaction, bootstrap,
  speech, acceptance/docs; push feature và CI đúng runtime SHA. Kiểm actual run
  và skip, gatefalse, release skipped. Không merge hoặc deploy production.
- [ ] Cuối cùng báo SA01–03 đã đóng bằng gì, gate nào còn thiếu và đúng đầu vào;
  không trả plan tiếp thay cho phần local đã được giao và còn thực thi được.
