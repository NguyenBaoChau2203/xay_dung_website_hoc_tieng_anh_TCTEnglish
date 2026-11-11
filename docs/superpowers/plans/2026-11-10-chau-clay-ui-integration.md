# Chau Clay UI integration and owner-authorized master merge

Date: 2026-11-10 (Asia/Bangkok).

Owner authority: consolidate the recent UI batches on the existing umbrella
branch, commit the accepted Admin/loader work, push, create a PR, resolve conflicts,
follow CI, merge to master and return this workspace to synchronized master.
Production deployment remains unauthorized/off. One agent performs every step.

## Verified starting state

- Umbrella: `feature/chau-ui-design-system` at `33e1416`.
- Active topic: `feature/chau-admin-loader-clay-ui` at `e9a9d45`, with accepted
  Admin/dashboard/user/loader changes still uncommitted.
- Other UI topic heads form one descendant chain; preserve their commit history.
- Fetched `origin/master`: `eed4e2c95c79ef708dc9af77c8b9326186835122`.
- GitHub repository variables: `PLESK_DEPLOYMENT_ENABLED=false`,
  `PLESK_STAGING_DEPLOYMENT_ENABLED=true`. Preserve both. Staging follows its
  existing independent authorization; production is not implied by this merge.
- Preserve the three unrelated untracked BTH3 prompt/report/review documents.

## Ordered checklist

- [x] Inspect local branches, accepted UI diff, remote refs and deployment gates.
- [x] Review remaining Admin/loader changes and the full publication boundary;
  run build/full application tests, Node checks and required tooling checks.
- [x] Commit only the accepted UI work and integration evidence; no credentials,
  runtime uploads, backups, database rows or unrelated BTH3 artifacts.
- [ ] Fast-forward the umbrella branch to the completed topic; confirm every
  UI topic head is included. Integrate newer origin/master if necessary, resolving
  conflicts by preserving both owners' intended behavior and rerunning checks.
- [ ] Push the umbrella branch, create the reviewable PR to master and inspect
  the actual GitHub CI results for its head before merging.
- [ ] Merge the verified head, follow the master workflow and verify that the
  production release job remains skipped. Report staging separately.
- [ ] Switch the workspace to master; use `scripts/pull-local.ps1` to pull with
  `--ff-only` and apply reviewed shared packages to this checkout's Docker.
  Confirm matching local/remote SHA and successful local data synchronization.

## Evidence and limits

Record actual local/remote results and blockers below as the work completes.
Earlier UI ledgers contain prior implementation/browser observations; do not
claim they were independently repeated during this integration unless they were.
No schema/content mutation is intended. Assess any newly discovered persisted
model/content change through the existing migration/shared-data rules before
publication. Do not enable paid/live acceptance flags or change production gates.

### Local publication checkpoint

- Admin/loader runtime, tests and ledger committed at `0bdaec1`.
- Complete Release application test/build: 1,188 passed, 22 skipped, 0 failed
  (1,210 total). Optional provider/MariaDB cases were not enabled locally.
- Node suite: 141 passed. Python script suite: 138 passed, 1 skipped. Tooling
  validator and shared-data immutable-history check against origin/master passed.
- The 117-path publication boundary passed private-path, credential-pattern,
  UTF-8 and SVG active-content checks. No persisted entity, migration, shared
  package or protected application configuration changed.
- All earlier UI topic heads were verified ancestors of the current topic.
  Origin/master has no divergent commit at this checkpoint.
- Corrected two literal control characters in the Speaking adoption row and
  updated the shared UI adoption guide/availability wording for the integration.
- Local logs/TRX remain ignored in `.appdata/local/test-results/`.
- This is the committed pre-publication checklist. Subsequent umbrella/PR/CI/
  merge and master/data-sync evidence is recorded in the integration PR and the
  final owner handoff, avoiding a separate post-merge source change just for logs.
