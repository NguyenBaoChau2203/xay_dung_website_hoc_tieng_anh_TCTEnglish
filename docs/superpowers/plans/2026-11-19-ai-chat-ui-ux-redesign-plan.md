# AI Chat UI/UX redesign — Gemini 3.8 Flash High

Date: 2026-11-19. Status: implementation handoff; product changes have not started.
Target: `http://localhost:5107/AI/Chat`.
Execution prompt: [redesign-ai-chat-ui-ux-gemini-3-8-flash-high.md](../../../.ai/prompts/redesign-ai-chat-ui-ux-gemini-3-8-flash-high.md).

## 1. Outcome and authority

Redesign the AI Chat experience so the learner immediately understands what to do,
has enough room to read/write, and can switch between text and voice without losing
context. Improve composition, information hierarchy, density, scrolling and feedback;
apply the project's existing Claymorphism direction with restrained depth.

This document is a source-grounded plan with a recommended direction. The owner has
asked Gemini 3.8 Flash High to develop the UI idea and implement the redesign locally.
The author's current task creates this plan and prompt only. The receiving agent may
make reversible design decisions within this scope and continue through implementation
and verification without a routine second approval round.

- Use one agent throughout this task, including design, implementation and review,
  per the owner's direct instruction. Do not delegate or switch models/clients.
- Local UI work only. No commit, push, pull, PR, remote CI, merge or deployment authority.
- Preserve the current dirty checkout. Do not reset, stash, discard or overwrite other work.
- No changes to `Program.cs`, appsettings, project files, model configuration,
  persisted models, migrations, quota/accounting, roles or shared content packages.
- Keep learner navigation and the shared header/sidebar/footer geometry. Shared
  design tokens, global CSS and layouts are reference dependencies; a global redesign
  is outside this task. Solve the feature layout in its own Razor/CSS/JS.

## 2. Evidence collected for this handoff

Verified checkout: branch `feature/ai-voice-conversation`, HEAD `e6be0e1`.
These values are a snapshot, not an instruction to check out/reset to that commit.
There are substantial uncommitted voice-feature changes, including the Chat views,
launcher, shared layout and new voice files/tests. Reinspect before editing.

### Browser observations, 2026-11-19

The running local page was inspected using an existing saved local test account.
No chat message was submitted, microphone enabled or voice session created.
No private conversation text or credentials are reproduced in this document.

| Observation | Evidence / consequence | Priority |
| --- | --- | --- |
| Mobile text chat is squeezed by expanded history | At 390×844 on a fresh load/top position: history height ~369px, chat surface ~382px, `#chatWindow` ~105px; its content scroll height was ~213px. Suggestions are clipped inside the small reading region. | P1 |
| Tablet has the same height-allocation failure | At 768×1024 after responsive reload: history ~442px and `#chatWindow` ~41px. | P1 |
| Desktop hierarchy is busy | At 1440×900, a 300px history column sits alongside the application sidebar; text mode stacks the mode switch, identity/intro, conversation metadata and observability above chat. Many nested rounded/shadowed surfaces compete. | P2 |
| Composer needs stronger usability | Mobile textarea measured 36px high and Send ~40px high. The primary input is visually subordinate and Send is below the project's 44px touch target. | P2 |
| The two modes have very different composition | Voice correctly hides text history and expands across the available workspace; its topic card, large mascot/status card, disclosures and fixed control area still need a cohesive density review. | P2 |
| Technical information occupies primary space | The default text view includes a system-metrics disclosure and conversation draft metadata above the conversation. Keep access, but subordinate these to learning. | P2 |

At 1440×768, the text reading region measured ~385px. No document horizontal overflow
was measured at the inspected 1440/768/390 widths. This does not establish absence
of clipped descendants or certify the whole feature.

Not yet verified: 375px, sidebar-collapsed desktop, keyboard/IME interactions,
native 200% browser zoom, real mobile keyboard, full/streamed conversation states,
embed/launcher, standalone voice, all roles and live provider/audio behavior.
The screenshots/measurements are a baseline audit, not completed acceptance.
Mobile emulation caused a document reload/reset to text mode; do not describe that
as a spontaneous voice-session lifecycle failure.

### Source facts and root cause to address

- `Views/Ai/Chat.cshtml` renders `aiHistoryDisclosure` with `open`; responsive CSS
  stacks it above the chat surface and permits a sizable history region.
- `wwwroot/css/ai-chat.css` combines a viewport-height page, min/max heights,
  multiple nested overflow regions, stacked history at narrow widths and additional
  height-specific overrides. Available height is consumed before the message region.
