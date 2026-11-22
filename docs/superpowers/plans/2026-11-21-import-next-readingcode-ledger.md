# Import next ReadingCode — ledger (2026-11-21)

Local only; no push/PR/deploy.

## Behavior (superseded 2026-11-21 — system assign)

Superseded by `2026-11-21-import-system-assign-readingcode-ledger.md`: Excel codes are join-only; import always assigns `RD-{CEFR}-NNN`.

- Legacy note: Duplicate `ReadingCode` previously remapped to next free trailing-digit code.
- Media ZIP matches `ImportFileReadingCode` (Excel/ZIP name) then final key.

## Checks

- Unit: AllocateNext + Parse remap DB/file + related Excel tests — **7 passed**
- Smoke Release `:5111`: re-import `TCT_Reading_Import_A1.xlsx` → Preview remapped `RD-A1-003`/`004` → Confirm success
