# Restore bounded Speaking shared-data adoption during staging release

Owner authority: complete PR/merge/CI/staging integration; investigate and fix the
observed deployment blocker. One agent. Production gate remains false.

Evidence: PR136/master build passes, migrations apply successfully, hosting keeps
importing public content after the publisher20-minute deadline. Private log grows
from3411 cues/20 videos to3762 cues/22 videos. Backup and previous-app are retained.
The engine scans the growing list of rows and bindings and canonicalizes every
candidate for every new cue. New rows already belong to this batch and cannot be
valid adoption candidates, but are repeatedly revisited.

- [x] Add a deterministic regression bounding candidate projections, and preserve
  ambiguity, claimed-row and reference/optional-field context behavior.
- [x] Snapshot only initial unbound shareable candidates; cache exact-match digest
  buckets per kind/reference/optional-field context. Never turn adoption into a
  title/ID match. Preserve transaction, conflict/rollback, owner/privacy rules,
  record IDs, immutable package hashes and existing release guards.
- [x] Run shared-data/transfer regressions and real matching MariaDB acceptance.
  No schema/package/model/config changes are required.
- [ ] Observe the current hosting process/transaction before recovery; do not
  delete locks, remove maintenance or start a concurrent import blindly. Use the
  reviewed release pipeline for any replacement artifact, with owned rollback
  and backup preserved if cancellation is needed.
- [ ] Push the focused fix, create/merge its PR after exact-head CI, then verify
  staging expected commit/run/digest, readiness and actual deployed static assets.
  Record real results in the PR136 release ledger or follow-up ledger and report
  in Vietnamese; pending deployment is not success.

## Verified local evidence and reviewed recovery

Deterministic baseline fails:100 original candidates were projected50000 times
for500 lookups. Indexed implementation projects100 times per context, preserving
ambiguity and claims. Shared-data/transfer suite:39 pass,3 gated skips. Separate
fresh disposable MariaDB10.5.23 acceptance:271 packages,90 Speaking roots,16262
cues,16536 operations in23.212s; repeat0.129s/zero operations. Private video and
existing IDs/progress survive. Windows PowerShell5.1 recovery/release tests:25 pass.

The tracked data/releases/staging-recovery.json pins failed run43538144629,
commit09d9c8e and previous successful commitd8c5773. Only a later tested master
publisher can include this identity. Healthy staging is a no-op. Offline recovery
requires exact owner/current/public markers, unchanged previous-app bytes,
preserved SQL backup and exactly the two reviewed additive migrations.
Only that run's stage/app executable with staging-sync arguments may be stopped.
The helper waits for the original parent's OS lock and failure receipt; it never
deletes locks/attempts/backups, restores SQL, changes Plesk settings or touches
production. Owned maintenance is preserved privately. The verified old app can
reopen on the compatible schema before a new normal backup/migration/import/
readiness release. Missing evidence keeps maintenance. No schema/package/config
changes; production gate remains false. Final remote results belong in the PR.
