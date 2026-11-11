# AI Chat UI, Interactions & Motion Redesign — Execution Ledger

## Overview & Context

- **Task**: Research, design, implement, and verify all existing AI Chat surfaces using the shared TCT Claymorphism system and purposeful motion. Deliver an original TCT AI chatbot logo to replace the temporary placeholder image, complete with vector master, monochrome variant, transparent 512px PNG export, and Vietnamese identity guide.
- **Date**: 2026-11-10 (Asia/Bangkok)
- **Mode**: `IMPLEMENT_LOCAL` (one agent investigating, designing, implementing, reviewing, and verifying inline; no workers/delegation; leave uncommitted for owner review; do not push, merge, or deploy).
- **Branch**: `feature/chau-ai-chat-clay-ui`
- **Model in Antigravity**: Gemini 3.8 Flash High
- **Preserved Unrelated Work**:
  - `.ai/prompts/bth3-class-report-logic-repair.md`
  - `docs/superpowers/plans/2026-11-08-bth3-class-report.md`
  - `docs/superpowers/plans/2026-11-09-bth3-logic-review.md`

### Surface Coverage Map

| ID | Surface / Route | Main Files | Role / Purpose |
|---|---|---|---|
| **AC01** | Full Chat & History (`/AI/Chat`, optional `conversationId`) | `TCTEnglish/Views/Ai/Chat.cshtml` | Full desktop learning workspace, conversation list, cursor paging, deletion modal/toast, prompt composer, observability disclosure. |
| **AC02** | Standalone Embedded Chat (`/AI/Chat?embed=true`) | `TCTEnglish/Views/Ai/ChatEmbed.cshtml` | Compact isolated iframe chat view. Explicitly loads `tct-learning-ui.css` and scopes `.tct-theme`, reachable composer, zero layout clipping. |
| **AC03** | Floating Launcher (`_AiChatLauncher.cshtml`) | `TCTEnglish/Views/Shared/_AiChatLauncher.cshtml`, `ai-chat-launcher.css`, `ai-chat-launcher.js` | Persistent clay trigger, authenticated dialog with same-origin iframe, lazy iframe loading, focus trap across host and frame, ESC handling, quota refresh on open. |
| **AA01** | Admin AI Management (`/Admin/AiManagement`) | `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml`, `admin-ai-management.css` | Admin status cards (model, dataset, BM25, vector/RRF), ML.NET intent classifier training with hot-reload, lexical index rebuild (dry-run/build), and vector embedding with token budget bounds. |
| **Partials** | Shared AI Shell & Sources | `TCTEnglish/Views/Ai/_ChatShell.cshtml`, `TCTEnglish/Views/Ai/_ChatSources.cshtml` | Message log, quick action suggestions, assistant & user message bubbles with clay depth, typing indicator, scroll-to-bottom button, RAG source citations with freshness/uncertainty notices. |

---

## 1. Baseline Verification & Backend Contracts

### Identity & History
- `AiController` authenticated with antiforgery protection. Conversations and messages are strictly isolated per `GetCurrentUserId()`.
- GET `/AI/Chat/Conversations` (`pageSize`, `beforeUpdatedAtUtc`, `beforeId`): Returns items and `hasMore`.
- GET `/AI/Chat/Messages` (`conversationId`, `pageSize`, `beforeCreatedAtUtc`, `beforeId`): Cursor-based paging. Prepending older messages uses DocumentFragment and scroll anchoring to preserve the learner's reading position without jumping.
- POST `/AI/Chat/Delete`: Deletes conversation with antiforgery token, validates user ownership, returns 404 for IDOR.

### SignalR Lifecycle & Stream Transport
- Connected to `/classChatHub`, calls `StartAiStream` and `StopAiStream`.
- Client receives events: `AiStreamStarted`, `AiStreamChunk`, `AiStreamCompleted`, `AiStreamFailed`.
- Generation identity & attempt counter: Stale or replayed chunks from cancelled attempts are ignored.
- Safe markdown rendering: Local pinned `marked-18.1.3.umd.js` and `purify-3.4.17.min.js`.
- HTTP fallback: Activated only if SignalR is unavailable upon initial attempt; unknown SignalR outcomes never auto-dispatch duplicates through HTTP.

