# Contact page: backend, interface and real SMTP acceptance

Prepared: 2026-11-16. Mode of this authoring turn: `PLAN_ONLY`.
Observed branch: `fix/writing-goals-profile-polish`; HEAD:
`2a20c6185c38b9f975ddf555b7dc73147e2bf1ca`. The working tree was clean
before this documentation pack. Recheck it when executing.

## Owner decisions and work order

Keep this branch. Do not create a branch/worktree, switch to master, pull or
replace the checkout. Preserve completed Writing/Goals/Speaking work and any
newer changes. Work sequentially, one agent at a time, under AGENTS.md.

| Step | Selected model | Plan | Execution prompt |
| --- | --- | --- | --- |
| 01 | GPT 6.1 Sol High | [Backend audit and repair](01-backend.md) | [Backend prompt](../../../../.ai/prompts/contact-refresh/01-gpt-backend.md) |
| 02 | Gemini 3.8 Flash High | [Contact UI/UX](02-interface.md) | [Interface prompt](../../../../.ai/prompts/contact-refresh/02-gemini-interface.md) |
| 03 | GPT 6.1 Sol High | [Integrated review and real SMTP](03-integration-smtp.md) | [Final acceptance prompt](../../../../.ai/prompts/contact-refresh/03-gpt-integration-smtp.md) |

Stage 01 establishes and tests the backend contract. Stage 02 consumes that
contract and redesigns the page. Stage 03 repairs any Contact integration defects
and sends the real message through the completed form. This order avoids
designing against invented statuses and testing only an isolated mail helper.

The owner authorized real Contact SMTP testing to `baochau1512v@gmail.com`.
Default to one real message; allow at most two in total for this work pack if a
specific correction needs another check. Do not automatically retry an ambiguous
send. Automated/repeated tests must use fake transports. This address is a test
destination, not approval to publish it as the site's official support address.

Executing a launch message permits its local Contact implementation/fixes and
checks. This planning turn sends no mail and changes no application code.
No commit, push, PR, remote CI, merge, staging or production operation is granted.
Program.cs, appsettings and project files retain their protected status. Reuse
existing registrations; a genuinely necessary protected-file change needs a
specific proposal rather than an inferred grant.

## Source evidence, not runtime acceptance

| Observed source | Implication |
| --- | --- |
| `HomeController.Contact` has anonymous GET and antiforgery-protected POST | Preserve `/Home/Contact`, anonymous access and locked-account appeal access |
| GET normalizes `subject`; keys include `khieu-nai-khoa-tai-khoan` | Preserve appeal deep links and existing subject values |
| POST normalizes the subject, validates, calls the sender, then redirects on both success and failure | Audit tampered subjects; preserve the form on failed/unknown sends |
| `ContactFormViewModel` has name/email/message limits of 100/254/2000 characters | Align server validation, browser limits and counters with the real contract |
| The view hardcodes a support Gmail, `+84 123 456 789`, Hanoi, office hours and a 24-hour response claim | Render only verified public information; do not invent replacement business facts |
| The view has no `tct-theme` wrapper; the UI tracker marks Contact as not migrated | Adopt the shared Clay foundation, with page-scoped CSS/JS |
| `contact.js` disables duplicate submits and restores the button on pageshow | Preserve IDs and test actual validity/loading/back behavior |
| `SendContactMessageAsync` returns bool; the tracked sender elsewhere distinguishes NotConfigured/Sent/Failed/Unknown | Give Contact truthful states without regressing other mail flows |
| SMTP recipient is configured SupportEmail, falling back to SenderEmail; From is the configured sender and Reply-To is the visitor | Keep recipient server-controlled and test routing explicitly |
| SMTP has a bounded deadline and a fake transport seam | Reuse the existing implementation rather than creating a second SMTP stack |
| The only configured rate-limit policy found is account-security; Contact does not opt into it | Do not claim this public form is already throttled or apply a shared anonymous-user bucket |
| Contact email already uses `TctEmailRenderer.RenderSupportContact` | Reuse the shared email design; no wholesale transactional-email redesign |

These are current source observations. Credential availability, Gmail arrival,
spam placement, current browser rendering and production topology were not
verified in this planning turn. Historical SMTP success does not certify today's
credentials or final implementation.

## Shared deliverables and boundaries

Use `docs/feature-audits/2026-11-16-contact-refresh/` for a short baseline,
backend contract, per-step ledger and final acceptance matrix. Use synthetic
message data; screenshots/scratch belong in ignored `.tmp/contact-refresh/` or
`.appdata/local/`, not tracked artifacts. Never include credentials, raw form
messages, real IP/user-agent records or mailbox screenshots in Git.

Contact remains a public support form, not a new ticketing/Admin system. No
auto-reply to arbitrary visitor addresses, attachments, paid AI, public inbox,
read receipts or status-tracking endpoint is required. Default to no persisted
model change; if implementation genuinely needs persistence, apply the existing
EF/migration/provider rules and justify the additional scope before proceeding.
No contact submissions, accounts or personal state belong in shared-data exports.

## Copy-paste launch messages

Select GPT 6.1 Sol High for step 01:

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/contact-refresh/01-gpt-backend.md and implement step 01 locally. Stay on the current branch; do not create or switch branches. Audit and fix the Contact backend, preserve anonymous access and appeal links, and hand off a tested contract to Gemini. Use fake SMTP for this step. Work as one agent, preserve existing work and report in Vietnamese. No commit, push or deployment.
```

After step 01, select Gemini 3.8 Flash High for step 02:

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/contact-refresh/02-gemini-interface.md and implement step 02 locally using GPT's actual backend contract. Stay on this branch and redesign Contact with the project's shared Claymorphism system. Preserve the form, anonymous appeal flow and all backend states; verify responsive and keyboard behavior. Work as one agent and report in Vietnamese. Do not send real email, commit, push or deploy.
```

After step 02, select GPT 6.1 Sol High for step 03:

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/contact-refresh/03-gpt-integration-smtp.md and complete step 03 locally. Review and fix Contact integration, then send one real synthetic Contact message through the completed form to baochau1512v@gmail.com using saved local SMTP settings. Reuse the owner's authorization; never expose credentials or automatically retry an unknown delivery. Stay on this branch, work as one agent and report actual SMTP versus Gmail-arrival evidence in Vietnamese. No commit, push or deployment.
```
