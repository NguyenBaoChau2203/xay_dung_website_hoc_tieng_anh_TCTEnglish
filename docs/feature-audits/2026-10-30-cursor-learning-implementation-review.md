# Review độc lập phần triển khai Learning Reliability của Cursor

Ngày review: **30/10/2026 (UTC+7)**. Một agent đọc báo cáo, source, diff và chạy kiểm chứng; **không sửa code ứng dụng, không apply migration/cài engine/commit/push**.

**Kết luận:** có triển khai thực tế và có nhiều cải thiện đúng hướng, nhưng **chưa đạt điều kiện đóng toàn bộ L0–L7 hoặc phát hành cho nhóm**. L2 đã làm được đường chấm Writing nội bộ; phần độ bền L6 còn lỗi nghiêm trọng, L4/L5 còn lỗi chức năng/tích hợp, L7 hiện mới là adapter chưa được sử dụng trong luồng Writing. Migration và engine chưa được vận hành theo chính ledger của Cursor.

## 1. Tài liệu và baseline được đối chiếu

- [Ledger của Cursor](bao-chau-learning-execution-ledger.md), đặc biệt mục 14–18 và dòng đầu “L7 — hoàn tất (L0→L7)”.
- [Plan đã giao](../superpowers/plans/2026-10-29-bao-chau-learning-reliability-plan.md).
- [Khảo sát gốc](2026-10-29-bao-chau-learning-reliability-audit.md).
- Các hướng dẫn trong `docs/operations/`: `local-speech.md`, `speaking-import.md`, `local-writing-engines.md`, `writing-local-evaluation.md`.
- Source hiện tại: HEAD vẫn `3d27a68c8468207eca5a11653c72e735117fc637`, nhánh `feature/bao-chau-ui-audit`; **47 tracked files modified**, còn nhiều file source/test/migration untracked. Đây là working tree local, chưa phải thay đổi đã commit/merge.

CodeGraph đã được gọi trước. Index không chứa các service mới và báo nhiều file đã thay đổi, nên review đọc source trực tiếp có giới hạn; không index/sync lại.

Đã so hash 19 file UI tồn tại trước đợt Cursor: **18/19 giữ nguyên byte**, file thay đổi tiếp là `Views/Speaking/Practice.cshtml` có phần trạng thái/script Speaking. Không quy các thay đổi Auth/Contact/AI/Admin có sẵn cho Cursor.

## 2. Các phát hiện cần xử lý

Mức P1: cần sửa trước khi phát hành phần liên quan. Mức P2: lỗi hoặc thiếu tích hợp cần sửa trước khi bật tính năng đó. Định vị source là dòng tại thời điểm review.

### R01 — P1: L6 chưa atomic; hoàn thành có thể lưu vĩnh viễn nhưng không có thưởng

**Source:** `TCTEnglish/Controllers/SpeakingController.cs:330,348,358`; `TCTEnglish/Services/WritingService.cs:216–236`; `TCTEnglish/Services/Learning/CompletionCreditService.cs:91,143–148`.

Controller Speaking đã save progress/consume assessment và chuyển video sang completed **trước** khi gọi credit service. Writing tương tự: attempt/progress save xong mới gọi credit. Transaction trong `CompletionCreditService` chỉ bao phần credit/goals/outbox, không bao completion đã lưu trước đó.

Ngoài ra, catch mọi `DbUpdateException` đều được coi là “race ignored” và trả `Applied=false` với status mặc định Success. Lỗi ghi DB thật vì thế cũng có thể bị nuốt như một request trùng.

**Đã tái hiện bằng fault injection trên SQLite in-memory:** trigger chỉ chặn INSERT credit; nộp các assessment hợp lệ vẫn nhận HTTP 200, video completed, credit/outbox bằng 0. Sau bỏ trigger, nộp lại assessment mới vẫn credit/outbox bằng 0 vì `firstTimeVideoCompletion` đã false. Không cần mất mạng hoặc kill process để tái hiện. Đây là rủi ro G01 cũ còn tồn tại, không phải chỉ thiếu benchmark MySQL.

