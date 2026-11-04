# Plan 1 UI Review Repair Execution Ledger — 02/11/2026

Nhánh: `feature/chau-ui-performance-audit` (base `210bf2d`).
Thực thi sửa chữa theo:
- Review: `docs/feature-audits/2026-11-02-gemini-plan1-review.md`
- Repair plan: `docs/superpowers/plans/2026-11-02-plan1-ui-review-repair.md`
- Prompt: `.ai/prompts/gemini-plan1-ui-review-repair.md`

Nguyên tắc:
- Single agent, local only, không commit/push/CI/merge/deploy, gate giữ false.
- Bảo tồn toàn bộ diff 22 files UI của Plan 1, tài liệu audit/plan gốc và BTH2.
- Giữ nguyên API backend, database, Program.cs, .csproj; không làm Plan 2.
- Mỗi finding kiểm chứng theo Red -> Green -> Regression.

---

## Bảng theo dõi tiến độ Repair (R0 – R6)

| Giai đoạn | Nội dung | Mục tiêu | Trạng thái |
|---|---|---|---|
| **R0** | Snapshot & Baseline | Kiểm tra môi trường, dirty diff, services, khởi tạo ledger | **HOÀN THÀNH** |
| **R1** | AI Chat Submit Guard (P1) | Ngăn gửi lặp bằng Enter/requestSubmit khi in-flight, giữ draft, đồng bộ lock/unlock | **HOÀN THÀNH** |
| **R2** | Avatar State Transition (P2) | Sửa preview ảnh mới chọn sau khi bị fallback error/no-avatar | **HOÀN THÀNH** |
| **R3** | AI Chat Viewport Thấp (P2) | Khắc phục min-height: 220px cắt dòng cuối ở 390x500 khi cuộn | **HOÀN THÀNH** |
| **R4** | Sidebar Tab Order & ARIA (P2) | Đồng bộ `inert`, `aria-expanded` khi sidebar mobile đóng | **HOÀN THÀNH** |
| **R5** | Admin Skip Link mobile (P2) | Đưa focused skip link lên trên sticky navbar (z-index/layering) | **HOÀN THÀNH** |
| **R6** | Verification, Tests & Ledger | Fresh build, fresh tests (Plan1Review), đồng bộ ledger gốc trung thực | **HOÀN THÀNH** |

---

## 1. R0 — Baseline & Snapshot

- **Branch:** `feature/chau-ui-performance-audit`
- **HEAD:** `210bf2d`
- **Dirty diff Plan 1:** 22 UI files (`_AdminLayout`, 3 Admin views, `_ExerciseForm`, 6 Account views, 2 AI views, `_Layout`, 6 CSS files, 2 JS files) + tài liệu audits/plans/bug-fix-log.
- **Dịch vụ đang chạy:**
  - MariaDB port 3307 (`tctenglish-mariadb-local`, healthy).
  - Web Release port 5112 (PID 10876, `/health/ready` -> `ready`).
  - Headless Chrome remote debugging port 9222 (daemon task-84).
- **Target findings cần sửa:** R1 (P1: Enter submit duplicate), R2 (P2: avatar preview), R3 (P2: chat short viewport scroll clip), R4 (P2: hidden sidebar tab/aria), R5 (P2: admin skip link z-index).

---

## 2. R1 — AI Chat Submit Guard (P1)

- **Vấn đề:** Khi request đang in-flight (`state === 'sending'`), người dùng nhấn Enter trên `messageInput` (textarea) hoặc gọi `elForm.requestSubmit()`, submit handler trước đó không có guard đồng bộ tại đầu hàm, dẫn tới việc xóa trắng nội dung draft của người dùng (`elInput.value = ''`), chèn thêm bubble user thứ hai vào DOM và gửi yêu cầu trùng lặp lên transport.
- **Giải pháp:**
  - Thêm cờ `let isSubmitting = false;` trong phạm vi hàm `init()` của `ai-chat.js`.
  - Guard đồng bộ tại đầu `elForm.addEventListener('submit', ...)`: nếu `isSubmitting || elSendBtn.disabled || elForm.dataset.state === 'sending'` thì `return` ngay lập tức, trước khi bất kỳ DOM manipulation, xóa draft hay gọi network nào diễn ra.
  - Đặt `isSubmitting = true;` ngay sau khi xác thực nội dung không rỗng.
  - Trong `elInput.addEventListener('keydown', ...)`: thêm điều kiện kiểm tra `isSubmitting || elSendBtn.disabled || elForm.dataset.state === 'sending'` trước khi gọi `requestSubmit()`.
  - Trong `elQuickActions.addEventListener('click', ...)`: thêm điều kiện kiểm tra tương tự.
  - Trong `finally` của submit handler: đặt `isSubmitting = false;` để mở khóa cho các lượt gửi tiếp theo (cả khi success và khi error).
