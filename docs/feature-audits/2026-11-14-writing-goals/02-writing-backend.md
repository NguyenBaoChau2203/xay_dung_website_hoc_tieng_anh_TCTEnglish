# Stage 02 — Writing backend audit và sửa lỗi local

Ngày: 14/11/2026. Nhánh: `fix/writing-goals-profile-polish`.
HEAD lúc bắt đầu: `c6367a00269bc23749a9a02a7d26bcc89001fac8`.
Phạm vi: IMPLEMENT_LOCAL, một agent; tiếp nối hai tài liệu bàn giao stage 01.
Không commit/push/PR/deploy, không gọi API trả phí, không chạy stage 03.

## 1. Công việc có sẵn và nguồn đã kiểm tra

- Đã đọc prompt 02, work-pack README trong `docs/superpowers/plans/2026-11-14-writing-goals`,
  kế hoạch 02, `01-ui.md`, `01-ui-data-contract.md` và bug memory liên quan.
  `.ai/prompts/writing-goals-2026-11-14/README.md` được prompt nhắc tới nhưng không tồn tại;
  README của work pack nằm trong thư mục kế hoạch nêu trên.
- CodeGraph dùng trước khi đọc/đổi source; những phần bị lược bỏ và test fixture được
  đọc trực tiếp để kiểm tra persistence, provider và hợp đồng HTTP thực tế.
- Năm tệp Gemini đang sửa lúc bắt đầu được kiểm tra SHA256 cuối lượt và giữ nguyên:
  `Views/Account/Profile.cshtml`, `Views/Goals/Index.cshtml`,
  `Views/Study/Writing.cshtml`, `Views/Study/WritingExercises.cshtml`, `wwwroot/js/goals.js`.
  Các đường dẫn ứng dụng trong báo cáo tính từ `TCTEnglish/`.
- Không thay đổi layout, CSS, shared partial, Program.cs, appsettings, project files,
  model/DbContext/migration hay nội dung bài học thật.

## 2. Phát hiện đã tái hiện và sửa

| Vấn đề | Bằng chứng trước sửa | Sửa và kết quả |
| --- | --- | --- |
| Câu dài có thể được nhận Accepted rồi vượt cột AcceptedAnswer(1000) | POST `Hello` + 1000 dấu `!` trả 200, test yêu cầu 400 thất bại | Evaluate từ chối raw input >1000 ký tự và ModelState lỗi trước rate-limit/service; không ghi attempt/progress |
| Unicode whitespace không được collapse giống phía browser | Hai test NBSP/EM SPACE/NARROW NBSP thất bại | Dùng tập Unicode whitespace của .NET; không lưu khoảng trắng thừa vào answer snapshot |
| Dấu Unicode tách có thể làm mất phân biệt từ hoặc từ chối cách mã hóa tương đương | `café`/`cafe\u0301` bị từ chối; `cafe`/`cafe\u0301` lại được nhận | NFC trước so sánh, giữ combining marks còn lại; không xóa dấu để tạo false pass |
| Admin có quyền qua Data/Hint/Evaluate nhưng không mở được trang bài unpublished | Access matrix: Practice trả404 thay vì200 | Trang Practice dùng cùng `ResolveWritingExerciseAccessAsync`, vẫn giữ bộ lọc level/contentType |
| Ctrl+Enter bỏ qua trạng thái nút disabled | Node ghi nhận 2 request khi một lần nộp đang chờ | Guard `isSubmitting` ở đầu hàm submit; đường thưởng vẫn chỉ ở server |
| Phản hồi Accepted cũ xóa bản nháp mới trong cùng tab hoặc tab khác | Hai Node test nhận `localStorage=null` thay vì bản nháp mới | Chỉ xóa draft nếu nội dung lưu vẫn khớp nội dung vừa nộp; không auto-next/ghi đè selection khi người học đã đổi câu hoặc gõ tiếp |
| Nộp câu cuối đồng thời gây deadlock trên MariaDB | Test có barrier thất bại với MySql deadlock tại `UpsertWritingProgressAsync` | Lấy `Users.UserID ... FOR UPDATE` trước INSERT attempt và mọi progress snapshot trong transaction Writing; cả hai request lưu, một lifetime credit |
| Token Admin không tương thích hash Unicode đã lưu từ policy cũ | Sau thay đổi NFC, fixture dùng legacy hash lưu form trả200 lỗi stale thay vì302 | Token GET và POST cùng dùng `NormalizedHash` đang lưu; hash chỉ được tính mới khi Admin thực sự lưu danh sách; không backfill hàng loạt |

