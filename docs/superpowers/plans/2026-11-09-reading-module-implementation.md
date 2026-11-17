# Reading Module Implementation Ledger (2026-11-09)

Executed from SRS Reading 1.1 + project audit plan. Local only; no push/PR/deploy.

## Decisions locked

- `User.ReadingLevel` (register choose, default A1, Admin edit)
- Reviewer = Admin ≠ `CreatedByUserId` (no new role)
- Question types: MultipleChoice, TrueFalse, GapFill, Matching, ShortAnswer
- ClosedXML added for Excel template/import/export

## Delivered phases

0. Admin Reading authorize + antiforgery; Study/Submit published gate  
1. Schema/migration `AddReadingModuleSchema` + `FixReadingQuestionPointsDefault`; Register/Admin ReadingLevel; SharedDataRegistry  
2–4. `ReadingAdminService` draft/submit/review/publish + Excel Import preview/confirm  
5–7. Level-gated library, study attempt/autosave/timer/mobile tabs, scoring + explanations  
8. Notebook + Progress  
9. `ReadingModuleTests` (11 passed)

## Migrations

- `20261109091316_AddReadingModuleSchema`
- `20261109092305_FixReadingQuestionPointsDefault`

Applied on local MariaDB `tctenglish_dev`. Model pending check: clean.

## Checks

- `dotnet build` Release: succeeded  
- `dotnet test --filter ReadingModuleTests`: 11 passed  
- Browser multi-viewport: not observed in this run (start local web to verify UI)

## Risks

- Admin question JSON form is content-first; full typed questions best via Excel round-trip  
- Import confirm embeds preview JSON in hidden field (size limits for very large workbooks)  
- Practice scores do not change `ReadingLevel`
