# Kế hoạch sửa và nghiệm thu phần việc 29–30/10 — Cursor Auto

Ngày 30/10/2026, UTC+7. Baseline review: `feature/bao-chau-ui-audit`, `54f0c14`.
Review: [REV01–REV04 và G01–G06](../../feature-audits/2026-10-30-final-acceptance-review.md).
Prompt: [Cursor Auto](../../../.ai/prompts/cursor-auto-final-acceptance-remediation.md).

## 1. Cách làm việc và thẩm quyền

- Một agent trong Cursor Agent mode, model selector **Auto** theo cấu hình owner
  chỉ cho Grok/Composer 2.5. Không ép Grok 4.7 High từ prompt cũ, đổi allowlist,
  gọi agent/CLI AI khác hoặc dùng reviewer riêng. Không tự đoán model Auto chọn.
- Lượt tạo tài liệu hiện tại chỉ review/plan. Câu chat thực thi ở cuối prompt,
  khi owner gửi vào chat mới, là phạm vi giao việc; tài liệu không tự cấp quyền.
- Làm C0–C8, mỗi lần nạp đúng mục đang làm và source cần thiết. Kết thúc bước
  ghi ledger rồi tiếp tục bước độc lập kế tiếp, không hỏi lại quyền đã có.
- Dùng AGENTS.md/.ai/README.md hiện hành, tct-superpowers + tct-engineering;
  CodeGraph trước cho indexed source, bounded reads cho gap/config/docs. Không
  tự init/full rebuild; giữ nguyên checkout-local opt-in và native trust.
- Không làm lại RR01–RR07 chỉ vì ledger cũ ghi chưa xong. Nếu finding đã được
  sửa ở HEAD mới hơn, kiểm tra regression và ghi bằng chứng thay vì sửa trùng.
- Giữ net9.0 / EF 9.0.4 / Pomelo 9.0.0; tool EF local. Không nâng framework,
  packages/model lớn, redesign toàn bộ UI hay đổi provider để hoàn thành plan.
- Các đường dẫn Services/Workers/Controllers bên dưới tương đối với TCTEnglish/;
  tên file test C# nằm trong TCTEnglish.Tests/. scripts/docs/.ai dùng repo root.
- Source sửa: các file trong từng bước dưới đây và phụ thuộc trực tiếp có bằng
  chứng. Program.cs chỉ endpoint marker/readiness và DI/options liên quan, nếu
  owner đã giao rõ bằng câu chat. Không thay auth/seeding/config AI chung.
- `.csproj` chỉ metadata fixture/content test thật sự cần thiết nếu được giao;
  không đổi target/package. Không ghi secret vào appsettings; local dùng env/User
  Secrets. Migration mới chỉ additive nếu schema cần thay đổi thực sự, kèm cùng PR.
  Các migration tháng 10 đã commit/apply local: không sửa lại để né lỗi.
- Docker local cô lập/loopback và fixture tổng hợp được phép khi owner gửi câu
  chat. Không đụng volume dev hiện có, workload Docker khác hoặc hosting thật.
- Chỉ commit/push/dispatch CI nếu câu chat giao quyền đó; không auto merge,
  bật gate, sửa Plesk hoặc deploy production. Credential rotation là thao tác
  provider có phối hợp, không suy ra từ quyền sửa code.

## 2. Thứ tự và tiêu chí cuối

| Bước | Nội dung | Phụ thuộc | Đầu ra |
| --- | --- | --- | --- |
| C0 | Baseline + tái hiện | Không | Ledger, tests đỏ đúng finding |
| C1 | REV01 Writing | C0 | Không auto-pass số/dấu đổi nghĩa |
| C2 | REV03 ASR schema | C0 | invalid_schema đúng, không 500/NoSpeech giả |
| C3 | REV02/04 release marker + rerun | C0 | Marker HTTP đúng và retry idempotent có guard |
| C4 | G03 MariaDB | C1/C2 cho completion; phần worker có thể chạy riêng | Race/crash invariants trên provider thật |
| C5 | G01/G02 engine và corpus | C1/C2 | Bằng chứng ASR thực và chất lượng Writing |
| C6 | G04 UI 25 view | Các thay đổi nghiệp vụ liên quan đã ổn | Matrix desktop/mobile/keyboard |
| C7 | G05 full suite + Linux CI | C1–C4; không đợi provider nếu thiếu | Đúng SHA, test mới không bị bỏ |
| C8 | G06 local release drill + bàn giao | C3/C7 | Recovery evidence, phần provider tách rõ |

