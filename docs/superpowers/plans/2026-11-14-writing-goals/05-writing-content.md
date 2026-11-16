# 05 — Add 108 complete Writing lessons in three bounded batches

Model: GPT 6.1 Sol High. Local content/tooling implementation when invoked.
Prerequisites: 02 Writing behavior and 03/04 learning contracts. Read README quota.
Use three chats: beginner, intermediate, advanced. Each chat finishes36 new lessons,
six per existing format. The final quota is **108 net new**, not144 and not merely
six lessons including pre-existing content. No A1–C2 taxonomy change.

## Stage 05a — inventory, tooling and beginner 36

- [x] Inspect source plus read-only local public catalog. Save a before matrix
  by level/content type, excluding private/unpublished rows. Preserve source IDs,
  old lesson/answer bytes and affected learner-row evidence in ignored backups.
- [x] Define a public manifest for 108 slots, 18 cells, stable per-item keys, level,
  format, topic/title, original/licensed source status, authoring provenance and
  validation state. Expected future slots are not counted as completed lessons.
  Use per-batch receipt/checkpoints so another chat resumes without duplicate work.
- [x] Inspect EnglishDatpmt's public Writing flow and verified educational sources
  for format/topic ideas. Record exact URLs/access date/use/rights per reused
  material. Author original bilingual exercises by default. Do not bypass access
  restrictions or reproduce an unlicensed third-party lesson collection.
- [x] Build/reuse a typed local importer/validator with dry-run, transaction,
  per-item stable identity, restart receipts and no-op reruns. Do not add an
  ordinary-startup seeder or register new services through protected config.
- [x] Draft/validate one complete original pilot per format before scaling to
  six. Verify actual exercise access, sentence order, accepted-answer evaluation
  and completion behavior without paid cloud calls. Continue the authorized batch
  after checks; no routine extra approval gate for original authored lessons.
- [x] Complete beginner 36, import, selectively export canonical WritingExercise
  packages and verify rerun/public counts. Update the matrix and ledger.

## Stage 05b — intermediate 36

- [x] Read the prior manifest/receipt and inspect current working tree. Reuse
  importer/validator and existing keys; do not recreate/reset the beginner batch.
- [x] Author six new lessons per format at intermediate complexity. Validate all
  bilingual pairs, approved alternatives, unique purpose/wording and paragraph
  structure, import/export, rerun and update actual counts/receipts.

Intermediate execution checklist (single agent, local only):

- [x] Preserve the original baseline and capture an additive pre-intermediate
  snapshot, including beginner rows and all307 existing package versions.
- [x] Author/review432 bilingual pairs and36 alternatives across the36 reserved
  identities; validate length, paragraphs, local assessment and cross-batch duplication.
- [x] Dry-run, six-format pilots in the isolated test host, transactional36-lesson
  dev import and zero-create rerun (local pilot command later confirmed6 no-ops).
- [x] Canonical export/re-export, privacy/hash review, Validate and Sync twice.
- [x] Fresh MariaDB10.5.23 roundtrip/repeat; focused tests and preserved-row proof.
- [x] Publish the intermediate receipt/editorial ledger and cumulative72/108 matrix.

## Stage 05c — advanced 36 and aggregate proof

- [x] Repeat for advanced only, preserving all previous rows/packages.
- [x] Prove each of 18 cells has exactly six new batch-owned public lessons,
  total 108. Net increase is measured against the saved baseline; no private rows
  are relabeled public to meet the quota and no old lesson counts as new.
- [x] Importing the same manifest again creates0 lessons/sentences/variants and
  leaves original IDs, progress, attempts, earned badges and credits unchanged.
- [x] Verify export→fresh disposable MariaDB import→repeat→content read/evaluation.
  Compare normalized content/answers and package digests, not local integer IDs.

## Definition of a complete lesson

Every lesson has a correct existing level/format, distinct scenario, natural
title/topic/preview, coherent full Vietnamese task text, faithful natural English
reference text split into ordered matching sentences, useful paragraph breaks,
and reviewed genuine alternative answers where needed. A title/placeholder,
Vietnamese-only text, empty answer array or repeated filler is not a lesson.

Editorial targets (adapt format-sensitive greetings/sign-offs; never pad text):

| Level | Meaningful sentences | English body words | Language |
| --- | --- | --- | --- |
| beginner |6–10|60–120|Common words, direct clauses, basic tenses |
| intermediate |10–14|120–190|Connected ideas, reasons/details, varied tenses |
| advanced |12–18|180–280|Nuance, register, argument/cohesion and complex clauses |

Vary communicative purposes within each format. Email is correspondence; diary
reflects a personal experience; essay develops a viewpoint; article informs an
audience; story has narrative progression; report organizes scenario findings.
Do not place an email in every category with only its tag changed. Label invented
statistics/scenarios as fictional rather than attributing them to real research.

Review **all 108** lessons and all sentence-pair alignments; automated length checks
alone do not certify language quality. Detect normalized exact/near duplicates
against old/new lessons and across batches. Keep proper punctuation, contractions,
Vietnamese Unicode, sentence segmentation and meanings consistent. Test reference
and approved alternative answers through the same server assessment policy; do
not loosen the global evaluator to compensate for poor content.

## Data delivery and acceptance

Use tct-data-seeder and docs/operations/shared-data-workflow.md. Inspect Status;
export selected allowlisted public roots with stable keys/package IDs. Preserve
published bytes and production.json. Badges/user goals, progress, user answers,
credentials and arbitrary uploads never enter these content packages. No SQL dump.

Run manifest/content regressions, relevant Writing tests, canonical Validate and
Sync twice, immutable-history checks and a fresh matching-provider round trip.
Write `05-content-matrix.md` and batch ledgers with before/new/after per cell,
source/provenance, editorial status, real checks and remaining blockers. Runtime
backups/DB IDs/receipts containing personal data stay ignored. Report in Vietnamese.

Execution evidence: `docs/feature-audits/2026-11-14-writing-goals/05-beginner-ledger.md`,
`05-intermediate-ledger.md`, `05-advanced-ledger.md` and `05-content-matrix.md`.
All three batches complete locally; cumulative108/108 new public lessons across
all18 cells. Advanced112 focused tests, canonical379-release validation, Sync twice,
fresh MariaDB10.5.23 roundtrip/repeat and all-level zero-create reruns pass.
Original/earlier rows,343 pre-advanced package versions and production.json remain
preserved. No remote Git/release work, UI acceptance or paid provider calls claimed.
