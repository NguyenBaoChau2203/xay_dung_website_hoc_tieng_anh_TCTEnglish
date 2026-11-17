# 02 — Creation panel, fallback confirmation and integration acceptance

Implementer: Gemini 3.8 Flash High; one agent. Local UI implementation only when
invoked. Prerequisite: backend step 01 contract and completed recent-history work.
Use the current branch/dirty code as baseline; preserve all previous work.

## User experience

- [ ] Keep the current Clay style and personal-exercise area. Add two accessible
  tabs: "Mô tả bài muốn luyện" and "Dán bài có sẵn". Retain source mode's text,
  limits, handlers and validation. Do not guess the mode from language/length.
- [ ] Brief mode includes description, three existing levels, six formats,
  supported length/tone controls, a live character count and concise examples.
  Examples insert editable text; they do not auto-submit or spend a request.
- [ ] Controls make the capability obvious: "Bạn muốn luyện viết bài gì?" and
  "Tạo bài theo mô tả". Keep draft inputs when switching modes or seeing errors.
  Send only the active mode's actual typed fields.
- [ ] Availability copy is truthful: normal creation uses OpenAI. During an outage,
  present the server's internal-template offer and limitations, not a fabricated
  success toast or vague promise to support every topic offline.
- [ ] Let the user inspect a proposed frame, its real level/format/tone/length and
  supported slots, see details that cannot be carried over and explicitly confirm.
  Free-text keyword match is not automatic consent to lose requirements.
- [ ] Template success says "Tạo theo khung song ngữ nội bộ"; AI success says
  "Tạo bằng OpenAI". Use persisted provenance on private cards/practice where the
  contract exposes it, including a truthful legacy/unknown state for old items.
- [ ] Unsupported inputs preserve the user's brief and offer supported frames or
  an honest "Chọn bài có sẵn" action. Opening a catalog result is not generation
  success and must not consume a creation slot or clone content automatically.
- [ ] Share the actual successful-creation quota across modes/engines. UI copy
  should describe the combined allowance, not two independent five-per-day pools.
  Server controls rights/quota; disabled buttons alone are not enforcement.

## Integration and recovery

Primary files: Views/Study/WritingExercises.cshtml, wwwroot/js/writing-exercises.js,
wwwroot/css/writing-exercises.css and narrowly scoped private-card/practice origin
markup. Read DESIGN.md/team UI workflow/tct-ui-component and tct-ui-review first.

- [ ] Consume the actual backend contract, including offer/recovery statuses.
  Preserve old create/delete/filter/pager selectors and Writing recent module.
  Keep Standard/guest restrictions and existing protected form tokens intact.
- [ ] Reuse one stable idempotency identity for one logical submission/retry.
  Edits produce a new payload identity; same-key conflict is not silently retried.
  A lost response triggers owner-scoped status recovery before any new creation.
  Session recovery state is account-scoped; old users' text/results never leak.
- [ ] Deduplicate submits, handle pending/known failure/uncertain outcome/choice/
  success distinctly, restore controls appropriately and preserve typed text.
  Reject HTML login redirects as data. Never retry generation from pageshow.
- [ ] On success navigate using returned exerciseId/actual level/contentType;
  the existing authorized practice initialization records recent history.
  Merely previewing or confirming a non-saved frame grants no recent visit/XP.
- [ ] Render user descriptions, titles, slot values and errors through encoding/
  textContent. Safe route construction only; no arbitrary HTML/URL execution.

## Acceptance

- [ ] Legacy source conversion still works; new brief creates the requested
  exercise under fake primary success and opens the normal practice flow.
- [ ] Outage/circuit/rate/billing/config failures produce correct offers/copy;
  unsupported details require informed confirmation rather than silent edits.
- [ ] Every supported template category creates an actual complete private
  bilingual exercise without any external call. Origin survives reload.
- [ ] Lost response/retry/tab switch/account switch cannot double-create or expose
  another user's state. Cancel/refusal/forbidden/quota are not bypassed.
- [ ] Two modes, slots, validation, loading, recoverable error, quota exhausted,
  empty suggestion and missing-configuration states work with keyboard and long
  Vietnamese/English text. Retain entered content after recoverable failures.
- [ ] Browser 1440/1366/768/390, reduced motion and native 200% zoom are observed or
  explicitly marked unverified. Preserve the current recent-section layout.
- [ ] Run relevant Node/UI/render tests and Release build; recheck backend contracts
  and Writing progress/Goals/recent regressions after integration. No live paid
  provider requests for browser proof: use a verified fake/controlled host.

Write `docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/02-interface.md` with
actual screens/contracts/tests, manual test order and remaining limits in Vietnamese.
Flag backend contract gaps rather than inventing frontend engine/quota logic.
Local success is not a deployment; do not commit/push/PR/merge/deploy here.
