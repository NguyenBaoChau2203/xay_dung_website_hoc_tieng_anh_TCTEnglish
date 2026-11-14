# Speaking: recently opened practice videos

Date: 2026-11-13
Status: implemented and reviewed locally on 2026-11-14 after the owner requested
completion of Gemini's partial work. Native 200% browser zoom remains unverified.
The initial document-authoring task was PLAN_ONLY; the owner's subsequent request
authorized local implementation and fixes. No Git or release stage was granted.
Evidence: [implementation ledger](../../feature-audits/2026-11-13-speaking-recent-videos-ledger.md).
Execution prompt: [Gemini handoff](../../../.ai/prompts/gemini-speaking-recent-videos.md).

## 1. Outcome and product decisions

Add a personal **“Luyện nói gần đây”** section to `/Speaking`. A learner can
quickly reopen the Speaking videos whose practice pages they most recently
visited, even before recording or receiving a score.

Example: open A, then B, then A again. The section contains A, B in that order,
with one card for A. Reloading, signing out and back in, or using another browser
with the same account preserves this order.

| Decision | Required behavior |
| --- | --- |
| Definition of a visit | The accessible practice page is loaded and becomes visible in the browser; no recording, assessment or completion is required. |
| Excluded actions | Catalog impressions, hovering, filtering, copying a link, video import alone, unsuccessful/unauthorized practice navigation and background preload do not count. |
| Returning to practice | A visible restored practice page, including bfcache restoration, counts as another visit. No continuous heartbeat or write on every tab focus. |
| Recent list | Show at most six distinct videos, newest first. Reopening updates the existing user/video row and brings it to the front. |
| Storage | Server database, one row per user and Speaking video. Browser storage may not be the source of truth. |
| Supported videos | Accessible public videos plus the current Premium/Admin learner's own usable private videos, following current practice access rules. |
| Role downgrade | Hide private videos when the current account no longer has permission; preserve history for a possible later upgrade. Public history still works for Standard users. |
| Filters | This personal section is independent of catalog search, CEFR/topic filters, mine/public paging and their counts. Keep its description explicit. |
| Existing data | Start empty. Do not infer historical opens from CreatedAt, transcript import time, best-score timestamps, completion or session time. |
| List limit | Six visible cards; retain the unique history rows without an arbitrary pruning job. No full-history screen, Show more, clear-history or remove-card actions in this scope. |
| Learning effects | The visit endpoint updates only recent history. It must not save a score, consume an assessment, award XP or change completion, streaks or learning-time totals. Existing independent page time tracking continues unchanged. |

UI copy is product data, not additional agent instructions:

- Heading: `Luyện nói gần đây`.
- Description: `Mở lại những video bạn vừa vào luyện tập.`
- Empty title: `Bạn chưa mở video luyện nói nào`.
- Empty guidance: `Chọn một video bên dưới để bắt đầu. Bài vừa mở sẽ xuất hiện ở đây.`
- Navigation label: `Mở lại bài luyện nói`; it must not imply resuming the exact
  sentence or playback second, because that capability is outside this feature.
- Optional time text: `Vừa mở`, `Hôm nay`, `Hôm qua`, or a localized date, based
  on real server UTC timestamps converted for display. Include a `<time>` value
  and accessible full local date/time. Never use fabricated progress or timestamps.

## 2. Evidence and current boundaries

Verified in source on 2026-11-13; the executor must recheck the actual checkout:

- Branch was `fix/speaking-catalog-transcript-polish`. Existing uncommitted
  Speaking/transcript/import/UI changes belong to other ongoing owner work.
  Preserve them; do not reset, switch branches or replace entire files.
- `SpeakingController.Index` calls
  `SpeakingService.GetSpeakingIndexViewModelAsync`. Practice is served through
  both `/Speaking/Practice?id=...` and `/Speaking/Practice/{id:int}`.
- `GetSpeakingPracticeViewModelAsync` verifies public/owned access and hides
  owned private practice from Standard users. The current normalized role is
  read from the database. Reuse this policy rather than copying cookie claims.
- `UserSpeakingProgress` stores sentence scores and PracticedAt, while
  `UserSpeakingVideoCompletion` stores completion evaluations. Neither represents
  all page opens. `LearningActivitySession` has no Speaking video identifier.
- `SpeakingIndexViewModel` currently has public catalogs, playlists and MyVideos,
  with no recent-video collection. The page has a hero/import panel, benefits,
  My Videos and the public library.
- `_CatalogVideoCard.cshtml` uses `.vi-video-col`, and `speaking.js` has broad
  selectors for public filtering, card detection and pagination. Recent cards
  must not accidentally participate in those selectors or public counters.
