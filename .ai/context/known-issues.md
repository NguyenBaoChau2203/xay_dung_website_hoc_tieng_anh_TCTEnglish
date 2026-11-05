# TCT English - Known Issues & Technical Debt

This document tracks the confirmed remaining issues after the March 2026
controller/service/view-model refactor. Agents should read this before
proposing follow-up work so they do not optimize already-completed items.

---

## Current Status Snapshot

- `HomeController` is now limited to dashboard/public pages; class, folder, set,
  study, and chat flows live in dedicated controllers while keeping legacy
  `/Home/*` routes.
- `ViewModels/` is the single feature view-model folder; the legacy
  `TCTEnglish/ViewModel/` folder is gone.
- `CurrentUserIdExtensions` plus `BaseController` now own claim parsing for MVC
  and API flows; direct inline `ClaimTypes.NameIdentifier` parsing in
  controllers has been removed.
- `TCTEnglish.Tests` exists and covers smoke routes, SQLite integration, SignalR
  chat upload authorization, vocabulary system pages, and folder/set anti-IDOR
  regressions.
- Earlier refactor blockers around `SpeakingController` includes, SignalR method
  name mismatches, class detail read-scope leakage, and split folder/set IDOR
  gaps are resolved.

---

## Known Bugs (Unresolved)

### STAGING-HTTPS-RENEWAL — operational follow-up05/11/2026

- Staging N518 đã chạy https://staging.tctenglish.site với DB/site/pool riêng;
  readiness200 sau Basic Auth, anonymous401, shared Writing/progress isolation
  và backup/restore/repeat schema đã kiểm. Không còn blocker “chưa có server”.
- Certificate hiện hợp lệ đến03/02/2027. Root Basic Auth từng chặn HTTP-01;
  issuance thành công khi app offline và tạm gỡ riêng bảo vệ staging, rồi đã
  khôi phục. Auto-renew qua auth chưa được nghiệm thu. Cần exception anonymous
  chỉ path ACME từ Nhân Hòa hoặc maintenance/reissue theo
  [runbook](../../docs/operations/staging-plesk-runbook.md). Không nới auth prod.
- Follow-up05/11: owner cấp key staging riêng; Chat200/STAGING_OK và Speech200/
  Completed/audio tổng hợp2.75s đã đạt sau khi khắc phục outbound IPv6 bằng env
  chỉ staging. Writing Local +cloud feedback; không copy key local. CD staging có
  source/job/15regressions, Environment chỉ master, webhook/manifest branch riêng;
  owner đã cho commit/PR/merge và bật riêng staging. Gate production vẫn false.
  Kích hoạt phải đối chiếu run/marker thật; xem [automation](../../docs/operations/staging-automation.md).
- Exception ACME cần Nhân Hòa xác nhận cấp server; [nội dung hỗ trợ](../../docs/operations/staging-https-support-request.md).

### WRITING-DRAFT-OWNER: Writing draft keys are shared across accounts [P1] — fixed 04/11/2026
- **Was**: wwwroot/js/writing.js used wp_draft_ex{exercise}_{sentence} without owner.
- **Fix**: server-verified `CurrentOwnerUserId` → `data-owner-user-id`; namespace
  `wp_draft_u{owner}_ex{exercise}_{sentence}`; legacy unowned keys ignored; missing
  owner does not persist. Node harness `test_writing_draft_isolation.cjs` green.
- **Browser verified04–05/11**: Standard draft retained in localStorage; logout→
  Premium in the same context does not restore it. No owner data was cleared.

### WRITING-REVIEW-STATE: NeedsReview can render as redo [P2] — fixed 04/11/2026
- **Was**: normalize dropped AssessmentStatus/ReviewRequired; label used “Làm lại”.
- **Fix**: snapshot/Razor/JS preserve status fields; label “Cần xem lại”; historical
  rows map via `WritingAssessmentPolicy.ResolveHistoricalStatus` from EvaluationSource.
- **Follow-up (same day UAT)**: `recentAttempts.statusLabel` also mapped NeedsReview
  → “Cần xem lại” via `ResolveHistoricalStatusLabel` (was binary “Cần sửa”).

### FINAL-POLISH-FINAL-CHECKS: C1.1 closed; R6 UAT advanced 04/11/2026

- **Owner closeout05/11**: rendered60role/viewport/routes; real Admin stale-form
  conflict/input preservation; Writing approved/local-exact/NeedsReview+reload;
  same-profile draft isolation; completion→Goals/badges/notifications +replay.
  Notifications CSRF400/non-owner404/read-all0; AI full/embed fake503; Speaking
  recorder/ASR fake, word chips literal-safe, NoSpeech no commit/mic-denied.
