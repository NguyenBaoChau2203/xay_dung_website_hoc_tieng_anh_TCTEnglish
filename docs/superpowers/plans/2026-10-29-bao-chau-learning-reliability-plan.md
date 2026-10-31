# TCT English — Plan nâng cấp chức năng học và độ ổn định

> Thực thi inline bằng **một agent** trong Cursor, model do chủ nhóm chọn **Grok 4.7 High**. Dùng `tct-superpowers` theo từng lô; không dùng worker/reviewer/model khác. Tên model ghi theo yêu cầu, không khẳng định có sẵn trong mọi tài khoản Cursor.

**Khảo sát:** 29/10/2026; **hoàn thiện:** 30/10/2026 (UTC+7). **Trạng thái:** PROPOSED — chưa triển khai code, chưa cài các engine bên dưới.

**Mục tiêu:** Speaking/Writing có đường học cơ bản đáng tin, ít lệ thuộc dịch vụ/key bên ngoài; Goals/Notifications đúng dữ liệu, không cộng thưởng/thông báo giả hoặc mất kết quả khi lỗi.

**Kiến trúc:** giữ MVC/EF/MySQL hiện tại; thay từng thành phần qua adapter có giới hạn thời gian và kiểm thử. Tách nội dung học đã lưu khỏi việc sinh nội dung; tách kết quả khớp lời nói khỏi đánh giá phát âm; giữ kết quả học/credit bền vững trong DB.

**Stack được kiểm tra:** net9.0, EF Core 9.0.4, Pomelo MySQL 9.0.0, Razor/Bootstrap/JS. Không nâng lên .NET 10 hay đổi DB vì tài liệu cũ ghi khác.

**Spec/bằng chứng:** [báo cáo khảo sát](../../feature-audits/2026-10-29-bao-chau-learning-reliability-audit.md).

**Prompt:** [Cursor Grok](../../../.ai/prompts/cursor-grok-bao-chau-learning-upgrade.md).

## 1. Ranh giới, quyền thực thi và công việc đang mở

- Source baseline HEAD `3d27a68c8468207eca5a11653c72e735117fc637`; lúc khảo sát ở `feature/bao-chau-ui-audit` có 19 file UI/CSS sửa chưa commit. Không reset/stash/checkout/format lại để làm sạch. Không coi các diff có sẵn là phần do Cursor làm.
- L0 chỉ đọc và ghi báo cáo preflight; từ L1 chỉ thực thi khi chủ nhóm yêu cầu rõ lô tương ứng. Việc đọc plan/prompt không tự cấp quyền sửa ứng dụng.
- Phạm vi: Speaking hai luồng, Writing người học, Goals, Notifications. Không sửa Auth, Contact, AI Chat, Admin, Billing, Reading, Listening, Vocabulary/Set/Folder/Class. Giữ quyền Premium/Admin/Standard của bài cá nhân.
- Đọc caller ngoài phạm vi để review; không thay hợp đồng chung của họ. Đặc biệt `IAiProviderClient`/Gemini được nhiều feature dùng; `IYoutubeTranscriptService` còn phục vụ Listening và Admin import.
- Chỉ chỉnh UI cho trạng thái nghiệp vụ mới cần thiết: unavailable, needs-review, processing, retry, nguồn kết quả. Không redesign CSS/layout; đọc DESIGN.md và giữ giao diện đang được Antigravity làm.
- Không sửa skills/rules/hooks/MCP, không cài repo vào thư mục skills cho việc nâng cấp chức năng. Giữ chính sách một agent.
- Không commit/push/PR/merge/deploy trừ khi chủ nhóm yêu cầu riêng. Không index/sync CodeGraph tự động.
- Không đọc/in secrets, thay key, gọi API trả phí, gửi bài/audio người thật ra ngoài, tải model lớn, tạo service/container lâu dài hoặc chạy migration trên DB thật nếu chưa có ủy quyền tương ứng.
- Không `dotnet run` thông thường trên config đang trỏ DB thật: startup seeder ghi DB. Dùng test host/DB thử nghiệm đã xác định. Không đổi startup seeding trong task này.

### Ma trận quyền cần có khi chủ nhóm duyệt lô

| Lô | Quyền cụ thể cần ghi nhận | Không suy ra từ việc duyệt |
| --- | --- | --- |
| L0 | Đọc source, Git, chạy build/test cô lập; ghi preflight | Không sửa source |
| L1 | JS/view người học và regression tests liệt kê ở L1 | Không Program/config/package/schema |
| L2 | Writing service/API/VM + **đăng ký options Writing riêng trong Program.cs**, tài liệu env | Không đổi AI options/provider toàn cục; không ghi key/appsettings thật |
| L3 | Notification/Goals/Streak/worker + **DI clock trong Program.cs**, entity/DbContext và **migration additive tạo dedup/checkpoint** | Không apply migration DB thật, không sửa Admin broadcast semantics |
| L4 | Speaking API/service/JS/VM, assessment entity + migration additive, **DI/options trong Program.cs**; thử engine theo artifact/pin đã trình | Không mở cổng public hoặc tự tải/triển khai model lớn; không chấp nhận điểm client |
| L5 | Speaking import riêng, job model/worker + additive migration, DI/options Program.cs | Không đổi pipeline Listening/Admin, không tải nội dung ngoài quyền cung cấp |
| L6 | Completion/Goals trong Speaking+Writing, credit ledger/outbox và additive migration, **DI dispatcher trong Program.cs** | Không sửa điểm/XP lịch sử tự động, không đổi công thức thưởng của đồng đội |
| L7 | Adapter grammar/LLM Writing riêng và vận hành thử với artifact/model được duyệt | Không cài cả danh sách repo, không đổi toàn bộ IAiProviderClient/AI Chat |

