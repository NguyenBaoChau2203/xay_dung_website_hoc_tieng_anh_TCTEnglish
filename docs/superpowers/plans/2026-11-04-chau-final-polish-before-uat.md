# Gói cải tiến cuối trước test tổng thể — Châu — 04/11/2026

**Current05/11**: reviewer đã hoàn tất fix local phát hiện trong UAT, gồm Speaking
init/NoSpeech và Goals summary/editor; 929pass/0fail/18skip, Node58pass. Browser
main journeys có evidence; paid/mic và broad owner UAT vẫn riêng. Đọc
[owner closeout](../../feature-audits/2026-11-05-chau-final-polish-owner-closeout.md).
Các trạng thái04/11 dưới đây là lịch sử, không yêu cầu làm lại implementation.

Trạng thái: **local B0–B7 đã triển khai bởi Cursor Auto (04/11/2026)** — F1/F2,
Speaking word comparison, Writing accepted answers + migration MariaDB verified
on disposable acceptance DB. Chưa commit/push/PR/merge/deploy; production gate OFF.

**Reviewer closure04/11: code/test findings R1–R5/RR1/RR2/C1.1 đã đạt; tiếp UAT
R6, chưa đóng toàn bộ B0–B7.** Run mới921pass/0fail/18skip; fresh MariaDB upgrade1,
leftover guard1, regressions4pass riêng. Không còn actionable code finding ở
scope lượt này. Full browser/demo-quality/startup measurement còn pending;
checklist dưới là báo cáo triển khai, không phải toàn bộ acceptance đã đạt. Xem
`docs/feature-audits/2026-11-04-chau-final-polish-code-acceptance.md` và
`docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`.

Audit và evidence: ../../feature-audits/2026-11-04-chau-progress-and-final-polish.md.
Prompt: ../../../.ai/prompts/chau-final-polish-before-uat.md.

## Mục tiêu và phạm vi kết thúc

Đóng lỗi local startup đã sửa, sửa hai finding Writing, thêm đối chiếu từng từ
trong Speaking và nhiều đáp án Writing do Admin duyệt, đồng bộ tài liệu, chuẩn
bị bộ demo rồi nghiệm thu luồng chính ba roles. **Hai tính năng mới là bắt buộc
trong lần thực thi này**, không còn là ý tưởng tùy chọn ngoài prompt.

| Lô | Kết quả cần giao | Phụ thuộc |
| --- | --- | --- |
| B0 | Chốt startup/key và các local changes có sẵn | Kiểm tra checkout thực tế |
| B1 | Nháp Writing tách theo tài khoản | B0 |
| B2 | Hiển thị đúng Accepted / NeedsRevision / NeedsReview | B1 |
| B3 | Speaking đối chiếu từ khớp, thiếu, thừa, khác | B2; không đổi công thức điểm |
| B4 | Admin duyệt thêm đáp án Writing; grading và migration đi cùng | B2; thực hiện sau B3 để review từng lô |
| B5 | Tài liệu, học liệu demo và số đo local | B0–B4 |
| B6 | UAT tổng thể ba roles, desktop/mobile | Smoke từng lô đã đạt |
| B7 | Review, evidence và điều kiện đóng | B0–B6 |

Giữ web text `gpt-6-luna` / reasoning `low`, audio `gpt-transcribe`; model chạy
Cursor Auto không phải model tích hợp web. Không khôi phục Whisper. Không sửa
Program.cs/appsettings/.csproj hoặc nâng framework/provider. B4 cho phép thêm
entity/DbContext mapping và migration bổ sung đúng tính năng đáp án đã duyệt;
không cần xin lại quyền cho các bước local nằm trong phạm vi đó. Schema khác
vẫn ngoài phạm vi. PLESK_DEPLOYMENT_ENABLED=false; hook/startup không gọi paid API.

## Checklist thực thi local (04/11/2026)

- [x] B0 startup/key dirty preserved; launcher tests green
- [x] B1 draft owner namespace + JS/C# regressions
- [x] B2 NeedsReview label/mapping
- [x] B3 Speaking word comparison (no extra ASR, no schema)
- [x] B4 WritingAcceptedAnswers + Admin form + grading + MariaDB upgrade
- [x] B5 README roles + known-issues/bug-log + demo manifest (.tmp)
- [x] B6 automated smoke; browser 3-role UAT còn chờ owner
- [x] B7 local evidence report; no commit/push/PR/deploy

