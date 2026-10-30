# Review lại phần Cursor sửa Learning Reliability — F0–F9

Ngày: 2026-10-30, UTC+7. Nhánh `feature/bao-chau-ui-audit`, HEAD `3d27a68c8468207eca5a11653c72e735117fc637`.

## Kết luận

**Chưa đạt tiêu chí nghiệm thu F0–F9.** Có sửa đổi hoạt động đúng và có bằng chứng mới, nhưng còn lỗi chức năng tái hiện được, luồng import chưa được nối tới người học, cơ chế lease/checkpoint chưa hoàn thành và full suite chưa chạy hoàn tất.

Ledger mới `docs/feature-audits/bao-chau-learning-remediation-ledger.md` đã ghi trung thực nhiều giới hạn. Việc người thực thi báo đã dừng làm không đồng nghĩa toàn bộ checklist đã hoàn thành. Review này đối chiếu ledger với source hiện tại, plan sửa ngày 2026-10-30 và kiểm thử độc lập.

Phạm vi: Speaking, Writing, completion credit, Goals/Notifications và phần test liên quan của đợt remediation. Không đánh giá lại toàn bộ các thay đổi UI/Auth/Contact/AI/Admin đã có trong working tree. Đây là lượt review; không sửa mã ứng dụng, không apply migration, không chạy ứng dụng trên database thật, không cài engine, không commit/push/merge.

Một agent thực hiện toàn bộ. Đã dùng `tct-engineering` và `tct-superpowers`; truy vấn CodeGraph trước. Index báo stale cho các file đã sửa và chưa chứa nhiều symbol mới, vì vậy dùng đọc source có giới hạn; không cập nhật index.

## Các phát hiện cần xử lý

### RR01 — P1: Job import đã failed không gửi lại được sau khi thêm unique index

**Vị trí:** `TCTEnglish/Services/Speaking/SpeakingImportJobService.cs:75`, `:93`; index trong `TCTEnglish/Migrations/20261030012638_LearningReviewRemediation.cs` và DbContext.

Service chỉ trả lại job đã tồn tại nếu status là `queued`, `running` hoặc `succeeded`. Với `failed`/`cancelled`, service tạo row mới nhưng fingerprint vẫn chỉ là hash của user + YouTube id. Unique `(OwnerUserId, RequestFingerprint)` mới khiến `SaveChangesAsync` ném `DbUpdateException`. Controller enqueue không xử lý trường hợp này thành retry hợp lệ.

**Đã tái hiện:** tạo job cho user Premium, đổi sang `failed`, gửi lại cùng URL ở scope mới → lỗi `UNIQUE constraint failed`. Không gọi YouTube hoặc engine. Đây là regression của việc thêm constraint mà chưa sửa đầy đủ lifecycle, không chỉ là thiếu load test. Hai request enqueue đồng thời cũng chưa có đường bắt đúng duplicate rồi trả job đã thắng.

**Sửa cần thiết:** định nghĩa retry có chủ đích trên job cũ hoặc mô hình attempt phù hợp unique key; bắt đúng unique conflict và đọc lại job theo owner. Xử lý cả succeeded nhưng result video đã bị owner xóa. Thêm regression failed→retry, double enqueue và re-import sau delete; giữ unique constraint.

### RR02 — P1: Một notification trùng có thể làm mất thông báo mới của cả batch, job vẫn succeeded

**Vị trí:** `TCTEnglish/Workers/NotificationWorker.cs:336` (tương tự nhánh streak ở `:231`), tăng cursor ở `:346`, chốt succeeded ở `:150`.

Nhánh badge tạo danh sách mà không loại event key đã tồn tại. Khi `SaveChangesAsync` lỗi, catch coi mọi `DbUpdateException` là duplicate, detach mọi notification đang Added rồi tiếp tục tăng cursor. Notification chưa được lưu cũng bị bỏ qua; lỗi DB khác cũng có thể bị che thành thành công.

