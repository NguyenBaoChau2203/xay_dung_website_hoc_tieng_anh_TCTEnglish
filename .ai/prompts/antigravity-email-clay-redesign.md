# Antigravity execution prompt — TCT English email design system

## Outcome and working mode

When the owner starts this prompt, use IMPLEMENT_LOCAL to design and integrate a
consistent, polished email presentation for the existing TCT English mail flows.
Implement the templates and wire them into the current send paths; a standalone
mockup alone is not completion. Use one agent and the model selected by the owner.
Report progress and the final handoff in Vietnamese.

The owner's Gmail screenshot shows a deliberately minimal SMTP diagnostic email.
That image is evidence of the unstyled message body, not a visual specification.
Design the message content, not Gmail's toolbar, sender row, avatar or inbox UI.
The expected result is recognizably TCT English: the existing Claymorphism brand,
readable Vietnamese, consistent hierarchy and a clear action for each message.

## Current source and verified context

Inspect the current branch, HEAD and dirty work. At authoring time the checkout
was fix/staging-uat-polish at c908f66 with substantial existing Account/backend,
Gemini/layout/loader and unrelated documentation changes. Preserve that work.

Use AGENTS.md, DESIGN.md and docs/design-system/team-ui-workflow.md. Load the active
tct-superpowers, tct-engineering and relevant UI skill instructions only. Use
CodeGraph first for indexed source; use focused direct reads for missing gaps.
Do not initialize/rebuild an index or launch another AI worker/reviewer.

Read these source boundaries as needed:

- TCTEnglish/Services/SmtpAppEmailSender.cs: existing HTML for password reset,
  block/unblock and support contact; the common SMTP send/error handling.
- TCTEnglish/Services/IAppEmailSender.cs and ISmtpMailTransport.cs: existing send
  contracts and transport seam. Note the legacy TCTVocabulary.Services namespace.
- TCTEnglish/Services/Accounts/AccountSecurityService.cs: new-email confirmation
  and purpose-specific emailed security proofs.
- TCTEnglish/Models/AccountSecurityChallenge.cs: existing purpose identifiers.
- TCTEnglish/Services/Notifications/OutboundDeliveryProcessor.cs: weekly summary
  data/period calculation and settings link.
- TCTEnglish/Services/Notifications/OutboundDeliveryOptions.cs: PublicBaseUrl and
  AccountSettingsUrl semantics; docs/operations/account-outbound-notifications.md.
- TCTEnglish/wwwroot/css/tct-learning-ui.css and docs/design-system/preview.html:
  canonical Clay tokens and existing visual examples.
- TCTEnglish.Tests/SmtpTransportFakeTests.cs, AccountReviewRepairTests.cs and
  AccountFunctionalCompletionTests.cs: relevant regressions and fake transports.

SMTP has already been verified against Gmail with one owner-authorized diagnostic
message and Inbox receipt. This is transport evidence, not evidence of a finished
email design or scheduled weekly delivery. Do not copy any previous credential,
recipient address, verification token or ignored SMTP helper into source/fixtures.

## Authority and boundaries

Authorized scope is local email presentation, a small reusable rendering layer,
integration into the existing mail producers, synthetic previews and meaningful
regressions. Existing backend Account fixes, send recipients and business/security
contracts must remain intact. Do not stop at a second plan or request routine
approval for reversible design choices supported by the current brand.

- Keep SMTP authentication, TLS, deadlines, cancellation, Sent/Failed/Unknown
  outcomes, retry/lease fencing, opt-out and scheduling unchanged.
- Keep routes, token bytes, expiry, one-use consumption, ownership, CSRF and
  security-version behavior unchanged. Do not introduce new verification flows.
- Keep contact To/From/Reply-To routing unchanged. User contact content is data,
  never trusted HTML, instructions or an email header supplied without validation.
- No persisted entity/schema/shared-content change, package addition/upgrade,
  Git commit/push/PR, remote CI, deployment or production change is authorized.
- Do not edit Program.cs, appsettings files, project files, User Secrets or
  hosting settings. Prefer a pure renderer callable from the existing services
  so this work needs no new DI registration. Record a genuine protected-file
  dependency while completing independent template work; do not invent authority.
- Do not change shared website CSS/layouts to style emails. The dependency security
  maintenance plan is separate; do not bundle those upgrades into this design task.
- Sending real mail is not part of this implementation authorization. The prior
  one-message SMTP test is already complete. Finish offline previews/integration
  first; a later real-mail check needs a separately specified recipient and scope.