**Cần sửa:** cùng transaction cho completion + credit/effects, hoặc durable pending event được commit cùng completion và có đường recovery thực sự. Chỉ bỏ qua đúng unique conflict đã xác định; lỗi DB khác phải nổi lên/retry có kiểm soát. Regression phải đi qua endpoint hoàn thành rồi gây lỗi credit, không chỉ gọi credit service hai lần tuần tự.

### R02 — P1: tự dừng thu âm 15 giây làm mất bản thu; dispose lúc chờ quyền mic vẫn có thể mở mic muộn

**Source:** `TCTEnglish/wwwroot/js/speech-recorder.js:79–107,111–151,157`; caller `wwwroot/js/speaking.js:701–765`.

Timer/`onaudioprocess` gọi `stop()` nhưng bỏ Promise chứa WAV. `stop()` đồng thời xóa chunks và đóng stream. UI không được nhận event/blob tự dừng; lần bấm dừng tiếp gọi `stop()` lần hai và nhận null. Bản thu đủ 15 giây không được gửi chấm.

`start()` còn await quyền getUserMedia rồi tiếp tục khởi tạo bất kể `dispose()` đã được gọi trong lúc đợi. Đổi câu/hủy thu trong khi hộp quyền đang mở có thể bị bản thu cũ mở lại sau khi người dùng chấp thuận.

**Đã tái hiện bằng Node VM chạy source recorder thật:** sau auto-stop, `stop()` lần sau trả null; dispose trước khi permission resolve thì `isRecording()` vẫn true và track chưa được stop. Các phép thử dùng fake audio/timer, không microphone người thật.

**Cần sửa:** lifecycle thu có đúng một kết quả hoàn tất, trả/callback blob cho cả manual/auto stop; lưu completion Promise/blob tới khi consumer nhận. Thêm generation/cancellation check sau await permission và cleanup khi start thất bại. Test tự dừng, late permission, đổi câu và cleanup track thực sự; test hiện tại của recorder chỉ kiểm exports/support/maxSeconds.

### R03 — P1: worker bỏ qua người thứ 201 trở đi nhưng đánh dấu cả ngày đã xử lý xong

**Source:** `TCTEnglish/Workers/NotificationWorker.cs:150,184,254`.

Hai query lấy `.Take(200)` nhưng không có vòng phân trang/cursor. Sau batch đầu, run được đặt `succeeded`, các vòng sau bỏ qua ngày đó. Đây là giới hạn tổng số người được xử lý, không phải chỉ giới hạn kích thước mỗi batch.

**Đã tái hiện:** seed 201 user đủ điều kiện cảnh báo streak; chạy worker hai lần vẫn chỉ 200 thông báo, user cuối không có thông báo, job báo succeeded.

**Cần sửa:** tiếp tục các batch có cursor/checkpoint riêng đến khi hết người dùng; chỉ succeeded sau toàn bộ công việc. Đồng thời claim/renew lease bằng thao tác DB có điều kiện, không đọc rồi save thông thường. Hiện lease của worker chưa có claim atomic/heartbeat như tiêu chí plan; chưa chứng minh multi-instance.

### R04 — P1: giới hạn tải ASR không được chia sẻ giữa các request

**Source:** `TCTEnglish/Program.cs:44`; `Services/Speaking/WhisperHttpTranscriber.cs:16–28,38–46`.

Typed HTTP client được đăng ký qua AddHttpClient; mỗi instance transcriber tạo SemaphoreSlim và bộ đếm `_queued` riêng. Những request MVC khác nhau có thể dùng instance khác nhau, nên cấu hình `MaxConcurrentInferences=1` không tạo giới hạn chung cho máy chủ. `WaitAsync(0)` cũng không thực hiện hàng đợi bốn lượt như options gợi ý.

**Bằng chứng:** source/lifetime DI; chưa load test engine thật. Khi bật ASR với nhiều người học, cấu hình này không bảo vệ CPU/GPU như plan dự kiến.

**Cần sửa:** limiter/queue có lifetime chia sẻ đúng phạm vi engine; ngân sách timeout bao cả chờ. Hàng đợi đầy trả 429 + Retry-After (hiện outcome busy đi 503). Nếu chạy nhiều app instance phải quyết định giới hạn ở gateway/service ASR, không chỉ trong một process. Test nhiều scope/request đồng thời.

### R05 — P1: job import chưa có cơ chế phục hồi/idempotency bền vững khi restart hoặc hai worker tranh nhau