## B0 — Đóng change set local có sẵn (P1 operational, làm trước)

Files đã dirty: scripts/start-local.ps1, scripts/LocalAiConfiguration.psm1,
scripts/tests/test_local_launcher.py, scripts/tests/test_local_ai_config.py,
AGENTS.md/.ai README, local guides và startup/uninstall audits.

- [x] Snapshot git status/diff; phân biệt sửa đã có với việc mới, không reset/stash.
- [ ] Review startup chỉ đọc User Secrets, DB/worker flags chỉ child env; env/key/
      TEMP restore cả success/failure/DatabaseOnly, occupied-port guard giữ nguyên.
- [ ] Review explicit key update có protected backup/recovery khi replace lỗi;
      không thử thay key thật để chứng minh test, không yếu hóa EFS/ACL.
- [ ] Tái dùng focused evidence nếu source chưa đổi; chạy mới khi đổi hoặc có
      khoảng trống. Không gọi lại live API để thay thế kiểm tra cấu hình.
- [ ] Ghi code/runtime nào local-only. Chuẩn bị nhóm commit để owner review sau;
      thực sự commit/push/PR/merge chỉ theo quyền giai đoạn owner đã cấp khi đó.

Acceptance: local chạy với store hiện có mà không thay length/mtime/protection,
healthready, đúng nguồn key; biết rõ configured không chứng minh live quota.
Không đổi DB volume, không seed/reset dữ liệu đang có, không dừng app khác.

## B1 — F1 owner-scoped Writing draft (P1, dependency B0)

Source confirmed: writing.js82–109 uses only exercise/sentence key. Logout
AccountController187–204 không xóa localStorage. Isolated production-helper probe
cho userB đọc được synthetic draftA. Đây chưa phải browser A→B acceptance.

Primary files:

- TCTEnglish/wwwroot/js/writing.js
- TCTEnglish/Views/Study/WritingPractice.cshtml
- TCTEnglish/ViewModels/WritingIndexViewModel.cs
- TCTEnglish/Controllers/StudyController.cs hoặc WritingService.cs, chỉ điểm tạo
  practice ViewModel thực tế sau khi CodeGraph trace caller xác nhận.
- scripts/tests/test_writing_draft_isolation.cjs (mới) và view/identity regression
  ở test integration hiện có hoặc file focused mới cần thiết.

Implementation checklist:

- [ ] Đọc newest symptom-relevant bug memory. CodeGraph trace practice controller
      →service→ViewModel→Razor→draft helpers. Dùng current-user helper chuẩn.
- [ ] Viết red regression chạy production JS/harness, không chép lại thuật toán.
- [ ] Đưa owner identity đã xác thực từ server qua ViewModel/data attribute;
      không lấy querystring/client-selected ID làm chứng minh quyền server.
- [ ] Version namespace mới theo owner/exercise/sentence. Không có owner hợp lệ
      thì không restore/persist draft; UI vẫn cho gõ/submit bình thường.
- [ ] Legacy key không có owner tuyệt đối không được tự nhận về account hiện tại.
      Không tự xóa draft người dùng đang có; bỏ qua hoặc cung cấp cleanup riêng
      khi owner cho phép. Không xóa tất cả localStorage của ứng dụng.
- [ ] Giữ recovery đúng tài khoản và catch storage disabled/full/SecurityError.
      Không thêm key/token/credential vào browser storage.
- [ ] Không đổi entity/schema chỉ để đặt namespace. Nếu phát sinh persistent
      design khác, tách proposal và kích hoạt quy trình migration, không lén thêm.

Regressions/acceptance:

1. A gõ draft public exerciseX/sentenceY →reload A: vẫn đúng draftA.
2. A logout, B login cùng browser origin/profile, mởX/Y: không hiện draftA.
3. B lưu draftB; A vào lại không nhậnB. Namespace2bài/câu không đụng nhau.
4. Legacy unowned draft không tự nạp vào A hoặcB.
5. localStorage không khả dụng không gây crash/mất quyền submit; thiếu owner
   không tạo global key mới; không in text draft trong log.
6. Browser acceptance A→B phải cùng storage context; chỉ mở2incognito profile
   riêng không chứng minh isolation của lỗi này.