Hai mức nghiệm thu: **code/local** (REV01–04 + automated provider/UI/engine checks
trong phạm vi có môi trường) và **production** (staging thật + credentials + owner
cho bật gate). Không ghi “hoàn thành tất cả” nếu còn hàng Blocked/Not run. Engine
LLM tùy chọn không được cài lớn chỉ để xóa một dòng Not run; ghi lựa chọn rõ.

## C0 — Baseline, bằng chứng và regression trước sửa

- [ ] Ghi branch/HEAD/status và initial file hashes; bảo toàn ba tài liệu review
  mới có thể còn untracked. Không reset/stash để làm sạch thay đổi của owner.
- [ ] Tạo `docs/feature-audits/2026-10-30-final-acceptance-execution-ledger.md`.
  Mỗi hàng: ID, file, trạng thái, command, expected/actual, SHA, artifact, giới hạn.
- [ ] Reuse review và kết quả cùng tree đã có; chỉ tái hiện finding đang sửa.
  Probe ignored có thể không được chuyển sang máy khác; không phụ thuộc chúng.
- [ ] Viết regression thật trong test project/scripts cho REV01–04, chạy đỏ
  vì hành vi lỗi tương ứng, không vì path/package/config thiếu.
- [ ] Baseline 620 tests là mốc tham chiếu, không phải số lượng phải giữ cố định.
  Không dùng `--artifacts-path` cho test fixture suy source bằng layout bin chuẩn.

## C1 — REV01: Writing giữ ý nghĩa khi chuẩn hóa (P1)

Files: `Services/Writing/WritingAssessmentPolicy.cs`, caller cần thiết trong
`Services/WritingService.cs`; `WritingLocalAssessmentTests.cs`,
`WritingPracticePersistenceIntegrationTests.cs`, `GoalsCompletionRecoveryTests.cs`
hoặc integration test mới tập trung vào evaluate và completion.

- [ ] Thêm theory cho -5/+5/5, ký tự minus Unicode, $/€/£, %, số thập phân,
  đơn vị, phủ định và case/whitespace/formatting đã được duyệt.
- [ ] Chọn quy tắc bảo toàn token số/dấu/đơn vị; chỉ bỏ punctuation không mang
  nghĩa trong trường hợp có cơ sở. Không coi mọi Symbol/Punctuation là khoảng
  trắng. Khi chưa chắc tương đương: NeedsReview, không Accepted.
- [ ] Giữ chính sách paraphrase chưa có variant được duyệt → NeedsReview;
  không keyword-coverage pass, không nhờ LLM tự tạo đáp án rồi tự cấp điểm.
- [ ] Regression gọi evaluate thật với fixture synthetic chứng minh 3 ca trong
  review không tăng completed count/XP/credit. Reload vẫn thấy attempt/review.
- [ ] Positive control: đáp án chuẩn vẫn Accepted; hoàn thành hợp lệ thưởng một
  lần; retry và credit-insert-failure rollback vẫn đúng.
- [ ] Nếu thay expectation cũ, giải thích case cũ sai hợp đồng ở đâu; không xóa
  assertion/test bảo mật chỉ để xanh. Không sửa dữ liệu/XP lịch sử tự động.

Chấp nhận: ba counterexample đều không passed/canAutoAdvance; unit + endpoint
chứng minh ảnh hưởng persistence; default-off cloud/engines vẫn không HTTP.

## C2 — REV03: Contract ASR và lỗi có cấu trúc (P2)

Files: `Services/Speaking/WhisperHttpTranscriber.cs`, `SpeechProcessingOptions.cs`
nếu cần validation; `SpeakingAssessmentIntegrationTests.cs`,
`SpeakingInferenceLimiterTests.cs` hoặc `WhisperHttpTranscriberTests.cs` mới.

- [ ] Validate root object, text/segments, từng element và kiểu string/number
  trước GetString/TryGetDouble. Kiểm tra số hữu hạn và start <= end khi có timing.
- [ ] Matrix: malformed JSON; {}, error object, null/array root; text number/
  object/null; segments sai kiểu; item không object; timing sai kiểu; response
  hợp lệ có text, segment-only nếu contract thực hỗ trợ, text rỗng/silence hợp lệ.
- [ ] Sai schema → Unavailable/invalid_schema; silence hợp lệ → NoSpeech.
  Endpoint trả lỗi có code, không 500 cho các response sai schema dự kiến.