- **Fixed during review**: Speaking init independent of video API/late callback;
  mobile toast; NoSpeech persistent message; Goals overall percent/editor target
  for non-Vocabulary goals. New regression red→green. Full .NET929/0/18skip,
  Node58/0, Goals51/0. Browser Writing1/2→50%, replay unchanged.
- **Still separate acceptance**: paid API/real mic, broad all-feature owner UAT,
  hosting/staging provision and production. Web5100 restarted with Local Writing
  cloud enhancement disabled for fee-free UAT; saved key unchanged/configured.
  [Owner closeout](../../docs/feature-audits/2026-11-05-chau-final-polish-owner-closeout.md),
  [database collaboration](../../docs/operations/database-collaboration.md).

- **R1–R5 / RR2 / C1.1**: reviewer-verified; not re-implemented.
- **R6 advanced (local)**: synthetic Admin/Standard/Premium; Writing #23 Admin
  variant→Accepted + NeedsReview; Speaking demo **55+44** (53 not demo-approved);
  cold launcher→ready ~105s; 3-role matrix + A→B draft PASS. Live mic/paid pending.
  Not B0–B7 production closure.
- **UAT fix**: `recentAttempts` NeedsReview label was “Cần sửa”; now
  `ResolveHistoricalStatusLabel` → “Cần xem lại” (unit + live Practice/Data).
- Evidence: `.tmp/chau-r6-uat-ledger.md`, `.tmp/chau-final-polish-demo-manifest.md`,
  `.tmp/uat-startup-measure.md`,
  [final-checks plan](../../docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md),
  [code acceptance](../../docs/feature-audits/2026-11-04-chau-final-polish-code-acceptance.md),
  [active prompt](../prompts/chau-final-polish-schema-guard-uat.md).

Historical reviewer round2, before final-check fixes (not current status):

Reviewer lần hai04/11 confirms app R1–R5 via .NET913/0/14, Node54/0, guarded
MariaDB upgrade1pass +HTTP concurrency1pass and extra empty/existing/save-vs-edit
probes. No old P1 reproduced. **Remaining tests RR1/RR2 [P2]**:

- Upgrade baseline test still lacks fresh-history/schema guard, so rerun on an
  upgraded acceptance target can run Down/drop variants. Dry-run confirmed SQL;
  reviewer did not execute downgrade.
- Render-twice test edits fixture DOM itself instead of invoking production
  renderer. Actual renderer passed reviewer fake assess/commit flow, but tracked
  regression must run that behavior; strengthen concurrency barrier coverage too.
- R6 browser/demo/measurement remains pending. [Rereview](../../docs/feature-audits/2026-11-04-chau-final-polish-rereview.md),
  [focused plan](../../docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md),
  [Cursor prompt](../prompts/chau-final-polish-final-checks.md).

### BUG-007: AutoUnlockWorker is disabled by default in the current config
- **Symptom**: Expired locks are not auto-cleared in the default app
  configuration; users rely on lazy unlock during login or manual admin unlock.
- **Root Cause**: `AutoUnlockWorker` exits early unless
  `BackgroundJobs:AutoUnlockWorkerEnabled` is set to `true`, and the shipped
  config does not currently enable it.
- **When it triggers**: Any environment that uses the current appsettings files
  without an override.
- **Recommended fix**: Add environment-specific configuration and validate
  `LockExpiry` data before enabling the worker broadly.
- **Status**: Confirmed unresolved / operational gap.
- **Local hardening03/11**: Conditional unlock is committed before email;
  fake SMTP timeout/cancel and worker retry/batch isolation are verified.
  Lazy-login uses the same conditional transition. The default-off rollout
  remains intentional until environment/data validation and owner release
  authority; this entry is not resolved by tests alone.

### BUG-008: Startup seeding always performs a full JSON sync — resolved 2026-10-30
- **Symptom**: Application startup still reads and synchronizes
  `wwwroot/data/system-vocabulary.json` on every boot, increasing startup cost
  and mutating the database during normal app launch.
- **Root Cause**: `Program.cs` always calls `JsonVocabularySeeder.SeedFromJsonAsync(...)`;
  the seeder is now upsert-based, but it still scans and saves each startup.
- **When it triggers**: Every application startup.
- **Recommended fix**: Gate seeding by environment/flag, or add a cheap
  change-detection guard before the expensive sync path.
