# Execution Prompt: Finish PR 144 review and staging acceptance

## Outcome

Verify the reviewed application and 12 Reading releases from PR 144 through staging once the owner reopens release work.

## Context and authority

Open the current PR and the integration plan at
`docs/superpowers/plans/2026-11-23-pr-144-integration.md`. Inspect the exact head,
base, previous run and staging marker. Follow AGENTS.md and the shared-data and
staging runbooks. Carry forward authority explicitly granted by the owner.
The owner deferred the staging release with the current Plesk limit. Data deletion, Plesk settings and production require separate explicit authority. Keep the production gate false.

## Work and acceptance

1. Preserve existing Class routes and the Reading learner-history regression.
2. Rehearse the 12 published Reading packages on MariaDB 10.5.23 and verify a rerun.
3. Run PR CI and capture desktop, tablet and mobile browser evidence. Inspect
   staging's private deployment state and confirm disk capacity before retrying.
4. If authorized and ready, merge the exact reviewed head into master. Follow
   the master run until staging reports the expected commit, run, digest and
   readiness. Report results and any blocker in Vietnamese.