### Quotas & Observability
- Standard Plan limit: 15 messages/day. Premium and Admin plans: Unlimited.
- GET `/AI/Chat/Usage`: Lazy fetch when launcher panel opens. Never assumes 0/15 before fetch completes.
- PostMessage event `tct-ai-launcher-quota-consumed` validates event origin (`window.location.origin`) and expected frame source before triggering a server usage refresh.

---

## 2. Original Chatbot Logo Design & Asset Ledger

### Comparison of Visual Directions

| Direction | Visual Concept | Pros & Cons | Decision |
|---|---|---|---|
| **A: Dialogue Spark** | Speech bubble cradling a 4-point AI spark with book fold. | Clean symbol of conversation; however, can look like a generic support chat icon at small sizes. | Explored; rejected in favor of more personal companion face. |
| **B: TCT Companion Beacon** *(Selected)* | Friendly clay bot squircle with integrated speech-bubble tail, crisp white visor, expressive eyes/smile, and golden TCT AI beacon spark atop. | Highly memorable, approachable mascot character for English learners; strong silhouette; scales down to 24px with zero loss of recognition; perfectly aligns with TCT Blue `#4255ff` and Yellow `#ffcd1f`. | **Selected for implementation.** |
| **C: T-Fold Monogram** | Abstract origami T-and-C monogram forming an open book. | Clever typography; but cold and corporate, lacking the warmth of an interactive study buddy. | Rejected. |

### Delivered Assets

| Asset Name | Relative Path | Format | Size | Characteristics & Provenance |
|---|---|---|---|---|
| **Master Vector Mark** | `TCTEnglish/wwwroot/images/ai/tct-ai-mark.svg` | SVG | 4.8 KB | Scalable vector master, viewBox 128x128, transparent background. Multi-stop clay gradients in brand `#4255ff` and `#ffcd1f`, dual soft drop shadows, self-contained, no external fonts or scripts. |
| **Monochrome Variant** | `TCTEnglish/wwwroot/images/ai/tct-ai-mark-mono.svg` | SVG | 1.9 KB | Pure single-color vector (`currentColor`) with compound path cutouts for 100% clean opacity on light or dark backgrounds. |
| **512px Transparent PNG** | `TCTEnglish/wwwroot/images/ai/tct-ai-mark.png` | PNG | 79.3 KB | Pixel-perfect 512x512 raster export rendered with headless Chromium. Crisp anti-aliased contours on transparent alpha. |
| **Legacy Fallback Image** | `TCTEnglish/wwwroot/images/ai/tct-ai-launcher.png` | PNG | 498 KB | Retained for backward compatibility with cached references. |
| **Identity Usage Guide** | `docs/design-system/ai-chatbot-identity.md` | Markdown | 4.2 KB | Comprehensive Vietnamese usage manual specifying clear space, color values, minimum sizes, and crop rules. |

### Multi-Size Verification

- **64px**: Tested on floating launcher trigger; golden spark and smile are crisp, rich clay depth visible.
- **48px**: Tested on panel header; clear contrast against gradient background.
- **40px**: Tested on modal dialog headers.
- **32px**: Standard size for message bubbles (`.ai-chat-avatar-assistant`). Eyes, mouth arc, and golden spark remain sharp.
- **24px**: **Minimum Usable Size**. Head silhouette and spark remain instantly recognizable.

---

## 3. Interaction and Motion Matrix