AGENTS bảo vệ `Program.cs`, `appsettings.json`, `.csproj`. Các lô cần sửa Program phải được chủ nhóm đồng ý phần DI/options cụ thể ở bảng trên trước khi sửa; hoàn thành các phần độc lập để có diff/contract cụ thể trước khi xin phần quyền còn thiếu. Plan ưu tiên HTTP adapter + API có sẵn của .NET, **không dự kiến thêm NuGet hoặc sửa .csproj**. Nếu phát sinh package, trình tên/version/license/TFM và lý do trước. Không coi migration additive là quyền apply; phát hành DB luôn là bước riêng.

## 2. Quy tắc chung và lựa chọn đã chốt cho proposal

1. CodeGraph trước để định vị symbol/caller; không được phủ nhận source mới hơn vì index/docs cũ. Đọc đúng file có freshness warning hoặc phần không được trả.
2. Giữ legacy `/Home/*`, GET/POST, antiforgery và ownership checks. `GetCurrentUserId()` ở controller. Không bỏ test bảo mật để làm xanh bộ test.
3. Bài đã lưu không gọi cloud ở đường đọc/lưu bài cơ bản. Không đổi nhãn thành “offline” nếu vẫn cần YouTube hoặc máy chủ ngoài thiết bị.
4. Kết quả không đủ bằng chứng phải nói rõ chưa đánh giá; không tạo điểm ngẫu nhiên, feedback bịa, bản dịch “fallback” do ghép từ, hoặc fluency/phoneme score từ edit distance.
5. Snapshot userId/contentId/câu/revision khi bắt đầu request. Callback audio/HTTP về muộn không được lưu vào câu đang được chọn mới; không để request cũ ghi đè trạng thái mới.
6. Dùng cancellation end-to-end; timeout là ngân sách tổng thao tác, không chồng retry ở UI, adapter và provider. 401/403 không retry; 429 có Retry-After trong giới hạn tổng; không xoay key để né quota. Ghi latency/error code/correlationId, không log key/audio/raw bài học của người dùng mặc định.
7. Adapter service do nhóm vận hành chỉ nhận endpoint từ cấu hình quản trị cố định; client không được gửi arbitrary URL/model/path. Không public inference/model-load/admin endpoint.
8. Lưu pin tag+SHA/digest+SHA-256 artifacts/model trước chạy; không `latest`, `main` hay pipe install script. Source/notice đặt trong mục provenance riêng của chức năng; không trộn `.ai/vendor` dành cho agent tooling.
9. Migration phải additive, kiểm dữ liệu trùng trước unique index, không tự xóa row. Review `Down()` có thể DROP phần mới tạo; không chạy downgrade có mất dữ liệu nếu chưa được duyệt.
10. Mọi lô có ledger công việc, test đúng failure mode, diff review inline và báo cáo tiếng Việt. Dừng đúng ranh giới lô; không kéo thêm “tiện tay”.

### Engine

- **ASR chính để thử:** whisper.cpp v1.9.5 / commit `d1be6fde11ac6e0407606b4e42fe72d34add8037`, HTTP nội bộ. Không native-load vào web process. Bản HTTP ví dụ có `/load`; chỉ adapter nội bộ được chạm `/inference`, chặn endpoint quản trị qua mạng/quyền process.
- Input adapter chuẩn: WAV mono PCM16, 16 kHz; nhận dạng English. Không gửi reference answer làm ASR prompt. `response_format=verbose_json` được source server cùng tag hỗ trợ; kiểm fixture response thật trước viết mapping segment. Không giả định một OpenAI client bất kỳ tương thích mọi field.
- **Grammar tùy chọn:** LanguageTool v6.8 / `e807fcde6a6506191e1470744d2345da28c26be6`, Java 17, POST `/v2/check`, `language=en-US`; không trỏ free public API. Tắt engine thì core vẫn chạy.
- Ear2Finger commit `675750c02c645e27a4fd00fd0dae4d2dfe4471ba` dùng tham khảo luồng dictation/history và dữ liệu test. Không port full React/FastAPI/SQLite. Nếu copy hàm cụ thể phải ghi file gốc/hash/notice và test đối chiếu; plan không yêu cầu copy nguyên code.
- whisper.net/faster-whisper là lựa chọn thay ASR, Harper là lựa chọn thay grammar; không cài đồng thời các engine để “dự phòng” khi chưa có benchmark.
- LLM cục bộ chỉ L7: Ollama release đã khảo sát v0.40.13, model candidate Qwen3-4B. Chưa chốt artifact/model digest, quantization, máy chủ, chất lượng. Không coi đây là dependency bắt buộc của core.

## 3. L0 — Preflight và hợp đồng hiện tại (chỉ đọc)

**Đầu ra mới:** `docs/feature-audits/bao-chau-learning-execution-ledger.md`.

