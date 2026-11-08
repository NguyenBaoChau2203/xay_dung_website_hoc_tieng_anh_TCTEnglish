# Team Design System — execution ledger (2026-11-05)

Plan: `docs/superpowers/plans/2026-11-05-team-design-system.md`. Prompt:
`.ai/prompts/claude-code-team-design-system.md` (the owner's Downloads copy is
identical apart from a 4-line header).

## Baseline

| Item | Value |
| --- | --- |
| Starting branch / HEAD | `master` / `6af768b` (== `origin/master`, fast-forward pulled earlier today) |
| Working branch | `feature/team-design-system` (created from `6af768b`; no `codex/` prefix) |
| Pre-existing local change | `TCTEnglish/appsettings.Development.json` — one trailing blank line (owner's, preserved, **not part of this work**) |
| Data sync | post-merge hook ran shared-data sync: 1 catalog release, 4 record operations, `last-sync.json` current |
| CodeGraph | `.codegraph/` exists but the MCP server reported no index for this path → bounded Read/Grep used |
| Browser | no browser MCP; headless Chrome driven through a local DevTools-protocol script kept outside the repo |

## DS0 findings (evidence)

Each row: finding → source.

1. **Four token dialects, no shared root.** Shell hardcodes hex (no variables) in
   `layout.css`; `tct-learning-ui.css` has `--tct-*`; 11 feature stylesheets each
   declare their own `:root` palette (`--rd-*`, `--li-*`, `--lp-*`, `--w-*`,
   `--wi-*`, `--we-*`, `--stitch-*`, `--spk-*`, `--md-sys-*`, unprefixed
   `--brand/--surface/--primary` in Dashboard/account/vocabulary). 274 declarations
   across 13 files.
2. **Same brand, drifting neutrals.** `#4255ff` is universal. Text is `#303545`
   (shell body), `#111c2d` (7 sheets), `#1e293b` (Dashboard, Reading). Canvas is
   `#f6f7fb` (body), `#f8fafc` (`.main`), `#f9f9ff` (pages). Card radius 18px
   (Dashboard, Speaking, tct), 16/22 (Listening), 16/24 (Reading), 20 (modal).
3. **Unprefixed collisions.** `--surface` means `#f6f7fb` in Dashboard.css and
   `#f9f9ff` in account.css, both in `:root`; Admin loads account.css globally.
4. **Font cascade is flattened.** `layout.css` line 2: `* { font-family: Inter, … }`
   gives every element its own value, so `font-family` set on a wrapper
   (`.tct-page-bg`, `--spk-font`) never reaches descendants. `Be Vietnam Pro` is
   only loaded on Account/Auth/Profile/Settings/landing. Canonical product font =
   Inter (loaded by the shell, has Vietnamese subset); weights loaded: 400–700
   (`.logo` 900 / `.modal-title` 800 are synthesized).
5. **Resets differ per shell.** Learner shell zeroes margin/padding on `*`; Admin
   (Bootstrap only) does not. Component CSS must set its own margins.
6. **Offsets.** Shell: header 72px, sidebar 280px (240px ≤1024px), main padding
   40px (16px ≤768px), `.main` bg `#f8fafc`. Admin sidebar 260px is declared twice
   (`account.css :root` and an inline `<style>` in `_AdminLayout.cshtml`).
   `--spk-sidebar-w: 260px` in speaking.css has **no consumers**. `account.css`
   `width: var(--sidebar-width)` is the Profile page's own sidebar, not the shell.
7. **Layers.** header 1000, sidebar 900, custom `.modal-overlay` 2000, skip link
   10000; Admin sidebar 1050, admin skip link 1060; Bootstrap dropdown 1000,
   backdrop 1050, modal 1055, tooltip 1080, toast 1090. The custom overlay (2000)
   sits above Bootstrap toasts (1090).
8. **Breakpoints.** Shell: ≤1024, ≤768 (JS `mobileMql`), ≤576 (placeholder JS),
   ≤480. `site.css` flips root to 16px at `min-width:768px`, so exactly 768px
   matches both. Admin: ≤991.98px (Bootstrap `lg`).
9. **Icons.** Learner shell: Font Awesome 6.0.0 + Material Symbols. Admin:
   Bootstrap Icons 1.11.3 + Font Awesome 6.5.1.
10. **Feedback.** No shared learner toast helper. SweetAlert2 (pinned
    `/lib/pinned/sweetalert2-11.26.25.all.min.js`) is loaded per page; Admin layout
    loads it globally. The layout's `#folderModal` is a custom overlay (Escape and
    focus return exist, no focus trap); new work should use Bootstrap modal.
