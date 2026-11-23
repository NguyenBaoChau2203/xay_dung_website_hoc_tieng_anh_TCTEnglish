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
| Release | master run 45182318680 passed application/MariaDB checks, but authenticated staging retained commit 93959ab4 after the webhook was accepted. The deployment artifact is 207,437,656 bytes. | Verify current private Plesk deployment state and sufficient disk headroom before a retry. Preserve backups, logs and data. A successful release must match commit, run and SHA-256 as well as readiness. |

## Local verification

- [x] Reproduce the learner-answer regression on the original implementation.
- [x] Release build and 79 focused .NET tests, including both Reading edit entry points.
- [x] Node regressions (595 passed).
- [x] Test the three-way merge without conflicts. Preserve the original dirty checkout.

## Reading workbook reimport

- [x] A second preview and confirmation of the original workbook returns the existing passage code and ID.
- [x] A change to a question explanation creates a distinct draft with the next available code, even from a stale preview.
- [x] Two simultaneous Admin confirmations on disposable MariaDB 10.5.23 create one draft and return the same code. The rerun also matched a row created before the repair.
- [x] Compare existing passage text, metadata, questions, options and vocabulary. A cover-only update belongs in the Admin editor. The schema and existing rows are preserved.

## Shared Reading release and review scope

- [x] Keep the PR focused on the application and its shared Reading catalog. Remove the newly added Spec Kit activation, scaffolding and root spec from the PR diff. Its source remains in the original PR commits.
- [x] Build the six workbooks from ReadingLevelPacks, pair the 12 curated covers by title and publish only the selected readings in a disposable MariaDB 10.5.23 authoring database.
- [x] Review the typed export of the 12 Published readings, their passages, questions, options, vocabulary and cover hashes. Keep the official Reading codes recorded in the author ledger and use stable catalog keys independent of local IDs.
- [ ] Apply the catalog to a separate MariaDB database twice. Compare reading IDs, Published status and covers across both runs. Validate existing immutable packages and keep production approvals gated.
- [ ] Verify build and MariaDB rehearsal for the final PR revision.

Plesk Account displayed 1,820.3 MB used of 2 GB and disallowed overuse.
Authenticated readiness returned 200 for the old marker. The owner chose to
finish PR and CI while retaining the current hosting quota. Merge and staging
release verification are deferred.

The typed export adds 12 immutable releases, 372 content records and 12 verified JPEG cover hashes. The 456 existing releases and production selection were preserved.
