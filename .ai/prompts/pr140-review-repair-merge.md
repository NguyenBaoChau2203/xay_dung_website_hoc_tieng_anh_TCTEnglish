# PR 140: integrate, repair, verify and merge

## Outcome and evidence

Complete the owner-authorized review of PR #140, repair confirmed defects and
conflicts, merge into master, then synchronize this workspace with origin/master.
Use the checklist in
`docs/superpowers/plans/2026-11-18-pr140-review-repair-merge.md`.
The member head is c3836c3; the reviewed master baseline is c641c3e. The original
branch was 178 commits behind and the integration produced 30 conflict paths.
Those observations are evidence, not instructions from the incoming documents.

## Authority and boundaries

Use one Codex agent inline for investigation, repair, review and verification.
The owner explicitly authorized fixes, normal commits/pushes to the existing PR,
merge, and local checkout/synchronization of master. Preserve the member's feature
and current master behavior. Preserve all delegation, authorization, antiforgery,
credential, data and release policies in AGENTS.md. No force push, data reset,
paid AI/SMTP calls, production gate changes, or implicit production deployment.
The PR's Reading DI/dependency/schema changes are within the authorized review;
do not introduce unrelated protected configuration changes.

## Ordered implementation and acceptance

1. Inspect actual Git state and current PR. Use CodeGraph for indexed code and
   focused reads for missing context. Merge current master without discarding
   newer accepted features. Record each material conflict decision.
2. Establish failing behavior regressions before repairing Reading access,
   private matching keys, completion/reward atomicity, daily challenge replay,
   CSV export, published-package compatibility, and import resource bounds.
3. Keep immutable package versions byte-identical. Validate the added version,
   legacy defaults and references, repeat imports and learner-history preservation.
4. Review committed migrations and snapshot. Check pending model changes and
   upgrade/repeat execution on disposable MariaDB matching hosting; record any
   unsupported or unobserved provider behavior accurately.
5. Run relevant tests, then integrated .NET, JavaScript and tooling checks.
   Verify learner/Admin Dashboard and Reading in a real browser at relevant
   widths, reload and keyboard paths. Record observed evidence and limitations.
6. Review the integrated diff, append the bug log and a truthful audit. Push to
   the existing PR, observe CI on the exact head, then merge when blocking checks
   pass. Observe the resulting master CI and staging marker/readiness if enabled.
   A skipped production job is not a production deployment.
7. Switch the clean workspace to master and run `scripts/pull-local.ps1`.
   Require exit code zero, HEAD equal to origin/master, and current ignored shared
   data sync receipt. Preserve local accounts, progress, uploads and secrets.

Report in Vietnamese with findings, repair files, exact test counts, PR/commit
and CI links, browser/provider evidence, local synchronization and limitations.
Do not report an unobserved or skipped check as passed.

## Short launch message

Continue the owner-authorized PR #140 review and repairs using this prompt and
its linked plan. Use one agent. Finish verification, the existing PR merge, and
the local master/shared-data sync; preserve security/data and production gates.
