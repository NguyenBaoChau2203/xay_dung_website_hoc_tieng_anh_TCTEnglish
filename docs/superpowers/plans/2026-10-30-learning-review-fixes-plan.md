# TCT English — Plan sửa các lỗi sau review triển khai Learning

> Một agent trong Cursor dùng model chủ nhóm chọn **Grok 4.7 High**, tự triển khai, review và kiểm chứng inline. Không subagent, reviewer riêng, chat/CLI AI khác. Dùng workflow phù hợp của `tct-superpowers`; không thực hiện các chỉ dẫn delegation/commit tự động từ tài liệu upstream.

**Ngày:** 30/10/2026 (UTC+7). **Trạng thái tài liệu:** READY FOR CURSOR EXECUTION — chưa có bản sửa ứng dụng nào được thực hiện bởi việc viết plan này.

**Mục tiêu:** sửa R01–R08 có bằng chứng từ review, khép các đường đi người dùng còn thiếu và phục hồi khả năng kiểm thử đáng tin; giữ các tính năng/phân quyền đang hoạt động.

**Kiến trúc:** giữ MVC/EF/MySQL; dùng transaction chung cho hoàn thành và ghi thưởng, outbox chứa đúng sự kiện, worker có checkpoint/lease thực sự, limiter ASR chia sẻ, lifecycle recorder hoàn tất đúng một lần. Engine Writing là feedback tùy chọn, không thay quyết định chấm Local.

**Stack đã kiểm:** net9.0, EF Core 9.0.4, Pomelo MySQL 9.0.0, Razor/Bootstrap/JavaScript. Không nâng framework/DB/package trong lần sửa này.

**Spec bắt buộc:** [review R01–R08](../../feature-audits/2026-10-30-cursor-learning-implementation-review.md).

**Bối cảnh:** [plan gốc L0–L7](2026-10-29-bao-chau-learning-reliability-plan.md), [ledger của Cursor](../../feature-audits/bao-chau-learning-execution-ledger.md).

**Prompt thực thi:** [Cursor Grok — sửa review](../../../.ai/prompts/cursor-grok-learning-review-fixes.md).

## 1. Quyền thực thi, giới hạn và nguồn sự thật

Plan này hướng dẫn sửa trên working tree hiện tại, không yêu cầu làm lại từ đầu. Lệnh chat đi kèm dùng để chủ nhóm giao **toàn bộ F0–F9**. Khi đã nhận lệnh đó, Cursor thực hiện tuần tự và tự tiếp tục sau mỗi bước đạt kiểm thử; không dừng hỏi duyệt lại từng bước.

Phạm vi cho phép khi được giao thực thi:

- Speaking người học, Writing người học, Goals/Notifications cùng service/model/worker/test trực tiếp phục vụ việc sửa R01–R08.
- `Program.cs` **chỉ** chỉnh DI/lifetime/options của transaction orchestration, notification intent/outbox, job lease, ASR limiter và Writing optional engines được mô tả trong plan. Không sửa auth, connection string, startup seeding hoặc provider AI toàn cục.
- Model/DbContext/snapshot và **migration bổ sung có review** cho outbox/checkpoint/import idempotency. Không sửa/xóa hai migration đã tạo trước; không apply DB thật.
- Bổ sung regression tests, sửa test infrastructure có nguyên nhân xác minh. Riêng `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` chỉ sửa đồng bộ/lifecycle/timeout của test bị treo; không sửa hành vi AI Chat production.
- Chỉnh tối thiểu view/JS cho trạng thái thu âm, import job/phụ đề, feedback Writing. Giữ giao diện/aria/selectors/hunk Antigravity. Đọc DESIGN.md trước thay view; không redesign/CSS toàn cục.
- Cập nhật ledger, bug memory, hướng dẫn vận hành và báo cáo sửa lỗi.

Không được làm trong lệnh này:

- Không đổi chức năng Auth/Profile/Settings, Contact, AI Chat, Admin, Billing, Vocabulary/Set/Folder/Class/Chat, Reading/Listening. Chỉ đọc caller của chúng và chạy regression để bảo toàn hợp đồng.
- Không sửa `.csproj`, `appsettings*`, secrets, skills/rules/hooks/MCP hoặc thêm dependency/package mới. Nếu thực sự cần package, trình tên/version/lý do/phạm vi riêng; vẫn hoàn thành phần độc lập.
- Không drop/truncate bảng/cột, xóa gộp dữ liệu lịch sử, tính lại XP/điểm cũ hoặc chạy migration trên DB thật. Không auto backfill credit thiếu vì dữ liệu cũ có thể đã được thưởng bằng đường trước ledger.
- Không tải/cài model/whisper.cpp/Java/LanguageTool/Ollama, mở cổng public, gọi API trả phí hoặc gửi dữ liệu thật ra ngoài. Dùng fixture/stub để sửa code; vận hành engine là cổng nghiệm thu riêng.
- Không commit/push/PR/merge/deploy; không reset/stash/clean/checkout đổi nhánh để xóa diff. Không re-index/sync CodeGraph.
- Không skip/delete test, tăng timeout vô hạn, bắt mọi exception rồi trả success, quay lại demo/điểm client hoặc lấy cloud làm đường cứu mặc định.