- **Kiểm chứng:**
  - Unit regression test: `scripts/tests/test_ai_chat_submit_guard.cjs` (4/4 tests passed).
  - Browser test trên Chrome DevTools (`http://127.0.0.1:5112/AI/Chat`): Giữ pending fetch, gõ tin 2 và nhấn Enter/gọi direct `requestSubmit()`; kết quả: `draftPreserved: true` (tin 2 vẫn nguyên vẹn trong textarea), `bubblesCount: 1` (chỉ có 1 bubble, không bị lặp), nút `sendBtn.disabled === true`. Sau khi tin 1 resolve, tin 2 gửi thành công (`bubblesCount: 2`, `inputCleared: true`).

---

## 3. R2 — Avatar State Transition (P2)

- **Vấn đề:** Khi tài khoản chưa có avatar (hoặc avatar cũ lỗi bị kích hoạt `onerror`), thẻ `<img>` có `style="display:none;"` và thẻ fallback initials có `style="display:flex;"`. Khi người dùng chọn một ảnh mới từ máy tính, hàm `previewImage` cũ chỉ gán `preview.src = e.target.result` mà không gỡ bỏ `display: none` của ảnh và không ẩn thẻ initials, khiến ảnh mới chọn không bao giờ hiển thị trên giao diện trước khi upload.
- **Giải pháp:**
  - Trong `Profile.cshtml`, cập nhật hàm `previewImage(input)`:
    - Khi FileReader đọc file ảnh thành công: đặt `preview.src = e.target.result; preview.style.display = '';` và `fallback.style.display = 'none';`.
    - Khi file không hợp lệ hoặc đọc lỗi: đảm bảo `preview.style.display = 'none';` và `fallback.style.display = 'flex';`.
    - Bổ sung `onerror` đầy đủ trên cả nhánh có avatar và nhánh không có avatar để luôn fallback về initials nếu URL/ảnh lỗi.
- **Kiểm chứng:**
  - Chạy kịch bản browser trên Chrome DevTools với DataTransfer 1px PNG trên tài khoản no-avatar:
    - Ban đầu: `preview: "none"`, `fallback: "flex"`.
    - Sau khi chọn file: `preview: "inline"`, `fallback: "none"`, `srcValid: true`.
    - Sau khi kích hoạt lỗi: `preview: "none"`, `fallback: "flex"`.
    - `allPassed === true`.

---

## 4. R3 — AI Chat Viewport Thấp (P2)

- **Vấn đề:**
  - Trong `TCTEnglish/wwwroot/css/ai-chat.css`, hai media queries `@media (max-width: 767.98px)` (dòng 801) và `@media (max-width: 425px)` (dòng 862) áp đặt cứng `min-height: 220px;` lên `.ai-chat-window`.
  - Trên màn hình chiều cao thấp (ví dụ: 390x500 portrait hoặc 844x390 landscape), `.ai-chat-window-wrapper` chỉ còn khoảng 127px khả dụng sau khi trừ header và composer.
  - Do `.ai-chat-window-wrapper` có `overflow: hidden;`, phần 93px dôi dư của `.ai-chat-window` bị wrapper cắt cụt hoàn toàn. Khi cuộn tới đáy (`scrollTop = scrollHeight`), dòng tin nhắn cuối cùng (`msg-20`) nằm ở tọa độ bottom = 506.5px, vượt quá đáy của wrapper (427.0px), khiến người dùng không thể nhìn thấy dòng cuối cùng dù đã cuộn hết cỡ (`clippedPixels: 79.5px`).
- **Giải pháp:**
  - Thay thế `min-height: 220px;` bằng `min-height: 0; height: 100%;` tại cả hai media queries trong `ai-chat.css`.
  - Bổ sung media query `@media (max-height: 550px)` để tối ưu hóa không gian hiển thị trên màn hình ngắn/landscape: thu gọn header, ẩn kicker/subtitle dư thừa, tinh chỉnh padding wrapper và composer để tối đa hóa không gian đọc tin nhắn.
