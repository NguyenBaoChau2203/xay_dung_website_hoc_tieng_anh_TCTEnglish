# 06 — Bind the completed backend to the approved interface

Model: Gemini 3.8 Flash High. Local UI implementation when invoked.
Prerequisites: 03-learning-contract.md, 04-badges-contract.md and the completed108
lesson matrix. Preserve stage 01's design; resolve actual contract drift first.

- [x] Inspect current ViewModels/routes and the backend handoffs. Use their real
  fields/status meanings; missing data must be resolved with a precise backend
  request, not simulated client counters or guessed private properties.
- [x] Render server-projected GenZ groups, counts and badge details. Filters act
  on supplied display groups; selection persists sensibly across refresh/back.
  Locked, earned, newly earned, unknown progress and catalog-empty are distinct.
- [x] Keep mystery redaction intact in text/tooltip/aria/DOM/JSON/next-award hints.
  A tooltip must not accidentally expose a hidden name or exact threshold. An
  earned secret reveals only the fields actually authorized by backend projection.
- [x] Goals/Profile use the same real streak and earned-badge semantics. Display
  received progress/date/units with an explanation of a qualifying study day.
  Keep vocabulary chart versus all-skill activity versus goal-met labels distinct.
- [x] Replace provisional stage 01 sections with real fields where now available.
  If Profile has no awards, show truthful empty state; no invented locked examples.
  Badge detail/filter controls are keyboard accessible and never grant awards.
- [x] Verify Writing's18 cells, six added lessons each, filters, pager, long
  titles, original lessons and private lists. Catalogue counts are read from the
  current response, not hardcoded108 or frontend arrays.
- [x] Verify accepted/revision/review status, completion feedback and refresh/
  resume; rewards shown after server confirmation only. Handle offline, 429,
  provider unavailability, timeout and duplicate click without losing typed text.
- [x] Maintain Clay tokens, shared assets, typed views, existing DOM hooks and
  scoped CSS. Use existing icon families; no new palette/CDN/font/package.
- [x] Run render/Node tests appropriate to changed files; build Razor. Test real
  browser at 1440/1366/768/390, keyboard, long copy, native 200% zoom and reduced
  motion. Distinguish screenshots/fixtures from actual live behavior.

Save `06-ui-integration.md` evidence, screenshots and unresolved backend needs in
the shared ledger directory. Report in Vietnamese. Do not alter award rules,
database history, authentication/billing flows or import content to make UI pass.
