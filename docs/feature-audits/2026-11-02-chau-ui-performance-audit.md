# Khảo sát UI, logic và hiệu năng — phạm vi Châu — 02/11/2026

Baseline: master `210bf2de12051db6238d4f53214aba4b73c3ce29` (PR123).
Nhánh khảo sát: `feature/chau-ui-performance-audit`. Một agent điều tra inline.
Owner yêu cầu phân tích + hai plan/prompt, chưa yêu cầu thực thi sửa ứng dụng.
Không commit/push/CI/merge/deploy. Plesk gate giữ false. File BTH2 chưa track giữ nguyên.

## Phạm vi, phương pháp và bằng chứng

- Phân công: README56–86; Account/Security/SMTP, Speaking, Writing, Goals/Badges,
  AI Chatbot, Admin Dashboard/users/Speaking/Writing/AI, hạ tầng dùng chung.
- Dùng tct-engineering, tct-superpowers, tct-ui-review, tct-ui-component,
  tct-ui-ux-pro-max; checklist speaking/admin/security và ASP.NET Core.
  Đọc DESIGN.md; lookup UX local: form feedback, labels, keyboard/skip links.
- CodeGraph trước cho controller/service; đọc có giới hạn Razor/CSS/JS khi graph
  không trả source hoặc bỏ khoảng cần kiểm. Không rebuild/index CodeGraph.
- Khởi động bằng launcher chuẩn. Cổng5100 bận nên giữ nguyên và mở Release5112.
  Docker DB3307 healthy; không migrations mới; bootstrap giữ existing users/data.
  Whisper tiny.en8092 inference ready; nguồn key User Secrets, Luna low.
  Không gọi OpenAI trả phí, SMTP thật hoặc import YouTube trong khảo sát.
- Browser Chrome: Login desktop1440, Auth mobile emulation390×844,
  Register/ForgotPassword/ResetPassword mobile. Resize cửa sổ có sàn500px:
  đã chuyển sang device emulation; không tính ảnh500px là nghiệm thu390px.
- HTTP authenticated bằng account local có sẵn:8 routes ×5 GET, không lưu/sửa
  hồ sơ hoặc dữ liệu học. Cookie/password không in log/tài liệu. HTML/private
  evidence chỉ ignored `.tmp/chau-audit-20261102/`.
- Sau đó owner cho phép tự tạo account/test local. Đã đăng ký synthetic account
  qua endpoint thật, login Standard, dùng admin local có sẵn promote CHỈ account
  synthetic qua endpoint Admin/Edit và re-login. Không in password. Browser
  private session giữ credential tạm trong sessionStorage, xóa khi cleanup.
- Browser authenticated mobile390: Profile/Settings/Goals/Speaking/Writing list,
  Practice Speaking44/Writing1, AI Chat; Admin Dashboard/users/Speaking list/create,
  Writing list/create/AI management. Desktop1440: Profile/Settings/Speaking/AI Chat.
  Đây là loaded/DOM/layout checks, không toàn bộ submit/25view/error/mic journeys.
- Đây là khảo sát để lập plan, không phải chứng nhận accessibility, tải lớn,
  bảo mật đầy đủ hoặc production readiness. Không gán điểm Lighthouse giả.

## Baseline đo thực tế

HTTP client loopback, Release, dữ liệu local hiện có, một lượt đầu +4 lượt warm
mỗi route; đơn vị ms. Median warm của4 mẫu, KHÔNG phải p95. Bao gồm render HTML,
không gồm tải assets/JS browser. App5100 cũ, Docker/Whisper cùng tồn tại; không
kiểm soát tải nền. Không dùng số này để suy ra năng lực hosting.

| GET | Status đúng route | Đầu | Median warm | Max warm | HTML UTF8 bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| /Account/Profile |200|692.4|59.6|76.3|43722|
| /Account/Settings |200|66.3|17.6|20.7|37399|
| /Goals |200|188.0|43.6|88.4|43532|
| /Speaking |200|282.7|80.0|101.9|53292|
| /Home/Writing/Exercises |200|262.7|38.8|51.5|37925|
| /AI/Chat |200|101.3|11.3|13.6|27881|
| /Admin/Dashboard |200|156.5|20.8|23.6|17645|
| /Admin/UserManagement |200|113.5|20.6|27.4|95022|