**Đã tái hiện:** 201 user đủ điều kiện; có sẵn đúng một `BadgeNear` của user đầu batch. Sau worker, lần chạy quan sát có **8/201** notification, thiếu **193**, nhưng job là `succeeded`. Số row còn lại phụ thuộc thứ tự insert/provider; lỗi cốt lõi là bỏ phần chưa gửi rồi chốt thành công. SQLite là bằng chứng hành vi trong fixture, không phải kết quả kiểm chứng MySQL.

**Sửa cần thiết:** chống trùng theo từng semantic event; chỉ bắt đúng unique conflict; insert và checkpoint batch có tính nguyên tử; lỗi khác giữ retry/failed. Sau retry phải đủ toàn bộ user hợp lệ, mỗi event một lần.

**Phần F3 còn thiếu:** claim vẫn là đọc entity → kiểm lease → gán trường → SaveChanges, không conditional update/fencing; không heartbeat. `Phase`, `LastUserId`, `LeaseToken` đã được thêm vào schema nhưng worker không sử dụng làm checkpoint. Cursor chỉ nằm trong biến local, mỗi poll chạy hết vòng lặp. F3 chưa có bằng chứng restart giữa batch/hai worker/lease hết hạn và giới hạn attempt như plan.

### RR03 — P1: Trang Speaking không dispose recorder PCM khi đổi câu hoặc dispose trang

**Vị trí:** `TCTEnglish/wwwroot/js/speaking.js:301`, `:705`, `:1200`; recorder ở `TCTEnglish/wwwroot/js/speech-recorder.js`.

`abortActiveRecognition()` chỉ xử lý Web Speech, snapshot và UI. Hàm không gọi `activePcmRecorder.dispose()`, trong khi `dispose()` của trang chỉ gọi lại hàm này. Recorder PCM đang chạy tiếp tục giữ stream/audio graph đến auto-stop; permission trả về muộn sau khi đổi câu cũng không được caller hủy qua recorder. Có thể bắt đầu recorder mới khi recorder cũ chưa được đóng.

**Đã tái hiện:** chạy source recorder thật với WebAudio giả và source `speaking.js` thật trong DOM fixture. Bắt đầu thu PCM, đổi câu, gọi API dispose của trang: số lần `track.stop()` vẫn bằng **0**. Phép thử trực tiếp `recorder.dispose()` thì dừng track đúng; lỗi nằm ở tích hợp caller còn thiếu.

**Các điểm cùng lifecycle cần đóng:** callback assessment/progress chưa có generation check sau các await hoặc AbortController; kết quả đang xử lý có thể tiếp tục tác động UI sau đổi câu. Auto-stop chưa reset biến `isRecording` của trang ở callback hoàn thành; `setRecordUIState(false)` chỉ đổi hình thức nút. Khi từ chối mic, recorder còn reject một completion promise không được caller quan sát dù caller đã catch `start()`; Node ghi nhận một `unhandledRejection`.

**Sửa cần thiết:** một đường cancel/dispose PCM và HTTP dùng chung cho đổi câu/rời trang; reset cả state và UI; kiểm epoch sau từng await trước cập nhật đúng câu. Chốt promise lỗi qua một đường được quan sát. Regression phải chạy caller PCM thật, không chỉ Web Speech mock.

### RR04 — P2: UI import vẫn đi đường đồng bộ, chưa có luồng phụ đề

**Vị trí:** `TCTEnglish/wwwroot/js/speaking.js:1348`; `TCTEnglish/Controllers/SpeakingController.cs:72`, `:103`, `:129`; form `Views/Speaking/Index.cshtml`.

UI vẫn POST `/Speaking/My/Create`, controller gọi thẳng `CreateOwnedVideoAsync`. API enqueue/poll đã có nhưng form người học không gọi. Không có endpoint multipart `/api/speaking/imports/subtitles` hoặc flow upload phụ đề trong form. Parser/cột `SubtitlePayload` chưa tạo ra chức năng sử dụng được từ trang.

**Hệ quả:** người học vẫn chờ request đồng bộ và không nhận các lợi ích restart/retry/status của job. Nhánh captions vẫn phụ thuộc kết nối YouTube; chưa có đường phụ đề tự cung cấp khi captions không khả dụng.

