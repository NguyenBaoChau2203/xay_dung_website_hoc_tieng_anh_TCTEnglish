# Redesign AI Chat UI/UX — Gemini 3.8 Flash High

## Outcome

Develop a coherent UI idea and implement a cleaner AI Chat experience at
`http://localhost:5107/AI/Chat`. Improve layout, hierarchy, density, readability,
scrolling and feedback in both Chat chữ and Luyện hội thoại. Prioritize laptop
demo quality while delivering mobile/tablet usability in the same change.

Follow the source-grounded plan:
`docs/superpowers/plans/2026-11-19-ai-chat-ui-ux-redesign-plan.md`.
Read it before starting and update its checklist as work progresses.

## Mode and authority

Mode: `IMPLEMENT_LOCAL`. The owner requests design ideation followed by local
implementation and verification. Compare/refine the plan's two layout approaches,
choose a sensible direction, explain your decision briefly and continue to code.
Do not stop after another plan or add a routine design-confirmation gate. Ask only
for a genuinely blocking requirement or action outside the authority below.

- Stay on the owner-selected Gemini 3.8 Flash High. The IDE model choice does not
  authorize changing the application's AI models or provider configuration.
- Use one agent for this task, including investigation, design, coding, review and
  testing. The owner's direct single-agent instruction applies even if another
  instruction mentions an optional Antigravity exception. Do not delegate.
- Local changes only: no pull, commit, push, PR, remote CI, merge or deployment.
- Preserve existing changes and running workloads. Do not stash/reset/discard work.
- No edits to `Program.cs`, appsettings, project files, persisted models/migrations,
  AI/provider configuration, quota/accounting, secrets or shared content packages.
- Keep shared shell geometry and brand. Do not edit global tokens/CSS/layouts to
  solve a feature-specific layout issue. Document any truly necessary shared proposal.

## Context and evidence

The plan was prepared on 2026-11-19 at branch `feature/ai-voice-conversation`,
HEAD `e6be0e1`, with many uncommitted AI Voice changes. Inspect the actual current
branch/status/diff before editing. Those identifiers are historical evidence,
not an instruction to change branches or reset code.

Observed on the running page:

- At 390×844, expanded text history consumed ~369px; the message region was only
  ~105px high and clipped the initial suggestions within its scroll area.
- At 768×1024, history consumed ~442px and the message region was ~41px high.
- Desktop text mode stacks mode buttons, branding/intro, draft metadata and
  technical observability above chat, alongside another history column.
- Mobile textarea was 36px high and Send ~40px high; the input needs stronger
  hierarchy and the primary touch action must reach 44px.
- Voice is already inline, uses a shared workspace and hides text history. Preserve
  this behavior, existing Tico assets and the recent lifecycle/history fixes.

These observations are reference evidence. Reproduce current conditions and identify
changes since the audit. Native zoom, embed/launcher, all roles and provider states
were not certified by the plan author. Do not report them as already passed.

## Read only the context needed for the active step

Follow `AGENTS.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md` and
`docs/superpowers/specs/2026-11-09-multi-platform-ui-design.md`.
Use canonical skills `tct-superpowers`, `tct-engineering`, `tct-ui-component` and
`tct-ui-review`; use `tct-ui-ux-pro-max` for a focused design lookup and
`tct-ui-motion` only if motion changes. Preserve voice domain rules when touching
that UI. Read the newest symptom-relevant bug memory before fixing defects,
especially inline iframe lifecycle and compact voice history.

Use CodeGraph first when `.codegraph/` exists; fill concrete unindexed/incomplete
source gaps with bounded direct reads. Never initialize/rebuild an index or load
unrelated plans/vendor bundles. Use only tools actually available in your client.

## Scope and preserved contracts

Primary files:

- `TCTEnglish/Views/Ai/Chat.cshtml`, `_ChatShell.cshtml`, `ChatEmbed.cshtml`.
- `TCTEnglish/wwwroot/css/ai-chat.css`, `ai-voice.css`.
- `TCTEnglish/Views/AiVoice/_Workspace.cshtml` when voice composition needs changes.
- Minimal presentation/focus changes to `ai-chat.js`, `ai-chat-mode.js` and
  `ai-voice-page.js` when justified by the selected design.

Inspect dependent source/citation markup, launcher and voice coordinator as needed.
List consumers before modifying reused partials/styles: full chat, embed/floating
launcher and standalone `/AI/Voice` where affected. Preserve those consumers with
responsive regression checks; keep unrelated Admin/launcher redesign out of scope.