- [ ] Đọc AGENTS.md, `.ai/README.md`, report này, known-issues/bug-fix-log, conventions, và skill `tct-superpowers`, `tct-engineering`, `tct-speaking-feature`; dùng `tct-api-endpoint`/`tct-ef-migration` ở lô tương ứng. Không load mọi skill.
- [ ] Ghi HEAD/branch/status, danh sách diff có sẵn. Nếu Antigravity còn sửa cùng file, giữ L0 và báo vùng xung đột cụ thể; không ghi đè các hunk đó.
- [ ] CodeGraph truy vấn controller/service/JS/VM/caller/test hiện tại. Xác nhận `UseMySql`, TFM/package, cả năm GoalArea; không nâng framework theo SDK máy.
- [ ] Chụp hợp đồng response `WritingSentenceEvaluationViewModel`, progress Speaking, `NotificationListViewModel`, Premium/ownership, selector JS trong view. Ghi rõ thay đổi hợp đồng dự kiến ở L2/L4/L5, không tự áp lên endpoint khác.
- [ ] Xác định test host chỉ dùng DB cô lập/provider stub; ghi máy chủ triển khai CPU/RAM/OS/GPU/concurrency nếu có dữ liệu. Thiếu hạ tầng không cản L1/L2; chặn khẳng định ASR production-ready.
- [ ] Chạy build và test baseline mục 11. Lưu log dưới `.tmp/learning-reliability-upgrade/`. Ghi lỗi có sẵn riêng; không sửa ngoài phạm vi để làm xanh.
- [ ] Ghi owner approval cho lô được yêu cầu và các quyền từ ma trận. Chưa có yêu cầu triển khai thì dừng sau L0, đề xuất bắt đầu L1.

**Done:** ledger đủ baseline, phạm vi, contract, điều kiện môi trường và test; source vẫn nguyên.

## 4. L1 — Loại kết quả bịa và phản hồi đọc thông báo sai

**Modify:** `TCTEnglish/wwwroot/js/speaking.js`; phần trạng thái/nhãn tương ứng trong `Views/Speaking/Practice.cshtml` nếu cần; `wwwroot/js/notifications.js`; inline script trong `Views/Notification/Index.cshtml`.

**Create tests:** `scripts/tests/test_speaking_behavior.cjs`, `scripts/tests/test_notification_behavior.cjs`. Dùng Node test runner và fixture DOM/recognition/fetch tối thiểu để chạy **code client thật**; không chỉ regex kiểm tên hàm. Không thêm npm framework chỉ để test lô này.

- [ ] Viết test đỏ: thiếu SpeechRecognition, từ chối mic, network/no-speech → không có POST progress sau mọi timer, không đánh dấu hoàn thành; lỗi fetch mark-read/mark-all giữ trạng thái chưa đọc và nút có thể thử lại.
- [ ] Viết test đỏ cho callback về sau khi đổi câu: kết quả chỉ thuộc câu snapshot hoặc bị hủy, không gắn cho câu mới. Bắt double-submit, cleanup timer khi rời trang.
- [ ] Xóa nhánh `simulateFeedback` lưu điểm và mọi `Math.random` trong kết quả học. Không thay bằng số đẹp cố định.
- [ ] Trong khoảng L1 đến L4, kết quả browser recognition là **preview-only**: hiển thị mức khớp văn bản nhưng không POST điểm/progress/XP. Vẫn nghe/thu/luyện lại được, tiến độ cũ vẫn đọc được. Đây là đánh đổi tạm thời cần chủ nhóm biết khi duyệt L1; không giả vờ chức năng lưu Speaking đã nâng cấp xong. Không khẳng định fluency/prosody; chỉ số không đo được hiển thị “Chưa đánh giá”, không hiện 0% như kết luận năng lực. L4 khôi phục lưu từ assessment phía server. API legacy còn tồn tại là residual risk S02, không được coi là đã khóa chỉ vì UI ngừng gọi.
- [ ] Chỉ bỏ unread/cập nhật badge khi HTTP + payload thành công; hoặc optimistic update có rollback đầy đủ. Phục hồi nút ở cả lỗi HTTP và lỗi mạng. Nếu vẫn điều hướng relatedUrl, không nói đã đọc khi request lỗi.
- [ ] Chạy hai Node tests, build, và browser scenario trên môi trường test nếu có. Không xóa điểm cũ trong DB.

**Done:** không có demo/rand score được tạo/lưu mới; thông báo lỗi không thành công giả. **S02 còn mở**: client vẫn có thể giả điểm ở API cũ cho đến L4; ghi rõ trong ledger, không gọi đây là hoàn tất chống gian lận.

## 5. L2 — Writing hoạt động cơ bản không chờ cloud

**Modify:** `Services/WritingService.cs`, `IWritingService.cs`, `WritingAiEvaluationService.cs`, `IWritingAiEvaluationService.cs`; **chỉ action Writing** trong `Controllers/StudyController.cs`; `ViewModels/WritingIndexViewModel.cs`; `wwwroot/js/writing.js`; trạng thái nhỏ của `Views/Study/WritingPractice.cshtml`; `Program.cs` chỉ bind/register thành phần Writing riêng khi đã được phép.

**Create:** `Services/Writing/WritingEvaluationOptions.cs`, `Services/Writing/WritingAssessmentPolicy.cs`; `TCTEnglish.Tests/WritingLocalAssessmentTests.cs`, `WritingAvailabilityIntegrationTests.cs`. New code namespace `TCTEnglish.*`; tôn trọng interface cũ đang ở namespace legacy, không rename diện rộng.

**Policy mới:** `WritingAssessmentStatus = Accepted | NeedsRevision | NeedsReview`. `WritingAssessmentDecision` chứa Status, Source, Feedback; policy nhận reference/answer, không I/O. VM JSON thêm `assessmentStatus`, `assessmentSource`, `reviewRequired`; giữ các field `passed`, `usedAi`, `evaluationSource`, feedback/attempt/progress để client cũ đọc được. `passed=true` và auto-advance chỉ khi Accepted.