**Sửa cần thiết:** làm phần UI 202→poll→result/error/resume và multipart subtitle như F5; giữ route cũ để tương thích. Worker import cũng chưa conditional claim/heartbeat/fencing; video publish và job succeeded còn hai lần commit riêng. Recovery owner+YouTube đã được bổ sung nhưng không thay thế tính nguyên tử.

### RR05 — P2: Timeout ASR không bao gồm thời gian xếp hàng

**Vị trí:** `TCTEnglish/Services/Speaking/WhisperHttpTranscriber.cs:37`, `:48`; `SpeechInferenceLimiter.cs:38`.

Transcriber chờ `AcquireAsync` bằng token của request gốc trước khi tạo CTS timeout inference. Khi hàng đợi có lượt phía trước, request có thể chờ vượt giới hạn rồi mới bắt đầu thêm một timeout đầy đủ. Limiter singleton đã đúng phạm vi process, nhưng chưa đạt ngân sách tổng mặc định 15 giây của F4.

**Đã tái hiện:** cấu hình timeout 1 giây, giữ slot và bắt đầu request xếp hàng. Sau 1,3 giây request vẫn pending; nhả slot thì nhận success sau khoảng 1,32 giây. Fake HTTP trả ngay; không có engine hoặc network thật.

**Sửa cần thiết:** tạo CTS tổng trước admission; dùng cùng deadline cho chờ và HTTP; phân biệt budget timeout với caller cancellation và trả slot/membership ở mọi đường. Thêm test nhiều DI scope, cap running+queue, timeout/cancel trong queue và phục hồi slot.

### RR06 — P2: Chạm lại kỷ lục cũ bị thông báo thành kỷ lục mới

**Vị trí:** `TCTEnglish/Services/Learning/LearningNotificationIntentWriter.cs:93`; đối chiếu logic cũ đúng ở `Services/StreakService.cs`.

Writer suy luận record từ trạng thái sau cập nhật: `LongestStreak == Streak`. Điều này cũng đúng khi người dùng chỉ bằng lại record trước đây. Direct notification đã bị suppress trong completion path nên điều kiện đúng `didIncrease && current > previousLongest` ở StreakService không còn quyết định intent này.

**Đã tái hiện:** trước học streak=4, longest=5, lần học hôm trước; hoàn thành Speaking hôm nay → streak=5, longest vẫn=5; drain outbox nhận `StreakRecord` với nội dung “kỷ lục mới”. Đáng lẽ không có event record.

**Sửa cần thiết:** truyền metadata của transition thực sự (previous/current longest, didIncrease) vào intent; không đoán lại từ snapshot sau cùng. Regression gồm vượt record, bằng record, học lần hai trong ngày và streak reset. Badge intent cũng đang suy theo cửa sổ `AwardedAt >= occurredAt - 2 phút`, cần thay bằng danh sách badge thực sự vừa được award trong operation.

### RR07 — P2: Ngân sách 3 giây của Writing chưa bao gồm cloud enhancement

**Vị trí:** `TCTEnglish/Services/WritingService.cs:202`, `:222`, `:274`, `:1620`.

Grammar và Ollama đã được gọi từ orchestrator và chia sẻ CTS 3 giây với nhau. Tuy nhiên cloud enhancement được await trước đó bằng CTS riêng, cho phép timeout cấu hình tới 30 giây; tổng optional feedback không bị giới hạn 3 giây khi cloud opt-in. Đây là kết luận từ call order và token trong source; chưa đo endpoint với engine thật.

Ollama `message.content` vẫn được nhận như chuỗi bất kỳ, ghi `ReviewText = "ollama: " + FeedbackJson`; chưa parse/allowlist feedback JSON để render thành thông tin hữu ích. LanguageTool response thiếu `matches` vẫn có thể được coi Available. F6/F7 còn thiếu regression response sai schema và spy enabled ở endpoint thật. Default-off và việc giữ pass/XP theo Local policy là những phần đã cải thiện.