- **Status**: Resolved by the explicit `Database:SeedOnStartup` gate (default false).
  Local restore keeps seeding off in User Secrets; release migration uses the
  design-time factory and does not start the web host or seeders.

---

## Technical Debt

### TD-001: HomeController split is complete, but the boundary must stay enforced [LOW]
- **Files**: `Controllers/HomeController.cs`,
  `Controllers/ClassController.cs`, `Controllers/FolderController.cs`,
  `Controllers/SetController.cs`, `Controllers/StudyController.cs`,
  `Controllers/ChatController.cs`
- **Issue**: The main split is done and `HomeController` is down to dashboard
  and public pages, but legacy `/Home/*` routes still make it easy to drift new
  feature work back into the wrong controller.
- **Impact**: Future maintenance will regress quickly if new domain logic is
  added back into `HomeController`.
- **Recommended fix**: Keep all new class/folder/set/study/chat behavior in the
  dedicated feature controllers and services only.
- **Effort**: Small.

### TD-002: Service extraction is still partial [MEDIUM]
- **Files**: `Controllers/AccountController.cs`,
  `Controllers/FolderController.cs`, `Controllers/SetController.cs`,
  `Controllers/SpeakingController.cs`,
  `Areas/Admin/Controllers/UserManagementController.cs`
- **Issue**: `IClassService`, `IStudyService`, `IStreakService`, and
  `IFileStorageService` are now in place, but several controllers still own
  validation, EF orchestration, and branching business rules directly.
- **Impact**: These areas are harder to unit-test and remain the next likely
  regression hotspots.
- **Recommended fix**: Extract the next bounded services around
  account/profile/security, folder/set mutations, and admin user-management
  workflows.
- **Effort**: Medium.

### TD-003: AccountController is still a large mixed-responsibility controller [MEDIUM]
- **Files**: `Controllers/AccountController.cs`,
  `Views/Account/Auth.cshtml`, `Views/Account/ForgotPasswordConfirmation.cshtml`
- **Issue**: Auth, social login, profile, settings, password reset, and account
  deletion all live in one controller, with a mix of typed models and
  `ViewBag`-driven UI messages.
- **Impact**: High churn, weak compile-time safety for some auth screens, and
  harder regression testing.
- **Recommended fix**: Split profile/security/account-lifecycle concerns into
  smaller services or slices, and finish the move to typed view models.
- **Effort**: Medium.

### TD-004: Goals rollout contracts can drift if dashboard/challenge wiring and docs/tests diverge [LOW]
- **Files**: `Views/Home/Index.cshtml`, `Controllers/HomeController.cs`,
  `Services/GoalsService.cs`, `docs/architecture-prioritized-backlog*.md`,
  `.github/copilot-instructions.md`, `AGENTS.md`,
  `TCTEnglish.Tests/GoalsPhase*.cs`
- **Issue**: The Goals rollout is live across dashboard/challenge and broader
  goals flows, but product/docs/test contracts can drift if feature changes
  bypass `IGoalsService` or update active/deferred areas without synchronized
  documentation and phased regression coverage.
- **Impact**: Product inconsistency, missing activity/XP telemetry, and weaker
  guidance for future changes.
- **Recommended fix**: Keep dashboard values model-driven, ensure daily
  challenge writes activity through `IGoalsService.RecordActivityAsync(...)`,
  and keep docs plus phased goals tests aligned with the live reward contract.
- **Effort**: Small ongoing.

### TD-005: Secrets remain in source-controlled appsettings [SECURITY]
- **Files**: `appsettings.json`
- **Issue**: OAuth credentials, SMTP credentials, and connection-string secrets
  are still committed in the default config file.
- **Impact**: Serious exposure risk if the repository is copied, published, or
  shared outside the intended environment.
- **Recommended fix**: Move secrets to User Secrets for local development and
  environment variables / secret store for deployed environments.
- **Effort**: Small, but coordinated.

### TD-006: Pagination is still missing in some user-facing queries [MEDIUM]
- **Files**: `Controllers/HomeController.cs`, `Services/ClassService.cs`
- **Issue**: Admin user management is paginated, but public search results and
  class discovery/search flows still materialize full result sets.
- **Impact**: Performance risk as the dataset grows.
- **Recommended fix**: Introduce paging parameters plus `.Skip()` / `.Take()`
  on search-heavy views and endpoints.
- **Effort**: Small per screen.

