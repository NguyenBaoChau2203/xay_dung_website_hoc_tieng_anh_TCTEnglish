# Review triển khai Account UI của Gemini

Ngày: 2026-11-08, Asia/Bangkok. Mode: `REVIEW_ONLY`.
Nhánh: `feature/chau-ui-design-system`; HEAD `eed4e2c95c79ef708dc9af77c8b9326186835122`.

**Kết luận: chưa đạt nghiệm thu.** Build và test tự động đang xanh, nhưng browser
tái hiện được hai lỗi P1: cả hai form khôi phục không gửi được lần đầu và menu
mobile để lại khóa cuộn khi chuyển về desktop. Có thêm sáu nhóm P2 cần hoàn thiện.
Review này không cấp quyền sửa ứng dụng; source hiện tại được giữ nguyên.

Tài liệu đi kèm: [repair plan](../superpowers/plans/2026-11-08-account-ui-review-repair.md),
[execution prompt](../../.ai/prompts/account-ui-review-repair.md),
[thiết kế đã duyệt](../design-system/account-ui-design.md).

## Phạm vi và bằng chứng

- Một agent tự review, thực nghiệm và kiểm tra. Dùng tct-superpowers,
  tct-engineering, tct-ui-review; giữ quy tắc owner và thiết kế đã duyệt.
- CodeGraph được gọi trước cho hai JS mới, Razor, render tests và test helpers.
  Source trả về bị lược một số body; Razor Auth/Profile/Settings thiếu markup;
  hai CSS mới không được index. Đọc trực tiếp các phần thiếu, không rebuild index.
- Đối chiếu bảy Account views, hai CSS, hai JS, sáu render tests, mười Node tests,
  contract appendix, checklist và execution ledger. Controller/service/model,
  config, shared CSS/layout không có thay đổi trong diff ứng dụng.
- `DESIGN.md` là thay đổi có trước theo ledger Gemini; review không quy thay đổi
  laptop-priority đó cho Gemini và không sửa nó. Các file bẩn/untracked đầu lượt
  được ghi hash tại `.tmp/account-ui-review/baseline.json`.
- Browser Chrome156.0.8078.80, Playwright từ cache sẵn có; không cài package/browser.
  Không thể chạy app với MariaDB: launcher đã thử khởi động Docker Desktop và
  thoát1 vì `context deadline exceeded`; không có readiness app/MariaDB được xác nhận.
- Để kiểm tra được giao diện thật, dùng `TestWebApplicationFactory` có sẵn với
  SQLite in-memory, test auth và fake mail. Host tạm trong ignored `.tmp` forward
  **GET allowlist** tới TestServer, render Razor đã compile từ source hiện tại,
  phục vụ CSS/JS thật. Mọi non-GET tới host bị405; login AJAX được intercept tại
  browser. Không reset/sửa người dùng thật, gửi mail, OAuth hay gọi AI/audio.
- Confirmation và expired reset được render qua cùng Razor engine với fixture
  ModelState; `/__fixture/*` chỉ thuộc host kiểm tra tạm, không phải endpoint app.
  Đây là bằng chứng fixture/browser, không phải nghiệm thu MariaDB/production.

## Findings cần sửa

### R1 — P1: hai form khôi phục hủy chính lần gửi hợp lệ đầu tiên

**Vị trí:** `Views/Account/ForgotPassword.cshtml:50`, `ResetPassword.cshtml:50`;
`wwwroot/js/account-auth-ui.js:354–361` cùng các handler ở227–287.

Hai view vẫn có `onsubmit="handleForgotSubmit(event)"` hoặc
`onsubmit="handleResetSubmit(event)"`, trong khi init JS đăng ký thêm submit
listener gọi lại cùng handler. Lượt inline đầu đặt `_inFlight=true` và disable
button. Lượt listener kế tiếp thấy pending và `preventDefault()` chính event đó.

**Tái hiện bằng click thật:** nhập email hợp lệ tại Forgot; nhập hai password
khớp và đủ dài tại Reset. Cả hai đều `checkValidity()=true`,
`defaultPrevented=true`, button disabled, `_inFlight=true`, **0 POST**. Người dùng
không thể gửi yêu cầu/reset và nút giữ trạng thái đang xử lý. Trong control
experiment chỉ gỡ inline attribute ở DOM fixture, lần click kế tiếp tạo đúng
1 POST đã bị browser intercept. Không có source nào được sửa trong thử nghiệm.

