# Compact Account settings in two columns

Mode: IMPLEMENT_LOCAL, one agent. The owner requests a compact two-column Settings
layout with all existing functions. Preserve the preceding navigation fix and all
other working-tree changes. No Git publication, protected configuration or data work.

Observed baseline at 1440px: Account content 1807px tall; the password and
notification cards are each stretched to 855px by display:contents/explicit grid
rows. The security grid is 1239px plus a separate 343px bottom row. Below 992px,
the current settings grid falls to one column.

Design: equal-width independent column stacks at 768px and above, natural card
height, shared Clay panels/tokens and tighter gaps/padding. Left: email, password,
linked accounts. Right: security health, notifications, danger zone. Stack columns
below 768px. Keep password controls legible at narrower column widths. Group
notification actions and email verification controls without hiding any function.

Scope: Profile.cshtml, account-ui.css and the two Account partials. The partials
currently have only one consumer: Profile. CSS changes target settings selectors;
other Profile/subscription/utility page layout remains unchanged. Adapt the obsolete
Node assertion that mandates equal-height rows; do not add implementation-only tests.

- [x] Inspect indexed source first, fill CSS/Razor gaps, measure baseline and look up
  relevant local UX guidance. Use existing DESIGN.md/team UI workflow.
- [x] Reorganize existing sections into two stacks; remove forced row/height rules
  and compact form layout. Preserve IDs, actions, antiforgery, labels and input names.
- [x] Run Release build, Account rendering/security regressions and existing Node
  suites. This reversible UI change needs browser geometry checks, not new unit tests.
- [x] Inspect laptop/desktop, 768 and 390px, keyboard, drawer/modal and reflow;
  record baseline/result heights and actual limits. Preserve other running workloads.
- [x] Review the complete incremental diff, update bug memory and hand off in Vietnamese.

## Results

- Release build of TCTEnglish.Tests and its web dependency succeeds through the
  existing isolated `.appdata/local/account-settings-build` output. WarningLevel=0
  suppresses pre-existing warnings; this does not claim their absence.
- Focused .NET execution from `TCTEnglish.Tests/bin/account-settings/net9.0`:
  58 passed, 0 failed, 0 skipped (AccountUiRenderTests, AccountReviewRepairTests,
  AccountCorrectnessIntegrationTests, AccountFunctionalCompletionTests).
- Existing Account/dropdown Node suites: 51 passed, 0 failed. Obsolete assertions
  requiring display:contents, fixed rows and a separate bottom grid were removed;
  actual layout is checked through browser geometry and visual inspection.
- Real rendered 1440px content: 1542px tall, previously 1807px (265px / 14.7% less).
  Two equal 484px columns; password card 480px instead of a forced 855px.
- Browser widths 1440/1366/1280/1024/768 use two columns; 390 uses one. All six
  cards remain present, with no horizontal overflow or controls outside cards.
  A 720x450 reflow viewport also has no overflow. Native 200% browser zoom is
  still unverified; reflow is not a substitute claim.
- Eight settings POST forms retain antiforgery. Keyboard Enter toggles password
  visibility with aria-pressed, and expands all three purpose-specific proof
  requests. Delete modal opens with focus inside and its submit still disabled;
  Escape closes it. No account settings were submitted in the owner's browser.
- Rebuilt only this chat's verification process on 5101 using the project launcher.
  `/health/ready` is 200. Existing 5100 process is preserved. No pending migrations
  were applied; shared-data sync reports 0 record operations. No live speech call.
- Reviewed incremental UI diff and `git diff --check` passes. No persisted-model,
  protected-config, dependency, commit, push, CI or deployment changes.