### TD-007: Database index review still has not been codified [MEDIUM]
- **Columns to review**: `LearningProgress.UserId`, `LearningProgress.CardId`,
  `ClassMember.ClassId`, `ClassMember.UserId`, `SpeakingVideo.Level`,
  `SpeakingVideo.Topic`
- **Issue**: The refactor improved query shape, but high-traffic lookup columns
  still need an explicit index review.
- **Impact**: Latency risk on larger datasets and production filtering.
- **Recommended fix**: Add a focused migration after validating the current SQL
  Server execution plans.
- **Effort**: Small.

### TD-008: Structured logging is only partially normalized [LOW]
- **Files**: Critical flows already use `ILogger<T>`, but not every controller
  and service emits equally rich structured events.
- **Issue**: Logging baseline now exists in key controllers/services, yet some
  secondary flows still have thinner diagnostics.
- **Impact**: Debugging production incidents outside the covered hotspots will
  still be slower than necessary.
- **Recommended fix**: Continue standardizing structured logs as the remaining
  controllers are service-extracted.
- **Effort**: Small ongoing.

### TD-010: Goals migration rollout checklist is not documented strongly enough [MEDIUM]
- **Files**: `Migrations/*Goals*`, `Models/DbflashcardContext.cs`, `Services/GoalsService.cs`
- **Issue**: Goals schema migrations are additive and safe by design, but there is
  no explicit deployment checklist for backup/apply/verify order on real SQL
  Server environments.
- **Impact**: Increased release risk when applying goals migrations at scale
  without a repeatable operator runbook.
- **Recommended fix**: Add and follow a rollout checklist: database backup,
  migration apply order, post-deploy query verification, and seed/idempotency
  validation for badge records.
- **Effort**: Small.

---

## Architecture Warnings

### NEVER do these in this codebase

1. Modify `Program.cs`, `appsettings.json`, or any `.csproj` without an explicit request.
2. Generate migrations with `DropTable` or `DropColumn` without approval and a rollback plan.
3. Reintroduce inline `ClaimTypes.NameIdentifier` parsing into controllers.
4. Use `.Result` or `.Wait()` on async paths.
5. Pass EF entities directly to Razor views.
6. Bypass ownership/membership checks on any parameterized read or mutation.
7. Replace typed view models with `ViewBag` on feature pages.

### Be careful with these areas

1. `AutoUnlockWorker` is config-gated; enabling it needs data/config validation.
2. `JsonVocabularySeeder` currently mutates data on startup; do not make it more expensive accidentally.
3. SignalR class access rules must stay aligned with HTTP class-detail rules.
4. File uploads should continue to go through `IFileStorageService` plus `ImageUploadPolicies`.
5. Legacy `/Home/*` routes are preserved intentionally; route changes can break bookmarks and tests.

---

## Recently Resolved / Verified

Pre-merge security04/11: pending browser bundle upgraded DOMPurify3.2.6 to
3.4.17 with immutable upstream source/hash/license and portable vendor bytes.
Manifest/Razor-path regressions and Chromium production chat hydration pass.
Known affected dependency removed; no claim of comprehensive XSS assurance.
Current pre-merge evidence:
[full diff audit](../../docs/feature-audits/2026-11-04-full-diff-review-and-merge.md).

Local storage04/11: large shared caches and project temp moved to D with C→D
junctions and supported user environment preferences; C gained~11.8GiB free.
**Host recovery verified, updated later04/11:** owner re-entered a working key;
metadata200 for both models and one tiny text200/completed/OK were verified.
Ordinary launcher then attempted to replace User Secrets and failed EFS, leaving
the active file absent. Local fix now removes startup credential writes, uses
child env for DB/worker flags and guards explicit key replacement with backup.
Owner initially deferred, then re-entered the key and explicitly requested a
restart. The corrected launcher now runs on5100 with text/speech configured
from User Secrets AI:ApiKey, ready200. Actual secret-file length/mtime/protection
attributes remained unchanged through startup. No new paid probe was run after
this restart: configured is not current account/quota/ASR quality verification.
Global fallback presence alone is also not proof of a valid key. Do not reuse the protected D copy as
active store or weaken file protection. See
[storage audit](../../docs/feature-audits/2026-11-04-local-storage-on-d.md).
Follow-up: [local key startup audit](../../docs/feature-audits/2026-11-04-local-key-startup-safety.md).