Browser Register trace, cold HTTP cache, CPU1x/network không throttle:
LCP472ms, CLS0.00, TTFB6ms; một trace lab, không INP/field data. DevTools chỉ ra
render-blocking/third-party/font chain; estimated savings là dự đoán công cụ,
không phải mức cải thiện đã đo. ForgotPassword mobile: TTFB36ms/load79ms;
ResetPassword GET không token:51/84ms. Không kiểm reset hợp lệ bằng GET rỗng.

Snapshot idle: Docker DB90.34MiB; audit web PID10876 WS194MiB;
Whisper PID6216 WS163.7MiB, private654.5MiB. WS khác private/commit; đây không
phải peak hoặc benchmark tải. Không áp resource limit/đổi model theo snapshot.

## Findings UI

Các dòng Source là điều kiện có trong source/HTML; Browser là đã quan sát DOM
hoặc ảnh. P1 cần sửa trước nghiệm thu; P2 cải thiện có phạm vi; A là việc nghiệm
thu còn thiếu, không tự coi thành bug.

| ID | Mức / bằng chứng | Trigger, ảnh hưởng và file | Hướng sửa / plan |
| --- | --- | --- | --- |
| U01 |P1 Browser+Source|Auth.cshtml156–203: labels không có for/wrapping; login-email/password và3 field đăng ký không có accessible name từ label. Login email type=text. Browser Settings:4fields thiếu associated label,3 toggle thiếu tên/tabindex=-1; Admin sidebarToggle không tên và Writing Create IsPublished checkbox thiếu label.|Labels/id/autocomplete/input type, toggle có tên và bàn phím; UI1/UI3/UI6.|
| U02 |P2 Browser+Source|Auth mobile390: card x=-3,width396,right393, document client390/scroll393. Hero chiếm khoảng481px; Register submit top1154px. auth-stitch.css319 và responsive745+. Không áp overflow-x:hidden để che.|Sửa box sizing/min-width/flex; compact hero mobile, giữ form dễ tiếp cận; UI1.|
| U03 |P1 Source+authenticated HTML|Profile.cshtml235+ hiển thị Writing75%/Speaking40% hardcode. HTTP Profile vẫn có cả hai. Chưa phải tiến độ người dùng thật.|UI3 bỏ số giả, thay CTA/empty honest; BE6 chỉ nối dữ liệu nếu ViewModel có định nghĩa rõ.|
| U04 |P1 Source|_Layout.cshtml42/99/225 và layout.js: i/div click-only cho menu/avatar/sidebar dropdown; không role/tabindex/expanded/keyboard. Layout lang=en trong app tiếng Việt; chưa có skip link tới main id.|UI2 dùng semantic button + Escape/focus/expanded, lang=vi, skip main; không rewrite modal folder của đồng đội.|
| U05 |P1 Browser+Source|_ChatShell.cshtml43–68 đặt Requests/Error rate/Latency/Token trên màn học; messageInput184 là input một dòng. Mobile390×844 đo metrics299px, chatWindow25.78px, composer y744: nội dung chat gần như bị ép mất.|UI5 ưu tiên learner content, metrics disclosure, composer multiline giữ Enter/Shift+Enter và vùng chat dùng được; không đổi quota/API/model.|
| U06 |P2 Source|Profile và Settings lặp form bảo mật; Settings liên kết Google dựa PasswordHash prefix, Facebook luôn false từ AccountController512–517. Không được vẽ trạng thái liên kết chắc chắn nếu backend chưa biết.|UI3 tập trung settings, giữ endpoints; BE1/BE6 xử lý thông tin unknown; không tự xây schema OAuth mới.|
| U07 |A Source+limited Browser|Speaking/Writing đã có honest NoSpeech/NeedsReview/aria-live/receipt. 25-view desktop/mobile/modal/zoom/focus chưa đủ bằng chứng. Forgot/Reset đã có labels và style mới; không làm lại finding cũ đã sửa.|UI4/UI6/UI7 kiểm từng state, chỉ sửa lỗi còn tái hiện; không khôi phục điểm demo/auto-pass.|
| U08 |P1 Browser+CSS|Mobile390 Profile: outer main390 nhưng .account-main x264/width122, sidebar vẫn sticky trong flex flow. account.css1303+ khai báo position:sticky sau media680 position:fixed nên thắng cascade. Settings cùng CSS. Hai main lồng nhau ở account.|UI3 sửa cascade/responsive sidebar trong scope, một main landmark, không overflow-hidden che lỗi.|
| U09 |P2 Browser|Speaking390: client390/scroll417, topic chips/button right417; có blob decorative right410. Không coi sidebar offscreen cố ý là lỗi overflow.|UI4 min-width/flex/scroll containment, chỉ vùng chips scroll; clip decoration tại container.|
| U10 |P2 Browser+HTTP|Standard account GET /Admin/Dashboard bị chặn và redirect /Account/AccessDenied nhưng handler thiếu, kết quả404. Chưa thấy dữ liệu admin bị lộ.|BE1 thêm denial handler403/view; UI7 kiểm denied state, giữ IDOR404.|
| U11 |P2 Browser|Profile account mới không avatar render /images/default-avatar.png404. Trạng thái mới chưa có ảnh bị broken-image.|UI3 fallback icon/initials hoặc asset có thật, không thêm CDN/dependency.|

