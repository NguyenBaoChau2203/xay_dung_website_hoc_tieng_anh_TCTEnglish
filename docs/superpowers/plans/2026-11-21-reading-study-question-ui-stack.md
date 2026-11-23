# Reading Study question UI — Variant A Matching + shared kit

> Scoped ledger for `/Reading/Study/{id}` answer surfaces. Execute inline; no subagents.

**Goal:** Fix Matching mid-column wrap with stack prompt→select; unify Clay answer surfaces for all five question types without changing autosave/scoring contracts.

**Architecture:** Markup + scoped CSS only in the sticky question panel. Keep `.matching-row`, `.matching-select`, `data-left-id`, radio and `.reading-text-answer` wiring.

**Tech stack:** Razor, Bootstrap 5, `reading-study.css` `--tct-*` / Clay tokens.

## Locked design kit

| Type | Chip label | Surface |
| --- | --- | --- |
| MultipleChoice | Trắc nghiệm | Full-width option rows, `overflow-wrap: break-word`, min-height 44px |
| TrueFalse | Đúng/Sai | Same as MC |
| GapFill | Điền khuyết | Visible “Nhập đáp án” + Clay input |
| ShortAnswer | Trả lời ngắn | Visible “Nhập đáp án” + Clay textarea |
| Matching | Ghép cặp | Variant A stack pair cards; select full width |

## Files

- `TCTEnglish/Views/Study/ReadingStudy.cshtml`
- `TCTEnglish/wwwroot/css/reading-study.css`
- `TCTEnglish/wwwroot/js/reading-study.js` — no change expected

## Checklist

- [x] Matching Variant A markup (column stack, pair cards, visible “Ghép với”)
- [x] Remove `flex-sm-row` / `flex: 0 0 280px` select lock
- [x] Shared `.reading-q-block` + type chips + MC/text polish
- [x] Browser: 1440 / 768 / 390; Matching stack + equal prompt/select width; autosave select→nav done
- [x] Anti-slop / DESIGN gate recorded (task UI; no purple/glow; tokens only)

## Verification evidence (local Study/11)

| Check | Result |
| --- | --- |
| Desktop 1440 Matching | `flex-direction: column`; left/select width ~343; left ~3 word-wrap lines; `overflow-wrap: break-word`; min-height select/option 44px |
| 768 Questions tab | stacked; left=select ~635; no page overflow-x |
| 390 Questions tab | stacked; left=select ~270; no page overflow-x |
| Type chips | MC Trắc nghiệm; TF Đúng/Sai; Gap Điền khuyết; SA Trả lời ngắn; Matching Ghép cặp |
| Autosave contract | Matching `change` marks nav done (`nav-btn-2.done`) |
| Zoom 200% (body.style.zoom) | Stack retained; page-level overflow-x observed (shell+zoom sim) — Matching not mid-word column |
| Keyboard | Not fully exercised in automation; Tab targets remain native radio/input/select |

## Follow-up: passage + vocabulary display (2026-11-21)

- Replaced wide vocab `<table>` with stacked `.reading-vocab-entry` cards (no horizontal scroll).
- Passage grid `minmax(0,1fr)`, title/body `overflow-wrap: break-word` (not mid-word column).
- Evidence: Study/11 — 14 entries; title width = scrollWidth; passageOverflowX false.

## Non-goals

Variant B/C, panel width redesign, Admin editor, schema, Program.cs, commit/PR.