- [ ] Phân biệt caller cancellation với timeout tổng; slot/queue được nhả kể cả
  parse lỗi. Sau invalid response, request hợp lệ tiếp theo vẫn vào được.
- [ ] Không dùng catch mọi Exception để nuốt bug; không log raw audio/transcript/
  payload riêng tư. Kiểm tra config URL/path khi cần, không nhận URL/model từ client.
- [ ] Giữ 409 assessment_required/invalid, ownership/CSRF, 429 + Retry-After,
  server-authoritative scores và Web Speech preview-only.

Chấp nhận: body REV03 không ném exception ra caller; error object không NoSpeech;
không tạo assessment/progress/XP cho engine failure; regression limiter vẫn pass.

## C3 — REV02/REV04: Marker và rerun phát hành (P1/P2)

Files: `scripts/deploy-plesk.ps1`, `scripts/publish-plesk-release.py`,
`scripts/tests/test_deploy_tools.py`, readiness/static-file integration test;
`Program.cs` chỉ nếu endpoint được chọn cần sửa; runbook Plesk.

- [ ] Thống nhất marker được ghi và phục vụ HTTP: ưu tiên file trong wwwroot
  hoặc endpoint hẹp đọc metadata, không mở static toàn content root.
- [ ] Marker chỉ có commit/run identity/digest cần thiết, không có path private,
  DB name/password/webhook/token. Chống stale cache; publisher so đúng release
  identity và readiness, không chỉ tin HTTP 200 hay marker commit cũ.
- [ ] Định nghĩa lúc marker thể hiện thành công và recovery khi backup/migration/
  copy/health lỗi. Không để marker bản mới tồn tại như thành công sau phục hồi app cũ.
- [ ] Test qua HTTP host thật/fixture: marker trả JSON đúng; path ghi sai không
  được âm thầm chấp nhận; stale marker/commit hoặc run khác không pass readiness.
- [ ] Publisher kiểm tra release/tag/asset đã có trước create. Chỉ reuse khi
  resolve đúng SHA và digest/identity; mismatch thì dừng rõ nguyên nhân, không
  `--clobber`, đổi/xóa published tag/asset hoặc force branch để vượt lỗi.
- [ ] Rerun cùng RUN_ID: release có sẵn + webhook lỗi trước khi deployment bắt
  đầu có thể tiếp tục an toàn; nếu marker/readiness đúng thì báo đã hoàn tất.
  Timeout với trạng thái hosting chưa rõ phải reconcile/poll hoặc yêu cầu operator
  kiểm tra, không đoán chưa migrate và phát lại mù.
- [ ] Không thêm RUN_ATTEMPT để né guard Plesk: runNumber/lock/existing attempt
  và migration-partial recovery phải nhất quán. Không auto Down/restore DB.
- [ ] Offline API fixtures: release chưa có; đã có đúng SHA/digest; sai digest;
  create/upload dở; webhook timeout; host đã đúng commit; retry bị chặn vì recovery.
- [ ] Giữ HTTPS POST, certificate verification, private backup/config, checksum,
  traversal guards, không gỡ maintenance do operator tạo trước.

Chấp nhận: rerun không chết vì tạo lại release hợp lệ; không phát hành nhầm asset;
marker kiểm được qua HTTP; có tests state transitions, không chỉ grep source.

## C4 — G03: MariaDB nhiều connection, lease và recovery

Files liên quan: `Services/Learning/CompletionCreditService.cs`,
`Services/Notifications/LearningOutboxDispatcher.cs`, `Workers/NotificationWorker.cs`,
`Workers/SpeakingImportWorker.cs`, `SpeakingImportJobService.cs`; chỉ sửa khi test
chứng minh lỗi. Thêm fixture/provider tests và script chạy cô lập nếu cần.

- [ ] Dùng MariaDB 10.5.23 pin hiện có, lower_case_table_names=1. Container/DB
  acceptance riêng, loopback, tên/label/identity được kiểm tra trước SQL/dọn dẹp.
  Không dùng volume tctenglish-local-mariadb-data hoặc DB hosting/dev hiện tại.
- [ ] Synthetic fixtures; mỗi task/context có connection riêng. Dùng barrier/
  TimeProvider/fault injection để đồng bộ, không sleep tùy tiện rồi gọi là race.
- [ ] Hai completion đồng thời cùng user/content: một credit, XP một lần;
  lỗi lưu credit không lưu completed; retry hợp lệ khôi phục đúng.