Baseline review: HEAD `3d27a68c8468207eca5a11653c72e735117fc637`, nhánh `feature/bao-chau-ui-audit`, 47 tracked files modified và nhiều file untracked. Chụp baseline mới ở F0; không giả định HEAD thể hiện đầy đủ code đang review. CodeGraph đang thiếu nhiều file mới: gọi trước, sau đó đọc đúng file stale/unindexed, không tự sửa index.

## 2. Ánh xạ và thứ tự thực hiện

| Bước | Phát hiện | Kết quả bắt buộc |
| --- | --- | --- |
| F0 | Baseline và điều kiện thực thi | Phạm vi/diff/contract được ghi lại; không mất hunk UI |
| F1 | R01 + R06 | Completion/credit atomic, outbox đúng nghĩa và thời điểm |
| F2 | R02 | Auto-stop giữ WAV; late permission/cancel không mở lại mic |
| F3 | R03 | Worker xử lý hết user theo batch/checkpoint và lease có điều kiện |
| F4 | R04 | Giới hạn ASR dùng chung, hàng đợi hữu hạn, timeout/cancel đúng |
| F5 | R05 + phần L5 chưa tích hợp | Import job bền vững, UI poll 202, phụ đề có đường sử dụng thật |
| F6 | R08 | Hai adapter phân biệt timeout nội bộ và caller cancellation |
| F7 | R07 | Engine Writing tùy chọn được nối vào luồng feedback, default off |
| F8 | Kiểm thử chưa đóng | Sửa đồng bộ test treo/điều tra flake trong phạm vi cho phép |
| F9 | Bàn giao | Ma trận bằng chứng R01–R08, ledger đúng trạng thái, cổng vận hành riêng |

F1 và F6 cung cấp nền cho các bước sau. Không phát hành từng trạng thái trung gian; tất cả là một đợt sửa local, được kiểm chứng theo bước. Nếu một bước bị chặn bởi môi trường/ngoài quyền, ghi blocker cụ thể rồi làm các bước độc lập còn lại; không tick done phần bị chặn.

## 3. Review focus và invariant xuyên suốt

1. Lỗi DB sau khi ghi progress, trước credit, hoặc ngay trước commit: không để completed không thể nhận thưởng; retry không thưởng lặp. F1 phải test qua endpoint Speaking **và** Writing.
2. Permission/audio/HTTP callback về muộn: không mở lại mic, gửi hai lượt hoặc hiển thị kết quả vào câu mới. F2 phải test vòng đời thực của script.
3. Restart, lease hết hạn, worker cũ còn chạy: không bỏ user/job và không để worker hết quyền publish kết quả. F3/F5 cần hai worker/DbContext trong test.
4. Cancel trong hàng đợi/timeout engine: trả đúng trạng thái, nhả slot/track/temp data, không mất draft/attempt hoặc làm chết toàn bộ worker. F4/F6/F7.
5. Ngày Việt Nam khác UTC, dispatch chậm qua nửa đêm, user có nhiều goal/badge: giữ businessDate/event key và occurrence timestamp của sự kiện. F1/F3.

Các nguyên tắc chốt: CSRF cho mutation, userId từ server, owner/Premium checks, NotFound cho IDOR; legacy routes/JSON giữ tương thích trừ thay đổi có mô tả ở đây. Không đổi năm GoalArea hoặc công thức XP. Không gắn nhãn fluency/prosody từ text-match. Engine/DB thật chưa được kiểm thì không gọi production-ready.

## 4. F0 — Baseline, tài liệu và regression ledger

**Tạo:** `docs/feature-audits/bao-chau-learning-remediation-ledger.md`.

- [ ] Đọc AGENTS.md, `.ai/README.md`, review, plan này, known issues/bug log/conventions, rồi đúng các skill TCT liên quan. Không nạp lại toàn bộ toolkit.
- [ ] Ghi branch/HEAD/status, danh sách tracked/untracked có sẵn và hash/diff các file sẽ sửa. Đánh dấu UI của Antigravity và phần Cursor đã làm. Phát hiện cùng file đang bị chỉnh đồng thời thì bảo toàn hunk, làm phần độc lập.
- [ ] CodeGraph trước định vị caller/service/test; xác nhận source thật và chữ ký hiện tại. Ghi contract của receipt Speaking, JSON evaluate Writing, import 202/status, notification keys và migration hiện có.
- [ ] Đọc artifact review nếu còn ở `.tmp/learning-cursor-review/`. Năm C# probe **assert lỗi đang tồn tại**, vì thế pass của chúng không phải sản phẩm đúng. Chuyển thành regression kỳ vọng đúng; không copy nguyên assert lỗi vào suite rồi tuyên bố fix.
- [ ] Nếu artifact ignored không có trên checkout khác, dùng kịch bản tái hiện được ghi ngay trong F1/F2/F3/F6; không bắt chủ nhóm tìm/gửi lại file để trì hoãn.
- [ ] Baseline review: build 0 lỗi/35 nullable warnings; Node 12/12; filter liên quan từng 162/163 rồi retry 163/163; full suite abort ở ca AI conflict sau 555 pass. Ghi mốc lịch sử; chạy baseline hiện tại cần thiết một lần, không lặp full suite đang treo để chỉ lấy số xanh.

**Done:** ledger đủ baseline, phạm vi được giao, failure modes và đường kiểm chứng. Sau F0 tự tiếp tục F1 khi đã có lệnh thực thi toàn plan.

