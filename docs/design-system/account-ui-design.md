**Approved — owner reviewed on 2026-11-08**

> **Visual update from Chau, later on 08/11/2026:** every redesigned page must
> follow Claymorphism and prioritize laptop/desktop demo quality. The approved
> option B, form/security contracts and information architecture below remain.
> Earlier flat shadow/shape choices and screenshots are superseded by the current
> Clay section of DESIGN.md and the shared component gallery. Revalidate the
> actual Account pages before claiming visual completion; this notice alone does
> not certify migration or resolve the separate functional review findings.

# Account UI — đặc tả để triển khai

Ngày 2026-11-08, source `eed4e2c`, nhánh `feature/chau-ui-design-system`.
Châu đã duyệt phương án B cho AU01–AU07 bằng tin nhắn “Duyệt” trong chat ngày
2026-11-08 (Asia/Bangkok). Stage D hoàn tất; Stage I chờ Châu khởi chạy prompt
triển khai Gemini. Phê duyệt này không mở rộng phạm vi backend/global hoặc quyền
Git/CI/release. [Nghiên cứu/bằng chứng](../ui-audits/2026-11-08-account-ui-research.md),
[preview tương tác tĩnh](account-ui-preview.html),
[checklist English cho Gemini](../superpowers/plans/2026-11-08-account-ui-implementation.md).

## 1. Mục tiêu và phương án đề xuất

Giúp học viên vào tài khoản, phục hồi truy cập và quản lý thông tin mà hiểu rõ
điều gì đã lưu, điều gì chỉ là thông tin. Giữ routing, policies, HTTP/JSON,
antiforgery, role, các đường vào và shell của hệ thống hiện tại.

| Phương án | Thực hiện | Lợi ích | Đánh đổi |
| --- | --- | --- | --- |
| A — chuyển token vừa đủ | Đổi CSS/control, giữ gần như toàn bộ grid/card/rail hiện tại | Ít dịch chuyển markup; ảnh before/after dễ so | Auth mobile còn nhiều phần trang trí; Profile security trùng layout; Settings vẫn nhiều rail; khó ưu tiên task |
| **B — sắp xếp lại nội dung trong từng trang, đề xuất** | Auth form trước trên mobile; recovery cùng composition; Profile identity/form/earned badges; Settings section nav gọn | Rõ tác vụ và trạng thái; giải quyết copy/keyboard/khả dụng trong page scope | Cần chuyển handler có kiểm soát và kiểm tra nhiều state; không giải quyết được thiếu backend data |

Phương án B được đề xuất ở Stage D và đã được Châu duyệt ngày 2026-11-08.
Không thay brand, root font, global nav, role, password policy hay
logic subscription. Không thêm MFA, preference API hay auth method.

## 2. Nền tảng, kích thước và cấu trúc chung

### Assets và phạm vi

- Nguồn màu/font/component duy nhất: `tct-learning-ui.css` và DESIGN.md. Font
  Inter 400/500/600/700; icon Font Awesome có sẵn; không 800/900/hex/palette riêng.
- Standalone AU01–04: `body.tct-theme.account-auth-ui`, Bootstrap đúng version
  đang dùng ở AU01 (5.3.3), Inter/FA URL đã có trong app, shared CSS rồi
  `account-auth-ui.css`; cuối document Bootstrap bundle, validation cần thiết,
  `account-auth-ui.js` một lần. Recovery có thể dùng cùng assets này; không thêm CDN.
- AU05/06: giữ `_Layout`, HideSidebar=true. Dùng wrapper
  `.tct-theme.tct-page.account-ui`; shell load order giữ nguyên rồi page
  `account-ui.css`. Bỏ link account.css/Be Vietnam Pro/Material Symbols dư tại
  **hai view**, không sửa những file đó. Không còn `.account-dashboard` gây
  legacy full-bleed margin. Header/footer/AI launcher vẫn do shell sở hữu.
- AU07: `_Layout` và learner sidebar hiện tại; `.tct-theme.tct-page.account-ui`
  + `account-ui.css`. Không bật HideSidebar cho AU07.
- Preview tự chứa style của prototype, nạp local Bootstrap 5.3.3, shared CSS,
  `site.css`/`layout.css` thật để kiểm tra cascade và chrome minh họa. Đây không
  phải render Razor; learner app đang nạp Bootstrap 5.3.2 vẫn cần kiểm tra sau.

Thứ tự assets khi triển khai đã chốt:

