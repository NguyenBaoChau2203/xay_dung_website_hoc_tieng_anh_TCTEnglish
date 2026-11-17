# Step 02: Contact UI/UX with the shared Clay foundation

Executor: Gemini 3.8 Flash High. Mode when invoked: `IMPLEMENT_LOCAL`.
Read [README](README.md) and the completed backend contract/ledger under
`docs/feature-audits/2026-11-16-contact-refresh/` before changing the page.
One agent, current branch, local work only. Do not send real email in this step.

## Outcome and boundaries

Make `/Home/Contact` welcoming and useful on the demo laptop, with an obvious
support form, trustworthy contact information and clear submission states.
Use the actual contract; no decorative fake success, invented queue status or
hidden backend errors. Preserve anonymous access, appeal links and existing
Writing/Goals/Speaking behavior.

Primary files are `TCTEnglish/Views/Home/Contact.cshtml`, `wwwroot/css/contact.css`
and `wwwroot/js/contact.js`. Read DESIGN.md, `docs/design-system/team-ui-workflow.md`
and `tct-ui-component`; use `tct-ui-review` for browser checks and the existing
UI/UX lookup only for a concrete design question. Use CodeGraph for source.
The current model/namespace includes legacy TCTVocabulary names: use the actual
backend contract rather than performing an unrelated namespace migration.

## Ordered checklist

- [ ] Record desktop/mobile baseline, actual browser/role and app build. Read
  the shared gallery and choose existing components/tokens. If tools are absent,
  state that limitation and use the available browser; do not claim Chrome MCP ran.
- [ ] Wrap the page in `tct-theme` and a scoped Contact page class. Use the
  shared `_PageHeader` and appropriate state component/Bootstrap feedback, one
  h1, semantic headings and the existing font/icon family. Use page-scoped token
  CSS, not a new palette, CDN, font, inline layout style or global selector reset.
- [ ] Compose a calm desktop layout: concise support introduction, compact
  verified contact/help panel and a prominent form. Keep the primary action
  reachable on a laptop with limited height. Stack naturally on narrow screens;
  avoid a tall decorative hero or repeated nested raised panels.
- [ ] Show only public contact information delivered by the backend contract.
  Omit unverified phone/address/hours and unsupported service-response promises.
  Do not publish `baochau1512v@gmail.com` merely because it is the test recipient.
  A public mailto link must use the verified contact address. Do not add a fake
  map, social link or support availability badge.
- [ ] Keep Name/Email/Subject/Message names, form action, POST, antiforgery,
  nonce fields and all required IDs/selectors. Preserve `contactForm` and
  `contactSubmitBtn` unless all real consumers/tests are intentionally updated.
  Keep the appeal key selectable through the query deep link. No files/upload.
- [ ] Add clear required indicators, sensible placeholders, autocomplete,
  matching input limits, an accessible message counter and per-field guidance.
  Match the server's actual string-count behavior for Vietnamese and emoji;
  labels and server validation remain authoritative without JavaScript.
- [ ] Implement idle, invalid, pending, accepted, unavailable, known-failure,
  throttled and unknown states from the real contract. Preserve text on errors;
  indicate waiting accessibly, prevent duplicate clicks and restore controls
  after a cancelled invalid submit/back navigation. Do not trigger sends on
  load, mode changes, counter updates, FAQ expansion or validation alone.
- [ ] Focus/announce the actual error summary or completion message without
  trapping keyboard users. Associate help/errors with controls, update
  aria-invalid as appropriate, use visible focus and text plus status icons.
  Respect reduced motion; no perpetual decorative animation.
- [ ] Keep FAQ content concise and correct for current routes/products. The old
  free-for-TCT-employees assertion is unverified; do not carry it into a new
  business promise. If using an accordion, use the existing Bootstrap component,
  real heading buttons and keyboard behavior. Verify password/support links.
- [ ] Update the Contact adoption row in the existing team UI tracker with
  actual evidence, without modifying unrelated rows or copying the design spec.
  Do not edit shared CSS/layouts/partials to solve a page-only problem. If an
  actual missing shared component requires a proposal, list affected consumers
  and the smallest change before touching that shared surface.

## Acceptance and handoff

Observe the actual Razor page at 1440, 768 and 375/390 pixels; start with the
desktop/laptop composition. Check native browser zoom 200% and keyboard Tab/
Shift+Tab/Enter, FAQ behavior, focus, contrast and long unbroken Vietnamese/English
text. CSS zoom/emulation alone does not establish native browser zoom acceptance.
Check scrolling geometry instead of hiding overflow. Confirm guest and signed-in
shells and appeal deep links; no login wall or irrelevant learner navigation.

Exercise error/loading/success states with the real rendering/contract and an
isolated test host/fake SMTP transport. Do not send live mail to populate UI
states. A DOM-only preview is visual evidence, not proof of backend integration;
label it if used. With JS disabled, a valid POST and server errors still work.

Run the Release build and targeted tests for changed form/JS behavior and the
backend regressions that cover markup coupling. Add behavior-focused JS tests
only for meaningful submission regressions, not snapshots mirroring CSS/markup.
Use the canonical local launcher if starting the app; preserve other processes.

Write `docs/feature-audits/2026-11-16-contact-refresh/02-interface-ledger.md` with
changed files, retained contract, actual browser/matrix, ignored screenshot
locations and unobserved checks. Append bug memory only for actual fixes.
Hand off in Vietnamese. No real SMTP, Git release or protected config changes.