## 5. F1 — Giao dịch hoàn thành/thưởng và outbox đúng sự kiện (R01, R06)

**Sửa:** `TCTEnglish/Controllers/SpeakingController.cs`; `Services/WritingService.cs`; `Services/Learning/CompletionCreditService.cs`; phần bounded mới trong `GoalsService.cs`/`IGoalsService.cs`, `StreakService.cs`/`IStreakService.cs`, `NotificationService.cs`; `Services/Notifications/LearningOutboxDispatcher.cs`; model outbox và DbContext nếu thêm metadata; DI trong Program.

**Tạo nếu cần để giữ controller mỏng:** `TCTEnglish/Services/Speaking/SpeakingProgressService.cs` với `ISpeakingProgressService.CommitAssessmentAsync(int userId, int sentenceId, Guid assessmentId, CancellationToken ct)`; kết quả giữ các trường progress/XP hiện có. Không dựng framework repository/unit-of-work tổng quát cho cả app.

**Tests:** mở rộng `TCTEnglish.Tests/GoalsCompletionRecoveryTests.cs`; tạo `LearningOutboxReliabilityTests.cs`; giữ GoalsPhase*, WritingProgress và SpeakingAssessment tests.

### Quyết định transaction

- Chọn **transaction chung cùng scoped DbContext** cho progress/consume receipt/completion transition/unique credit/daily activity/streak/badges/outbox intents. Chấm ASR/AI/grammar, tải media và I/O mạng nằm ngoài transaction.
- `CompletionCreditService` tham gia `Database.CurrentTransaction` nếu caller đã mở; chỉ tự mở/commit khi thực sự là owner của transaction ở đường standalone. Không nested transaction hoặc commit transaction của caller.
- Speaking bắt đầu transaction trước khi consume receipt/upsert tiến độ. Writing bắt đầu trước persist attempt/upsert progress. Chỉ trả success sau commit. Lỗi ở bất kỳ bước ghi nào rollback toàn đơn vị, trả lỗi có thể retry; giữ draft phía client.
- Chỉ nhận diện đúng duplicate credit key như replay; không coi FK, timeout, disk/storage error hoặc mọi SQLite code 19 là duplicate. Race recovery phải rollback context lỗi rồi load/retry có kiểm soát với state sạch, không nuốt lỗi hoặc detach toàn bộ entity đang chờ của caller.
- Giữ credit key user+feature+content như hiện tại; không thêm revision/ngày làm cớ thưởng lại bài cũ. Với completion lịch sử không có ledger, xuất thống kê đọc-only và ghi unknown; không tự cấp lại XP.

### Quyết định notification intent

- Thêm đường gọi **riêng cho completion Speaking/Writing** để Goals/Streak tạo durable notification intents trong transaction, thay vì direct insert rồi catch/silent drop. Đường caller cũ của Vocabulary/Reading/Listening giữ nguyên hành vi; default không bị chuyển ngầm sang outbox.
- Dùng payload version 2 đã validate: loại thông báo thật, event key, title/message/icon/relatedUrl, occurrence UTC và businessDate snapshot. Chỉ tạo GoalCompleted khi target đạt, GoalProgress khi đúng mốc, StreakRecord theo previous longest, BadgeEarned theo badgeId. Không phát generic completion dưới tên GoalCompleted.
- Notification `EventKey` của direct/outbox dùng cùng semantic identity để chống trùng xuyên hai đường. Outbox eventId vẫn unique; nếu cần thêm EventKey/OccurredAtUtc nullable và unique user+EventKey thì dùng migration bổ sung, giữ row cũ.
- Dispatcher lấy occurrence UTC thật (có thể fallback CreatedAtUtc có nguồn rõ), không `BusinessDate.AddHours(12)`, không tính lại ngày/target hiện tại để thay snapshot cũ. Save notification + processed trong cùng transaction; duplicate đúng key được nhận diện và event có thể đóng idempotent.
- Payload legacy `learning.completion` thiếu nghĩa notification không được đoán thành goal completed. Ghi failed/needs-review có lý do và giữ nguyên dữ liệu để kiểm tra; lỗi một event không chặn event hợp lệ tiếp theo. Không xóa thông báo lịch sử đã sai.
- Khi dispatch thất bại, rollback rồi ghi retry metadata bằng state sạch, processed vẫn null. Giới hạn 10 attempts, backoff hữu hạn, giữ failed event để kiểm tra. Caller cancellation dừng sạch, không bị tính như lỗi nghiệp vụ. Không fire-and-forget dùng DbContext đã dispose.

### Các bước và acceptance tests

