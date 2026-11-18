# Step 03 — Contact integration and bounded real-mail acceptance

Date: 2026-11-17. One agent; local implementation/review only.
Branch preserved: `fix/writing-goals-profile-polish`.
HEAD remains `2a20c6185c38b9f975ddf555b7dc73147e2bf1ca`.

**Latest status, 2026-11-17:** owner-authorized SMTP settings are now saved in
local User Secrets. **One real SMTP dispatch returned Accepted**, marker
`TCT-CONTACT-20261117-NQTWKFYE`; the owner explicitly confirmed the matching message in **Inbox**.
No automatic retry or second message was sent. Earlier missing-configuration
results below are historical and are superseded by this verified continuation.

## Owner-authorized grouped local commits — 2026-11-17

The owner subsequently authorized committing the completed work in groups on the
current branch. That authorization supersedes the work pack's original no-commit
default for this local stage; push/PR/CI/release authority is still absent.
Grouping: complete application implementation (MVC/Razor share nonce/state
contracts), regression tests, then plans/contracts/acceptance documentation.

Precommit verification against the final hero-based UI: Release .NET focused
suite **125 passed / 0 failed / 2 MariaDB opt-in skips**, Node **11/11 passed**.
Chrome rechecked 1440/1024/768/390/375px with no horizontal overflow and confirmed
the final hero, absent breadcrumb/quick actions, and nonce/form bindings. The
earlier shared-header UI descriptions are historical, not the final composition.
There was no additional real SMTP call. The configured SMTP password and its
grouped-input form were checked against all 28 candidate files with zero matches;
only redacted counts were printed. Runtime logs, credentials and scratch are
ignored and excluded from the commits. The normal local web was preserved.

Local implementation commit: `2ed9d51` (`feat(contact): secure public delivery and
refresh the Clay interface`). Local regression commit: `bbb99aa`
(`test(contact): cover delivery, replay and accessible form behavior`). The plans,
contracts and acceptance records form the following documentation commit. No
commit contains the persisted SMTP credential; that remains in local User Secrets.

## Initial dispatch accounting checkpoint (before credential configuration)

The stage-01 and stage-02 ledgers record no real SMTP attempts. One actual-form
smoke submission is reserved in this stage; no automatic retries are permitted.
The only allowed destination is the owner-authorized Gmail from the work-pack
README. The temporary owned host overrides SupportEmail only in its child
environment; sender identity and the supported secret store are preserved.

Configuration preflight: Host present in the base configuration; SenderEmail and
Password absent from appsettings/Development, the current project User Secrets,
and process/user/machine SMTP environment settings. Only presence was printed;
no secret value was displayed, copied into argv, or rewritten.

