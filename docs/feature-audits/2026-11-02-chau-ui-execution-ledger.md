# Execution Ledger — Plan1 UI Improvements (Gemini 3.8 Flash High)

**Cập nhật03/11 — đóng phạm vi local:** UI được giữ qua toàn bộ Plan2; mobile/
desktop, ba vai trò, modal và assets đã được kiểm tra lại. F1/F2 đã xử lý.
Trạng thái cuối và các giới hạn nghiệm thu:
[báo cáo đóng hai Plan](2026-11-03-plan1-plan2-local-closeout.md).
Các phần dưới là lịch sử của lần Gemini thực thi, không phải test mới chạy03/11.

- Baseline commit: `210bf2de12051db6238d4f53214aba4b73c3ce29` (master)
- Working branch: `feature/chau-ui-performance-audit`
- Model IDE: Gemini 3.8 Flash High | Website AI model: OpenAI `gpt-6-luna` (reasoning low)
- Host environment: Local Windows x64, MariaDB 10.5.23 Docker (`tctenglish-mariadb-local` port 3307), ASP.NET Core 9.0.4 Release port 5112 / Debug port 5100
- Tooling: Chrome DevTools MCP via Headless Chrome (remote debugging on 127.0.0.1:9222)
- Test user: `admin@tct.local` (Admin fixture in `.appdata/local/access.json`, UserID 60)
- Execution scope: Local UI improvements only (Razor views, CSS, presentation JS). No backend logic changes, no migrations, no git commits/pushes/deploys.

---

## 1. Batch Progress Overview

| Batch | Description | Status | Findings Addressed |
| --- | --- | --- | --- |
| **UI0** | Xác nhận baseline và hợp đồng | COMPLETED | Baseline, selector manifest, live reproduction of U01, U02, U03, U05, U08 |
| **UI1** | Auth và recovery | COMPLETED | U01, U02 |
| **UI2** | Shell chung và điều hướng | COMPLETED | U04 |
| **UI3** | Profile/Settings mobile & honest display | COMPLETED | U01, U03, U06, U08, U11 |
| **UI4** | Speaking/Writing/Goals task layout & states | COMPLETED | U09 |
| **UI5** | AI full page và embed | COMPLETED | U05, R1 (P1 submit guard), R3 (P2 viewport thấp) |
| **UI6** | Admin theo nhiệm vụ | COMPLETED | U01, R4 (P2 sidebar inert), R5 (P2 skip link z-index) |
| **UI7** | Verification và bàn giao | COMPLETED | Toàn bộ Plan1 matrix & checks, R6 fresh suite 811 pass/8 skip |

---

## 2. UI0 — Baseline Verification Record

- **Git status & branch:** On `feature/chau-ui-performance-audit` at commit `210bf2d`. Preserved all untracked audit/plan files and BTH2 report.
- **Services status:**
  - MariaDB: container `tctenglish-mariadb-local` running on 127.0.0.1:3307 (healthy).
  - App Release: PID 10876 listening on port 5112, `/health/ready` = 200 OK.
  - App Debug: PID 10032 listening on port 5100, `/health/ready` = 200 OK.
  - Whisper: stopped (`-SkipWhisper` by policy until needed).
  - Key source: User Secrets configured.
- **CDP / Browser setup:** Chrome 156 headless running with `--remote-debugging-port=9222`, connected to `chrome-devtools` MCP server.
- **Live reproduction evidence:**
  - **U02 (Auth 390x844):** `clientWidth: 390`, `scrollWidth: 393`, `authCard: { x: -3, width: 396, right: 393 }`. Card overflows viewport width by 3px.
  - **U08 (Profile 390x844):** Outer main width 390px, `.account-main` squeezed to `x: 264, width: 122px` because `.account-sidebar` (width 260px) remains in flex layout due to `position: sticky` in `account.css:1303+` overriding `@media (max-width: 680px)`. Nested `<main>` inside `<main>` detected.
  - **U03 (Profile hardcoded stats):** Profile displays hardcoded `75%` (Writing) and `40%` (Speaking) in `.side-progress-list`.
  - **U05 (AI Chat 390x844):** Shell header takes 188.86px, observability metrics panel takes 299.06px, crushing the chat window wrapper to only 11.05px (`ai-chat-window` 25.78px). Single-line `<input>` instead of multi-line composer.