- `AccountDeletionService.DeletePersonalRowsAsync` explicitly deletes personal
  rows before either physical user deletion or anonymization. New history must
  be cleared on both paths; a user FK cascade alone cannot cover anonymization.
- Target/provider: net9.0, repository-local EF 9.0.4, Pomelo 9.0.0 and MariaDB
  10.5.23. Many existing Speaking types use legacy `TCTVocabulary.*` namespaces;
  new types should use canonical `TCTEnglish.*` with explicit imports.

Observed source does not prove browser behavior. Current local counts, app PID,
build freshness, available Chrome MCP tools and test fixture accounts must be
verified by the implementing agent, not copied from an older report.

## 3. Authority and scope

The handoff requests IMPLEMENT_LOCAL for this specific feature: additive history
entity/mapping/migration, owned read/write endpoints, service/ViewModels, Speaking
page integration, account cleanup, focused tests and local browser verification.
This is required implementation work, not a mandate to redesign all Speaking.

Use one agent and the current suitable checkout. No commit, push, PR, remote CI,
merge, staging or production deployment is granted. Do not modify Program.cs,
appsettings files, project files, dependencies, global UI tokens/layouts, applied
migrations, old content packages or release gates. Use the existing Speaking
service construction/interface path; a separate new DI registration is unnecessary.

The migration is additive. Creating/applying this feature's reviewed migration to
local Docker is in scope; verify first on a disposable matching MariaDB. Never
reset the owner's database or change production. Test deletion/anonymization
only on synthetic accounts in an isolated test database.

Recent history is personal data. Do not export it into `data/shared`, modify
shared adapters to include it, or change any curated public/private video content.
No shared-content export is required if only schema/code and private history
fixtures change. Audio recording or a paid AI/ASR request is unnecessary for this
feature. Do not spend or extend the earlier separate UAT assessment allowance.

## 4. Proposed design

### Persistence

Add `TCTEnglish/Models/UserSpeakingVideoVisit.cs` with a minimal typed entity:

- `UserId`, `VideoId`: required FK identifiers, composite primary key.
- `LastOpenedAtUtc`: required UTC DateTime stored with `datetime(6)` precision.
- Navigation to the existing User and SpeakingVideo types if useful for queries.

Add the DbSet and mapping in `Models/DbflashcardContext.cs`. Use an index starting
with `(UserId, LastOpenedAtUtc, VideoId)` for bounded recent queries. Select
provider-compatible FK deletion behavior after inspecting current Speaking
relationships. Deleting a video must remove its history via reviewed cascade
or explicit deletion in every relevant path. Account cleanup must explicitly
delete visits before the existing physical/anonymization decision.

Do not add columns to existing learning/completion tables or backfill fake visits.
No existing data must be rewritten. Include generated migration, Designer and
snapshot together, using the repository EF tool. If another task has already
changed the model/snapshot, reconcile carefully without capturing unrelated
pending schema changes in this migration.

### Service and endpoint contract

Extend the current Speaking service/interface rather than adding business logic
to HomeController or making new protected configuration changes.

1. `RecordVideoVisitAsync(userId, videoId, cancellationToken)`:
   - Check the current user's existence/state as required by current auth,
     current database role, video access, and usable practice content. Reuse
     the practice eligibility policy; account for legacy nullable metadata
     instead of excluding valid old videos by an invented ImportStatus rule.
   - Inaccessible, missing, another user's private, locked private or unusable
     video: return NotFound, creating no history.
   - Take the timestamp on the server, never from request body/clock parameters.
   - Perform an atomic insert/update keyed by `(UserId, VideoId)`. Duplicate
     POSTs and concurrent tabs must leave one row, not an HTTP 500. Preserve
     monotonic time if an earlier timestamp update completes later.
   - Use the existing provider/execution strategy conventions. Any raw SQL must
     be parameterized. Catch only the specific duplicate-key race if choosing
     an EF retry approach; unrelated DB errors must remain diagnosable.
2. `GetRecentVideosAsync(userId, limit = 6, cancellationToken)`:
   - Bound the result to six server-side. Filter by caller and current access
     **before Take**; hidden rows must not displace valid visible entries.
   - Order LastOpenedAtUtc descending, then VideoId descending for stable ties.
   - Use AsNoTracking/projection and bounded SQL. No full-history materialization
     or one query per card. Use current video title/metadata, not stale copies.
3. New controller routes, preserving all existing contracts:
   - `POST /api/speaking/{videoId:int}/recent`, authenticated + antiforgery.
     No userId/timestamp/score input. Successful new or repeat write returns
     HTTP 200 with `{ success: true, lastOpenedAtUtc: "...Z" }`.
   - `GET /api/speaking/recent`, authenticated, read-only; returns HTTP 200 with
     `{ success: true, data: [...] }`, max six typed recent-card DTOs. Send
     private/no-store cache headers for this personal response and ensure the
     authenticated Index HTML cannot be shared through a public cache.
   - Follow current middleware for expired authentication and antiforgery error
     behavior. The client must detect a login HTML redirect rather than treating
     any HTTP 200 as successful tracking. Never accept caller-supplied identity.

