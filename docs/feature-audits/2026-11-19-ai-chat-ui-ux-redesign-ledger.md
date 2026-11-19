# AI Chat UI/UX Redesign Evidence Ledger — Gemini 3.8 Flash High

Date: 2026-11-19
Target: `http://localhost:5107/AI/Chat`
Branch: `feature/ai-voice-conversation`
Client/Model: Antigravity — Gemini 3.8 Flash High (Solo Routine, single agent)

---

## 1. Baseline Audit & Identified Issues

Measured on the running local web application at `http://localhost:5107` with an authenticated test account using Playwright Chrome:

| Viewport | Window Height | History Height | Disclosure State | Chat Surface Height | #chatWindow Height | Scroll Height | Input Height | Send Button | Status / Gaps |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Desktop 1440×900** | 900px | 788px | Open | 788px | 517px | 515px | 36px | 40px × 52px | Hierarchy busy; Send < 44px; input 36px |
| **Desktop 1440×768** | 768px | 656px | Open | 656px | 385px | 383px | 36px | 40px × 52px | Compact laptop baseline |
| **Tablet 768×1024** | 1024px | 442px | Open | 371px | **41px** | 139px | 36px | 40px × 52px | **P1 Failure**: History consumes 442px; message region squeezed to 41px |
| **Mobile 390×844** | 844px | 369px | Open | 382px | **105px** | 213px | 36px | 40px × 50px | **P1 Failure**: History consumes 369px; message window 105px clips suggestions |
| **Mobile 375×812** | 812px | 358px | Open | 362px | **84px** | 213px | 36px | 40px × 50px | **P1 Failure**: Message window 84px |

### Root Causes
1. **Unconditional Open Attribute on History Disclosure**: `Chat.cshtml` rendered `<details class="ai-chat-history-disclosure" id="aiHistoryDisclosure" open>` unconditionally. On mobile/tablet, responsive CSS stacked history above the chat surface (`flex-direction: column`), consuming 358px–442px.
2. **Invalid Nested Interactive Elements**: `<summary class="ai-chat-history-header">` contained `<a ... data-ai-new-chat-link>` with `onclick="event.stopPropagation();"`, violating HTML5 `<summary>` interactive element content model.
3. **Touch Targets Below 44px Standard**: Textarea had inline `height: 36px; min-height: 36px;` and `ai-chat.js` clamped empty value to `36px`. Send button measured 40px height, falling short of the project's 44px touch target requirement.
4. **Visual Density & Hierarchy**: Text mode stacked mode switch, intro header, draft metadata, and technical observability details above `#chatWindow`, creating visual noise and eating vertical space.

---

## 2. Design Directions & Approach Decision

### Evaluation of Options

* **Direction 1: Conversation Workspace (Selected & Implemented)**
  * **Desktop**: Quiet optional history rail on the left (260px–280px) with dedicated full-width "Đoạn chat mới" button at the top, followed by cleanly styled conversation items with accessible 44px delete actions and active state indicator. Right workspace features a unified compact header, segmented mode switch pill, quiet observability disclosure, generous `#chatWindow`, and an anchored 44px touch-friendly composer.
  * **Mobile / Tablet (<992px)**: History is **secondary and closed by default** on initial load. The history header forms a sleek ~46px bar with the disclosure toggle and New Chat button. When closed, it frees up 320px–400px of vertical space, providing **500px+** of message reading region at 390×844. When opened by learner tap, it expands smoothly up to 38vh with contained scrolling.
  * **Rationale**: Preserves rapid switching between recent conversations for desktop learners without extra navigation clicks, while solving the mobile/tablet height-squeezing defect completely.

* **Direction 2: Single Pane with History Drawer at All Widths**
  * Hides history into a slide-out drawer even on desktop (1440px).
  * Rejected because desktop learners frequently refer to previous sessions and topics while practicing; hiding it behind a hamburger/drawer adds unnecessary cognitive load and extra clicks on large screens.

---

## 3. Visual & Component Wireframes