- `_ChatShell.cshtml` owns the two mode buttons, text panel/composer and inline
  `AiVoiceWorkspace` component. `ChatEmbed.cshtml` reuses the same shell.
- `wwwroot/css/ai-voice.css` also styles the mode switch and text/voice containers.
  Read both stylesheets together before changing height/flex/overflow behavior.
- `wwwroot/js/ai-voice-page.js` binds the mode controller and moves voice controls
  into a footer. Preserve those assumptions or update them deliberately with tests.
- The server's chat routes, access controls and antiforgery checks are already
  feature-owned. This redesign does not need backend contract changes.

CodeGraph was queried first. Its Razor output contained only the initial lines,
so direct reads filled the markup gap. CSS and the new unindexed voice files were
read directly. Do not initialize or rebuild the graph to repeat this audit.

## 3. Design brief and room for Gemini's ideas

Use `DESIGN.md`, `docs/design-system/team-ui-workflow.md`, the Clay gallery and
the canonical multi-platform spec. Preserve TCT blue/yellow, Inter, existing Tico
assets and the page's icon family. Use current `--tct-*` tokens; do not create a
new palette, font, dependency, CDN or mascot artwork.

Compare these two practical directions briefly, then select/refine one from the
live baseline. The recommendation is a proposal, not a claim of owner approval
of exact dimensions or copy:

1. **Conversation workspace (recommended):** compact identity/toolbar, clear text/
   voice selector, quiet optional history rail on desktop, history disclosure or
   drawer on narrow screens, generous message area and anchored composer. Technical
   details remain available through secondary disclosure. Best fit for the current
   feature without extending backend behavior.
2. **Single main pane with history drawer at all widths:** maximum reading width
   and fewer simultaneous panels, but more steps to revisit conversations. Choose
   this only if desktop comparisons show a clear benefit while keeping discoverability.

Record a concise design decision and desktop/mobile wireframes in the implementation
ledger before editing. Use existing HTML/CSS or simple diagrams; no image-generation
or new mockup package is needed. Continue with the selected design.

### Text mode

- Give the page one concise, mode-independent accessible heading; avoid duplicate
  headings when combining partials. Keep product-facing copy Vietnamese.
- Make the active mode explicit through text, shape/state and `aria-pressed`.
  The switch should be a compact control, not the dominant decorative panel.
- Keep desktop history quiet and proportionate. Use readable compact rows, active
  state, full-title access, dates and independent 44px delete controls. Avoid a
  raised card for every history row or hover-only delete on touch.
- Default narrow-screen history to closed without closing an in-progress user
  interaction on every resize. Keep New chat/history easy to find and preserve
  pagination. Avoid an interactive anchor nested in a disclosure summary if
  restructuring the current header.
- Replace the oversized empty well with a balanced Tico greeting, short guidance
  and the existing actionable suggestions. Keep suggestions reachable and relevant;
  do not invent new AI capabilities, generate a reply on load or silently auto-send.
- In populated chat, readable message content takes priority. Preserve full learning
  text, safe Markdown, source/citation panels and actions. Quiet message surfaces
  should distinguish speakers without excessive visual depth.
- Anchor composer to the workspace, allow the input to grow within a reasonable
  bound, and leave room for pending/error/retry feedback. Preserve Enter, Shift+Enter,
  IME composition, Stop, duplicate-submit protection and the 1000-character limit.
- Keep useful usage information accurate. Move Requests/Latency/Token details out
  of the primary path while retaining their existing IDs and access. Do not invent
  online/response-speed/remaining-quota claims or turn text/voice quotas into one.

### Voice mode

- Reuse the existing inline workspace and Tico animation/state mapping.
- Keep topic choice compact, selected context visible, and optional topic details/
  private-topic tools secondary. All existing private CRUD and history stay reachable.
- Balance mascot size, status, instructions and transcript against available height.
  Do not make the mascot the dominant content at the expense of controls/learning text.
- Keep Start, Mute and Dừng phiên visible and distinct. Preserve the privacy/quota
  explanation and immediate local media teardown. Opening voice starts no call.
- Switching back to text must wait for confirmed voice cleanup; pending/error remains
  actionable, blocks another Start and allows retry. Keep the existing text DOM,
  draft, conversation ID, history and scroll position when switching modes.

### Layout and accessibility

- Let the message/workspace region consume the remaining height using a coherent
  flex/grid hierarchy with `min-height: 0`. Prefer one primary scrolling content
  region plus explicit secondary history scrolling; audit every overflow ancestor.
- Consume shell geometry tokens; avoid hardcoded header/sidebar deductions and
  accumulating breakpoint patches. Do not hide overflow to conceal broken layout.
