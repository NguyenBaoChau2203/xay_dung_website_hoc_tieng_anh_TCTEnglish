# Plan1 — giao diện các module Châu — Gemini3.8 Flash High

Ngày02/11/2026. Trạng thái cập nhật03/11: **ĐÃ ĐÓNG PHẠM VI LOCAL**.
Triển khai, các vòng repair và kiểm chứng cuối:
[báo cáo đóng hai Plan](../../feature-audits/2026-11-03-plan1-plan2-local-closeout.md).
Checklist dưới là thiết kế gốc; gate ngoài local chưa chạy được liệt kê riêng
trong báo cáo, không được suy thành đã deploy/clean-PC acceptance.
Nhánh đã tạo: `feature/chau-ui-performance-audit`; baseline master210bf2d.
Model thực thi trong IDE: Gemini3.8 Flash High. Model website vẫn Luna low.
Một agent, từng batch, không giao worker/reviewer/model khác.

## 1. Đích đến và giới hạn

Các màn của Châu rõ việc cần làm, thuận tiện trên mobile/bàn phím, phản hồi đúng
trạng thái thật. Giữ brand DESIGN.md: Inter/primary#4255ff/nền#f6f7fb,
Bootstrap5.3/Razor, icon đang được layout tương ứng nạp. Không redesign toàn bộ
website, không React/Tailwind/GSAP, không thêm icon CDN hoặc số liệu giả.

Đọc [audit hiện tại](../../feature-audits/2026-11-02-chau-ui-performance-audit.md),
AGENTS.md, DESIGN.md, tct-superpowers/tct-engineering/tct-ui-review rồi skill
tct-ui-component; UX/motion/domain chỉ load khi batch cần. Audit29/10 là lịch sử,
không thi công mù những finding đã sửa.

**Quyền trong prompt thực thi:** sửa Razor/CSS và UI JavaScript trong phạm vi
dưới; chạy build/kiểm tra local. Không sửa controller/services/entities/DbContext,
Program.cs/appsettings/.csproj/migrations hoặc đổi API/quota/thuật toán.
Đây là plan; việc tạo tài liệu chưa thực thi các thay đổi đó.

Không commit/push/PR/CI remote/merge/deploy, không đổi branch đang có dirty work,
không Plesk hoặc gate. Key chỉ local; không in vào output/ảnh/prompt. Không gọi
OpenAI trả phí/gửi mail/import YouTube chỉ để chụp UI. Không sửa báo cáo BTH2.

## 2. Thứ tự và checkpoint

`UI0 → UI1 → UI2 → UI3 → UI4 → UI5 → UI6 → UI7 → owner review → Plan2`.
Chạy tuần tự trong nhánh hiện tại, giữ diff Plan1 để Cursor kế thừa sau review.
Không mở hai IDE agent thực thi đồng thời. Sau mỗi batch cập nhật ledger:
`docs/feature-audits/2026-11-02-chau-ui-execution-ledger.md`.

Mỗi dòng ledger: batch/finding, file, hành vi trước/sau, viewport/role/state,
check thực chạy/kết quả, ảnh ignored, còn thiếu và bước kế tiếp. Không ghi pass
cho trạng thái chỉ xem source. Nếu context gần đầy, ghi checkpoint và tiếp tục
từ batch dở, không đọc lại toàn repo hoặc làm lại batch đã đủ bằng chứng.

## UI0 — xác nhận baseline và hợp đồng

**Ưu tiên:** bắt buộc trước sửa. **Files:** ledger mới, không runtime.

- [ ] `git status --short`, branch/HEAD; phân biệt5 tài liệu audit/plan/prompt mới
  và BTH2 với diff agent khác. Không stash/reset/checkout mất việc.
- [ ] CodeGraph exact affected symbols/views maxFiles1–3; đọc gap CSS/Razor/JS.
- [ ] Lập manifest selector/endpoint thực tế từ view + caller; dùng danh sách
  mục4 làm seed, không coi tên selector cũ là luôn còn đúng.
