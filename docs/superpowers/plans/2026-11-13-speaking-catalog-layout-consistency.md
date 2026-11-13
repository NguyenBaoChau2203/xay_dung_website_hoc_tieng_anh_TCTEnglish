# Speaking catalog layout consistency

Mode: IMPLEMENT_LOCAL. Owner requested Chrome MCP DevTools investigation and
fixes for clipped topic controls and uneven personal cards. Work as one agent,
preserving existing uncommitted imports, practice fixes and visual work.

## Observed causes

- Chrome MCP1440: document scrollWidth2304. Toolbar/topics/public sections are
  siblings of `.spk-page-wrapper` under main; page/theme-scoped CSS does not
  match. A surplus div close after the import placeholder closes the page wrapper.
- The new carousel wrapper has no sizing styles and grows to1873px. The existing
  wrapped-topic rule cannot match while outside the page wrapper.
- Chrome MCP1920: personal grid cells are333.625px tall, but short-title cards
  are314.031px. Actions differ by19.594px. Cards do not stretch through their
  outer grid item and titles do not reserve their existing two-line allowance.
- Controls-row markup uses a new class without flex styling; mode/level controls
  stack with inconsistent spacing. Fix within the existing Clay tokens/layout.

## Checklist and acceptance

- [x] Inspect actual rendered DOM, winning CSS and card/filter geometry through
  Chrome MCP. Read relevant bug memory/design and preserve local changes.
- [x] Remove surplus closing tag, constrain the topic carousel and style control rows.
- [x] Stretch personal cards and align title/meta/action rows for varied titles.
- [x] Verify Chrome MCP1440/1920/768/390 and zoom, sidebar modes, filtered/no-result
  states, keyboard topic access and delete-modal dismissal without deleting.
- [x] Run relevant existing checks/build, review focused diff, update bug memory
  and report Vietnamese with screenshots and any unobserved checks.

## Owner clarification: preserve the Gemini interaction design

The owner supplied the complete prior requirements: one horizontal topic carousel
with arrows/wheel/touch, topic icons, same-row desktop view/level controls,
no document reload for filters/search, URL/back/forward sync and8 cards per
numbered slide. These newer explicit requirements supersede the wrapped-chip
and native-link choices above. Retain the structural/card fixes.

Git audit: current branch is fix/speaking-catalog-transcript-polish at9d55f6f.
The Gemini8-item pagination/carousel DOM/JS is already uncommitted here. No stash;
the only other worktree is an old CI/CD guidance checkout. The old Speaking UI
branch is already an ancestor. No unrelated branch merge is needed. A private
snapshot of the3 UI files and full diff is in ignored
.appdata/local/speaking-ui-reconcile-20261113 (no credentials/data included).

Reconcile in this branch: implement the missing carousel/pagination/icon CSS;
preserve the Gemini8-card renderer; fetch current MVC catalog HTML as needed so
filters remain within the page and do not assume a partial DOM is the entire
catalog. Keep private cards and practice/import fixes untouched. Add meaningful
interaction regressions and rerun Chrome MCP acceptance for these exact choices.

Preserve existing IDs, routes, fetch/filter contracts and all topic labels. No
shared stylesheet/layout edit, dependency, schema, data/export or paid call.
No commit/push/CI/release authority. No empty CSS-mirroring tests: keep browser
geometry/DOM assertions and existing interaction regressions as evidence.

## Final evidence after reconciliation

- All work remains in fix/speaking-catalog-transcript-polish. No unrelated branch
  was merged, reset or deleted; no commit/push was performed. Recent-videos/UAT
  prompt artifacts are preserved as documents, not treated as new feature grants.
- Reconstructed missing carousel/icon/pagination CSS with existing tokens;
  `.vi-card-page-hidden` now enforces the existing8-card renderer. Desktop arrows,
  wheel scrolling and touch overflow retain the original IDs. Mobile hides arrows.
- Async read-only Index requests replace only public sections. All continuation
  pages are collected before counting/sliding. Sequence guards/abort prevent stale
  responses. Filters, search, group view and Back/Forward share URL state; private
  cards/import and score restoration stay in the same document.
- Chrome MCP: A1=18 loaded/8 visible/3 slides; page2 has8 different IDs; B2=15
  loaded/8 visible/2 slides. A JS marker survives these filter/group changes and
  history.back/forward. All filters load95 public videos; every topic has<=8
  visible cards, including20-card exam topic with3 slides. Counts remain95.
-1440/768/390/375 final layout: documentWidth equals viewport, topics stay on one
  scrollable line, every section respects8; desktop arrows visible when needed,
  mobile arrows hidden. Arrow click moves scrollLeft0→220. Personal cards have
  equal heights/action baselines; desktop1920 reproduces the old19.594px defect
  and now has0px spread. Sidebar expanded/collapsed both checked.
- Search empty/clear, Enter on a topic, group/CEFR changes and delete dialog
  open/Escape pass without deletion or paid calls; console has no errors/warnings.
- Screenshots were inspected inline through Chrome MCP. File export to the D
  checkout was rejected by that MCP server's configured path roots; no workaround
  was used. Native200% zoom could not be confirmed. CSS zoom is not equivalent to
  native breakpoint reflow and is not claimed as a passed native zoom check.
- New navigation tests reproduce the old behavior before the fix and cover
  eight-card slides, missing continuation data, failed reads, stale responses,
  popstate and modified clicks.144 DOM-fixture consumer checks pass after fixing
  DocumentFragment insertion in the existing helper and updating the obsolete
  empty saved-score-status assertion. Release build passes0 errors; diff check
  and local5100 readiness HTTP200 pass. No schema/config/shared-data changes.