**Source:** `TCTEnglish/Workers/SpeakingImportWorker.cs:55–74,93–114`; `Services/Speaking/SpeakingImportJobService.cs:69–94`; `Services/SpeakingService.cs:199–207`; `Models/DbflashcardContext.cs:983`.

Worker chọn job rồi gán running/lease bằng SaveChanges, không conditional claim. Fingerprint job chỉ có index thường; enqueue check-then-insert chưa ngăn hai request tạo hai job cùng input.

Quan trọng hơn, video được tạo/commit trong `CreateOwnedVideoAsync`, rồi job mới được save succeeded. Nếu restart ở giữa, job reclaimed gọi create lại và gặp “Video này đã có”, sau đó bị queued/failed thay vì tìm lại kết quả thành công. Unique index video giúp chặn row video trùng, nhưng không phục hồi đúng trạng thái/result job.

**Bằng chứng:** source xác nhận các transaction boundary/claim/index; chưa tái hiện multi-process MySQL. Không gọi đây là job chịu crash đã nghiệm thu.

**Cần sửa:** atomic claim, unique request identity theo semantics đã chọn, kết quả import có thể tìm lại sau restart; job/result publication cùng giao dịch phù hợp hoặc bước recovery nhận diện video đã tạo. Có heartbeat/backoff, kiểm lại quyền trước publication, test crash sau video commit trước job success.

### R06 — P2: outbox gắn sai loại thông báo và tự tạo thời gian tương lai

**Source:** `Services/Notifications/LearningOutboxDispatcher.cs:70–78`; credit vẫn gọi `GoalsService.RecordLearningActivityAsync` tại `CompletionCreditService.cs:122`.

Mọi event hoàn thành bài đều bị map thành `NotificationType.GoalCompleted`, kể cả user chưa có goal hoặc chưa đạt target. `CreatedAt` được đặt bằng businessDate + 12 giờ như giờ UTC, không lấy thời điểm xảy ra sự kiện. Goal/streak/badge notification trực tiếp vẫn còn trong Goals/Streak path; outbox mới chỉ tạo thông báo generic, không thay thế đầy đủ intent của các loại đó như plan yêu cầu.

**Đã tái hiện:** user không có goal, event completion lúc 09:00 Việt Nam tạo thông báo GoalCompleted có CreatedAt 19:00 Việt Nam (tương lai 10 giờ).

**Cần sửa:** event type/metadata đúng nghĩa, tách hoàn thành bài với hoàn thành goal; dùng occurrence timestamp thật và giữ businessDate riêng cho khóa/ngày học. Chuyển đúng intent goal/streak/badge sang outbox cho credit path nếu tiếp tục chọn kiến trúc này. Bổ sung kiểm thử dispatch lỗi/duplicate và retry state, không chỉ test có một row outbox.

### R07 — P2: L7 mới có adapter; bật config chưa làm Writing sử dụng LanguageTool/Ollama

**Source:** `Program.cs:60–78`; constructor/evaluation của `Services/WritingService.cs`; `Services/WritingService.Generation.cs`.

Hai interface chỉ có đăng ký DI, implementation và test gọi trực tiếp; chưa có caller trong chấm Writing hoặc tạo bài. Không có wiring áp grammar findings/LLM feedback lên response học. Tạo bài Writing hiện vẫn dùng provider AI cũ.

**Bằng chứng:** bounded search interface/call sites trong source; code trong constructor Writing không nhận hai engine này.

**Cần sửa hoặc báo đúng phạm vi:** đổi trạng thái L7 thành “adapter scaffold, chưa tích hợp/chưa nghiệm thu”, hoặc nối vào luồng tùy chọn có UI/schema/leak guards và test hành vi khi enabled. Giữ default off là đúng; không tuyên bố có grammar/local LLM hoạt động chỉ từ đăng ký DI. Corpus 40 câu và kiểm artifact/model vẫn chưa có.

### R08 — P2: timeout nội bộ của hai adapter Writing bị ném lỗi; caller cancellation lại bị nuốt

**Source:** `Services/Writing/LanguageToolGrammarChecker.cs:105`; `Services/Writing/OllamaWritingModel.cs:70`.