- **Kiểm chứng:**
  - **Red state:** Viewport 390x500, nạp 20 tin nhắn, `scrollTop = scrollHeight`:
    - `wrapperBottom: 427.01px`, `chatBottom: 517.03px`, `lastMsgBottom: 506.51px`.
    - `isLastMsgClippedByWrapper: true`, `clippedPixels: 79.5px`.
  - **Green state:** Viewport 390x500 sau sửa đổi:
    - `wrapperHeight: 197.67px`, `wrapperBottom: 445.60px`, `chatHeight: 197.67px`, `chatBottom: 445.60px`.
    - `chatMinHeightStyle: "0px"`, `lastMsgBottom: 438.32px`.
    - `isLastMsgClippedByWrapper: false` (chênh lệch -7.28px, nằm trọn vẹn trong vùng hiển thị).
    - Mở rộng textarea 6 dòng (`inputHeight: 120px`): `wrapperHeight: 143.67px`, `chatHeight: 143.67px`, `isLastMsgClippedByWrapper: false`.
  - **Landscape 844x390:** `wrapperHeight: 55.42px`, `chatHeight: 55.42px`, `lastMsgBottom: 368.04px`, `wrapperBottom: 377.40px`, `isLastMsgClippedByWrapper: false`.
  - **Standard mobile 390x844:** `chatHeight: 482.98px` (`>= 220px: true`), `isLastMsgClipped: false`.
  - **Desktop 1440x900:** `chatHeight: 469.64px` (`>= 220px: true`), `isLastMsgClipped: false`.

---

## 5. R4 — Sidebar Tab Order & ARIA Synchronization (P2)

- **Vấn đề:**
  - Trên mobile (`<= 768px` với layout chính, `<= 992px` với Account và Admin), sidebar đóng được ẩn bằng `left: -260px` hoặc `transform: translateX(-100%)` ngoài màn hình nhưng không được đánh dấu `inert` hoặc loại khỏi Tab order.
  - Khi người dùng điều hướng bằng bàn phím (Tab), tiêu điểm nhảy vào 17 liên kết/nút bấm nằm ngoài màn hình (tọa độ x = -226px).
  - Nút bấm toggle menu trên Account (`#sidebarMobileToggle`) và Admin (`#sidebarToggle`) không cập nhật thuộc tính `aria-expanded` ("true"/"false") theo trạng thái mở/đóng thực tế.
- **Giải pháp:**
  - Trong `TCTEnglish/wwwroot/js/layout.js`:
    - Thêm helper `syncSidebarState()` đồng bộ theo `window.matchMedia("(max-width: 768px)")`.
    - Khi đóng trên mobile: `sidebar.inert = true; menuBtn.setAttribute("aria-expanded", "false");`.
    - Khi mở trên mobile: `sidebar.inert = false; menuBtn.setAttribute("aria-expanded", "true");`.
    - Khi đóng bằng nút toggle/Escape: hoàn trả focus về `menuBtn` trước khi gán `inert` để tránh bẫy focus vào phần tử trơ.
    - Khi ở desktop (`> 768px`): luôn đảm bảo `sidebar.inert = false;`.
    - Bổ sung `asp-append-version="true"` cho `layout.js` trong `_Layout.cshtml` để đảm bảo trình duyệt nạp script mới nhất.
  - Trong `Profile.cshtml` và `Settings.cshtml`:
    - Thêm helper `syncAccountSidebar()` đồng bộ theo `window.matchMedia("(max-width: 992px)")`.
    - Khi đóng: `sidebar.inert = true; toggleBtn.setAttribute("aria-expanded", "false");`.
    - Khi mở: `sidebar.inert = false; toggleBtn.setAttribute("aria-expanded", "true");`.
    - Desktop (`> 992px`): `sidebar.inert = false; toggleBtn.setAttribute("aria-expanded", "false");`.
  - Trong `_AdminLayout.cshtml`:
    - Bổ sung helper `syncAdminSidebarState()` đồng bộ theo `window.matchMedia("(max-width: 992px)")`.
    - Đồng bộ `inert`, `aria-expanded`, click-outside và phím Escape hoàn trả tiêu điểm về `#sidebarToggle`.
