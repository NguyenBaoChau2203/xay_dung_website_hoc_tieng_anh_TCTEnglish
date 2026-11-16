# Personal recent Writing exercises

Date: 2026-11-15. Target implementer: GPT 6.1 Sol High.
Page: `/Home/Writing/Exercises`.
Current branch: `fix/writing-goals-profile-polish`.
Base HEAD observed during planning: `c6367a00269bc23749a9a02a7d26bcc89001fac8`.

## Outcome, authority and current state

Add a compact **"Luyện viết gần đây"** section so a signed-in learner can return
to an exercise after leaving its practice page, including an exercise opened but
never submitted. Display up to six distinct exercises, newest first, with an
accurate link to resume the existing practice flow.

This request authorizes planning documents now. Invoking the companion execution
prompt requests local implementation, migration, UI and meaningful verification.
It does not grant commit/push/PR/remote CI/merge/deployment or paid-provider use.
Do not pull, switch branches, stash/reset or clean the checkout as a startup step.
The current substantial uncommitted Writing/Goals/badge/content work is the
feature baseline and must be preserved. One agent performs all work inline.

Read AGENTS.md and the active skill/source step. Use tct-superpowers,
tct-engineering, tct-api-endpoint, tct-ef-migration and relevant UI skills.
Reuse existing registered Writing services; Program.cs, appsettings and project
files remain protected. No change to assessment/reward rules or lesson packages.

## Verified source observations

Application paths below are relative to `TCTEnglish/`. Proposed symbols/files in
later sections do not exist yet and must not be represented as implemented.

| Current evidence | Consequence |
| --- | --- |
| StudyController.WritingExercises is anonymous-capable and receives optional current-user identity; the list ViewModel has public/private lists and paging, but no recent section | Preserve guest catalog and existing filters/counts; recent history is authenticated-only |
| WritingService.Catalog.cs projects LastAttemptedAtDisplay from UserWritingAttempts.CreatedAtUtc | Attempt order cannot represent an exercise merely opened and abandoned |
| UserWritingExerciseProgress stores LastAttemptAt, not a separate opened-at field | Do not create progress/attempt rows to simulate a visit |
| WritingPracticeViewModel already contains ExerciseId, current owner scope, level/content type and ResumeSentenceId | Reuse the existing practice/resume contract and correct exercise metadata |
| writing.js stores unsent drafts in keys scoped by user, exercise and sentence | Same-browser drafts are distinct from server-persisted progress/history |
| WritingService.PrivateExercises.cs centralizes public/published, owned-private/Premium and Admin access | Recheck current access on both recent reads and visit writes; avoid broadening rights |
| SpeakingService already implements a separate visit table, conditional monotonic updates, recent queries and account cleanup | Reuse the reviewed ideas, not Speaking-specific entities or blindly copied predicates |
| AccountDeletionService explicitly deletes personal Speaking visits before removal/anonymization | Extend the real account cleanup path for Writing visits |
| Current WritingService already injects LearningBusinessClock | Use its server UTC instant; no new service registration or client timestamps |

Reference the current Writing/Goals stage07 ledger and the symptom-relevant
Speaking recent-history bug entry, especially the first-insert race. They are
supporting evidence, not proof that Writing already has this feature. CodeGraph
may omit most Razor source; read only the missing view sections directly.

## Product behavior

1. Place the recent section immediately below the exercise-page header and above
   the filter toolbar. Preserve the existing Clay design and page sections.
2. Show **six or fewer unique exercises across all Writing levels/formats** for
   the current account. It is independent of level/contentType/topic/status/page/
   myPage filters, so changing the catalog cannot hide the exercise just left.
   Add helper copy: "Những bài bạn vừa mở, ở mọi cấp độ và dạng bài."
3. Only a successfully rendered, authorized, nonempty practice exercise records
   a visit. Catalog browsing, filter/pager changes, hovering/prefetch, GET data/
   hint requests, exercise creation without opening, and failed/forbidden loads
   do not record visits. No submission is required.
4. Opening A → B → A produces A, B, with one row per user/exercise. Completed
   exercises remain eligible. Reopening updates recency, never learning rewards.