| Nhóm | CSS / font | Scripts |
| --- | --- | --- |
| AU01 | Inter 400/500/600/700 cùng URL shell hiện tại; giữ FA 6.4.0 và Bootstrap 5.3.3 URL/SRI hiện tại; shared CSS → account-auth-ui.css | Giữ SweetAlert2 URL hiện có cho warning terms cũ, không nâng version/thêm consumer; Bootstrap bundle hiện tại → account-auth-ui.js cuối body. Success dùng feedback bền, không cần Swal success mới |
| AU02–04 | Cùng Inter, giữ FA 6.4.0 hiện tại; reuse Bootstrap 5.3.3 đã có ở Auth → shared CSS → account-auth-ui.css | Bootstrap bundle 5.3.3 → account-auth-ui.js; không thêm jQuery vào standalone. Native required/type=email; JS chỉ bổ sung minlength6/Compare tương ứng server và nối inline error |
| AU05/06 | Toàn bộ shell load order nguyên vẹn → account-ui.css trong Styles | Giữ `_ValidationScriptsPartial` tại Scripts → account-ui.js một lần; không nạp thêm Bootstrap vì shell đã có5.3.2 |
| AU07 | Shell nguyên vẹn → account-ui.css | Không có script Account mới |

Warning terms hiện có, nếu gọi qua callback compatibility, giữ nội dung và dùng
màu từ computed token, không chép hex vào JS. Không dùng Swal cho các form recovery.
Trong standalone, reset ConfirmPassword chỉ được so với NewPassword khi có dữ
liệu/đã thử submit; xóa custom validity khi sửa field, vẫn để server kiểm tra lại.
Không thay policy bằng rule từ thư viện khác. File preview dùng FA6.0.0 của shell
và local Bootstrap5.3.3; khác biệt version này là giới hạn prototype đã biết.

### Layout đã chốt cho bản nháp

| Thành phần | 1440 | 768 | 375–390 | Chiều cao ngắn / zoom |
| --- | --- | --- | --- | --- |
| Auth | Max 70rem, panel thương hiệu 5/12 + form 7/12; form <=30rem | Một cột <=30rem; brand gọn | Padding 1rem, card padding 1.5rem; social có thể xếp dọc | Không fixed height/overflow hidden; nội dung bắt đầu từ trên và cuộn document |
| Recovery | Một cột <=30rem; card padding 2rem, brand phía trên | Cùng composition | Card 1.5rem, title wrap | min-height:100dvh, không center gây mất đầu form khi nội dung cao |
| Account | Bên trong `.main` hiện có; grid nav 15rem + gap token xl + content minmax(0,1fr); max token container | Account nav thành drawer <=992; content 100% | Nút “Menu tài khoản” trong flow trước heading; content 1 cột | Sidebar scroll riêng khi mở; nội dung cuộn document; không CTA fixed che keyboard |
| Profile content | Identity ngang; form 2fr + membership/goals 1fr từ >=1200; badges dưới | Form rồi membership/goals rồi badges, một cột | Avatar/name wrap; buttons full-width nếu cần | Không min-height card; tên/email không ellipsis |
| Settings | Heading, section nav wrap ngang, security, linked, notifications, danger | Cùng thứ tự, password một cột | Mọi row đổi thành stack; controls dài không co dưới min-width | Section nav không fixed trên mobile; scroll-margin từ header token + space |
| AccessDenied | Nội dung <=container-narrow trong shell; một khối text/hành động | Shell drawer do layout quản lý | Hai CTA stack | Không full-screen illustration, không timer/back-history phụ thuộc nguồn ngoài |

Kích thước grid là quyết định page-level, không token mới. Không sửa `.main`,
`:root`, html font-size hoặc offset header chung. Header đo trước cao hơn token
72px là vấn đề nền tảng đã biết; thêm khoảng thở trong content không chứng nhận
đã sửa header. Với short viewport ưu tiên văn bản, form và thao tác tiếp cận được.

### Mapping component/token

| Mục | Reuse / token |
| --- | --- |
| Heading | `_PageHeader` / PageHeaderViewModel, đúng 1 h1; `--tct-title-size`, weight-bold |
| Form/card | `.tct-card`, Bootstrap `.form-label/.form-control/.form-text/.input-group`; `--tct-surface`, border-control, radius-md/lg, space-md/lg/xl |
| CTA | `.btn.btn-primary`, secondary `.btn-outline-primary`; destructive `.btn-danger`; min touch height `--tct-target-min` |
| Hiện/ẩn password, avatar edit, close | `.tct-btn-icon`, accessible label riêng, 44×44; input-group không đè text |
| Error/success | `.alert` cho ngữ cảnh form; `_StateMessage` cho result/empty; Error=alert, Success/Empty=status; danger-text/success-text + semantic-soft |
| Empty badges | `_StateMessage` Kind=Empty, ActionUrl=/Goals; không chart/skeleton giả |
| Role/link badge | `.tct-badge--primary/--warning/--success` + text; status dài dùng wrap tại `.account-ui .tct-badge` |
| Nav | Bootstrap list/nav semantics, active primary-tint/primary-strong; border chỉ trang trí; min-width:0 |
| Focus/motion | Shared focus ring; transition token dur-fast/dur, reduced-motion không animation bắt buộc |

Không thiếu shared token/component để triển khai. Sidebar mobile dùng Bootstrap
offcanvas behavior với adapter giữ contract (mục 4); backdrop theo Bootstrap có
sẵn, không đề xuất màu opacity riêng. Không tạo design system thứ hai.

## 3. Hành vi chung của form và feedback

