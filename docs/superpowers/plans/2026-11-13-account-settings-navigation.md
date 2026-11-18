# Unify Account settings navigation

Mode: IMPLEMENT_LOCAL. The owner authorized the Account navigation fix and local
verification. Work inline as one agent; Git publication and release are outside scope.

Evidence: the header links to Settings, whose GET and failed security POSTs render
a separate Settings view. The Profile sidebar activates its own security pane.
Use `/Account/Profile?tab=security` as the canonical settings surface. Preserve
authentication, antiforgery, form actions, notification anchors and saved preferences.
No persisted model or protected configuration changes are needed.

Shared impact: `_Layout.cshtml` serves all learner/public pages using the main
layout. Change only the settings link destination; preserve its IDs, classes,
accessible name and dropdown behavior. Before: Settings view. After: Profile security.

- [x] Inspect routes, caller markup, security result handling and relevant bug memory.
- [x] Add failing HTTP regressions for legacy redirect, header destination and failed
  legacy form submissions using the canonical settings view without secret reflection.
- [x] Redirect legacy Settings, update the header and consolidate security results;
  remove the duplicate view and its obsolete builder. Keep old form inputs compatible.
- [x] Run focused .NET and existing Account/dropdown Node tests, plus Release build.
- [x] Inspect both entry points in a browser at 1440/768/390, keyboard and zoom;
  record actual results and blockers. Preserve the existing process on port 5100;
  use the project launcher on a free port for changed binaries if needed.
- [x] Review diff, append bug memory and provide a Vietnamese handoff.

## Verification evidence

- Before implementation: 3 meaningful failures / 1 pass for redirect and failed
  legacy form regressions. Initial infrastructure-only failures (locked Release
  executable, relocated test content root) were resolved before recording this baseline.
- Release build: `dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
  --artifacts-path .appdata/local/account-settings-build --nologo -v quiet
  -p:WarningLevel=0` succeeds. Warning output is suppressed, not claimed absent.
  Copy built test output to ignored `TCTEnglish.Tests/bin/account-settings/net9.0`
  to retain the test factory's existing four-parent repository-root convention;
  execute that test DLL with `dotnet test` while preserving the running Release app.
- Final focused .NET suite: 92 passed, 0 failed, 0 skipped. Covers Account UI,
  review repair, correctness, functional completion, Profile badges, Speaking/
  Writing account lifecycle, Personal Sets and existing review probes. The token
  helper follows one relative page redirect for existing legacy-route callers;
  the navigation regression still explicitly checks the original 302 response.
- Node: `node --test scripts/tests/test_account*.cjs
  scripts/tests/test_user_dropdown_and_loader.cjs` (PowerShell expands Account
  filenames first): 51 passed, 0 failed.
- Browser: launcher `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
  -Configuration Debug -WebPort 5101`; readiness 200. Header and sidebar keyboard
  activation both reach Profile security with the same active pane. Legacy
  Settings redirects there; `#section-notifications` is retained. Actual viewport
  widths 1440, 768, 390 have 0 horizontal overflow. Mobile drawer Escape returns
  focus to its opener after the opening transition. Reflow at 720x450 also passes.
- Native browser 200% zoom could not be confirmed: shortcut injection did not
  change observed viewport/DPR. Reflow evidence is not native zoom certification.
- Launcher migration check applied no migrations; bootstrap preserved existing
  users; shared-data sync checked 271 releases with 0 record operations and wrote
  its current local receipt. Speech metadata is configured, not a paid/live check.
- `git diff --check` passes. No existing known-issue entry matches this duplicated
  settings surface; append a new bug-memory entry. No schema/data/config changes,
  commit, push, CI or deployment. Existing port 5100 remains on its prior process;
  changed binaries are available on 5101 until its owner restarts 5100.