- [ ] Viết test đỏ cho spy provider bị cấm gọi: mở bài, hint, nộp exact answer và lưu attempt khi không có key đều không gọi `IAiProviderClient`, trả JSON hợp lệ, reload còn kết quả.
- [ ] Chốt options `Learning:Writing:EvaluationMode=Local` mặc định, `CloudEnhancementEnabled=false`. Không đổi `AI:*` hoặc binding global của `IAiProviderClient`. Ghi biến env vào docs, không nhét key vào appsettings.
- [ ] Exact match sau chuẩn hóa case/whitespace/punctuation hợp lý được Accepted. Không xóa từ phủ định, số, chủ ngữ, động từ để “chuẩn hóa”. Nếu chưa có tập đáp án biến thể được duyệt, không tự sinh biến thể rồi xem như truth.
- [ ] Câu rỗng/ký hiệu hoặc sai phạm vi câu vẫn NeedsRevision theo kiểm tra hiện tại; câu không khớp nhưng có thể hợp lệ thành NeedsReview, không auto-pass bằng coverage 0.4. UI nói rõ giới hạn, cho xem hint/so sánh để sửa, không khẳng định bản dịch sai. Đây là đánh đổi sản phẩm cần chủ nhóm duyệt khi chọn L2.
- [ ] Test dữ liệu: reference “I like tea.” → “i like tea” Accepted; “I do not like tea.” không Accepted; “Tea likes me.” không Accepted; “I enjoy tea.” NeedsReview nếu chưa có đáp án được duyệt. Giữ test khác-câu/nhiều-câu, leak reference và ownership.
- [ ] Luồng AI tùy chọn có linked cancellation **tổng 3 giây** cho chấm câu, không retry lồng nhiều tầng; timeout/missing key/401/403/429/network/malformed JSON quay về quyết định Local, không giữ nút loading. Propagate request abort; không biến hủy chủ động thành câu sai.
- [ ] AI tùy chọn không tự làm thay đổi pass/XP trong chế độ Local; trước mắt là feedback bổ sung có nhãn. Test provider failure chứ không chỉ test fake client trả sẵn success. Test cấu hình opt-in riêng nếu giữ test AI cũ.
- [ ] Lưu attempt/progress theo quyết định authoritative đúng một request; không “cập nhật ngầm” pass sau khi trả response. L6 xử lý atomic reward, lô này chưa tuyên bố giải quyết crash window.

**Done:** core chấm/lưu không cần key, không chờ provider; người học thấy nguồn và needs-review trung thực. Không hứa LanguageTool/heuristic chấm đúng mọi bản dịch. Sinh bài Premium bằng AI vẫn là tiện ích tùy chọn, được xử lý riêng ở L7.

## 6. L3 — Goals và Notifications đúng ngày, không bỏ sót/trùng thông báo

**Modify:** `Services/NotificationService.cs`, `INotificationService.cs`, `GoalsService.cs` (nguồn ngày nghiệp vụ, giữ công thức), `StreakService.cs`, `StreakUpdateResult.cs`, `Controllers/NotificationController.cs`, `Workers/NotificationWorker.cs`, `Models/Notification.cs`, `Models/DbflashcardContext.cs`, ViewModel notification; Program.cs đăng ký clock sau approval.

**Create:** `Services/Notifications/LearningBusinessClock.cs`; `Models/NotificationJobRun.cs`; migration additive `LearningNotificationReliability`; `TCTEnglish.Tests/NotificationReliabilityIntegrationTests.cs`, `NotificationWorkerRecoveryTests.cs`. Test provider MySQL cô lập nếu được cung cấp; SQLite không thay kiểm chứng unique/race trên MySQL.

**Contract:** clock nhận `TimeProvider`, cung cấp BusinessDate và UtcStart/UtcEnd cho ngày Việt Nam; Goals/notification/streak/worker trong lô dùng cùng clock để test điều khiển được thời gian. Giữ UTC+7 và không thay nghĩa global BusinessDateHelper của module khác. Query paged mới nhận userId/page/pageSize/unreadOnly và trả items + total/unread total; wrapper API cũ giữ JSON envelope.

- [ ] Test đỏ: 23:59/00:01/06:59/07:01 giờ Việt Nam, UTC lưu Kind.Unspecified, ngày đổi năm; dữ liệu Goals và Notifications cùng business date. Dùng clock fake/injectable, không sleep chờ ngày.
- [ ] Truyền PreviousLongestStreak trong metadata trước save; ghi thông báo record theo snapshot. Test vượt kỷ lục đúng một lần, tăng nhưng chưa vượt thì không tạo; giữ API callers/constructors hiện có tương thích.
- [ ] Event identity đề xuất: `goal:{user}:{area}:{businessDate}:{80|100}`, `badge:{user}:{badgeId}`, `streak-record:{user}:{businessDate}:{newRecord}`, `streak-warning:{user}:{businessDate}`, `badge-near:{user}:{badgeId}:{businessDate}`. Duyệt semantics trước migration; thông báo record cùng ngày tối đa một lần với cùng record.
- [ ] Thêm `EventKey` nullable có giới hạn độ dài và unique index (hoặc user+key); null dành cho lịch sử/broadcast cũ. Không dedup broadcast Admin bằng một key rỗng. Kiểm duplicate-key recovery không để entity lỗi tiếp tục nằm tracked.
- [ ] Test hai goal Speaking/Writing hoàn thành cùng ngày đều có thông báo; replay cùng event và hai request cạnh tranh chỉ một row; hai badge cùng tên nhưng khác ID không bị gộp. Không dùng message text làm khóa.
- [ ] DB-side filter/count/paging, order CreatedAt DESC + Id DESC. Test 501+ rows, unread nằm ngoài 500 mới nhất, đúng total/unread count toàn bộ user, isolation user, page 0/âm/quá lớn không overflow. Giữ pageSize API 1–50 và trả NotFound cho đọc-sửa chéo user.
- [ ] Worker kiểm tra due job mỗi phút thay vì ngủ hết tới ngày mai; checkpoint DB unique theo job/date. Mỗi vòng xử lý tối đa 200 user, bỏ qua user blocked, retry hữu hạn. Khởi động sau 20:00 Việt Nam bắt kịp **ngày hiện tại** nếu chưa chạy; không phát hàng loạt cảnh báo của ngày cũ.
- [ ] Claim job có lease 5 phút, heartbeat ít nhất mỗi phút khi chạy dài; instance khác chỉ tiếp quản lease hết hạn. Dedup event DB giữ tác động idempotent dù một lease bị mất. Test restart/crash nửa batch/two workers, cancel dừng sạch, không skip cleanup nếu job cảnh báo lỗi.
- [ ] Poll chuông khi trang visible; chỉ một request in-flight/tab, refresh lúc hiện lại, backoff 60s→120s→240s tối đa 5 phút khi lỗi; không đổi SignalR ClassChatHub hay thêm SaaS notification.
- [ ] Review additive migration/index với data cũ và MySQL. Sinh script review, không apply DB thật. Không thêm migration drop dữ liệu trùng để né lỗi.