1. Giữ label visible và for/id; help/error `aria-describedby`. Không hiển thị
   lỗi đỏ trước lần submit. Native constraint/server validation vẫn hoạt động;
   không thêm novalidate toàn bộ. Required phải có copy/chỉ dẫn, không chỉ dấu sao.
2. Khi submit hợp lệ: guard đồng bộ theo **form**, không chỉ button; disabled
   submit + aria-busy=true + spinner/text đúng hành động. Không disable input
   bound trước khi browser tạo payload. Không để Enter/requestSubmit gửi trùng.
3. AJAX login: nhận response thực mới đi Home; lỗi JSON/network khôi phục nút,
   giữ email và text không nhạy cảm; không log body/password/token. Lỗi generic
   giữ “Email hoặc mật khẩu không đúng.”; không chỉ lỗi riêng email tồn tại.
4. Register/recovery/profile/settings/delete giữ **native POST/navigation**.
   Không chuyển thành API/fetch để giả toast. On invalid/unobtrusive validation
   không latch pending; pageshow phục hồi pending khi back/bfcache. Mất mạng sau
   navigation có thể thành trang lỗi browser: không hứa custom error mà server
   chưa hỗ trợ. Quay lại form phải retry được. Không setTimeout quyết định success.
5. Error summary tabindex=-1, có link đến field khi biết field; focus summary sau
   server error. Inline validation và token summary AU04 vẫn render. Lỗi AJAX
   focus `#login-error-msg`; Escape không xóa lỗi/form. Success polite, không
   tự dismiss sau 5s; cho nút “Đóng thông báo” 44px. Không toast “Đã lưu” trước PRG.
6. Password hide mặc định; show/hide không submit, `aria-pressed`, `aria-controls`
   và tên riêng từng field. Paste/password manager/autofill được phép. Trước
   submit chuyển type về password, không thay giá trị. Không lưu password vào
   URL, local/session storage hay draft. Không thêm confirm field cho Register;
   giữ confirm hiện có ở AU04/AU05/AU06.
7. Refresh native server error có thể mất input vì controller hiện tại không
   round-trip các giá trị. Không hứa giữ email/fullName sau Register server
   return View("Auth") hoặc PRG Settings/Profile nếu source không có dữ liệu đó.
   Trong cùng DOM lỗi AJAX/client-side giữ field; backend round-trip là task riêng.

## 4. Account navigation, deep links và ownership JavaScript

### Navigation

AU05 giữ `#accountSidebar`, `#accountSidebarToggle`, icon ID, mobile toggle,
overlay, `.sidebar-nav-item[data-tab=profile|security|subscription]` và
`#tab-profile/#tab-security/#tab-subscription`. Query `?tab=` chọn pane hợp lệ;
không có/không nhận diện chọn profile. Click cập nhật `history.replaceState`
như hiện tại, giữ query khác và không thêm history entry. Sau chọn pane trong
mobile, đóng drawer rồi focus heading pane tabindex=-1. Một h1 page-level
luôn ở ngoài cả ba panes. Pane ẩn dùng hidden để không lọt Tab/AT.

AU06 giữ links `/Account/Profile`, `/Account/Settings`,
`/Account/Profile?tab=subscription`, Logout. Nav mục con giữ các ID/data-section
nhưng theo thứ tự DOM: security → linked → notifications → danger. Scroll tới
`#section-*`, focus heading tương ứng; active không dựa màu riêng. Hash hiện
tại không có parser ở source; không giả đây là deep-link feature sẵn có.

### Drawer và collapse

- >992px: sidebar nằm trong grid, inert=false; collapse vẫn dùng
  `accountSidebarCollapsed`, `.sidebar-collapsed`, icon rotate và aria-expanded.
  Icon rail dùng 44px target, accessible name/tooltip text; text không bị xóa
  khỏi accessible name. Storage read/write try/catch, không lưu dữ liệu cá nhân.
- <=992px: dùng Bootstrap Offcanvas trên **cùng aside `#accountSidebar`**, chỉ
  adapter page quản lý instance (backdrop/scroll lock/focus trap/Escape). Thêm
  `.offcanvas-start` và class `.offcanvas` chỉ trong mobile mode; desktop dispose
  sau khi hidden, xóa inline offcanvas attrs/styles, giữ sidebar semantic nav.
  `#accountSidebarToggle` đóng khi mobile; mobile opener vẫn trong flow.
- Dùng `backdrop:false` trong instance và tái sử dụng `#sidebarOverlay` làm
  backdrop Bootstrap `.offcanvas-backdrop.fade`, click gọi hide; scroll=false
  và keyboard=true. Không có backdrop thứ hai. Bootstrap dùng z-index offcanvas
  1045/backdrop1040, nằm trên header1000. AI launcher1600 vẫn là shared limitation;
  adapter tạm inert chính launcher và shell header/footer khi mở, khôi phục giá
  trị inert cũ khi hidden/dispose; không sửa layout hay CSS toàn cục.
  Selectors tích hợp đã đối chiếu: `.header`, `.tct-footer`, `[data-ai-launcher]`.
  Chỉ đổi inert tạm thời, không đổi listener/HTML của shell; có smoke check
  header/footer/launcher sau đóng. Không gọi mở launcher hay tải iframe AI.