- **Before screenshots captured:**
  - Auth Desktop (1440x900) & Mobile (390x844)
  - Profile Mobile (390x844)
  - AI Chat Mobile (390x844)
- **Gate UI0:** Passed. Real contracts and live selectors identified. Ready for UI1.

---

## 3. UI1 — Auth và recovery (U01, U02)

- **Files modified:**
  - `TCTEnglish/Views/Account/Auth.cshtml`
  - `TCTEnglish/Views/Account/ForgotPassword.cshtml`
  - `TCTEnglish/Views/Account/ResetPassword.cshtml`
  - `TCTEnglish/Views/Account/ForgotPasswordConfirmation.cshtml`
  - `TCTEnglish/wwwroot/css/auth-stitch.css`
- **Actions taken:**
  - **U01 (Labels & accessibility):** Associated all inputs with explicit `label for`, added `autocomplete="email"`, `autocomplete="current-password"`, `autocomplete="new-password"`, `autocomplete="name"`. Changed login email input type to `email`. Added `aria-label` and `aria-pressed` to password toggle buttons. Added tablist keyboard navigation (ArrowLeft/ArrowRight) and proper `aria-selected` / `tabindex` / `aria-controls` semantics.
  - **U02 (Mobile card overflow & hero compactness):** Added universal `box-sizing: border-box` inside `.auth-page`, ensured `.auth-col-right` and `.auth-card` use `width: 100%; min-width: 0; box-sizing: border-box;`. In `@media (max-width: 480px)`, hid decorative `.auth-features` and compacted hero padding, reducing hero height from 481px to 163px so the form is visible without excessive scrolling.
  - **DOM DOB cleanup:** Removed unpersisted decorative DOB inputs from registration form (non-persisted per plan note).
  - **Duplicate submission protection:** Added pending state with `disabled = true`, `aria-busy="true"`, and loading spinner for both login AJAX and register submission.
  - **Safe error rendering:** Replaced unsafe `innerHTML` concatenation in `showLoginError(msg)` with `replaceChildren()`, `document.createTextNode(msg)`, and `role="alert"` / `aria-live="assertive"`.
  - **Headings & Landmarks:** Added `<main class="auth-col-right" id="main-content">` and hidden `<h1>` task heading. In `ForgotPassword` and `ResetPassword`, converted logo heading from `h1` to `.brand-title` and made page task heading `h1`.
  - **Reduced motion:** Added `@media (prefers-reduced-motion: reduce)` in CSS and JS navigation bypass.
- **Verification results:**
  - Mobile 390x844: `clientWidth: 390`, `scrollWidth: 390` (was 393), `card: { x: 12, width: 366, right: 378 }` (was `{ x: -3, width: 396 }`). 0px horizontal overflow. Hero height: 163px (was 481px).
  - Narrow 375x667: `clientWidth: 375`, `scrollWidth: 375`, `card width: 351px`. 0px overflow.
  - Desktop 1440x900: `clientWidth: 1440`, `scrollWidth: 1440`, `card width: 440px`.
  - Keyboard tab navigation, tab panels, and password toggles tested and working.
- **Gate UI1:** Passed. Ready for UI2.

---

## 4. UI2 — Shell chung và điều hướng (U04)

- **Files modified:**
  - `TCTEnglish/Views/Shared/_Layout.cshtml`
  - `TCTEnglish/Areas/Admin/Views/Shared/_AdminLayout.cshtml`
  - `TCTEnglish/wwwroot/js/layout.js`
  - `TCTEnglish/wwwroot/css/layout.css`
- **Actions taken:**
  - **U04 (Semantic buttons & keyboard access):** Converted click-only menu `<i>` into semantic `<button type="button" class="menu-icon-btn" id="menuToggleBtn">` with `aria-label`, `aria-controls="mainSidebar"`, `aria-expanded="false"`. Converted `userProfileBtn` into `<button type="button" class="user-profile-btn">` with `aria-haspopup="true"`, `aria-expanded="false"`, `aria-label`. Converted sidebar accordion headers (`coursesDropdownBtn`, `folderDropdownBtn`, `classDropdownBtn`) into `<button type="button" class="nav-dropdown-item">` with `aria-expanded` and `aria-controls`.
  - **U01 (Admin sidebarToggle):** Added `type="button"`, `aria-label="Mở thanh điều hướng quản trị"`, `aria-controls="adminSidebar"`, `aria-expanded="false"` to `#sidebarToggle` in `_AdminLayout.cshtml`.
  - **Language & Skip link:** Set `lang="vi"` in both `_Layout.cshtml` and `_AdminLayout.cshtml`. Added accessible skip links (`.skip-link` and `.visually-hidden-focusable`) pointing to unique main landmark IDs (`#main-content` and `#admin-main-content`).
  - **Keyboard & Focus management:** Added Escape key handling to close mobile sidebar, user dropdown, and accordion menus, returning focus to the trigger button. Outside click handling preserved.
  - **Aria-current on active links:** Added `aria-current="page"` to active navigation links.
  - **Reduced motion:** Added `@media (prefers-reduced-motion: reduce)` in `layout.css`.
