# Reading Excel Authoring — execution ledger (2026-11-11)

Local only; no push/PR/deploy.

## Locked

- Import → **Draft** (review/publish unchanged)
- Workbook 5 sheets with prompt column names
- Questions/Answers: existing types; `ReadingCode` on Questions
- Partial import; no overwrite of ReadingCode

## Done

- Spec: `docs/superpowers/specs/2026-11-11-reading-excel-authoring-design.md`
- `ReadingExcelService` rewrite: template/export/parse/confirm/error report
- Admin Import preview/result + error report download
- Student study: structured paragraphs + vocabulary panel
- Admin edit: show paragraphs/questions/vocab summary
- Vocab `ParagraphOrder` → `ParagraphId` on save

## Checks

- `dotnet test --filter ReadingModuleTests`: **18 passed**

## Schema

- No new migration (mapped to existing entities)