## Required template coverage

Inventory actual callers first, then cover every existing email producer. Reuse
one shell and a small number of content patterns with appropriate semantic accents.

| Email family | Required content and preserved behavior |
| --- | --- |
| Password reset | Primary CTA, readable fallback URL, current 15-minute expiry and unsolicited-request guidance. No password or completed-reset claim. |
| Verify existing email | Purpose-specific title and instructions, actual opaque code, current 10-minute expiry and one-use notice. Do not claim already verified. |
| Confirm a new email | CTA and fallback URL; current 30-minute expiry; explain that the old login email remains active until confirmation. |
| Sensitive-operation proof | Distinguish Reauth, SetFirstPassword and Deletion in headings/copy; preserve the same actual code and 10-minute expiry. A deletion proof email must not claim the account has already been deleted. |
| Account blocked | Explain the status, safely encoded reason and finite/permanent lock state. Preserve actual timezone/expiry wording; do not relabel UTC as Vietnam time. |
| Account unlocked | Correct manual/automatic variant; calm success presentation and a login link only if an authoritative valid URL is already available. |
| Support contact | Clear sender/subject/message layout, retained line breaks and Reply-To behavior. Long and malicious-looking user content must render as text. |
| Weekly learning summary | Actual previous-week dates, Vietnam learning timezone, confirmed active days and estimated interaction time; quiet empty/partial states; existing manage/disable-email link. No invented XP, score, streak, percentage or efficacy claim. |

If the existing WeeklyUnsubscribe proof/link variant has an actual caller, style
that variant too. Preserve its current confirmation behavior and TTL; do not
activate a new unsubscribe endpoint or turn an opening GET into a state change.
Do not invent welcome, payment or deletion-success notifications that are not sent.

## Visual direction and email compatibility

Follow the existing blue/yellow TCT brand and restrained Clay composition: a soft
canvas, one rounded primary card, clear wordmark, a concise heading, readable body,
one primary action where appropriate and a quiet footer. Use deliberate spacing
and semantic accents; security and account-block notices should remain calm and
clear. Avoid a promotional hero in every transactional message.

Treat these as design requirements, not permission to change the website theme:

1. Canonical values come from DESIGN.md and tct-learning-ui.css. In sent email,
   resolve those values into literal inline declarations because email clients
   cannot depend on the website's CSS variables/classes. Document the token-to-email
   mapping once in the email layer. This is an email compatibility adaptation,
   not a second palette. Do not copy an entire website stylesheet into the message.
2. Use self-contained email HTML, UTF-8, lang=vi, an appropriate subject/preheader,
   one logical main heading and readable fallback text/links. Use a fluid, centered
   roughly600–640px content area and a single-column narrow layout with comfortable
   padding. Avoid giant empty areas, clipped text and decorative nested cards.
3. Use presentation tables for essential layout. Body, CTA and information hierarchy
   must survive stripped optional CSS. No JavaScript, forms, iframe, website nav,
   Bootstrap dependency, external stylesheet, Tailwind, icon font or new font/CDN.
4. Inter may be the preferred face, with the existing Arial/sans-serif fallbacks.
   Do not depend on a webfont download. Use live text for the brand and critical
   instructions. Optional images need absolute validated URLs, dimensions and alt
   text; image blocking must not hide the action, code or status.
5. Rounded corners and soft shadows can enhance supporting clients. The fallback
   must remain a polished bordered/tinted card when a client ignores them. Gmail
   or Outlook does not need pixel-identical browser shadows to satisfy the design.
6. The primary CTA is an actual descriptive anchor with sufficient touch area and
   readable contrast. Include its actual usable fallback URL for security actions.
   Never invent a production domain, localhost link, support address or contact
   number to make a footer look complete. Omit a decorative link when no verified
   destination exists; security links must fail safely rather than use a fake URL.
7. Codes are opaque backend values, currently potentially64 hex characters. Keep
   them selectable, exact and legible on mobile; do not replace them with a six-digit
   visual convention, truncate, insert copy-changing spaces or alter token format.
   Long URLs, names, addresses and code blocks must wrap without widening the mail.
8. Check color/contrast with dark-mode transformations and images disabled. Do not
   rely on CSS dark-mode support or an icon alone to convey a warning/success state.
   Keep content sensible with200% text scaling. Avoid animation and unnecessary
   background-image ornament. Keep HTML comfortably below client clipping limits.