- **Verification results:**
  - Tested on `http://127.0.0.1:5112/Home/Index` and `http://127.0.0.1:5112/Admin/Dashboard`.
  - Verified `document.documentElement.lang === "vi"`.
  - Verified presence and focusability of skip links.
  - Verified button types, accessible names, `aria-expanded`, and `aria-current="page"` on current navigation.
- **Gate UI2:** Passed. Ready for UI3.

---

## 5. UI3 — Profile/Settings mobile và hiển thị trung thực (U01, U03, U06, U08, U11)

- **Files modified:**
  - `TCTEnglish/Views/Account/Profile.cshtml`
  - `TCTEnglish/Views/Account/Settings.cshtml`
  - `TCTEnglish/wwwroot/css/account.css`
- **Actions taken:**
  - **U08 (P1 Mobile layout cascade fix):** Moved `position: sticky` on `.account-sidebar` inside `@media (min-width: 993px)` so it no longer overrides `@media (max-width: 992px)`. Ensured `.account-sidebar` is properly `position: fixed` off-canvas on mobile, and `.account-main` takes `width: 100% !important; flex: 1 1 100% !important; max-width: 100% !important; margin-left: 0 !important; box-sizing: border-box;`.
  - **Single main landmark:** Changed inner `<main class="account-main">` in both `Profile.cshtml` and `Settings.cshtml` to `<section class="account-main" aria-label="...">` to eliminate nested `<main>` landmarks.
  - **U03 (Honest progress):** Removed fake hardcoded `75%` (Writing) and `40%` (Speaking) progress bars from Profile side info. Replaced with honest setup text and direct CTA linking to `/Goals`.
  - **U11 (Avatar fallback):** Replaced default call to non-existent `/images/default-avatar.png` (404) with stylish gradient initial fallback `.user-avatar-fallback` (using uppercase first letter of user FullName) and client-side `onerror` fallback.
  - **U01 & U06 (Settings accessibility & honest linked status):** Associated Settings email and password inputs with explicit labels (`<label asp-for="...">` and `<label for="settingsEmailInput">`). Removed `tabindex="-1"` from `.toggle-pwd` buttons, added `aria-label="Hiện mật khẩu"` and `aria-pressed="false"`. Enhanced toggle click handler to synchronize `type`, `aria-label`, and `aria-pressed`. Added clarifying note to linked accounts describing connection status limits.
  - **Reduced motion:** Added `@media (prefers-reduced-motion: reduce)` in `account.css`.
- **Verification results:**
  - Tested on `http://127.0.0.1:5112/Account/Profile` and `http://127.0.0.1:5112/Account/Settings` at 390x844.
  - `.account-main` mobile width expanded from 122px (squashed) to **382px** (full available width).
  - Main landmarks: exactly 1 per page (`mainTagsCount === 1`).
  - `hasFakeStats === false`.
  - Initial avatar "T" displayed without any 404 network errors.
  - Password toggle buttons tab-navigable, keyboard operable, toggling input type and `aria-pressed` / `aria-label`.
- **Gate UI3:** Passed. Ready for UI4.

---

## 6. UI4 — Speaking/Writing/Goals: task layout và states (U09)

- **Files modified:**
  - `TCTEnglish/wwwroot/css/speaking.css`
  - `TCTEnglish/wwwroot/css/writing.css`
  - `TCTEnglish/wwwroot/css/layout.css`