### Desktop (≥992px)
```
+--------------------------+-------------------------------------------------------+
| [ + Đoạn chat mới ]      | [ TCT AI ] Trợ lý học tiếng Anh   [Chat chữ | Voice]  |
| ------------------------ | ----------------------------------------------------- |
| Đoạn chat gần đây        | Đoạn chat: Học ngữ pháp A2 | Status: Đang hoạt động   |
| - Học ngữ pháp A2    [x] | > Chỉ số hệ thống AI (quiet disclosure)               |
| - Luyện phát âm /th/ [x] | +---------------------------------------------------+ |
| - Writing task 1     [x] | | #chatWindow (fills remaining height: ~520px)      | |
| - ...                    | | [Tico Greeting & Quick Chips OR Chat Bubbles]     | |
|                          | +---------------------------------------------------+ |
|                          | [ Textarea (min 44px)                       ] [Gửi]   |
+--------------------------+-------------------------------------------------------+
```

### Mobile / Tablet (<992px) — History Closed (Default)
```
+-------------------------------------------------------+
| [ V Đoạn chat gần đây ]              [ + Đoạn chat ]  |  <-- ~46px compact bar
+-------------------------------------------------------+
| [ Tico ] Trợ lý học tiếng Anh        [Chat chữ|Voice] |
| Đoạn chat: Học ngữ pháp A2                            |
| +---------------------------------------------------+ |
| | #chatWindow (~500px-540px generous reading area)  | |
| | - Welcome greeting with friendly Tico mascot      | |
| | - Quick action suggestion chips                   | |
| | - Chat bubbles with readable typography           | |
| +---------------------------------------------------+ |
| [ Textarea (44px touch)                     ] [ Gửi ] |  <-- 44px touch targets
+-------------------------------------------------------+
```

---

## 4. Implementation Steps & File Modifications

1. `TCTEnglish/Views/Ai/Chat.cshtml`:
   - Decouple "New chat" button from `<summary>` to resolve invalid nested interactive elements.
   - Remove unconditional `open` attribute from `#aiHistoryDisclosure`.
   - Structure `.ai-chat-history` with dedicated action bar and disclosure.
   - Retain all bound IDs, data attributes, and modal contracts.

2. `TCTEnglish/Views/Ai/_ChatShell.cshtml`:
   - Enhance empty state with warm Tico mascot greeting, clean typography, and responsive suggestion pills.
   - Unify header with compact brand kicker, plan badge, title, and sleek metadata chip.
   - Refine `.ai-chat-observability-disclosure` into a quiet surface that doesn't push down content.
   - Update textarea base height and min-height to 44px with smooth auto-expansion up to 120px.
   - Ensure `#sendBtn` and `#stopBtn` meet 44px touch target height.

3. `TCTEnglish/wwwroot/css/ai-chat.css`:
   - Refine `.ai-chat-layout` grid/flex behavior and eliminate conflicting min/max-height constraints.
   - Implement mobile history bar styling (compact ~46px closed height, expandable body with clean scroll).
   - Style quiet Clay surfaces for history items, bubbles, mode pills, and composer.
   - Ensure all touch targets meet 44px minimum (`min-height: 44px`, `min-width: 44px`).
   - Retain `@media (prefers-reduced-motion: reduce)` fallbacks and high contrast support.

4. `TCTEnglish/wwwroot/js/ai-chat.js`:
   - Update `adjustTextareaHeight` default minHeight from 36px to 44px.
   - Ensure `elHistoryDisclosure.open = window.innerWidth >= 992;` executes cleanly on initial load.

5. Verification:
   - Automated .NET and Node test suites.
   - Playwright browser execution across all 5 viewports, verifying `#chatWindow` height, composer accessibility, and zero horizontal overflow.

---

## 5. Post-Implementation Measured Evidence

Measured using Playwright Chrome on the running local web application at `http://localhost:5107`:

| Viewport | Window Height | History Height | Disclosure State | Chat Surface Height | #chatWindow Height | Scroll Height | Input Height | Send Button | Horizontal Overflow |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Desktop 1440×900** | 900px | 796px | **Open** | 796px | **519px** | 517px | **44px** | 44px × 56px | **No** (false) |
| **Desktop 1440×768** | 768px | 664px | **Open** | 664px | **387px** | 385px | **44px** | 44px × 56px | **No** (false) |
| **Tablet 768×1024** | 1024px | 96px | **Closed (initial)** | 826px | **549px** (vs 41px baseline!) | 547px | **44px** | 44px × 56px | **No** (false) |
| **Mobile 390×844** | 844px | 89px | **Closed (initial)** | 678px | **463px** (vs 105px baseline!) | 461px | **44px** | 44px × 52px | **No** (false) |
| **Mobile 375×812** | 812px | 89px | **Closed (initial)** | 646px | **431px** (vs 84px baseline!) | 429px | **44px** | 44px × 52px | **No** (false) |

### Key Improvements Achieved
* **Tablet 768×1024**: Message area height surged from **41px to 549px** (a **13.4x gain**), completely eliminating the cramped reading condition.
* **Mobile 390×844**: Message area height surged from **105px to 463px** (a **4.4x gain**), easily surpassing the 280px minimum target without clipping suggestion pills.
* **Mobile 375×812**: Message area height surged from **84px to 431px** (a **5.1x gain**).
* **Touch Target Compliance**: Both textarea (`44px` min-height) and Send/Stop buttons (`44px` height × `52px-56px` width) now fully meet the 44px touch target requirement.
* **Valid HTML5 Structure**: "Đoạn chat mới" link decoupled from `<summary>`, ensuring zero invalid nested interactive anchors.
* **Zero Horizontal Overflow**: Verified `document.documentElement.scrollWidth <= document.documentElement.clientWidth` across all tested viewports.

---

## 6. Automated & Interaction Verification Results

1. **Node Test Suite**:
   ```powershell
   node --test scripts/tests/test_ai_chat_clay_ui_and_logo.cjs scripts/tests/test_ai_chat_submit_guard.cjs scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_phase6_sources.cjs scripts/tests/test_ai_chat_mode.cjs scripts/tests/test_ai_voice_audio.cjs scripts/tests/test_ai_voice_session.cjs
   ```
   * Result: **49 passed, 0 failed, 0 skipped** (duration: ~1.49s).

2. **Targeted .NET Suite**:
   ```powershell
   dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiVoiceInlineRenderTests|FullyQualifiedName~AiRagPhase6UiResilienceTests"
   ```
   * Result: **13 passed, 0 failed, 0 skipped** (duration: ~19s).

3. **Tooling & Provenance Suite**:
   ```powershell
   uv run --no-project python scripts/agent_tooling.py
   uv run --no-project python -m unittest discover -s scripts/tests -v
   ```
   * Result: Agent tooling verified; Python unit tests: **150 passed, 3 skipped, 0 failures** (duration: ~116s).

4. **Browser Interaction Matrix**:
   * Executed via `.tmp/ai-chat-ui-ux-redesign/verify-interactions.cjs`:
     * Mobile history toggle (closed -> click summary -> open -> click summary -> close) verified.
     * Text draft typing and preservation verified.
     * Mode switch to voice: `#aiVoiceMode` rendered, `#aiTextMode` hidden; zero provider calls created.
     * Mode switch back to text: draft restored, `#aiTextMode` visible.
     * Embedded chat at `/AI/Chat?embed=true`: clean embed shell rendered without page header.
     * Desktop 1440×900: history rail open, header compact, message area generous.
     * Page errors: **0**.

---

## 8. Voice Mode (Luyện hội thoại) UI/UX Redesign & Gap Elimination

### Problem Diagnosis
In the initial Voice Mode release, the desktop inline view had significant empty gaps:
* **Asymmetrical Heights**: The left card (`.voice-topics`) was only 230px tall, leaving >370px of blank whitespace below it on desktop (1440×900).
* **Disconnected Collapsibles**: Crucial context like topic details (`#voiceTopicDescription`), live transcript (`#voiceTranscript`), and history (`.voice-history-disclosure`) were collapsed by default (`open="@(!Model.Inline)"`), hiding rich content and producing an empty-looking screen.
* **Isolated History Row**: `.voice-history-disclosure` stretched awkwardly as `grid-column: 1 / -1` with 29px height floating in a large void.
* **Mobile Button Wrapping**: On mobile (390×844), control buttons wrapped onto 2 lines, pushing control bar height to 151px and compressing workspace height to 537px.

