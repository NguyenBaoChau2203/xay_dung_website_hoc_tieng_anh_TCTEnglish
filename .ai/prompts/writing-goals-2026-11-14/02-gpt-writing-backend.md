# Execution prompt 02: Audit and repair Writing backend behavior

Selected model: GPT 6.1 Sol High. One agent; no delegation/model substitution.

## Outcome and references

Make Writing access, server evaluation, saved attempts/progress, drafts and
first-completion behavior reliable. Read AGENTS.md, `.ai/prompts/README.md`,
the work-pack README, stage 01 handoff and
`docs/superpowers/plans/2026-11-14-writing-goals/02-writing-backend.md`.
Inspect actual branch/HEAD/working tree, expected
`fix/writing-goals-profile-polish`. Preserve Gemini's earlier UI work.

## Mode and authority

IMPLEMENT_LOCAL: audit, reproduce, fix and verify stage 02 only. Keep existing
routes/JSON/access/antiforgery contracts. No remote Git/release stages, paid
provider calls, DB reset, personal-data export or protected config edits without
actual authority. Persistent changes trigger tct-ef-migration automatically.

## Execute

1. Use tct-superpowers/tct-engineering and CodeGraph for the current Writing
   call path. Read relevant bug memory; do not assume old fixes still apply.
2. Follow the 02 plan for roles/ownership, approved answers, normalization,
   assessment uncertainty, answer secrecy, history/resume and duplicate/racy
   submissions. Reproduce meaningful defects with failing regressions.
3. Implement focused corrections. Keep Local assessment authoritative; optional
   feedback cannot turn uncertainty into an invented pass/XP. Preserve Accepted,
   NeedsRevision and NeedsReview through persistence and reload.
4. Prove final-exercise progress/credit/outbox atomicity and once-only behavior,
   including credit insert failure and concurrent final submits. Do not add a
   second browser/controller reward path.
5. Identify the actual accepted-sentence persistence boundary and how stage 03
   can consume it to count a confirmed practice day. Stage 02 must not invent a
   competing streak policy; record this integration point precisely.
6. Run relevant Writing/transaction tests and meaningful matching-provider checks
   when relational behavior changes. Include required migration metadata and
   MariaDB 10.5.23 upgrade/repeat/preservation proof for persistent changes.

## Deliverables and handoff

Code fixes/regressions and `docs/feature-audits/2026-11-14-writing-goals/02-writing-backend.md`
with confirmed/disproved findings, real checks, current contracts, event boundary,
schema/data impact and unresolved issues. Update actual fixed bug memory. Report
in Vietnamese; no unverified complete claims or automatic next-stage execution.