**Sửa tối thiểu:** một event owner cho mỗi form; giữ HTML helper, action/method,
antiforgery, hidden Token/Email, public aliases nếu còn cần. Thêm regression dựa
trên form được render và event lifecycle đầy đủ, không chỉ gọi handler một lần.

Evidence: `behavior-findings.json`, `forgot-stuck.png`, `reset-stuck.png`.

### R2 — P1: đổi mobile → desktop khi drawer mở làm trang mất khả năng cuộn

**Vị trí:** `wwwroot/js/account-ui.js:61–68`.

`syncDrawer()` xóa class rồi gọi `dispose()` trên Offcanvas đang mở, không chạy
hide/hidden lifecycle trước. Thực nghiệm mở menu ở390×844, resize1440×900 cho
kết quả body `overflow:hidden`, `padding-right:15px`, sidebar còn class `show`,
`role=dialog` và `aria-modal=true`. Document cao1975px nhưng wheel600px vẫn giữ
scrollY=0. Nội dung dưới màn hình không tiếp cận được bằng cuộn thông thường.

**Sửa tối thiểu:** yêu cầu hide, đợi hidden rồi mới dispose/cleanup/đặt desktop
state; phục hồi scrollbar, overlay, ARIA, inert và collapse preference qua một
owner. Kiểm tra resize cả lúc mở, đóng và đang transition trên Profile/Settings.

Evidence: `behavior-findings.json`, `drawer-resize-lock.png`.

### R3 — P2: pageshow bật nút xóa bất kể CONFIRM; Settings modal nằm ngoài scope

**Vị trí:** `wwwroot/js/account-ui.js:490–501`;
`Views/Account/Settings.cshtml:297–329`.

Handler pageshow enable tất cả submit buttons. Với modal Profile đang mở và
CONFIRM rỗng, nút disabled trước event; phát `PageTransitionEvent('pageshow',
{persisted:true})` trong browser làm nút enabled dù input vẫn rỗng. Đây là kiểm
tra handler phục hồi trạng thái; **chưa xác minh một vòng BFCache thực**. Không
gửi thao tác xóa. Lỗi thuộc guard UI, không phải chứng minh bypass authorization
hay antiforgery của server.

Ngoài ra modal Settings nằm sau khi wrapper `.account-ui.tct-theme` đã đóng.
Delete form không thuộc collection `.account-ui form`, nên thiếu pending guard
được hứa trong thiết kế và không nhận đầy đủ theme/focus rules.

**Sửa tối thiểu:** phục hồi pending theo từng loại form; destructive button chỉ
enable khi điều kiện CONFIRM hiện tại đúng, reset mỗi show/hidden/pageshow.
Đặt modal Settings trong page theme/scope và giữ một Bootstrap modal owner.
Regress empty/lowercase/trim CONFIRM, reopen, pageshow và duplicate-submit cho
cả hai entry point. Không dùng tài khoản thật để thử POST DeleteAccount.

### R4 — P2: Hủy làm mất hiển thị ảnh đại diện đã lưu

**Vị trí:** `wwwroot/js/account-ui.js:429–438`, `setAvatarState:384–393`.

Handler Hủy đổi sang reset cục bộ rồi luôn gọi `setAvatarState('default')`, nhánh
này ẩn ảnh và hiện initials. Với fixture đã có avatar tải thành công, chỉ bấm
Hủy, không cần chọn file, đã đổi từ imageVisible=true sang false và fallback
false sang true. UI hiển thị sai danh tính hiện tại dù DB không thay đổi.

**Sửa tối thiểu:** khôi phục hành vi reload đã duyệt hoặc khôi phục đầy đủ trạng
thái ảnh/name gốc tin cậy; vẫn xử lý missing/broken avatar và file hợp lệ. Không
ghi Role/email/avatar vào storage để thay source of truth.

Evidence: các trường avatarBefore/avatarAfterCancel trong `behavior-findings.json`.

### R5 — P2: chữ trạng thái chưa xác nhận không đạt tương phản

**Vị trí:** `Views/Account/Settings.cshtml:221`, `236`.

Branch `HasUnconfirmedSocialLoginMarker` dùng Bootstrap `.text-warning` thay vì
semantic text token. Browser đo chữ14px `rgb(255,193,7)` trên trắng **1.63:1**;
badge12px cùng màu trên `rgb(255,243,205)` **1.47:1**. Đều dưới4.5:1. Đây là
thông tin quan trọng về độ chắc chắn của liên kết, không phải control disabled.

