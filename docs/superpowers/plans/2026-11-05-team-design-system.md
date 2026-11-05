# Team Design System — scoped plan (2026-11-05)

Source prompt: `.ai/prompts/claude-code-team-design-system.md`. Mode:
`IMPLEMENT_SHARED_FOUNDATION`, local only, one agent. Ledger:
`docs/feature-audits/2026-11-05-team-design-system-ledger.md`.

Goal: a shared, working design foundation (tokens + scoped components + gallery +
docs + agent rules) that teammates adopt per screen. Not a site-wide redesign.

## Decision record (from DS0 evidence, see ledger)

- **Reuse** `TCTEnglish/wwwroot/css/tct-learning-ui.css` as the shared token and
  component layer. It already owns the `--tct-*` / `.tct-*` namespace and has only
  two consumers (Auth, Listening), so it is the lowest-risk single source. No new
  competing token file.
- **Bootstrap-first**: standard controls stay Bootstrap 5.3.2; a scoped
  `.tct-theme` bridge maps TCT tokens onto Bootstrap variables. Nothing is
  re-themed globally; pages opt in with one wrapper class.
- **Shell untouched visually**: `layout.css` only swaps literal offsets for equal
  valued tokens (zero visual change, verified by computed-style before/after).
- Brand, primary color, navigation, root font size and global Bootstrap theme are
  **proposals for Chau**, not applied.

## Batches

| Batch | Deliverable | Files | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| DS0 | Investigation, baseline, decision record | ledger | — | Findings cite source/browser evidence; structure decision recorded |
| DS1 | Tokens in code + DESIGN.md | `tct-learning-ui.css`, `layout.css` (offset tokens), `_Layout.cshtml`, `_AdminLayout.cshtml`, `DESIGN.md` | DS0 | Every documented token exists in CSS; shell computed offsets unchanged; Listening/Auth unaffected |
| DS2 | Components + minimal integration | `tct-learning-ui.css`, `PageHeaderViewModel.cs`, `StateMessageViewModel.cs`, `_PageHeader.cshtml`, `_StateMessage.cshtml` | DS1 | `dotnet build` passes; partials take typed models, encode text; no raw entities |
| DS3 | Gallery | `docs/design-system/preview.html` | DS1, DS2 | Opens from disk using repo CSS/JS; desktop/tablet/mobile + zoom evidence; no overflow; forms inert |
| DS4 | Team docs + agent rules | `docs/design-system/team-ui-workflow.md`, `.ai/prompts/implement-page-with-team-design-system.md`, `AGENTS.md`, docs index links | DS1–DS3 | One source of rules; all four clients read the same files |
| DS5 | Review + verification | ledger | all | Build, tooling checks, browser evidence; unverified items listed |

## Hard boundaries (carry through every batch)

No change to `Program.cs`, appsettings, `.csproj`, controllers, services,
entities, DbContext or migrations. No new package/CDN/font/icon family. No
public demo route. No commit/push/PR/CI/merge/deploy. Preserve selectors/IDs
consumed by `layout.js`, Bootstrap, SignalR and page JS.

## Verification checklist (filled in the ledger)

Status after execution (details and caveats in the ledger):

- [x] `dotnet build` (Debug) succeeds.
- [x] Learner shell computed metrics identical before/after (public page + logged-in): 0 differences in 75 combinations.
- [x] Admin shell renders; layout identical (0 differing pixels); Admin JS untouched. Mobile drawer/dropdown not re-walked.
- [x] Gallery at 1440 / 1024 / 768 / 390 / 375, 200%/400% zoom equivalents, keyboard, modal focus return.
- [x] Contrast table computed from final token values; rendered measurement of 32 component pairs (31 pass, 1 exempt).
- [x] `uv run --no-project python scripts/agent_tooling.py` (pinned uv from `tooling/.runtime`).
- [x] `uv run --no-project python -m unittest discover -s scripts/tests -v` (135 OK, 1 skipped).
- [x] Diff reviewed; screenshots only in ignored `.tmp/team-design-system/` and use synthetic accounts.
- [ ] Not done by design: commit, push, PR, CI, merge, deploy (owner stages).