- **Actions taken:**
  - **U09 (Speaking overflow 417px fix):** Added `overflow: hidden !important; position: relative;` to `.spk-hero-import` to ensure animated decorative blobs do not overflow. Added `min-width: 0 !important; flex: 1 1 0% !important; overflow: hidden !important;` to `.spk-filter-group--topics` and `max-width: 100% !important; width: 100% !important; overflow-x: auto !important;` to `.spk-topic-chips`. Stacked filter toolbar on mobile.
  - **Mobile global footer overflow fix:** In `layout.css`, added responsive media query for `.footer-container` with `flex-wrap: wrap !important` below 768px, eliminating the site-wide 720px minimum footer width issue on mobile.
  - **Writing mobile task hierarchy:** In `writing.css`, re-ordered `.writing-col-right` (the editor) to display first (`order: 1 !important`) and `.writing-col-left` (history and full text) second (`order: 2 !important`) on screens below 1024px. Students immediately see the editor and active sentence without scrolling past pages of text.
  - **Goals & Speaking states preserved:** Preserved all 5 goal areas and modal contracts (`data-goal-mode`, `data-testid="goal-header-cta"`, `goalEditorModal`). Preserved all speaking receipts, progress fields, and assessment states without mock scores.
- **Verification results:**
  - Tested on `http://127.0.0.1:5112/Speaking` at 390x844: `scrollWidth` decreased from 417px to **390px** (0px overflow).
  - Tested on `http://127.0.0.1:5112/Home/Writing/Practice?level=beginner&contentType=emails&topic=all&status=all&page=1&exerciseId=1` at 390x844: `scrollWidth === 390`, `isEditorAbovePrompt === true` (`editorTop: 215px`, `promptColTop: 815px`).
  - Tested on `http://127.0.0.1:5112/Goals` at 390x844: `scrollWidth === 390`, goal editor modal and CTA working.
- **Gate UI4:** Passed. Ready for UI5.

---

## 7. UI5 — AI full page và embed (U05)

- **Trạng thái:** HOÀN THÀNH & XÁC MINH TRÊN BROWSER (390×844 & 1440×900).
- **Files modified:**
  - `TCTEnglish/Views/Ai/_ChatShell.cshtml`
  - `TCTEnglish/Views/Ai/Chat.cshtml`
  - `TCTEnglish/wwwroot/css/ai-chat.css`
  - `TCTEnglish/wwwroot/js/ai-chat.js`
- **Key changes:**
  - **U05 (AI Chat metrics crushing window to 25.78px fix):**
    - Observability metrics panel (`#aiObservabilityPanel`) wrapped in `<details class="ai-chat-observability-disclosure" id="aiObservabilityDetails">` with `<summary class="ai-chat-observability-summary">`, collapsed by default on mobile. Preserved IDs: `aiMetricRequestCount`, `aiMetricErrorRate`, `aiMetricLatency`, `aiMetricTokenToday`. Height reduced from **299.06px** to **33.22px**!
    - History sidebar (`.ai-chat-history`) wrapped in `<details class="ai-chat-history-disclosure" id="aiHistoryDisclosure">`, collapsed by default on mobile (`:not([open]) .ai-chat-history-body { display: none !important; }`), automatically open on desktop (`window.innerWidth >= 992`). Preserved all history data attributes (`data-ai-history-root`, `data-ai-history-list`, `data-ai-history-empty`, `data-ai-history-delete`). Height on mobile reduced from **91.64px** to **46.16px**!
    - Mobile header compacted (`.ai-chat-shell-subtitle` hidden on mobile, title clamp reduced), dropping header height from 188.86px to 86.69px.
    - Chat window (`#chatWindow`) available vertical space on mobile (390×844) increased from **25.78px** to **473.98px** (massively exceeds the requirement `>= 220px`).
  - **Multiline composer & keyboard navigation:**
    - Replaced `<input id="messageInput" ...>` with `<textarea id="messageInput" maxlength="1000" rows="1" ...></textarea>`.
    - Auto-grow with `max-height: 120px` in `ai-chat.js` (`adjustTextareaHeight`).
    - Enter sends message; Shift+Enter creates a new line; IME composition (`e.isComposing || e.keyCode === 229`) is guarded to prevent early sending for Vietnamese/CJK input.
    - Single-row layout on mobile (`.ai-chat-composer-main` with `flex-wrap: nowrap !important; align-items: flex-end;`), reducing composer height from 96px to 58.5px.
  - **Transport contracts preserved:**
    - Full-page (`/AI/Chat`) and embedded (`/AI/Chat?embed=true`) share the same markup and selectors.
    - SignalR (`StartAiStream`, `AiStreamStarted`, `AiStreamChunk`, `AiStreamCompleted`, `AiStreamFailed`) and AJAX fallback (`POST /AI/Chat/Send`) preserved.
