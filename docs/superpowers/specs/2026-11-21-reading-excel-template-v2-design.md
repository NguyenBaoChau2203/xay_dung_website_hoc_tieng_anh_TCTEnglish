# Reading Excel Template v2 Design (2026-11-21)

## Goal

Complete the Admin downloadable Reading Excel template and polish the Import UI so authors can fill all five question types correctly, then import Draft passages without schema or pipeline changes.

## Locked decisions (carry forward)

| Topic | Decision |
|---|---|
| Workbook | Guide + 5 data sheets (`Reading_Info`, `Passage`, `Questions`, `Answers`, `Vocabulary`) |
| Schema | No new columns required; keep existing headers including `ImageUrl`, `Match_Key`, `Match_Side` |
| Import | Draft only; partial import; Excel ReadingCode is join-only; system always assigns `RD-{CEFR}-NNN`; duplicate file keys hard-fail |
| Question types | MultipleChoice, TrueFalse, GapFill, Matching, ShortAnswer |
| Libraries | ClosedXML only (already in tree); no Program.cs / package changes |

## Drift corrected vs 2026-11-11 sheet contract

- `Reading_Info` includes optional `ImageUrl` (path `/images/...`, `/uploads/...`, or `https://...`).
- Optional media ZIP at Import: root files `{file ReadingCode}.jpg|png|webp|gif` win over `ImageUrl`.
- `Answers` includes `Match_Key` and `Match_Side` (`Left`|`Right`) for Matching.
- Learner Study UI chips (2026-11-21) do not change Excel type strings; Guide documents Vietnamese labels for authors only.
- Excel `ReadingCode` (e.g. `L1`) is a temporary join key; import assigns `ExternalReadingKey = RD-{CEFR}-NNN`.

## Template v2 authoring UX (Excel)

1. **Guide sheet** — structured table: rules, CEFR list, question-type → Answers pattern, media rules, Evidence_Paragraph_ID = Passage ParagraphNo, ReadingCode = join key only.
2. **Sample reading `L1`** — one coherent passage with **one sample question per type** (5 questions) and matching Answers/Vocabulary rows so `ParseAndValidateAsync` yields a valid Draft candidate with system code `RD-A1-001` (empty DB).
3. **Styling** — frozen header row on data sheets; bold header fill; sensible column widths; wrap text on Content/Question_Content.
4. **Data validation** — dropdowns on sample/data ranges:
   - `Reading_Info!CEFR` → A1–C2
   - `Questions!Question_Type` → five types
   - `Answers!Is_Correct` → TRUE/FALSE
   - `Answers!Match_Side` → Left/Right
5. **No schema rename** — headers remain English identifiers used by the parser.

### Sample question map (L1)

| Question_ID | Type | Answers pattern |
|---|---|---|
| L1-Q1 | MultipleChoice | Options A/B…; one `Is_Correct=TRUE` |
| L1-Q2 | TrueFalse | TRUE/FALSE options; one correct |
| L1-Q3 | GapFill | `Option_ID=ANSWER`, `Is_Correct=TRUE`, text = accepted answer |
| L1-Q4 | Matching | Pairs share `Match_Key`; `Match_Side` Left/Right; `Is_Correct=FALSE` |
| L1-Q5 | ShortAnswer | `Option_ID=ANSWER`, `Is_Correct=TRUE` |

## Admin Import UI (Clay / anti-slop)

Scope: Index CTA cluster, Import, ImportPreview, ImportResult, `_ReadingImportSteps`.

- Keep `.tct-theme` + `--tct-*` tokens in `admin-reading.css`; no new palette/hex/CDN/font.
- One job per section; quiet Clay cards; no purple/glow/gradient hero/stat-pill clusters.
- Strengthen step indicator readability; file upload affordance; type-chip legend on Import “prepare” card matching Study labels.
- Mobile + desktop in the same change (375–390 / 768 / 1440).

## Design / UI checklist

- [ ] Guide lists all five types and Answer patterns
- [ ] Template parse → single valid reading with 5 questions
- [ ] Header freeze + CEFR/type dropdowns present
- [ ] Import pages use tokens only; equal content edges; hit targets ≥44px
- [ ] No AI-slop patterns (purple-on-white, glow, emoji decoration, multi-shadow stacks)

## Tester checklist

1. DownloadTemplate opens; sheets/columns match contract.
2. Import sample template → Preview valid → Confirm → Draft created.
3. Break CEFR / missing Meaning / duplicate ParagraphNo → FailedReading + error report.
4. Matching Left/Right with shared Match_Key scores after publish path (existing study tests).
5. Viewport 1440 / 768 / 390: Import steps + upload form usable; no page overflow-x.

## Non-goals

Schema/migration, Program.cs, learner Study rewrite, new question types, push/PR/deploy.
