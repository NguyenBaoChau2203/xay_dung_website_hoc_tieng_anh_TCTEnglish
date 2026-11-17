# Contact UI/UX implementation — Gemini 3.8 Flash High

## Outcome, context and authority

Redesign the actual Contact page using TCT English's shared Claymorphism system,
with a clear support form, verified contact details and the backend's real
validation/loading/delivery states. Work as one agent on the current branch.

Read AGENTS.md and:

- `docs/superpowers/plans/2026-11-16-contact-refresh/README.md`
- `docs/superpowers/plans/2026-11-16-contact-refresh/02-interface.md`
- completed `backend-contract.md` and `01-backend-ledger.md` under
  `docs/feature-audits/2026-11-16-contact-refresh/`;
- DESIGN.md, `docs/design-system/team-ui-workflow.md`, `tct-ui-component` and
  `tct-ui-review` for their active responsibilities.

Mode: `IMPLEMENT_LOCAL` for step 02. Recheck actual source with CodeGraph. Wait
for the backend contract rather than inventing success fields or backend logic.
Preserve all existing work; do not create/switch branches, pull, reset or stash.

## Execution and deliverables

Follow step 02's checklist and browser matrix. Prefer changes in
`Views/Home/Contact.cshtml`, `wwwroot/css/contact.css` and `wwwroot/js/contact.js`.
Use `tct-theme`, existing tokens/components, one h1, semantic accessible controls
and feature-scoped styles. Preserve `/Home/Contact`, guest/appeal access, field
names, antiforgery, nonce and retained selectors. Keep content on failed/unknown
delivery. Do not invent a public phone/email, support schedule or response SLA;
the SMTP-test Gmail is not permission to publish a support contact address.

Test real Razor rendering at desktop/tablet/mobile, keyboard and native zoom 200%.
Use isolated fake SMTP for repeated submission states; never send real mail in
this step. Record visual-only previews separately from actual integrated checks.
Keep no-JavaScript server form behavior. Update only Contact's UI adoption row.

Do not edit protected config/project files, global layouts/design tokens or
shared mail templates to solve a page-only design problem. No new UI libraries,
CDNs, fonts, AI services, ticketing backend or data package. Report a necessary
shared change with consumer impact rather than silently widening the task.

Run the Release build and meaningful relevant tests. Write
`docs/feature-audits/2026-11-16-contact-refresh/02-interface-ledger.md` with files,
real browser/tool evidence, synthetic screenshot paths and outstanding checks.

## Vietnamese handoff

Report the result, exact checks and remaining limits in Vietnamese for GPT's
step 03. Never call an emulated/blocked check passed. No real SMTP, paid API,
commit, push, PR, CI, merge, staging or production operation is granted.