## Findings logic, hiệu năng, hạ tầng

| ID | Mức / bằng chứng | Source/trigger và hệ quả | Hướng / plan |
| --- | --- | --- | --- |
| L01 |P1 Source+primitive repro|AccountController115–122 gọi BCrypt.Verify trực tiếp; social account được tạo PasswordHash=SOCIAL_LOGIN_... ở245. Verify sentinel local bằng DLL thật ném BCrypt.Net.SaltParseException. Chưa tạo account để tái hiện HTTP500.|BE1 guard social/malformed hashes, generic failure, validate register/reset/settings; regressions HTTP với synthetic DB.|
| L02 |P1 Source path, cần regression|AiChatService113–120 lưu user message trước provider; catch185 chỉ bắt AiProviderException. OpenAiProviderClient94 rethrow caller cancellation. Hủy sau save có thể để message/draft mồ côi; chưa chạy fault injection ở khảo sát.|BE2 tái hiện cancel mới/cũ, cleanup bounded bằng token riêng, giữ exception và lease; không ghi success giả.|
| L03 |P1 risk concurrency, chưa repro|AiChatService72–80 đếm successful log trước call; daily token check148+ tương tự. Guard102/269 theo conversation, không theo user/ngày. Hai conversation cùng user có thể tranh quota.|BE2 regression barrier fake provider; chọn cơ chế reservation có authority rõ nếu tái hiện. Không gọi đây là breach production đã xảy ra.|
| L04 |P2 Source scalability|SpeakingService27–126 tải toàn bộ public videos, lặp video trong playlist, toàn bộ my videos. speaking.js filter/page client. Dataset nhỏ hiện80ms median không chứng minh query chậm.|BE4 thiết kế server paging/filter projection, giữ private ownership/locked metadata; đo SQL và payload scale.|
| L05 |P2 Source scalability|WritingService402/530 tải cả catalog; paging490–502 thực hiện sau materialize. Metadata550–572 tải attempt feedback đầy đủ1019–1049 rồi lặp Where/First theo exercise; recent history cuối mới Take5.|BE4 SQL aggregates/latest attempt + paging trước materialize; không mất lịch sử/Accepted semantics/status filters.|
| L06 |P2 Source scalability|AiConversationService59/89 tải toàn bộ conversations/messages; AiChatService116–120 tải toàn history trước context builder cắt token. Token cap đang có, không giới hạn DB rows/HTML.|BE3 paged history/messages, bounded context traversal giữ token/role rules. Không Take tùy tiện làm mất hội thoại mà không có Load older.|
| L07 |P2 Source cost candidate|AccountController437 lấy toàn GetGoalsAsync chỉ cần badges; _AiChatLauncher partial gọi GetDailyUsageAsync mỗi render. Chưa đo query count/EXPLAIN nên chưa kết luận bottleneck.|BE0/BE6 đo rồi dùng projection riêng/request-scope reuse nếu có lợi; không cache private HTML dùng chung.|
| L08 |P2 Source operational|SmtpAppEmailSender134–188 thiếu deadline/cancel theo app;3 wrappers Task bỏ bool failure. Block/Unlock save trước await mail; AutoUnlockWorker gửi mail trước SaveChanges và disabled mặc định.|BE5 deadline/result semantics, fake transport, worker retry/data validation. Không Task.Run fire-and-forget, không tự bật worker trên hosting.|
| L09 |P2 Source supply/perf|Auth head SweetAlert2@11; Chat.cshtml marked không version; layout nhiều font/icon CDN, auth-stitch @import lại tct-learning-ui vốn đã link. Pin/critical chain/cache cần kiểm với browser.|BE7 pin artifact hiện tại đã xác minh, không chọn version tùy đoán; UI giữ font identity. Không nâng package nền tảng tùy tiện.|
| L10 |A Infrastructure|Dev Docker/Whisper ready; hợp đồng setup Windows x64. Chưa clean teammate physical PC, human mic/accent/load, full MariaDB competing import/outbox matrix hoặc staging recovery. Shared hosting N518 chưa chứng minh chạy ASR/FFmpeg service lâu dài.|BE8 acceptance + runbook topology decision; không deploy/enable gate.|
| L11 |P1 Source path, cần regression|AiStreamingService70–79 await toàn provider trước trả session; ClassChatHub246–256 chỉ gửi AiStreamStarted sau đó. ai-chat.js132–140 Stop return nếu chưa activeStreamId. Stop trong lúc provider chờ không có handle. Hub scheduling/fallback retry có thể làm vấn đề nặng hơn; chưa chạy fake-provider integration.|BE2 test Stop-before-started, request identity/cancel handle/scheduling và fallback dedup; không gọi chunk-after-generation là provider token streaming.|