- [ ] Viết regression đỏ **Speaking**: trigger/interceptor chặn INSERT credit ở DB thử; nộp đủ receipt để hoàn thành. Kỳ vọng không HTTP success giả; completion/consume/progress mới và credit được rollback cùng nhau. Bỏ lỗi, retry bằng receipt hợp lệ → đúng một credit, +20XP completion, không mất thưởng.
- [ ] Viết regression tương tự **Writing**: gây lỗi sau attempt/progress trước credit, rollback; retry nội dung đúng → một completion credit, không cộng lặp. Thất bại một transaction không làm hỏng request sau.
- [ ] Sửa orchestration và error classification tối thiểu; truyền cancellation xuyên các I/O mới, bỏ `CancellationToken.None` ở đường request trừ cleanup rollback có ngân sách riêng.
- [ ] Test hai DbContext đồng thời cùng completion; hai feature cùng user cùng ngày; streak XP một lần, tổng XP đúng, best score không tụt và không có hai row progress. Không serialize bằng khóa static chỉ để test qua SQLite.
- [ ] Test response bị mất sau commit rồi replay cùng receipt còn hợp lệ: trả trạng thái server đã lưu, không thêm credit/XP. Receipt user/câu khác/hết hạn vẫn không ghi dữ liệu; không nhận lại numeric score client.
- [ ] Test outbox: user chưa có goal không có GoalCompleted; goal đạt mới có một event đúng; hai lĩnh vực cùng ngày không chặn nhau; badge/streak đúng loại. Sự kiện 09:00 VN giữ timestamp đó dù drain sau 00:00 ngày kế tiếp.
- [ ] Test dispatch lỗi một lần rồi hồi phục, duplicate dispatch hai context, malformed/legacy payload, retry limit và event sau vẫn chạy. Không đặt processed thành công trước khi dữ liệu thật commit.
- [ ] Chạy toàn bộ regression F1, build, review diff; ledger đóng R01/R06 ở mức code/test khi đủ bằng chứng, cổng MySQL vẫn riêng.

**Done:** endpoint fault-injection không còn tái hiện completed mất thưởng; retry/crash/duplicate không tăng XP sai; notification có đúng nghĩa/thời gian và intent bền vững.

## 6. F2 — Recorder hoàn tất đúng một lần (R02)

**Sửa:** `TCTEnglish/wwwroot/js/speech-recorder.js`, `wwwroot/js/speaking.js`; chỉ trạng thái/phát lại bản thu cần thiết ở `Views/Speaking/Practice.cshtml`. **Tests:** mở rộng `scripts/tests/test_speech_recorder.cjs`, `test_speaking_behavior.cjs`, `dom_fixture.cjs`.

**Contract mới trong API recorder hiện có:** `create({ onComplete, onError })`; `start()` có generation guard; `stop()` trả cùng completion Promise cho lần gọi lặp; `dispose()` hủy và cleanup idempotent. `onComplete` là đường consumer gửi chấm duy nhất của caller; manual/auto stop không tự gửi thêm từ `.then()` lần hai. Có thể giữ wrapper không truyền options cho caller/test cũ.

- [ ] Test đỏ auto-stop bằng fake timer/source audio thật: đủ mốc 15 giây vẫn lấy được một WAV hợp lệ, caller gửi assess/commit tối đa một lần. Manual stop trùng timer không sinh hai request hoặc null do chunks bị xóa sớm.
- [ ] Test đỏ dispose/đổi câu trong lúc getUserMedia pending; khi permission resolve, stop mọi track ngay, không tạo AudioContext/recorder mới và không POST.
- [ ] Tách finish-once state, giữ blob/Promise tới consumer; cleanup chỉ sau khi đã chốt kết quả. Giới hạn output **tối đa 240.000 sample 16kHz** để frame cuối không vượt 15 giây làm validator từ chối. WAV mono PCM16/16kHz, giới hạn 2MiB giữ nguyên.
- [ ] Thêm generation checks sau từng await start và callback/HTTP; cancel/đổi câu/rời trang abort request còn pending, kết quả cũ không ghi đè UI câu mới. Nếu server đã commit thì không giả rollback ở client, chỉ reload trạng thái đúng câu.
- [ ] Nếu AudioContext/source/processor init fail sau khi mở stream, cleanup track/context trước trả lỗi; dispose gọi nhiều lần an toàn, không giữ timer/audio graph/blob URL.
- [ ] Duy trì nghe bản gốc và nghe lại/xóa bản thu trong trạng thái ASR unavailable bằng UI tối thiểu nếu chưa có. Không giữ mic/audio lâu hơn cần thiết, không chuyển sang demo/client score để có XP.
- [ ] Test 44.1/48kHz input, max frame, silence/no chunks, permission denied, cancel, start/stop nhanh; chạy code script thật, không chỉ assert tên exports. Chạy Node suite không `--test-force-exit`.

**Done:** auto/manual stop và late permission có regression xanh; không double-submit, không mở lại mic sau cancel; vẫn ghi rõ browser/mic thật chưa kiểm nếu chỉ dùng fixture.

## 7. F3 — Notification worker có tiến độ bền vững (R03)

**Sửa:** `TCTEnglish/Workers/NotificationWorker.cs`, `Models/NotificationJobRun.cs`, DbContext/snapshot và migration bổ sung. **Tests:** mở rộng `NotificationWorkerRecoveryTests.cs`, `NotificationReliabilityIntegrationTests.cs`.

**Thiết kế checkpoint:** thêm `Phase` nullable/default cho dữ liệu cũ và `LastUserId` nullable. Các phase `streak`, `badge`, `cleanup`, `complete`; mỗi phase duyệt userId tăng dần theo keyset, batch <=200. Chỉ succeeded sau complete. Không dùng Take(200) một lần như giới hạn tổng user.