**Done:** case ngày/kỷ lục/multiple goals/paging/restart có regression test; UI/API giữ hợp đồng. Giữ tất cả năm GoalArea đang hoạt động. Không đổi XP hay dashboard của đồng đội trong lô này.

## 7. L4 — Speaking thu thật và nguồn điểm phía server

**Modify:** `Controllers/SpeakingController.cs`, `Services/SpeakingService.cs`, `ISpeakingService.cs`, speaking VM/JS/view; riêng phần mic của `Views/Study/Speaking.cshtml` dùng khả năng thu/nghe đối chiếu chung nếu giữ được quyền bộ thẻ; Program.cs đăng ký/options và DbContext/migration mới sau approval.

**Create:** `Services/Speaking/ISpeechTranscriber.cs`, `WhisperHttpTranscriber.cs`, `SpeakingAssessmentService.cs`, `SpeechProcessingOptions.cs`; `Models/SpeakingAssessment.cs`; migration additive `SpeakingServerAssessment`; `wwwroot/js/speech-recorder.js`; `TCTEnglish.Tests/SpeakingAssessmentIntegrationTests.cs`; `scripts/tests/test_speech_recorder.cjs`; `docs/operations/local-speech.md`.

**Năng lực:** `ISpeechTranscriber.TranscribeAsync(Stream wav, CancellationToken ct)` trả transcript + segment offsets + engine/model identity + trạng thái speech/no-speech, không trả “điểm phát âm”. Assessment service nhận sentenceId/userId/audio, load reference ở server, so sánh từ có thứ tự, ghi assessment gắn chủ sở hữu.

- [ ] Trước tích hợp, chạy spike engine trên **audio fixture có quyền sử dụng** và máy thử được duyệt; ghi OS/CPU/RAM/model hash, cold/warm latency và RSS. Model thiếu/offline phải báo not-ready, không tự download lúc người học bấm mic. Chưa có model/máy → vẫn làm unit/HTTP fixture tests, ghi local ASR chưa xác minh, không đóng lô production-ready.
- [ ] Thu bằng getUserMedia + AudioContext/AudioWorklet tạo WAV PCM16 mono 16kHz hoặc converter được kiểm thử; không giả định Blob WebM của MediaRecorder là WAV. Chọn giải pháp PCM cho lô đầu để không cần shell ffmpeg cho upload mic.
- [ ] Giới hạn mỗi audio 15 giây, payload tối đa 2 MiB; decode kiểm duration/channels/sample rate/magic bytes, không tin Content-Type. Bỏ mic track/timer/buffer khi dừng, lỗi hoặc rời trang; có nghe lại/xóa bản thu. Audio không lưu lâu mặc định, cleanup cả khi request bị hủy.
- [ ] Endpoint mới POST `/api/speaking/{sentenceId}/assess` (multipart), Authorize + CSRF + quyền video; reference lấy DB. Adapter HTTP `/inference` dùng English, verbose_json, timeout tổng 15s, concurrency khởi điểm 1, queue tối đa 4, đầy trả 429/Retry-After. Không dùng điểm/expectedText/model URL do client gửi.
- [ ] Test silence/noise/no speech trả “Chưa nhận được lời nói” không auto-pass; missing engine/network/timeout trả khả dụng kém có retry, không tạo score/completion. Engine không nhận full reference prompt để tránh làm đẹp kết quả sai.
- [ ] New assessment lưu ID khó đoán, userId/sentenceId/createdAt/engine-model version/text-match và trạng thái, không raw audio mặc định. Client commit qua route progress cũ phải có assessmentId của chính user/câu. Numeric scores client nếu còn gửi bị bỏ qua; thiếu/giả/khác user/câu/hết hạn 10 phút → không ghi progress, không XP. Khóa các metadata identity ở server.
- [ ] Giữ route `/api/speaking/{sentenceId}/progress` nhưng **đổi có chủ đích hợp đồng đầu vào**: score-only cũ trả 409 với code `assessment_required`. Cập nhật tất cả caller/test Speaking cùng lô; success response giữ các field progress/XP đang được client dùng. Không giữ đường legacy cho phép tự cấp điểm để “tương thích”. Không mở route legacy bypass bằng flag production.
- [ ] Chỉ ghi mức khớp lời nói có căn cứ, không fluency/prosody bịa; trường cũ chưa đo phải có metadata unavailable và UI thích ứng, không dùng để quyết định pass. Ngưỡng TotalScore >=70 và ceil(70% số câu) giữ làm **điều kiện hoàn thành bài luyện**, chưa phải chuẩn phát âm; chủ nhóm có thể duyệt chuẩn mới riêng.
- [ ] Test audio/result binding, replay receipt, đổi câu lúc chờ, request đồng thời, best score không tụt; legacy score forgery không tạo row/XP; chỉ chủ sở hữu được truy cập audio/assessment. Nhánh nói theo bộ thẻ không tự thêm tín hiệu Goals mới hoặc sửa tiến độ Vocabulary của đồng đội.
- [ ] Chạy browser mic thực trên test environment, Chrome/Edge và browser không có Web Speech; permission denied/no mic, cancel, HTTPS/localhost, ASR chết, two tabs. Không dùng screenshot tĩnh để chứng minh audio hoạt động.

