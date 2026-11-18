# Account UI — nghiên cứu và kiểm tra hiện trạng

Ngày: 2026-11-08, Asia/Bangkok. Phạm vi: Stage D, `ANALYSIS_AND_DESIGN_ONLY`.
Nhánh quan sát: `feature/chau-ui-design-system`; HEAD
`eed4e2c95c79ef708dc9af77c8b9326186835122`.

Thiết kế được tạo dưới dạng bản nháp ở Stage D và **đã được Châu duyệt ngày
2026-11-08** bằng tin nhắn “Duyệt” trong chat. Stage I chờ Châu khởi chạy prompt
Gemini; không sửa ứng dụng trong đợt này. [Đặc tả](../design-system/account-ui-design.md),
[preview](../design-system/account-ui-preview.html) và
[checklist triển khai](../superpowers/plans/2026-11-08-account-ui-implementation.md)
là ba phần còn lại của bộ bàn giao.

## 1. Phương pháp, xuất xứ và giới hạn bằng chứng

- Một agent tự đọc source, nghiên cứu, dựng preview, review và kiểm tra. Không
  khởi chạy model/agent khác; tên model trong prompt không phải bằng chứng đã
  chạy model đó. Không thay cấu hình model của ứng dụng.
- Đọc AGENTS.md, chuẩn prompt/template, DESIGN.md, team UI workflow, Stage D và
  năm skill TCT được yêu cầu. Áp dụng review inline. Tham khảo hướng dẫn audit/
  craft ở bản vendor, ưu tiên quy chuẩn TCT khi khác nhau.
- CodeGraph được gọi trước cho AccountController, bảy view, ViewModels, partial,
  upload policy và test liên quan. Không rebuild/init index. **Khoảng trống cụ
  thể:** Auth không trả thân file; Profile/Settings/Forgot chỉ trả 4 dòng đầu;
  Reset/AccessDenied không trả markup trong truy vấn; CSS không có nội dung hữu
  ích. Đọc trực tiếp phần thiếu có đánh số dòng. Confirmation và shared partials
  được đọc từ output verbatim; phần controller bị lược được đọc bổ sung.
- Context7 không được expose. Nghiên cứu ngoài dùng nguồn chính thức qua web.
  Không đưa source riêng, email đăng nhập, key hay token lên web query.
- Chuẩn công bố: baseline đã chứa merge PR #130 và #131. Câu “chỉ cục bộ, chưa
  commit” trong UI workflow là ghi chép cũ, không phải trạng thái Git hiện tại.
- Năm file untracked có trước được ghi hash tại `.tmp/account-ui/baseline.json`:
  hai prompt Account; orchestration plan Account; báo cáo bth3; scope UI. Không
  ghi đè những file đó. Bốn đầu ra của Stage D chưa tồn tại lúc bắt đầu.

Các nhãn dùng xuyên suốt:

| Nhãn | Ý nghĩa |
| --- | --- |
| O — observed | Đã xem/đo trên ứng dụng local hiện tại |
| T — existing test fixture | Test sẵn có chạy trên test host cô lập; không phải browser live |
| S — source | Suy ra từ source hiện tại, chưa đi hết luồng trên browser |
| P — preview | Dữ liệu/trạng thái tổng hợp trong HTML tĩnh |
| B — blocked/not exercised | Không thao tác để bảo toàn tài khoản hoặc tránh dịch vụ ngoài |

## 2. Ma trận route, quyền, view, trạng thái và assets

View trong bảng đều thuộc `TCTEnglish/Views/Account/`.