Test bảo vệ việc đổi câu trong lúc chấm được thêm để kiểm tra đúng selection và
không auto-next sang câu thứ ba. Đây là coverage bổ sung; ba lỗi Node có bước đỏ
trước sửa là duplicate submit và hai trường hợp mất draft nêu trên.

## 3. Những nghi vấn đã kiểm tra, không cần sửa lại

- Standard/Premium dùng bài public đã publish; bài public bị ẩn không dùng được.
  Premium chỉ mở bài private của mình; chủ bị downgrade Standard nhận404 ở luồng
  private và thấy shell khóa ở catalog. Bài private người khác không lộ qua catalog,
  Data, Hint, Evaluate hoặc Delete. Admin dùng quyền riêng có sẵn của resolver.
- GET practice/data, hint và POST evaluate ràng buộc exercise/sentence; ID không có
  hoặc không thuộc bài trả404. ID <=0 và request thiếu dữ liệu trả400. Authentication,
  antiforgery và rate limiter giữ nguyên; identity lấy từ helper của server.
- Admin approved answers đã có bảo vệ role, antiforgery, tối đa5 biến thể/câu,
  giới hạn1000 ký tự, hash unique và content-bound concurrency token. Ba test cạnh
  tranh trên MariaDB (empty list, replace list, bulk edit) đều đạt sau chỉnh sửa.
- Bootstrap và Practice/Data không chứa EnglishMeaning hoặc danh sách biến thể chưa
  nộp. `acceptedAnswer` chỉ là câu của chính người học đã được nhận. Nút Gợi ý chủ ý
  mở bản dịch tham khảo của câu đang chọn: đây là hợp đồng hiện tại, không đổi thành
  một cơ chế hint khác. Feedback sau nộp có thể chứa rewrite/reference theo workflow.
- Canonical/approved match là Accepted. Paraphrase chưa duyệt, sai thì/negation hoặc
  khác số/dấu mang nghĩa không được đoán pass từ keyword overlap; kết quả có thể là
  NeedsReview, không đồng nghĩa chắc chắn sai. Rỗng/ký hiệu/phạm vi câu sai dùng
  NeedsRevision hoặc HTTP400 tùy loại đầu vào. Không bịa kết quả semantic grading.
- Local là authority mặc định. Cloud tùy chọn không đổi Passed/status/auto-next trong
  Local. Test fake 429, unavailable, invalid JSON và budget timeout giữ local status
  qua reload, không lộ provider details, không cấp credit cho NeedsReview.
- Retry bị từ chối không xóa trạng thái accepted trước đó: history giữ lần mới,
  `HasAccepted`/accepted text vẫn giữ lần đạt, resume chọn câu chưa đạt đầu tiên.
  Trạng thái reload suy từ Passed + EvaluationSource, gồm legacy `local+ai-feedback`.
- Rollback credit/outbox/cancellation đã có transaction đúng; không tạo thêm reward
  path. Các test mới chứng minh điều này cả SQLite lẫn MariaDB. Credit lỗi không được
  biến thành replay thành công; retry request mới có thể hoàn thành và nhận đúng một credit.

## 4. Ranh giới persistence cho stage 03

Luồng thật nằm trong `Services/WritingService.cs`, phương thức
`EvaluateWritingSentenceAsync`:

1. Resolve quyền và đúng câu; lấy canonical + approved variants; chấm local rồi thêm
   feedback tùy chọn trong budget. Chưa có learning write ở phần này.
2. Mở `writingUnit`; MariaDB lấy row lock theo user trước khi ghi.
3. `PersistWritingAttemptAsync` thêm `UserWritingAttempt` và SaveChanges **bên trong
   transaction đang mở**. `Id` được DB cấp ở đây; SavedChanges chưa có nghĩa là commit.
   Các trường authority cần dùng: `UserId`, `WritingExerciseId`,
   `WritingExerciseSentenceId`, `Passed`, `EvaluationSource`, `CreatedAtUtc`.
4. `LoadWritingExerciseProgressSummaryAsync` và `UpsertWritingProgressAsync` cập nhật
   sentence/exercise progress, giữ trạng thái đã đạt trước đó.
5. Chỉ lần chuyển sang hoàn thành cả bài mới gọi
   `ICompletionCreditService.TryApplyWritingExerciseCompletionAsync`.
   Credit key hiện tại: `{userId}:Writing:exercise:{exerciseId}`; completion XP20,
   `WritingCompletedCount=1`. Credit/goals/outbox tham gia cùng transaction.
