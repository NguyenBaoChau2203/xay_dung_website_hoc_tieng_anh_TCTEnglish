# Plan sửa sau review vòng 2 — Learning Reliability

Ngày 2026-10-30. Dành cho Cursor, model theo lựa chọn chủ nhóm: Grok 4.7 High. Một agent tự điều tra, thực thi, review và kiểm chứng.

## 1. Mục tiêu và tài liệu chuẩn

Khép các lỗi **RR01–RR07** trong `docs/feature-audits/2026-10-30-learning-remediation-rereview.md` và phần F1–F9 còn mở. Giữ những sửa đổi đã được chứng minh đúng; không làm lại toàn bộ đợt nâng cấp.

Nạp theo nhu cầu, không đọc toàn bộ danh sách đầu phiên:

1. Tái sử dụng `AGENTS.md`/`.ai/README.md` đã có; prompt `.ai/prompts/cursor-grok-learning-remediation-round2.md` là entrypoint. Ban đầu chỉ cần mục 1–4 của plan này; tới bước C nào đọc mục đó, vẫn hoàn thành mọi bước được giao.
2. Báo cáo review vòng 2: chỉ đọc RR đang sửa hoặc bằng chứng chưa có trong context. Ledger: hàng trạng thái liên quan; không dùng pass lịch sử thay kiểm chứng mới.
3. `docs/superpowers/plans/2026-10-30-learning-review-fixes-plan.md` và ledger cũ là tham chiếu khi thiếu contract, không phải bundle bắt buộc. `docs/project-structure.md`, conventions, known issues và bug log chỉ đọc phần liên quan.
4. CodeGraph exact symbol/maxFiles 1–3 trước source; verbatim còn mới không đọc lại. Stale/unindexed/gap cụ thể mới dùng bounded reads. Context7 chỉ cho tài liệu API/library bên ngoài đúng version nếu host thực sự cung cấp tool; không thay source hoặc tự cài/sync.

Source thực tế: **net9.0, EF Core 9.0.4, Pomelo MySQL 9.0.0**; SDK mới không thay target. SQL Server trong một số tài liệu là thông tin cũ, không phải quyền chuyển provider.

## 2. Chế độ và quyền triển khai

Đợt tạo tài liệu này **chỉ lập plan/prompt**, chưa sửa mã ứng dụng. Khi chủ nhóm gửi thông điệp duyệt thực thi kèm prompt, Cursor được làm C0–C9 và tiếp tục qua các bước trong phạm vi đã duyệt, không xin lại quyền cho mỗi bước.

Phạm vi sửa: Speaking/Writing người học, Goals/Notifications, credit/outbox, import jobs/workers, các model/ViewModel/JS/view/tests liên quan trực tiếp. UI chỉ thay trạng thái và form nghiệp vụ cần thiết; đọc `DESIGN.md` và giữ cấu trúc/giao diện hiện tại.

Quyền đặc biệt được nêu rõ trong câu chat duyệt:

- `Program.cs`: chỉ sửa DI/lifetime/options của các thành phần Learning được sửa ở đây, nếu cần; không đổi startup seeding, auth hoặc cấu hình AI chung.
- Model/DbContext và migration **bổ sung trong repo**: chỉ khi metadata/checkpoint/source provenance còn thiếu. Ưu tiên dùng các cột đã có. Giữ nguyên ba migration hiện tại; không sửa lịch sử, không apply DB thật, không DROP dữ liệu cũ.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` và test infrastructure: chỉ sửa đồng bộ/gate/deadline/lifetime có liên quan tới F8; không sửa production AI Chat.

Không sửa `.csproj`, `appsettings*`, package/framework, skills/rules/hooks/MCP, Auth/Profile/Contact/Admin/Billing, AI Chat production hoặc các domain khác. Đọc caller/chạy regression ngoài phạm vi được phép; không thay semantics của chúng. Không chạy app trên database thật do startup seeding, không tải/cài engine/model hay dựng hạ tầng mới, không tự gọi dịch vụ trả phí/dùng audio hoặc key thật, không auto index/sync CodeGraph. Không commit/push/PR/merge/deploy.

Nếu cổng engine/browser/MySQL thiếu môi trường, làm xong code và regression độc lập có thể làm, ghi đúng Not run/Blocked rồi tiếp tục bước không phụ thuộc. Thiếu engine không phải lý do bỏ UI polling, cancel, atomic claim hoặc regression dùng stub.

## 3. Baseline đã biết và phần phải bảo toàn

- Nhánh review: `feature/bao-chau-ui-audit`, HEAD `3d27a68c8468207eca5a11653c72e735117fc637`; working tree có nhiều thay đổi của nhiều đợt. Phải chụp baseline mới lúc bắt đầu, không stage/reset toàn bộ.
- Đã chứng minh Speaking credit insert lỗi rollback completion; retry thành công và không cộng lặp. Không đưa logic credit ra ngoài transaction trở lại.
- Recorder riêng đã giữ WAV sau auto-stop, callback một lần; dispose trực tiếp xử lý late permission. Sửa caller, không bỏ cơ chế finish-once này.
- Worker đã duyệt được user thứ 201 trong luồng bình thường. Giữ keyset/batch <=200; không nâng Take để né checkpoint.
- Limiter đã singleton theo process, có queue thật; giữ state admission dùng chung, không đưa lại vào từng typed client.
- Outbox v2 có semantic type/key/UTC occurrence; legacy needs-review. Không quay về completion generic hoặc gán mọi completion thành GoalCompleted.
- Grammar/model đã có caller; default-off và Local policy quyết định pass/XP. Không dùng engine để cấp điểm giả hoặc thay quyết định học tập.

Baseline test vòng review: Node 13/13; focused .NET hai lượt đều 165 pass/1 fail/166 ở hai ca SQLite khác nhau; AI conflict chạy riêng pass nhưng full suite abort sau 558 pass/60 giây inactivity. Đây là baseline lỗi, không phải tiêu chí số lượng test cần giữ cố định.

## 4. Thứ tự và phụ thuộc

| Bước | Phát hiện/phạm vi | Phụ thuộc | Kết quả cần có |
| --- | --- | --- | --- |
| C0 | Baseline, lỗi hiện tại, chẩn đoán F8 | Không | Ledger mới và kịch bản tái hiện |
| C1 | RR01 — retry/idempotency import | C0 | Failed/cancelled retry hợp lệ, không lỗi unique |
| C2 | RR02, phần F3/F1 — notification batch/lease | C0 | Không bỏ người nhận; checkpoint/claim có kiểm chứng |
| C3 | RR03 — PCM/HTTP lifecycle | C0 | Đổi câu/dispose đóng mic, bỏ callback cũ |
| C4 | RR05 — deadline ASR | C0 | Queue + inference chung một ngân sách |
| C5 | RR06 — notification transition | C0 | Goal/streak/badge đúng sự kiện và thời gian |
| C6 | RR04, phần F5 — job publish/UI/subtitle | C1, mẫu claim từ C2 | Người học đi hết luồng import bền vững |
| C7 | RR07, phần F6/F7 — Writing feedback | C0 | Tổng budget 3 giây, schema/mapping đúng |
| C8 | F8 — test gate/SQLite lifetime | Bắt đầu chẩn đoán ở C0 | Test không flake/hang theo các kịch bản cũ |
| C9 | F9 — nghiệm thu/diff/docs | C1–C8 | Build, regressions, full run Completed và ledger trung thực |

Thực thi tuần tự bằng cùng agent. Có thể xử lý C8 sớm nếu fixture cản regression, nhưng phải ghi rõ nguyên nhân; không bỏ ba lỗi P1 để chỉ chạy lại test cho xanh.

## 5. C0 — Chụp baseline và khẳng định lỗi

- [ ] Ghi branch/HEAD, tracked/untracked status và hash các file có trước; lưu artifact ignored dưới `.tmp/learning-remediation-round2/`. Không reset/stash/clean/checkout để bỏ diff.
- [ ] Tạo `docs/feature-audits/bao-chau-learning-remediation-round2-ledger.md`: RR01–RR07, phần F1/F3/F5/F6/F8/F9 còn mở; mỗi mục có source, regression, red/green, command/TRX, status code/test/environment và residual.
- [ ] CodeGraph trước khi tìm hiểu source; nếu stale/không phủ symbol thì đọc source có giới hạn. Không re-index.
- [ ] Xem lại ca AI conflict và SQLite init/dispose từ review; chạy riêng có deadline để lấy log, không chạy full suite liên tục trước khi sửa nguyên nhân.
- [ ] Chuyển kịch bản đã tái hiện bên dưới thành regression **kỳ vọng hành vi đúng**. Không phụ thuộc file `.tmp` của máy reviewer; artifact đó có thể không có trên máy khác.

**Xong khi:** có baseline an toàn, checklist và tests đỏ đúng lỗi cần sửa; không dùng test thiếu sample/assertion rỗng để hợp thức hóa tiến độ.

## 6. C1 — RR01: import retry và unique fingerprint

**File:** `Services/Speaking/SpeakingImportJobService.cs`, `Models/SpeakingImportJob.cs`, controller/API contract nếu cần; tests `SpeakingImportReliabilityTests.cs`, `PremiumSpeakingLifecycleIntegrationTests.cs`. Các path ứng dụng nằm dưới `TCTEnglish/`.

- [ ] Giữ unique `(OwnerUserId, RequestFingerprint)`. Failed/cancelled được **user chủ động** gửi lại: requeue cùng row/PublicId bằng conditional transition; clear lease/error/result/completed timestamp không còn hợp lệ, reset attempt cho lượt retry chủ động theo policy hữu hạn. Poll status không tự requeue, không tạo vòng retry vô hạn.
- [ ] Queued/running trả job hiện tại; succeeded chỉ trả result nếu video còn tồn tại và đúng owner/source. Video đã bị owner xóa có đường re-import chủ động hợp lệ.
- [ ] Bắt đúng unique conflict khi enqueue đồng thời, rollback/detach hoặc dùng scope sạch rồi đọc job thắng theo owner. Không bắt mọi DbUpdateException thành success. Không reuse context còn entity Added lỗi.
- [ ] Không đổi thuật toán fingerprint của caption cũ một cách âm thầm. C6 phải có chiến lược tương thích row `SourceKind=null`; source kind/hash phụ đề mới không làm lách unique video hoặc nhận nhầm kết quả cũ.
- [ ] Test failed→resubmit không 500, cancelled→resubmit, hai request cùng nguồn trả cùng job, user khác không đọc/requeue được, Premium bị thu hồi không được publish; succeeded+deleted video có re-import.

**Xong khi:** user Premium có thể retry đúng giới hạn; không xóa unique index để làm test qua; kết quả không trỏ vào video đã mất hoặc owner khác.

## 7. C2 — RR02: không bỏ batch, có checkpoint/lease thực sự

**File:** `Workers/NotificationWorker.cs`, `Models/NotificationJobRun.cs`, `Services/Notifications/*` liên quan, tests `NotificationWorkerRecoveryTests.cs`, `NotificationReliabilityIntegrationTests.cs`; DbContext/migration chỉ nếu thiếu metadata.

- [ ] Regression: 201 và 450 user hợp lệ; có sẵn một `BadgeNear` ở đầu batch. Kỳ vọng cuối cùng **đủ 201/450 event tương ứng, không trùng**, blocked user bị loại. Không assert số 8/201 của probe cũ như hành vi đúng.
- [ ] Query các semantic keys đã có trong đúng batch; xử lý duplicate hữu hạn. Nếu SaveChanges thất bại, không detach toàn bộ notification rồi tăng cursor. Lỗi không phải unique phải giữ batch retry/failed, LastError rõ.
- [ ] Dùng `Phase` (`streak`, `badge`, `cleanup`, `complete`) và `LastUserId` đã có. Mỗi poll tối đa một batch <=200, lưu tiến độ; chỉ succeeded sau hết mọi phase. Phân biệt poll tiếp tục với acquisition attempt mới.
- [ ] Conditional claim bằng trạng thái/lease hết hạn, rowsAffected=1 mới sở hữu. Mỗi claim có LeaseToken; heartbeat/checkpoint/finish đều có điều kiện token còn hợp lệ. Worker cũ không được commit/finish sau mất lease. Lease 5 phút, renew tối thiểu mỗi phút khi đang xử lý; không heartbeat song song trên cùng DbContext.
- [ ] Insert notification và cập nhật cursor trong một transaction, bảo vệ quyền lease trước khi commit; crash không tạo khoảng skip. Retry acquisition hữu hạn theo F3 cũ, tối đa 3 lần thất bại/ngày; không vòng nóng vô hạn.
- [ ] Cleanup là phase độc lập có trạng thái; lỗi notification không bị đổi thành cleanup success cho cả job. Restart không phát nhắc nhở sai ngày.
- [ ] Test restart giữa batch, DB fail giữa save/checkpoint, hai worker, lease expiry và worker cũ finish muộn. Dùng TimeProvider/barrier, không đợi 5 phút thật. Concurrency fixture phải có các DbContext/connection độc lập; không dùng cùng connection song song rồi gọi đó là proof MySQL.
- [ ] Với outbox, giữ v2+UTC và kiểm lỗi một event không làm mất tracking/checkpoint của event tiếp theo; không dùng ChangeTracker.Clear trên batch rồi coi các entity detached sau đó đã được persist.

**Xong khi:** đủ người nhận sau retry/restart, không trùng semantic key, không chốt thành công thiếu dữ liệu; các metadata checkpoint có đường thực thi và test. MySQL multi-instance chưa chạy phải ghi riêng, không lấy SQLite thay bằng chứng.

## 8. C3 — RR03: caller PCM, HTTP và state của trang

**File:** `wwwroot/js/speaking.js`, `speech-recorder.js`; tests `scripts/tests/test_speaking_behavior.cjs`, `test_speech_recorder.cjs`, `dom_fixture.cjs` nếu cần.

- [ ] Một đường cancel/dispose cho recorder đang chạy hoặc đang đợi quyền: tăng epoch, dispose PCM, abort Web Speech và HTTP pending, clear timer/reference, reset state/UI. Dùng khi đổi câu, reselect/cancel, dispose/page lifecycle tương ứng; không làm cleanup recorder mới bởi callback recorder cũ.
- [ ] Auto/manual stop dùng cùng finish-once consumer; chuyển `isRecording=false` trước khi đánh giá, giữ isScoring chính xác. Sau auto-stop/chấm xong, lần nhấn đầu tiếp theo bắt đầu thu mới.
- [ ] Sau mọi await assessment/progress kiểm epoch và sentenceId trước khi cập nhật UI. Stale response không được gắn điểm/feedback sang câu hiện tại. Abort request không đồng nghĩa rollback nếu server đã commit; reload/reconcile trạng thái thật khi cần.
- [ ] Bắt và quan sát mọi promise lỗi. Mic denied/AudioContext init fail chỉ có một đường báo lỗi, không unhandled rejection, track/context đã mở phải được đóng.
- [ ] Regression dùng source recorder **và caller thật** trong DOM fixture: auto-stop có PCM samples/WAV, manual stop, double click, đổi câu khi đang thu, dispose lúc permission pending, callback permission muộn, đổi câu khi HTTP đang chờ, request fail/cancel, silent/no chunks.
- [ ] Kiểm 44.1/48kHz downsample, tối đa 240.000 sample, WAV PCM16 mono/16kHz <=2MiB; callback/submit đúng một lần, stop lại cùng Blob. Assertion phải fail nếu Blob null hoặc callback=0 trong ca có samples.
- [ ] Không dùng `--test-force-exit`; fixture phải tự dọn timer, audio và request. Browser/mic thật chưa có môi trường thì ghi Not run.

**Xong khi:** track dừng ngay khi đổi câu/dispose, không có stale UI/double submit/unhandled rejection, state phục hồi và các test hành vi đúng qua.

## 9. C4 — RR05: một deadline cho queue và inference

**File:** `Services/Speaking/WhisperHttpTranscriber.cs`, `SpeechInferenceLimiter.cs`, `SpeakingAssessmentService.cs` và options; Program.cs chỉ nếu cần DI/options. Thêm `TCTEnglish.Tests/SpeakingInferenceLimiterTests.cs`.

- [ ] CTS tổng tạo trước admission; budget mặc định 15 giây bao queue và HTTP/parse. Mọi chặng dùng deadline còn lại, không reset về 15 giây sau khi được cấp slot.
- [ ] Caller cancel propagate; deadline nội bộ trả timeout/504; queue overflow 429 + Retry-After; unconfigured/unreachable 503. Không tạo assessment khi HTTP/JSON lỗi hoặc không có lời nói.
- [ ] Release membership/slot đúng một lần ở cancel queue, timeout, handler throw, JSON sai và thành công; singleton không giữ DbContext scoped.
- [ ] Test nhiều DI scope: một đang chạy, bốn đang đợi, lượt thứ sáu bị busy và chưa gọi HTTP. Test budget 1 giây với slot bị giữ: request phải timeout trong sai số hợp lý quanh 1 giây, không chờ được slot mới timeout.
- [ ] Test hủy giữa queue và giữa inference, sau đó request mới dùng được slot; `engine_not_ready` zero HTTP. Chỉ fake handler, không cài ASR.

**Xong khi:** giới hạn và deadline được đo qua transcriber từ nhiều scope; docs ghi cap theo process, không claim cap toàn cluster.

## 10. C5 — RR06 và F1: intent dựa vào transition thật

**File:** `Services/Learning/CompletionCreditService.cs`, `LearningNotificationIntentWriter.cs`, `DirectNotificationGate.cs`, `Services/GoalsService.cs`, `StreakService.cs`, result DTO liên quan; outbox dispatcher/tests nếu cần.

- [ ] Truyền metadata của operation vào intent writer: previous/current streak/longest, DidIncrease, badge IDs vừa được award, goal transition cần thông báo. Không suy record từ `Streak == LongestStreak` hoặc badge mới từ cửa sổ thời gian 2 phút.
- [ ] Kỷ lục: 4→5 với longest cũ 5 không có StreakRecord; 5→6 với longest cũ 5 có đúng một event; lần học thứ hai trong ngày/reset không tạo record giả.
- [ ] Goal 80/100 theo đúng GoalArea và business date; không có goal thì không GoalCompleted; giữ semantics năm GoalArea và các direct caller ngoài completion.
- [ ] Capture occurrence UTC và business date nhất quán cho operation, giữ timestamp thật khi drain qua nửa đêm; payload v2 typed, legacy needs-review; không đoán notification từ credit generic.
- [ ] Giữ transaction bao attempt/progress/completion/credit/reward/outbox. Bổ sung fault-injection cho **Writing** và Speaking retry sau lỗi; rollback không để context tái dùng mang trạng thái sai. Test concurrent completion bằng DbContext độc lập, đúng một credit/reward; lỗi DB không phải duplicate không thành replay thành công.
- [ ] Regression newly awarded badges, badges cũ trong/ngoài khoảng 2 phút, replay, mid-operation failure, midnight clock; không sửa dữ liệu XP lịch sử trong đợt này.

**Xong khi:** notification thể hiện đúng thay đổi thật, không mất tính nguyên tử đã sửa ở vòng trước; scope SQLite/MySQL được báo riêng.

## 11. C6 — RR04 và F5: import chạy hết luồng

**File:** `SpeakingImportJobService.cs`, `Workers/SpeakingImportWorker.cs`, `Services/SpeakingService.cs`/interface, `SpeakingSubtitleParser.cs`, `Controllers/SpeakingController.cs`, `Models/SpeakingImportJob.cs`, view `Views/Speaking/Index.cshtml`, `wwwroot/js/speaking.js`; các test import/Premium và thêm `scripts/tests/test_speaking_import_behavior.cjs`.

- [ ] Áp dụng conditional claim/lease token/heartbeat từ C2, backoff+jitter có NextAttemptAtUtc, tối đa 3 attempts/lượt, timeout prepare 5 phút và cancellation sạch. Không hai worker publish một job.
- [ ] Tách network/parse khỏi transaction. Trước publish recheck owner/Premium, source và lease; **video + sentences + job succeeded/result trong cùng transaction**. Không commit video rồi update job ở transaction sau.
- [ ] Recovery phải đúng owner/source. Không gắn video cùng YouTube nhưng transcript nguồn khác thành success cho subtitle mới; giữ video sẵn có, trả conflict có ích hoặc dùng provenance được kiểm chứng. Không ghi đè transcript. Nếu metadata mới thật sự cần, impact assessment và migration bổ sung, không sửa ba migration trước.
- [ ] Giữ caption fingerprint cũ tương thích; nguồn subtitle có kind + hash nội dung chuẩn hóa. `SourceKind=null` được hiểu theo nguồn legacy rõ ràng, không backfill/xóa row tự động.
- [ ] UI POST `/api/speaking/imports`, nhận 202/jobId/statusUrl; poll 5 giây, backoff tới 30 giây, pause hidden/offline, hiển thị queued/running/succeeded/failed/retry. Sau 5 phút UI dừng theo dõi có thông báo/resume jobId, không giả cancel job đang chạy. Scope owner ở API là bắt buộc.
- [ ] Giữ `/Speaking/My/Create` tương thích caller cũ; UI mới dùng job API, không tiếp tục gọi route đồng bộ.
- [ ] Form phụ đề tùy chọn SRT/WebVTT + YouTube URL; multipart POST `/api/speaking/imports/subtitles`, Authorize/CSRF/Premium/owner như enqueue. Parse text <=1MiB, <=2.000 cue, <=30 phút; không tin extension/MIME/path từ client.
- [ ] Parser hỗ trợ BOM/multiline, VTT cue id/settings và timecode có/không phần giờ. Validate hữu hạn, phút/giây 0–59, start<end, thứ tự/overlap theo policy; cue hỏng phải lỗi rõ, không nuốt âm thầm. Render transcript encode an toàn.
- [ ] Test enqueue/status IDOR, double enqueue, failed retry, re-import sau delete, two workers, lease expired, role revoke trước publish, crash trước/sau commit, captions missing, subtitle hợp lệ/sai, UI poll success/error/resume. Stub đếm learner pipeline gọi Gemini **0 lần**; không đổi pipeline Listening/Admin.
- [ ] ASR-for-import vẫn Deferred, không tải media/cài yt-dlp/ffmpeg hay mở rộng quyền để đóng mục này.

**Xong khi:** có một luồng người học thực sự dùng job và phụ đề; restart không làm kết quả đã commit mất liên kết; nguồn/timing/lease được kiểm chứng.

## 12. C7 — RR07 và F6/F7: Writing feedback hữu hạn, có schema

**File:** `Services/WritingService.cs`, `Services/Writing/{LanguageToolGrammarChecker,OllamaWritingModel,ILocalGrammarChecker,ILocalWritingModel}.cs`, result DTO/ViewModels/`wwwroot/js/writing.js` liên quan; tests `WritingOptionalEngineContractTests.cs`, `WritingAvailabilityIntegrationTests.cs`.

- [ ] Một linked CTS tổng **3 giây/request cho toàn bộ cloud enhancement + grammar + local LLM**, tạo trước chặng optional đầu tiên. Không cộng timeout riêng. Bỏ các chặng sau khi budget hết; request gốc cancel propagate, budget hết giữ Local và lưu attempt hợp lệ.
- [ ] Default-off/unconfigured zero HTTP; chỉ gọi engine đã được bật. Engine nằm ngoài transaction; không bật mặc định hoặc đổi key.
- [ ] Validate JSON shape/type/range cần dùng. LanguageTool thiếu `matches` hoặc sai loại là unavailable/invalid_schema, không Available rỗng giả. Ollama kiểm outer response và parse/allowlist nội dung feedback JSON theo DTO, không coi mọi chuỗi là feedback hợp lệ.
- [ ] Map findings/feedback/unavailable/source `languagetool`/`ollama`/cloud ra VM/JS qua text an toàn; không raw HTML/JSON. Giữ JSON contract hiện tại bằng field bổ sung khi cần.
- [ ] Local Passed/CanAutoAdvance/ReviewRequired/XP không bị model hoặc grammar thay đổi; dữ liệu bài học không thành prompt điều khiển policy. Không đưa engine vào sinh bài song ngữ trong task này.
- [ ] Endpoint integration spies chứng minh enabled gọi engine và trả feedback có nguồn; disabled không gọi; HTTP fail/malformed JSON/schema sai không phá core. Kiểm cả **hai adapter**: internal timeout→unavailable, caller cancel→throw; lỗi một adapter không vượt tổng budget.
- [ ] Test cloud chậm + grammar/model cùng bật, tổng chặng optional <=3 giây với sai số scheduler hợp lý; dùng gate/clock ở nơi phù hợp. Test câu sai nghĩa không qua vì grammar sạch hoặc model khen đúng.

**Xong khi:** endpoint dùng được feedback chuẩn hóa và phản hồi hữu hạn; stub coverage có thật. Engine/corpus 40 câu/benchmark còn Not run đến khi có môi trường hợp lệ.

## 13. C8 — F8: test không treo, SQLite fixture có lifetime đúng

**File:** `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`, `Infrastructure/TestWebApplicationFactory.cs` và test helper trực tiếp có nguyên nhân đã xác minh.

- [ ] Thay `Task.Delay(50)` ở ca conflict bằng tín hiệu request đầu đã vào vùng xử lý/provider thật. Mọi wait có deadline khoảng 5 giây và thông báo lỗi hữu ích; provider stub tôn trọng cancellation.
- [ ] `try/finally` luôn release gate và drain/cancel request còn lại có giới hạn dù assertion fail. Giữ expected: request thứ hai Conflict, request đầu OK, không double-send. Không sửa production AI để chữa test.
- [ ] Dựa log điều tra SQLite ObjectDisposed tại init và NullReference tại close: thứ tự host/schema/reset/request/worker/dispose, ownership connection của từng factory. Không khẳng định một root cause chỉ từ stack hoặc sequence.
- [ ] Connection/schema không bị reset/dispose khi request/worker còn dùng; tách resource của factory, chỉ dispose đúng owner/lần cần thiết. Hai context concurrency phải dùng connection phù hợp, không dùng một connection không hỗ trợ đồng thời rồi thêm global lock để che.
- [ ] Không tắt parallel toàn suite, skip/delete test, bỏ assertion hay chỉ tăng blame timeout. Nếu collection thật sự dùng resource không hỗ trợ parallel, chỉ giới hạn đúng collection kèm lý do; không thay concurrency test.
- [ ] Chạy ca conflict 5 lần có deadline; chạy riêng hai ca SQLite từng lỗi và relevant suite. Chỉ chạy lại full suite sau khi đã có sửa đổi hoặc bằng chứng mới, không lặp đến khi may mắn xanh.

**Xong khi:** không còn indefinite wait ở test, lỗi fixture được sửa dựa trên bằng chứng; các ca cũ ổn định và full suite có kết quả hoàn tất. Nếu phát hiện root cause ngoài scope, ghi blocker cụ thể, không âm thầm mở rộng AI production.

## 14. C9 — Kiểm chứng, migration, docs và bàn giao

- [ ] Build thông thường trước `--no-build`; không import observational probes. Node regression không force-exit. TRX/exit code phải lưu cả fail/abort lẫn retry; không gộp pass trước abort thành full green.
- [ ] Review mọi diff mới với baseline C0; giữ hunk UI/Auth/Admin/AI có trước, kiểm CSRF/owner/Premium/NotFound/async/route/JSON tương thích. Không pass raw entity ra view.
- [ ] Migration nếu cần: impact tables/index/backward compatibility/data loss; cột nullable/default an toàn, Up/Down review; preflight duplicate fingerprint/outbox bằng truy vấn read-only trên môi trường được phép. Không tự kết nối/apply/merge/delete dữ liệu thật; không startup app thật để sinh migration.
- [ ] Giữ báo cáo và ledger cũ làm lịch sử. Cập nhật ledger vòng 2, ops docs `local-speech.md`, `speaking-import.md`, `local-writing-engines.md`, `writing-local-evaluation.md` theo thực tế. Bug log chỉ ghi bug đã sửa/kiểm chứng; known issues cập nhật đúng mục.
- [ ] Mỗi RR01–RR07 và phần F1/F3/F5/F6/F8/F9 có status riêng: Implemented, Fixture verified, Environment not run hoặc Blocked; kèm regression/test command/TRX và residual. Không chỉ tick “đã code”.
- [ ] Browser/mic/MySQL/engine/corpus nếu chưa có môi trường thì Not run. Không lấy screenshot/SQLite/stub làm bằng chứng vận hành thật. Theo dõi phần ASR-import còn Deferred.
- [ ] Cuối phiên báo tiếng Việt: code đã sửa, file, tests cụ thể, migration chưa apply, các cổng còn mở. Nếu tự review còn lỗi, bàn giao kèm plan/prompt/chat cập nhật như quy ước chủ nhóm; không tuyên bố hoàn thành toàn bộ khi gate còn fail.

Lệnh mẫu (chỉ chạy Node file mới sau khi đã tạo):

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity minimal
node --test scripts/tests/test_notification_behavior.cjs scripts/tests/test_speaking_behavior.cjs scripts/tests/test_speech_recorder.cjs scripts/tests/test_speaking_import_behavior.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Speaking|FullyQualifiedName~Writing|FullyQualifiedName~Notification|FullyQualifiedName~Goals|FullyQualifiedName~Streak|FullyQualifiedName~Sprint1Smoke|FullyQualifiedName~Completion|FullyQualifiedName~Outbox" --blame-hang --blame-hang-timeout 60s --blame-hang-dump-type none --logger "trx;LogFileName=focused.trx" --results-directory .tmp/learning-remediation-round2/focused --verbosity minimal
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Send_WhenConversationAlreadyProcessing_ReturnsConflict" --blame-hang --blame-hang-timeout 30s --blame-hang-dump-type none --logger "trx;LogFileName=ai-conflict.trx" --results-directory .tmp/learning-remediation-round2/ai-conflict --verbosity minimal
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --blame-hang --blame-hang-timeout 60s --blame-hang-dump-type none --logger "trx;LogFileName=full.trx" --results-directory .tmp/learning-remediation-round2/full --verbosity minimal
git diff --check
```

**Điều kiện nghiệm thu code/test:** regression đúng hành vi cho các phát hiện đã qua, UI job/subtitle có đường thực thi, deadline và transition đúng, lease/checkpoint có test, full suite **Completed + exit 0**, diff trong phạm vi. Các cổng môi trường phải nêu đúng thực tế; chưa đạt thì bàn giao phần hoàn thành cùng blocker, không ghi toàn bộ F0–F9 xong.