**Sửa cần thiết:** một CTS/deadline optional chung trước cloud/grammar/model; mapping feedback có nguồn và unavailable rõ ràng; validate schema nội dung model, không hiển thị raw JSON. Không bật engine để thay Local pass/XP.

## F8/F9: Cổng kiểm thử vẫn chưa đạt

`TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs:732` chưa thay đổi so với baseline trước Cursor remediation: vẫn chờ `Task.Delay(50)`, provider đợi gate không deadline/cancellation, release gate chỉ sau assertion. Nếu request thứ hai đi vào provider hoặc assertion lỗi trước release, test có thể giữ request/factory không kết thúc. Đây là rủi ro đồng bộ trong test, chưa đủ bằng chứng quy nguyên nhân cho production AI Chat.

`Infrastructure/TestWebApplicationFactory.cs` cũng không đổi trong đợt sửa này. Hai lượt focused phát sinh lỗi khác nhau ở SQLite init/dispose, vì vậy chưa được kết luận đã sửa race/lifetime của fixture:

- Lượt 1: `WritingProgressIntegrationTests.WritingProgress_ListAndJsonShowRealMetadataForTheCurrentUser` — `ObjectDisposedException`, `SQLitePCL.sqlite3_stmt`, tại `EnsureCreatedAsync`/factory initialize dòng 107. Chạy riêng sau đó qua.
- Lượt 2: `GoalsPhase2IntegrationTests.GetGoalsAsync_FillsMissingWeeklyDays_FromUserDailyActivity` — `NullReferenceException` tại `SqliteConnection.Close()`/factory dispose dòng 122.
- Full suite có **558 passed, 0 failed được ghi nhận**, nhưng run **Aborted/exit 1**. TRX ghi hết 60 giây inactivity; sequence nêu ca AI conflict đang chạy. Ca này chạy riêng đã qua, nên đây vẫn là vấn đề tái hiện trong suite; không thể kết luận root cause chỉ từ sequence.

F8 cần sửa đồng bộ/gate và fixture dựa trên bằng chứng, không skip test hoặc tắt parallel toàn suite. F9 cần full run **Completed, exit 0**; con số passed trước abort không thay thế tiêu chí này.

## Những sửa đổi có tiến triển đã xác nhận

| Hạng mục | Bằng chứng hiện tại | Giới hạn |
| --- | --- | --- |
| R01: completion + credit transaction | Probe fault-injection Speaking: insert credit lỗi → HTTP 500/completion rollback; bỏ lỗi, retry thành công; retry tiếp vẫn đúng một credit | Chưa hai DbContext đồng thời/MySQL; Writing mới xác nhận transaction qua source và suite liên quan |
| R02: recorder finish-once | Probe bơm 1.600 PCM samples thật vào script: auto-stop giữ WAV **3.244 bytes**, callback một lần, stop lại cùng Blob | Caller lifecycle còn RR03; chưa browser/mic thật |
| R02: late permission ở recorder riêng | Dispose trực tiếp trước getUserMedia resolve → không recording, track bị stop | Caller trang chưa gọi đường dispose này |
| R03: hết lỗi cắt ở user 200 | Probe 201 user: nhận đủ 201 streak warnings, job succeeded | RR02, lease/checkpoint và crash recovery vẫn mở |
| R04: limiter lifetime | DI đăng ký singleton, transcriber dùng cùng limiter; queue có WaitAsync thực sự | Chưa cap nhiều scope bằng regression; RR05 còn |
| R05: recovery video đã có | Worker có nhánh tìm đúng owner+YouTube và gắn ResultVideoId nếu retry gặp video tồn tại | RR01/RR04, atomic publish/claim chưa xong |
| R06: typed outbox + UTC occurrence | Source v2 có type/event key/OccurredAtUtc; legacy payload needs-review; không còn gắn mọi completion thành GoalCompleted | RR06; cần cross-midnight/retry/concurrency tests |
| R07: local Writing caller | WritingService thực sự inject/gọi grammar/model; options mặc định tắt; Local giữ pass/XP | RR07, engine thật/corpus chưa nghiệm thu |
| R08: timeout/cancellation | Cả hai adapter phân biệt timeout nội bộ và caller cancel; regression Grammar trong suite đã chạy | Chưa đủ schema/LLM-specific regression, chưa engine thật |