**Sửa tối thiểu:** dùng `--tct-warning-text/--tct-warning-soft` hoặc
`.tct-badge--warning`; không sửa linked booleans hay suy provider từ sentinel.
Giữ nhánh Google unconfirmed và Facebook khác nhau như contract hiện tại.

Evidence: `behavior-findings.json`, `unconfirmed-contrast.png`.

### R6 — P2: focus/control styles dùng token sai kiểu và chưa khớp foundation

**Vị trí:** `wwwroot/css/account-auth-ui.css:233–236`,167–170,266–269;
`account-auth-ui.css:18–21`, `account-ui.css:16–18`; các chỗ dùng `--tct-shadow-sm`.

`--tct-focus-ring` là **màu**, không phải box-shadow shorthand. Đặt
`box-shadow:var(--tct-focus-ring)` đồng thời `outline:none` làm focus input Auth
mất ring2px đã duyệt. Sau Tab đến login-email và đợi transition kết thúc,
computed outline-style=none, box-shadow=none, chỉ còn viền đổi xanh. Không kết
luận mọi tab/nav cũng mất ring: shared selector vẫn thắng ở một số control.

Rule `:where(button,.btn)` có specificity thấp hơn `.tct-theme .btn`: nút login
và email-save vẫn cao40px thay vì44px của bản duyệt. Icon controls có rule riêng
44px vẫn có thể đúng. `--tct-shadow-sm` không tồn tại trong shared CSS nên các
shadow đó bị bỏ; không cần tạo token mới vì đã có `--tct-shadow-soft`.

**Sửa tối thiểu:** tái sử dụng shared focus outline hoặc đủ width/style/color;
đo computed focus/target sau cascade. Dùng token tồn tại. Scope lại modal
selectors sau R3; giữ shared CSS/layout/brand/root font nguyên vẹn.

Evidence: `behavior-findings.json` authFocusSettled, `auth-keyboard-focus.png`.

### R7 — P2: reset invalid/expired thiếu đường yêu cầu link mới

**Vị trí:** `Views/Account/ResetPassword.cshtml:57`,88–91.

Razor đã render Token error đúng, nhưng khi ModelState có lỗi hết hạn/không hợp
lệ, anchor duy nhất trong content là “Quay lại đăng nhập”. Thiết kế đã duyệt yêu
cầu copy hướng dẫn và link trực tiếp ForgotPassword để tiếp tục recovery. Điều
này đặc biệt cần khi chính recovery đang bị R1 chặn.

**Sửa tối thiểu:** chỉ khi Token có ModelState errors, render hướng dẫn và anchor
tới AU02, focus summary có thể tiếp cận. Giữ Token/Email hidden và server validation;
không tự xác định expiry bằng client clock hoặc tạo confirmation GET mới.

Evidence: `behavior-findings.json` expiredReset,
`reset-expired-no-recovery-link.png`. Đây là state render fixture, không reset user thật.

### R8 — P2: ledger/tracker và test coverage chưa hỗ trợ claim B5 hoàn tất

**Vị trí:** `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md:38`,46–54,71–85;
`docs/design-system/team-ui-workflow.md:147–148`;
`TCTEnglish.Tests/AccountUiRenderTests.cs`; hai `scripts/tests/test_account_*`.

Ledger gọi26 test có filter là “full regression” và đánh dấu B5 DONE dù browser
bị blocked. Nó còn nêu Auth IDs/fields `loginForm`, `UsernameOrEmail`, RememberMe,
RegisterUsername, AcceptTerms... không có trong source triển khai; đúng contract
là form-login/form-register và email/password/fullName. Tracker ghi `/Account/Auth`
như entry dù không có action đó. Những ghi nhận này khiến người sau tưởng đã
nghiệm thu đúng flow và dễ làm hỏng binding khi tái sử dụng.

Sáu render tests chủ yếu kiểm tra substring/ID và GET happy path; test mang tên
RetainsHiddenTokens_AndValidationSummary không kiểm tra summary. Mười Node tests
không dựng event lifecycle gồm inline+listener, không có Forgot-submit case,
resize while open, native validation cancellation, pageshow guard, cancel avatar
gốc, hoặc contrast. Test xanh là có thật nhưng không chứng minh các hành vi đó.

**Sửa tối thiểu:** thêm regression meaningful cho R1–R7 rồi hoàn thiện ledger bằng
kết quả thực; không lấy skipped/fixture thay live provider proof. Adoption status
phân biệt “đã chuyển code” và “đã nghiệm thu”. Giữ nguồn thiết kế đã duyệt và
unrelated DESIGN/report changes, không đổi lịch sử hay tự công bố release.