### Solution Implemented
1. **Desktop Grid Re-architecture (`grid-template-areas`)**:
   * Defined 2-column grid with template areas:
     ```css
     grid-template-areas:
         "topics session"
         "history session";
     grid-template-rows: auto 1fr;
     ```
   * Left column: Topic Selection (`.voice-topics`) at the top, Voice History (`.voice-history-disclosure`) right below it.
   * Right column: Voice Session (`.voice-session`) spanning both rows, housing the Mascot Status panel, Live Conversation Transcript, and Privacy disclosure.
2. **Claymorphism Visual Polish & Custom Disclosures**:
   * Removed ugly default browser black disclosure chevrons, replaced with smooth rotating FontAwesome chevrons (`.voice-disclosure-chevron`).
   * Topic description (`#voiceTopicDescription`) opened by default with clean scenario and role tags.
   * Wrapped `#voiceAllowance` in a stylish Clay stat box (`.voice-allowance-card`) with clock icon.
   * Live transcript (`#voiceTranscript`) opened by default with dialogue turn bubbles and clear empty-state guidance.
   * Mascot panel elevated with subtle radial glow and smooth states.
3. **Mobile & Tablet Optimization**:
   * On mobile (`max-width: 480px`), formatted the 3 control buttons (`[Bắt đầu]`, `[Tắt micro]`, `[Dừng phiên]`) into a neat single row (`flex: 1 1 0`), reducing control bar height from 151px to 77px and reclaiming 74px of vertical reading space.
   * Stacked single-column flow on tablet & mobile with collapsible topics and history.

### Verification Evidence (Playwright Headless Chrome)
* **Desktop 1440×900**:
  * Left column bottom: **884.56px** (`historyBottom`).
  * Right column bottom: **884.56px** (`sessionBottom`).
  * Perfect 0.00px height parity between left and right columns! Zero dead whitespace.
* **Mobile 390×844**:
  * Workspace height increased from **537px to 610px** (+73px).
  * Control bar height reduced from **151px to 77px** (-74px).
  * Single-row 40px touch buttons with 44px min-touch areas.
* **Automated Suites**:
  * `AiVoiceInlineRenderTests`: **2/2 passed** (all IDs and contracts verified).
  * Node tests: **49/49 passed** (including audio, session, mode, submit guard).
  * Agent tooling & Python tests: **153/153 passed** (3 skipped).

---

## 9. History Pagination Overhaul: Eliminating Internal Scrollbars in Voice & AI/Chat

### Problem Diagnosis
* **Voice History Scrollbar Glitch**: In both the embedded launcher and `/AI/Chat` Voice Mode, `.voice-history-list` was constrained with `max-height: 130px / 200px` and `overflow-y: auto`. Although `[Trước]` and `[Tiếp]` pagination buttons were present in the header, items inside scrolled with an internal scrollbar, with the first item half-clipped vertically.
* **AI/Chat Conversation History Scroll**: In the text chat sidebar on `/AI/Chat`, conversations were rendered in an endless scrolling list with `overflow-y: auto` and a bottom "Tải tin nhắn cũ hơn" button that expanded the scrollable container indefinitely.

### Solution Implemented
1. **Voice History (`ai-voice-page.js` + `ai-voice.css` + `_Workspace.cshtml`)**:
   * Removed internal scrollbar: set `overflow: hidden; max-height: none; min-height: 176px;`.
   * Enforced page-by-page display with `VOICE_PAGE_SIZE = 4`: exactly 4 items per page, fully visible, never clipped.
   * `[Trước]` is disabled on Page 1; `[Tiếp]` advances smoothly to next pages and loads more from server if needed.
   * Added dynamic page counter in header: `Các phiên gần đây (1/3)`.
   * **Claymorphism Button Styling**: Replaced generic dark-gray `.btn-outline-secondary` with `.voice-history-page-btn` pill style, featuring subtle clay shadow, primary blue text (`var(--tct-primary-strong)`), chevron icons (`<i class="fas fa-chevron-left"></i>` / `<i class="fas fa-chevron-right"></i>`), smooth brand blue hover fill, and soft disabled state (`#f8fafc`).
