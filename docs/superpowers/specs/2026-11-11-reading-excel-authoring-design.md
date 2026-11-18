# Reading Excel Authoring Design (2026-11-11)

## Locked decisions

| Topic | Decision | Rationale |
|---|---|---|
| Workbook | Exactly 5 data sheets + Guide | Owner prompt §4 |
| Import status | **Draft** (`IsPublished=false`) | Preserves Admin review → Approve → Publish; students only see Published |
| Duplicate ReadingCode | Reject that reading; never overwrite | Owner prompt §6.2 |
| Partial import | Per-reading transaction; skip failures | Owner prompt §6.1 |
| Question types | MultipleChoice, TrueFalse, GapFill, Matching, ShortAnswer | Existing project; no new types |
| Questions/Answers columns | Keep existing question/answer column names; `Reading_ID` → `ReadingCode` | Multi-reading grouping |
| Vocabulary Meaning | Maps to `VietnameseMeaning` | Owner Meaning = nghĩa tiếng Việt |
| Pronunciation | Maps to `Ipa` | Existing field |
| Schema | No new tables; reuse entities | Plan Approach A |

## Sheet contracts

### Reading_Info

`ReadingCode*`, `Title*`, `CEFR*`, `Topic*`, `Duration*`, `Description`

### Passage

`ReadingCode*`, `PassageTitle*`, `ParagraphNo*`, `Content*`

### Questions

`Question_ID*`, `ReadingCode*`, `Question_Order*`, `Question_Type*`, `Question_Content*`, `Paragraph_Reference`, `Points`

### Answers

`Question_ID*`, `Option_ID`, `Option_Content`, `Is_Correct`, `Explanation`, `Evidence_Text`, `Evidence_Paragraph_ID`, `Paraphrase`

### Vocabulary

`ReadingCode*`, `Word*`, `Pronunciation`, `PartOfSpeech`, `Meaning*`, `Example`, `ParagraphNo`

## Entity mapping

| Excel | Entity field |
|---|---|
| ReadingCode | `ReadingPassage.ExternalReadingKey` |
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
