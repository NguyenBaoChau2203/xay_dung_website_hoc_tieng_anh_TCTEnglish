# Contact backend contract for step 02

Implemented locally on 2026-11-16. Branch: `fix/writing-goals-profile-polish`.
Baseline HEAD: `2a20c6185c38b9f975ddf555b7dc73147e2bf1ca`.
This describes implemented symbols and behavior, not a proposed API.
Verification details and remaining acceptance dependencies are in
[01-backend-ledger.md](01-backend-ledger.md).

Final verification: 42/42 Contact regressions passed in the Release suite;
focused mail/account/notification checks had 124 passed and 2 opt-in skips.
The full suite had 1670 passed, 2 AI/SignalR cancellation failures, and 53 skips.
Both failures passed isolated reruns on current and baseline sources; their
full-suite cause remains unresolved. Do not describe the full suite as green.

Step-03 update (2026-11-17): 43 Contact regressions, 11 Node checks and real Chrome
viewport/keyboard evidence now cover the Clay UI integration. Client validation
explicitly cancels invalid POST; quick login uses Account/Login. Live-form smoke
returned 503/Unavailable before SMTP because local sender/password are absent:
zero dispatches at that time. This was superseded by the owner's subsequent
explicit SMTP configuration operation: sender/password are saved locally, one
real Contact form submission returned Accepted, and the owner confirmed the
matching marker in Gmail Inbox. No retry was sent. See
[stage03 ledger](03-integration-smtp-ledger.md) for current attempt accounting.

## Authority and integration boundary

Step 01 uses fake SMTP only. Step 02 owns the Contact Claymorphism interface,
responsive/keyboard/browser checks, and integration with this contract. Continue
on the same branch as one agent, preserving existing work and the owner's
protected-file rules. No live mail, Git publication or deployment is authorized
by this document. Use the existing step-02 prompt and shared design standard.

Public Contact remains in `HomeController`. There is no JSON endpoint, ticket
database, auto-reply, attachment, delivery polling or persistent form draft.
No Program.cs, configuration, project, entity or DbContext change was needed.

## Routes and form fields

Both actions remain `[AllowAnonymous]`, including for locked-out visitors.

| Request | Behavior |
| --- | --- |
| `GET /Home/Contact?subject=...` | 200; no SMTP; trims/lowercases known subjects, otherwise selects `general`; issues a fresh Contact nonce; no-store |
| `POST /Home/Contact` | Server-rendered form POST with antiforgery validation and no-store; field validation precedes SMTP |
| Accepted POST | 302 to `/Home/Contact?subject=<canonical-key>`; GET clears completed fields and issues a new nonce |

The existing account appeal links in `Views/Account/Auth.cshtml` still use
`subject=khieu-nai-khoa-tai-khoan`. Preserve them and anonymous access.

Model: `TCTVocabulary.ViewModels.ContactFormViewModel` (existing legacy namespace).

| Field | Server contract |
| --- | --- |
| `Name` | Required; trimmed; at most 100 UTF-16 code units; CR/LF in the submitted value is rejected, including at the edges |
| `Email` | Required; trimmed; at most 254 UTF-16 code units; exactly one bare email address, parsed with EmailAddress validation and MailAddress; no display name, list, or CR/LF |
| `Subject` | Required POST field; trimmed/lowercased; at most 50; must match one of the five keys below; CR/LF is rejected; an unknown POST value is never silently changed to another category |
| `Message` | Required after trimming; at most 2000 UTF-16 code units; Unicode, Vietnamese diacritics, punctuation, HTML-looking text and internal paragraphs are retained as text |
| `ContactNonce` | Required hidden field; at most 2048; server-issued/protected, bound to the connection-client partition and issuance time; do not create it in JavaScript |
| `__RequestVerificationToken` | Existing MVC antiforgery hidden field, with its corresponding cookie |

Subject keys/labels:

| Key | Label |
| --- | --- |
| `general` | Câu hỏi chung |
| `khieu-nai-khoa-tai-khoan` | Khiếu nại tài khoản |
| `bug` | Báo lỗi / Bug |
| `feedback` | Góp ý cải tiến |
| `other` | Khác |

The setters normalize before MVC validation. The controller reconciles ModelState
attempted values with these normalized values. GET also removes the query-bound
Subject ModelState entry so unknown/missing/blank subjects really display general.