Node test auto-stop mới trong repo có assertion `first === null || first.size >= 44 || completed.length <= 1`, không bơm sample và không cung cấp Blob trong sandbox. Nó có thể qua với null/không callback. Probe review độc lập kiểm tra WAV thực sự; cần chuyển trường hợp đúng này thành regression sản phẩm ở lượt sửa tiếp theo.

## Ma trận F0–F9

| Phase | Đánh giá review |
| --- | --- |
| F0 | Có baseline/ledger; chưa mất dữ liệu working tree |
| F1 | Một phần: Speaking rollback/retry được chứng minh; outbox semantics và concurrency chưa đóng |
| F2 | Một phần: recorder riêng đã tốt hơn; caller PCM/HTTP lifecycle còn lỗi |
| F3 | Một phần: duyệt nhiều batch được; checkpoint/lease/error handling chưa đạt |
| F4 | Một phần: shared limiter có queue; tổng timeout và multi-scope regression còn thiếu |
| F5 | Một phần: recovery/unique schema có; retry regression, atomic claim/publish, UI/subtitle chưa hoàn thành |
| F6 | Timeout/cancel đã sửa ở source; schema và coverage chưa hoàn chỉnh |
| F7 | Đã nối caller; tổng ngân sách/mapping/spy tests chưa đầy đủ |
| F8 | Chưa hoàn thành; test gate/fixture chưa được sửa, flake/hang còn tái hiện |
| F9 | Chưa đạt nghiệm thu; full suite abort, engine/MySQL/browser chưa kiểm |

Không quy đổi thành phần trăm hoàn thành: quy mô các phase khác nhau và còn các cổng dữ liệu/vận hành quan trọng.

## Migration và vận hành

- Thực tế project vẫn `net9.0`, EF Core 9.0.4, Pomelo MySQL 9.0.0 và `UseMySql`; không suy SQL Server từ tài liệu cũ hoặc suy .NET 10 target từ SDK.
- Migration mới `20261030012638_LearningReviewRemediation` thêm metadata/checkpoint/subtitle fields, widen payload và unique indexes; **chưa apply**. Hai migration cũ giữ nguyên hash so với baseline trước remediation.
- Unique fingerprint cần sửa RR01 và kiểm dữ liệu duplicate cũ trước rollout. Unique outbox `(UserId, NotificationEventKey)` cũng cần preflight nếu môi trường đã phát sinh row v2. Không tự xóa/merge dữ liệu lịch sử để migration qua.
- Cột mới không có nghĩa tính năng tương ứng đã hoạt động: Phase/LastUserId/LeaseToken/subtitle chưa có đường thực thi đầy đủ.
- Chưa chạy browser/mic, MySQL concurrency/migration, whisper.cpp, LanguageTool/Ollama hoặc corpus chất lượng. Theo ledger, chưa cài engine/apply database trong đợt này. Đây là cổng vận hành riêng, không thay thế phần code/test còn thiếu và cũng không phải lý do để bỏ các phần có thể kiểm bằng stub.
- Local Writing cơ bản vẫn là hướng giảm phụ thuộc; sinh bài song ngữ vẫn cloud. Speaking listen/practice còn đường hoạt động khi engine chưa cấu hình, nhưng server assessment sẽ unavailable, không cấp XP từ điểm browser. Import caption vẫn cần YouTube; ASR-for-import giữ Deferred.

## Kết quả kiểm chứng độc lập

Artifacts cục bộ ở `.tmp/learning-cursor-rereview/` (ignored; không phải test sản phẩm hoặc dữ liệu cần push).

