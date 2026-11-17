# Grouped publication and staging verification

Owner authority: commit completed work by coherent groups, rename the existing
branch, push/create PR, merge to master and follow CI through staging. One agent.
Production is not authorized: preserve PLESK_DEPLOYMENT_ENABLED=false and existing
host/data/security/delegation boundaries. No paid AI/SMTP acceptance calls.

Initial checkout: fix/writing-goals-profile-polish, HEAD9bc9fc8, no upstream.
After fetch, origin/master308e307 has80 unique commits; this branch has8 unique
Writing/Goals/content/Contact commits. Preserve both histories. New work covers
Antigravity policy/hook repairs, chatbot presentation, reviewed demo guidance,
isolated SMTP tests and evidence. Generated email previews show no canonical
content diff; do not invent a content commit for line-ending/stat churn.

- [x] Review current diff/authority and rerun required tooling checks.
- [x] Rename to feature/learning-demo-and-agent-polish; commit agent policy/hooks,
  test isolation, chat presentation, guidance/data and evidence as coherent groups.
- [x] Merge current origin/master without rewriting/dropping either side; resolve
  conflicts from actual feature contracts and verify the integrated diff.
- [ ] Run full relevant application/Node and package/tooling checks. No new schema
  changes are expected from chatbot work; inspect inherited branch schema/content.
- [ ] Push the renamed branch, create an accurate PR against master, inspect
  required checks and merge with a merge commit to retain grouped history.
- [ ] Follow the exact master SHA/run through build, MariaDB/package checks,
  artifact publication and staging. Inspect production skipped, staging marker,
  expected commit/run/digest and readiness; report skips/blockers honestly.
- [ ] Record final commit/PR/run evidence and report in Vietnamese. Preserve local
  workloads, original secret stores and unrelated work; do not reset any database.

Grouped commits:363a345(agent policy/hooks), c444297(SMTP test isolation),
7cf9261(chat presentation), eb1bf1a(guidance/data),17c03f1(evidence).
Merge06e9f9c preserves master308e307 and all8 pre-existing branch commits.
Three documentation conflicts were integrated; no feature implementation conflict
was discarded. The obsolete unresolved Sets-card header was removed consistently
with master's actual fix; Contact and all other feature histories remain.

Tooling:153 Python tests (150 pass/3 platform skips), setup Node11/11,
working-tree/index validator and configured pwsh/cmd.exe hook checks pass.
Shared-data immutable Git guard against origin/master passes; production manifest,
workflow, Program.cs/appsettings/project have no branch diff. EF reports no pending
model changes. The three inherited additive migrations include Designer/snapshot;
upgrade/repeat/preservation runs remain mandatory in PR/master CI before staging.
No migration was generated or applied manually to hosting in this publication.

Full Node initially494/498; focused fixture/line-ending corrections29/29.
Full integrated gates are still running at this checkpoint. A remote result is
recorded by the PR/master Actions run and final handoff, not invented in this plan
before it occurs. Do not create another deployment just to update a status ledger.
