# Execution Prompt: Finish PR 144 review and staging acceptance

## Outcome

Preserve the Reading import repair and resolve the shared catalog and tooling findings in the PR
144 integration plan, then verify the reviewed revision through staging.

## Context and authority

Open the current PR and the integration plan at
`docs/superpowers/plans/2026-11-23-pr-144-integration.md`. Inspect the exact head,
base, previous run and staging marker. Follow AGENTS.md and the shared-data and
staging runbooks. Carry forward authority explicitly granted by the owner.
Obtain a decision for catalog selection or tooling activation if
unresolved. Data deletion, Plesk settings and production require separate
explicit authority. Keep the production gate false.

## Work and acceptance

1. Preserve existing Class routes and the Reading learner-history regression.
2. Retain reimport and concurrent-confirmation coverage on MariaDB 10.5.23. Apply the approved
   shared-data selection, if any, through reviewed packages and verify a rerun.
3. Resolve Spec Kit provenance and activation per the owner's decision.
4. Run PR CI and capture desktop, tablet and mobile browser evidence. Inspect
   staging's private deployment state and confirm disk capacity before retrying.
5. If authorized and ready, merge the exact reviewed head into master. Follow
   the master run until staging reports the expected commit, run, digest and
   readiness. Report results and any blocker in Vietnamese.