11. **`tct-learning-ui.css` is the closest thing to a shared layer** and is
    nearly unused: classes only in `Auth.cshtml` (modal button), `--tct-text-muted`
    in `auth-stitch.css` (7 uses); linked from Auth (`Layout=null`) and Listening.
12. **Contrast, computed from the real values** (WCAG 2.x, `contrast.cjs` in the
    session scratchpad):

    | Pair | Ratio | Result |
    | --- | --- | --- |
    | `#64748b` muted on canvas `#f6f7fb` | 4.45 | fails 4.5 (passes on white, 4.76) |
    | `#9ca3af` `.workspace-title` 11px on white | 2.54 | fails (legacy) |
    | header search placeholder, browser default `#757575` on `#f6f7fb` (**rendered**; my first source-based guess of `#9ca3af`/2.37 was wrong, that is the decorative icon) | 4.30 | fails 4.5 (legacy) |
    | `#10b981` as text on white | 2.54 | fails (fine as icon/fill only) |
    | `#d97706` as text on white | 3.19 | fails |
    | input border `#e2e8f0` on white | 1.23 | fails 3:1 control boundary |
    | `#4255ff` focus ring on Admin dark `#212529` | 2.92 | fails; white passes 15.4 |
    | Admin sidebar section label, **rendered** `#745b00` on `#212529` (first estimate from Bootstrap `#6c757d` was 3.29) | 2.38 | fails (legacy) |
    | white on `#4255ff` | 5.28 | passes |
13. **Focus (confirmed in the browser).** `site.css` forces `.btn:focus` ring `#258cfb`
    (template default). The header search input has `outline: none` and no replacement
    (Tab stop reports `outline none`). In the Admin sidebar the brand link shows a dark
    `auto` outline (`rgb(16,16,16)`) on the dark background and nav links show only a
    faint `rgba(13,110,253,.25)` Bootstrap shadow.
14. **`account.css` overrides Bootstrap on every Admin page.** Line 960:
    `.text-secondary { color: #745b00 !important; }`. `_AdminLayout.cshtml` links this
    file, so all Admin `text-secondary` text is brown, which is what produces the
    2.38:1 sidebar labels. Competing definition, not touched here.
15. **Shell header height.** Logged-in header measures 83/83/79/75/75px at
    1440/1024/768/390/375 while `.sidebar` top and `.main` margin-top are 72px, so it
    covers 3-11px. Anonymous header is 72-73px. Baseline, unchanged.
16. **Page routes.** Study pages live under `/Home/` because `StudyController` has
    `[Route("Home")]` (`/Home/Listening`, `/Home/Writing`, `/Home/Reading`,
    `/Home/Listening/Practice/{id}`); `/Study/*` and `/Listening` are 404.

## Decisions

| # | Decision | Rationale |
| --- | --- | --- |
| D1 | Extend `tct-learning-ui.css`; no new token file | Existing namespace, 2 consumers, no competing layer |
| D2 | Inter is the canonical font | Loaded everywhere by the shell; Vietnamese subset; avoids adding a font |
| D3 | Canonical text `#111c2d`, secondary `#444656`, muted `#5b6578` | Majority value; muted fixed to pass 4.5:1 on canvas, white and tint |
| D4 | Control borders `#7c869b`, decorative borders keep `#e5e7eb` | 3:1 for component boundaries; decorative separators exempt |
| D5 | Semantic **text** colors are separate from fills | `#10b981`/`#d97706` fail as text |
| D6 | Bootstrap bridge is opt-in via `.tct-theme` | Global Bootstrap primary change = brand decision for Chau |
| D7 | Shell offsets move to tokens at identical values | Single source, no visual change |
| D8 | Root font size (14px <768px) not changed | Affects every rem value; proposal only |
| D9 | Legacy `--tct-*` values with consumers are kept (`--tct-border-input`, `--tct-font-family`, `--tct-text-main`, `--tct-white` ...) except `--tct-text-muted` | `auth-stitch.css` uses 7 of them, `listening-index.css` 1. Muted changed `#64748b` to `#5b6578` (4.45 to 5.48 on canvas); isolated to 15 `auth-*` elements, max channel delta 19/255 |
| D10 | `tct-theme` does not map warning/success to Bootstrap | Their brand fills fail as text; `.tct-badge--*` carries the text-safe pairs |