| ID | Route/kết quả | Quyền và layout | View / dữ liệu | Assets hiện tại | Bằng chứng |
| --- | --- | --- | --- | --- | --- |
| AU01 | GET/POST `/Account/Login`, `/Account/Register` | Anonymous; GET đã đăng nhập chuyển Home; standalone | `Auth.cshtml`, `dynamic`, ActiveTab/ViewBag/TempData | Bootstrap 5.3.3, FA 6.4, Be Vietnam Pro/Inter, shared CSS, auth-stitch.css, SweetAlert CDN, inline JS | O: login/register, tab phím, visibility, safe local login. B: đăng ký thật, blocked, OAuth |
| AU02 | GET/POST `/Account/ForgotPassword` | Standalone, không cần đăng nhập | `ForgotPassword.cshtml`, ForgotPasswordViewModel | Inter đến 800, FA 6.4, inline CSS/palette và submit callback | O: GET ở 4 viewport. S: ModelState. B: POST email hợp lệ |
| AU03 | `View("ForgotPasswordConfirmation")` từ POST AU02 | Kết quả flow, không có action GET riêng | `ForgotPasswordConfirmation.cshtml`, ViewBag.Message | Inter/FA, inline CSS riêng | S: cả email có/không tồn tại cùng generic message. P: composition. B: browser flow thật |
| AU04 | GET/POST `/Account/ResetPassword` | Standalone | `ResetPassword.cshtml`, ResetPasswordViewModel | Inter/FA, inline CSS/palette, submit callback | O: GET không query. T: null expiry, dùng một lần, cạnh tranh. B: reset tài khoản local |
| AU05 | GET `/Account/Profile`; POST `/Account/UpdateProfile` | Authorize; `_Layout`; HideSidebar=true, Account sidebar | `Profile.cshtml`, UpdateProfileViewModel | shell assets + account.css + Be Vietnam Pro/Material Symbols + inline JS | O: Standard/Premium, các tab, sidebar. T: earned-badge query. B: lưu/upload thật |
| AU06 | GET `/Account/Settings`; POST UpdateEmail/ChangePassword/DeleteAccount | Authorize; `_Layout`; HideSidebar=true | `Settings.cshtml`, SecuritySettingsViewModel | giống Profile; validation partial | O: Standard, controls/menu/modal hủy. T: social marker. B: mọi thay đổi tài khoản/OAuth/xóa |
| AU07 | GET `/Account/AccessDenied` | Authorize; `_Layout`, giữ learner sidebar | `AccessDenied.cshtml`, không model | shell Bootstrap 5.3.2/shared CSS/layout; chưa tct-theme | O: 403 bằng Standard. T: Admin forbid Standard / IDOR 404 |

`account.css` còn được `_AdminLayout.cshtml:32` nạp cho mọi trang Admin.
File có `:root` và override `.text-primary/.text-secondary !important` ở 959–960.
Đây là consumer dùng chung, không phải stylesheet có thể sửa tùy ý cho Account.
Đề xuất bỏ **link tại hai view Account** và thay bằng scoped assets; giữ file và
hai shared layout nguyên vẹn. Shell còn nạp Be Vietnam Pro cho route Account;
page có thể render Inter dù tải font legacy dư vẫn còn.

## 3. Kết quả chính và mức ưu tiên

Mức ưu tiên là đánh giá thiết kế/khả dụng, không phải chứng nhận bảo mật toàn hệ thống.