Add a typed recent-card ViewModel containing existing safe video display data
and LastOpenedAtUtc. Populate `SpeakingIndexViewModel.RecentVideos` through the
same service query used by the read endpoint. Catalog continuation JSON should
retain its current contract; avoid repeatedly loading recent rows for every
pagination request if a small explicit service option/separate page composition
can keep that query at the initial Index boundary.

### Browser integration

Use a small scoped module such as `wwwroot/js/speaking-recent.js` for lifecycle
tracking and recent-section refresh. This avoids coupling history to the large
recording/scoring state machine and to the catalog's filter-button guard.

- Render server-validated VideoId and the actual antiforgery token on Practice;
  locate the existing token/header convention before implementing the fetch.
- Send one small POST when an eligible practice page first becomes visible.
  Handle background-tab opening by waiting for first visibility. A visible
  `pageshow.persisted` restoration may send another visit; deduplicate overlapping
  initial pageshow/DOMContentLoaded/visibility handlers. Ordinary tab focus must
  not repeatedly write history. Support both existing Practice URL formats.
- Keep navigation, recording, self practice and playback usable if this request
  fails. Do not trigger recording/ASR or award anything. Avoid unhandled promise
  rejections, infinite retries and persistent service-worker/background tracking.
- Preserve a small in-flight request on quick navigation where browser support
  permits (`fetch` keepalive with the actual antiforgery header). Verify a quick
  Back path rather than relying on a catalog click handler as tracking evidence.
- On Index bfcache return, refresh the recent section from the read endpoint so
  it does not show the previous order. A bounded follow-up read may be needed
  if the write is still completing; do not introduce polling. Failed refresh
  keeps existing server-rendered cards and gives a small accessible retry state.
- Build refreshed content with DOM text/attributes and trusted internal URLs;
  no untrusted title HTML. Initial Razor rendering must encode the same data.
- The new module must exit harmlessly on other pages and not depend on
  microphone permission, player initialization or filter controls.

### UI placement and appearance

Place the section directly after the current hero/import panel and before the
benefits/My Videos/library sections. Keep it compact, with a semantic h2 and
three columns on wide desktop, two on tablet, one on mobile, up to six cards.
Adapt to the actual available content width; no new carousel package.

Reuse existing card visuals and TCT Clay tokens inside the current `.tct-theme`.
Use a scoped partial such as `Views/Speaking/_RecentVideos.cshtml` and recent-only
card wrappers/classes. Do not copy `.vi-video-col`, `.vi-level-section`, IDs,
filter data attributes or catalog pagination hooks onto recent cards. Shared
visual CSS classes may be reused without sharing their JS behavior hooks.

Each card shows real thumbnail, title, available CEFR/topic/duration, private badge
when appropriate, last-opened time and an anchor to the correct Practice route.
Metadata that is unavailable is omitted or given honest neutral text. The whole
card may be one link; no nested buttons/anchors. No exact-sentence resume promise,
invented percentage, score or completion badge is required. Empty state is a small
helpful panel, not six placeholders. Use existing image fallback behavior.

Preserve one h1, keyboard focus, native navigation/new-tab behavior, existing icon
family, reduced motion and readable text. Page CSS stays scoped in speaking.css
and uses shared tokens. Shared foundation/layout changes require separate scope.

## 5. Ordered implementation checklist

- [x] **Baseline:** inspect current branch/HEAD/diff and active edits. Read only
  relevant source with CodeGraph first. Capture current route/access contracts,
  schema/snapshot state and target test commands. Record pre-existing failures.
- [x] **Schema:** publish the additive impact assessment; add model/DbSet/mapping,
  migration+Designer+snapshot. Review FK/index order and data-preserving Up.
  Do not execute destructive Down on any populated owner database.
- [x] **Behavior tests:** add meaningful tests for all below contracts before
  implementing the service/endpoints; demonstrate failures for the missing feature.
- [x] **Server implementation:** upsert/query, authenticated antiforgery endpoints,
  typed ViewModels, Index composition and account/video deletion cleanup.
- [x] **UI implementation:** scoped recent partial/CSS/module, Practice visit trigger,
  server-rendered Index, bfcache refresh and truthful empty/error states.
- [x] **Provider validation:** migrate a disposable MariaDB 10.5.23 from the previous
  head, verify old rows/FKs/hashes, repeat update with no changes, exercise concurrent
  visits and deletion/anonymization. Then apply reviewed additive migration locally.
