# Import always assigns system ReadingCode — ledger (2026-11-21)

Local only; no push/PR/deploy.

## Behavior

- Excel `ReadingCode` = temporary join key only (`L1`/`L2`, unique in file).
- Import always assigns `ExternalReadingKey = RD-{CEFR}-NNN` (next free per CEFR).
- Duplicate file keys hard-fail; never overwrite existing DB keys.
- Preview shows system code + “Mã file: …”; Guide/Import copy updated.
- Media ZIP still matches `ImportFileReadingCode`.

## Checks

- Unit: `ReadingModuleTests` + `ReadingImportContentGeneratorTests` + evidence Excel — **passed** (isolated Release out; Debug bin locked by running web).
- Re-import A1 (same file twice in ConfirmImport test) → `RD-A1-001`…`004`.
- Content packs under `.appdata/local/reading-imports/` use L1/L2 (verified sharedStrings).
- Live HTTP smoke deferred: `start-local` Release rebuild blocked by locked `bin/Debug`/`TCTEnglish.exe` (pid holding port 5100).