| ID / mức | Trigger, vị trí và ảnh hưởng | Bằng chứng | Xử lý trong thiết kế / giới hạn |
| --- | --- | --- | --- |
| F01 / P1 | Profile `?tab=security`: ba `.toggle-pwd` có tabindex=-1, không accessible name; label mật khẩu chưa nối input (`Profile:311–336`) | S+O DOM | Giữ flow; thêm label/id phân biệt, 44px toggle, aria-controls/pressed; chung hành vi với Settings |
| F02 / P1 | Notification checkboxes checked dù không lưu (`Settings:269–296`); bỏ chọn rồi reload không phải save | S+O: name rỗng, ngoài form; model và toàn bộ script không có preference/save | Giữ mục và `.notification-checkbox`, disabled + unchecked; copy giải thích chưa hỗ trợ, không toast đã lưu |
| F03 / P2 | Profile membership hiển thị Premium “Không giới hạn”, trạng thái “Đang hoạt động” (`149–164`, `439–453`), model không có ngày hết hạn/trạng thái subscription | S+O Premium | Hiển thị Role có thật; thời hạn “Chưa có thông tin thời hạn tại đây”. Giữ `/Premium`; không suy ra quyền/expiry |
| F04 / P2 | Profile security/subscription không có h1 nhìn thấy vì h1 chỉ ở pane profile; query tab không hợp lệ tạo 0 pane active | S+O | Một `_PageHeader` ngoài panes; fallback query không nhận diện về profile; giữ tab hợp lệ và replaceState |
| F05 / P2 | Form đổi mật khẩu nói 8 ký tự nhưng controller/VM tối thiểu 6; register/reset cũng 6 | S+O Settings | Copy “Ít nhất 6 ký tự theo yêu cầu hiện tại”. Không nâng policy. Mâu thuẫn chính sách tách riêng để owner quyết định |
| F06 / P2 | LinkExternalCallback chỉ so email rồi báo thành công; Settings luôn set hai linked bool=false. Social sentinel chỉ là dấu hiệu, không chứng minh provider | S: controller 340–376, 532–540; T marker | Giữ nhánh boolean/unconfirmed/false, route và TempData. Thêm thông tin giới hạn xác nhận; không tự biến thành badge “Đã kết nối” |
| F07 / P2 | Social marker vẫn gặp yêu cầu CurrentPassword không rỗng trước nhánh bypass (`ChangePassword:612–629`) | S, chưa thao tác real | Không quảng bá “đặt mật khẩu đầu tiên không cần mật khẩu hiện tại”; giữ form, cung cấp đường khôi phục. Backend cần task riêng |
| F08 / P2 | Auth password toggle ~24.9×30.5, close 40×40, tabs cao 41px; Be Vietnam Pro; heading chỉ visually-hidden | O ở 1440; source CSS | Bootstrap form/tokens; target 44px; h1 nhìn thấy theo tab; bỏ hero cao trên mobile |
| F09 / P2 | Profile và Settings dùng hai bản security/delete markup; Profile variant yếu a11y hơn; Settings có thêm rail mục con | S+O | Giữ cả đường vào; security quick panel tại Profile cùng style/behavior; mục con Settings thành nav wrap, không tạo rail thứ ba |
| F10 / P2 | Recovery dùng palette riêng, không Bootstrap/shared CSS; title AU02 dễ nhầm bước reset; confirmation fallback khẳng định đã gửi email | S+O AU02 | Một composition; AU02 “Quên mật khẩu”; AU03 luôn generic cả fallback. Không invent standalone confirmation GET |
| F11 / P2 | TempData alert Profile/Settings bị xóa tự động sau 5s; dễ bỏ lỡ lỗi/thành công | S | Alert bền đến dismiss/navigation; native redirects giữ nguyên; không tạo thành công giả cho save |
| F12 / P3 | Auth `500+ Từ vựng` không lấy số liệu; dark/glass hero chiếm 163px đầu mobile ngắn | S+O screenshot 390×500 | Text hỗ trợ gọn, không số liệu/quyền lợi không kiểm chứng; desktop giữ panel thương hiệu nhẹ |

Không kết luận có lỗi tràn ngang hiện tại ở các lượt đo dưới đây: scrollWidth
không vượt innerWidth. Điều đó chưa chứng minh mọi descendant không bị clip,
đặc biệt shell có quy tắc overflow cũ. Không gọi các lỗi avatar/sidebar đã sửa
ngày 02/11 là bug còn tồn tại.

## 4. Những hành vi cần bảo toàn từ bug memory

Đã đọc các mục liên quan ngày 2026-11-02 trong `.ai/context/bug-fix-log.md`:
Plan2 Account correctness; Plan1 UI review repair; Plan1 mobile fixes. Known
issues hiện tại TD-002/TD-003 nói về controller/service debt; không mở scope
refactor controller ở Stage D hay Stage I UI.

- Avatar: valid FileReader load phải bỏ display:none của ảnh và ẩn fallback;
  invalid MIME/read/image error trở lại initials. Chưa upload tài khoản live.
- Closed Account sidebar ở <=992px: inert=true; desktop inert=false; mobile
  aria-expanded true/false; Escape trả focus toggle đã quan sát lại.
- Earned badges và streak phải từ model/service. Profile hiện chỉ render count,
  chưa render tên badges; có sẵn `Name/Description/IconClass/AwardedAt` để render.
  Không thêm 75%/40% hoặc mục tiêu giả.
- BCrypt/social/malformed hash fail closed; reset null expiry bị từ chối;
  IDOR vẫn 404, trang AccessDenied vẫn 403. Test bằng fixture sẵn có, không sửa DB.

## 5. Kiểm tra local đã chạy