- shown.bs.offcanvas: `.open`, overlay `.show`, aria-expanded=true, inert=false,
  focus first account nav item. Ở hidden.bs.offcanvas, sau khi Bootstrap tắt
  focus trap, chuyển focus ra opener **trước** inert=true, xóa open/show,
  aria-expanded=false. Không focus opener trong hide event khi trap còn hoạt động.
  Giữ main content không inert
  nếu nó chứa drawer; Bootstrap focus trap giữ Tab trong drawer. Không custom
  document-wide Tab handler cạnh tranh focus trap.
- Breakpoint transition đóng/hoàn tất hidden trước dispose; bỏ collapsed rail
  khi mobile, giữ preference để phục hồi desktop. Closed drawer không có link
  focusable. Test Escape sau shown event, không trong transition.

### Handler files

`account-auth-ui.js` sở hữu Auth và recovery: một DOMContentLoaded init có
data-initialized guard, resolve bằng ID trong page wrapper. Giữ public callback
names `switchTab`, `togglePassword`, `handleLogin`, `handleRegister`,
`handleExternalLogin`, `showLoginError`, `showLoaderAndRedirect`,
`handleForgotSubmit`, `handleResetSubmit` nếu còn inline call; **chọn một đường
bind cho mỗi form**, không vừa inline vừa listener. Khuyến nghị chuyển bind vào
file và giữ thin window aliases cho hợp đồng cũ; các alias không bind lần hai.
Route/dữ liệu Razor truyền bằng `data-*`/JSON encoded, không interpolate JS string
thô. TempData blocked/success render encoded trong view, file chỉ khởi tạo modal.

`account-ui.js` sở hữu Account nav/tab/sidebar collapse, settings section nav,
avatar preview, `.toggle-pwd`, native pending, dismiss, delete modal guard. Chỉ
init khi `.account-ui` tương ứng hiện diện; không query `.sidebar-nav-item`
ngoài wrapper. Giữ `window.previewImage` alias khi `onchange` chưa dời. Avatar
input change chỉ có một listener. Profile và Settings không load cả inline cũ
lẫn JS mới. Shared `layout.js` vẫn sở hữu learner/global sidebar, không dùng
account IDs và không bị thay thế.

Delete modal dùng Bootstrap modal, không custom dialog. Reset confirm/nút disabled
ở mỗi show/hidden, focus input sau shown, Escape/Cancel trả trigger sau hidden;
pending chỉ khóa submit đúng form. Điều kiện hiện tại `value.trim()==='CONFIRM'`
giữ nguyên; đây là guard UI hiện có, không tự biến thành server authorization.

## 5. Đặc tả từng màn hình

### AU01 — Đăng nhập / đăng ký

**Task:** quay lại học hoặc tạo tài khoản. `Auth.cshtml`, `account-auth-ui.css/js`.
H1 nhìn thấy “Đăng nhập” / “Tạo tài khoản”; mô tả “Tiếp tục học với TCT English.” /
“Tạo tài khoản để bắt đầu học.”. Thứ tự: brand gọn → h1 → tabs → server feedback →
Google/Facebook → separator → form → link đổi tab. Desktop panel thương hiệu chỉ
copy về việc học, không số lượng/achievement. 768/390 bỏ panel trang trí, brand
một dòng; social 2 cột từ 576, 1 cột dưới 576; short-height bắt đầu ở trên.

Login: Email / Mật khẩu / “Quên mật khẩu?” / “Đăng nhập”. Register: Email / Tên
hiển thị / Mật khẩu / terms / “Đăng ký”. Hint “Ít nhất 6 ký tự theo yêu cầu hiện
tại.”; giữ nguyên terms copy và checkbox required không name, không bịa link
điều khoản chưa có route. Nút “Quay lại” giữ history.back contract, accessible
target44; không thêm external destination.

Tablist giữ selected/tabindex/controls/labelledby; Left/Right/Home/End đổi tab,
focus ở tab; Tab đi social rồi input pane active. Không autofocus input lúc load
mobile. Esc chỉ đóng blocked/success modal khi đang mở. Visibility label gồm
“Hiện mật khẩu đăng nhập” / “Hiện mật khẩu đăng ký”. Email autocomplete giữ email;
login password current-password, register new-password. Autofill/paste không chặn.