- [x] **Verification:** Release build, focused .NET/Node checks, pending-model check,
  authenticated Chrome MCP scenarios and visual evidence. Review the complete diff
  inline, including data/privacy and compatibility with pre-existing edits.
- [x] **Handoff:** update this checklist and create the implementation ledger. Leave
  the local site ready for the owner to review when it is safe to run it.

## 6. Acceptance matrix

| ID | Scenario and required result | Evidence |
| --- | --- | --- |
| R01 | Account with no visit rows sees the honest compact empty state. No fabricated backfill. | Integration + browser |
| R02 | Opening an accessible public Practice page creates one personal visit without recording. Both legacy/query and path URLs work. | HTTP + DB + browser |
| R03 | A → B → A produces A, B; only one A row exists and timestamp advances. | Service/integration + browser |
| R04 | More than six distinct eligible visits returns exactly the newest six with stable ties. | Query tests |
| R05 | Reload and fresh authenticated browser session preserve history; account B sees only B's visits. | Integration + browser |
| R06 | Premium sees their own private visit. Another learner's private ID returns 404 with no visit/metadata leak. | Security integration |
| R07 | Role downgrade hides own private visits before limiting; public list fills remaining slots; re-upgrade can show them again. | Integration |
| R08 | Guest, missing ID, unavailable/unusable video and missing/invalid antiforgery cannot create history. GET endpoints never mutate. | HTTP integration |
| R09 | Concurrent first writes/repeated POSTs leave one row; interleaved timestamps never move backward. | Real MariaDB concurrency |
| R10 | Calling the visit endpoint changes no progress, assessment, completion, XP, streak or learning-time state; existing page time tracking still operates independently. | Before/after persistence assertions |
| R11 | Deleting a synthetic video clears its visit rows and does not orphan/break deletion. | Provider integration |
| R12 | Both synthetic account deletion and anonymization clear private history; another learner's history survives. | Account/provider regression |
| R13 | Public CEFR/topic/search, empty result, paging and counts behave identically with recent cards present. Recent remains independent. | Node + browser |
| R14 | Browser Back/bfcache shows the new order; background opening waits for visibility; no duplicate handlers/writes on ordinary focus. | Browser + JS lifecycle tests |
| R15 | Quick navigation and network failure do not block learning or falsely report a saved visit; bounded refresh/retry behaves honestly. | Browser fault + JS tests |
| R16 | Long/untrusted titles remain text; personal HTML/JSON is not publicly cached; login redirects are not parsed as success. | Security + HTTP/JS tests |
| R17 | Six cards and empty/error states fit 1440/1366/768/390px, keyboard, native 200% zoom and reduced motion. | Actual browser; blocked checks explicit |
| R18 | Additive upgrade preserves old content/users/progress, repeated migrate is a no-op and EF reports no pending model changes. | Disposable MariaDB + EF output |
| R19 | Current private import, transcript selection, score restoration, recording/self practice and dictation regressions remain passing. | Existing targeted suites; provider calls faked |

Useful verified command entrypoints (adapt test filters to final test names):

```powershell
dotnet tool restore
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --filter "FullyQualifiedName~Speaking|FullyQualifiedName~AccountReview"
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Use existing MariaDB acceptance factories/connection guards and inspect the
current harness before running provider tests; do not aim destructive fixture
operations at the owner's development database. A skipped MariaDB case is not
provider proof. Run the relevant existing speaking Node suites plus the new
recent-history suite. If modifying prompt/guidance artifacts, also run the
AGENTS.md tooling validator and Python tests; they do not prove this feature.

For browser UAT prefer Chrome DevTools MCP, discover actual tools, verify current
build and `/health/ready`. Default port is 5100. Preserve an occupied process
unless its ownership and restart authority are established; otherwise use a free
port through the launcher and report the real URL. Never report an old running
binary as the new implementation. No paid audio calls are needed.

## 7. Deliverables and completion evidence

- Feature code, additive migration/Designer/snapshot and meaningful regressions.
- `docs/feature-audits/2026-11-13-speaking-recent-videos-ledger.md` with baseline,
  decisions, changed files, R01–R19 actual results, exact commands/counts, schema
  preservation evidence, screenshots and unresolved blockers.
- Keep synthetic account names, recordings, credentials and private runtime
  evidence out of tracked artifacts; ignored scratch belongs under
  `.appdata/local/speaking-recent-videos/` on this checkout drive.
- If an implementation bug is fixed, update only the symptom-relevant bug memory
  per AGENTS.md. Do not claim a new feature was already an old resolved defect.
- Vietnamese owner handoff: what was built, how to review it, actual checks,
  remaining limits and local URL. Explicitly distinguish local completion from
  CI/staging/production. No ungranted Git/release stage.
