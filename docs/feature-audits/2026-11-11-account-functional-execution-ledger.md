# Account functional completion — execution and review ledger

Date: 2026-11-11. Branch `fix/staging-uat-polish`, baseline HEAD `c908f665cff1a5b9b65a05e9009de24e571cde7a`.
Pre-existing dirty UI (Account views, layouts, loader, account CSS/JS, galleries, bug log) was left in place and not attributed to this change.

## Decisions

- Public code is `TCT-` plus invariant UserId, minimum 8 digits. No new column.
- Email change stores a one-use digest and does not replace the login email until POST confirm. SMTP missing returns a failure.
- `SOCIAL_LOGIN_` is not a password bypass. First local password needs an emailed proof.
- Cookie claim `tct_secver` is checked in cookie validation and every hub invocation. Missing/stale versions or a changed role are rejected. Test authentication now supplies version0 explicitly; there is no missing-version bypass.
- New activity time is stored only in `LearningActivitySessions` / `LearningActivitySegments`. Legacy `UserFeatureTimeLogs` stay readable and are not rewritten.
- Vocabulary WriteMode maps to Vocabulary, not Writing. Speaking practice and WritingPractice now load the tracker.
- Notification preferences default to opt-out. 20:00 is only the unsaved suggestion.
- Push and weekly email use `OutboundDeliveries` with a lease. Timeout is `Unknown` and is not retried blindly.
- Payment orders stay. Their user FK is now Restrict. Users with payment or admin-audit rows are anonymized instead of removed.
- Owned content that other people still use blocks deletion.

## Schema

Migration `20261111114120_AddAccountFunctionalCompletion` adds nonnullable integer SecurityVersion with default0, nullable EmailVerifiedAt/AnonymizedAt, plus challenge, activity, goal-revision, preference, push, delivery and file-cleanup tables. Existing goals receive a revision1 baseline at rollout time; earlier history remains unknown. Up changes the payment-order user FK to Restrict. Down drops the new tables and restores the prior cascade FK; no Down was executed. No old learning counts are rewritten. The initially unapplied migration was corrected before first application and was not edited after application.

`dotnet ef migrations has-pending-model-changes` reported no pending changes.

## Initial Cursor checks (superseded by review evidence below)

- `dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release` succeeded.
- Account functional tests: 10 passed (code, profile HTML, social-password refusal, email unchanged without SMTP, 17/20 = 85%, tracker retry and second tab, push URL rejection, delivery lease and opt-out, delete without confirm, synthetic delete).
- Account UI render and account correctness tests passed in the same session before the last delete assertion (27 passed, 0 failed).
- `dotnet list TCTEnglish/TCTEnglish.csproj package --vulnerable --include-transitive` was started; see the handoff if the command completed.
- `TCT_ACCEPTANCE_MARIADB` was not set. Disposable MariaDB upgrade/repeat was not run and is not a provider pass.
- Browser 1440/768/390 and 200% zoom were not observed. The local web host was not started, because the dev database has not applied this migration.

## Provider gap

Web Push needs `Notifications:Outbound:VapidPublicKey`, `VapidPrivateKey` and `VapidSubject` from the existing configuration system. Weekly email needs the existing SMTP settings and a verified address. Neither was exercised against a live provider. No secret was written.

## Owner-authorized review and repairs

The owner explicitly requested direct fixes after Cursor execution. One agent
reviewed and repaired locally; no commit, push, CI or deployment was performed.
The initial73 changed/untracked files were fingerprinted in ignored local evidence.
Existing Gemini UI/layout/loader work and unrelated BTH3 documents were preserved;
the aggregate Git diff is not attributable to this review alone.

| Priority | Repaired finding | Evidence |
| --- | --- | --- |
| P1 | Reusable proofs, stale password/user state, missing existing-email verification and stale hub/admin sessions | Atomic-consumption, stale-user, version/role, fake email roundtrip and injected transaction-retry regressions |
| P1 | Instant/parallel heartbeat time inflation and hidden/pending/idle client races | Zero-time test; two-tab MariaDB concurrency; production-script lifecycle tests |
| P1 | Foreign push endpoint takeover and account-switch notification disclosure | Ownership/key tests; push client tests; service-worker account/origin guards |
| P1 | Stranded delivery leases, stale settlement, disabled/finished/expired reminders and ambiguous SMTP receipts | Lease/eligibility/fake-transport tests and SMTP connection-loss regression |
| P1 | MySQL retry strategy rejected deletion; foreign learner progress could be removed | Native retry/delete/last-admin/tombstone cases; Speaking/Writing integration cases preserve outsiders |
| P2 | Goal baseline/revision races, habit/email interval boundaries, 28-day selector/badge | Native revision concurrency; hour/week fixtures; fake weekly summary and Profile checks |
| P2 | Avatar replacement raced anonymization; cleanup exhaustion falsely reported success | Staging/deletion interleaving and cleanup-failure regressions |
| P2 | Unverified email shown as verified; failed email input discarded; missing proof/unregister UI | Render regressions and actual Profile/Settings flows |
| P2 | Register submit handled twice and silently cancelled; hidden mobile aside occupied normal flow | Actual-script regression, browser register/login and scoped CSS repair |