| State | Copy/behavior | Bằng chứng hiện tại |
| --- | --- | --- |
| Login/Register | Theo ActiveTab từ action, không tự suy route khi server validation fail | O+S |
| Client invalid | Visible label + inline hint, native invalid focus; chưa gửi network | S; P |
| AJAX generic error | “Email hoặc mật khẩu không đúng.”, focus error; có recovery link | S+T; P |
| Network/HTTP/parse fail | “Không thể kết nối. Vui lòng thử lại.”; restore controls, không lộ exception | S; P |
| Pending login/register | “Đang đăng nhập…” / “Đang đăng ký…”; duplicate guard | S; P |
| Success | Chỉ dựa JSON success/TempData; “Đăng ký thành công!…” từ server, giữ login return | S+T; P |
| Blocked local | POST `{success:false,blocked:true}` → reload → `#blockedAccountModal`; reason/expiry/appeal, không tự unlock | S; B browser; P |
| Blocked external | TempData BlockedReason/BlockedExpiry → `#blocked-alert`; không gọi provider trong preview | S; B; P |

Acceptance: cả hai entry route đúng tab; Enter/double click chỉ một request;
HTTP failure retry được; không dùng timer suy success; terms thật; JSON/redirect
không đổi; blocked modal keyboard/focus + appeal giữ nguyên. Không có empty
state cho màn login; blank inputs là default form.

### AU02 — Quên mật khẩu

**Task:** yêu cầu khôi phục. `ForgotPassword.cshtml`, shared auth assets.
Cùng card recovery (1440/768 <=30rem, 390 full available width), brand → h1
“Quên mật khẩu” → “Nhập email đã dùng để đăng ký. Nếu email có trong hệ thống,
bạn sẽ nhận được hướng dẫn đặt lại mật khẩu.” → Email → “Gửi liên kết khôi phục”
→ “Quay lại đăng nhập”. Short-height cuộn document, không hero/center clipping.

Label/hint/error nối `Email`; pending “Đang xử lý…” chỉ sau valid submit; giữ
Html.BeginForm, required/email validation và ModelState return. Focus error khi
server có lỗi; Tab theo DOM, Esc không hủy submit. Không frontend xác minh email
có tài khoản; không thêm resend timer. Native network-error limitation theo mục 3.
O: GET ở bốn kích thước; S/P: invalid/pending; B: gửi email. Acceptance: đúng
POST/antiforgery, thông báo non-enumerating, không invent route confirmation.
Không empty state; default là field rỗng.

### AU03 — Xác nhận yêu cầu

**Task:** biết bước tiếp theo. `ForgotPasswordConfirmation.cshtml`, auth CSS;
không cần auth JS ngoài pending handler chung không có form. Envelope icon
trang trí → `_PageHeader` h1 “Hãy kiểm tra email của bạn” → `_StateMessage`
Success không action duplicate, message từ server hoặc generic fallback:
“Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật
khẩu.” → “Kiểm tra cả thư mục thư rác. Liên kết khôi phục có thời hạn.” → CTA
“Quay lại đăng nhập”, link “Nhập lại email” về AU02. Không echo email/token.

1440/768/390 cùng card; short-height vẫn đọc hết. Focus heading khi render sau
POST; Tab chỉ hai links; Esc không có hành vi đặc biệt; không spinner hay empty
state. Đây là result AU02, không có `/Account/ForgotPasswordConfirmation` GET.
S/P, B browser thật; acceptance cùng generic copy với email tồn tại/không tồn
tại trong isolated fake-mail fixture, không suy “đã giao thư” từ success UI.

### AU04 — Đặt lại mật khẩu

**Task:** đặt password mới bằng link hợp lệ. `ResetPassword.cshtml`, auth assets.
Brand → h1 “Đặt lại mật khẩu” → hint ít nhất6 → server validation summary →
Mật khẩu mới / Xác nhận mật khẩu mới (show buttons riêng) → “Xác nhận đặt lại
mật khẩu” → “Quay lại đăng nhập”. `Token`/`Email` hidden giữ nguyên, không hiển
thị/sao chép vào docs. Field mapping chính xác ở phụ lục.

Invalid/expired: hiển thị Token error đã có từ ModelState ngay trong summary;
copy thêm “Yêu cầu liên kết mới để tiếp tục.” với anchor AU02. Chỉ nhận diện
Token validation errors hiện có, không tự suy expiry bằng client clock/query.
Nếu Token errors có mặt, vẫn giữ hidden và form contract; focus summary, không
autofocus password. Không thay backend gate khi GET chưa kiểm tra token.

Mismatch/short client validation inline, summary khi server trả; pending
“Đang xử lý…”; success server redirect Login với TempData, không tạo success
route mới. O: GET không token; T: null/valid/replay/concurrency; S/P: lỗi và
thành công trực quan; B: browser reset user live. Acceptance: name/casing giữ,
HTML helper/antiforgery, Compare, không consume token lần hai, link login/AU02.

### AU05 — Hồ sơ, quick security, subscription

**Task:** xem danh tính và chỉnh tên/ảnh. `Profile.cshtml`, `account-ui.css/js`.
Giữ route/model và ba pane. PageHeader “Hồ sơ của tôi”, desc “Cập nhật thông tin
cá nhân và xem thành tích học tập.” ở ngoài panes. Profile pane: identity row
(initials/avatar + FullName + Email + Role) → inline dl streak/count → form
“Thông tin cá nhân” → membership/goals info → “Danh hiệu đã đạt”. Desktop form
và info cạnh nhau; 768/390 stack theo thứ tự trên. FullName/email wrap; identity
không có date/achievement invented. Form `FullName`, hidden file `Avatar`, read
only Email có visible label và link Settings; không biến Email thành bound update.