- [ ] Hai notification worker, 450 recipients có một key sẵn: đủ người nhận,
  không trùng. Lỗi DB giữa batch không tăng cursor vượt phần chưa lưu.
- [ ] A hết lease, B claim, A quay lại: A không publish/chốt/checkpoint bằng token
  cũ. Restart tiếp tục từ checkpoint; xác minh kiểu datetime/collation/SQL update
  của MariaDB, không suy từ SQLite.
- [ ] Hai enqueue/import cùng fingerprint; failed/cancelled retry; result bị xóa;
  hash khác không ghi đè; Premium bị thu hồi; crash giữa video và job commit.
- [ ] Hai dispatcher drain cùng event: notification một lần; lỗi DB khác unique
  giữ retry/failed; occurrence UTC và business date UTC+7 đúng qua nửa đêm.
- [ ] Chạy lặp 3–5 lượt các case race có barrier; không chạy lại toàn suite vô hạn.
  Nếu gặp lỗi, ghi red → fix nhỏ → green, giữ tests SQLite/ownership hiện có.

Chấp nhận: assertions trên số row/XP/status/history, không chỉ log “không exception”.
Migration upgrade/checksum đã có không thay phần test concurrency này.

## C5 — G01/G02: Engine thực và corpus chất lượng

Files: speech adapters/options, local speech/writing runbooks, fixture corpus và
manifest vận hành riêng khi cần. Không trộn artifact app engine vào `.ai/vendor`.

- [ ] Xem CPU/RAM/disk/Docker và service hiện có trước. Chọn một ASR English
  phù hợp; verify official tag/commit, image digest/model SHA256/license trước tải.
  Các pin/version trong plan cũ là ứng viên, không coi mặc nhiên còn đúng.
- [ ] Khi được owner giao trong câu chat: cho phép Docker loopback và model ASR
  <=500 MiB. Không tự tải LLM nhiều GB hoặc dừng workload khác. Nếu vượt giới hạn,
  hoàn thành adapter/harness độc lập rồi nêu đúng artifact/tài nguyên cần quyết định.
- [ ] Dùng audio tổng hợp hoặc fixture có license/provenance; PCM16/mono/16kHz.
  Không gửi reference answer làm prompt ASR và không dùng audio người thật ra ngoài.
- [ ] Ghi request/response thực đã loại dữ liệu riêng tư, model identity/hash,
  OS/CPU/RAM, cold/warm latency, memory và sample count. Verify parser theo contract
  thực; không coi stub-transcriber tests là bằng chứng đã tích hợp engine.
- [ ] E2E đúng câu: WAV → assessment → receipt → progress → credit, reload còn.
  Sai câu/silence/mic denied/network down/timeout/cancel không cấp điểm giả.
- [ ] Corpus Writing >=40 ca có expected labels trước chạy: 10 exact/formatting,
  10 đổi nghĩa/số/dấu/đơn vị/phủ định, 10 paraphrase NeedsReview, 5 input rỗng/lạc
  đề/nhiều câu, 5 Unicode/decimal/boundary. Ghi case nào cần xác nhận nhãn.
- [ ] LanguageTool/Ollama là feedback tùy chọn. Nếu chưa có engine được duyệt,
  giữ default-off, chứng minh core chạy, ghi Deferred có lý do; không dùng chúng
  để quyết định passed/XP. ASR-for-import/prosody không tự biến thành scope mới.

Chấp nhận: báo cáo số đo thực và kết quả fixture; không gọi là full offline khi
video/captions/generation vẫn phụ thuộc mạng. Local Docker không phải endpoint
ASR production cho Nhân Hòa; phần compute/network hosting phải được chốt riêng.

## C6 — G04: Nghiệm thu UI và browser thật

Đọc DESIGN.md + skill UI liên quan trước sửa. Dùng matrix 25 view trong
`docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md`: AU01–06, CO01, SP01–03,
WR01–03, GO01, AI01–02, AD01–09. Không tự tuyên bố redesign hoàn tất.

- [ ] App acceptance port riêng (ví dụ 5110), DB/user synthetic riêng; không sửa
  User Secrets dev đang dùng hoặc ngừng app 5100 của owner để đổi dữ liệu.
- [ ] Desktop 1366x768 và mobile 390x844; keyboard Tab/Shift+Tab/Enter/Escape,
  focus, label/error association, tương phản, overflow, loading/empty/error states.