Account mutations serialize on the existing user row inside a retryable transaction.
Email-change reauthentication and challenge issuance share that lock; mail is sent
after commit. Retried security operations load fresh tracked state. Password/email/
admin security changes revoke reset tokens and all outstanding challenges.
`SOCIAL_LOGIN_` never proves a local password or linked OAuth identity.

Accepted activity uses server wall time, CSRF, sequences and per-user serialization;
it grants no XP. Hidden/idle/unload and late-start paths are covered by production
script tests. Habit aggregates clip/merge/split intervals and retain unknown-history
states. Today and pre-baseline goal dates are excluded;17/20=85% remains verified.

Push validates subscription encryption keys and matching VAPID keys. An endpoint
cannot move to another account. DNS is checked once and the actual connection uses
that IP, with redirects/proxies disabled. The service worker checks the authenticated
account before display. Notification clicks remain same-origin.

Deliveries use random per-claim lease tokens and conditional settlement. Expired
leases and ambiguous SMTP/Push outcomes become Unknown without blind resend.
Dispatch rechecks opt-out, daily enablement, goals, account state and expiry;
exceptions are isolated per delivery. Weekly summaries use the stored period and
clipped, merged accepted intervals, with an absolute settings/unsubscribe link.

Deletion checks other users' content dependencies and the last active admin. It
preserves financial references through a minimal tombstone with personal/learning
fields removed; sole-owned private content and personal rows are removed in one
transaction. Cleanup retries and marks exhaustion Failed. No data reset occurred.

Several new regressions failed on the faulty code before fixes: proof reuse,
zero-time credit, endpoint takeover, expired leases, SMTP connection loss, client
lifecycle/permissions, failed-form preservation, week clipping, avatar/deletion
interleaving and security transaction retry. Existing deletion tests were corrected
to assert the owner-approved data policy rather than deleting outsiders' progress.

## MariaDB and local data evidence

Disposable MariaDB10.5.23, isolated sentinel-protected database and container
`tctenglish-mariadb-acceptance-accountreview-20261111` on loopback3359:

1. Upgraded from `20261107043157_AddAiMessageGroundingMetadata` with a synthetic
   existing user/goal. Both survived; the goal received an honest rollout revision.
2. Repeated migration applied nothing; final pending-model check found no changes.
3. Six Account native cases plus three existing notification-worker cases:
   **9 passed,0 failed**. Includes concurrent tabs, one-use proof, concurrent goal
   revisions, retry-enabled deletion, simultaneous admin deletion, financial
   tombstone retention and worker claim/recovery.

The owner's local development DB was backed up to ignored local evidence before
the reviewed migration was applied through the normal launcher. Existing data/users
were preserved. Credentials, SQL backup, screenshots/logs and TRX remain under ignored
`.appdata/local/account-review-20261111` and must not enter Git. No shared content or
personal progress was exported to a package; no production data was copied.

## Review verification

| Check | Observed result |
| --- | --- |
| Initial focused Account baseline |29 passed |
| Release app/test build | Succeeded; existing repository warnings remain |
| Focused Account review/functional/correctness/render suite |52 passed; final fake-email roundtrip and retry regression separately both passed |
| Full .NET before final security follow-up |1,244 passed,0 failed,28 environment-dependent skips |
| Final full .NET including Admin retry repair |1,247 passed,0 failed,28 environment-dependent skips; total1,275;4m21s; final TRX retained locally |
| Final Admin/account focused follow-up |42 passed,0 failed; injected retry first reproduced false success without persisted role, then passed after repair |
| Full Node suite after final JS/CSS changes |182 passed,0 failed |
| Native MariaDB Account + worker filter |9 passed,0 failed |
| Final pending model/repeat migration | No model changes; DB already up to date |
| Dependency audit | Completed; two existing moderate transitive advisories below |
| Local web | scripts/start-local.ps1 -NoBrowser; /health/ready200 |
| Speech metadata | Configured via saved local User Secrets; VerifiedLive=false; no paid request |

The full-suite skips are not provider passes; native results are a separate run.
Detailed local commands/results are retained in ignored logs/TRX.

Chrome isolated context, normal routes and synthetic local account:

- Registered/logged in, saved/reloaded name, and kept the same `TCT-00000070` code.
- Submitted an actual multipart PNG from a safe existing same-origin repository
  image. A generated avatar URL loaded. Browser MCP denied native filesystem paths,
  so the fixture was supplied to the file input through the page; no controller,
  storage or upload mock was used. Screenshots were inspected inline.
- Switched7/28-day views with honest empty history. Saved/reloaded19:25UTC reminder
  preferences while Push/weekly email remained off; no real delivery occurred.