Inventory existing IDs, `data-*`, form tokens, event delegation and dynamic templates
before moving markup. Preserve routes, JSON/SignalR contracts, access/antiforgery,
safe Markdown/DOMPurify and citations, history/message pagination, delete confirmation,
drafts, Stop/retry, scroll behavior, quota and error states. Do not duplicate bound IDs
or remove technical panels simply because their controls become secondary.

Keep text DOM/draft/conversation during mode changes. Opening voice must not create
a session or activate the microphone. Return to text only after confirmed cleanup;
pending/failure must keep Stop/retry reachable and prevent duplicate Start. Preserve
iframe origin/source validation, modal/Escape focus behavior and immediate media teardown.

## Design and ordered execution

1. Inspect the live page and relevant dirty diffs. Record a short baseline and
   unresolved gaps in `docs/feature-audits/2026-11-19-ai-chat-ui-ux-redesign-ledger.md`.
2. Develop two concise composition options within the existing Clay system; compare
   learning clarity and space use. Select/refine one and record desktop/mobile
   wireframes, hierarchy and scroll ownership. The recommended direction is a
   conversation workspace with quiet optional desktop history, closed/secondary
   narrow-screen history, compact identity/mode control, generous messages and an
   anchored composer. Exact arrangement remains your design decision.
3. Implement the entire responsive layout. Fix narrow-screen height allocation first.
   Consolidate conflicting flex/height/overflow rules across both feature stylesheets;
   avoid additional hardcoded viewport deductions and blanket `!important` patches.
4. Polish empty/populated text chat and ready/active/error voice states. Use shared
   Clay tokens on key surfaces, quiet depth for dense text, existing Tico imagery,
   clear Vietnamese copy, readable type and accurate usage/status. Keep all existing
   features discoverable. Do not add unsupported AI abilities or decorative widgets.
5. Verify interactions and dynamic templates against the moved markup. Add meaningful
   regression coverage for changed behavior, reproducing a failure before its fix
   when adding a bug regression. Preserve Enter/Shift+Enter/IME and duplicate-send guards.
6. Run the plan's build, focused Node/.NET tests and browser acceptance matrix. Review
   actual failures before attributing them to pre-existing work. Do not weaken security/
   lifecycle tests or substitute markup assertions for browser acceptance.
7. Compare before/after at identical sizes, self-review your diff, update the plan,
   evidence ledger and only the AI Chat row of the design workflow tracker. Append
   bug-memory entries for actual fixes, preserving unrelated concurrent entries.

## Acceptance and verification boundaries

- Both modes work at 1440×900, 1440×768, 768×1024, 390×844 and 375×812.
  Check desktop sidebar open/collapsed, mobile drawer, actual embed/launcher and
  affected standalone voice. No clipped essential text, page overflow or inaccessible
  composer/call controls; inspect clipped descendants as well as document width.
- Narrow text history is secondary/closed initially. Proposed normal-zoom targets:
  message region at least 280px at 390×844 and 360px at 768×1024, with composer fully
  visible. Adapt smaller heights/keyboard/zoom without hiding content or reducing
  readability; record any justified deviation from these proposed targets.
- Keyboard navigation, visible focus, 44px actions, modal focus/Escape return,
  long multilingual text, reduced motion, contrast and native 200% zoom are checked.
- Exercise pending/disabled/loading/error/retry/429/outage, source panels, history,
  safe Markdown, mode cleanup and iframe-close states using synthetic data and
  local doubles/intercepted responses. No personal-data deletion or paid provider
  calls/microphone requests solely to test layout. Label simulations honestly.
- Use the current server only when it represents the edited code. Preserve port
  5107's listener; if a fresh preview is needed, check a free port and use
  `scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort <free-port>`
  through `pwsh -NoProfile -File`. Confirm readiness and speech metadata status.
  Do not print/change saved local keys or reset data.
- Run the exact focused commands listed in the plan after confirming their paths;
  if guidance/prompt files change, run AGENTS.md's tooling validator and Python suite.
  Keep skipped/blocked/native-zoom/physical-device checks explicit. No full app build
  is required merely to edit these handoff documents; implementation requires its
  own fresh build and tests.

## Deliverables and Vietnamese handoff

Deliver the implemented local UI, updated plan/checklist, focused regressions where
needed and the evidence ledger with exact commands/results and before/after notes.
Keep synthetic screenshots under ignored `.tmp/ai-chat-ui-ux-redesign/`.

Finish in Vietnamese: chosen idea and benefits, changed files, actual checks and
skips, local preview URL, preserved work and remaining risks/blockers. Distinguish
local verification from CI/staging/production. Do not claim complete responsive
acceptance when required browser states remain unobserved.