Lưu ý: per-user prefix chỉ ngăn lẫn UI, không phải security boundary chống XSS
hoặc người dùng có quyền vào toàn profile trình duyệt. Nếu yêu cầu bảo mật draft
riêng cao hơn, thiết kế retention/session/server-owned draft riêng sau scope này.

## B2 — F2 giữ đúng3trạng thái Writing (P2, dependency B1)

Files: WritingIndexViewModel.cs, WritingService.cs mappings/saved snapshot,
Views/Study/WritingPractice.cshtml, wwwroot/js/writing.js, CSS chỉ nếu cần để
phân biệt trạng thái; tests UI behavior và backend snapshot/mapping liên quan.
Nếu sửa UI đọc DESIGN.md +tct-ui-component/ui-review trước.

- [ ] Trace AssessmentStatus/AssessmentSource/ReviewRequired từ decision→response,
      persisted evaluation snapshot→ViewModel→Razor payload→normalize→label/banner.
- [ ] Red regression: NeedsReview/ReviewRequiredtrue/passedfalse phải ra “Cần xem
      lại”, không bị gán nhãn “Làm lại” như NeedsRevision.
- [ ] Preserve optional fields qua normalize, response và payload reload. Với
      historical rows thiếu fields, dùng mapping dựa source/status đã kiểm chứng,
      không đoán verdict bằng text của SummaryTitle.
- [ ] NeedsRevision hiển thị cần sửa; Accepted hoàn thành/auto-next như cũ.
      NeedsReview vẫn passedfalse/không auto-next/không tự cấp completion/XP.
- [ ] Giữ accepted progress lịch sử nếu lượt sửa mới cần review; không thu hồi
      hoặc cộng lại credit chỉ vì UI trạng thái của draft hiện tại thay đổi.
- [ ] Không để AI optional feedback đổi quyết định Local hoặc sinh điểm giả.

Acceptance: submit/reload/error/history cùng phản ánh3trạng thái; pass→review
không làm mất completion cũ; retry/reload không nhân XP/notification. Mặc định
ViewModel/JSON mapping-only, không migration; đánh giá schema nếu thiết kế đổi.

## B3 — Tính năng mới: Speaking đối chiếu từng từ (dependency B2)

Phạm vi: luồng ghi âm và server assessment của trang video Speaking hiện có.
Dùng reference và transcript của chính lần đánh giá; không mở rộng ASR sang
mọi study mode, không thêm engine hoặc lần gọi OpenAI để tạo phần đối chiếu.

Source đã xác nhận: SpeakingAssessmentService lưu transcript đầy đủ nhưng
SpeakingController.AssessSpeaking chỉ trả transcriptPreview. SpeakingTextMatch
đang tính điểm theo LCS/reference tokens. So khớp chữ không chứng minh phát âm,
phoneme hay fluency. So sánh phải dùng transcript đầy đủ trước bước cắt preview.

Primary files, đọc caller/contract bằng CodeGraph trước khi sửa:

- TCTEnglish/Services/Speaking/SpeakingAssessmentService.cs, SpeakingTextMatch.cs;
  thêm pure helper SpeakingTextAlignment.cs hoặc tên tương đương trong thư mục này.
- TCTEnglish/Controllers/SpeakingController.cs, DTO/outcome đang chứa
  SpeakingAssessmentOutcome; tìm đúng chỗ định nghĩa, không tạo contract song song.
- TCTEnglish/Views/Speaking/Practice.cshtml, wwwroot/js/speaking.js,
  wwwroot/css/speaking.css; giữ lifecycle của speech-recorder.js.
- TCTEnglish.Tests/SpeakingAssessmentIntegrationTests.cs,
  OpenAiSpeechAssessmentAppTests.cs và focused alignment/JS regressions mới cần thiết.

Implementation checklist:

- [ ] Đọc DESIGN.md, tct-speaking-feature, tct-api-endpoint và UI skills liên quan.
      Viết regression trước khi nối vào outcome/JSON/UI.
- [ ] Pure deterministic alignment trả các thao tác matched/missing/extra/
      substituted, có cặp từ reference/transcript khi phù hợp. Tokenization thống
      nhất với SpeakingTextMatch; tie-break từ lặp phải cố định và có test.
