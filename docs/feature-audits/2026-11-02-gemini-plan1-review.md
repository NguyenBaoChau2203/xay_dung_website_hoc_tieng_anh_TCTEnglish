# Review Gemini Plan1 — 02/11/2026

Nhánh `feature/chau-ui-performance-audit`, HEAD210bf2d; review dirty diff22 UI
files và bug-fix-log/ledger. Một agent review, không sửa runtime, không commit/
push/CI/merge/deploy. Owner đã cho tạo/thao tác fixture local; user thật giữ nguyên.

## Kết luận

**Chưa nên chốt Plan1 hoặc chuyển sang Plan2.** Có5finding tái hiện được:
1P1 (gửi AI lặp),4P2 (avatar preview, chat bị cắt ở viewport thấp, keyboard/ARIA
sidebar, Admin skip link bị che). Phần cải thiện chính đúng hướng và build/full suite xanh; các tests
hiện có chưa bao phủ những tương tác browser này.

Không coi L01/L02/L03/L11 của audit gốc là regression Gemini: backend không đổi.
R1 bên dưới là submit bypass mới từ Enter handler, khác L11 Stop-before-Started.

## Những phần đã kiểm chứng cải thiện

- Auth390: client/scroll đều390, hero163px,5 input login/register đều có label,
  một main landmark. Fields/actions/antiforgery vẫn có.
- Profile390: account-main382px, một main, bỏ75%/40% giả; fallback initials hiện.
- AI Chat390×844: log khoảng471px, metrics/history disclosure và multiline composer
  có trong source/live DOM; chưa đủ để kết luận mọi chiều cao/mobile keyboard tốt.
- Semantic buttons/lang/skip links, labels Admin/Settings đã thêm. Main/sidebar
  click/Escape có phản ứng; remaining keyboard-state gap được tách ởR4.
- Smoke browser authenticated Home/Index, Vocabulary/Index, Home/Class, Home/Folder
  render được shell mới. Lần điều hướng /Vocabulary không phải đường canonical
  không được dùng làm pass; canonical /Vocabulary/Index đã kiểm lại.
- Không diff Controllers/Services/Models/Program/settings/csproj/migrations;
  không schema change cần EF migration trong Plan1.

## R1 — P1: Enter gửi thêm request khi request trước đang chờ

**Files:** `wwwroot/js/ai-chat.js:132–142`, submit handler212–236, setUiState767+.

Keydown mới gọi `elForm.requestSubmit()` vô điều kiện. Khóa sendBtn không ngăn
requestSubmit không submitter; submit handler không guard in-flight trước append/
clear input/gửi HTTP. Native Enter trên textarea đã tạo đường đi vượt nút disabled.

**Repro browser:** account fixture mới/conversationId rỗng. Chặn riêng fetch
`/AI/Chat/Send` bằng promise giữ chờ; không request nào tới backend/OpenAI.
Nhập tin1/Enter → intercepted1, buttonDisabled=true, state=sending. Nhập tin2/
Enter → intercepted2; cả2 call đều xảy ra khi buttonDisabled=true. Với draft,
hai request có thể tạo2conversation/lượt provider và mất nội dung đang soạn.

**Sửa nhỏ nhất:** guard đồng bộ tại submit handler dùng một state in-flight chung
cho Enter/click/quick action/HTTP/SignalR; reject trước mọi thay đổi draft/DOM.
Keyboard handler cũng tôn trọng pending. Unlock trong đúng finally của lượt đang
chạy; không biến completion lượt1 thành unlock lượt2. Regression giữ promise và
assert1call, giữ draft tin2; sau failure/success mới được gửi tiếp. Không chỉ chặn
keydown vì direct requestSubmit vẫn có đường khác.

## R2 — P2: avatar mới chọn không hiện sau fallback

**Files:** `Views/Account/Profile.cshtml:100–106`, `previewImage:545–551`.

No-avatar render img display:none; error fallback cũng set display:none. Preview
FileReader chỉ đổi src, không chuyển lại display và không ẩn initials.

**Repro browser:** account chưa có avatar; gắn File PNG1px vào input qua
DataTransfer và dispatch change. didLoad=true,naturalWidth=1,src=data:image nhưng
computed imageDisplay=none,imageWidth=0,fallbackDisplay=flex. Không upload/save.

**Sửa:** state preview success/error rõ; valid loaded selection hiện img/ẩn
fallback, lỗi hiện initials; giữ file input/binding/server upload policy. Test
no-avatar, saved URL lỗi→chọn mới, repeated select/cancel; không gọi backend thật.

## R3 — P2: log Chat bị cắt ở viewport thấp dù đã scroll hết

**Files:** `wwwroot/css/ai-chat.css:799–801` và860+; parent wrapper346+.

Min-height220px mới áp vào log bên trong wrapper có min-height0 và overflow:hidden.
Page/shell dùng chiều cao dvh giới hạn. Khi vùng còn lại thấp hơn220px, log cao
hơn vùng clip; phần cuối nội dung không thể đưa vào vùng nhìn bằng scroll log.

**Repro:** mobile emulation390×500 (chiều cao thấp mô phỏng, không keyboard vật
lý). Wrapper127.25px/bottom427.02, log clientHeight218. Thêm20 dòng DOM fixture,
scrollBehavior auto, scrollTop=maxScroll=406; last line top489/bottom510 vẫn nằm
dưới clip. Default390×844 log471px tốt không phủ nhận lỗi này.

**Sửa:** layout co theo available height hoặc outer scrolling hợp lý; không min
cứng trên clipped child. Target>=220px tại390×844 khi đủ chỗ, nhưng chiều cao thấp
phải đọc được dòng cuối và dùng composer. Test full/embed,390×500/390×844/landscape,
metrics/history mở và textarea grow; không dùng overflow hidden để che lỗi.