- [ ] Speaking: ghi → đổi câu/rời trang/hủy/auto-stop; permission denied;
  request cũ về muộn không gắn vào câu mới; unavailable không có điểm/thưởng giả.
- [ ] Writing: submit/reload/draft/review state/đổi câu khi đang chấm; không mất
  bài khi engine lỗi; phản hồi grammar không biến câu sai nghĩa thành passed.
- [ ] Notification mark-read/all thất bại phải giữ/khôi phục unread và retry được;
  import 202/poll/timeout/resume/subtitle/permission theo đúng UI.
- [ ] Matrix mỗi view ghi role/URL/viewport/actual/evidence. Screenshot/log ở
  ignored output/playwright/acceptance-2930; tài liệu không chứa PII/secret.
- [ ] Các màn ngoài module đang sửa chỉ acceptance; sửa shared CSS hoặc Auth/
  Admin/AI behavior phải có lỗi cụ thể và blast radius, không tiện tay refactor.

## C7 — G05: Full suite và CI đúng SHA

- [ ] Dùng layout bin chuẩn. Nếu app Release đang chạy khóa output, dùng output
  riêng cho build app, còn test theo layout fixture hỗ trợ; không tự sửa hàng loạt
  helper để đáp ứng tùy chọn --artifacts-path của một lần gọi.
- [ ] dotnet build app; dotnet test full, không skip. Kỳ vọng baseline 620 cộng
  regression mới, không cố giữ số 620. Chạy Node behavior và deploy guard tests.
- [ ] Khi sửa scripts/tooling theo scope, chạy agent_tooling.py và Python suite;
  nếu sửa bootstrap/MCP ngoài scope thì phải xin phạm vi riêng, không tự kéo vào.
- [ ] Nếu owner cho commit/push: nhóm commit theo fix/test/acceptance; push nhánh
  feature, workflow_dispatch `deploy.yml` trên chính nhánh, kiểm tra head SHA của run.
  Không dispatch master để lấy CI xanh của source khác.
- [ ] Linux full application/MariaDB/publish/bundle pass. Với checks chỉ Windows
  (PowerShell 5.1), có Windows job/harness tương ứng; không dùng Linux skipped
  tests để nói toàn bộ deploy script đã qua CI.
- [ ] Báo rõ CI success và release skipped/gate false; không tự merge.

## C8 — G06: Diễn tập release và bàn giao

- [ ] Chạy enabled release trên Windows staging/harness cô lập, cùng artifact
  app/bundle của commit test. Mặc định production guards giữ nguyên; nếu refactor
  dependency/test profile để test, test mode phải từ chối hosting/DB/root production.
  Không thay chuỗi/path trong script tạm rồi coi là đã test production script thật.
- [ ] Injection: artifact/checksum lỗi; backup fail; migration fail; copy bị ngắt;
  app health fail; duplicate/concurrent run; webhook/rerun; operator maintenance.
  Assert app/schema/marker/state/maintenance và exit code ở mỗi trạng thái.
- [ ] Restore backup vào DB riêng, kiểm count/checksum/history; không tự Down
  hoặc overwrite dữ liệu production. Xác minh uploads và config private giữ nguyên.
- [ ] Owner/provider thực hiện rotation credentials cũ và cung cấp staging/ASR
  endpoint reachable từ hosting khi cần; không yêu cầu gửi password vào chat.
  Những phần này thiếu thì ghi Blocked/Not run cụ thể, giữ gate false và làm hết
  các bước code/local khác; không hỏi lại permission chung chung.
- [ ] Cập nhật runbooks, bug-fix-log cho lỗi đã sửa, known-issues nếu liên quan,
  ledger và báo cáo nghiệm thu cuối. Không sửa review cũ thành “đã luôn đúng”.
- [ ] Bàn giao diff/commits/tests/SHA, từng REV/Gate đóng bằng chứng nào, phần
  Deferred/Blocked và hành động owner còn cần làm. Không tuyên bố 100% khi còn gate.

## Lệnh tham chiếu (chạy đúng project root, không chứa secret)

```powershell
dotnet tool restore
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release
dotnet ef migrations has-pending-model-changes --project TCTEnglish --configuration Release
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
```

Node: lấy danh sách `scripts/tests/test_*.cjs` rồi `node --test` các file đó;
không dùng screenshot/source-string test thay kiểm chứng lifecycle/HTTP/state.
Tests MariaDB, engine, Windows release và UI có command/artifact riêng ghi ledger.