- [ ] Regression đỏ 201 và 450 user: lặp poll có giới hạn đến complete; tất cả user đủ điều kiện có đúng một thông báo, không bỏ người thứ 201/401; blocked user bị bỏ đúng.
- [ ] Claim bằng conditional DB update trên trạng thái/lease hết hạn; rowsAffected=1 mới sở hữu. Mỗi lần claim có lease token riêng, mọi heartbeat/checkpoint/finish update kèm token; worker cũ mất lease không được chốt thành công.
- [ ] Lease 5 phút; renew ít nhất mỗi phút khi đang xử lý. Không chạy heartbeat song song trên cùng DbContext. Mỗi poll xử lý tối đa một batch, lưu phase/cursor và giữ running nếu còn dữ liệu; không reset cursor khi tiếp tục cùng lease.
- [ ] Event insert và checkpoint của batch không được tạo khoảng skip khi crash; unique event key giúp replay an toàn. Chỉ bắt đúng duplicate; lỗi DB khác khiến batch retry, không detach mọi notification rồi coi batch đã xử lý.
- [ ] Retry job hữu hạn, tối đa 3 lần acquisition thất bại/ngày; không tính mỗi batch thành một attempt mới. Hết lượt giữ failed/LastError để owner xử lý; không vòng nóng mỗi phút vô hạn.
- [ ] Cleanup thông báo cũ là đơn vị độc lập hoặc đường finally được kiểm chứng: lỗi streak/badge không khiến cleanup biến mất cả ngày. Không tạo nhắc nhở của ngày cũ lúc restart.
- [ ] Test restart giữa batch; hai worker tranh claim; lease hết hạn và worker cũ finish muộn; lỗi DB/retry; cùng ngày chạy lại không trùng. Điều khiển TimeProvider thay sleep theo thời gian thật.

**Done:** nhiều batch thực sự đi hết tập user, trạng thái chỉ complete sau hết việc và có proof restart/lease; không suy từ SQLite sang multi-instance MySQL đã đạt.

## 8. F4 — Limiter ASR dùng chung và có hàng đợi (R04)

**Sửa:** `Services/Speaking/WhisperHttpTranscriber.cs`, `SpeechProcessingOptions.cs`, `SpeakingAssessmentService.cs`, Program.cs DI. **Tạo:** `Services/Speaking/SpeechInferenceLimiter.cs`; `TCTEnglish.Tests/SpeechInferenceLimiterTests.cs` (đặt namespace chứa Speaking để lọc kiểm thử liên quan).

**Contract:** limiter singleton cho endpoint ASR được cấu hình; `AcquireAsync(CancellationToken ct)` trả lease phải dispose, hoặc outcome busy/cancel. Typed HTTP client vẫn lifetime phù hợp HttpClientFactory, chỉ state admission chia sẻ; không đặt scoped DbContext vào singleton.

- [ ] Test đỏ nhiều DI scope nhận các transcriber khác nhau nhưng handler HTTP chỉ thấy max concurrency 1; bốn người chờ, lượt thứ sáu bị từ chối mà chưa gọi engine.
- [ ] Admission có tổng running+queued hữu hạn, `SemaphoreSlim`/counter chia sẻ hoặc primitive có sẵn của framework. Request chờ thật, không `WaitAsync(0)` rồi gọi đó là queue. Validate options không âm/0 vô nghĩa.
- [ ] CTS tổng bao cả chờ+inference, mặc định 15 giây; cancel/timeout chờ phải nhả membership, không rò slot. Không retry nhiều tầng, không kéo tổng thời gian theo số lượt chờ.
- [ ] Busy trả **429 + Retry-After**; unconfigured/unreachable 503, timeout 504; payload/malformed response có lỗi hữu hạn và không cấp assessment giả. Không public `/load`, không nhận URL/model/path từ client.
- [ ] Test cancel ở queue và khi chạy, handler ném lỗi/JSON sai, slot phục hồi, engine_not_ready zero HTTP calls. Giữ các guard user/câu/CSRF.
- [ ] Docs ghi giới hạn này theo process. Nếu triển khai nhiều web instance, ghi yêu cầu limiter/gateway ở ASR service; không tự claim giới hạn toàn cluster hoặc dựng hạ tầng mới.

**Done:** cap được kiểm qua DI nhiều scope, hàng đợi thật và cleanup đúng. Load test/model thật còn là cổng vận hành, không chặn regression fixture.

## 9. F5 — Import job, UI và phụ đề đi hết luồng (R05)

**Sửa:** `Services/Speaking/SpeakingImportJobService.cs`, `Workers/SpeakingImportWorker.cs`, `Services/SpeakingService.cs`/ISpeakingService, `Controllers/SpeakingController.cs`, `Models/SpeakingImportJob.cs`, DbContext/migration, `wwwroot/js/speaking.js`, phần form import của `Views/Speaking/Index.cshtml`. **Tests:** mở rộng `SpeakingImportReliabilityTests.cs`, `PremiumSpeakingLifecycleIntegrationTests.cs`; tạo `scripts/tests/test_speaking_import_behavior.cjs`.

**Giữ:** learner import captions/subtitle, không Gemini; Listening/Admin giữ pipeline/hợp đồng cũ. Không cài yt-dlp/ffmpeg hoặc tải audio YouTube để mở rộng sang ASR import trong task sửa này.