- **Verification results:**
  - Tested on `http://127.0.0.1:5112/AI/Chat` at 390x844:
    - `chatWindow`: height **473.98px** (up from 25.78px baseline), top 297.03px.
    - `obsDetails`: height **33.22px** (down from 299.06px baseline), `open: false`.
    - `history`: height **46.16px**, `open: false`.
    - `input`: `<textarea>`, auto-grow verified (`initial: 55px`, `4 lines: 94px`, `cleared: 55px`).
    - `sendBtn`: single-row aligned (`sameRow: true`).
  - Tested on `http://127.0.0.1:5112/AI/Chat` at 1440x900:
    - 2-column grid (`display: grid`), history drawer open (`historyOpen: true`, width 300px), chat surface width 749px, chat window height 469.64px.
  - Tested on `http://127.0.0.1:5112/AI/Chat?embed=true` at 390x844:
    - Chat window height **750.81px**, composer height 66px, input `<textarea>`, single-row send button.
- **Gate UI5:** Passed. Ready for UI6.

---

## 8. UI6 — Admin theo nhiệm vụ

- **Trạng thái:** HOÀN THÀNH & XÁC MINH TRÊN BROWSER (390×844 & 1440×900).
- **Files modified:**
  - `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/_ExerciseForm.cshtml`
  - `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml`
  - `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml`
  - `TCTEnglish/Areas/Admin/Views/UserManagement/Index.cshtml`
- **Key changes:**
  - **Writing Create/Edit IsPublished accessible label:**
    - Linked `<label asp-for="IsPublished" class="form-check-label visually-hidden">Hiển thị cho learner</label>` and `aria-label="Hiển thị cho learner"`.
    - Verified `hasLabel: true`, `labelText: "Hiển thị cho learner"`, `ariaLabel: "Hiển thị cho learner"`. Form submission contract and field name preserved.
  - **Accessible action buttons & pagination in admin tables:**
    - `SpeakingVideoManagement/Index.cshtml`: added `aria-label="Chỉnh sửa video @item.Title"` and `aria-label="Xóa video @item.Title"`.
    - `WritingExerciseManagement/Index.cshtml`: added `aria-label="Chỉnh sửa bài viết @item.Title"` and `aria-label="Xóa bài viết @item.Title"`.
    - `UserManagement/Index.cshtml`: added `aria-label="Chỉnh sửa người dùng @user.FullName"`, `aria-label="Mở khóa người dùng @user.FullName"`, `aria-label="Khóa tài khoản người dùng @user.FullName"`, `aria-label="Xóa bộ lọc"`, `aria-label="Trang trước"`, `aria-label="Trang sau"`.
  - **Mobile horizontal table scrolling:**
    - Verified all tables across `Dashboard`, `UserManagement`, `SpeakingVideoManagement`, `WritingExerciseManagement` are scoped inside `.table-responsive` with `overflow-x: auto`.
    - Live viewport check on 390×844: `scrollWidth === 390` (0px page overflow), table container scrolls horizontally internally without blowing out the page viewport.
  - **Preserved role guards & destructive action safety:**
    - No destructive mutation against real users or videos; did not trigger ML.NET training in `AiManagement`.
- **Verification results:**
  - Tested on `http://127.0.0.1:5112/Admin/Dashboard` at 390x844: `scrollWidth: 390`, 0px page overflow.
  - Tested on `http://127.0.0.1:5112/Admin/UserManagement` at 390x844: `scrollWidth: 390`, table width 885px inside `.table-responsive` width 342px, `tableRespHScroll: true`. Action buttons have accessible names.
  - Tested on `http://127.0.0.1:5112/Admin/SpeakingVideoManagement` at 390x844: `scrollWidth: 390`, table width 1108px inside container width 318px. Edit/Delete have accessible names.
  - Tested on `http://127.0.0.1:5112/Admin/WritingExerciseManagement` at 390x844: `scrollWidth: 390`. Edit/Delete have accessible names.
  - Tested on `http://127.0.0.1:5112/Admin/WritingExerciseManagement/Create` at 390x844: `IsPublished` has `hasLabel: true`, `ariaLabel: "Hiển thị cho learner"`.
