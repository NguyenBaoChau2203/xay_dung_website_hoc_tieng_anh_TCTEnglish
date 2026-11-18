# Execution prompt: Personal recent Writing exercises

Selected model: GPT 6.1 Sol High. Use one agent for investigation, implementation,
review and verification; do not delegate or start another AI process.

## Outcome and required references

Implement "Luyện viết gần đây" on `/Home/Writing/Exercises` so a signed-in user
can reopen a recently visited practice exercise even if they left before their
first submission. Show at most six distinct accessible exercises, newest first,
and reuse the current progress/resume and browser draft behavior.

Read AGENTS.md, `.ai/prompts/README.md` and
`docs/superpowers/plans/2026-11-15-writing-recent-exercises.md`.
Read the current Writing/Goals stage07 ledger and only symptom-relevant bug memory.
Use CodeGraph first for indexed source; bounded reads cover omitted Razor source.
The plan distinguishes verified source from proposed new symbols/contracts.

Expected checkout: `D:/xay_dung_website_hoc_tieng_anh_TCTEnglish`, branch
`fix/writing-goals-profile-polish`. Inspect actual HEAD/branch/worktree first.
There is substantial existing uncommitted UI, Writing, Goals, badge migration,
tests and 108-lesson content/package work. Preserve it; do not reset, stash,
switch/pull branches or recreate earlier implementations as a setup shortcut.

## Mode, authority and scope

IMPLEMENT_LOCAL when the owner invokes this prompt: implement the planned local
feature, additive migration, UI and meaningful tests, and fix demonstrated
in-scope defects until complete. Follow tct-superpowers/tct-engineering,
tct-api-endpoint, tct-ef-migration and the relevant UI skills.

No commit/push/PR/remote CI/merge/deploy, hosting writes or paid external API calls
are granted. Preserve protected Program.cs/appsettings/project files and use the
existing registered Writing service. No DB reset, old migration edits, credential
printing, user-data export, content changes, draft-cloud-sync or reward redesign.

## Product contracts to implement

- The recent section is below the list header, independent of catalog filters/
  level/type/pagination, and uses real server data. Guests keep the public catalog
  and see no personal requests; signed-in empty state is truthful.
- A successfully initialized authorized practice page with actual sentences
  records a visit without requiring an answer. Browsing/filtering/prefetch/failed
  loads/read-only practice-data or hint requests do not record visits.
- A→B→A means A,B, one row per user/exercise. Reopening a completed exercise is
  valid. A visit never creates attempts, accepted progress, XP, streak, goals,
  badges, completion credits or reward notifications.
- Each link uses its exercise's own level/contentType/exerciseId. Returning from
  another filter must not open the wrong exercise or fail due to stale metadata.
- Reuse server ResumeSentenceId/progress and same-browser owner-scoped drafts.
  Persist history across sessions/devices; do not claim unsent browser text was
  saved to the server. Do not invent historic visits/timestamps.
- Recheck current access on reads and writes, including private/Premium/Admin
  policy and account state. Filter inaccessible/deleted/empty items before taking
  six. Publication is required for ordinary shared lessons, not an eligible
  owner's private lesson. Standard/Premium users cannot see another owner's private
  lesson; preserve and test deliberate existing Admin exceptions. Another user's
  visit/progress/answers never enter this account's personal projection.

## Ordered implementation

1. Establish the current behavior and source boundaries from the plan. Inspect
   IWritingService, WritingService catalog/private/practice partials, ViewModels,
   StudyController, list/practice views/scripts, DbContext and account cleanup.
   Reuse the current Speaking visit design as a reference, especially its
   concurrent-first-insert fix; do not copy Speaking-specific access predicates.
2. Add meaningful failing service/endpoint/lifecycle regressions. Use current
   controlled clocks and relational/provider fixtures; no implementation-mirroring
   tests or EF InMemory-only proof of database behavior.