- [ ] Giới hạn 256 tokens mỗi phía trước khi cấp phát ma trận; quá giới hạn trả
      comparisonAvailable=false và lý do an toàn. Không âm thầm cắt chữ rồi coi
      kết quả là so sánh toàn câu; không tạo vòng lặp/bộ nhớ không giới hạn.
- [ ] Tính bằng reference đã qua access check và full transcript đã nhận trong
      cùng assessment. Thêm field JSON tùy chọn comparison + availability; giữ
      nguyên các fields/routes/status cũ. Fixture/client thiếu field mới vẫn chạy.
- [ ] UI có câu mẫu/câu nhận diện và chú giải từ khớp, thiếu, thừa, khác; dùng
      textContent/encoding, không innerHTML với nội dung transcript. Không chỉ
      dùng màu; có chữ/icon, focus/keyboard và layout 390px/1440px.
- [ ] Nhãn “Đối chiếu nội dung nhận diện”; không mô tả chữ khác là lỗi phát âm
      chắc chắn. NoSpeech/error/timeout/chưa cấu hình không có comparison giả.
- [ ] Xóa kết quả cũ khi bắt đầu lượt mới/đổi câu/hủy; response đến muộn không
      được gắn vào câu hoặc lượt ghi âm khác. Reload không tự gọi speech API.
- [ ] Không đổi điểm text-match, ngưỡng pass, receipt binding user/sentence,
      TTL/single-use hoặc luồng commit/XP. Client không dùng diff làm quyền pass.
      Giữ fluencyAvailable/completenessAvailable đúng hiện trạng.
- [ ] Không lưu bảng alignment mới ở v1. Kết quả phục vụ lần đánh giá hiện tại;
      reload không có kết quả đang giữ thì hiển thị trạng thái chờ đánh giá.

Regressions/acceptance:

1. Exact/case/punctuation, missing/extra/substitution, từ lặp, rỗng, Unicode,
   số và giới hạn tokens có output xác định, không crash hoặc sai thứ tự.
2. Transcript dài hơn preview vẫn so đủ nội dung trong giới hạn; không diff
   trên preview 120 ký tự. Fake transcriber chứng minh đúng một lần gọi ASR.
3. NoSpeech/401/429/timeout/cancel không có điểm/word feedback giả, không credit.
4. Access/antiforgery, stale response khi đổi câu, transcript chứa markup,
   replay receipt, reload/duplicate commit giữ hành vi an toàn hiện có.
5. Browser desktop/mobile đọc được diff và chú giải. Tính năng không phát sinh
   thêm provider request, không cần entity/schema/migration.

## B4 — Tính năng mới: đáp án Writing do Admin duyệt (dependency B2/B3)

Hành vi: canonical EnglishMeaning vẫn được chấp nhận như trước. Mỗi câu được
Admin thêm tối đa năm đáp án khác, mỗi đáp án tối đa 1.000 ký tự. Khớp chính xác
sau normalization hiện tại với một đáp án đã duyệt thì Accepted; câu chưa khớp
vẫn NeedsReview, hard-invalid vẫn NeedsRevision. AI không tự duyệt đáp án.

Primary files:

- TCTEnglish/Models/WritingExerciseSentence.cs; entity mới WritingAcceptedAnswer.cs
  (tên có thể theo convention hiện có), Models/DbflashcardContext.cs.
- TCTEnglish/Services/Writing/WritingAssessmentPolicy.cs, WritingService.cs,
  loader/mappings cho evaluate/historical snapshots có liên quan.
- TCTEnglish/Areas/Admin/Controllers/WritingExerciseManagementController.cs,
  Areas/Admin/ViewModels/WritingExerciseManagementViewModel.cs,
  Areas/Admin/Views/WritingExerciseManagement/_ExerciseForm.cshtml và partial/JS
  authoring liên quan. Namespace legacy TCTVocabulary phải kiểm tra trước khi dùng.
- TCTEnglish/Migrations: migration bổ sung, Designer và snapshot cùng change set.
- TCTEnglish.Tests/WritingLocalAssessmentTests.cs, WritingAdminContentIntegrationTests.cs,
  WritingPracticePersistenceIntegrationTests.cs, WritingProgressIntegrationTests.cs,
  WritingNormalizationPersistenceIntegrationTests.cs và MariaDB acceptance mới.