- Composer/call controls stay reachable at limited laptop height, mobile keyboard
  resize, safe-area inset and 200% zoom. Avoid viewport-fixed elements escaping
  their iframe or covering disclosures, status or Bootstrap modals.
- Preserve the shared footer and keep it outside the workspace; fix the feature's
  height allocation rather than removing shared navigation/footer content.
- Use shared Clay depth on main surfaces and quiet depth on dense content. Tokenized
  contrast, focus indicators, 44px targets and reduced-motion behavior are mandatory.

## 4. Scope and dependency map

Paths in this section are relative to the repository root.

| File / group | Role and permitted work | Consumers to verify |
| --- | --- | --- |
| `TCTEnglish/Views/Ai/Chat.cshtml` | Page/history composition, disclosure, modal and controls | `/AI/Chat`, populated conversation query route |
| `TCTEnglish/Views/Ai/_ChatShell.cshtml` | Feature-shared identity, mode switch, messages, composer | Full chat and `/AI/Chat?embed=true` inside floating launcher |
| `TCTEnglish/Views/Ai/ChatEmbed.cshtml` | Only adjustments required by the shared redesign | Standalone embed and launcher iframe |
| `TCTEnglish/wwwroot/css/ai-chat.css` | Main layout/typography/surfaces/responsive behavior; consolidate affected rules | Full and embedded chat |
| `TCTEnglish/Views/AiVoice/_Workspace.cshtml`, `wwwroot/css/ai-voice.css` | Compact voice composition and mode-container rules as needed | Inline full/embedded chat and `/AI/Voice` |
| `wwwroot/js/ai-chat.js`, `ai-chat-mode.js`, `ai-voice-page.js` under `TCTEnglish` | Minimal presentation/focus/disclosure changes when necessary | Existing DOM contracts and lifecycle tests |
| `TCTEnglish/Views/Ai/_ChatSources.cshtml`, launcher files and `ai-voice-session.js` | Inspect actual dependencies; preserve behavior | Grounding, iframe focus/Escape/close, microphone cleanup |
| `docs/design-system/team-ui-workflow.md` | Update only the AI Chat adoption/verification entry and evidence link | Preserve Admin AI's separate audit status |

Do not broaden this into a launcher visual redesign, Admin AI redesign, standalone
voice product redesign or backend rewrite. Shared consumers receive compatibility
fixes necessary for the changed component and complete responsive regression checks.
If a shared token/component is truly missing, document a proposal instead of
silently editing global foundation files.

## 5. Ordered implementation checklist

- [x] **A. Rebaseline:** inspect branch/status, relevant diffs, current browser and
  available local credentials without printing secrets. Read the newest chat/voice
  bug-memory entries, especially inline iframe lifecycle and compact voice history.
  Use CodeGraph first for indexed dependencies and direct reads for concrete gaps.
- [x] **B. Design:** compare the two directions; select/refine one, record hierarchy,
  desktop/mobile wireframes, scroll ownership, empty/populated/voice states and
  shared-consumer list. Maintain the owner-approved visual standard.
- [x] **C. Layout:** implement coherent page/history/message/composer height allocation
  across desktop/tablet/mobile together. Fix mobile history priority first.
- [x] **D. Presentation:** refine text mode and voice mode density, typography, Clay
  depth, useful disclosure, actions and all existing feedback states.
- [x] **E. Interaction:** verify/update DOM consumers for moved markup, dynamic
  history/message rendering, focus, drawer behavior and mode/iframe lifecycle.
  Keep one instance of each bound ID. Add meaningful regression coverage only for
  changed behavior; reproduce a failure before implementing its regression fix.
- [x] **F. Verification:** run the relevant tests/build and browser matrix below;
  compare before/after in the same viewport/data conditions. Fix scoped issues and
  rerun affected checks. Record blockers accurately.
- [x] **G. Handoff:** self-review the complete task diff against the dirty baseline;
  update this checklist, the AI Chat tracker and the implementation ledger. If an
  actual bug is fixed, append a specific entry to `.ai/context/bug-fix-log.md` and
  update a matching known issue only when applicable. Report in Vietnamese.

Suggested evidence ledger:
`docs/feature-audits/2026-11-19-ai-chat-ui-ux-redesign-ledger.md`.
Keep screenshots with synthetic data under ignored `.tmp/ai-chat-ui-ux-redesign/`.
Do not commit screenshots containing personal history, emails or credentials.

## 6. Verification and acceptance

### Automated checks