5. A card shows encoded title, real level/format, optional topic/private label,
   last-opened time, and current server progress/status. A never-submitted visit
   is honestly "Chưa bắt đầu"/"Chưa gửi bài", not completed or learned.
6. Use "Mở bài" for no saved progress, "Tiếp tục" for started work, and the
   existing equivalent of "Luyện lại" for completed work. Keep canonical Writing
   status semantics, including NeedsReview in the existing practice experience.
7. Card navigation uses **the item's own** level/contentType/exerciseId, not the
   current list's filter values. Do not inherit a stale topic/status that prevents
   opening another-level exercise. Native Back returns to the original list URL;
   existing practice back links remain valid. No arbitrary external return URL.
8. Resume with the current server ResumeSentenceId/progress plus existing
   same-browser owner-scoped drafts. Verify this works rather than replacing the
   editor or assuming every typed draft was uploaded.
9. Visit history and submitted progress persist across sessions/devices in DB.
   Unsent draft text remains browser-local under current behavior. Do not promise
   cross-device recovery of unsent text or change draft storage in this scope.
10. Guests see no personal recent cards or requests and retain the public catalog.
    Signed-in users with no history see a truthful empty state. Do not synthesize
    historical opened-at timestamps from account creation or old attempts.
11. Revalidate actual user/exercise access before selecting the latest six. Skip
    deleted/empty/unavailable exercises. Ordinary shared lessons require
    `UserId == null && IsPublished`; an eligible owner's private lesson does not
    have to be published. Apply the current private/Premium/Admin predicate,
    not a blanket IsPublished check. Standard/Premium users never see another
    person's private lesson.
    Preserve the current deliberate Admin access policy and test its exception
    explicitly. Even Admin recent history/progress belongs to the signed-in Admin,
    not another learner. Do not add a new private-content browsing capability.
12. Transient tracking/read failure does not break writing or erase drafts. Report
    a nonblocking failure honestly; provide a real retry for recent-list loading.
    Do not report an unsuccessful visit as saved. An instant close before the page
    initializes/request reaches the server is not guaranteed recoverable.

## Data and service design

Recommended new entity: `TCTEnglish.Models.UserWritingExerciseVisit` in
`Models/UserWritingExerciseVisit.cs`; table/DbSet `UserWritingExerciseVisits`.

- `UserId`, `WritingExerciseId`, `LastOpenedAtUtc` only; composite unique identity
  `(UserId, WritingExerciseId)` and user/time/exercise ordering index.
- Capture one server `_clock.UtcNow` per logical visit. Store explicit UTC with
  suitable MariaDB precision; return the persisted time with UTC semantics.
- Preserve referential integrity and supported cascade behavior for real user/
  exercise removal. Account anonymization must explicitly clear visits too.
- Keep the history model separate from UserWritingAttempts, sentence/exercise
  progress, XP/streak/goal counters, grants and completion credits.
- Do not automatically backfill a new visit table or create fake attempts. Old
  attempts/progress remain usable through their existing lists/resume paths.

Extend existing IWritingService and a focused partial, for example
`Services/WritingService.RecentExercises.cs`, with proposed methods:

```csharp
Task<DateTime?> RecordExerciseVisitAsync(int userId, int exerciseId, CancellationToken ct = default);
Task<IReadOnlyList<RecentWritingExerciseViewModel>> GetRecentExercisesAsync(int userId, int limit = 6, CancellationToken ct = default);
```

Return null/not-found for inaccessible visit targets. Validate active/non-anonymized
user state as required by current account policy; identity is server-derived.
Reuse/factor current access predicates without rewriting unrelated catalog rules.
Queries use AsNoTracking/projections, access filtering **before** OrderBy/Take(6),
and stable `LastOpenedAtUtc desc, WritingExerciseId desc` tie-breaking. Aggregate
current-user progress for the selected exercises without loading full attempt
feedback/answers or using one query per card. New recent DTOs expose metadata and
progress counts/status, never answer keys, draft text or another user's answers.