Upload: “Đổi ảnh đại diện”; hint “JPG hoặc PNG, tối đa 2 MB. Ảnh chỉ được lưu khi
bạn chọn Lưu thay đổi.” từ policy thật. Local preview state hiển thị “Ảnh mới
chưa lưu”. Missing/broken URL → initials (U nếu tên rỗng), load ảnh hợp lệ →
show img/hide fallback; invalid MIME/read → fallback + error, không khẳng định
upload. “Hủy” reload như hiện tại; “Lưu thay đổi” native multipart. Không upload
khi chỉ chọn file; server là nguồn sự thật validate. File error/size copy không
thay whitelist. File không hợp lệ client phải clear input/retry được.

EarnedBadges.Count và StreakDays giữ thật. Danh sách từ EarnedBadges, chỉ item
IsUnlocked được xác nhận; Name/Description encoded, không locked placeholders,
không tự tính progress. Empty: “Chưa có danh hiệu” + “Danh hiệu sẽ xuất hiện
khi bạn hoàn thành các mốc học tập.” + `/Goals` “Xem mục tiêu học tập”. Không
khẳng định zero badge là hệ thống hỏng. Đủ data: list responsive, không carousel.

Membership: Role Standard/Premium/Admin hiển thị đúng model; Standard có nâng
cấp `/Premium`; Premium giữ entry Gói dịch vụ và nói “Gói Premium”; Admin là
“Vai trò Admin”, không đổi thành Standard. Không có VM expiry: “Chưa có thông
tin thời hạn tại đây.”, không “Không giới hạn/Đang hoạt động” suy diễn.
Goals info link `/Goals`; Tiếng Việt/GMT+7 là thông tin hiện tại, không controls.

Security pane giữ UpdateEmail/ChangePassword/DeleteAccount tại chỗ và hướng
dẫn “Quản lý thêm thông báo và liên kết tại Cài đặt tài khoản.” → Settings.
Không redirect tự động làm mất đường vào. Dùng cùng composition/form semantics
AU06; thêm IDs `profileEmailInput/profileCurrentPassword/profileNewPassword/
profileConfirmPassword`, không thay names. Subscription pane giữ URL/query và
entry `/Premium`, chỉ trình bày thông tin Role có thật. Một h2 pane, h1 chung.

Focus/Tab: menu rồi heading rồi form; file chooser cancel trả trigger; toggles
focusable; success/error theo TempData. Invalid Profile ModelState hiện redirect
không bảo toàn lỗi/giá trị; **chưa hứa sửa server**. Upload-exception branch không
repopulate Role nên có thể mặc định Standard: không chế giải pháp client lưu
Role; backend limitation được nêu riêng. O: hai role/default/tabs/nav, S/P:
preview avatar/errors/earned-list; acceptance gắn checklist batch3.

### AU06 — Cài đặt tài khoản

**Task:** bảo mật và hiểu trạng thái tài khoản. `Settings.cshtml`, account assets.
PageHeader “Cài đặt tài khoản”, desc “Quản lý bảo mật, liên kết và các tùy chọn
hiện có.”. Sau feedback: section nav wrap → Security (Email rồi password) →
Linked → Notifications → Danger. Desktop mỗi form tối đa khoảng70ch, không
ép password thành ba ô rất hẹp; từ <1200 stack, row email/button wrap. 390 CTA
toàn chiều rộng, no fixed footer. Short-height có document scroll và modal body
scrollable. Tất cả `#section-*`, `.settings-section-nav-item` và nav ID giữ lại.

- Security Email: visible “Địa chỉ email”, input `settingsEmailInput` name Email,
  “Cập nhật email”; password “Mật khẩu hiện tại”, “Mật khẩu mới”, “Xác nhận mật
  khẩu”, hint ít nhất6, “Đổi mật khẩu”. Fields current/new autocomplete. Show
  labels riêng. Không quảng bá đã thiết lập MFA. Social marker note: “Nếu bạn
  chưa có mật khẩu riêng, hãy dùng Quên mật khẩu để yêu cầu hướng dẫn.”; giữ
  server CurrentPassword requirement, không tự đặt placeholder làm password.
- Linked: từng provider icon/text + trạng thái. IsGoogleLinked/IsFacebookLinked
  true → “Đã kết nối” không action; Google false+HasUnconfirmedSocialLoginMarker
  → “Chưa xác nhận” và không Kết nối giống source; false còn lại → “Chưa kết nối”
  với `/Account/LinkGoogle` hoặc LinkFacebook. Không suy Facebook marker từ Google
  branch. Permanent note “Hệ thống chưa cung cấp bằng chứng liên kết cho mọi tài
  khoản. Thông báo xác nhận email không thay thế trạng thái đã kết nối.”. Giữ
  TempData response text encoded; không invent unlink action. Không gọi OAuth để QA.
