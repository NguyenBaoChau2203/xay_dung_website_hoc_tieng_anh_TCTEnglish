# Reading Excel Authoring Design (2026-11-11)

## Locked decisions

| Topic | Decision | Rationale |
|---|---|---|
| Workbook | Exactly 5 data sheets + Guide | Owner prompt §4 |
| Import status | **Draft** (`IsPublished=false`) | Preserves Admin review → Approve → Publish; students only see Published |
| Excel ReadingCode | Temporary join key only (unique in file). Import **always** assigns `RD-{CEFR}-NNN`; never overwrite DB keys (2026-11-21) | Owner import UX |
| Partial import | Per-reading transaction; skip failures | Owner prompt §6.1 |
| Question types | MultipleChoice, TrueFalse, GapFill, Matching, ShortAnswer | Existing project; no new types |
| Questions/Answers columns | Keep existing question/answer column names; `Reading_ID` → `ReadingCode` | Multi-reading grouping |
| Vocabulary Meaning | Maps to `VietnameseMeaning` | Owner Meaning = nghĩa tiếng Việt |
| Pronunciation | Maps to `Ipa` | Existing field |
| Schema | No new tables; reuse entities | Plan Approach A |

## Sheet contracts

### Reading_Info

`ReadingCode*`, `Title*`, `CEFR*`, `Topic*`, `Duration*`, `Description`, `ImageUrl` (optional; corrected 2026-11-21)

### Passage

`ReadingCode*`, `PassageTitle*`, `ParagraphNo*`, `Content*`

### Questions

`Question_ID*`, `ReadingCode*`, `Question_Order*`, `Question_Type*`, `Question_Content*`, `Paragraph_Reference`, `Points`

### Answers

`Question_ID*`, `Option_ID`, `Option_Content`, `Is_Correct`, `Explanation`, `Evidence_Text`, `Evidence_Paragraph_ID`, `Paraphrase`, `Match_Key`, `Match_Side` (Matching; corrected 2026-11-21)

### Vocabulary

`ReadingCode*`, `Word*`, `Pronunciation`, `PartOfSpeech`, `Meaning*`, `Example`, `ParagraphNo`

> Template v2 authoring UX (Guide table, dropdowns, full five-type sample): see `2026-11-21-reading-excel-template-v2-design.md`.  
> CEFR content length, assessment difficulty, and editorial rules: [`docs/content/reading-content-authoring-guidelines.md`](../../content/reading-content-authoring-guidelines.md) (Layer 3 = this Excel contract; Layers 1–2 = language/assessment).

## Entity mapping

| Excel | Entity field |
|---|---|
| ReadingCode (Excel) | Temporary join / ZIP match (`ImportFileReadingCode`); system assigns `ExternalReadingKey = RD-{CEFR}-NNN` |
| CEFR | `Level` |
| Duration | `EstimatedTimeMinutes` |
| PassageTitle | Must match `Title` for the reading (validated); display uses `Title` |
| ParagraphNo | `ReadingParagraph.ParagraphOrder` (+ `ExternalParagraphKey` = `P{n}`) |
| Content (passage) | `ParagraphContent`; denormalized join → `ReadingPassage.Content` |
| Meaning | `VietnameseMeaning` |
| Pronunciation | `Ipa` |
| ParagraphNo (vocab) | Resolve to `ReadingVocabularyItem.ParagraphId` |

## Import flow

1. Admin downloads template / uploads `.xlsx`
2. Parse + validate sheets/columns
3. Group by ReadingCode; validate each reading independently
4. Preview: valid + failed + warnings (duplicate words)
5. Confirm: import each valid reading as Draft in its own transaction
6. Report Success / Failed / Skipped
