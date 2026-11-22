# Reading Excel Template v2 — execution ledger (2026-11-21)

Local only; no push/PR/deploy. Scope B: workbook + Admin Import UI.

## Done

- Spec: `docs/superpowers/specs/2026-11-21-reading-excel-template-v2-design.md`
- Drift fix: `docs/superpowers/specs/2026-11-11-reading-excel-authoring-design.md` (+ ImageUrl / Match_*)
- `SPEC.md` C.5 note for template v2
- `ReadingExcelService.BuildTemplate`: Guide table, freeze/headers/validation, RD001 sample with all five question types
- Tests: `ReadingModuleTests` Excel/parse cases updated — **28 passed** (Release isolated output)
- Admin Import / steps / Index CTA polish in `admin-reading.css` + views
- Download filename: `Reading_Template_v2.xlsx`

## Browser evidence (Release http://127.0.0.1:5110)

| Check | Result |
| --- | --- |
| Import 1440 | chips ×5; download “Tải template Excel v2”; overflowX false |
| Import 768 | overflowX false; steps 3-col |
| Import 390 | steps stacked 1-col; min hit 44px; overflowX false |
| DownloadTemplate | Guide first; 5 Questions rows; sheets complete |

## Anti-slop / DESIGN

- Tokens only (`--tct-*`); Clay cards; no purple/glow/new CDN/font
- Keyboard: not fully exercised in automation (native file inputs remain)

## Non-goals preserved

Schema/migration, Program.cs, learner Study rewrite, push/PR.