| Surface / Control | Trigger | Actual State Source | Visual Response | Focus & Announcement | Reduced-Motion Result | Interruption & Cleanup | Verification Evidence |
|---|---|---|---|---|---|---|---|
| **AC01: History Paging** | Click "Tải tin nhắn cũ hơn" button | Server GET `/AI/Chat/Conversations` (`hasMore`) | Button displays loading indicator; new chronological conversation rows prepend smoothly; cursor updated | Focus remains anchored to loaded list; screen reader announces new history items | Instant list update; no fade/slide transitions | Abort on tab switch; button re-enabled on error | Verified in unit & browser tests |
| **AC01: Conversation Deletion** | Click trash icon on history item | Server POST `/AI/Chat/Delete` (`conversationId`) | Modal `#deleteConversationModal` displays; confirm button shows loading; on success, item fades out and toast `#deleteSuccessToast` displays | Focus returns safely to "New chat" button or nearest remaining history item | Immediate item removal without fade animation | Modal cancel closes cleanly without mutation | Tested with modal dismiss & keyboard |
| **AC01/02: Composer Auto-Grow** | Input in `#messageInput` | `textarea.scrollHeight` | Expands smoothly from 44px up to 120px max-height; overflow scrolls after limit | Standard keyboard caret maintained; no layout thrashing | Height changes instantly without CSS transition | Cleared on successful submission | Verified with multiline text & Shift+Enter |
| **AC01/02: Send Guard & State** | Enter key / Submit button | `chatForm[data-state]` (`idle` → `sending`) | Send button changes to disabled; stop button appears; textarea is locked to prevent duplicate sends | `aria-busy="true"` announced; focus remains in composer | No pulsing or scale animation on buttons | Reset to `idle` on stream complete or failure | Tested with `test_ai_chat_submit_guard.cjs` |
| **AC01/02: Live Streaming Tokens** | SignalR `AiStreamChunk` events | Hub stream messages | Chunks append directly to current assistant bubble without re-animating parent container | Live region `aria-live="polite"` announces streaming updates | Stream text updates instantly with `scroll-behavior: auto` | `StopAiStream` safely terminates and marks stream cancelled | Tested with SignalR mock & unit tests |
| **AC01/02: Scroll to Bottom Button** | User scrolls up > 80px while messages arrive | `scrollTop + clientHeight < scrollHeight - 80` | Floating pill button `#scrollToBottomBtn` slides in smoothly (`opacity: 1`, `translateY(0)`) | `aria-hidden="false"`; keyboard reachable via Tab | Button toggles instantly without transform | Clicking button smooth-scrolls to bottom and hides pill | Verified in desktop & mobile viewports |
| **AC01/02: RAG Sources & Uncertainty** | Completed answer with citations | `message.Grounding` DTO (`status`, `outcome`, `sources`) | Clay citation panel attaches to bubble; status pill displays (`answered`, `needs_clarification`, `snapshot`); verified links | Semantic `<section>` with `aria-label`; safe external URL links | Instant rendering without accordion slide | Links open in new tab with `rel="noopener noreferrer"` | Tested with `test_ai_chat_phase6_sources.cjs` |
| **AC03: Floating Launcher Toggle** | Click launcher trigger button | `data-ai-toggle` click event | Trigger expands smoothly (`scale(0.95)` → `scale(1)`); backdrop fades in; panel slides up | `aria-expanded` toggles; focus moves to close button via `[data-ai-initial-focus]` | Panel opens/closes instantly; zero transform | Repeated clicks toggle cleanly without memory leak | Tested across host pages |
| **AC03: Launcher Focus Trap** | Tab / Shift+Tab while launcher is open | Keyboard events inside panel & iframe | Focus cycles strictly between panel header controls and iframe content; cannot escape to host page | Focus strictly contained; Escape key triggers `closeLauncher()` and restores host focus | Focus moves immediately without scroll animation | Closing panel restores focus to trigger button | Verified with keyboard-only navigation |
| **AA01: Model Training Confirm** | Click "Huấn luyện Model" button | SweetAlert2 confirmation dialog | Confirmation dialog opens; on confirm, loading spinner displays; form submits to POST `/Admin/AiManagement/TrainModel` | Alert dialog with `role="alertdialog"`; focus trapped in dialog | Spinner without perpetual motion in reduced-motion | Cancellation closes dialog cleanly | Tested via controller & view verification |
| **AA01: Index Maintenance Operation** | Click "Dry-run" or "Xây chỉ mục" / "Tạo vector" | Form submitter value (`dry-run`, `build`, `embed-dry-run`, `embed`) | Form appends hidden operation input; buttons disable to prevent double submit; status alert displays | Operation feedback rendered with semantic status alert | Immediate submit state | Disabling buttons retains exact submitted operation | Verified with form submission handlers |

---

## 4. Implementation Checklist & Verification Progress

- [x] Baseline facts, contracts, and existing test suites verified.
- [x] Vector master `tct-ai-mark.svg` and monochrome `tct-ai-mark-mono.svg` created with TCT brand colors.
- [x] 512px transparent PNG `tct-ai-mark.png` exported using headless Chromium.
- [x] Multi-size scaling verified at 24px, 32px, 40px, 48px, 64px; minimum usable size documented (24px).
- [x] Vietnamese identity guide `docs/design-system/ai-chatbot-identity.md` authored.
- [x] Implement AC01 (`Chat.cshtml`) & shared partials (`_ChatShell.cshtml`, `_ChatSources.cshtml`):
  - Added `.tct-theme` wrapper and clay surfaces (`--tct-clay-shadow`, `--tct-clay-shadow-button`, `--tct-clay-shadow-field`).
  - Integrated new logo mark (`tct-ai-mark.svg`) across header brand icon, message bubbles, and dynamic avatars (`ai-chat.js`).
  - Refined composer with auto-grow (`textarea.scrollHeight`), send guard, scroll-to-bottom button, and reduced motion support.