Launcher:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5102
pwsh -NoProfile -File scripts/speech-status.ps1
```

MariaDB 10.5.23 ready. Launcher báo build succeeded, không có migration được
áp dụng, bootstrap existing users skipped; shared-data kiểm tra 1 release,
**0 record operations**. `/health/ready` trả `200 {"status":"ready"}`. Speech
`Configured=true`, `ConfigurationValid=true`, `VerifiedLive=false`; không paid
request. Không đọc/in key hay ghi lại User Secrets. Chỉ login Standard/Premium
UAT có sẵn; credential chỉ trong bộ nhớ, ảnh đã thay danh tính bằng dữ liệu giả.

Browser: Chrome 156.0.8078.80 headed, viewport CSS emulation, DPR 1; Playwright
1.62 alpha từ cache sẵn có, không cài dependency. DevTools mở Auth thành công
nhưng ghi screenshot tuyệt đối bị workspace-root check của connector từ chối;
chuyển sang browser local hiện có và lưu trong `.tmp`. Không đổi trust/config.

| Màn / viewport | scrollWidth / innerWidth | Trạng thái đã xem |
| --- | --- | --- |
| Auth login 1440×900, 768×1024, 390×844, 390×500 | 1440/1440, 768/768, 390/390, 375/390 | Default; register tương ứng cũng không rộng hơn viewport |
| Forgot cùng 4 viewport | 1440/1440, 768/768, 390/390, 390/390 | GET, không submit email |
| Reset 390×500 | 375/390 | GET không token; không chứng minh token hợp lệ |
| Profile Standard cùng 4 viewport | 1425/1440, 753/768, 375/390, 375/390 | Danh tính, empty avatar, count/streak, các tab; ảnh redacted |
| Profile Premium 1440×900, 390×844 | 1425/1440, 375/390 | Role khác, membership copy hiện tại; ảnh redacted |
| Settings Standard cùng 4 viewport | 1425/1440, 753/768, 375/390, 375/390 | Security, linked false, notification controls; ảnh redacted |
| AccessDenied Standard 390×500 | HTTP 403 | Explanation/internal links, không vào tài nguyên khác |

ArrowRight đổi Auth tab và chuyển focus đúng `tab-register`; visibility chuyển
input sang text. Settings menu mở aria-expanded=true/inert=false, Escape đóng
aria-expanded=false/inert=true và trả focus `sidebarMobileToggle`. Password
toggle Settings hoạt động với Enter. Mở modal, gõ CONFIRM chỉ để kiểm tra nút
enable rồi hủy; **không gửi DeleteAccount**. Escape gửi ngay trong transition
show của Bootstrap có thể bị bỏ qua; Escape sau transition đóng được. Không
dùng lần gửi phím quá sớm đó làm kết luận về accessibility ở trạng thái ổn định.

Ctrl++ qua protocol không đổi DPR/viewport: **chưa xác minh browser zoom thật**.
Kiểm tra reflow/text-size bổ sung của preview được ghi ở mục 7; không thay tên
thành “đã pass zoom 200%”. Không thử thiết bị vật lý/screen reader.

Baseline regressions:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests" --logger "trx;LogFileName=account-baseline.trx" --results-directory .tmp/account-ui/test-results
```

**20 passed, 0 failed, 0 skipped, 27s.** Test build có warning nullability/analyzer
sẵn có ngoài phạm vi; không coi là build sạch warning. TestWebApplicationFactory
dùng SQLite in-memory, no-op migrator, test auth, internal AI provider, blocked
embedding/speech. Các test này không phải chứng minh SMTP/OAuth hay provider
MariaDB cho một schema mới. Không có model/schema change cần migration trong đợt này.

Evidence local (ignored): `before-public.json`, `before-accounts.json`,
`before-settings.json`, `before-exploration.json`, các `before-*.png` và
`test-results/account-baseline.trx` dưới `.tmp/account-ui/`. JSON chỉ chứa số đo/
kết quả; không lưu password/cookie/reset token.

## 6. Nghiên cứu → quyết định

Các URL dưới đây được mở ngày **2026-11-08**. Đây là hướng dẫn tham khảo,
không cấp quyền đổi security policy hay nhập code/style của tổ chức khác.