6. `writingUnit.CommitAsync` mới là ranh giới dữ liệu có hiệu lực. Mọi lỗi trước đó
   rollback attempt, progress, credit, daily activity và notification intent.

**Điểm nối stage 03:** sau khi attempt/progress được save, trước nhánh
`if (firstTimeExerciseCompletion)` và trước Commit, xử lý confirmed-practice event
cho **mọi attempt Accepted**, kể cả khi bài còn dang dở hoặc đã hoàn thành từ trước.
Nên để private helper `PersistWritingAttemptAsync` trả entity/Id cùng occurrence
timestamp, rồi dùng writer/orchestrator dùng chung DbContext/transaction. Nếu cần
durable event/outbox mới, stage 03 phải thêm nó trong chính unit này; không phát
thông báo hoặc gọi external side effect trước commit.

- `UserWritingAttempt.Id` là identity của submission đã lưu; `CreatedAtUtc` là thời
  điểm occurrence phù hợp để stage 03 chuyển sang business date bằng clock UTC+7
  dùng chung. `UserWritingSentenceProgress.PassedAt` là lần đạt đầu, không đại diện
  một lần luyện lại ngày sau. Không dùng CompletedAt của bài cho mọi câu.
- Mỗi POST hiện tạo một attempt. API không có request-idempotency key để hợp nhất
  hai POST thành một attempt. Replay bài/lặp câu vẫn có thể là practice event mới;
  **không** được cấp lifetime exercise credit lần nữa. Stage 03 cần guard theo event
  và business date cho một streak step/bonus mỗi ngày, kể cả hai request trùng.
- Chỉ `Passed=true` đủ điều kiện confirmed event theo authority của assessment;
  NeedsReview/NeedsRevision, mở trang, login, hint, lưu local draft và lỗi rollback
  không phải confirmed practice. Không suy event từ lời feedback hay `UsedAi`.
- Không đặt event trong riêng nhánh first completion: cách đó bỏ mất accepted câu
  giữa bài và luyện lại ngày sau. Không thêm reward ở controller/browser.
- Stage 02 **chưa** ghi practice-day/streak cho từng Accepted sentence; đây là việc
  stage 03. Writing daily-goal counter vẫn đo first completed exercises theo work pack.

## 5. Kiểm chứng và artifact

Artifact local: `.appdata/local/test-results/writing-stage02/` (ignored).

| Kiểm tra | Kết quả thực tế |
| --- | --- |
| Baseline Writing + ambient transaction | 184 pass /0 fail /7 skip |
| Regression backend trước sửa | 5 fail /1 pass (`stage02-red.trx`) |
| Access + atomicity trước sửa quyền Admin | 1 fail /15 pass (`stage02-access-red.trx`) |
| Draft/duplicate trước sửa | 3 fail /4 pass trong Node |
| MariaDB trước khóa user | 1 deadlock fail /3 rollback pass (`stage02-mariadb-red.trx`) |
| Legacy Unicode hash trước sửa token | 1 fail (`stage02-legacy-hash-red.trx`) |
| Writing + ambient transaction + Goals completion recovery, sau review | 216 pass /0 fail /12 conditional skip (`stage02-final-writing-reviewed.trx`) |
| MariaDB completion + approved-answer concurrency | 10 pass /0 fail /0 skip (`stage02-mariadb-final-reviewed.trx`) |
| Node Writing + Goals + Account | 38 pass /0 fail /0 skip |
| Build ứng dụng `--no-restore` cuối lượt | Thành công; log `build.log`; clean compilation trước đó có nullable/analyzer warnings có sẵn |
| Model và migration repeat trên MariaDB | `HasPendingModelChanges=false`; applied history không đổi; attempt/progress/credit/award được giữ |
| Bảo toàn UI Gemini | SHA256 cả5 tệp khớp lúc bắt đầu |