Read-only follow-up after the owner recalled prior success: the
[2026-11-11 Account SMTP follow-up](../2026-11-11-account-functional-execution-ledger.md#owner-authorized-live-smtp-follow-up--2026-11-11)
records Sent and an exact-marker Inbox confirmation for
`TCT-SMTP-20261111-8c20ac037e`. It explicitly states that credentials were supplied
temporarily to a one-shot helper through non-echoing input, the helper exited,
and neither sender nor password was persisted to configuration/User Secrets.
This explains prior success alongside the current missing persistent settings.
The current project's supported User Secrets, all current appsettings files,
process/user/machine SMTP variables and launch-profile SMTP entries were rechecked
for presence only; sender/password remain absent. The configuration/launcher/
project files have no worktree diff. No credential recovery, store rewrite or
additional form/SMTP submission was performed during this follow-up.

Reserved form marker: `TCT-CONTACT-20261117-H9XL5W17`.
Reservation made at 2026-11-16 20:02 UTC / 2026-11-17 03:02 Asia/Bangkok.

Final result: one native browser form POST at **2026-11-16 20:03:08 UTC /
2026-11-17 03:03:08 Asia/Bangkok**. The form had real antiforgery/nonce, selected
`general`, synthetic text with Vietnamese and a line break, and the approved
Gmail as Reply-To. No raw submitted message, cookies, nonce or mailbox data is
stored in this ledger.

| Evidence level | Actual result |
| --- | --- |
| Actual form submissions | 1; Chrome network request 149, POST `/Home/Contact` |
| App outcome | HTTP 503 / `Unavailable`; no redirect |
| Browser response wait | About 40 ms; tool click wall time about 273 ms |
| Service operation | `49f95d4ced274490939c518285b01a8a`, `ConfigurationUnavailable`, about 2.95 ms |
| Redacted app preflight | recipientPresent=True, senderPresent=False, hostPresent=True; saved password also absent |
| SMTP transport dispatches | **0**: configuration validation returns before the transport call |
| SMTP acknowledgment | None; not attempted |
| Gmail arrival / appearance | Not tested: no message was dispatched; no mailbox was inspected |
| Duplicate/retry | None; no second click, automatic retry or other-host attempt |

This is a real form/configuration rejection, **not a successful real-mail smoke
test**. The two-attempt work-pack dispatch budget has not been consumed. A future
authorized continuation must check this ledger again and count any uncertain
dispatch as an attempt. Actual delivery remains blocked until SenderEmail and
Password are securely configured in the current application's supported local
secret store. Do not recover an old password from chat, search unrelated private
backups, paste secrets into chat, or weaken EFS/ACLs.

## Current changes and regressions

### Owner-authorized SMTP configuration and live continuation — 2026-11-17

The owner explicitly supplied an app password and authorized local configuration
and testing. The sender is the same approved Gmail identity recorded by the prior
successful Account SMTP test. The password was entered through hidden console
input; no credential was embedded in source, argv, application logs or Git.
Only SenderEmail and Password were added to the current project's User Secrets.

The existing ReplaceFile writer received UnauthorizedAccessException; the original
store was preserved. The supported `dotnet user-secrets set` JSON-stdin interface
then succeeded. A same-directory backup with the original ACL was created before
the write, unrelated values were compared in memory afterward, and the backup
was removed only after successful exact readback. The original file's EFS/ACL
protection was not changed. No tracked configuration or project file was edited.

Reserved live marker: `TCT-CONTACT-20261117-NQTWKFYE`. Prior actual SMTP dispatch count: 0.
This is the first live attempt reserved under the two-attempt work-pack ceiling.
Treat the reservation as potentially dispatched until a definite result is
recorded. No automatic retry or second-host resend after an ambiguous result.

Actual live result:

| Evidence level | Result |
| --- | --- |
| Actual send time | 2026-11-17 06:14:51 UTC / 2026-11-17 13:14:51 Asia/Bangkok |
| Marker | `TCT-CONTACT-20261117-NQTWKFYE` |
| Actual UI path | Chrome isolated context; GET completed Contact, fill synthetic name/approved Reply-To, general subject and Vietnamese two-line message, one send-button click |
| Protection | Real antiforgery and server-issued ContactNonce; native validity passed |
| HTTP | POST `/Home/Contact` 302, then GET category form 200 |
| Production service receipt | Operation `303af90229034412919dd57bfe2ea094`: Accepted, about 6036 ms |
| App result | contactSuccessAlert; name/message cleared; focus on success alert |
| SMTP | One actual dispatch using saved credentials and SmtpClientMailTransport to the approved Gmail; Gmail SMTP host/587 selected in the owned child environment |
| Gmail arrival | Owner explicitly answered “Đã thấy trong Hộp thư đến” for this exact marker |
| Mail appearance | Not independently inspected; no private mailbox or mailbox screenshot was accessed |
| Retry / work-pack count | 1 actual SMTP attempt total; zero retries or automatic resends |

The missing-sender/password blocker is resolved for this local configuration.
This is local acceptance, not CI/staging/production release or multiple-worker
deduplication proof. The owner's ordinary web at port 5100 is preserved; the
temporary test host at port 5127 is stopped after recording the result.

The entire stage-01/02 Contact implementation was reviewed in place; its existing
uncommitted work and unrelated features were preserved. Concrete step-03 repairs:

- `wwwroot/js/contact.js`: native and jQuery-invalid submissions now call
  preventDefault; native validation reports/focuses the invalid control. The
  previous `return` allowed a novalidate form's default browser POST. Reconcile
  stale aria-invalid, announce native required errors only after an attempt,
  update on input, and retain the over-limit message indicator. The loading
  button receives a meaningful accessible name and still restores on pageshow.
- `Views/Home/Contact.cshtml`: quick login linked to nonexistent `/Account/Auth`;
  use `/Account/Login`. Duplicate state no longer promises that support received
  and will answer the mail. Unavailable no longer guesses maintenance. Remove
  unverified 24/7 availability and an absolute safety claim; preserve Clay layout,
  field bindings, subject keys, nonce/CSRF and anonymous appeal access.
- `wwwroot/css/contact.css`: replace the private hex palette with existing shared
  semantic tokens; fix undefined `--tct-leading-normal` to `--tct-leading-base`.
  No shared token, layout, package, config or brand change.
- `scripts/tests/test_contact_ui.cjs`: meaningful regressions for native/jQuery
  prevention, stale ARIA, untouched required fields, and valid shared token use.
  `ContactBackendHttpTests.cs`: verify the real login URL and absence of 24/7 copy
  in the existing Razor/semantics test.

Red evidence before fixes: 3 Node behavior failures; the token check failed on
undefined leading-normal; the HTTP/Razor test failed with `/Account/Auth` versus
`/Account/Login`. A browser review caught premature native aria-invalid; its
dedicated Node regression failed before repair. Logs are ignored under
`.tmp/contact-refresh/03-*-red.log`.

## Build and automated verification

```text
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/
node --test scripts/tests/test_contact_ui.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/ --filter "FullyQualifiedName~ContactBackendHttpTests|FullyQualifiedName~ContactMailAndGateTests|FullyQualifiedName~SmtpTransportFakeTests|FullyQualifiedName~TctEmailDesignSystemTests|FullyQualifiedName~AccountReviewRepairTests|FullyQualifiedName~UnlockWorkerRegressionTests|FullyQualifiedName~AccountFunctionalCompletionTests|FullyQualifiedName~Notification" --logger "console;verbosity=minimal"
```

- First Release build: 0 errors, 42 existing warnings outside Contact. Final
  incremental Release build with the isolated output: 0 errors / 0 warnings.
- Node: **11 passed, 0 failed**.
- Focused .NET: **125 passed, 0 failed, 2 skipped** (MariaDB opt-in Notification
  acceptance). Includes all **43 Contact tests**, existing SMTP cancellation,
  shared email design, account/reset and weekly-notification regressions.
- No full-suite rerun in this UI-focused stage. Stage 01's full-suite result
  remains 1670 passed / 2 AI-SignalR cancellation failures / 53 skipped; isolated
  reruns passed then, but that does not make the full suite green.
- No persisted entity/mapping change: no migration required or generated.
  Standard launcher reported the existing DB already up to date, with no migration
  applied. Shared-data startup catch-up checked 379 releases and performed zero
  record operations; no Contact submission or private data was exported.
- Final diff/whitespace review performed. Unrelated line-ending-only changes from
  email gallery generation are removed; the existing Contact edge fixture remains.

## Actual Chrome acceptance

Owned host: `http://localhost:5127`, started via
`pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5127`
inside an owned child wrapper. No existing listener was stopped. `/health/ready`
returned 200/ready; speech-status reported configured metadata only, not verified
quota/account access. AI BaseUrl was process-limited to a closed loopback address;
no paid AI/speech request was submitted.

Chrome used an isolated `contact-step03` context. HTTP tests cover authenticated
visitors; actual browser acceptance here used an anonymous visitor and verified
the real Account Login page's appeal link. No blocked account was modified.

| Check | Observed evidence |
| --- | --- |
| 1440 × 900 | Document width 1440; Contact grid 340 + 716 px; no out-of-bounds controls; desktop screenshot visually inspected |
| 1024 × 768 | Document width 1024; two 340 px columns; controls within viewport |
| 768 × 1024 | Document width 768; one 736 px column |
| 390 × 844 | Actual CDP mobile viewport width 390, document 390, one 358 px column |
| 375 × 812 | Actual CDP mobile viewport width 375, document 375; controls left 31/right 344; mobile form screenshot inspected |
| Reduced motion | Media query matches reduce; submit-button transition duration 0s |
| Keyboard | Tab from Name to Email; Enter toggles FAQ aria-expanded true to false; native invalid submit focuses Name, keeps button enabled and emits zero POSTs |
| Native form validation | jQuery is absent on the real page; native fallback actually cancels invalid submit; network retained only GET 200 |
| Valid form dry-run | Final test listener prevented network submission; nonce/CSRF present, UTF-16 counter updated, loading reset after prevented default |
| Login / back | `/Account/Login` renders password form; account appeal link points to Contact with the appeal subject; Back restores enabled send button and selected appeal category |
| Actual valid POST | HTTP 503 / Unavailable, marker/name/email/message retained, focus moved to contactStatus, send button enabled |
| Reflow proxy for zoom | 720 × 450 CSS viewport at scale factor 2 has document width 720; alert/form remain available |

Native browser 200% zoom was **not verified**: sending Ctrl-plus through this tool
did not change innerWidth/DPR. The 720-CSS-pixel emulation is reflow evidence, not
proof of native browser zoom. Native screen-reader output, all mail-client
rendering and live Gmail appearance are also unverified. Screenshots were viewed
inline; the MCP rejected saving to the requested workspace path, so no nonexistent
PNG artifact is claimed.

## Remaining constraints and cleanup

- The in-process guard still cannot certify multiple-worker/restart deduplication;
  production worker/proxy topology is not observed or changed.
- Protected shared `_Layout.cshtml` still contains unverified footer contact
  placeholders. This is observed in Chrome and remains outside shared-UI authority;
  the Contact body does not publish the test Gmail or a fabricated support channel.
- The initial secure-configuration blocker is superseded by the explicit owner
  configuration and successful live continuation above: one SMTP Accepted result
  and owner-confirmed Gmail Inbox arrival. Mail-client visual rendering was not
  independently inspected.
- Temporary owned web PID 2196 (confirmed command line localhost:5127) was stopped
  and its isolated Chrome page was closed. Child-only routing/AI overrides ended
  with the owned launcher; no caller/system environment was rewritten. MariaDB
  was left running and its volume preserved. The subsequent owned SMTP test PID
  8616 on port 5127 was also stopped, and its isolated Chrome page was closed.
  The normal web on port 5100 remains available with health 200/ready. The one-shot
  credential-entry helper was deleted; it contained no embedded password.
  Only the owner-authorized SMTP sender/password were persisted to User Secrets;
  all other existing secret values were verified preserved. No tracked appsettings/
  project/Program, deployment gate, production/staging environment, Git branch or
  HEAD was changed. No commit/push/deployment or additional SMTP attempt.