- [ ] Tách prepare I/O (caption/metadata/parser) khỏi persist; **publish video/câu + job result succeeded trong cùng transaction**, sau recheck ownership/Premium và lease token. Network không nằm trong transaction.
- [ ] Reuse pattern conditional claim/fencing F3. Max3 attempts, backoff+jitter có NextAttemptAtUtc, lease/heartbeat và timeout tổng 5 phút; cancel dừng sạch. Không hai worker cùng publish từ một lease.
- [ ] RequestFingerprint có unique user+fingerprint. Fingerprint gồm canonical YouTube id + source kind + hash phụ đề chuẩn hóa nếu có; request đồng thời trả cùng job hợp lệ. Kiểm duplicate cũ trước tạo unique index; không tự xóa row để migration qua.
- [ ] Sau crash/retry, nếu video đúng owner/source đã được tạo, phục hồi result tương ứng thay vì báo failed “đã có video”. Unique video owner+YouTube vẫn giữ. Không ghi đè transcript video có sẵn hoặc trả video owner khác.
- [ ] User resubmit job failed/cancelled được retry có chủ đích; job succeeded nhưng video đã bị owner xóa có đường re-import hợp lệ. Không trả vĩnh viễn succeeded với result đã mất; không tạo vòng re-import tự động.
- [ ] UI chuyển sang POST `/api/speaking/imports`, nhận 202 + jobId/statusUrl, poll status (5 giây, backoff tới 30 giây, pause khi hidden/offline). Sau 5 phút chờ UI có thể kết thúc theo dõi nhưng không giả cancel job còn chạy; cho resume theo jobId owner-scoped. Giữ đường `/Speaking/My/Create` tương thích cho caller cũ, không dùng nó trong UI mới.
- [ ] Thêm flow phụ đề thực tế trong form hiện có: file SRT/WebVTT tùy chọn + YouTube URL, POST multipart `/api/speaking/imports/subtitles`; Authorize/CSRF/owner/Premium như enqueue. Payload <=1MiB, <=2.000 cue, media <=30 phút; không tin MIME/tên file.
- [ ] Parser hỗ trợ timecode WebVTT có/không phần giờ, cue settings/identifiers, BOM/multiline; validate phút/giây 0–59, hữu hạn và 0<=start<end<=duration, thứ tự/overlap theo policy rõ. Không silently nuốt cue timestamp sai rồi báo success cho phần còn lại. Encode text ở Razor/JS.
- [ ] Nếu lưu phụ đề để worker đọc, thêm SourceKind và payload cue đã validate có giới hạn, nullable cho job cũ; không lưu file path tùy ý từ client. Metadata lấy từ YouTube theo id hợp lệ, không fetch arbitrary URL.
- [ ] Test endpoint enqueue/owner status, double enqueue, two workers, role đổi sau prepare trước publish, crash trước/sau commit, re-import sau delete, captions missing, parser lỗi, polling 202/retry/error/success. Dùng transcript stub có đếm calls: learner path gọi Gemini **0 lần**.
- [ ] ASR-for-import không được bật trong task này. Ghi **Deferred** rõ trong ledger/ops vì chưa engine/quyền media; không quảng bá đã làm toàn bộ pipeline offline.

**Done:** người học thật sự đi qua job, captions hoặc phụ đề có đường sử dụng, restart không biến kết quả thành công thành failed. Runtime YouTube/MySQL thật chỉ đóng khi có môi trường được phép.

## 10. F6 — Timeout/cancel của adapter Writing (R08)

**Sửa:** `Services/Writing/LanguageToolGrammarChecker.cs`, `OllamaWritingModel.cs`. **Tests:** mở rộng `TCTEnglish.Tests/WritingOptionalEngineContractTests.cs`.

- [ ] Test đỏ handler chờ cancellation với internal timeout 1 giây: cả hai adapter trả Available=false có mã/lý do timeout; không ném OperationCanceledException ra core khi caller chưa hủy.
- [ ] Test caller token hủy: propagate OperationCanceledException, không nuốt thành unavailable; token đã hủy không gọi network. Phân biệt bằng caller token và linked CTS.
- [ ] Catch HTTP/JSON/schema không hợp lệ thành unavailable có cấu trúc, không catch lỗi lập trình tùy ý thành success. Không báo Available=true cho JSON bất kỳ hoặc field message.content sai kiểu.
- [ ] Kiểm giới hạn text/payload/response, UTF-16 offset findings hợp lệ với text có Unicode, schema feedback typed. LLM lấy và validate `message.content`, không trả nguyên envelope Ollama như feedback đã chuẩn hóa.
- [ ] Test HTTP 400/429/500, malformed/empty/wrong schema, cancellation trong read body, disabled/unconfigured zero calls. Không cần cài engine để xác minh các hợp đồng này.

**Done:** regression timeout xanh, caller cancellation đúng, malformed không tạo phản hồi giả.

## 11. F7 — Nối engine Writing tùy chọn vào luồng người học (R07)

**Sửa:** constructor/evaluation `Services/WritingService.cs`; `Writing/ILocalGrammarChecker.cs`, `ILocalWritingModel.cs` cùng adapter/options nếu cần; `ViewModels/WritingIndexViewModel.cs`, `wwwroot/js/writing.js`, view trạng thái Writing tối thiểu; Program.cs DI có giới hạn. **Tests:** `WritingOptionalEngineContractTests.cs`, `WritingAvailabilityIntegrationTests.cs`.

