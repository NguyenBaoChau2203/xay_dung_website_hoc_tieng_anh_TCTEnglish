# Step 01: Contact backend audit and repair

Read [the work-pack contract](README.md) first. Executor: GPT 6.1 Sol High.
Mode when invoked: `IMPLEMENT_LOCAL`, limited to Contact and necessary regressions.
Use `tct-superpowers`, `tct-engineering` and the relevant security procedure.
Load `tct-api-endpoint` only if adding JSON behavior; the preferred contract
remains the existing server-rendered form POST.

## Outcome and source boundaries

The form must accept a valid support request, route it to the configured inbox,
give an honest delivery result, and preserve the user's text when not sent.
Anonymous visitors and locked-out users must remain able to contact support.

Inspect these current files with CodeGraph first; do not reload unrelated features:

- `TCTEnglish/Controllers/HomeController.cs`: only Contact and required constructor wiring.
- `TCTEnglish/ViewModels/ContactFormViewModel.cs` and `Views/Home/Contact.cshtml`.
- `TCTEnglish/Services/IAppEmailSender.cs`, `SmtpAppEmailSender.cs`, `ISmtpMailTransport.cs`.
- `TCTEnglish/Services/Emails/TctEmailRenderer.cs`: Contact methods and shared callers.
- `TCTEnglish.Tests/SmtpTransportFakeTests.cs`, `TctEmailDesignSystemTests.cs` and
  the existing web test factory/HTTP antiforgery helpers as needed.
- `TCTEnglish/wwwroot/js/contact.js`: read the caller contract; leave visual redesign to step 02.

Record HEAD/working-tree baseline and read the newest relevant bug-memory entries
on ambiguous SMTP outcomes and the 2026-11-11 shared email renderer. Prefer small
changes in the existing registered mail boundary and pure typed helpers.
`IAppEmailSender` is currently singleton: do not capture a DbContext or scoped
service inside it. Keep Home thin; public Contact belongs in Home under the
current public-page boundary. Do not create a feature-controller migration.

## Ordered checklist

Execution checkpoint (2026-11-17): the local backend checklist below is implemented
and reviewed; see [the execution ledger](../../../feature-audits/2026-11-16-contact-refresh/01-backend-ledger.md)
and [Gemini contract](../../../feature-audits/2026-11-16-contact-refresh/backend-contract.md).
All 42 Contact regressions pass. Full-suite evidence is partial: 1670 pass,
2 AI/SignalR failures (both pass isolated current/baseline reruns), 53 skips.
Shared footer contact placeholders, browser acceptance, live SMTP and production
worker/proxy topology remain explicitly outside this step's verified acceptance.

- [x] Capture existing GET/POST/redirect/status/form contracts and callers of
  Contact subject links. Inventory the known public-contact fields; distinguish
  configured technical recipients from owner-approved public contact details.
- [x] Add meaningful failing regressions for reproduced behavior before fixing
  it. Preserve existing fake SMTP and account/reset/notification consumers.
- [x] Validate trimmed required inputs on the server: name <=100, one valid
  email <=254, message <=2000 characters, non-whitespace content and bounded
  subject. Preserve Vietnamese diacritics, paragraphs and legitimate punctuation.
  Keep GET unknown/missing subject -> general; reject a tampered unknown POST
  subject rather than silently sending it as another category. Preserve all
  five real subject keys, especially the appeal key. Reconcile ModelState with
  normalized values so the user sees the values actually validated.
- [x] Bound request/form sizes with room for a valid percent-encoded 2000-character
  Vietnamese message. Reject before SMTP; do not accept arbitrary huge headers
  or include an unbounded User-Agent in mail. Do not invent global request settings.
- [x] Keep antiforgery on mutations and anonymous access on the public page.
  Never add an authentication requirement that prevents a blocked account's
  appeal. No SMTP operation on GET, failed validation or failed antiforgery.
- [x] Keep To server-controlled: validated configured SupportEmail, then the
  existing SenderEmail fallback if appropriate; document missing versus malformed
  configuration. Never accept posted To/Cc/Bcc/From/SMTP options. From uses the
  configured authenticated sender; Reply-To contains only the validated visitor
  address. Reject CR/LF header attempts. Encode HTML/attribute data once, preserve
  readable line breaks and reuse the shared Contact template.