### B4.1 — Data và quy tắc lưu

- [ ] Đọc tct-admin-panel, tct-api-endpoint, tct-ef-migration; ghi migration impact
      trước khi thay entity. Đọc model/mapping thực, không nâng EF/Pomelo/net9.0.
- [ ] Thêm bảng con, không thay canonical/historical attempts: Id, FK sentence,
      AnswerText(max1000), NormalizedHash, ApprovedByUserId nullable, ApprovedAtUtc.
      Admin ID lấy từ current-user helper ở server; không bind từ request.
      FK sentence cascade theo xóa sentence hiện có; approver SetNull nếu bị xóa.
- [ ] NormalizedHash là SHA-256 của normalized answer UTF-8, 64 ký tự ASCII/binary;
      unique index ngắn UX_WritingAcceptedAnswers_Sentence_Hash trên sentence+hash.
      Không unique-index toàn bộ varchar1000 utf8mb4 vượt giới hạn MariaDB.
      Hash dùng chặn trùng; grading vẫn so normalized text, không so hash mù.
- [ ] Không backfill hoặc sửa EnglishMeaning/users/attempts/progress đang có.
      Zero variants phải chạy như trước. Validate server +UI: tối đa5 đáp án,
      không rỗng, không quá1000, không trùng canonical/variant sau normalization;
      không silent truncate hoặc bỏ dấu phủ định, số, tiền tệ/toán để cho pass.
- [ ] Serialize thay đổi cùng sentence trong transaction và kiểm trạng thái cũ
      để tránh hai Admin ghi đè âm thầm/cạnh tranh vượt giới hạn. Trả lỗi hữu ích
      khi nội dung hoặc danh sách đã đổi từ lúc mở form; test bằng MariaDB.

### B4.2 — Admin authoring, giữ câu và lịch sử

Source pitfall đã xác nhận: ValidateFormModel tạo lại model.Sentences từ hai
FullText; SyncWritingExerciseSentences ghép các câu cũ/mới theo vị trí, không
theo posted sentence ID. Chỉ gắn list vào input ViewModel sẽ bị mất khi validate
hoặc gắn biến thể sang câu khác khi passage được tách lại.

- [ ] V1 giữ form tạo passage hiện có. Sau khi lưu bài, Admin quản lý biến thể
      theo từng sentence đã có ID trên trang Edit qua partial/form riêng và
      POST riêng, tránh nested forms. Canonical và biến thể được giải thích rõ.
      Không cần gắn biến thể vào bước auto-split lúc Create.
- [ ] Endpoint Admin-only +antiforgery, ViewModel riêng; query ràng buộc
      exerciseId +sentenceId (+variantId nếu dùng). ID không thuộc bài/câu trả404.
      GET dùng projection/AsNoTracking; không đưa raw entity vào view.
- [ ] Thêm/sửa/xóa biến thể chỉ chạm bảng con; không recreate sentence hoặc xóa
      attempts/progress. Lưu lỗi phải rollback cả list, giữ validation/input.
- [ ] Chặn bulk Edit làm đổi nội dung, vị trí ghép hoặc xóa câu đang có variants
      nếu chưa có thao tác Admin xử lý rõ. Báo cần quản lý/xóa variants có chủ ý
      trước khi sửa passage; không tự mang variants qua câu mới/âm thầm bỏ chúng.
      Giữ nguyên việc sửa metadata hoặc câu khác không ảnh hưởng mapping.
      Check guard và save cùng transaction; test cạnh tranh với save variants.
- [ ] Hai Admin cùng sửa một danh sách: phát hiện stale state, yêu cầu tải lại;
      không lấy hash/ID client làm bằng chứng quyền. Không tạo Teacher role,
      review queue cho learner hoặc cơ chế AI tự chèn đáp án.

### B4.3 — Server grading và tương thích

- [ ] Sau access check, tải variants đúng sentence bằng query bounded/projection,
      tránh N+1 toàn catalog. Không nhận danh sách đáp án từ learner POST.
- [ ] Policy ưu tiên canonical local-exact, rồi approved variant với source rõ
      như local-approved-variant. AssessmentStatus=Accepted, Passed=true và
      CanAutoAdvance thống nhất với response/snapshot/UI B2.