Browser Goals modal mở được và có đủ5area, Escape đóng được. Focus sau Escape
ở BODY trong probe programmatic-click; không dùng kết quả này để kết luận focus
return của native keyboard đã lỗi. SpeakingPractice44 HTML231064bytes là case
thực, chưa phải toàn bộ ảnh/network hoặc benchmark scale. Không bấm mic/AI/Train.

## Những phần đã có, không được làm lại theo backlog cũ

- Goals hỗ trợ cả5 areas; không tắt Reading/Listening theo tài liệu deferred cũ.
- Không điểm Speaking ngẫu nhiên; server receipt và credit transaction phải giữ.
- Writing local exact/NeedsReview, optional AI feedback không làm verdict giả.
- Whisper pin/lifecycle/queue, OpenAI Luna low, loopback DB và key source đã có.
- MariaDB race/batch/expired lease từng chạy31/10; skip ở suite mới nhất không
  chứng minh chưa bao giờ chạy. Broader matrix vẫn cần thêm.
- CI master/source đã merge; release skipped/gate false là chủ ý owner.

## Hai gói thực thi

1. [Plan1 UI cho Gemini3.8 Flash High](../superpowers/plans/2026-11-02-chau-ui-improvement-plan.md)
   và [prompt](../../.ai/prompts/gemini-chau-ui-improvement.md).
2. [Plan2 logic/performance cho Cursor Auto](../superpowers/plans/2026-11-02-chau-logic-performance-plan.md)
   và [prompt](../../.ai/prompts/cursor-auto-chau-logic-performance.md).

Thứ tự: UI → review → backend/performance → review → acceptance; không chạy hai
agent đồng thời. P1 security/logic trong Plan2 cần xong trước bất kỳ release nào.
Mỗi lượt triển khai đọc source mới/dirty diff và chỉ load batch hiện hành.

## Tài liệu kỹ thuật tham chiếu

- [Microsoft EF efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying): projection, result limits, query plan; áp trên Pomelo/MariaDB, không chép SQL Server SQL.
- [EF DbContext lifetime/thread safety](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues): không Task.WhenAll trên cùng scoped DbContext.
- DESIGN.md và local skill adapters là hướng dẫn giao diện hiện hành; audit29/10
  chỉ là lịch sử. Trang ASP.NET performance9 không tải được qua web tool;
  dùng skill local và hai trang Microsoft đọc được, không bịa kết quả tài liệu.

## Chốt phiên khảo sát

- Account synthetic id61 đã được xóa bằng chính Account/DeleteAccount sau khi
  kiểm tra Profile session đúng fixture; response redirect Login200. Credential
  tạm trong browser sessionStorage đã xóa. Không xóa tài khoản/dữ liệu có trước.
- Chỉ thêm5 Markdown files (audit,2plans,2prompts); source app/schema không sửa.
  Launcher build succeeded, health200, migration update không có migration mới.
  Không chạy lại full regression suite vì đây là khảo sát/tài liệu.
- Validator5files: UTF8/fences/trailing whitespace và6relative links hợp lệ;
  git diff --check sạch. Evidence private nằm trong ignored .tmp.
- GitHub variable kiểm lại khi chốt: PLESK_DEPLOYMENT_ENABLED=false. Không push,
  không CI run mới hoặc deploy trong phiên này. Chưa commit5tài liệu mới.