- Notifications: giữ `#section-notifications` và hai `.notification-checkbox`
  trong disabled fieldset, unchecked; legend “Tùy chọn thông báo”; badge “Chưa hỗ
  trợ”; copy “Các tùy chọn này hiện chưa được lưu. Bạn chưa thể bật hoặc tắt tại
  đây.”. Không nút Save/toast/tooltip tự nhận đã bật push; không bảo người dùng
  rằng mọi thông báo hiện bị tắt. Disabled là UI khả năng chỉnh, không preference.
- Danger: giải thích dữ liệu sẽ bị xóa vĩnh viễn, button “Xóa tài khoản” → modal;
  không primary toàn trang. CONFIRM case-sensitive sau trim như source. Cancel
  và close/Escape không gửi POST. Modal nằm trong tct-theme; pending chỉ khi
  thực submit. Reset guard mỗi mở, disable double-submit.

Success/Error chỉ từ TempData PRG; no-op UpdateEmail với email không đổi không
có SuccessMessage: không phát thêm “Cập nhật thành công”. Khi lỗi PRG focus
alert, không claim inline server errors mà controller chưa trả. O: Standard
security/menu/modal cancellation/linked false; T social unconfirmed; P các
branches; B OAuth/save/delete. Acceptance: missing antiforgery reject, native
forms và response contracts không đổi, chữ rõ cho unsupported control.

### AU07 — Không đủ quyền

**Task:** hiểu vì sao bị từ chối và về trang hợp lệ. `AccessDenied.cshtml`,
`account-ui.css`; không thêm JS nếu chỉ links. `_Layout` và HTTP403 nguyên vẹn.
PageHeader h1 “Bạn không có quyền truy cập trang này” + text hiện tại giải thích
quyền; icon lock chỉ trang trí. CTA “Quay lại trang chủ” → Home/Index; secondary
“Về hồ sơ của tôi” → Profile. Không request access/contact mới; không link tài
nguyên private từ URL bị từ chối. 1440 text <=70ch, 768/390 wrap/stack; short
height cuộn; Tab hai links trong content theo DOM, Escape theo shared drawer nếu
đang mở. Không pending/empty/success giả cho error page. O/T 403, P composition;
acceptance Authorize và 403 giữ, IDOR resource checks tiếp tục 404.

## 6. Phụ lục hợp đồng source phải giữ

Line refs dưới đây tại `eed4e2c`, không phải line sau triển khai. Controller vẫn
ở namespace legacy `TCTVocabulary.Controllers`; ViewModels dùng
`TCTVocabulary.ViewModels`, GoalsBadgeViewModel dùng `TCTEnglish.ViewModels`.
Không đổi namespace trong UI task.

### HTTP và binding

| View/action | Form/field/hidden | Kết quả hiện tại |
| --- | --- | --- |
| Auth login `144–183`; Controller `129–185` | `#form-login`, POST Login, email/password, __RequestVerificationToken; `#login-email/#login-password/#btn-login-submit` | JSON `{success:true}` → client Home/Index; false+message generic; false+blocked → reload TempData modal. Không returnUrl logic hiện hữu |
| Auth register `186–223`; Controller `74–127` | `#form-register`, POST Register, email/password/fullName, antiforgery; register-email/name/password, terms required **không name**, btn-register-submit | Invalid/duplicate → HTML Auth với ActiveTab register và ViewBag.ErrorMessage; success →302 Login + SuccessMessage |
| External auth `131–137`; Controller `209–314` | btn-google/btn-facebook → ExternalLogin?provider=Google/Facebook | Challenge → ExternalLoginCallback; cookie sign-in →Home; error Auth/Login; blocked reason/expiry TempData |
| Forgot `263–277`; Controller `405–446` | HTML helper `#form-forgot-password` POST ForgotPassword; Email `#Email`; antiforgery; btn-forgot-submit | Invalid ModelState → same view; valid both existence branches → confirmation ViewBag.Message. Existing account token expiry15min; không gọi trong live audit |
| Reset `263–287`; Controller `700–757` | HTML helper `#form-reset-password` POST ResetPassword; Token/Email hidden `#Token/#Email`; NewPassword/ConfirmPassword; antiforgery; btn-reset-submit | Invalid model/token/expiry → same model+summary errors keyed Token; valid atomic consume →302 Login+SuccessMessage |
| Profile `205–233`; Controller `473–515` | POST UpdateProfile, **multipart/form-data**, FullName/Avatar + antiforgery; FullName validation; Email display disabled không gửi | Valid→save/sign-in claims+TempData→302 Profile; upload error→View(model); invalid model→redirect Profile. Không hứa round-trip khi không có |
| Profile security `287–346`, Settings `128–192`; Controller `545–650` | Hai POST riêng UpdateEmail (Email) và ChangePassword (CurrentPassword/NewPassword/ConfirmPassword), mỗi form antiforgery | TempData success/error→302 Settings, kể cả submit từ Profile security. Email no-op chỉ redirect. ChangePassword không dùng ModelState.IsValid làm toàn bộ policy |
| Delete modal Profile `366–396`, Settings `319–349`; Controller `652–699` | POST DeleteAccount chỉ antiforgery; deleteConfirmInput không có name/không gửi; deleteAccountBtn disabled tới trim CONFIRM | Transaction cleanup; failure→TempData error/302 Settings; success signout→302 Login. Không thêm confirm field endpoint |
| Linking Settings `196–267`; Controller `316–377` | LinkGoogle/LinkFacebook GET Authorize → LinkExternalCallback | Callback email match message; không provider-ID storage proof; redirect Settings |
| AccessDenied `1–16`; Controller `37–44` | Authorize GET, no form | View explicit + **403**; không đổi thành 200/404 hay dùng Forbid cho IDOR |

