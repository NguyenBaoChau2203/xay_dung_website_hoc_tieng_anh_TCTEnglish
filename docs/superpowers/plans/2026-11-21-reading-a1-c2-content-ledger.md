# Reading A1–C2 content packs — ledger (2026-11-21)

Local only; no push/PR/deploy/Publish.

## Deliverables

Six Excel imports under `.appdata/local/reading-imports/`:

| File | Readings |
| --- | --- |
| `TCT_Reading_Import_A1.xlsx` | RD-A1-001, RD-A1-002 |
| `TCT_Reading_Import_A2.xlsx` | RD-A2-001, RD-A2-002 |
| `TCT_Reading_Import_B1.xlsx` | RD-B1-001, RD-B1-002 |
| `TCT_Reading_Import_B2.xlsx` | RD-B2-001, RD-B2-002 |
| `TCT_Reading_Import_C1.xlsx` | RD-C1-001, RD-C1-002 |
| `TCT_Reading_Import_C2.xlsx` | RD-C2-001, RD-C2-002 |

Each reading: ≥2 paragraphs, 5 question types, Matching pairs, ≥5 vocab (VI meanings).  
`Question_ID` is workbook-unique (`RD-A1-001-Q1`, …) because Answers are indexed by Question_ID only.

## Source

- `TCTEnglish.Tests/ReadingContent/ReadingLevelPacks.cs`
- `TCTEnglish.Tests/ReadingContent/ReadingImportWorkbookBuilder.cs`
- `TCTEnglish.Tests/ReadingImportContentGeneratorTests.cs`

Regenerate: `dotnet test --filter FullyQualifiedName~ReadingImportContentGeneratorTests`

## Checks

- Generator + ParseAndValidateAsync: **2 passed** (0 FailedReadings × 6 files; A1 ConfirmImport in-memory → 2 Drafts)
- Local Admin smoke (`:5100`): A1 ImportPreview valid → ImportConfirm 302 → ImportResult shows RD-A1-001/002 success (Draft)

## Non-goals

Publish, shared-data package, media ZIP, parser change for multi-reading Question_ID scoping.