POST has endpoint-specific limits: 32768-byte request body, 16 form values,
64-character form keys, 24576 form-value parser limit, 32768 multipart body limit.
The value limit accommodates a percent-encoded 2000-character Vietnamese message.
The controller additionally rejects a User-Agent over 2048 characters or aggregate
header names/values over 16384 characters before dispatch. Existing server/proxy
limits still apply; no global hosting settings were changed. Excessive form-parser
input and antiforgery failure can return 400 before the action with no preserved
model; Kestrel/body-limit rejection can return 413. Do not promise preserved text
for a request that the server never parsed.

## Response states and retry behavior

`State` is `ContactFormState`, marked `[BindNever]`. `StatusMessage` is a read-only
Vietnamese message derived from State. Render it encoded. Never accept state or
delivery claims from submitted fields.

| Service result (`ContactSubmissionStatus`) | HTTP / model state | Form and operation behavior |
| --- | --- | --- |
| Initial GET | 200 / `Ready` | Empty form, chosen category, fresh nonce |
| MVC input validation fails | 200 / `Invalid` | Trimmed fields and field errors remain; fresh nonce; zero SMTP calls |
| `Accepted` | 302 PRG, then GET | Static success text in `TempData["ContactSuccessMessage"]`; no form PII in TempData; completed form clears |
| `ConfigurationUnavailable` | 503 / `Unavailable` | Fields remain; fresh nonce for a deliberate later attempt; no SMTP call |
| `Failed` | 503 / `Failed` | Fields remain; fresh nonce; deliberate retry permitted, never automatic |
| `Unknown` | 202 / `Unknown` | Fields and original nonce remain; explain possible acceptance and avoid immediate resend |
| `Throttled` | 429 / `Throttled` | Fields/nonce remain; wait, then deliberately retry; no SMTP call |
| `Duplicate` | 409 / `Duplicate` | Fields/nonce remain; already handled or in flight; do not dispatch again |
| `InvalidOperation` | 400 / `Expired` | Tampered, expired, future or wrong-client nonce; fields remain in a fresh form; zero SMTP calls |
| `InvalidInput` from the service safety check | 400 / `Invalid` | No SMTP call; normal HTTP binding is already checked earlier |
| Excessive action-level headers | 400 / `Invalid` | Parsed fields remain with a global error; zero SMTP calls |

`Accepted` means the SMTP transport returned acceptance, not Gmail inbox arrival,
spam placement, an admin reading it, or a response-time guarantee. `Unknown` is
not a queue and there is no automatic background retry. It can result from a
deadline, caller/request cancellation after dispatch, connection loss, or an
exception without a definite SMTP receipt. Explicit SMTP rejection status
400–599 is Failed. Cancellation observed before dispatch is Failed with zero
transport calls. There are no automatic SMTP retries in either case.

The old `ContactErrorMessage` TempData contract is retired. Current error states
come from the returned model. Use the HTTP status together with the rendered
state, rather than treating every 2xx response as known delivery.

## Required retained markup and browser integration

Keep `contactForm`, `contactSubmitBtn`, `Name`, `Email`, `Subject`, `Message` and
the existing loading spans `.btn-send-default` / `.btn-send-loading` unless their
actual consumers and regressions are updated together. The existing
`wwwroot/js/contact.js` still handles loading and pageshow; step 01 does not
certify its native validity, keyboard or back-navigation behavior.

Keep `@Html.AntiForgeryToken()` and `<input asp-for="ContactNonce" type="hidden" />`.
Keep field validation spans and a global validation summary. Current state alert:
`#contactStatus`, `role="alert"`, `data-contact-state="<State>"`; it renders when
StatusMessage is non-null. Success uses the static TempData message above.
Step 02 may restyle these using the shared design system while retaining the
state information and form bindings.

Do not replace Unknown/Duplicate with a success/failure guess, silently obtain a
new nonce to resend, or put form PII into query strings, local/session storage,
logs or TempData cookies. A user can deliberately open a new form, but that is
a new operation and can duplicate an earlier unknown delivery. Keep that limit
visible when designing any restart action.

## SMTP routing, encoding and compatibility

`IAppEmailSender` remains the registered singleton. `SmtpAppEmailSender` owns a
`ContactOperationGate`, using the already-registered IDataProtectionProvider;
no scoped dependency is captured. New methods:

```csharp
string IssueContactNonce(string clientPartition);
Task<ContactSubmissionStatus> SubmitContactAsync(
    ContactFormViewModel model, string clientPartition, string? userAgent,
    CancellationToken cancellationToken = default);
```

The interface has fail-closed defaults for legacy test doubles. Production DI
resolution is tested. The old bool `SendContactMessageAsync` is retained for
compatibility: true only for Accepted, false for all other outcomes. It is not
the public controller path and does not itself admit/replay-protect operations.
Other mail callers keep their existing signatures and cancellation behavior.

To uses one validated configured `SmtpSettings:SupportEmail`. Missing/whitespace
SupportEmail falls back to SenderEmail. Malformed nonempty SupportEmail fails
closed; it does not silently fall back. SenderEmail must be one bare mailbox and
is both the configured From address and SMTP username. Reply-To is exactly the
validated visitor address. Posted To/Cc/Bcc/From/SMTP options are ignored.

Missing/invalid host, sender, password, display-name headers or port makes Contact
unavailable. Host must parse as a host name/IP without whitespace, at most 253
characters. SenderName has a 200-character limit and no CR/LF. Missing port uses
587; a nonempty malformed port or one outside 1–65535 is unavailable. No setting
value or credential is exposed to the view or rewritten.

The existing `ISmtpMailTransport`/`SmtpClientMailTransport` and shared
`TctEmailRenderer.RenderSupportContact` are reused. The existing deadline defaults
to 30 seconds and clamps positive configured values to 1–60 seconds. It links
caller cancellation and RequestAborted. The .NET transport honors cancellation;
the gate never evicts an active send if a replacement transport fails to finish.

HTML/attribute data is encoded. The Contact preheader is now encoded exactly
once by RenderShell; CR, LF and CRLF message separators render as readable line
breaks. Diagnostics in mail are bounded to IP 64 / User-Agent 512 characters.
Application Contact logs contain operation/category/timing or redacted config
presence, never visitor address, message, raw IP or exception payloads.

## Abuse gate and release constraints

- Nonce lifetime: 15 minutes, with issuance time validated before dispatch.
- All content variants using the same admitted nonce are blocked, including
  concurrent, accepted, unknown and known-failure replays. A failed retry is a
  deliberately new operation using the newly rendered nonce.
- At most 3 admitted operations per connection-client partition per fixed
  10-minute window. Configuration failures and SMTP rejections also count.
- Client identity comes from `Connection.RemoteIpAddress`, normalized to IPv6,
  then HMAC-hashed with a per-singleton random key. No raw IP or form content is
  cached. Missing connection identity uses a conservative `unavailable` bucket.
  Account-security's anonymous user ID=0 policy is not used. Contact does not
  directly trust X-Forwarded-For or change proxy settings.
- Capacity: 4096 operation entries and 2048 client-window entries. Capacity full
  returns Throttled. GET issuance allocates no metadata entries. Finished
  operation entries are retained through nonce expiry plus 60 seconds and then
  pruned on admission; client windows expire after 10 minutes. Pending entries
  are never evicted to admit another send.
- Scope is one singleton in one process, with one operation nonce. New nonces,
  IP rotation, multiple processes and restarts are not distributed deduplication.
  NAT or an unconfigured reverse proxy can cause clients to share a partition.
  Production worker/proxy topology was not observed in step 01. A single-worker,
  correctly attributed client topology is a release constraint if relying on
  these limits. Multiple workers need a separately reviewed shared admission/
  replay design and any necessary protected-config/persistence authorization.

## Public contact information and remaining shared dependency

No owner-verified public inbox, telephone, address, business hours or response-time
promise was established. The page-specific Contact content therefore offers the
form only. Neither SenderEmail nor SupportEmail is published; the step-03 test
destination is not a public channel. Do not invent replacement contact details.

The shared `_Layout.cshtml` footer, lines 389–408 at this baseline, still contains
an unverified address, email, placeholder telephone and domain. Step 01 deliberately
does not modify the protected shared layout. Consequently the *whole rendered
page* still inherits that footer. Smallest future proposal: remove unverified
footer contact rows in the shared layout, preserve navigation/IDs, list all
layout consumers, and compare shared pages before/after under explicit shared-UI
authority. Do not hide the footer with Contact CSS to simulate fixing it.

Native browser rendering, responsive widths, zoom, keyboard, actual reverse-proxy
limits, live SMTP and Gmail arrival remain acceptance work for later steps.