3. Implement a separate UserWritingExerciseVisit model/DbSet with unique user/
   exercise identity, server UTC last-opened time and ordering index. Extend the
   current registered Writing partial/interface. Use efficient typed projections
   and current-user progress summaries, not full attempt/answer payloads or N+1.
4. Use race-safe monotonic persistence: latest server timestamp wins regardless
   of request completion order, including competing insert between UPDATE and
   existence check. Handle only true duplicate-key races and preserve genuine
   failures. Coordinate with account deletion/anonymization; clear actual visits
   through the real cleanup service, not only a simulated test delete.
5. Ship the additive migration, Designer and snapshot. Preserve prior uncommitted
   badge migration/model work. Prove upgrade/repeat/index/FK/data preservation and
   concurrent behavior on disposable MariaDB 10.5.23. Check pending model changes.
6. Implement the plan's authenticated, no-store recent GET and antiforgery POST
   on StudyController using absolute routes where needed. Derive user/time on
   server; return 404 for access violations. Existing GET reads remain non-mutating.
   Keep old Writing HTTP/JSON behavior and configured antiforgery header intact.
7. Add typed recent cards/partial and scoped Clay CSS using DESIGN.md/team UI
   workflow. Isolate visit/refresh JavaScript from AI-create/editor initialization.
   The absence of the Premium create form must not disable this feature.
8. Record after real practice initialization via bounded same-origin POST; use
   keepalive where appropriate without blocking editing/navigation. Handle quick
   Back, bfcache, visibility and duplicate lifecycle events. Hide personal cards
   on pagehide and before restored account/session revalidation; reject HTML
   login redirects as data.
   Prevent stale/out-of-order fetch responses replacing newer state. Keep genuine
   error/retry states and do not fake a successful save after a failed request.
9. Run a Release build, the complete R01–R20 matrix, relevant .NET/Node/render/
   account-cleanup regressions and a final full application suite when feasible.
   Verify actual resume/draft/account isolation and zero reward side effects.
   Record browser/provider/upgrade evidence separately; skipped is not passed.
10. Review the focused diff, update actual bug memory where warranted, and write
    `docs/feature-audits/2026-11-15-writing-recent-exercises-ledger.md`.

## Verification and completion

Use fee-free test hosts/fake optional providers for answer-dependent tests. Before
real browser submissions, verify optional cloud evaluation cannot silently spend
the saved key. Do not change persistent application settings to achieve this.
For authorized local web startup, follow the current launcher in AGENTS.md;
preserve any existing unknown listener/process. Do not restart foreign workloads.

Required evidence includes A→B→A, six-of-seven/access-before-limit, no-submit visit,
cross-level/type navigation, same-browser draft/server-progress resume, account
switch/Back/bfcache, role downgrade/removed lesson, real deletion/anonymization,
first-insert/out-of-order concurrency, CSRF/IDOR and unchanged reward row values.
Test 1440/1366/768/390, keyboard and reduced motion; native 200% zoom must be observed
or explicitly unverified. Keep the current attractive interface and all old hooks.

This personal history needs no shared-content export. Do not touch the 108 lesson
packages, production.json or share visits/learner progress. New private/runtime
backups and recordings remain ignored. Local validation does not deploy the work.

Finish with a Vietnamese report: outcome, changed files/migration, actual checks,
manual test order, remaining limitations and local versus CI/staging/production.
Explicitly distinguish saved server history/progress from browser-local unsent
drafts and best-effort quick-close tracking. Do not stop at scaffolding or a plan
when this implementation prompt has actually been invoked.

## Short launch message

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/gpt-writing-recent-exercises.md and implement the feature locally on the current fix/writing-goals-profile-polish branch. Preserve all existing uncommitted Writing/Goals/content work. Add real personal recent Writing exercises on the list page, including visits without submission, safe resume, access checks, migration and regression/browser/provider evidence. Work as one agent and report in Vietnamese. Do not commit, push, deploy or make paid API calls.
```