| Nguồn chính thức | Phát hiện dùng được | Màn | Adopt / adapt / reject và lý do |
| --- | --- | --- | --- |
| [W3C WAI Forms](https://www.w3.org/WAI/tutorials/forms/) | Label, chỉ dẫn và nhóm field rõ | AU01/02/04/05/06 | Adopt semantics; dùng Bootstrap/TCT, chỉ hỏi field hiện có |
| [WAI User Notifications](https://www.w3.org/WAI/tutorials/forms/notifications/) | Lỗi ngắn có cách sửa, gắn field; feedback sau submit | AU01/02/04/05/06 | Adapt error summary focusable và inline associations; alert lưu đến dismiss |
| [GOV.UK Password input](https://design-system.service.gov.uk/components/password-input/) | Hide mặc định, show có tên, autocomplete phù hợp và paste | AU01/04/05/06 | Adopt hành vi; reject bỏ confirm-password ở reset/settings vì hợp đồng hiện tại cần field đó |
| [GOV.UK Passwords](https://design-system.service.gov.uk/patterns/passwords/) | Lỗi login generic; reset bằng link có thời hạn | AU01–04 | Adapt generic message; reject áp dụng minimum 8 hoặc cơ chế khóa mới từ reference trong task UI này |
| [Bootstrap 5.3 Validation](https://getbootstrap.com/docs/5.3/forms/validation/) | Custom validation tooltip chưa tự đảm bảo AT; server/native feedback cần giữ | Các form | Adopt server/native + aria-describedby, không thêm novalidate chỉ để tô màu |
| [WAI Disclosure Navigation](https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/) | Navigation link không cần role=menu; Escape trả focus | AU05/06 | Adapt state/focus cho account drawer hiện có; không copy example code; giữ inert khi đóng |
| [WCAG 2.2 Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html) | Nội dung cần đọc được ở chiều rộng hẹp, không cuộn hai chiều | Cả bảy | Adopt wrapping, min-width:0 và document scroll; bổ sung 320px kiểm tra preview |

Đã chạy installed UX lookup (không regenerate DESIGN.md):

```powershell
uv run --no-project python .agents/skills/tct-ui-ux-pro-max/scripts/search.py "accessible authentication password show hide error recovery" --domain ux -n 3
uv run --no-project python .agents/skills/tct-ui-ux-pro-max/scripts/search.py "mobile navigation focus disclosure responsive short viewport" --domain ux -n 3
```

Kết quả: error recovery, password-manager/paste, visibility; viewport meta,
dynamic viewport height và mobile-first. Adopt các nguyên tắc này; không thêm
passkey, stack React/Tailwind hay font/palette mới từ dataset.

## 7. Kiểm tra preview và đóng gói

HTML mở trực tiếp từ ổ đĩa trong Chrome 156.0.8078.80; CSS shared, site/layout,
Bootstrap local5.3.3 và icon/font hiện có nạp được. `document.fonts.check` cho
Inter trả true; không có missing glyph. Không cài dependency/browser/package.

| Kiểm tra | Kết quả quan sát cuối | Evidence trong `.tmp/account-ui/` |
| --- | --- | --- |
| 7 màn × 1440×900 / 768×1024 / 390×844 / 375×812 / 390×500 / 320×720 | 42 lượt; không scrollWidth vượt viewport, không descendant visible nằm ngoài mép theo phép đo; một h1 nhìn thấy mỗi màn | `preview-layouts.json`; `preview-au01` đến `preview-au07` ở 1440/390 PNG |
| Bộ chọn state | 39 trạng thái tổng hợp; không h1 thừa hoặc JS error; reset success minh họa chuyển sang login success | `preview-states.json` |
| Text contrast / control boundary | 34 state đại diện, 767 mẫu chữ computed; không mẫu chữ đang bật dưới 4.5:1 (chữ lớn3:1), không viền control đang bật dưới3:1 | `preview-contrast.json` |
| Keyboard Auth | Left/Right/Home/End roving tabs; Enter reveal; focus outline2px, offset2px, màu token | `preview-keyboard.json` |
| Menu Account | Trap Tab/Shift+Tab trong drawer; Escape → opener focus, inert=true/expanded=false; resize sang desktop dispose drawer, inert=false, body scroll phục hồi; desktop collapse chạy | `preview-keyboard.json` |
| Delete confirmation | Rỗng/lowercase disable; trim CONFIRM enable; reopen reset; Escape trả focus “Xóa tài khoản”; không xóa | `preview-keyboard.json` |
| Cỡ chữ và long text | 14 lượt (7 màn ×1440/390 ngắn), root font-size200% trong browser và văn bản tổng hợp dài; không tràn document | `preview-reflow.json` |
| Reduced motion | Emulated preference=true; shared duration token0.01ms | `preview-reflow.json` |
| Target/hover/font | Icon visible Auth44×44; primary button44px; Inter loaded; social hover dùng text/surface-muted, không palette phụ | `preview-assets.json` |
| Không thao tác live | Chỉ GET assets; không XHR/fetch; bấm submit với dữ liệu tổng hợp và Google không điều hướng tới dịch vụ, không gửi request tài khoản; CSP connect-src/form-action chặn thêm một lớp | `preview-keyboard.json`, `preview-assets.json` |
| Syntax | Inline script được tách vào scratch, `node --check .tmp/account-ui/preview-inline.js` exit0 | `preview-inline.js` |

Đã xem ảnh desktop Profile và Auth, mobile Settings cùng các ảnh bố cục bảy
màn. Bộ chọn dùng nội dung/tên/email `example.invalid`; không chứa reset token,
cookie hay credential. Preview có disclaimer rõ, từng provider/save/delete
chỉ minh họa. Khung learner được mô phỏng bằng dữ liệu tổng hợp; không phải copy
HTML hay trạng thái private của người đang đăng nhập.

Trong review prototype đã sửa ba vấn đề nội bộ: specificity khiến nút thường
còn40px; focus opener quá sớm khi Bootstrap trap còn active; hover social dùng
màu Bootstrap không hợp với text override. Lượt cuối đã đo lại sau sửa. Font
Awesome6.0 không có glyph shield-halved nên đổi sang alias shield-alt đã có.
Đây là chỉnh sửa prototype, không phải sửa ứng dụng hay thêm icon family.

Contrast tính từ computed foreground và background ghép alpha qua ancestors;
loại hidden/offcanvas đóng và mẫu đang transition/opacity, tách disabled miễn
trừ. Không đo raster anti-alias và không tuyên bố WCAG conformance toàn trang.
Các trạng thái modal/error/success cần tiếp tục kiểm tra ở Razor sau triển khai.
**Zoom200% thật chưa xác minh**; 14 lượt cỡ chữ200% không thay thế zoom browser.
Không thử assistive technology hoặc thiết bị cảm ứng vật lý.

Preview khác ứng dụng ở nguồn Bootstrap local5.3.3 (learner shell5.3.2) và FA6.0
(Auth/recovery6.4). Những ID cần tách để bảy màn cùng tồn tại trong một document
được prefix ở prototype; phụ lục đặc tả mới là hợp đồng Razor. Collapse state
preview chỉ trong bộ nhớ, không ghi localStorage. Không dùng preview để nghiệm
thu persistence/RBAC/SMTP/OAuth.

Kiểm tra đóng gói: UTF-8, 11 Markdown local links, 106 HTML IDs (không trùng),
32 label/ARIA references, 5 local assets, 58 token references, JS syntax,
sensitive patterns và whitespace. Kết quả cuối lưu trong
`artifact-verification.json`. Chỉ có đúng bốn đầu ra mới; tracked app/test/script
diff rỗng. Không chỉnh adoption tracker trước khi ứng dụng được triển khai và
quan sát thực tế.

**Preservation notice:** hash cuối của `2026-11-08-bth3-class-report.md` khác
baseline. Tác vụ này không có thao tác ghi vào file đó; chưa xác định tác nhân
thay đổi và giữ nguyên bản hiện tại, không restore/ghi đè. Bốn file có trước còn
lại giữ nguyên SHA-256. Lượt validator đầu đã báo chênh lệch này; kết quả cuối
ghi `passed_with_preservation_notice`, không nhận năm file đều byte-identical.

## 8. Các trạng thái còn chưa quan sát trên ứng dụng

- AU03 browser thực; reset expired/invalid/valid trong browser; blocked modal/
  external callback; save/error/upload trên tài khoản hiện có. Source và các test
  đã chạy chỉ bao phủ phần được nêu, không đại diện mọi trạng thái.
- Không gọi SMTP, OAuth, speech, AI/chat; không đăng ký/xóa/reset/chỉnh role hay
  cập nhật email/avatar/password của người dùng. Không bịa token sản xuất.
- Không thay production gate, không commit/push/PR/remote CI/staging/deployment.
  Không có kết luận phát hành từ kiểm tra local này.

## 9. Phê duyệt thiết kế và các quyết định còn tách riêng

1. **Đã duyệt:** Châu chấp nhận phương án B và bộ hồ sơ thiết kế AU01–AU07 bằng
   tin nhắn “Duyệt” ngày 2026-11-08 (Asia/Bangkok). Chờ khởi chạy prompt Stage I;
   chưa triển khai ứng dụng hoặc nghiệm thu kết quả triển khai.
2. Tách riêng nếu muốn làm backend: persistence thông báo; nguồn sự thật liên
   kết provider; trường hợp social-only đặt local password; dữ liệu expiry gói;
   thống nhất policy password. Phương án UI mặc định vẫn thực hiện được bằng
   thông tin trung thực/disabled và giữ policies hiện tại.
3. Các đề xuất global đã có trong DESIGN.md (root font mobile, header height,
   tct-theme mặc định) tiếp tục để riêng. Bản Account không cần token mới và
   không yêu cầu quyết định màu/font/thay shell để triển khai độc lập.