**Done:** bỏ browser recognition làm nguồn điểm thưởng; người học dùng nghe/thu/đối chiếu khi engine unavailable. Nếu backend ASR chưa kiểm chứng thực tế, báo còn blocked ở vận hành; không fallback sang điểm client. L6 còn cần cho crash-safe reward.

## 8. L5 — Import Speaking không gọi Gemini ngầm, timecode đáng tin

**Modify:** `SpeakingService.CreateOwnedVideoAsync`, `ISpeakingService`, Speaking create API/UI. **Không thay mặc định** `YoutubeTranscriptService` dùng chung hoặc Listening/Admin importer.

**Create:** `Services/Speaking/ISpeakingTranscriptProvider.cs`, `SpeakingTranscriptProvider.cs`, `SpeakingImportJobService.cs`, `Workers/SpeakingImportWorker.cs`; `Models/SpeakingImportJob.cs`; additive migration `SpeakingDurableImports`; `TCTEnglish.Tests/SpeakingImportReliabilityTests.cs`; `docs/operations/speaking-import.md`. Nếu tách caption parser từ service cũ, giữ wrapper tương thích và test parity cho caller cũ.

- [ ] Test đỏ caption có sẵn → zero LLM/ASR calls; caption thiếu/Gemini key thiếu không làm request treo; transcript đã lưu không tải/inference lại khi mở bài.
- [ ] Pipeline chỉ Speaking: caption English có timestamp → subtitle/timecode được cung cấp hợp lệ → ASR nội bộ khi được phép. Không fallback bằng chia duration theo số từ. Mọi segment phải 0 <= start < end <= media duration, hữu hạn và có thứ tự; reject dữ liệu rỗng/invalid chứ không bịa timecode.
- [ ] Chọn đường import SRT/WebVTT trong flow người học hiện có; test cue overlap, multi-line/Unicode, invalid timestamps, giới hạn 1 MiB/2.000 cue/30 phút. Không sửa Admin upload form và không mang frontend của Ear2Finger vào.
- [ ] Import online chậm trả **202** jobId/statusUrl thay vì giữ HTTP request 5 phút. Job DB gắn owner/canonical youtubeId/request fingerprint, trạng thái queued/running/succeeded/failed/cancelled, attempts/max3, retry thời gian tăng có jitter. UI xử lý 200 cũ và 202 mới; status/delete đều kiểm owner/Premium hiện hành.
- [ ] Persist idempotency theo user+source/fingerprint; restart không mất job, concurrent cùng input không tạo video trùng. Quyền bị hạ trong khi chờ phải được check lại trước persist/publication. Không expose job user khác.
- [ ] Dùng worker lease/bounded concurrency; timeout mỗi job tối đa 5 phút, video <=30 phút, audio/temp hạn mức 100 MiB, cleanup khi fail/cancel. Không dùng MemoryStream+base64 full video trong request. Nếu quyền nội dung không cho tải audio, chỉ dùng captions/subtitle hợp lệ hoặc báo chưa hỗ trợ.
- [ ] Test fake network timeout/403/429/unavailable content, caption parse, timestamp boundary, crash/retry, idempotency MySQL, disk full/cleanup; malformed transcript không xuất bản video học lỗi.
- [ ] Regression caller Listening/Admin vẫn dùng hợp đồng cũ. Không tự cập nhật YoutubeExplode hoặc thêm yt-dlp/ffmpeg; nếu cần, trình artifact/version/license/phạm vi subprocess riêng và xin quyền tương ứng.

**Done:** không Gemini trong Speaking import mới; bài đã lưu dùng dữ liệu đã chuẩn bị. YouTube vẫn là phụ thuộc nội dung cho video iframe, không gọi toàn hệ thống “offline”.

## 9. L6 — Hoàn thành và thưởng không mất/nhân đôi khi lỗi

**Modify:** orchestration completion trong `SpeakingController`/service được trích ở L4, `WritingService` progress, `GoalsService` bounded API mới, IGoalsService, `StreakService` notification orchestration, DbContext; `NotificationWorker` drain outbox; Program.cs đăng ký dispatcher sau approval.

**Create:** `Models/LearningCompletionCredit.cs`, `Models/LearningNotificationOutbox.cs`; `Services/Learning/CompletionCreditService.cs`, `Services/Notifications/LearningOutboxDispatcher.cs`; additive migration `LearningCompletionCredits`; `TCTEnglish.Tests/GoalsCompletionRecoveryTests.cs`. Có thể thêm partial service có tên rõ thay làm controller lớn hơn; không tạo generic repository framework.

**Invariant:** từ trạng thái hoàn thành bài luyện sang daily counts/XP credit phải atomic cho Speaking/Writing. Notification failure không làm mất credit; replay không ghi thêm. Event key stable theo user + feature + contentId, giữ chính sách completion chỉ thưởng một lần cho nội dung hiện tại, không tự thêm revision làm cớ thưởng lại.

