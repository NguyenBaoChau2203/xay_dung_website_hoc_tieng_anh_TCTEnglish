# Local grouped commits for the Writing / Goals branch

## Authority and outcome

The owner accepted the Writing behavior and authorized local commits for the
existing work on `fix/writing-goals-profile-polish`. Use one agent. Preserve all
working files, the local database, credentials and the running web on port 5100.
Do not push, merge, create a PR, deploy or make paid provider requests.

## Commit boundaries

1. Goals, business dates, completion rewards, badge tiers and profile/dashboard
   consistency, including their migration, tests and reconciliation utility.
2. Writing practice fixes, private durable brief generation, limited bilingual
   template fallback, recent exercise history and browser recovery, including
   their migrations, UI and regressions. Keep shared contracts together.
3. The 108 original public Writing lessons, immutable shared packages, authoring
   sources, import/export utility and content regressions.
4. Feature prompts, plans, contracts, acceptance ledgers and bug memory. Preserve
   the documented unintended live-call incident and unverified browser limits.
5. Existing BTH4 report revision plans, separate from application changes.

Shared source files may be staged partially without changing their working-tree
contents. The intermediate Goals snapshot must match its committed migration;
the final Writing snapshot must remain byte-equivalent to the existing final
model after Git line-ending normalization. Never rewrite applied migrations.

## Checklist

- [x] Inspect the actual branch, status, commit history and existing evidence.
- [x] Save an ignored baseline of all pending file hashes and review boundaries.
- [x] Check candidate files for credentials, private runtime artifacts and immutable
      shared-content preservation; validate the shared package catalog.
- [x] Run focused offline regression checks with live provider gates disabled.
- [x] Stage and inspect each group, then create local conventional commits.
- [x] Verify final hashes, clean Git status, commit list and local readiness.

## Verification limits

This is a commit organization task, not a new implementation or paid acceptance
run. Reuse the recorded full .NET and MariaDB evidence for unchanged behavior;
report newly executed checks separately. No commit authorizes remote actions.

## Current verification receipts

Ignored evidence is under `.tmp/commit-review/`.

- Baseline: 265 pending files. All 265 retained their exact on-disk hashes before
  the deliberate update to this checklist. No private runtime artifact or
  high-confidence credential pattern was found in the candidate files.
- Focused .NET Release: 178 passed, 0 failed, 2 explicitly gated skips, total 180;
  `grouped-commits.trx`. The three OpenAI live gates were explicitly disabled;
  no acceptance database opt-in was set. Compilation succeeded with existing
  project warnings. This does not replace the earlier full/provider evidence.
- Writing / Goals JavaScript regressions: 72 passed, 0 failed, 0 skipped.
- Canonical package validation: 379 immutable releases, 17,904 record revisions.
  All 271 previous release entries were preserved, with 108 new public Writing
  packages: 1,152 sentence pairs, 108 alternatives, six lessons in every one of
  the 18 existing level/format cells. All 379 catalog package hashes matched
  their actual Git index bytes, including line-ending handling.
- Repository EF 9.0.4 reported no pending model changes. The check used the known
  local Docker connection through child environment only and applied no schema.
- `/health/ready` returned HTTP 200 / `ready`; the existing web on port 5100 was
  kept running. No paid request, database reset or secret-store write was made
  during this commit task.
- Each local commit used the configured pre-commit hook without bypasses. The
  implementation/content/report commits are `5752a79`, `0c0f5a4`, `50b7c8f` and
  `e3cc3d5`. Feature documentation is committed last. Final Git status and file
  preservation are checked after that documentation commit.
- The five-group sequence was created and a clean working tree was observed.
  Final documentation amendment only completes this checklist and normalizes
  trailing whitespace in four existing feature ledgers. Markdown hard breaks
  are retained using explicit backslashes; the extra EOF blank line is removed.
  Original document bytes are retained in ignored `original-docs/` evidence.
  All other baseline files retain their exact original on-disk hashes. No
  application, migration, package or authoring-source content was rewritten.