**Quyết định sản phẩm:** cả LanguageTool và local LLM chỉ bổ sung feedback có nguồn. Default off; **Local assessment policy quyết định passed/XP** như hiện tại. Không thêm engine vào đường tạo bài song ngữ trong task sửa này; `WritingService.Generation.cs` vẫn là tiện ích cloud riêng và phải được docs ghi rõ.

- [ ] Inject interface thật vào Writing orchestrator và gọi khi option tương ứng enabled. Disabled/unconfigured vẫn zero HTTP, mở bài/hint/chấm exact/lưu/reload không cần key.
- [ ] Tổng ngân sách optional feedback tối đa **3 giây/request**, gồm cloud enhancement nếu user đã opt-in, grammar và local LLM; không cộng 2+3+3 giây theo từng adapter. Dùng linked CTS tổng và timeout nhỏ còn lại. Khi budget tổng hủy token đưa vào adapter, Writing orchestrator phân biệt với request token gốc: budget hết thì bỏ enhancement và giữ Local, request gốc hủy thì propagate. Không gọi cả engine ngoài quyền đã cấu hình hoặc retry nhiều tầng.
- [ ] Mapping VM/JS cho findings/feedback nguồn `languagetool`/`ollama` và unavailable có lý do; render text an toàn, không raw HTML/JSON. Không leak reference qua narration hoặc nhập prompt instructions từ bài học làm policy điều khiển.
- [ ] Preserve Passed/CanAutoAdvance/ReviewRequired của Local trước/sau mọi optional result; câu trái nghĩa không qua vì grammar sạch hoặc model nói đúng. Không mutation pass/XP sau response.
- [ ] Adapter lỗi/timeout không làm mất draft hoặc attempt; caller hủy request thì propagate đúng và rollback phần chưa commit. Network optional ở ngoài transaction F1.
- [ ] Integration test spy enabled: endpoint evaluate thực sự gọi grammar/model và response chứa feedback đúng nguồn; disabled không gọi; engine down/JSON sai không phá core; payload độc hại được encode và reference guard hoạt động.
- [ ] Docs minh bạch: **code integrated + stub verified** khác **engine deployed/quality validated**. Chưa corpus 40 câu/benchmark thì không claim semantic grading chuẩn hoặc bật mặc định.

**Done:** bật option có tác động kiểm được trên endpoint thật bằng stub, tắt option core vẫn chạy. Việc triển khai engine/model và đánh giá chất lượng vẫn chưa được tự cấp quyền.

## 12. F8 — Kiểm thử bị treo và flake

**Sửa trong phạm vi test:** `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` — chỉ ca conflict và helper cần thiết; `Infrastructure/TestWebApplicationFactory.cs` nếu đã chứng minh nguyên nhân shared connection/schema race. Không sửa controller/service AI Chat, classifier/provider toàn cục.

- [ ] Chạy riêng `Send_WhenConversationAlreadyProcessing_ReturnsConflict` với timeout hữu hạn và lưu log. Xác định request nào thực sự đã vào provider/giữ gate; không giả định `Task.Delay(50)` là đủ đồng bộ.
- [ ] Dùng TCS tín hiệu “request đầu đã vào vùng đang xử lý”/test barrier; mọi wait có deadline 5 giây với thông báo có ích. `try/finally` luôn release gate và chờ/abort request còn lại có giới hạn; fake provider tôn trọng cancellation. Test vẫn phải chứng minh request thứ hai Conflict, request đầu OK và không double-send.
- [ ] Không đảo kỳ vọng Conflict thành OK, bỏ assertion, skip test hoặc chỉ tăng blame timeout để suite qua. Nếu root cause nằm ở behavior AI ngoài phạm vi, ghi blocker và tiếp tục các phần khác; chưa đóng full-suite gate.
- [ ] Với lỗi SQLite `DBNull/ObjectDisposedException`: đo đúng đường tạo/khởi tạo host/schema/dispose. Sửa lifetime/reset dữ liệu cho từng factory nếu có bằng chứng; không chia sẻ một connection dùng song song giữa factory, không dispose khi request/worker còn sống. Test cần background worker sẽ tự gọi worker có kiểm soát; các integration test khác không chạy worker mạng ngầm.
- [ ] Không tắt song song toàn suite hoặc thêm global lock để che race. Nếu một collection thực sự dùng tài nguyên không hỗ trợ parallel, giới hạn đúng collection và giải thích resource; không làm thay kiểm thử concurrency MySQL của F1/F3/F5.
- [ ] Sau sửa, ca conflict chạy ổn định 5 lần có timeout; ca private access từng flake chạy lại. Chạy relevant suite rồi full suite một lần hoàn tất; nếu fail/hang có bằng chứng mới, sửa nguyên nhân trong phạm vi rồi chạy lại, báo cả lần fail lẫn retry.

**Done:** có kết quả full suite **Completed, exit 0** hoặc blocker nêu rõ; 555 pass rồi abort vẫn là chưa hoàn tất.

## 13. Migration và dữ liệu — chung cho F1/F3/F5

