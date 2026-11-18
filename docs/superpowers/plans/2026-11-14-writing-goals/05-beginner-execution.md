# Beginner batch execution checklist

Authority: owner launch on 2026-11-14; IMPLEMENT_LOCAL, one agent. Exactly 36 new
beginner public lessons, six per existing format. Overall reserved quota is108.
Preserve all prior-stage dirty work, all existing content and learner rows.
No Git publication, deployment, paid API, protected configuration or schema edit.

- [x] Read stage05, source contracts, authoring/import/shared-data procedures.
- [x] Inspect branch and existing work; inspect reference pages without copying lessons.
- [x] Implement typed bilingual validation and explicit offline importer with safe receipts.
- [x] Capture immutable ignored local baseline, content snapshot and row hashes.
- [x] Author36 lessons /288 sentence pairs /36 semantic alternatives; reserve72 future slots.
- [x] Pass six-format pilot access/assessment/progress/completion tests and local pilot import.
- [x] Import remaining30; rerun with zero new rows; prove restart recovery and preservation.
- [x] Export36 immutable canonical packages; validate privacy/hash/history and no-op export.
- [x] Run canonical Sync twice; verify fresh last-sync receipt and unchanged original data.
- [x] Prove content/answer roundtrip and repeat on fresh MariaDB10.5.23.
- [x] Review diff, record full18-cell matrix and actual checks; hand off in Vietnamese.

Data: `data/writing/2026-11-14/{manifest,beginner}.json`.
Typed boundary: `TCTEnglish/Infrastructure/WritingContent/WritingContentBatch.cs`.
Explicit utility: `scripts/writing-content.{cs,ps1}`; never starts the web host.
Regression: `TCTEnglish.Tests/WritingContentBatchTests.cs`.
Runtime receipts/backups: ignored `.appdata/local/writing-content-20261114/`.
Ledger: `docs/feature-audits/2026-11-14-writing-goals/05-beginner-ledger.md`.

Meaningful acceptance: exact old-row hash multisets survive; no personal entities
in packages; all36 full lessons roundtrip with paragraphs/answers intact; only the
beginner matrix increases; no duplicate lessons/sentences/variants on rerun.