Filter catch dùng `ex is not OperationCanceledException || cancellationToken.IsCancellationRequested`. Khi CTS timeout riêng hết hạn, caller token chưa hủy, filter false và exception thoát ra. Ngược lại khi caller chủ động hủy, filter true và trả unavailable.

**Đã tái hiện:** fake HTTP handler chờ cancellation; cấu hình timeout một giây cho mỗi adapter, cả hai ném OperationCanceledException thay vì kết quả unavailable. Không gọi LanguageTool/Ollama hoặc dùng mạng thật.

**Cần sửa:** tách timeout nội bộ thành unavailable có lý do; propagate caller cancellation. Kiểm malformed JSON/schema của enabled path, không chỉ default-disabled test.

## 3. Thực tế mỗi lô đã tới đâu

| Lô | Trạng thái review | Điều đã có / điều còn thiếu |
| --- | --- | --- |
| L0 | Đã làm | Ledger/baseline/môi trường/contract. Nội dung L0 lịch sử trong ledger cần đọc theo mốc, không coi mọi đoạn “chưa áp” là trạng thái cuối. |
| L1 | Cốt lõi đã làm | Bỏ random/demo persist, mark-read xử lý lỗi có test. Lỗi recorder R02 thuộc phần thêm sau ở L4. |
| L2 | Phần chính đã làm | Local mặc định, exact/reference được Accepted, non-exact NeedsReview, cloud feedback off; test không gọi provider và reload kết quả có thực. Chưa có đáp án biến thể được duyệt hoặc quy trình review của giáo viên. |
| L3 | Một phần đạt | Business clock cho Notification/Streak, event key, DB paging, checkpoint. R03 khiến worker chưa hoàn thành phạm vi. Goals vẫn dùng clock/helper cũ ở một số chỗ; chưa chứng minh clock thống nhất qua boundary. |
| L4 | Có backend/client, chưa sẵn sàng vận hành | Receipt bound user/câu, score-only 409 và stub tests có thực; R02/R04 còn mở. Chưa cài/spike whisper.cpp, chưa mic/browser thật; URL engine rỗng trả 503. |
| L5 | Một phần, chưa hết đường đi người dùng | Learner captions-only đã tránh Gemini; có API job/worker/parser. UI vẫn POST `/Speaking/My/Create` đồng bộ ở `speaking.js:1359`, chưa dùng API job/poll 202. Parser SRT/VTT chỉ được gọi từ test, chưa có flow upload phụ đề; ASR-for-import chưa làm. R05 còn mở. |
| L6 | Chưa đạt invariant chính | Có credit unique/outbox, nhưng R01 vẫn tái hiện mất thưởng, R06 sai nghĩa/thời điểm thông báo. Test “recovery” mới có apply twice + negative input, chưa test crash/recovery/multi-DbContext. |
| L7 | Scaffold, chưa tích hợp/kiểm chất lượng | Có hai adapter default off; R07/R08. Chưa engine thật, model, artifact digest, corpus hay benchmark. |

Không gán phần trăm hoàn thành vì số file/test không đo được mức đạt invariant. “Đã thêm code cho mọi lô” khác với “mọi tiêu chí nghiệm thu đã đạt”.

## 4. Kết quả kiểm chứng độc lập

| Kiểm tra | Kết quả thực tế |
| --- | --- |
| Build solution hiện tại | **0 lỗi / 35 cảnh báo nullable**, chủ yếu nằm ở source ngoài phần mới; không sửa cảnh báo trong lượt review. |
| Node regressions của Cursor | **12/12 pass**, không cần `--test-force-exit` trong lần review. Test recorder hiện chỉ kiểm support/export/maxSeconds. |
| Bộ test liên quan giống phạm vi ledger | Lần 1: **162 pass / 1 fail / 163**, lỗi SQLite `DBNull` trong `TestWebApplicationFactory.InitializeAsync` ở test private access. Ca đó chạy riêng **1/1 pass**. Chạy lại toàn bộ filter sau build thường: **163/163 pass**. Cần ghi cả flake và lần retry, không chỉ chọn lần xanh. |
| Full .NET suite | **Bị abort**, exit 1; 555 ca đã pass trước lúc dừng vì inactivity 90 giây. Test đang chạy được blame báo là `AiPhase4HardeningIntegrationTests.Send_WhenConversationAlreadyProcessing_ReturnsConflict`. Không coi 555 là tổng suite hoàn tất. |
| Ca AI nêu trên chạy riêng | Vẫn bị abort bởi inactivity 30 giây. Chưa kết luận nguyên nhân hoặc quy cho diff Cursor; file test này không thay đổi trong diff. Test có delay 50ms để phối hợp hai request và một gate không timeout, cần điều tra đồng bộ riêng. |
| 5 C# observational probes | **5/5 xác nhận điều kiện lỗi**: mất credit dù replay sau DB phục hồi; user thứ 201 bị bỏ; outbox sai type/time; timeout Grammar; timeout Ollama. Đây không phải 5 ca chứng minh sản phẩm đúng. |
| 2 Node recorder probes | Xác nhận auto-stop làm mất WAV và permission resolve sau dispose vẫn bắt đầu recording. Không dùng mic thật. |
| Browser/ASR/MySQL thật | **Chưa kiểm**; không chạy app trên DB thật vì startup seeding; không tự tải/cài engine/model/apply migrations. |