- [x] Add a typed Contact result distinguishing accepted-by-SMTP, configuration
  unavailable, definitely failed, throttled/duplicate and delivery unknown.
  Reuse existing transport/cancellation semantics; preserve compatibility of
  other IAppEmailSender callers/fakes and existing bool behavior if retained.
  Do not map a lost SMTP acknowledgment or cancellation after dispatch to a
  safe-to-retry definite failure. No automatic SMTP retries.
- [x] On SMTP acceptance, use POST/Redirect/GET and clear the completed form;
  refresh must not send again. On validation/configuration/known failure, render
  the encoded posted model and field/global errors without losing content.
  On Unknown, retain the message and explain the uncertainty without claiming
  non-delivery or encouraging an immediate duplicate. Do not place form PII
  in URL/query strings, TempData cookies, logs or persistent browser storage.
- [x] Protect the anonymous send path against spam and concurrent duplicate
  requests on the server, beyond the disabled browser button. Use a bounded
  short-lived server-issued, protected Contact nonce/operation gate and documented rate/capacity limits
  inside an existing registered boundary/pure helper, with separate actual
  client partitions rather than the account-security anonymous ID=0 bucket.
  Validate time/nonce before sending. Concurrent/replayed accepted or unknown
  operations must not dispatch again within the supported scope; a nonce reused
  with changed content must not send. Known-failure retry is a deliberate new
  operation. Keep metadata caches bounded/expiring and free of raw form PII.
  Reject an expired nonce before SMTP; do not evict an active send to admit
  another request when capacity is full. Keep pending/unknown replay protection
  consistent with the nonce validity period and the transport deadline.
  Do not trust arbitrary forwarded headers or change proxy settings.
- [x] State the limits of an in-process gate: it does not certify deduplication
  across worker processes/restarts. Establish whether multiple workers are a
  release constraint; do not promise distributed/exactly-once mail. If a stronger
  design actually needs protected configuration or persistence, document the
  concrete dependency and finish independent work rather than silently adding it.
- [x] Use a small response-safe form state contract for Gemini. No hardcoded
  fake inbox/phone/address/hours or unverified 24-hour promise. A missing verified
  channel is omitted, not replaced with the test Gmail. Do not expose SenderEmail
  or credentials merely because they configure the SMTP transport. An explicitly
  configured support address is public only when its purpose is established.
- [x] Keep logging to opaque operation ID, category, elapsed time and redacted
  configuration presence. Never log visitor email/message/IP, credentials or
  complete SMTP exception payloads. Do not add visitor auto-replies.

## Verification and handoff

| Check | Required evidence |
| --- | --- |
| HTTP access/CSRF | Anonymous GET works; appeal subject selected; missing/invalid antiforgery rejects with zero transport calls; authenticated visitor also works |
| Input boundaries | Empty/whitespace, exact/over limits, valid Vietnamese, malformed email, CR/LF and tampered subject; invalid cases send zero messages |
| Routing/encoding | Support recipient and Sender fallback; malformed/missing configuration; configured From, one valid Reply-To, no injected recipients/markup |
| Delivery states | Fake success, known rejection, missing config, stalled deadline, request cancellation and connection loss; Unknown stays distinct |
| Preservation/replay | Failed/unknown text remains; success refresh has zero extra sends; simultaneous same nonce and altered payload cannot duplicate |
| Abuse isolation | Throttling prevents sends, normal second client is not blocked by the first, metadata expires and capacity is bounded |
| Regressions | Existing mail design, SMTP cancellation, reset/account/weekly mail tests; no real mail in ordinary tests |

Run `dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release` and focused
tests using actual discovered test names, then the Release suite when feasible.
If no persisted model changes, record that no migration is required. If any
persisted mapping is changed, load `tct-ef-migration`, ship migration metadata and
snapshot and verify upgrade/repeat/data preservation on disposable MariaDB 10.5.23.
Do not create a database, shared-data package or migration solely to tick a box.

Write `docs/feature-audits/2026-11-16-contact-refresh/01-backend-ledger.md` and
`backend-contract.md`: route/method, model fields/limits, subject keys, form states,
status codes, nonce/throttle/retry behavior, retained IDs, public-info source,
SMTP semantics, known process limits and exact test results. State any proposed
symbol/file as new until implemented; do not claim a guessed contract exists.
Append the required bug-memory entry after actual fixes. Handoff in Vietnamese.
Do not send real SMTP during this step; stage 03 owns the live send.