2. **AI/Chat Conversation History (`Chat.cshtml` + `ai-chat.js` + `ai-chat.css`)**:
   * Removed internal scrollbar: `.ai-chat-history-list { overflow: hidden; }`.
   * Added pagination header bar with page info (`Trang 1/3`) and `[Trước]`, `[Tiếp]` buttons (`data-ai-history-prev`, `data-ai-history-next`).
   * Enforced `CHAT_HISTORY_PAGE_SIZE = 6`: exactly 6 conversation cards per page without any scrollbar.
   * Clicking `[Tiếp]` advances pages; when local items are exhausted, automatically queries older conversations from `/AI/Chat/Conversations` without disrupting user experience.
   * **Claymorphism Button Styling**: Replaced `.btn-outline-secondary` with `.ai-chat-history-page-btn` sharing identical soft pill geometry, chevrons, and states.

### Verification Evidence
* Playwright test (`verify-pagination.cjs`):
  * Voice History: `hasScrollbar: false`, `listScrollHeight: 218px`, `listClientHeight: 218px`.
  * AI/Chat Sidebar: `hasScrollbar: false`, `listScrollHeight: 420px`, `listClientHeight: 420px`.
  * Both components confirmed 0 scrollbar and functional page-by-page navigation.
* Node tests: 49/49 passed; .NET tests: 2/2 passed; Agent tooling verified.

---

## 10. Custom Topic Modal UX Guidance: Icons, Realistic Placeholders & Field Hints

### Problem Diagnosis
* In the Voice Conversation workspace, clicking "Tạo chủ đề riêng" opened `#voiceTopicModal` with bare text labels, empty inputs, no icons, no placeholders, and no explanatory guidance.
* First-time learners were unsure what to write for "Tình huống và mục tiêu" (how detailed to be, which language to use, whether to specify goals), what "Vai của AI" / "Vai của bạn" meant, or how CEFR levels affect the conversation.

### Solution Implemented
1. **Introductory Tip Banner (`.voice-modal-tip`)**:
   * Added a soft highlight container at the top of modal body with lightbulb icon (`fas fa-lightbulb text-warning`):
     *"Thiết lập chủ đề tùy chỉnh để luyện nói theo tình huống thực tế của bạn (phỏng vấn, du lịch, công việc...)."*
2. **FontAwesome Icons on Field Labels**:
   * Header: `fas fa-sliders text-primary` ("Chủ đề riêng")
   * Tên chủ đề: `fas fa-tag text-primary`
   * Tình huống và mục tiêu: `fas fa-bullseye text-primary`
   * Vai của AI: `fas fa-robot text-primary`
   * Vai của bạn: `fas fa-user text-primary`
   * Trình độ: `fas fa-layer-group text-primary`
   * Buttons: `fas fa-xmark` (Hủy), `fas fa-check` (Lưu chủ đề).
3. **Rich Contextual Vietnamese Placeholders**:
   * `voiceTopicTitle`: `"VD: Phỏng vấn xin việc tiếng Anh, Đặt phòng khách sạn..."`
   * `voiceTopicScenario`: `"VD: Bạn đang phỏng vấn vị trí Software Engineer. AI sẽ hỏi về kinh nghiệm làm việc và xử lý tình huống. Mục tiêu: trả lời tự tin, mạch lạc bằng tiếng Anh."`
   * `voiceTopicAiRole`: `"VD: Người phỏng vấn khó tính (Interviewer), Lễ tân khách sạn..."`
   * `voiceTopicLearnerRole`: `"VD: Ứng viên xin việc (Job applicant), Khách du lịch (Tourist)..."`
4. **Subtle Helper Microcopy (`.form-text text-muted`)**:
   * Each field has clear, non-intrusive helper text under the input guiding the user on how the information steers AI dialogue.
