# Contact backend audit and repair — GPT 6.1 Sol High

## Outcome, context and authority

Implement the Contact backend locally so valid public requests reach the
configured support inbox, failures preserve the form and delivery states are
honest. The owner chose this model for backend work and explicitly requested
the current branch, without a new branch. Work as one agent.

Read AGENTS.md, then:

- `docs/superpowers/plans/2026-11-16-contact-refresh/README.md`
- `docs/superpowers/plans/2026-11-16-contact-refresh/01-backend.md`
- `tct-superpowers`, `tct-engineering` and the relevant security skill.

Mode: `IMPLEMENT_LOCAL` for step 01 and its regressions. The plan's source
observations are a starting point; recheck actual code/branch/working tree with
CodeGraph first. Preserve all existing Writing/Goals/Speaking and newer work.
Stay on this checkout/branch; no branch/worktree creation, switch, pull or reset.
The owner has authorized later real SMTP testing to the specified Gmail, but
this step uses fake transports; step 03 owns that bounded live test.

## Execution and deliverables

Follow the ordered backend checklist and acceptance matrix. Preserve anonymous
`/Home/Contact`, the account-appeal subject deep link, POST/antiforgery and the
current subject keys. Fix validation/routing, conservative Unknown semantics,
form preservation and server-side abuse/duplicate handling in the existing
registered boundaries. Reuse the existing SMTP transport and shared email
renderer; keep other mail callers compatible. Do not attach account-security's
shared anonymous bucket to Contact or promise distributed deduplication from
an in-process guard. Avoid a new ticketing system, auto-replies and persistent
Contact storage unless an actual reviewed requirement warrants them.

Keep Program.cs, appsettings, project files, shared UI and hosting protected.
Do not invent permission to change DI/configuration or publish the test Gmail.
If a protected change is truly necessary, explain the smallest concrete proposal
and continue independent work. Never expose/rewrite secrets or reset data.

Add behavior regressions, run the Release build and relevant tests, review the
complete diff and produce `backend-contract.md` plus `01-backend-ledger.md` under
`docs/feature-audits/2026-11-16-contact-refresh/`. Follow automatic migration rules
only if the persisted model actually changes. Append required bug memory for
actual fixes. The contract must be usable by Gemini without this chat.

## Acceptance and Vietnamese handoff

Report the fixed behaviors, files, exact checks and remaining limits in Vietnamese.
Distinguish source observations, automated proof and unobserved runtime behavior.
No live mail, paid AI, commit, push, PR, remote CI, merge or deployment in this step.