Artifact local trong `.tmp/learning-cursor-review/` (ignored):

- `test-results/cursor-review-full.trx`, focused/focused-retry và isolated TRX.
- `probe-results/review-defect-probes.trx`.
- `ReviewProbeTests.cs`, `ReviewProbes.targets`: C# probes được thêm vào lần build test bằng MSBuild import trong thư mục ignored; **không sửa .csproj hoặc source/test do Cursor tạo**. Sau đó đã build/chạy lại bằng command bình thường, không import probes.
- `probe-recorder.cjs`, `recorder-probes.json`.

Lệnh kiểm tra chính:

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity quiet
node --test scripts/tests/test_speaking_behavior.cjs scripts/tests/test_notification_behavior.cjs scripts/tests/test_speech_recorder.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --blame-hang-timeout 90s --logger "trx;LogFileName=cursor-review-full.trx" --results-directory .tmp/learning-cursor-review/test-results --verbosity minimal
```

Focused filter đã dùng: `Speaking|Writing|Notification|Goals|Streak|Sprint1Smoke|Completion|OptionalEngine|SpeakingImport|SpeakingAssessment`, mỗi token ở dạng `FullyQualifiedName~...` như ledger.

## 5. Trạng thái phát hành và việc nên làm tiếp

1. **Mở lại L6/R01 trước:** sửa transaction/recovery thật, kiểm lỗi DB không bị nuốt. Thêm endpoint fault-injection regression cùng concurrency/replay.
2. **Sửa R02/R03/R04:** lifecycle mic, batch toàn bộ user, limiter ASR dùng chung. Đây là các lỗi có thể gặp ngay khi bật chức năng hoặc tăng lượng người học.
3. **Khép L5:** UI chuyển sang job/poll, atomic claim/idempotency, phục hồi sau video commit, phụ đề qua flow thật; báo rõ ASR import còn hoãn nếu chưa triển khai.
4. **Sửa R06 và R08; chỉnh mô tả L7:** event/occurrence đúng; wiring grammar/local LLM chỉ khi được chọn, đủ timeout/schema/test chất lượng. Không kéo cloud trở lại core để né lỗi local.
5. Điều tra test suite abort và SQLite flake; chạy lại full suite thành công trước handoff. Không sửa AI ngoài phạm vi một cách ngầm định để chỉ làm xanh kiểm thử.
6. Review các migration MySQL, dữ liệu duplicate trước unique progress index, backup/rollback và test DB thử. **Chưa apply DB thật** theo ledger; không coi source mới đọc được schema cũ là chắc chắn tương thích. Không khuyên apply ngay trong khi còn lỗi trên.
7. Sau khi được duyệt vận hành: chuẩn bị artifact/model đã pin, cấu hình service nội bộ, thử mic/audio thật, cold/warm latency/RAM, lỗi engine/no-egress/concurrency. Hiện không có bằng chứng các engine đã được cài hoặc dùng được chỉ vì đã thêm adapter.

Đề nghị sửa ledger đầu trang từ “hoàn tất L0→L7” thành trạng thái tách **code đã có / test đã đạt / tích hợp còn thiếu / vận hành chưa xác minh**, và ghi R01–R08. Review này không tự chỉnh ledger của Cursor hoặc đóng các finding thay người triển khai.