- [x] Implement AC02 (`ChatEmbed.cshtml`):
  - Explicitly loaded `tct-learning-ui.css` for standalone iframe context.
  - Added `.tct-theme` and `ai-chat-embed-body` to `<body>`.
  - Ensured compact responsiveness with flex wrapping for quick actions and accessible composer without layout clipping.
- [x] Implement AC03 (`_AiChatLauncher.cshtml`, `ai-chat-launcher.css`, `ai-chat-launcher.js`):
  - Added `.tct-theme` wrapper.
  - Integrated new logo on trigger button (`tct-ai-mark.svg`) and panel header.
  - Refined clay trigger with `--tct-clay-shadow-lift`, soft elevation, accessible focus trap across host and iframe, and Escape key dismissal.
- [x] Implement AA01 (`AiManagement/Index.cshtml`, `admin-ai-management.css`):
  - Wrapped in `.tct-theme admin-ai-management-page`.
  - Created scoped stylesheet `admin-ai-management.css` providing clay KPI cards, form fields, and status badges.
- [x] Updated JS references in `ai-chat.js` for new logo fallback and reduced-motion instant scrolling.
- [x] Automated regression tests:
  - Passed `scripts/tests/test_ai_chat_submit_guard.cjs` (5/5 passing).
  - Passed `scripts/tests/test_ai_chat_load_older_order.cjs` (2/2 passing).
  - Passed `scripts/tests/test_ai_chat_phase6_sources.cjs` (7/7 passing).
  - Created and passed `scripts/tests/test_ai_chat_clay_ui_and_logo.cjs` (6/6 passing).
  - Passed `dotnet test` on `AiPhase4HardeningIntegrationTests` and `AiRagPhase6UiResilienceTests` (43/43 passing, 0 failed).
- [x] Full local verification with Chrome DevTools Protocol across target viewports:
  - `ac01-full-chat-1440.png` (1440x900 - Classroom demo priority)
  - `ac01-full-chat-1366.png` (1366x768 - Laptop standard)
  - `ac01-full-chat-768.png` (768x1024 - Tablet portrait)
  - `ac01-full-chat-375.png` (375x812 - Mobile portrait)
  - `ac01-full-chat-short-550.png` (1366x550 - Short-height laptop with virtual keyboard/toolbars)
  - `ac02-embed-frame-400.png` (400x650 - Isolated embed frame)
  - `aa01-admin-ai-1440.png` (1440x900 - Admin AI management console)
  - `ac03-launcher-host-1440.png` (1440x900 - Host page with floating launcher trigger)
- [x] Leave uncommitted for Chau's review; no commit, push, PR, merge, or deployment executed.

## Owner-accepted local commit checkpoint — 2026-11-10

- Chau accepted this AI Chat batch and moved to Dashboard, user management,
  the shared Admin shell and the login loader. Preserve the three unrelated
  BTH3 prompt/report/review files outside the AI Chat commit.
- Fresh Release test/build covering the current Ai-prefixed suites, HybridRag,
  MlNetAi, InternalKnowledgeProvider and SharedUiPartialsRenderTests:
  **359 passed, 0 failed, 0 skipped**. Paid/live acceptance flags were not enabled.
  This focused filter is not a full application or live provider acceptance.
- Fresh `node --test scripts/tests/*.cjs`: **132 passed, 0 failed**.
- Existing test edits update the expected chatbot image path. The production JS
  diff updates avatar defaults/labels, empty textarea sizing and reduced-motion
  scrolling; no backend, schema or model/provider configuration changed.
- Logs/TRX are ignored under `.appdata/local/test-results/ai-chat-commit-*`.
  No browser session, image-quality judgement or shared launcher acceptance was
  independently repeated in this checkpoint. Earlier screenshot/motion claims
  remain the preceding implementation's reports, not fresh verification here.
- Commit is local only. No push, remote CI, merge, hosting/configuration change
  or production release; existing credentials and runtime data stay outside Git.