Use a race-safe monotonic upsert/transaction strategy. The database must retain
the maximum accepted server timestamp even if an earlier request finishes last.
Cover the UPDATE → competing INSERT → existence-check path, not only duplicate
inserts. Recover only the specific unique-key conflict; propagate genuine DB/FK/
deadlock failures appropriately. Compare the timestamp actually stored at DB
precision. Coordinate with current user mutation/account-deletion locking so
anonymization cannot leave a newly inserted visit behind. Do not regress the
current Writing reward transaction while introducing visit writes.

## HTTP and browser contract

Proposed new absolute routes on StudyController, kept separate from its `Home`
route prefix (inspect current route attributes before implementation):

| Method/route | Response |
| --- | --- |
| GET `/api/writing/recent` | Authenticated `{ success: true, data: [...] }`, no-store |
| POST `/api/writing/{exerciseId:int}/recent` | Authenticated, antiforgery; `{ success: true, lastOpenedAtUtc }`, no-store |

Both use the verified current-user helper. Do not accept UserId or opened-at from
the client. Parameterized access violations return 404; malformed input uses the
established 400 contract. Auth/session failure must not be parsed as a successful
HTML redirect. Existing Writing routes/JSON continue unchanged. GET paths and
existing service read methods remain read-only; all history mutation uses POST.

Prefer initial server-rendered recent cards for authenticated list requests,
using a new typed property on WritingExerciseListViewModel and a scoped partial
such as `Views/Study/_RecentWritingExercises.cshtml`. Any list refresh API returns
the same data semantics. Do not add recent data to lightweight routes needlessly.

Use an isolated module such as `wwwroot/js/writing-recent.js` with explicit list
and practice modes, independent of the create-from-AI form. That form's current
early return must not disable recent refresh for Standard users.

- On authorized practice initialization with valid payload/sentences, send one
  antiforgery POST, independent of evaluation. Keep navigation/editor unblocked.
- Use bounded, same-origin fetch/keepalive where appropriate for quick Back/close;
  do not rely solely on unload/beforeunload, fake success or synchronous requests.
- Dedupe overlapping DOMContentLoaded/pageshow/visibility triggers. A genuine
  practice reactivation may refresh recency; heartbeat/typing does not spam visits.
- Refresh the list on Back/bfcache restoration and appropriate tab activation.
  Handle each fresh response with latest-request ordering/abort protection.
- Hide personal cards on pagehide before a possible bfcache snapshot, and on
  restoration keep them concealed until revalidation. On 401,
  account switch or rejected session, remove stale recent content. No persistent
  localStorage cache for this personal list. Cache headers alone do not prove
  bfcache/account isolation.
- If data refresh fails for the same still-valid session, a clearly marked last
  known list may remain with retry. Never retain it after identity uncertainty.
- Build links safely from server routes/current metadata; render text through
  Razor encoding or textContent. No innerHTML of untrusted titles/feedback.

## Ordered implementation checklist

- [x] Inspect actual dirty work, stage07 ledger, relevant bug memory, current
  interface/access/resume/draft/UI/account contracts. Record baseline behavior
  and tests. Preserve all preceding 108-lesson/Goals/badge work.
- [x] Write meaningful failing service/endpoint/lifecycle regressions for the new
  behavior, using existing relational/provider fixtures and controlled clocks.
- [x] Implement visit entity/mapping/service/query and real deletion/anonymization
  cleanup. Create an additive migration, Designer and snapshot via repo-local EF.
  Preserve the existing uncommitted badge migration/snapshot work.
- [x] Implement protected POST/read API and extend typed list ViewModel/read path.
- [x] Add the recent partial/scoped Clay CSS/module to actual list/practice pages.
  Preserve existing DOM IDs, catalog counts, pagination, create/delete, editor,
  draft, assessment, Goals and reward behavior.
- [x] Verify browser recovery and role/access cases; finish with focused/full
  appropriate regression checks, model/provider evidence and a reviewed diff.
- [x] Write `docs/feature-audits/2026-11-15-writing-recent-exercises-ledger.md`,
  update bug memory only for actual fixes, and report in Vietnamese.

## Acceptance matrix

