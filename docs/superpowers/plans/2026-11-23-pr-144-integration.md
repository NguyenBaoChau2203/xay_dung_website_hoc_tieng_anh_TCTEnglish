# PR 144 integration and staging verification

- [x] Confirm the open PR, its exact head, the default branch, and the staging and production gates.
- [x] Test the Git merge with current master in an isolated checkout.
- [ ] Restore Class navigation and interactions backed by the current Class controller. Remove the incomplete Browse view.
- [ ] Protect existing Reading attempts and answers when an Admin replaces questions. Run a regression against the original PR behavior and the repair.
- [ ] Review Reading import, Account, Onboarding, shared layout, and the imported Spec Kit files. Record actionable findings and verify application and tooling checks.
- [ ] Verify browser behavior at the project breakpoints and a fresh PR CI run for the reviewed commit.
- [ ] Confirm staging can accept a release before integrating. Track master CI and verify the staging marker, run, digest, and readiness if integration proceeds.

## Observations

- Git merges PR head `473d3ef8` and master `95d2576b` cleanly.
- PR CI `45157878048` fails to compile Razor because the BrowseClasses view uses a missing model. Its navigation and join-by-code routes also lack backend implementations.
- PR question replacement deletes learner answers before replacing the questions. Preserve learner history and make the Admin operation fail before it writes.
- Staging accepted the webhook for run `45182318680` and continued serving `93959ab4`. The latest documented Plesk quota measurement left roughly 228 MB after cleanup. Confirm the current host state before any retry.

## Review findings requiring a decision before merge

| Priority | Finding | Repair and acceptance |
| --- | --- | --- |
| P1 | Reimporting the same workbook in a fresh preview allocates new official codes, so confirmation's final-code check cannot detect the previous import. The PR ledger documents local duplicate Published readings and manual archival. | Decide import identity and intended reimport behavior. Compare stable content identity without overwriting existing lessons. Cover repeated preview/confirm, legacy codes, partial failures and parallel confirmations on MariaDB 10.5.23. |
| P1 | The Reading ledger documents Published local content for 12 titles, while the PR adds no package under data/shared. | Confirm the intended staging catalog. If the lessons are intended for staging, export reviewed allowlisted readings and referenced media using the shared-data workflow. Verify immutable versions and a second import; retain learner history and separate production selection. |
| P2 | The always-applied Cursor rule and Spec Kit 1.4.3 add an active workflow without a corresponding upstream commit and installed file hashes in the team lock file. | Obtain the owner's decision on activation. If approved, review the source, license and host compatibility and pin the installed files. Otherwise move the tooling changes to their own review. |
| Release | master run 45182318680 passed application/MariaDB checks, but authenticated staging retained commit 93959ab4 after the webhook was accepted. The deployment artifact is 207,437,656 bytes. | Verify current private Plesk deployment state and sufficient disk headroom before a retry. Preserve backups, logs and data. A successful release must match commit, run and SHA-256 as well as readiness. |

## Local verification

- [x] Reproduce the learner-answer regression on the original implementation.
- [x] Release build and 79 focused .NET tests, including both Reading edit entry points.
- [x] Node regressions (595 passed).
- [x] Test the three-way merge without conflicts. Preserve the original dirty checkout.