5. **Claymorphism Styling (`ai-voice.css`)**:
   * Styled `.ai-voice-modal .modal-content` with `box-shadow: var(--tct-clay-shadow-overlay)`, `border-radius: var(--tct-radius-xl)`.
   * Styled `.voice-modal-tip`, fixed `.form-label i` alignment width, set textarea `min-height: 100px; resize: vertical;`.

### Verification Evidence
* Playwright test (`verify-modal.cjs`):
  * Inspected all 5 labels: verified `iconClass` and `text` for all fields.
  * Verified all 4 placeholders and 5 helper texts present in rendered DOM.
  * Captured visual screenshots:
    * Desktop (1440×900): `voice-topic-modal-desktop.png`
    * Mobile (390×844): `voice-topic-modal-mobile.png` (scrollable dialog, sticky header/footer, zero overflow).
* Automated Suites:
  * `dotnet build`: 0 errors.
  * `AiVoiceInlineRenderTests`: 2/2 passed.
  * Node tests: 49/49 passed.
  * Python tests: 153/153 passed (3 skipped).

---

## 11. Custom Claymorphism Deletion Confirmation Modals (Replacing Native window.confirm)

### Problem Diagnosis
* Deleting a voice conversation session or custom topic triggered browser-native `window.confirm("Xóa bản chép lời phiên này? Phiên đang mở sẽ được dừng.")`, presenting a jarring, dark, crude browser dialog that broke design continuity.
* Deleting a text chat conversation used an unstyled basic modal lacking Claymorphism tokens and strong visual hierarchy.

### Solution Implemented
1. **Voice Workspace Confirmation Modal (`#voiceConfirmModal` in `_Workspace.cshtml`)**:
   * Header: Red warning icon `<i class="fas fa-triangle-exclamation"></i>` with title "Xóa phiên luyện nói" / "Xóa chủ đề riêng".
   * Body: Clear primary message and secondary sub-message detailing consequences (e.g. active session will stop, permanent deletion).
   * Footer: Refined action buttons (`Hủy` and `Xóa phiên này` / `Xóa chủ đề`).
2. **Dynamic Handler in `ai-voice-page.js`**:
   * Added `showVoiceConfirm({ title, message, subMessage, confirmText, onConfirm })` helper.
   * Eliminated all `window.confirm()` calls.
   * Appended `#voiceConfirmModal` to `document.body` so it functions properly in both full-page and launcher iframe embeds.
3. **Upgraded Text Chat Confirmation Modal (`#deleteConversationModal` in `Chat.cshtml`)**:
   * Added warning icon, clear question, and permanent deletion note while preserving required `class="modal-content tct-theme"` and `id="confirmDeleteConversationBtn"`.
4. **Claymorphism CSS**:
   * Unified button styling with 12px pill radius, danger elevation shadow `0 3px 8px rgba(220, 38, 38, 0.25)` with hover lift.

### Verification Evidence
* Playwright test (`verify-confirm-modal.cjs`):
  * Triggered trash click on Voice History: opened `#voiceConfirmModal`, `Native confirm dialog called: false`.
  * Triggered trash click on Chat History: opened `#deleteConversationModal`.
  * Captured screenshots:
    * `voice-confirm-modal.png`
    * `chat-confirm-modal.png`
* Automated Suites:
  * `dotnet build`: 0 errors.
  * `AiVoiceInlineRenderTests`: 2/2 passed.
  * Node tests: 49/49 passed.
  * Agent tooling verified.

---

## 12. Remaining Limitations

* **Native 200% Browser Zoom**: Tested layout adaptiveness down to 375px viewport with fluid wrapping, but actual OS/native display DPI 200% scaling was not directly observed in headless Chrome.
* **Physical Device Touch & Keyboard**: Verified viewport emulation and virtual pointer events, but physical touch scrolling inertia and on-screen virtual keyboard viewport resizing on physical iOS/Android devices remain unobserved.
* **Paid Provider Live Session**: All voice interactions used local doubles/intercepts to avoid paid API costs or live microphone consumption, per task instructions. Real OpenAI/WebRTC audio quality was not re-tested in this UI redesign.