| ID | Required result |
| --- | --- |
| R01 | Fresh account: honest empty section; guest: no personal section/requests |
| R02 | Open valid A without submitting → back/reload → A appears, progress remains zero |
| R03 | A → B → A yields A,B; one visit per exercise/user |
| R04 | Open seven valid exercises: latest six, deterministic ties, no duplicates |
| R05 | Catalog level/type/topic/status/page/myPage changes do not filter or alter recent order |
| R06 | Cross-level/type recent link opens the actual exercise without stale-filter404 |
| R07 | Accepted progress and same-browser unsent draft restore through the current flow |
| R08 | New session/device sees persisted history/progress; no false claim about local-only drafts |
| R09 | User A→logout→B/Back/bfcache: no A's recent titles, progress or drafts leak |
| R10 | Eligible own private works even if unpublished; Standard/Premium foreign-private or unpublished-public IDs return 404; deliberate Admin exception is verified |
| R11 | Role downgrade/removal/deletion/empty/unpublished-public hides unavailable entries before limit under the current access policy |
| R12 | Real account delete and anonymization clear visits; concurrent visit cannot survive cleanup |
| R13 | Concurrent first inserts/out-of-order completion keep one row and newest UTC time |
| R14 | Missing/bad CSRF rejected; user/time spoofing ignored/rejected; GET reads create no visit |
| R15 | Visit alone changes no attempts/progress/XP/streak/goals/badges/credits/outbox/reward state |
| R16 | Quick Back/close and retry are observed honestly; offline/read/write failure preserves editing/draft |
| R17 | Recent-list lifecycle works without AI create form, on Back and with out-of-order fetch responses |
| R18 | Encoded hostile/long titles, safe URLs, keyboard, loading/empty/error/retry and completed cards |
| R19 | 1440/1366/768/390 widths, reduced motion and native 200% zoom observed or marked unverified |
| R20 | MariaDB 10.5.23 upgrade/repeat/concurrency/cascade/preservation; EF model clean |

For R15, compare relevant persisted row values, not just row counts. Avoid creating
accepted submissions/learning telemetry during that isolated test. Ordinary
existing time-tracker behavior is not evidence that the new visit path grants
learning credit; keep the boundaries testable separately.

Run a Release build, focused Writing recent and existing Writing progress/draft/
completion/Goals/account-cleanup regressions and the affected Node/render suites.
Run the full application suite once after final integration when feasible; retain
baseline/flaky failures honestly and do not weaken unrelated assertions.
Use matching disposable MariaDB
10.5.23 for schema, index, concurrent and cleanup behavior; EF InMemory/SQLite
alone cannot certify it. Test upgrade from the current badge-schema baseline,
not a made-up older snapshot. Preserve old rows/relationships and include the
new migration+Designer+snapshot with code. No runtime migration generation.

The visit table is personal data, not shared content. Do not export visits or
change the 108 lesson packages, catalog hashes or production.json for this feature.
No empty shared-data package is needed. Before any actual local migration, retain
an ignored affected-data backup and verify the Docker target; never use hosting.

## Vietnamese owner handoff

Implementation checkpoint (2026-11-15): implemented locally on the existing dirty
branch by one agent. See
`docs/feature-audits/2026-11-15-writing-recent-exercises-ledger.md` for the completed
R01–R20 evidence and limitations. Final Release full suite: 1,584 passed, 51 gated
skips, zero failures; Node:42 passed; final matching MariaDB exact-insert-race,
upgrade/repeat/preservation/access/cleanup/concurrency run plus recent regressions:
10 passed, zero skipped. EF model clean. No remote Git/release or paid calls.
Native 200% zoom and actual restored bfcache/focus remain unobserved in MCP;
controlled lifecycle regressions pass. Initial EXE/content-root setup failures
and the non-reproducing classifier full-suite failure are retained in the ledger.

Report changed files, exact checks/provider/browser evidence and remaining limits.
Give manual steps: open A without submitting → list → open B → list → reopen A →
verify order → filter another level → recent link → confirm draft/progress →
switch accounts. State that local unsent drafts are not cloud backups. Distinguish
local from CI/staging/production and stop before ungranted release actions.