- [ ] Chạy local qua `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
  Cổng bận: giữ workload, chọn free port + `-Configuration Release -WebPort <port>`.
  Xác nhận health, Speech Status và key SOURCE, không giá trị key.
- [ ] Đăng nhập tài khoản local được phép; nếu không có, ghi authenticated browser
  blocked rồi tiếp tục source/public. Không tự sinh account trong DB đã có user.
- [ ] Chụp before ở1440×900 và390×844; dùng device emulation thật, ghi actual
  clientWidth. Resize window có sàn500 không chứng minh mobile390.

**Gate:** biết rõ app/role/port/source hiện tại; nhận diện selector/contracts;
không lấy screenshot instance cũ làm bằng chứng source mới.

## UI1 — Auth và recovery (U01,U02)

**Files:** Views/Account/{Auth,ForgotPassword,ForgotPasswordConfirmation,
ResetPassword}.cshtml; wwwroot/css/{auth-stitch,auth-recovery,tct-learning-ui}.css
chỉ file thực tồn tại; JS scoped mới nếu cần tách logic UI. Xác minh CSS recovery
đang được view nạp trước khi tạo file mới.

- [ ] Gắn `label for` với input id thật, `autocomplete` email/current-password/
  new-password/name, login email type=email. Giữ name/email/password/fullName,
  action/method/antiforgery và JSON success/blocked/message.
- [ ] Switch tabs có trạng thái selected/controls phù hợp; bàn phím và focus
  theo semantic tabs hoặc button đơn giản nhất quán. Không ẩn panel còn focus.
- [ ] Chặn submit lặp bằng pending state cho login/register; aria-busy,
  label đang xử lý; finally khôi phục khi lỗi. Nếu redirect thành công giữ state.
  Không dùng form.submit để bỏ qua native validation; tránh submit-event recursion.
- [ ] Error render bằng textContent/DOM encoded; role=status/alert và liên kết
  lỗi với form; không nối server message vào innerHTML.
- [ ] Sửa card overflow390px (x=-3,width396); kiểm padding/min-width/box-sizing
  trong scope, không che bằng overflow-x:hidden toàn body.
- [ ] Compact hero <=480px: giữ logo + tên ngắn; chuyển/hide feature decoration
  để form xuất hiện sớm. CTA đăng nhập có thể tới bằng ít cuộn; không ép height
  cứng để đạt một ảnh khi bàn phím ảo mở. Register dài được cuộn tự nhiên.
- [ ] DOB hiện không có persistence: không hứa đã lưu ngày sinh; giữ ngoài luồng
  submit như hiện tại hoặc bỏ cụm trang trí khỏi form với ghi chú ledger. Không
  tự thêm field database. Terms vẫn phải được kiểm trước submit.
- [ ] H1 cho nhiệm vụ đăng nhập/đăng ký và main landmark; logo không chiếm H1
  nhiệm vụ recovery. Giữ token/email hidden, không hiển thị token trong ảnh.
- [ ] Reduced motion cho animation trang/loader, không dùng timer làm điều kiện
  đăng nhập thành công. Không nâng/depin SweetAlert ở batch UI; BE7 phụ trách asset.

**Nghiệm thu:** keyboard switch/toggle/submit; label click focus đúng; bad input,
error/network/pending/blocked mô phỏng; không duplicate POST;390/375/768/1440px
không tràn ngang; recovery đã tốt không bị làm lại. Không gửi reset mail thật.

## UI2 — shell chung và điều hướng (U04)

**Files:** Views/Shared/_Layout.cshtml, wwwroot/js/layout.js,
wwwroot/css/layout.css; Areas/Admin/Views/Shared/_AdminLayout.cshtml.
Header/nav là shared: chạm tối thiểu và smoke route của đồng đội.

- [ ] lang=vi cho layout tiếng Việt; main id duy nhất; skip link hiện khi focus.
- [ ] Menu icon/avatar/sidebar accordion dùng button type=button có accessible
  name, aria-controls/expanded đồng bộ trạng thái. Giữ class/id event hooks.
- [ ] Enter/Space hoạt động tự nhiên; Escape đóng và focus trở về trigger.
  Khi mobile sidebar mở, không để nội dung ngoài panel nhận focus vô tình;
  không áp focus trap cho dropdown nhỏ như dialog lớn. Outside click vẫn đúng.
- [ ] Không sửa thuật toán notifications/AI launcher/folder creation. Modal folder
  cũ ngoài phạm vi: ghi issue riêng nếu cần, không rewrite theo tiện tay.
- [ ] Active link có aria-current; nhãn điều hướng nhất quán; top actions không
  chèn lên nội dung hoặc composer. Scope CSS tránh ảnh hưởng Admin/account.

**Nghiệm thu:** tab/Shift+Tab/Enter/Space/Escape; desktop collapsed/mobile overlay;
Home/Vocabulary/Class/Folder và5 nhóm màn Châu không mất nav; không lỗi console
mới. Các transition tắt/giảm khi reduced-motion, vẫn biết panel mở hay đóng.

## UI3 — Profile/Settings mobile và hiển thị trung thực (U01,U03,U06,U08,U11)

**Files:** Views/Account/{Profile,Settings}.cshtml; css/account.css;
UI script hiện có hoặc tách scoped account-page.js.

- [ ] Sửa P1 trước: account.css1303+ position:sticky đang override media680,
  làm sidebar260px vẫn chiếm flex flow ở390 và account-main chỉ122px. Gom rule
  desktop/mobile theo scope, mobile sidebar fixed/off-canvas đúng state, main
  dùng đủ chiều ngang. Không fix bằng overflow-x:hidden/body margin hack.
- [ ] Không main lồng main: giữ outer layout main, đổi inner account-main sang
  section/div với tên/heading nếu cần. Kiểm không đổi class/id JS đang dùng.
- [ ] Bỏ Writing75%/Speaking40% hardcode. Trong phạm vi UI, thay bằng CTA đến
  Goals hoặc trạng thái chưa có dữ liệu; không tính số từ DOM/badge/streak.
  Nếu muốn mini-progress thật ghi dependency BE6, không mở backend ở Plan1.
- [ ] Profile tập trung avatar/tên/thông tin; security CTA rõ tới Settings.
  Không mất endpoint đổi email/password/delete hoặc confirmation CONFIRM.
- [ ] Labels cho settings email/password; toggle có tên/pressed state và tab
  được, bỏ tabindex=-1 trên thao tác người dùng cần dùng. Tên icon decorative ẩn.
- [ ] Linked account: không tuyên bố đã liên kết Facebook/Google chắc chắn dựa
  thông tin không đủ. Hiển thị mô tả giới hạn/unknown; không giả trạng thái.
- [ ] Profile mặc định gọi default-avatar.png404: dùng initials/icon có sẵn hoặc
  asset local tồn tại, có fallback khi URL ảnh lỗi. Không ảnh placeholder CDN.
- [ ] Form error/success giữ typed validation/TempData hiện hành; scroll/focus
  đúng field sau invalid submit. File avatar có trigger bàn phím/label, accept
  giữ nguyên. Kiểm tên dài, ảnh lỗi/fallback, nút lưu chờ và khôi phục.
- [ ] Kiểm sidebar account ở mobile, dùng Settings là nơi quản lý bảo mật rõ
  ràng; không remove hàng loạt ID/tab mà chưa cập nhật caller thực tế.

**Gate:** không còn số tiến độ giả; form actions/contracts và xác nhận xóa còn
nguyên; không click lưu/xóa user thật để chụp ảnh. Tests mutation dùng disposable
fixtures trong Plan2, browser ở Plan1 có thể dùng interception không gửi thật.

## UI4 — Speaking/Writing/Goals: task layout và states (U09)

**Files:** Views/Speaking/{Index,Practice}; Views/Study/{Speaking,Writing,
WritingExercises,WritingPractice}; Views/Goals/Index; CSS speaking/writing*/goals;
JS speaking.js/writing.js/goals.js chỉ phần presentation/event wiring cần thiết.

- [ ] SpeakingPractice: desktop media/câu/recorder có hierarchy rõ; mobile media
  trước, câu đang luyện và mic sát nhau, control wrap chứ không tràn ngang.
  Primary controls ưu tiên44×44px; text không chỉ phân biệt bằng màu.
- [ ] SpeakingIndex mobile390 hiện scrollWidth417 do chips/button và hero blob.
  min-width:0 cho flex child, scroll chỉ bên trong chips; clip blob trong hero,
  không ẩn overflow viewport để che nội dung thật.
- [ ] States idle/requesting-mic/recording/uploading/assessing/saved/NoSpeech/
  permission-denied/engine-busy/timeout/cancel. Dùng state hiện có; không thêm
  request hoặc auto retry thu âm. Giữ câu đang ghi đúng context khi chuyển câu.
- [ ] Hiển thị mức khớp lời nói; fluency/completeness chưa đo giữ dấu — và giải
  thích. Không đổi threshold, score, assessmentId hoặc commit semantics.
- [ ] Import learner giữ captions/SRT/VTT-only và Premium lock; trạng thái queued/
  running/failed/done rõ. Không biến UI này thành audio upload/transcribe mới.
- [ ] Writing: editor nằm trước hoặc sát câu đang làm ở mobile, không mất draft;
  nút hint/submit rõ. Accepted/NeedsRevision/NeedsReview khác biệt bằng text/icon.
  Không đổi NeedsReview thành sai/đạt chỉ để tự next câu.
- [ ] Goals: số liệu từ Model, có empty/no-activity, progress labels đọc được,
  biểu đồ keyboard/text alternative. Modal create/edit focus đúng/invalid vẫn mở;
  không bỏ Reading/Listening khỏi5 areas hiện có.
- [ ] Giữ receipt/progress/XP phía server; mock states qua browser interception
  hoặc fixture, không gửi assessment/progress giả vào DB thật.

**Nghiệm thu:** long VN/EN, empty/no lesson, Standard/Premium lock, network error;
375/390/768/1440, zoom200%, reduced motion. Mic/accent thật là manual acceptance
riêng; không gọi simulated permission là mic người thật đã pass.

## UI5 — AI full page và embed (U05)

**Files:** Views/Ai/{Chat,ChatEmbed,_ChatShell}; css/ai-chat.css;
js/ai-chat.js và ai-chat-launcher.js chỉ phần UI.

- [ ] Composer textarea giữ id messageInput/maxlength1000/aria-describedby.
  Enter gửi, Shift+Enter xuống dòng, IME composition không gửi sớm; kiểm virtual
  keyboard/scroll ở mobile. Auto-grow có max-height, không đẩy mất send/stop.
- [ ] Đưa observability vào disclosure đóng mặc định; giữ metric IDs/API nếu
  chưa đổi backend. Quota vẫn dễ thấy, không biến screen thành bảng kỹ thuật.
- [ ] P1 đã đo390×844: metrics299px, chatWindow25.78px. Sau sửa history empty
  collapsed/metrics collapsed không ép log còn một dòng; mục tiêu log>=220px
  ở viewport này trước mở bàn phím, hoặc page scroll tự nhiên không mất messages.
  Kiểm chiều cao thực, không chỉ screenshot đẹp. Không ẩn bớt tin nhắn thật.
- [ ] History trên mobile dùng disclosure/drawer hợp lý; chưa thêm paging ở UI
  khi BE3 chưa có contract. Có thể chuẩn bị vị trí Load older nhưng không nút giả.
- [ ] Transport có cả SignalR và HTTP fallback. Giữ StartAiStream/StopAiStream,
  AiStreamStarted/Chunk/Completed/Failed, launcher postMessage. L11 Stop-before-
  provider-complete thuộc BE2; UI không được giả báo đã cancel server khi chưa có.
- [ ] Giữ log/live regions, send/stop busy semantics, scroll-to-bottom không kéo
  người đang đọc lên trên; empty suggestions thật, error/retry không mất draft.
- [ ] Markdown vẫn qua sanitizer; composer/input là text, không innerHTML user.
  Full/embedded cùng selectors/logic, không nhân bản controller hoặc signal path.

**Nghiệm thu:** fake text dài/code/list/link, lỗi API401/429/timeout dưới dạng UI
friendly (không raw error/key), gửi/dừng/đóng-mở embed, return focus; no paid API.

## UI6 — Admin theo nhiệm vụ

**Files:** Admin Views/Dashboard/Index, UserManagement/Index,
SpeakingVideoManagement/{Index,Create,Edit}, WritingExerciseManagement/{Index,
Create,Edit,_ExerciseForm}, AiManagement/Index; CSS admin đang được views nạp.

- [ ] Filters/primary CTA/count/table/empty/pagination có hierarchy; bảng mobile
  scroll ngang trong vùng có tên hoặc card layout thật, không cả viewport.
- [ ] Icon action có tên; edit/block/unlock/delete modal giữ data attributes,
  antiforgery/confirmation/error pending. Không thực hiện destructive action lên
  user/content thật; fixture hoặc intercept riêng.
- [ ] Admin sidebarToggle hiện không accessible name; Writing Create IsPublished
  checkbox chưa label: sửa trực tiếp label/id/name, không thay checkbox thành
  control trang trí không submit. Kiểm cả9 admin view liên quan.
- [ ] Form Speaking/Writing giữ field names, dynamic rows và preview; label
  rõ required/error. Không sửa parser/_ExerciseFormScripts/ML.NET training logic.
- [ ] Dashboard recent rows/readable values, không thay số liệu thật bằng chart
  trang trí. AIManagement giữ nút train thật, không bấm train khi audit UI.
- [ ] Nếu gặp controller response tiếng Việt mojibake ghi BE1/BE6, không kéo
  controller vào Plan1 chỉ để sửa text. View text sửa UTF8 trong phạm vi.

**Gate:** Admin quyền đúng; Standard không thấy/admin direct URL bị chặn như
baseline; frontend không che đi lỗi quyền;9 view admin có before/after nếu sửa.

## UI7 — chốt và bàn giao

- [ ] `dotnet build TCTEnglish/TCTEnglish.csproj -c Release --no-restore`;
  nếu binary đang bị process khóa, dùng artifacts output riêng đúng fixture
  contracts hoặc dừng đúng audit-owned instance đã xác minh, không kill toàn dotnet.
- [ ] Chạy các test hiện có liên quan markup/JS bị sửa. Không thêm unit tests
  soi từng CSS class. New JS state logic đáng kể cần behavior regression meaningful.
- [ ] Xác định tập tests bằng source hiện tại: SpeakingAssessment/PremiumSpeaking,
  Writing*Progress/PremiumWriting, GoalsPhase*, AiPhase4. Chạy focused sau batch,
  full solution một lần cuối nếu sửa nhiều shared contracts; không chạy cloud/live
  flags mặc định hoặc tự gọi scripts bootstrap acceptance thiếu identity.
- [ ] `git diff --check`; xem diff theo files/selector; ghi actual pass/fail/skip.
- [ ] Matrix25 view: AU01–06 Account, CO01 Contact, SP01–03 Speaking, WR01–03
  Writing, GO01 Goals, AI01–02 Chat/embed, AD01–09 admin. Chỉ Contact/shared views
  smoke nếu không sửa; các view chưa có role/data phải đánh dấu unverified.
- [ ] Mỗi view sửa: desktop/mobile, keyboard/focus, long text, pending/error/
  empty/success liên quan; screenshots ignored, không ảnh chứa email/key/token thật.
- [ ] Ledger kết luận findings closed/partial/deferred, file/change/check/risks;
  pending backend dependencies BE1/BE3/BE6; source model Luna low không đổi.
- [ ] Dừng ở local handoff để owner review. Không tự commit/push/CI/merge/deploy.

## 4. Selector/contracts cần giữ (xác minh lại tại UI0)

| Nhóm | Các mốc |
| --- | --- |
| Auth |form-login/form-register, tab-login/tab-register, btn-google/btn-facebook, login-email/password, register-email/name/password, blocked-alert, token/form names|
| Account |Avatar/avatarClickTarget, sidebarMobileToggle/accountSidebarToggle, deleteConfirmInput/deleteAccountBtn, modal deleteAccountModal|
| Speaking |youtube-player, current-transcript-en, sentence-scroll-container, btn-record, score-total/ring-total, tab-pronunciation/tab-dictation, my-import-panel/mip-submit-btn; server receipt/progress fields|
| Writing |data-writing-practice/data-sentence-id, answerInput/btnSubmit/btnHint, feedbackBanner/Title, writingPracticeData, data-writing-create-form, query filters/page|
| Goals |goalEditorModal, data-goal-mode, data-testid goal-header-cta, goalUpdateToast, five GoalArea values|
| AI |data-ai-chat-shell, chatForm/chatWindow/messageInput/sendBtn/stopBtn/typing, conversationIdInput, deleteConversationModal; POST /AI/Chat/Send/Delete; launcher postMessage contracts|
| Admin |usersTable/videoTable, btn-edit/btn-block-user/btn-unlock, editUserModal/blockUserModal/deleteVideoModal, writingExerciseForm, btnTrain/trainForm|

## 5. Không nằm trong đợt UI này

Phoneme/fluency engine mới, chấm Writing ngữ nghĩa tự động, xây OAuth link schema,
email outbox, server pagination, query indexes, CI/deployment changes. Những mục
logic đã có trong Plan2; ý tưởng sản phẩm mới cần scope riêng sau acceptance.