### DOM/callback/validation

- Auth: `tab-login/tab-register`, `form-login/form-register`, `auth-heading`,
  `btn-close-auth`, `blocked-alert/blocked-reason/blocked-expiry`,
  `blockedAccountModal/blockedAccountModalLabel`, `loader-overlay/loading-fill`,
  `login-error-msg`, `btn-google/btn-facebook`; các callback ở mục4. Loader có
  thể thành pending tối giản nhưng giữ IDs khi handler còn tham chiếu. Không để
  decorative fake-percent xác nhận thành công. Terms native required giữ.
- Recovery: `_main-content` không phải ID hiện tại; đúng là `main-content`.
  Giữ đúng ID này ở standalone. `handleForgotSubmit/handleResetSubmit` không
  preventDefault lần submit hợp lệ; antiforgery helper không bị bỏ khi đổi markup.
  Reset ValidationSummary **excludePropertyErrors:false**, gồm Token errors.
- Profile: `avatarPreview/avatarFallback/avatarClickTarget/Avatar`,
  `previewImage`, `FullName`, `.toggle-pwd/.input-group`, account nav IDs,
  `.account-tab-pane`, `deleteAccountModal/deleteAccountModalLabel/
  deleteConfirmInput/deleteAccountBtn`, key `accountSidebarCollapsed`.
- Settings: `settingsEmailInput`, `CurrentPassword/NewPassword/ConfirmPassword`
  IDs từ asp-for; `navSecurity/navLinked/navNotifications/navDanger`,
  `data-section`, `section-security/linked/notifications/danger`,
  `.settings-section-block`, `.notification-checkbox`, modal IDs, sidebar IDs.
- Page-specific classes đổi được, các selectors/IDs kể trên có adapter/test khi
  đổi ownership. Preserve Type/Name/Action/Method/Enctype, không chỉ giống ảnh.
- ViewBag.ErrorMessage; TempData SuccessMessage/ErrorMessage/IsBlocked/LockReason/
  LockExpiry/BlockedReason/BlockedExpiry phải tiếp tục được render đúng ngữ cảnh,
  encoded; không dùng Html.Raw cho user text. JSON serialization chỉ khi cần.

## 7. Bản đồ file và acceptance sau triển khai

| Batch | Files ứng dụng được đề xuất | Phụ thuộc / kiểm tra |
| --- | --- | --- |
| 1 | Auth.cshtml + mới account-auth-ui.css/js | Tab/login JSON/register native, terms, blocked, target44, Inter; desktop/mobile keyboard |
| 2 | ForgotPassword/ForgotPasswordConfirmation/ResetPassword.cshtml + auth assets | HTML helpers/hidden/summary/return path; fixture fake mail + invalid/replay, không token thật |
| 3 | Profile.cshtml + mới account-ui.css/js | Nav contract/drawer/collapse; avatar preview/fallback; earned badges; Standard/Premium/Admin/empty; không submit tài khoản live |
| 4 | Settings.cshtml, AccessDenied.cshtml + account assets | Scope scripts; security forms, linked certainty, disabled notifications; modal CONFIRM/reset;403/404 |
| 5 | UI tests mới đã nêu trong checklist; ledger/adoption rows sau nghiệm thu | Build/focused/full suite; browser actual zoom; Admin CSS consumer và learning smoke |

Không thêm partial Account trong phương án mặc định: mỗi route giữ typed model
hiện tại, tránh adapter ViewModel chỉ để share form. Giảm drift qua cùng CSS/JS
và test behavior của cả hai security entry points. `_PageHeader/_StateMessage`
được reuse nhưng implementation shared của chúng read-only.

Stage I acceptance phải ghi tách O/T/S/P/B như nghiên cứu. Preview không chứng
minh Razor/RBAC/persistence/SMTP/OAuth. Bộ hồ sơ hiện tại hoàn thành thiết kế,
không ký nghiệm thu ứng dụng sau triển khai. Phương án B đã được owner duyệt;
Stage I chờ owner khởi chạy prompt triển khai. Các backend/global đề xuất cần
quyền riêng. Không có bước triển khai
in-scope nào phụ thuộc việc chọn font/màu/token mới.