- [ ] Trace TryBuildOutOfScopeSubmissionFailure đang chạy trước Assess: đáp án
      đã duyệt cho câu hiện tại không bị từ chối vì trùng reference câu lân cận;
      chỉ khớp variant của câu khác không được tự accepted. Giữ các guard còn lại.
- [ ] Không lộ canonical/approved list trong initial learner JSON/evaluate payload
      chỉ để chấm client. Giữ hint/feedback contract đã có; optional AI feedback
      không đổi verdict, không tự duyệt hoặc lưu biến thể.
- [ ] Thêm/xóa variant chỉ ảnh hưởng submission mới. Không chấm lại lịch sử,
      không thu hồi completion cũ hoặc cộng lại XP/badge/notification. Replay
      và completion-credit transaction vẫn dùng cơ chế hiện có.

### B4.4 — Migration và regression bắt buộc

- [ ] Repo-local EF9.0.4: thêm migration có tên rõ, giữ Designer/snapshot; review
      Up/Down/FK/index và chạy has-pending-model-changes. Không sửa migration đã
      áp dụng, không invent history hoặc DROP/ALTER dữ liệu cũ. Down chỉ review
      để hoàn nguyên phần bổ sung, không chạy trên dev/hosting chứa dữ liệu.
- [ ] Disposable MariaDB10.5.23 tách biệt: áp lịch sử cũ + tạo dữ liệu sentinel
      users/sentences/attempts/progress →upgrade→kiểm hàng/FK/IDs giữ nguyên,
      thêm/đọc variants→chạy migrate lần hai không thay dữ liệu. Chứng minh unique,
      độ dài/index, max5/concurrency và transaction rollback bằng đúng provider.
- [ ] Unit/integration: canonical không variants; variant accepted; nonmatch
      NeedsReview; hard-invalid NeedsRevision; số/dấu/negation không bị normalize
      sai. Empty/duplicate/limit/stale form/cross-ID/CSRF/role và không leak JSON.
- [ ] Admin thêm/sửa/xóa variants và bulk-edit guard giữ sentence IDs/attempts;
      failed save rollback; approved-current vs other-sentence guard đúng;
      bỏ variant không hủy accepted history, submit lại không nhân XP.
- [ ] Build +focused .NET/JS; schema và MariaDB acceptance phải đạt trước UAT.
      SQLite-only hoặc skip MariaDB không đủ điều kiện gọi B4 hoàn thành.

Acceptance: Admin duyệt một cách diễn đạt khác →learner submit được Accepted
không cần AI; cách chưa duyệt vẫn NeedsReview; reload giữ đúng trạng thái,
đúng quyền, không đổi lịch sử và migration lặp an toàn. Nếu provider bị chặn,
ghi blocker/evidence còn thiếu, không đánh dấu đã hoàn tất.

## B5 — F3 docs + học liệu + đo local (P2, dependency B0–B4)

- [ ] README: role thực Admin/Premium/Standard, Student legacy; speaking runtime
      mới, model/ASR và giới hạn text-match. Giữ thông tin đóng góp thành viên.
- [ ] Annotate/update phần current snapshot của architecture backlog/known issues
      tháng3: provider MariaDB/Pomelo; source secrets/startup seeding/paging/Goals
      đã thay đổi. Không rewrite historical ledger hoặc tuyên bố mọi key cũ đã rotate.
- [ ] Chọn bộ demo nhỏ: ít nhất2Speaking videos có captions/timecodes hợp lệ,
      2Writing exercises đáp án đã duyệt, đủ goal/badge flow. Ưu tiên dữ liệu có
      sẵn; nếu tạo sample phải dùng namespace/owner riêng, không seed đè dev DB.
- [ ] Demo Speaking có case từ thiếu/thừa/khác từ fake fixture không nhạy cảm;
      Writing có canonical, một variant Admin đã duyệt và một câu NeedsReview.
      Học liệu/variants ghi rõ nguồn duyệt, không coi AI-generated là đã duyệt.
- [ ] Ghi manifest local không chứa password/key/private answer vào ignored data;
      sample bootstrap không tự coi là học liệu đã kiểm chất lượng.
- [ ] Đo cold/warm startup, memory, API timings trong điều kiện ghi rõ. Máy snapshot
      15.69GiB/available1.05GiB và43containers là dấu hiệu đo tiếp, không phải root cause.
      Inventory/đề xuất quản lý workloads nhưng không tự dừng stack khác.