- [ ] Test đỏ fault-injection giữa persist progress/completion và credit: lỗi làm rollback toàn phần hoặc để một durable pending credit có thể phục hồi; không để “completed nhưng không còn đường ghi reward”.
- [ ] Chọn transaction cùng DbContext cho progress/completion/credit+daily counts+streak/badges và unique credit key. Network inference ở ngoài transaction. Không chỉ thêm flag in-memory hoặc transaction quanh riêng lệnh cuối.
- [ ] `CompletionCreditService` nhận user/feature/content và activity update **do server tạo**; reject increment âm/nguồn không hợp lệ. Unique insert quyết định chỉ một lần apply. Retry sau lỗi chỉ retry transaction an toàn, không chấm audio/AI lại.
- [ ] Tách lỗi tạo thông báo khỏi atomic credit: enqueue **bắt buộc** notification event bền vững trong cùng transaction của credit. Outbox chứa eventId/type/user/content hoặc badgeId/businessDate/payload có version/createdAt, attempts/nextAttemptAt/processedAt; không raw bài/audio. Trong credit path, thay direct notification calls của Goals/Streak bằng outbox, giữ đường gọi cũ của feature khác tương thích.
- [ ] `NotificationWorker` drain tối đa 100 outbox event mỗi vòng một phút qua dispatcher; retry tối đa 10 lần rồi giữ failed record để owner kiểm, không silent drop. Ghi notification + processedAt trong một DB transaction với unique EventKey ở L3. Dùng businessDate của event khi retry qua nửa đêm, không chuyển thành mục tiêu ngày mới. Không fire-and-forget task dùng DbContext đã dispose.
- [ ] Thêm unique index user+sentence vào UserSpeakingProgress nếu chưa có. Trước migration xuất thống kê duplicate và đề xuất merge giữ best score; **không tự delete/backfill** khi chưa được duyệt. Không cấp lại reward cho điểm lịch sử chưa rõ nguồn.
- [ ] Test hai request đồng thời từ hai DbContext: một credit, +20XP hoàn thành Speaking/Writing theo rule hiện tại, streak XP không lặp; crash-before-commit/retry-after-commit/cancel không double count. Test notification lỗi không gây trả thành công giả hoặc rollback bất ngờ credit.
- [ ] Giữ tất cả regression GoalsPhase1–7 và caller Vocabulary/Reading/Listening; không chuyển các feature kia sang credit path mới trong task này. Migration/script được review riêng trên MySQL thử nghiệm, không áp DB production.

**Done:** chứng minh transactional invariants bằng failure injection + concurrency thật, không chỉ chạy lại happy-path hai lần. Nếu DB integration chưa có, ghi rõ giới hạn và chưa đóng rủi ro G01 production.

## 10. L7 tùy chọn — Grammar và semantic feedback do nhóm vận hành

Lô này tách khỏi core. Chỉ thực hiện sau L2 và khi chủ nhóm duyệt tài nguyên/độ trễ/chất lượng cụ thể. Có thể chọn **grammar**, **local LLM**, hoặc không chọn; không tự bật cả hai.

**Create (nếu chọn):** `Services/Writing/ILocalGrammarChecker.cs`, `LanguageToolGrammarChecker.cs`; hoặc `Services/Writing/ILocalWritingModel.cs`, `OllamaWritingModel.cs`; `TCTEnglish.Tests/WritingOptionalEngineContractTests.cs`; `docs/operations/local-writing-engines.md`. Program.cs DI/options Writing riêng khi đã được phép.

- [ ] Grammar: LanguageTool 6.8, Java17, artifact/image digest và license/resources ghi manifest. Chỉ server TCT gọi `/v2/check` nội bộ, language cố định en-US, 2 giây tổng, text <=4.000 ký tự, có xử lý offset Unicode. Return grammar findings, không tự set passed cho câu trái nghĩa.
- [ ] Server grammar down/circuit open/invalid response → không mất draft/attempt, không chặn core. Không mở public port hoặc wildcard CORS theo ví dụ upstream.
- [ ] Local LLM: Ollama local `/api/chat`, model tải sẵn có digest/quantization/license được duyệt, không dùng cloud tag. HTTP client Writing riêng; không đổi Gemini BaseUrl rồi mong Gemini protocol nói chuyện được với Ollama.
- [ ] Chấm feedback local LLM tổng <=3 giây; tạo bài tùy chọn <=30 giây hoặc trả job được thiết kế riêng. Không kéo dài đường chấm core để đợi model cold start. Thiếu model/GPU/RAM thì tắt tiện ích có lý do.
- [ ] Dùng schema JSON kiểm được, chống reference leak theo guard hiện tại, input length/token budget, guard prompt injection của nội dung học. Không nhận model instruction/path từ người dùng.
- [ ] Corpus tối thiểu 40 câu song ngữ do giáo viên/owner kiểm: đúng khác cách diễn đạt, phủ định, số/đơn vị, thì, chủ ngữ/tân ngữ, nội dung ngoài phạm vi, output lỗi/leak. Báo false accept/false reject/latency theo từng loại; chưa đủ chất lượng thì không dùng model quyết định completion.
- [ ] Sinh bài song ngữ phải có bản dịch đúng và segmentation hợp lệ; LanguageTool và tách câu không thay được việc tạo bản dịch. Giữ quota/ownership/idempotency; nếu không có engine, học bài hiện có vẫn bình thường, nút sinh bài báo không khả dụng. Không bịa dữ liệu mẫu như bài của người dùng.

