# Hybrid RAG integration into master — 2026-11-08

## Authority and outcome

The owner explicitly requests grouped commits on the current branch, a PR,
merge into master, switching this workspace to master, and synchronizing the
latest master locally. This grants the required Git push and normal CI checks.
Execute inline as one agent. Preserve existing data, secrets, workloads and all
release guards. Do not enable production deployment or make paid API calls.

Input branch: feature/hybrid-rag-chatbot, HEAD6af768bd32d16cc6d012d484ee82df4cab805c69.
The reviewed local172-entry identity is
76a51a99633d0ea1574de7d0e63b7f7564e3a6b479df6c36a61ff16478a36806.
Phase7 live acceptance remains BLOCKED; merging completed local work does not
claim that its paid benchmark passed.

## Ordered work

- [x] Inspect actual Git status/remotes, verify local evidence and inspect schema
      changes, current release gates, existing PRs and new master commits.
- [x] Inventory all pending files, exclude runtime/private content, review the
      additive migrations with existing MariaDB evidence and group commits.
- [x] Commit core RAG/lifecycle plus C# regressions, chat/admin interface plus
      browser tests, login integrity repair, and audit/planning documentation.
- [ ] Merge the current origin/master into the feature branch without rewriting
      history; resolve any conflicts, run meaningful integration checks and review
      the final diff. Preserve historical audit identities instead of rewriting
      old handoffs to claim approval.
- [ ] Push the feature branch, create the PR with schema/data/test/live limits,
      wait for the actual required CI checks and address any failures.
- [ ] Merge the verified PR head into master with a merge commit, retaining grouped
      commits; inspect the resulting workflow and keep production deployment off.
- [ ] Switch this checkout to master, run scripts/pull-local.ps1 and confirm exit0
      plus current ignored shared-data/last-sync.json. Restart only the owned
      local web if needed to serve the merged source and check readiness/speech
      metadata without paid requests.
- [ ] Report grouped commit SHAs, PR/merge, final branch/HEAD, local sync and actual
      CI/staging/production outcomes in Vietnamese.

## Deployment boundaries

Repository production gate was inspected as false. Staging gate is already true;
the documented owner-enabled master-to-staging pipeline remains unchanged.
Follow that existing pipeline and its environment protections; no ad hoc hosting
configuration, private data copy or production release is authorized here.
Inspect the actual run before reporting any staging result. If it fails, inspect
the failed job and hosting evidence through available authorized read paths before
any retry; do not force recovery, delete deployment state or bypass protections.

## Commit preparation evidence

The pending265 files are grouped as124 core/test files,8 chat/admin UI files,
1 auth repair and132 documentation/audit files (including this plan). The local
172-entry fingerprint still matches before committing. A candidate-secret scan
of every pending file found no API-token/private-key pattern. Runtime credentials,
uploads, provider logs and local QA accounts stay ignored and are not staged.
Core commit07855f8, UI1873e29 and auth9d24e23 precede the documentation commit.

The two schema additions are nullable columns on AiQuotaReservationLedgers and
AiMessages. Up paths contain no destructive operation. Down removes only the new
columns and is not executed. Both migration Designers and the final model snapshot
ship with dependent code. Prior disposable MariaDB10.5.23 upgrade/repeat checks
preserved user900001 and the legacy message with NULL grounding metadata; the
latest local dry-run reports no pending model change. Fresh integration checks
will run after bringing origin/master into the branch. Historical phase reports
keep their original branch/HEAD/permission statements as evidence of those runs;
this owner request grants the later Git integration stage only.
