# Email Clay redesign — scoped implementation checklist

Status: completed locally; tested on Debug and Release builds with production send-path integration.
Execution prompt: .ai/prompts/antigravity-email-clay-redesign.md.
Owner: wants Antigravity to design all existing email presentation in the current
project style. One agent, local work. Existing dirty changes remain protected.

## Ordered work

- [x] E0 Inventory actual producers in SmtpAppEmailSender, AccountSecurityService
  and OutboundDeliveryProcessor; record purpose, recipient, URL and expiry contracts.
  Run the relevant baseline checks. Reuse the canonical design tokens.
- [x] E1 Create a pure shared email renderer/shell and representative reset,
  long-code verification and weekly-summary fixtures. Provide a concrete preview
  with email-safe markup and documented token-to-literal mapping.
- [x] E2 Integrate every existing mail family/variant, preserving transport,
  security, contact routing and weekly calculations. Do not change the ignored
  SMTP diagnostic alone or create a separate mockup-only design.
- [x] E3 Generate the preview gallery from production-rendered synthetic messages.
  Inspect message desktop/narrow layouts, long content, empty data, images off,
  dark-mode legibility and text enlargement. Fix observed defects.
- [x] E4 Add meaningful rendering/encoding/link/fake-transport regressions and
  run Release build plus relevant SMTP/Account checks. Record client limitations.
- [x] E5 Review diff, update the execution ledger and deliver Vietnamese results
  with the preview entrypoint and any owner decision genuinely still required.

## Coverage matrix

| Family | Variants / edge cases | Evidence to collect |
| --- | --- | --- |
| Password reset | Actual link/15-minute expiry, long fallback URL | Preview + fake sender + existing reset checks |
| Existing-email verification | Opaque long code/10-minute expiry | Mobile wrap/copy + one-use proof checks |
| Email change | Pending email,30-minute confirmation, old login preserved | Fake mail + confirmation regression |
| Sensitive proof | Reauth, first local password, deletion; correct purpose/TTL | All variants + encoding + no premature completion claim |
| Blocking | Permanent/finite status, long hostile-looking reason, UTC label | Text encoding + preserved status details |
| Unlocking | Manual/automatic copy | Both variants + fake transport |
| Support contact | Long names, subject/body, line breaks, original Reply-To | Header/content encoding and routing regression |
| Weekly | Real period/count/time, zero data, long manage link | Production fixture + period/eligibility regressions |
| Existing unsubscribe proof | Only if an actual caller exists | Preserve current TTL and confirmation; no new flow |

Use docs/feature-audits/2026-11-11-email-clay-redesign-ledger.md for execution
results; that path is a proposed deliverable. Do not prefill it with assumed passes.
Browser previews are not proof of actual Gmail/Outlook delivery/rendering.

## Authority and excluded work

No live mail, credentials/configuration, Program.cs, package/project files,
database/shared-data changes or release work. Use a pure renderer and current send
seams. Package security maintenance is documented separately in
docs/superpowers/plans/2026-11-11-dependency-security-maintenance.md and must not
be silently bundled into this design implementation.