**Done:** adapter/quality report có bằng chứng, mặc định off cho tới khi được chọn bật. Tắt tiện ích không làm hỏng học cơ bản. Không tự quảng bá khả năng “phát âm chuẩn” hay “chấm nghĩa tuyệt đối”.

## 11. Ma trận kiểm thử và lệnh handoff

Baseline khảo sát: build incremental 0 lỗi/0 cảnh báo; test filter **111 passed/0 failed/0 skipped**. Số này là trước nâng cấp, không thay kết quả chạy của Cursor. Không bắt số lượng tương lai phải đúng 111 vì sẽ thêm regression.

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity minimal
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Speaking|FullyQualifiedName~Writing|FullyQualifiedName~Goals|FullyQualifiedName~Notification" --blame-hang-timeout 90s --logger "trx;LogFileName=learning-upgrade.trx" --results-directory .tmp/learning-reliability-upgrade/test-results --verbosity minimal
# Sau khi tạo client regression tests ở L1/L4, chạy đúng file tương ứng:
node --test scripts/tests/test_speaking_behavior.cjs scripts/tests/test_notification_behavior.cjs
node --test scripts/tests/test_speech_recorder.cjs
# Khi hoàn tất lô thay service dùng chung và trước giao nhánh:
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --blame-hang-timeout 90s --verbosity minimal
git diff --check
```

Không chạy lệnh Node file chưa được tạo; không đánh dấu pass bằng cách skip test đang fail. Nếu restore thiếu, phục hồi dependency đã pin đúng môi trường; không tự upgrade để vượt lỗi. Khi thay file test/source thì build lại trước `--no-build`.

| Kiểm tra | Tiêu chí nghiệm thu |
| --- | --- |
| No-key/no-egress | Bài Writing có sẵn, hint, nộp/lưu/reload, Goals và Notifications không gọi cloud; Speaking ASR nội bộ đã warm vẫn chạy khi chặn outbound. YouTube có thể không phát khi chặn mạng, phải ghi riêng. |
| Quyền | Guest/Standard/Premium/Admin/outsider, IDOR/CSRF, role đổi giữa job; không rò bài/audio/job riêng. |
| Nguồn đánh giá | Không demo/rand, không client-issued score; silence/noise không thành hoàn thành; nghĩa chưa chắc đúng không auto-pass. |
| Độ bền | Restart, timeout, retry, cancel, hai tab/hai instance, DB fault giữa completion/credit; không duplicate hoặc mất credit. |
| Ngày và thông báo | UTC+7 boundary, năm mới, hai goal cùng ngày, badge đổi tên, >500 rows, unread/filter/count/paging đúng. |
| Tương thích | Routes/JSON/selectors, các view UI đang sửa; AI Chat/Listening/Reading/Vocab/Admin/Billing không bị đổi semantics. |
| Tài nguyên | 15s/2MiB mic; engine queue đầy; 30min/100MiB import; temp cleanup; không model-download khi request; đo RSS/latency thực tế. |

Mục tiêu đo nghiệm thu trên máy thử được ghi rõ: Writing Local warm p95 <=1 giây/20 lượt tuần tự; notification page với 1.000 record p95 <=500ms; ASR clip 10 giây warm p95 <=10 giây với concurrency1. Đây là **mục tiêu đề xuất**, không phải benchmark đã đạt. Nếu không đạt, báo số thật và chọn engine/hạ tầng/giới hạn khác, không che bằng timeout dài. Cần test tải theo số người dùng dự kiến trước khi mở cho cả nhóm.

Browser check phải ở môi trường test có dữ liệu/phiên hợp lệ, chạy các hành vi chức năng và kiểm network/console. Không giả login của người thật hay khai key để “thử cho xong”. Nếu browser/DB/engine chưa có, ghi cụ thể phần chưa kiểm và hoàn tất phần độc lập, không tuyên bố end-to-end pass.

## 12. Phát hành, rollback và bàn giao từng lô

- [ ] Đọc full diff của **phần mình sửa**, so với baseline dirty ban đầu; không stage tất cả. Ledger ghi file/test/đầu việc xong, residual risk, engine chưa chứng minh.
- [ ] Nếu thực sự sửa bug, thêm entry đúng format trong `.ai/context/bug-fix-log.md`; chỉ đóng known issue đã xác minh, không ghi mọi đề xuất là đã sửa.
- [ ] Với schema: backup, kiểm dữ liệu trùng, review script MySQL additive, chạy DB thử, đối chiếu migration lịch sử/snapshot và chuẩn bị rollback. Apply production chờ yêu cầu riêng. Không regenerate toàn bộ lịch sử migration.
- [ ] Rollback engine bằng options về unavailable/manual practice và Writing Local; không quay về demo hoặc client-trusted reward. DB additive giữ lại trước, không chạy Down xóa dữ liệu đánh giá/credit đã phát sinh.
- [ ] Dùng feature flag theo từng engine/lô, fail closed nguồn điểm; thông báo status triển khai thực tế cho chủ nhóm. Không bảo mọi máy pull repo là có sẵn model/Java/binary/service.
- [ ] Báo cáo cuối lô bằng tiếng Việt: kết quả và hành vi đổi, file đổi, lệnh và số pass/fail thật, kiểm browser/engine/DB còn thiếu, nguy cơ còn mở, lô tiếp theo cần duyệt. Dừng sau lô được yêu cầu.

Gợi ý bắt đầu: duyệt **L0 + L1** trước. L2/L3 là bước giá trị cao tiếp theo; L4/L5 cần chốt tài nguyên máy chủ. Không gộp dự án chức năng này vào đợt polish giao diện đang diễn ra bằng Antigravity.