- [ ] Sinh impact assessment theo `tct-ef-migration`: bảng/cột/index thay đổi, backward compatibility, dữ liệu duplicate hiện hữu, phương án rollout/rollback. MySQL/Pomelo là provider thật; SQLite chỉ test hành vi độc lập provider.
- [ ] Giữ hai migration đã có: `20261029175518_LearningNotificationReliability`, `20261029181904_LearningReliabilityL4ThroughL6`. Thêm migration follow-up cho metadata/checkpoint/idempotency còn thiếu, không viết lại lịch sử hoặc drop schema để dựng lại.
- [ ] Cột mới nullable hoặc có default an toàn cho row cũ; unique nullable key xử lý lịch sử rõ. Query read-only phát hiện duplicate import/credit/progress, không auto merge/delete.
- [ ] Review Up/Down; không DROP TABLE/COLUMN dữ liệu cũ. Thay index bằng DropIndex/CreateIndex cần review tác động; không suy từ đó thành quyền xóa dữ liệu. Không chạy Down mất dữ liệu mới.
- [ ] Không dùng application startup trên DB thật để tạo migration. Kiểm tra công cụ thiết kế/connection trước; nếu tooling tự connect thật thì dùng workflow thiết kế offline hoặc ghi blocker, không đọc/in secrets để lách.
- [ ] Chỉ tạo/review script và thử schema ở DB test cô lập đã được cung cấp. Apply production, xử lý dữ liệu lịch sử và tạo hạ tầng test MySQL mới cần yêu cầu riêng; không là bước tự động của plan này.

## 14. F9 — Kiểm chứng cuối, trạng thái đúng và bàn giao

Lệnh nền; build lại khi thay source/test trước dùng `--no-build`:

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity minimal
node --test scripts/tests/test_speaking_behavior.cjs scripts/tests/test_notification_behavior.cjs scripts/tests/test_speech_recorder.cjs scripts/tests/test_speaking_import_behavior.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Speaking|FullyQualifiedName~Writing|FullyQualifiedName~Notification|FullyQualifiedName~Goals|FullyQualifiedName~Streak|FullyQualifiedName~Sprint1Smoke|FullyQualifiedName~Completion|FullyQualifiedName~Outbox" --blame-hang-timeout 90s --logger "trx;LogFileName=review-fixes-focused.trx" --results-directory .tmp/learning-review-fixes/test-results --verbosity minimal
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Send_WhenConversationAlreadyProcessing_ReturnsConflict" --blame-hang-timeout 30s --verbosity minimal
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --blame-hang-timeout 90s --logger "trx;LogFileName=review-fixes-full.trx" --results-directory .tmp/learning-review-fixes/test-results --verbosity minimal
git diff --check
```

Chỉ chạy Node test mới sau khi file được tạo. Không dùng import `.tmp/.../ReviewProbes.targets` khi đo suite cuối; không để observational probes assert lỗi tồn tại lẫn vào sản phẩm. Không tăng test count bằng skip/duplicate test. Số 163 là baseline lịch sử, không phải số bắt buộc sau thêm regression.

- [ ] Ma trận R01–R08 trong ledger mới: root cause, file, regression name, red/green, command/TRX, scope source/test/engine/DB, residual blocker. Các R01/R06 phải có endpoint fault-injection; R02 có lifecycle tests; R03 có >200 users; R04 có nhiều DI scopes; R05 có restart/unique job; R07 có enabled caller; R08 có internal timeout và caller cancellation.
- [ ] Chạy browser trên môi trường test hợp lệ nếu sẵn: mic auto-stop/cancel, ASR unavailable, đổi câu lúc chờ, job poll/reload/subtitle, Writing feedback. Không tự chạy `dotnet run` trên config thật vì startup seeding, không dùng tài khoản/audio/key thật để “thử cho xong”. Thiếu môi trường ghi Not run, không tick pass.
- [ ] Review toàn diff mình thêm so với baseline dirty; giữ các hunk UI/Auth/Contact/AI/Admin đã có. Không stage toàn working tree. Xác nhận CSRF/IDOR/Premium/5 GoalArea/caller ngoài phạm vi không đổi semantics.
- [ ] Append bug-fix-log đúng format cho bug thực sự đã sửa, cập nhật known issue nếu trùng. Cập nhật ledger cũ bằng mục sửa sau review; giữ lịch sử, sửa dòng tổng “hoàn tất L0→L7” thành trạng thái có bằng chứng. Báo cáo review gốc giữ nguyên làm mốc.
- [ ] Cập nhật `docs/operations/local-speech.md`, `speaking-import.md`, `local-writing-engines.md`, `writing-local-evaluation.md`: option, trạng thái triển khai thật, giới hạn process/DB, rollback về unavailable/Local chứ không về điểm giả.
- [ ] Báo cáo cuối tiếng Việt: các lỗi đã sửa, file, test pass/fail/hang kể cả retry, migration chưa apply, engine chưa cài, ASR-import/corpus/benchmark còn Deferred/Blocked. Không gộp “có adapter” với “đã vận hành”.

**Tiêu chí kết thúc:** toàn bộ phần sửa code/test trong quyền được làm và tự review xong; các cổng engine/MySQL/browser có trạng thái trung thực. Nếu full suite hoặc cổng môi trường còn bị chặn, bàn giao phần hoàn thành kèm blocker, **không** kết luận toàn bộ hệ thống sẵn sàng production. Không commit/push/merge cho tới khi chủ nhóm yêu cầu riêng.