- [ ] Không cài thêm AI engine/model/framework hoặc dùng Colab vào đường demo.

Acceptance: docs không hướng agent làm lại việc đã xong; bộ demo mở được; số đo
thật và limitations rõ, không gán %tăng tốc từ việc gỡ Whisper.

## B6 — Test tổng thể và sửa theo lô (dependency B0–B5)

Mỗi lô B1–B4 chạy smoke/regression ngắn trước; B6 là đợt UAT tổng thể owner mong
muốn sau gói cải tiến, không bỏ regression trong quá trình triển khai.

| Phạm vi | Luồng chính và negative checks |
| --- | --- |
| Account/Profile/Settings | Login/logout, validation, social sentinel, sửa profile, session expired, reset-token invalid/replay; SMTP/OAuth thật chỉ nếu cấu hình/quyền test có |
| Speaking | Playlist/filter/page, record/listen, mic denied, stop/đổi câu, NoSpeech, queue/timeout/cancel, đối chiếu từ và response cũ, assess→commit→reload, replay không cộng thưởng |
| Writing | Filter/page/owned permission, draft isolation,3statuses/reload, canonical/approved/unapproved answers, hint, duplicate submit, local mode khi AI lỗi; generation/quota khi được bật |
| Goals/Badges/notifications | Learning completion→progress/XP/streak/badge/notification; reload/replay không trùng, user khác không đọc/ghi hộ |
| AI full/embed | History/load older, send/stop/disconnect, quota, không HTTP retry sau unknown outcome, error status giữ draft/history hợp lý |
| Admin | Standard/Premium không vào Admin; Admin CRUD/filter/page/modals/validation; quản lý variants, stale/cross-ID/max5 và passage-edit guard đúng |
| Layout |390px/1440px, keyboard/focus/Escape/reduced motion, loading/empty/error, không overflow ngoài vùng table được cuộn |

Quy tắc test:

- Synthetic accounts riêng; không đổi role/xóa dữ liệu của owner. Tất cả user
  mutations giữ antiforgery và ownership. Dùng context chung riêng cho test F1.
- Fake-provider/harness cho401/429/timeout/outage, không cố làm cạn quota thật.
- Paid live audio/text/mic thực chỉ khi owner cho phép riêng, có budget hữu hạn
  và media không nhạy cảm; live test opt-in không bật cho full suite. Budget test
  hiện tại8POST/120s tính trên từng process, không phải global account cap.
- Lỗi P0 mất dữ liệu/quyền →P1 chặn luồng chính/privacy →P2 UI/feedback →P3cosmetic.
  Một bug log có steps/expected/actual/file/fix/regression; không gom tất cả thành
  redesign lớn. Không call skipped/live-unrun là pass.

## B7 — Điều kiện đóng, không tự deploy

- [ ] F1/F2 red→green và browser cùng session chứng minh đúng.
- [ ] B3 word comparison và B4 approved variants đã triển khai, regression/browser
      đạt; không để hai tính năng này thành TODO hoặc chuyển lại “optional”.
- [ ] 3roles, core journeys +negative/error/reload đã kiểm, P0/P1 trong scope đã đóng.
- [ ] Build/relevant .NET/JS/Python checks theo thay đổi; full suite khi scope đủ
      rộng. B4 có schema nên MariaDB upgrade/repeat/data-preservation bắt buộc;
      không dùng SQLite thay provider proof. Ghi skip/blocker và exact commands.
- [ ] Review diff +secret/artifact scan; key/file protection và runtime data giữ nguyên.
- [ ] Báo cáo UI desktop/mobile +các acceptance còn chờ owner rõ ràng.
- [ ] Commit/push/PR/merge/CI/release chỉ theo owner stage authority. Production
      gatefalse; staging backup/restore/hosting access là nghiệm thu riêng.

## Ngoài scope tự động

New Teacher role/review queue, controller rewrite, framework/model/provider
migration, index/schema không liên quan B4, Reading/Listening/Class/Billing
features mới. Account-service extraction, broad load testing và enabled staging
backup/restore drill để sau UAT, cần scope riêng. Hai tính năng B3/B4 đã được
owner chọn và là điều kiện đóng B0–B7.