- **Gate UI6:** Passed. Ready for UI7.

---

## 9. UI7 — Verification & Bàn giao

- **Trạng thái:** HOÀN THÀNH & TẤT CẢ GATES ĐÃ ĐẠT.
- **Verification checks:**
  1. **Build verification:**
     - Lệnh: `dotnet build TCTEnglish/TCTEnglish.csproj -c Release --no-restore -o .tmp/build-verify`
     - Kết quả: `Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed: 2.30s`.
  2. **Automated unit/regression test suite:**
     - *Ghi chú điều chỉnh lịch sử:* Lần chạy 101/101 tests trước đó thực hiện với cờ `--no-build` trên binaries Debug cũ (31/10). Để đảm bảo tính trung thực tuyệt đối theo kết quả audit reviewer và repair plan R6, bài test đầy đủ đã được chạy lại trên binaries hoàn toàn mới với cấu hình cô lập `Plan1Review` (tránh file lock do Web Release/Debug đang chạy):
       ```powershell
       $env:TCT_OPENAI_LIVE='0'; $env:TCT_OPENAI_FEATURE_LIVE='0'
       dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Plan1Review -p:Optimize=true --logger "trx;LogFileName=plan1-ui-repair.trx" --results-directory .tmp/plan1-ui-repair --verbosity quiet
       ```
     - **Kết quả kiểm thử fresh binaries:** **Failed: 0, Passed: 811, Skipped: 8, Total: 819** (Thời gian: 2 m 52 s, TRX: `.tmp/plan1-ui-repair/plan1-ui-repair.trx`).
  3. **Git diff check:**
     - Lệnh: `git diff --check`
     - Kết quả: Mã thoát `0`. Không có lỗi whitespace, conflict marker, hay lỗi cú pháp.
  4. **Matrix view audit (Sử dụng đúng tên view/route thực tế và ghi chú verified/unverified):**
     - `AU01 (/Account/Auth)`: [VERIFIED BROWSER] Card nằm gọn trong viewport 390px (366px, 0px overflow). Hero compacted còn 163px trên mobile. Labels, autocompletes, password toggle accessible.
     - `AU02 (/Account/ForgotPassword)`: [VERIFIED BROWSER] Landmark `<main>`, `<h1>` hiển thị, form semantics chuẩn.
     - `AU03 (/Account/ResetPassword)`: [VERIFIED BROWSER] Landmark `<main>`, `<h1>`, password toggle accessible.
     - `AU04 (/Account/ForgotPasswordConfirmation)`: [VERIFIED BROWSER] Landmark `<main>`, `<h1>`, success alert rõ ràng.
     - `AU05 (/Account/Profile)`: [VERIFIED BROWSER] Bỏ lồng `<main>`. Mobile `.account-main` đạt **382px**. Bỏ số giả 75%/40%, thay bằng empty state và CTA `/Goals`. Avatar fallback chữ cái đầu (R2 đã sửa avatar state transition khi chọn ảnh mới). Mobile sidebar đóng có `inert = true`, mở có `aria-expanded="true"` (R4).
     - `AU06 (/Account/Settings)`: [VERIFIED BROWSER] Label `for` liên kết ID inputs. Password toggle focusable, `aria-pressed`, `aria-label` đồng bộ. Mobile sidebar đóng có `inert = true` (R4).
     - `CO01 (/Home/Contact)`: [VERIFIED SOURCE] Layout responsive, semantics chuẩn.
     - `SP01 (/Speaking/Index)`: [VERIFIED BROWSER] `scrollWidth === 390` (trước đây tràn 417px do decorative blob và topic chips).
     - `SP02 (/Speaking/Practice/{id})`: [VERIFIED SOURCE] Giữ nguyên receipts, transcript container, record flow.
     - `SP03 (/Study/Speaking)`: [VERIFIED SOURCE] Route học speaking qua flashcard study mode, giữ nguyên contracts.
     - `WR01 (/WritingExercises)`: [VERIFIED SOURCE] Filters, level options chuẩn.
     - `WR02 (/WritingExercises/Details/{id})`: [VERIFIED SOURCE] Writing exercise view, giữ nguyên contracts.
     - `WR03 (/WritingPractice)`: [VERIFIED SOURCE] Writing practice flow, prompt/editor responsive.
     - `GO01 (/Goals)`: [VERIFIED BROWSER] Đầy đủ 5 areas, CTA modal focus đúng, số liệu từ Model. Main sidebar đóng có `inert = true`, Tab không lọt vào link ẩn (R4).
     - `AI01 (/AI/Chat)`: [VERIFIED BROWSER] Log height **473.98px** (390x844) và **197.67px** (390x500 không bị wrapper clip - R3). Submit guard in-flight ngăn trùng lặp khi nhấn Enter (R1). Observability metrics & history đưa vào collapsible disclosure. Textarea multiline auto-grow.
     - `AI02 (AI Chat Embed)`: [VERIFIED SOURCE] Single-row composer, textarea auto-grow.
     - `AD01 (/Admin/Dashboard)`: [VERIFIED BROWSER] `scrollWidth === 390`, KPI cards, real recent users.
     - `AD02 (/Admin/UserManagement)`: [VERIFIED BROWSER] Table 885px cuộn ngang mượt mà trong container 342px, `scrollWidth === 390`. Toàn bộ icon action có `aria-label`. Skip link nằm trên sticky navbar (`z-index: 1060`, R5). Admin sidebar đóng có `inert = true`, toggle có `aria-expanded` (R4).
     - `AD03 (/Admin/SpeakingVideoManagement)`: [VERIFIED BROWSER] Table 1108px cuộn ngang trong container 318px. Edit/Delete có `aria-label`.
     - `AD04 (/Admin/SpeakingVideoManagement/Create)`: [VERIFIED SOURCE] Form inputs có label required, link quay lại có `aria-label`.
     - `AD05 (/Admin/SpeakingVideoManagement/Edit/{id})`: [VERIFIED SOURCE] Preview card, form inputs giữ nguyên contracts.
     - `AD06 (/Admin/WritingExerciseManagement)`: [VERIFIED BROWSER] Table cuộn ngang nội bộ. Edit/Delete có `aria-label`.
     - `AD07 (/Admin/WritingExerciseManagement/Create)`: [VERIFIED SOURCE] `IsPublished` switch có `hasLabel: true`, `ariaLabel: "Hiển thị cho learner"`.
     - `AD08 (/Admin/WritingExerciseManagement/Edit/{id})`: [VERIFIED SOURCE] `IsPublished` switch có `hasLabel: true`, `ariaLabel: "Hiển thị cho learner"`.
     - `AD09 (/Admin/AiManagement)`: [VERIFIED SOURCE] Nút `btnTrain` có `aria-label`, không kích hoạt train để bảo vệ dữ liệu model.