## Kiểm tra đã chạy trong lượt review

| Kiểm tra | Kết quả | Ý nghĩa/giới hạn |
| --- | --- | --- |
| `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` | Pass;0 warnings,0 errors trong lượt này | Compile hiện tại; không chứng minh browser |
| Bốn nhóm Account/render/badge/partial | **26 pass,0 fail,0 skip**,12s | Bao gồm6 render tests mới |
| Hai Node test files | **10 pass,0 fail** | Harness hiện có, không bao phủ các regressions nêu trên |
| Normal app suite, Release `--no-build` sau compile mới | **1175 pass,22 skip,0 fail**,1197 total;5m15s | Live OpenAI/audio và MariaDB-dependent checks skip; không gọi chúng là pass |
| Browser layout default | **48 lượt**,8 entry/state variants ×6 viewport; một visible h1, không page overflow trong phép đo | 1440×900,1366×768,768×1024,390×844,375×812,390×500;1366 chỉ là sample laptop viewport, không suy ra laptop demo thật |
| Browser behavior | R1–R7 reproduced/observed như từng finding | R3 là event pageshow tổng hợp; không actual BFCache proof |
| Login AJAX failure/duplicate | Một request khi click+Enter pending;HTTP500 stub khôi phục nút và focus error | Intercept browser, không gửi credential tới server ngoài |
| Admin/learning smoke | Admin Dashboard200, account.css vẫn nạp; Vocabulary/Index200, không nạp account-ui.css | `browser-smoke.json`; SQLite fixture và assets thật, không chứng minh hosting |
| Speech metadata | Configured=true,ConfigurationValid=true,VerifiedLive=false | Không paid request |
| Local MariaDB app launch | Exit1, Docker Desktop context deadline exceeded | Không `/health/ready` pass cho app thật |

Commands có thể đối chiếu:

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests" --logger "trx;LogFileName=account-review-focused.trx" --results-directory .tmp/account-ui-review/tests
node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --no-build --logger "trx;LogFileName=account-review-full.trx" --results-directory .tmp/account-ui-review/tests
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5104
pwsh -NoProfile -File scripts/speech-status.ps1
```

Ảnh/JSON/TRX và helper review nằm trong ignored `.tmp/account-ui-review/`.
Không lưu password/cookie/key/live recovery token vào report. Source hashes đầu
review dùng để xác minh rằng review không sửa triển khai Gemini.

Kiểm tra đóng gói cuối: **26 file có trước giữ nguyên SHA-256**, chỉ thêm đúng
ba tài liệu review/plan/prompt;8 local links, UTF-8, JS syntax và whitespace được
kiểm tra. `artifact-verification.json` ghi trạng thái passed. Không sửa execution
ledger/tracker đang review để che các claim chưa có bằng chứng.

## Điểm đã làm đúng và giới hạn còn lại

- Giữ các routes/actions, antiforgery và reset hidden fields; không có controller,
  service, model, migration hay config change trong diff Account.
- Nền tảng composition B đã hiện diện, Profile có một h1 ngoài panes, dữ liệu badge
  và role không bị thay bằng số liệu giả; notification controls đã disabled.
- Login AJAX duplicate/error handling đã qua browser stub và tests;403 và IDOR404
  tiếp tục qua focused existing tests. Không tuyên bố đã thử OAuth/SMTP/real reset.
- Browser shell báo `jQuery is not defined`. Đối chiếu HEAD cho thấy `_Layout`
  trước thay đổi cũng không nạp jQuery, trong khi Settings đã gọi validation
  partial: ghi nhận dependency có trước, **không tính là regression mới của Gemini**.
  Chưa khẳng định client-validation cancellation behavior trong môi trường có
  jQuery. Không sửa shared layout để chữa trong lượt review.
- Chưa xác minh actual200% browser zoom, screen reader, touch device vật lý,
  live MariaDB upgrade/readiness, SMTP/OAuth hay production. Các kiểm tra đó
  không được thay bằng preview/fixture hoặc trạng thái test xanh.

## Handoff

Ưu tiên R1 và R2 trước, sau đó bảo toàn destructive guard/avatar, sửa a11y và
recovery exit rồi bổ sung test/evidence. [Repair plan](../superpowers/plans/2026-11-08-account-ui-review-repair.md)
chốt files/dependencies/acceptance; [prompt](../../.ai/prompts/account-ui-review-repair.md)
chỉ được kích hoạt khi owner cho phép sửa. Không commit/push/PR/remote CI/merge/
deploy và không thay production gate trong lượt review này.