UI/logic closeout03/11: local Plan1/2 implemented and verified, including
MariaDB quota/migration/concurrency, SQL Writing/Admin pagination, Speaking
dynamic-card safety/actions/counts, AI unknown-outcome handling, SMTP/worker
ordering, awarded-only Profile badges and lazy launcher usage.
Evidence and explicit external gates:
[closeout](../../docs/feature-audits/2026-11-03-plan1-plan2-local-closeout.md).
This does not resolve whole-controller extraction debt, production release,
real-microphone quality or clean-PC acceptance.

Team local bootstrap (01/11/2026): a fresh Docker DB now gets sample accounts and
learning fixtures through an explicit Development-only CLI; existing users/data
and credentials are preserved. No real backup/keys/volumes are shipped in Git.
Prerequisites/onboarding and cold MariaDB/login evidence:
[team rollout](../../docs/feature-audits/2026-11-01-team-local-rollout.md).

Plan2 (01/11/2026): runtime text provider OpenAI `gpt-6-luna`/low. **Superseded for
ASR (03/11/2026):** active Whisper retired; Speaking/import use OpenAI
`gpt-transcribe` with measured PCM windows (`openai-audio-vad`). Review-repair
R1–R7 (overlap normalize, shared live budget, credentials/IOException/admission/
status/Admin copy) closed offline + fake-provider browser; paid live still
owner opt-in. Reviewer04/11 additionally hardened verified WAV budget,
launcher missing-vs-empty key handling, status config/privacy and persisted
completion/replay assertions. Full897/0/12 and script/JS checks verified;
no new reviewer browser/mic/live run. See
[rereview](../../docs/feature-audits/2026-11-04-openai-speech-rereview.md) and
[speech replacement ledger](../../docs/feature-audits/2026-11-03-openai-speech-replacement-ledger.md).
Earlier OpenAI text migration evidence remains in
[migration ledger](../../docs/feature-audits/2026-11-01-openai-provider-migration-ledger.md).
Paid live speech / real-mic acceptance and production release remain owner gates.

Whisper retirement follow-up04/11: unused loopback test helper removed; legacy
prompts superseded; startup fixture traps retired engines/downloads/processes.
The old .appdata/local/speech directory is absent. Follow-up uninstall04/11:
43engine/model/archive files109780875bytes were deleted directly by the owner
after tool policy rejection, and the unused Whisper Docker image was removed.
No installation binaries/models/archives remain in retired-speech-2026-11-04.
Six audio/log evidence files remain unchanged; two stale state/lock metadata files
1426bytes remain after a separate policy rejection. They are never read/started.
See [uninstall audit](../../docs/feature-audits/2026-11-04-whisper-uninstall.md) and
[retirement audit](../../docs/feature-audits/2026-11-04-whisper-runtime-retirement.md).

Plan2 follow-up (01/11/2026): owner-approved OPENAI_API_KEY now works through the
local launcher when app env/User Secrets have no key. Key source is reported
without its value; the mapping is child-process-only and cleaned up on failure
or exit. Occupied ports fail before build so an older web is not reported as
using new API settings. All four AI clients follow the shared launcher rule.

| Item | Current state |
|------|---------------|
| Speaking detail include issue | Fixed in `SpeakingController.Practice` |
| SignalR method naming mismatch | Server/client names now match (`SendMessage`, `ReceiveMessage`, `JoinClass`) |
| Class detail private graph leak | Fixed through sanitized outsider view + service/controller split |
| Folder/set split-controller anti-IDOR gaps | Fixed and covered by `FolderSetIdorRegressionTests` |
| Dashboard SQLite random ordering failure | Fixed with provider-safe fallback logic |
| Duplicate `ViewModel/` vs `ViewModels/` folders | Normalized to `ViewModels/` only |
| BUG-006 Goals placeholder page | Resolved with real `GoalsController` + `IGoalsService` read/write flow and integration tests |
| Goals business-date badge/streak alignment | Resolved with business-date normalization plus service-level learning activity orchestration |
| Goals phase-5 documentation closeout | Resolved by removing stale placeholder descriptions from backlog/instruction docs and aligning known-issues with real Goals state |
| Goals phase-7 closure and reward hardening | Resolved with atomic speaking/writing completion transitions, removal of request-time writing migration, new replay regression coverage, and an explicit rollout close decision |
| Daily challenge trusted client `correctCardId` | Resolved by server-signed challenge token validation in `HomeController.CheckAnswer` |
| CI/CD test suite 14 failures (billing, speaking, DI, writing, AI host) | Resolved across 5 root cause groups; full suite verified green at 449/449 (2026-04-30) |
