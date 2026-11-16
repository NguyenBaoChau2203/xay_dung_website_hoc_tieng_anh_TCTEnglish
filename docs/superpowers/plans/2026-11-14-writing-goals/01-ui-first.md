# 01 — Polish the interface before backend expansion

Model: Gemini 3.8 Flash High. Mode: IMPLEMENT_LOCAL when this stage is invoked.
Read README.md for owner decisions/authority. Prerequisite: current feature branch;
no dependency on future backend fields. Preserve the attractive Goals composition.

## Files and boundaries

- Views/Study/Writing.cshtml, WritingExercises.cshtml, WritingPractice.cshtml.
- wwwroot/css/writing-index.css, writing-exercises.css, writing.css;
  wwwroot/js/writing-index.js, writing-exercises.js, writing.js.
- Views/Goals/Index.cshtml; wwwroot/css/goals.css; wwwroot/js/goals.js.
- Only the learning stats/earned-badges portions of Views/Account/Profile.cshtml
  and narrowly scoped rules in account-ui.css/account-ui.js when actually needed.
- Read DESIGN.md, the team UI workflow and tct-ui-component/tct-ui-review.
  Shared styles/layouts affect other screens; use the existing Clay foundation.
  Account security, billing and unrelated tabs are outside this visual task.

## Ordered work

- [ ] Capture baseline screenshots, routes, DOM selectors, forms and current
  ViewModel fields. Use Chrome DevTools MCP when available; report a real-browser
  fallback accurately. Discover tools instead of assuming a tool name exists.
- [ ] Inventory every metric/award/CTA as real data, decorative copy, misleading
  placeholder or missing backend capability. Save `01-ui-data-contract.md` in
  the shared ledger directory from README.md.
- [ ] Polish Writing selection/list/practice: visible level/format/filter state,
  pager/back navigation, typing area, hint/feedback hierarchy, saved draft/resume,
  Accepted/NeedsRevision/NeedsReview states, disabled/pending/error/retry feedback.
  Keep form fields, JSON/status contracts, antiforgery and JS hooks intact.
- [ ] Preserve Goals' Clay hero/cards/chart; improve unit clarity, modal target
  editing, focus, overflow and honest empty/error states. Do not swap in a new
  global palette or rebuild the whole page to resolve small issues.
- [ ] Remove the three invented locked award examples from Profile's live empty
  state. Use the existing real empty-state component and link to Goals. Until
  backend data supports recommendations, do not invent a target/progress value.
- [ ] Label the existing weekly series as vocabulary activity. Its current
  CardsReviewed>0 signal is not proof that all daily goals are complete. Replace
  misleading labels/tooltips now; request separate verified all-skill day states
  for stage 03. Do not derive them in JavaScript from current streak length.
- [ ] Replace unverified live-update/leaderboard claims with copy matching actual
  behavior. Retain numerical values from the ViewModel, including real zeros.
- [ ] Record tier/mystery component designs and required fields in the handoff.
  Preview-only fixtures may live in a clearly labeled docs/demo artifact; new
  unsupported tiers, secret awards and fake unlocked cards stay out of live pages.
  Existing badges continue rendering correctly before stage 04.

## Acceptance

- [ ] Real routes work before any new backend code; no undefined proposed fields.
- [ ] No invented stats/awards; decorative SVGs remain decorative/aria-hidden.
- [ ] Role/public/private behavior, answer secrecy, drafts and assessment states
  are preserved. Draft isolation is verified for two users sharing one browser.
- [ ] Desktop/laptop first: 1440 and 1366 width, then 768 and 390/375. No horizontal
  overflow; long text, empty/zero states and keyboard-only editing remain usable.
- [ ] Native browser zoom 200% is observed or explicitly marked unverified;
  CSS zoom/emulated viewport alone does not certify it. Reduced motion is usable.
- [ ] Relevant render tests and Node UI/draft/status tests pass after affected
  JS/Razor changes. A .NET build verifies changed Razor compilation.
- [ ] Browser submissions cannot silently call paid optional providers. Verify
  fee-free runtime/test-host configuration before submitting answers.

## Handoff

Record screenshots/actual viewport, changed files, retained selectors, backend
requests, findings and check results in `01-ui.md` plus `01-ui-data-contract.md`.
Report in Vietnamese. Do not advance into backend, content imports or stage 06.