Run from the repository root after implementation; confirm files still exist.

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj --no-restore
node --test scripts/tests/test_ai_chat_clay_ui_and_logo.cjs scripts/tests/test_ai_chat_submit_guard.cjs scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_phase6_sources.cjs scripts/tests/test_ai_chat_mode.cjs scripts/tests/test_ai_voice_audio.cjs scripts/tests/test_ai_voice_session.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiVoiceInlineRenderTests|FullyQualifiedName~AiRagPhase6UiResilienceTests"
```

If dependency restoration is required, use the established local setup; do not
change versions or project files. Expand tests only for an actual dependency
change/failure. Keep behavior/security assertions when updating structural tests;
do not weaken tests to approve an attractive screenshot. Provider-gated skips
are not passes. No migration/shared-data export is needed for UI-only changes.

Task prompt/guidance changes also require the tooling checks in AGENTS.md:

```powershell
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
```

### Browser acceptance matrix

| Surface / state | Required checks |
| --- | --- |
| Full text, empty/populated | 1440×900 and short laptop 1440×768, application sidebar open/collapsed; history accessible, readable main region, usable composer |
| Tablet/mobile text | 768×1024, 390×844 and 375×812; initial history closed/secondary; clear next action; input/Send reachable |
| Inline voice, ready/active/pending/error | Same widths; topic/consent/controls/transcript/history accessible; mode switch preserves text and respects cleanup |
| Embed and actual floating launcher | Desktop/mobile, both modes, parent/child scrolling, modal focus trap, Escape dismissal order, focus return, close/cleanup |
| Standalone `/AI/Voice` | Responsive smoke checks if its shared partial/CSS changes; no clipped controls/history/modal |
| Content/feedback | Long Vietnamese/English titles and messages, unbroken strings, Markdown lists/code/tables, citations, empty/loading/pending/error/retry, 429/outage/disabled states |
| Input/accessibility | Keyboard Tab/Shift+Tab/Escape, Enter/Shift+Enter/IME, visible focus, 44px targets, contrast, reduced motion and native 200% zoom |

Specific layout targets proposed for this redesign at normal zoom: at 390×844
with history closed, aim for at least 280px of visible message region and a fully
visible composer; at 768×1024 aim for at least 360px. Do not achieve these numbers
by shrinking type, clipping content or hiding essential feedback. Shorter viewports,
zoom and the mobile keyboard require an adaptive reachable layout, not a rigid
minimum. Record final measured dimensions and any justified deviation.

Exercise transport/provider states using local test doubles or intercepted browser
responses before they reach a real provider. Use synthetic histories and intercept
destructive test actions; do not delete personal conversations/topics. Do not start
paid chat/Realtime sessions or request microphone access solely for layout testing.
Label simulated behavior separately from real provider evidence.

Use the current server when suitable. If updated Razor/JS needs a fresh application
build, do not assume a successful build updates an already-running process. Preserve
the listener on 5107; start a free-port preview with the current launcher, for example:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5117
```

Check port availability first and record the actual URL. Confirm `/health/ready`
and `scripts/speech-status.ps1`; configuration presence does not prove live quota.
Use saved local secrets automatically without displaying/replacing them. No database
reset, implicit data synchronization or process termination is authorized.

Completion requires working responsive UI plus regression evidence. A mockup, build
success, document zoom emulation or one viewport alone is insufficient. Explicitly
list native zoom, physical keyboard/microphone or browser checks that remain unavailable.

## 7. Vietnamese handoff contents

Report the selected idea and why it improves the learning task, changed files,
before/after observations, exact checks/results/skips, preserved existing work,
local preview URL and unresolved risks. Distinguish local from CI/staging/production.
Leave ungranted Git/release stages to the owner.

## 8. Plan-author verification record

Completed for this documentation handoff on 2026-11-19:

- Local browser baseline inspected at 1440×900, 1440×768, 768×1024 and 390×844;
  text and ready voice composition observed. This is not the acceptance matrix.
- The seven Node test files listed above: 49 passed, 0 failed, 0 skipped.
  This records the pre-redesign regression baseline, not a UI redesign result.
- `uv run --no-project python scripts/agent_tooling.py`: exit 0, working tree verified.
- `uv run --no-project python -m unittest discover -s scripts/tests -v`:
  153 cases, 150 passed, 3 skipped, 0 failures. Skips cover the non-Windows
  rejection contract and two pinned alignment-runtime cases. Staging-related
  output came from local test fixtures; no real deployment was performed.
- Handoff files checked for unresolved template markers, trailing whitespace,
  balanced code fences and the plan-to-prompt link. `git diff --check` exited 0
  for the tracked working diff; the two new untracked files were checked directly.

Only this plan and its execution prompt were created by the plan-author task.
No application build/.NET suite was run for the documentation-only change.
No product UI, credentials, database or provider state was changed by this task.
The receiving agent must perform fresh implementation verification.