Lệnh chính:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~Writing|FullyQualifiedName~CompletionCreditAmbientTransaction|FullyQualifiedName~GoalsCompletionRecovery'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~WritingCompletionMariaDbTests|FullyQualifiedName~WritingAcceptedAnswersMariaDbConcurrencyTests'
node --test scripts/tests/test_writing_ui.cjs scripts/tests/test_writing_assessment_status.cjs scripts/tests/test_writing_draft_isolation.cjs scripts/tests/test_goals_ui.cjs scripts/tests/test_account_ui.cjs
dotnet build TCTEnglish/TCTEnglish.csproj --no-restore
```

Lượt MariaDB dùng sentinel-guarded `tct_acceptance_writing021114`, loopback3338,
run ID `writing021114`, image `mariadb:10.5.23`, digest
`sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730`.
DB này tách khỏi Docker volume/DB học hiện có. Bài đồng thời cùng câu chạy5 vòng,
mỗi vòng có2 DbContext/connection và barrier; cả2 attempt tồn tại,1 credit20XP,
1 Writing completion/day increment; bonus streak giữ chính sách hiện tại.
Một test dùng trigger DB thật `SIGNAL SQLSTATE '45000'` chặn insert credit, xác minh
không còn bất kỳ learning write nào rồi bỏ trigger, retry và replay thành công.

## 6. Browser và giới hạn bằng chứng

- Chrome DevTools kết nối thành công. Mở HTML **Razor render từ TestWebApplicationFactory**,
  tài khoản synthetic101, qua static server loopback8892 và JS/CSS hiện tại.
  Evaluate được fake có kiểm soát; không phải một phiên web thật ghi progress vào DB.
- Ctrl+Enter hai lần khi đang chờ: chỉ1 Evaluate request; bản nháp mới còn nguyên sau
  Accepted. Chọn câu3 khi câu2 đang chờ: vẫn ở câu3 và giữ draft, progress thành2/14.
- Hai tab cùng context: tabB khôi phục draft, ghi nội dung mới; tabA nhận Accepted
  nhưng không xóa draft mới của B. Node kiểm tra thêm phân lập owner A/B và legacy key.
- Đã đo `innerWidth == documentElement.scrollWidth` tại1440,768 và390;390 dùng mobile
  emulation vì cửa sổ Chrome thường có minimum width500. Đã xem screenshot390,
  bật reduced motion và xác nhận focus vào textarea khi chọn câu.
- Chưa nghiệm thu lại full live login/logout nhiều tài khoản, native200% zoom,
 375px hoặc mọi responsive state của stage01. Không lấy khẳng định responsive trong
  bàn giao trước làm kết quả đã quan sát của stage02. Browser reload/progress thật
  được chứng minh bằng integration tests, không bằng static capture.

## 7. Schema/data, vấn đề còn lại và bàn giao

- Không thêm/sửa persisted entity, mapping hay relationship: không có migration mới.
  Không rewrite lịch sử accepted/status/credit. Unicode policy dùng cho so sánh hiện
  tại; legacy approved hash được giữ nguyên cho concurrency token và được tính mới
  theo luồng lưu Admin có sẵn, không chạy bulk backfill.
- Không thay đổi shared content thực; fixture test dùng DB disposable nên không có
  shared-data export package. Không đụng User Secrets hoặc key đã lưu.
- Optional feedback source/text bổ sung chưa có cột persistence riêng; attempt giữ
  local decision source, UsedAi và các feedback field hiện hữu. Accepted/NeedsReview/
  NeedsRevision reload đã kiểm chứng; không hứa khôi phục mọi provenance phụ như một
  assessment record mới. Đây là giới hạn schema hiện tại, không phát sinh event thưởng.
- Normalization/local exact policy không phải semantic model: cách dịch hợp lý nhưng
  chưa được duyệt vẫn NeedsReview. Không có paid-provider/live-AI quality claim.
- Docker Desktop trên máy lỗi startup tại Secrets Engine socket; không sửa credential,
  ACL, trust hoặc global config để vượt lỗi. Kiểm chứng dùng daemon WSL task riêng,
  storage trong file ext4 ignored trên D. Helper test/DB/daemon được dừng sau lượt;
  Docker Desktop cần xử lý riêng nếu chủ dự án muốn dùng launcher thông thường.
- Phạm vi hoàn tất là local stage02. CI/staging/production không được chạy hoặc xác
  nhận. Stage03 tiếp nhận ranh giới ở mục4; stage06 tiếp nhận việc bind UI mới.

## Đối chiếu final ở stage07

Header practice dùng `exerciseStatusLabel`, `exerciseAttemptCount`,
`sentenceAttemptCount` và `completedSentenceCount` của response đã lưu từ
POST `/Home/Writing/Practice/Evaluate`. Không bỏ metadata tổng hợp qua normalizer
hoặc tự tăng số bài từ trạng thái draft. Đây là binding những field backend đã
có, không đổi HTTP contract hay schema. Response lỗi/offline/429 giữ draft và
không tạo progress hiển thị giả. Lịch sử có timestamp được wrap trong cột có
min-width0 để completed/history state không tràn ở375–390px. Xem ledger07.
