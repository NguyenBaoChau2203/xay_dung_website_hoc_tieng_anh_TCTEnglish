# Execution prompt 07: Combined review, remaining fixes and acceptance

Selected model: GPT 6.1 Sol High. One agent implements and reviews inline.

## Outcome and references

Review the combined Writing/Goals/Profile work, fix remaining in-scope defects
and provide a trustworthy manual-test handoff. Read AGENTS.md,
`.ai/prompts/README.md`, the work-pack README and
`docs/superpowers/plans/2026-11-14-writing-goals/07-integrated-acceptance.md`.
Read actual prior ledgers/contracts as needed. Verify that stage 05 has all three
batches and 108 completed new lessons; do not treat draft slots as imported data.

## Mode and authority

IMPLEMENT_LOCAL includes review, reproduction, focused fixes and regression
verification. This is not review-only. Preserve current branch/work/user data.
No paid API calls, production/hosting writes, protected config changes without
authority or commit/push/PR/remote CI/merge/deploy. Do not infer those stages from
the previous Speaking release. Schema/data work follows canonical EF/package rules.

## Execute

1. Review the actual combined diff and its callers/contracts, migration metadata,
   immutable packages and UI data origins. Use CodeGraph first and relevant bug
   memory. Build a short scoped checklist only for real repair findings.
2. Run the 07 scenario matrix: login-only, accepted partial practice, final/replay/
   concurrent completion, UTC+7/missed day, target history, badge thresholds,
   mystery redaction, Profile, account-switch isolation and all 18 content cells.
3. Fix demonstrated defects and add meaningful failing-then-passing regressions.
   Re-run affected checks after fixes; do not broaden/repeat checks without cause.
4. Verify Release build, relevant/full .NET suite, established Node suites,
   clean EF model state and disposable MariaDB 10.5.23 upgrade/repeat/preservation/
   concurrency evidence for changes that require it.
5. Verify canonical package Validate/immutable history and repeat Sync with the
   current last-sync catalog hash. No personal data or old package rewrites.
6. Perform actual browser acceptance using available tools and controlled fee-free
   tests. Fake clocks/providers are test fixtures, not fabricated evidence of
   real elapsed days or live provider success. Mark blocked/skipped work honestly.

## Completion and owner handoff

Update final contracts/matrix/bug memory after fixes. Write
`docs/feature-audits/2026-11-14-writing-goals/07-integrated-acceptance.md` with
findings, changes, exact checks/results and remaining limits. Provide a numbered
manual test order with routes, expected results and safe test data. Report in
Vietnamese, distinguishing local, CI, staging and production. Stop before any
ungranted Git/release stage and do not describe partial acceptance as complete.