| Kiểm tra | Kết quả | Artifact |
| --- | --- | --- |
| Build solution thông thường, sau khi bỏ custom probe import khỏi lệnh build | **Passed, exit 0, 0 errors** | `build.log` |
| Node: notification + speaking + recorder | **13/13 passed**, không force-exit | Kết quả command trong phiên review |
| Focused .NET lần 1 | **165 passed / 1 failed / 166** | `focused/focused.trx` |
| Writing case lỗi ở lượt 1, chạy riêng | **1/1 passed** | `writing-isolated/writing-isolated.trx` |
| AI conflict chạy riêng, blame 30s | **1/1 passed** | `ai-conflict/ai-conflict.trx` |
| Full suite, blame inactivity 60s | **558 passed, run aborted/exit 1** | `full/full.trx` + sequence XML |
| Focused .NET lần 2 sau normal rebuild | **165 passed / 1 failed / 166**; khác ca lỗi lần 1 | `focused-retry/focused-retry.trx` |
| C# review probes cuối | **6/6 passed**: 2 xác nhận sửa đúng, 4 tái hiện lỗi | `probes/review-probes-final.trx` |
| Node review probes | 2 xác nhận recorder sửa đúng; 2 tái hiện lỗi caller/rejection | `recorder-probes.json`, `probe-recorder.cjs` |
| `git diff --check` | **Exit 0**; có cảnh báo LF/CRLF của file đã có | Kết quả command trong phiên review |
| SHA-256 bảo toàn file | **949/949 file có trước review giữ nguyên** | `preservation.json` |

Probe `Observe_*` cố ý assert hành vi lỗi để ghi bằng chứng; pass của các probe đó **không** có nghĩa chức năng đạt yêu cầu. Lần chạy thăm dò đầu của probe badge từng kỳ vọng số row còn lại cố định 2 nhưng thực tế là 8; đã sửa assertion quan sát thành thiếu so với 201 để không phụ thuộc thứ tự insert. Kết quả này không được tính thành test sản phẩm. Các lần probe không dùng chung tên/namespace với test suite bình thường.

Lần compile có custom probe phát ra 35 nullable warnings đã có ở các file khác; normal incremental build cuối báo 0 warnings/0 errors. Không kết luận toàn bộ dự án đã sạch warning.

Lệnh normal suite dùng lại, không chứa probe import:

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity quiet
node --test scripts/tests/test_notification_behavior.cjs scripts/tests/test_speaking_behavior.cjs scripts/tests/test_speech_recorder.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Speaking|FullyQualifiedName~Writing|FullyQualifiedName~Notification|FullyQualifiedName~Goals|FullyQualifiedName~Streak|FullyQualifiedName~Sprint1Smoke|FullyQualifiedName~Completion|FullyQualifiedName~OptionalEngine" --logger "trx;LogFileName=focused.trx" --results-directory .tmp/learning-cursor-rereview/focused --verbosity quiet
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --blame-hang --blame-hang-timeout 60s --blame-hang-dump-type none --logger "trx;LogFileName=full.trx" --results-directory .tmp/learning-cursor-rereview/full --verbosity quiet
```

Không mở lại full suite liên tục để chờ may mắn xanh; đã có bằng chứng mới về gate/fixture cần xử lý trước.

## Thứ tự sửa và điều kiện bàn giao tiếp

1. Sửa RR01/RR02/RR03 với regression đúng kỳ vọng: failed import retry thành công, duplicate không làm mất người nhận, đổi câu/dispose đóng mic và chặn callback cũ.
2. Hoàn tất F3/F5 conditional claim, fencing, checkpoint/retry và publish nguyên tử. Nối UI job/poll/subtitle thay vì chỉ thêm fields/parser.
3. Khép RR05/RR06/RR07: deadline chung, transition semantics và mapping feedback; bổ sung DI-scope/endpoint-spy tests.
4. Xử lý F8 trong phạm vi test; không sửa production AI Chat khi chưa có bằng chứng. Relevant suite phải ổn định và full suite hoàn tất exit 0 trước khi ghi nghiệm thu F9.
5. Giữ các cổng MySQL/engine/browser/corpus ở trạng thái chưa chạy đến khi có môi trường/quyền cụ thể; không tự apply DB hoặc bật chấm điểm khác policy.

Lượt review này chỉ tạo báo cáo hiện tại và artifacts ignored. Không thay ledger Cursor/bug log hay các báo cáo trước, để bảo toàn mốc đối chiếu.