## Checkpoints

### DS1 tokens + shell integration (done)

- `tct-learning-ui.css`: legacy block kept, new token set (color, semantic fill/text, focus,
  type, spacing, shape, controls, shell geometry, z-layers, motion), reduced-motion durations.
- `layout.css`: only literals replaced by equal-valued tokens with fallbacks (header/sidebar/main
  offsets, header/sidebar z-index). `_Layout.cshtml` links the shared file before `layout.css`;
  `_AdminLayout.cshtml` links it and its duplicate `--sidebar-width: 260px` now reads
  `--tct-admin-sidebar-w`. `Listening.cshtml` lost its now-redundant link.
- Evidence: 75 page/viewport combinations (public, Standard, Admin; 1440/1024/768/390/375)
  captured before and after: **0 computed-style differences** for header, sidebar, main offsets,
  fonts, colors, overflow. See the A/B section for pixels.

### DS2 components (done)

- Opt-in `.tct-theme` Bootstrap bridge, `.tct-btn-icon`, page header, toolbar, badges, tables,
  state blocks, skeleton, prose/phonetic helpers. Partials `_PageHeader`, `_StateMessage`
  with typed models in `ViewModels/SharedUiViewModels.cs` (namespace `TCTEnglish.ViewModels`).
- No page in the application uses the partials yet (adoption is teammates' work), so they are
  proven by `SharedUiPartialsRenderTests` (8 tests through the real Razor engine: encoding,
  local-only URLs, one `h1`, `role`/`aria`). Mutation check: removing the `Url.IsLocalUrl`
  guards made 2 tests fail, then the file was restored.
- Bugs I introduced and fixed while testing: (1) a long unbreakable word overflowed a card and
  widened the whole page, fixed in `.tct-card` (`min-width:0`, `overflow-wrap:anywhere`); (2) the
  shared `:focus-visible` rule matched `[tabindex]`, so a keyboard-opened Bootstrap modal
  (`tabindex=-1`) got a 2px ring on the whole `.modal`, fixed with `:not([tabindex="-1"])`;
  (3) checkbox showed a ring on mouse click, now `:focus-visible` only. These are new code, not
  released bugs, so no bug-log entry.

### DS3 gallery (done)

`docs/design-system/preview.html` loads the real `tct-learning-ui.css`, `layout.css` and
`layout.js`; token values and contrast are read live from CSS. Headless Chrome (local
DevTools script outside the repo) results:

| Check | Result |
| --- | --- |
| Console errors / failed requests | none |
| Horizontal overflow at 1440, 1024, 768, 390, 375, 720 (=200% zoom at 1440), 360 (=400%) | none (checked per element because `body` has `overflow-x:hidden`) |
| Token file loaded, one `h1` | yes at all widths |
| Modal opened by keyboard | Tab stays inside, Escape closes, focus returns to the trigger, no ring on the `.modal` container |
| Mobile drawer (`layout.js`) | `aria-expanded` and `inert` toggle, focus moves in, Escape closes and returns focus to the button |
| Pending-button demo | `disabled` + `aria-busy` during, restored after |
| Form submit | stays on the page |
| Themed controls | inputs/buttons show a solid 2px `#4255ff` outline, no template box-shadow |
| IPA and Vietnamese glyphs | visually inspected: no missing glyphs (Inter) |

Rendered contrast (computed foreground vs effective background, 32 component pairs): 31 pass;
the only failure is the disabled input (4.24:1, WCAG-exempt). Shell elements measured the same way:
`.workspace-title` 2.54 (fail), header search placeholder 4.30 (fail), sidebar nav 7.56 and 4.77 (pass).

### Regression A/B against the real app (done)

Old CSS was served from `git show HEAD:` through request interception and compared with the new
CSS in the same browser session, with a noise floor (same CSS captured twice), 3s settle.

- **0 differing pixels** (noise 0): `/Home/Introduction` (1440, 390), `/Home/Listening` (1440, 390),
  `/Account/Settings`, `/Home/Writing`, `/Admin/UserManagement`, `/Admin/SetManagement`,
  `/Home/Listening/Practice/1` (1440; and 390 in a 5-capture interleaved run).
- Differences no larger than the noise floor (animation / dynamic content): `/Home/Index`,
  `/Vocabulary/Index`, `/Speaking`, `/Goals`.
- `/Account/Login`: noisy (animated background); the intended muted-color change is isolated by
  computed style: the same 15 `auth-*` elements move from `rgb(100,116,139)` to `rgb(91,101,120)`.
- One unexplained transient: a single run showed 250 differing pixels (max delta 82) on
  `/Home/Listening/Practice/1` at 390 while noise was 0. It did not reproduce in a 5-capture
  interleaved run (0 pixels), and a computed-style diff over all 400 elements found 0 differences.
  Reported rather than hidden; cause not established.
- Final re-measure of the 75 combinations (taken after the `.tct-card` fix): 0 computed-style
  differences. Pixel hashes differed on many pages in that run because it ran while the unittest
  suite loaded the machine (web-font/icon timing); the controlled A/B above is the pixel evidence.
  The later focus-rule edits only touch `.tct-theme` / `.tct-focusable` / `.tct-on-dark` selectors,
  which no application page uses yet; they were verified by the gallery re-run, not by another shell pass.

### DS4 docs and agent rules (done)

`team-ui-workflow.md` (Vietnamese), `.ai/prompts/implement-page-with-team-design-system.md`
(English), `AGENTS.md` (rule 6, new "Shared UI foundation" block, routing row), links added in
`docs/project-structure.md` and `README.md`. One policy source: `AGENTS.md` is already read by
all four clients; `CLAUDE.md` is untouched. No skill, hook, MCP, lock or bootstrap file changed,
so no trust/reload step is needed and the bootstrap/native-hook checks are not applicable.

### DS5 verification summary

| Check | Result |
| --- | --- |
| `dotnet build` (Debug) | 0 errors; 43 warnings, all pre-existing, none from new files |
| `SharedUiPartialsRenderTests` | 8/8 pass |
| Full `dotnet test` (Debug) | 960 passed, 0 failed, 19 skipped (live OpenAI/MariaDB acceptance tests that need their own environment; they skip by design) |
| `uv run --no-project python scripts/agent_tooling.py` (pinned uv from `tooling/.runtime`) | passed |
| `python -m unittest discover -s scripts/tests -v` | 135 tests OK, 1 skipped (pre-existing) |
| Browser gallery, A/B, Admin, Auth | as above |

Not verified, stated plainly:
- The **Premium** role was not walked through; Standard, Admin and anonymous were.
- Only the Listening practice screen was exercised as a practice page (Speaking, Writing, Vocabulary
  study pages need ids/state and were not individually A/B'd; they were covered by shell metrics only
  where they are reachable without ids).
- Admin interactions beyond load/focus (mobile drawer, dropdown) were not re-walked; the Admin JS is untouched
  and the 0-pixel A/B plus identical computed metrics cover the layout change.
- Firefox/Safari, a real screen reader and real OS text-size changes were not tested; 200%/400% zoom is
  emulated by viewport width.
- The static gallery needs internet for the same CDNs the app uses and does not prove server rendering or RBAC.
- Starting the app with `scripts/start-local.ps1` also ran its normal startup (`ef database update`,
  shared-data sync) against the local Docker database; no migration or data change originates from this work.
- Nothing was committed, pushed or released. `appsettings.Development.json` still carries the owner's
  pre-existing one-line whitespace change.

## Decisions for Chau

See "Open proposals for Chau" in `DESIGN.md` (global `tct-theme`, mobile root font size, header height,
legacy contrast/focus failures including the `account.css` `!important`, `#folderModal`, palette consolidation).
Also: whether `tct-learning-ui.css` should stay the single token file (recommended) and who owns each
screen in the adoption tracker.
