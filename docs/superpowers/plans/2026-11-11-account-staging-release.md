# Account polish release and master handoff — 2026-11-11

Owner authority: group commits, push the topic branch, create a PR, resolve
conflicts, merge to master, follow CI/staging and return this workspace to a clean
master synchronized with origin. One agent. Production release remains ungranted.

Initial checkout: fix/staging-uat-polish at c908f66, with reviewed Account, learning
habits, Push/email, email Clay templates, shared UI/loader and dependency repairs.
Existing BTH3 planning documents are a separate documentation commit. No runtime
uploads, local evidence, keys, DB backups, private hosting state or personal data
package is part of source delivery.

## Ordered release work

1. Preserve the current diff and assemble logical commits for dependency security,
   Account/learning/email behavior, shared shell UI and documentation. Keep schema,
   Designer and snapshot together with dependent code.
2. Integrate current origin/master on the topic branch, preserving both parties'
   feature/model registrations. Resolve conflicts from source, reconcile EF
   migrations/snapshot without editing already-applied migrations, and verify
   the combined model against disposable MariaDB10.5.23.
3. Run meaningful final checks on the integrated tree, including CI's migration
   preservation/shared-data contract where affected. Fix actual failures within
   the authorized release scope; preserve owner data and protected release guards.
4. Push the topic branch and create a reviewer-readable PR with scope, schema,
   validation, known limits and no secrets. Wait for the PR's actual CI result;
   resolve any merge conflict or required check before merging.
5. Merge to master, inspect its exact GitHub run and staging job. Initial repo
   variables: PLESK_DEPLOYMENT_ENABLED=false and
   PLESK_STAGING_DEPLOYMENT_ENABLED=true. Keep production false. Do not modify
   protected host configuration or manually bypass the deployment pipeline.
6. Require successful staging job, expected commit/run marker and /health/ready.
   If CI/hosting fails, inspect actual state before retrying; a timeout does not
   prove hosting stopped. Keep maintenance/locks/backups under the normal runbook.
7. Switch this checkout to master after all source work is preserved, then run
   scripts/pull-local.ps1. Require ff-only code sync, successful shared-data sync
   and current ignored last-sync metadata. Report clean status, origin equality,
   PR/commit/run links and actual staging/production outcomes in Vietnamese.

The prior reviewed source passed1,276 .NET tests with29 environment skips,
84 focused checks and11 separately executed native DB cases. Those are historical
pre-integration results; current remote master contains newer personal-set/practice
and Clay changes and must be verified together. Test-only SQLite native advisory,
live Push setup and native200% browser zoom limits remain recorded in their ledgers.

Final release evidence belongs in the PR/CI timeline and the owner handoff; do not
claim success from this plan or start a new automatic release merely to update it.