- Changed password; returned fields were blank. Mobile menu opened; Escape closed
  it and restored focus. Synthetic account deletion with CONFIRM/new password
  succeeded, avatar returned404 and protected Settings redirected to Login.
- Inspected1440/768/390px layouts. Scoped offcanvas positioning removed the mobile
  blank gap; no horizontal document overflow at768/390. The 28-day badge now matches
  its data window.

Native browser200% zoom was **not established**: shortcuts under device emulation
did not change measured zoom. A CSS zoom probe is not counted as native acceptance.
This remains a manual UI check. Live OAuth and outbound channels were not exercised.

## Remaining configuration and dependency limits

Full Push needs matching VAPID keys, real subject contact, HTTPS/localhost, browser
permission and device subscription. Weekly email needs SMTP, verified email and
`Notifications:Outbound:PublicBaseUrl`. At initial review, no real provider delivery/receipt was tested; the authorized SMTP follow-up below supersedes that transport-only limit.
Presence of settings is not proof of provider access, inbox/device receipt. See
[outbound operations](../operations/account-outbound-notifications.md).

The audit found pre-existing transitive AngleSharp1.4.0 and MySqlConnector2.4.0
moderate advisories. The new Web Push package was not listed by this audit.
[AngleSharp advisory](https://github.com/advisories/GHSA-pgww-w46g-26qg) describes
parser/sanitizer mutation-XSS; fixed release1.5.0.
[MySqlConnector advisory](https://github.com/advisories/GHSA-473q-m89c-ghf8) concerns
specific remote zero-configuration TLS conditions; loopback local DB is outside
those conditions, fixed release2.6.1. Production exploitability was not evaluated.
Broad dependency/provider upgrades and protected hosting connection changes are
outside the original narrow Account authority. No dependency pin, secret or TLS
setting was changed by this review. Native200% zoom, live Push/weekly-workflow acceptance,
dependency maintenance and any release remain separate outstanding work.

## Owner-authorized live SMTP follow-up — 2026-11-11

The owner supplied a temporary test credential and explicitly named the same Gmail
address as sender/recipient. One self-addressed message was sent through the current
production SmtpAppEmailSender and SmtpClientMailTransport, loaded from the Release
assembly by an ignored one-shot console helper. Gmail SMTP587/STARTTLS returned
EmailDispatchResult.Sent. A read-only exact-marker search in the matching connected
Gmail account confirmed the message has INBOX, UNREAD and SENT labels.

Evidence marker: TCT-SMTP-20261111-8c20ac037e. This proves real SMTP authentication,
transport and inbox receipt for the supplied test identity. It does not prove the
complete scheduled weekly delivery, email verification or Web Push workflow.
The temporary password was supplied through non-echoing console input, never in
argv/source/logs; the helper process exited. No password, sender or public-base-URL
was persisted into the app configuration or User Secrets. The supported local
configuration currently has Gmail host/port but no SMTP sender/password or outbound
PublicBaseUrl/VAPID entries. Existing SMTP code predated Account work; the initial
report meant unverified live delivery, not an unimplemented SMTP service.

Dependency paths were checked in resolved project assets: YoutubeExplode6.5.7 brings
AngleSharp1.4.0; Pomelo.EntityFrameworkCore.MySql9.0.0 brings MySqlConnector2.4.0.
DatabaseSettings defaults to10.5.23-mariadb and uses MySqlConnectionStringBuilder;
Program registers Pomelo via UseMySql. The connector name does not mean the app has
switched from MariaDB to a MySQL server. No direct AngleSharp/HTML-sanitizer calls
were found in application C#/Razor source; that narrows the observed exposure but
is not a complete proof of non-exploitability. No package or production setting was
changed during this explanatory/live-test follow-up.

## Email-design handoff and updated dependency recommendation — 2026-11-11

The owner requested an Antigravity Markdown handoff for all existing email
presentation. See .ai/prompts/antigravity-email-clay-redesign.md and the matching
email-clay-redesign plan. This authoring pass did not change app code or send mail.

Further primary-source verification found a second AngleSharp maintainer advisory,
[GHSA-cgp3-27rh-pcp2](https://github.com/AngleSharp/AngleSharp/security/advisories/GHSA-cgp3-27rh-pcp2),
affecting1.3.1 through1.8.1 and fixed in1.8.2+. The earlier1.5.0 minimum fixes only
the first reported issue and is not the current maintenance recommendation.
Official NuGet metadata confirmed stable AngleSharp1.10.1 and MySqlConnector2.6.2
as the proposed targets on11/11/2026. Their framework assets and Pomelo's stated
MySqlConnector minimum are compatible at metadata level; actual app/provider
behavior has not been verified with those proposed versions. See
docs/superpowers/plans/2026-11-11-dependency-security-maintenance.md for the bounded
upgrade and regression plan. No package pin, project/configuration or database was
changed in this planning pass.