## R4 — P2: sidebar đóng vẫn nhận Tab; ARIA mới chưa đồng bộ

**Files:** `wwwroot/js/layout.js:26–51`; Profile/Settings mobile toggle và script;
Admin `_AdminLayout.cshtml` toggle script.

Đây là requirement UI2/UI3/UI6 chưa hoàn tất, không khẳng định là regression mới
của mọi behavior cũ. Main sidebar chỉ transform ra ngoài viewport, không inert/
ẩn khỏi focus. Keyboard Tab đi vào các links không nhìn thấy. Account/Admin
toggle có aria-expanded=false mới thêm nhưng handler chưa cập nhật.

**Repro native Tab ở Goals390:** menu trigger→logo→search→bell→avatar→link trong
sidebar đóng (bounds left=-226/right=-15)→coursesDropdownBtn cũng ngoài màn hình.
Escape đóng sidebar và trả trigger hoạt động, nhưng không loại links khỏi Tab.
Profile click mobile toggle → class open=true, aria-expanded vẫn false.

**Sửa:** một hàm sync state mỗi loại sidebar, mobile closed inert (hoặc tương
đương), expanded/controls/focus đúng. Khi chuyển breakpoint desktop phải gỡ inert
đúng; close khi focus trong panel trả focus trước khi inert. Admin/Account actual
open/close/overlay/Escape đồng bộ, tránh blanket focus trap cho dropdown nhỏ.

## R5 — P2: skip link Admin bị navbar che trên mobile

**File:** `Areas/Admin/Views/Shared/_AdminLayout.cshtml:66`.

Link mới dùng `position-absolute ... z-3`, trong khi navbar `sticky-top` có lớp
cao hơn. Focus link ở viewport390×844: rect x8/y8/w221.25/h38, document.activeElement
đúng link nhưng elementFromPoint tại tâm trả NAV navbar thay vì link. Desktop1440
test tương tự không bị che; đây là lỗi mobile, không gán cho mọi breakpoint.

**Sửa:** focused skip link có vị trí/layer cao hơn navbar/overlay, giữ href/mainId;
Tab nhìn thấy được, Enter chuyển tới main, không bị cắt khi drawer mở. Không
tăng z-index toàn bộ links hoặc xóa sticky navbar để che lỗi.

## Verification và độ tin cậy của ledger

Gemini ghi101 tests bằng `--no-build` không -c, sau khi chỉ build app Release
ra `.tmp/build-verify`. Khi review, Debug TCTEnglish.dll timestamp31/10 08:15,
Tests.dll31/10 08:40, trong khi Razor đổi02/11 15:03–15:16. Factory hiện còn
Testing:DisableRuntimeCompilation=true. Vì vậy101pass đó không chứng minh compiled
views mới. Reviewer đã bù bằng full build/test fresh sau đây.

| Check lần review | Actual |
| --- | --- |
| Full fresh project build+tests |811 passed,0failed,8skipped,total819;3m08s|
| Command |dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Plan1Review -p:Optimize=true --logger trx;LogFileName=plan1-review.trx --results-directory .tmp/plan1-review --verbosity quiet|
| Configuration isolation |App/test bins ở bin/Plan1Review/net9.0; không đụng process Debug5100/Release5112|
| Compiled timestamps |TCTEnglish.dll02/11 15:42:24, Tests.dll15:42:38, mới hơn diff Razor|
| TRX |.tmp/plan1-review/plan1-review.trx (ignored)|
| Skips |2cloud live,3ASR/media live,3MariaDB opt-in; không gọi tất cả acceptance pass|
| Browser findings |R1–R5 tái hiện như trên; stub/DOM data không gọi paid provider|
| git diff --check |pass|
| GitHub gate |PLESK_DEPLOYMENT_ENABLED=false; không chạy remote stages|

Lệnh solution -c Plan1Review ban đầu không hợp lệ vì .sln chỉ khai báo Debug/
Release; đã đổi sang test project cùng config, không sửa solution hoặc tests
để né lỗi. Fresh build có warnings (phần lớn có sẵn, cùng nullable helper mới
ở_layout), không dùng incremental '0warnings' để nói codebase không warning.

Ledger còn UI5–UI7=PENDING ở bảng đầu nhưng cuối 'tất cả gates đạt'; IDs U04/
U06/U07/U10 không còn khớp audit gốc. U10 gốc là AccessDenied404 thuộc Plan2,
không phải nested-main để đóng. BE6 không yêu cầu tự xây email outbox/OAuth schema;
hai mục đó nằm ngoài scope theo Plan2 hiện hành. Cần sửa bản bàn giao, giữ evidence
lịch sử phân biệt với kết quả reviewer mới và không sửa tests/guard để đạt xanh.
Matrix còn đổi SP03 từ Study/Speaking thành SpeakingManage, WR02/WR03 từ
WritingExercises/WritingPractice thành Practice/History; phải liệt kê route/view
thật đã kiểm, không dùng nhãn sai để tuyên bố25view đã nghiệm thu.

## Handoff

- [Repair plan](../superpowers/plans/2026-11-02-plan1-ui-review-repair.md)
- [Gemini repair prompt](../../.ai/prompts/gemini-plan1-ui-review-repair.md)
- Chỉ sửa R1–R5 và đồng bộ evidence; không làm lại toàn Plan1/khởi động Plan2.
- Review lần này không sửa source của Gemini. Synthetic account review id62 đã
  xóa bằng own DeleteAccount sau identity guard; redirect Login200, credential
  sessionStorage đã xóa. Không thao tác tài khoản thật hoặc hosting.