- **Kiểm chứng:**
  - **Red state:**
    - Main `/Goals` 390px: `sidebarInert: false`, `isOffscreen: true`, `focusableLinksCountInClosedSidebar: 17`.
    - Account `Profile` 390px: `initialInert: false`, mở sidebar `openedAriaExpanded: "false"`.
    - Admin `UserManagement` 390px: `adminSidebarInert: false`, `toggleAriaExpanded: "false"`.
  - **Green state:**
    - Main `/Goals` 390px: `initialInert: true`, `initialAria: "false"`, mở: `openedInert: false, openedAria: "true"`, đóng: `closedInert: true, closedAria: "false"`, `focusReturnedToMenuBtn: true`, `canAnyDescendantBeFocused: false` (0/17 liên kết nhận focus khi đóng).
    - Desktop `/Goals` 1440px: `sidebarInert: false`, `menuBtnAriaExpanded: "true"`, `canFocusDesktopSidebarLink: true`.
    - Account `Profile` & `Settings` 390px: `initialInert: true`, `initialAria: "false"`, mở: `openedInert: false, openedAria: "true"`, đóng: `closedInert: true, closedAria: "false"`.
    - Admin `UserManagement` 390px: `initialSidebarInert: true`, `initialToggleAria: "false"`, mở: `openedSidebarInert: false, openedToggleAria: "true"`, đóng: `closedSidebarInert: true, closedToggleAria: "false"`.

---

## 6. R5 — Admin Skip Link Mobile (P2)

- **Vấn đề:**
  - Trong `TCTEnglish/Areas/Admin/Views/Shared/_AdminLayout.cshtml`, skip link có class `btn btn-primary position-absolute top-0 start-0 m-2 z-3`.
  - Thanh navbar của Admin layout dùng Bootstrap `.sticky-top` có `position: sticky; top: 0; z-index: 1020;`.
  - Do `z-index: 3 < 1020`, khi người dùng bấm Tab trên mobile (390x844), skip link xuất hiện tại (x: 8, y: 8) nhưng bị che khuất hoàn toàn phía sau thanh navbar (`elementFromPoint` tại tâm skip link trả về thẻ `<nav class="navbar ... sticky-top">`).
- **Giải pháp:**
  - Trong `_AdminLayout.cshtml`, định nghĩa class `.admin-skip-link` với `z-index: 1060 !important;` (cao hơn cả navbar `1020` và sidebar `1050`).
  - Khi được focus:
    ```css
    .admin-skip-link:focus,
    .admin-skip-link:focus-visible {
        position: fixed !important;
        top: 12px;
        left: 12px;
        z-index: 1060 !important;
        box-shadow: 0 0 0 3px rgba(66, 85, 255, 0.4), 0 4px 12px rgba(0,0,0,0.2) !important;
    }
    ```
  - Thay đổi vị trí của skip link thành `position-fixed` với class `admin-skip-link`.
- **Kiểm chứng:**
  - **Red state:** Mobile 390x844: `skipLink.focus()`, `hitElementTagName: "NAV"`, `isCoveredByNavbar: true`.
  - **Green state:** Mobile 390x844:
    - `skipLink.focus()`, `hitElementTagName: "A"`, `hitElementText: "Chuyển đến nội dung chính"`.
    - `isSkipLinkHitDirectly: true`.
    - `computedZIndex: "1060"`.
  - **Desktop control 1440x900:** `viewportWidth: 1440`, `isSkipLinkHitDirectly: true`, `computedZIndex: "1060"`.

---

## 7. R6 — Verification, Tests & Final Diff Review

- **Kiểm tra cú pháp và định dạng:**
  - Lệnh: `git diff --check`
  - Kết quả: `exit code 0` (không có trailing whitespace hay conflict markers).
- **Kiểm thử tự động với Fresh Binaries (Isolated Configuration):**
  - Môi trường: Tắt OpenAI live flags (`$env:TCT_OPENAI_LIVE='0'; $env:TCT_OPENAI_FEATURE_LIVE='0'`).
  - Lệnh:
    ```powershell
    dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Plan1Review -p:Optimize=true --logger "trx;LogFileName=plan1-ui-repair.trx" --results-directory .tmp/plan1-ui-repair --verbosity quiet
    ```
  - File kết quả: `.tmp/plan1-ui-repair/plan1-ui-repair.trx`
  - Kết quả: **Failed: 0, Passed: 811, Skipped: 8, Total: 819** (Thời gian chạy: 2 m 52 s).
  - Khẳng định: 811 executed pass / 8 not run (skipped guards: 2 OpenAI live, 3 ASR/media live, 3 MariaDB acceptance). Không gọi là 100% toàn bộ suite đã chạy.
- **Bảo toàn phạm vi:**
  - Giữ nguyên các tệp cấu hình lõi: `Program.cs`, `appsettings.json`, `.csproj`, migrations và API contracts.
  - Giữ nguyên toàn bộ 22 tệp UI của Plan 1 ban đầu + tài liệu audit/plan và BTH2.
  - Công việc hoàn toàn cục bộ trên máy local, tuân thủ nghiêm ngặt quy định không commit, không push, không remote CI, không merge và không deploy.