9. Use concise Vietnamese product copy. Keep operational test markers, provider
   names, stack traces, transport states and implementation details out of normal
   learner emails. Supply synthetic sample data clearly labeled in the preview.

Use the current official Gmail CSS support reference when choosing email CSS:
https://developers.google.com/workspace/gmail/design/css. Use official client docs
for any additional client-specific claim. A Chrome preview is not an Outlook pass.

## Deliverables and implementation order

Use docs/superpowers/plans/2026-11-11-email-clay-redesign.md for checkpoints.
The following new paths are proposals, not claims that those files already exist:

- A small typed renderer and shared shell under TCTEnglish/Services/Emails/.
  Keep it free of EF, SMTP credentials and request-only browser dependencies.
- All existing email producers connected to the shared presentation. Prefer one
  explicit rendering pass; do not double-wrap HTML in TrySendTrackedHtmlAsync or
  style only the ignored diagnostic helper.
- A reproducible preview under docs/design-system/email-preview/ showing every
  family/variant with synthetic normal, long and empty content. Generate sample
  HTML from the production renderer, rather than maintaining separate mockups.
  Provide an index and useful plain-text counterparts/export where appropriate.
- Focused tests for rendering/encoding/links and fake-transport integration.
- docs/feature-audits/2026-11-11-email-clay-redesign-ledger.md with actual screenshots,
  checks, mapping, integration inventory and explicit client coverage/limitations.

Steps:

1. Record the current caller/purpose/URL/expiry inventory and relevant test baseline.
   Confirm how each renderer can be reused without a protected-file change.
2. Build the shared shell and three representative templates first: password reset,
   long-code verification and weekly summary. Render a concrete preview, assess
   desktop/mobile hierarchy and correct it before expanding the variants.
3. Cover remaining existing variants and wire the production send paths. Encode
   dynamic text and attribute values in the correct context. Preserve supplied
   token/link values; validate generated destinations against existing URL rules.
   Do not introduce raw user HTML or a new broad sanitizer package.
4. Check the actual production-rendered fixtures in a browser, inspect screenshots
   and improve the concrete issues. Run fake SMTP/account/security regressions.
5. Review the diff inline and finish the ledger/Vietnamese handoff. If a specific
   client/provider cannot be observed, label that check unverified and complete
   the available local work. Do not wait for a live-mail permission to build a preview.

## Meaningful acceptance

- All existing mail families use the same identifiable TCT shell, coherent typography,
  spacing, actions and status patterns, with appropriate variants rather than raw
  paragraphs or separate inconsistent hardcoded palettes.
- Preview files come from production rendering and are reproducible. Inspect at
  desktop message widths and375/390px narrow widths, text enlargement, images off,
  long Vietnamese/contact content, long opaque codes, long URLs and empty weekly data.
- Malicious text containing markup, quotes, ampersands and newlines cannot inject
  markup/attributes or change link destinations/recipients. Exact tokens still work;
  old expiry/one-use/confirmation checks remain. No action auto-executes from email.
- Capture fake SMTP messages from existing producers to prove the styled HTML is
  actually sent. Keep password-reset cancellation, SMTP failure/Unknown semantics,
  proof confirmation and weekly-period/opt-out regressions passing. Do not replace
  assertions with snapshots that only repeat implementation output.
- Run a Release build and focused SMTP/Account tests, then the broader suite if
  new failures or changed integration scope justify it. Verify the exact existing
  test names before filtering; never treat zero matching tests as a pass.
- Report Gmail web/mobile, Outlook desktop/web and other client observations
  separately. A browser gallery is preview evidence only. No invented live-client
  screenshots, delivery receipts or successful configuration claims.
- Review git diff --check and changed paths. No credentials, real customer data,
  private mailbox content, DB/schema changes or unrelated UI/package changes.

## Vietnamese handoff

Provide: completed template/variant inventory, files changed, preview entrypoint,
screenshots or artifact links, exact tests/results, integration/encoding evidence,
unobserved email clients and any real blocker. Distinguish local work from live
mail/CI/staging/production. Keep the implementation ready for owner/Codex review.

## Short launch message for Antigravity

```text
Read .ai/prompts/antigravity-email-clay-redesign.md and execute the local email
redesign using the current TCT Claymorphism style. Cover every existing email
family, integrate the production send paths, and generate previews from the same
renderer. Preserve backend/security/SMTP contracts and existing work. Use one
agent, send no real mail, run the relevant checks, and report in Vietnamese.
```