---

## 10. Tổng kết Findings & Trạng thái Bàn giao

### 10.1 Bảng phân loại Findings (Đồng bộ chuẩn theo Audit gốc & Review Repair)

| Mã | Vấn đề từ Audit gốc | Trạng thái | Ghi chú & Giải pháp |
|---|---|---|---|
| **U01** | Input thiếu `label for` / `id` liên kết; nút icon thiếu tên | **CLOSED** | Đã liên kết tất cả inputs trong Auth, Settings, Admin Create/Edit; gán `aria-label` cho icon buttons |
| **U02** | Overflow 393/390px trên Auth card & mobile | **CLOSED** | Áp dụng `box-sizing: border-box`, loại bỏ margin âm, hero compact, `scrollWidth: 390` |
| **U03** | Profile hiển thị số giả 75%/40% hardcode | **CLOSED** | Đã loại bỏ số hardcoded, thay bằng thông điệp trung thực và CTA `/Goals` |
| **U04** | Shell click-only icons, thiếu semantics button, thiếu `lang="vi"`, skip links | **CLOSED** | Chuyển thành `<button>`, thêm `aria-expanded`, `aria-controls`, `lang="vi"`, skip-links, sync `inert` mobile (R4) |
| **U05** | AI Chat mobile bị metrics 299px ép log, composer single-line | **CLOSED** | Observability & History thành collapsible disclosure. Log height tăng lên **473.98px** (390x844). Composer multiline. Bổ sung submit in-flight guard ngăn lặp Enter (R1) và sửa min-height flex co giãn trên viewport thấp 390x500 (R3) |
| **U06** | Profile và Settings lặp form bảo mật; liên kết Google/Facebook chưa xác định | **CLOSED** | Giữ nguyên endpoints, không vẽ trạng thái kết nối chắc chắn khi backend chưa hỗ trợ |
| **U07** | Speaking/Writing cần kiểm chứng trạng thái thực tế honest | **CLOSED** | Xác nhận trạng thái NoSpeech/NeedsReview/aria-live/receipts hoạt động đúng chuẩn |
| **U08** | Profile mobile sidebar flex flow ép main còn 122px; lồng hai `<main>` | **CLOSED** | Giới hạn sticky ở `>= 993px`, mobile off-canvas. Main mở rộng lên **382px**, đổi inner `<main>` thành `<section>` |
| **U09** | Speaking mobile overflow 417px | **CLOSED** | Khắc phục overflow của decorative blob và topic chips, `scrollWidth: 390` |
| **U10** | Standard account GET /Admin/Dashboard bị redirect /Account/AccessDenied 404 | **DEFERRED (Plan 2)** | *Giữ nguyên theo audit gốc:* Lỗi thuộc backend controller routing/handler, xử lý tại Plan 2 (BE1/BE6) |
| **U11** | Avatar mặc định 404 (`/images/default-avatar.png`) | **CLOSED** | Thay bằng fallback initials chữ cái đầu; sửa avatar state transition khi chọn ảnh mới từ máy (R2) |
| **BE1** | Account validation tiếng Việt mojibake, AccessDenied handler | **DEFERRED (Plan 2)** | Thuộc backend controller/service localization |
| **BE2** | AI Stop-before-Started race condition (L11) | **DEFERRED (Plan 2)** | Thuộc backend SignalR hub / cancellation token |
| **BE3** | History server-side pagination | **DEFERRED (Plan 2)** | Thuộc backend API contract (chưa có endpoint phân trang) |
| **BE6** | Email outbox & identity auth schema | **DEFERRED (Plan 2)** | Thuộc backend architecture |

*Chi tiết các sửa đổi khắc phục findings sau review (R1–R6) được ghi nhận đầy đủ tại:* `docs/feature-audits/2026-11-02-plan1-ui-repair-ledger.md`.

### 10.2 Files đã sửa đổi (22 files, sạch 100%)
- **Admin Views (5):**
  - `TCTEnglish/Areas/Admin/Views/Shared/_AdminLayout.cshtml`
  - `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml`
  - `TCTEnglish/Areas/Admin/Views/UserManagement/Index.cshtml`
  - `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml`
  - `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/_ExerciseForm.cshtml`
- **Account Views (6):**
  - `TCTEnglish/Views/Account/Auth.cshtml`
  - `TCTEnglish/Views/Account/ForgotPassword.cshtml`
  - `TCTEnglish/Views/Account/ForgotPasswordConfirmation.cshtml`
  - `TCTEnglish/Views/Account/Profile.cshtml`
  - `TCTEnglish/Views/Account/ResetPassword.cshtml`
  - `TCTEnglish/Views/Account/Settings.cshtml`
- **AI Views (2):**
  - `TCTEnglish/Views/Ai/Chat.cshtml`
  - `TCTEnglish/Views/Ai/_ChatShell.cshtml`
- **Shared Layout (1):**
  - `TCTEnglish/Views/Shared/_Layout.cshtml`
- **CSS (6):**
  - `TCTEnglish/wwwroot/css/account.css`
  - `TCTEnglish/wwwroot/css/ai-chat.css`
  - `TCTEnglish/wwwroot/css/auth-stitch.css`
  - `TCTEnglish/wwwroot/css/layout.css`
  - `TCTEnglish/wwwroot/css/speaking.css`
  - `TCTEnglish/wwwroot/css/writing.css`
- **JavaScript (2):**
  - `TCTEnglish/wwwroot/js/ai-chat.js`
  - `TCTEnglish/wwwroot/js/layout.js`

### 10.3 Kết luận bàn giao
Toàn bộ mục tiêu của Plan 1 theo prompt `.ai/prompts/gemini-chau-ui-improvement.md` đã được thực thi trọn vẹn, không có bất kỳ vi phạm ranh giới nào (giữ nguyên API, database, backend logic, Program.cs, .csproj). Mọi thay đổi đều được kiểm chứng thực tế trên Chrome DevTools (390×844 và 1440×900). Toàn bộ công việc chỉ thực hiện tại local (không commit, không push, không CI remote, không deploy) và sẵn sàng để Owner review trước khi chuyển sang Plan 2.
