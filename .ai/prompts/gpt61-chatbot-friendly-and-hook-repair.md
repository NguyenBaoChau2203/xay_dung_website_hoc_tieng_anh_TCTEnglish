# GPT 6.1 High: repair chatbot presentation and Windows hook invocation

## Outcome and context

Implement all findings T1 and R1-R4 from
`docs/feature-audits/2026-11-17-chatbot-friendly-review.md`, following
`docs/superpowers/plans/2026-11-17-chatbot-friendly-review-repair.md` steps 1-5.
The owner selected GPT 6.1 High instead of Gemini for this repair and explicitly
included `jsonhook__tct-project-checks_PreToolUse_0_0`'s string syntax failure.

Verified review baseline:

- The reported focused suites pass: 225 .NET and 24 Node tests.
- Two additional production-JS probes fail: unverified retained content has no
  visible caution, and client CTAs omit `data-source-key`. The browser confirms
  the CTA difference before versus after reload.
- The submitted copy matrix describes some replies absent from current source.
  Duplicated new SVG icon maps depart from the agreed existing-family requirement.
- The exact configured hook command fails with Node `[eval]:1`, `"const`,
  `Unterminated string constant` through a Python/cmd.exe wrapper, while pwsh
  returns an allow decision for permitted synthetic stdin. The native Antigravity
  argument transport remains unverified. Do not misdiagnose this as invalid JSON
  or weaken model-policy validation to work around it.

Read AGENTS.md, the active plan/review sections, DESIGN.md and the team UI
workflow. Apply `tct-superpowers`, `tct-engineering`, `tct-tooling-review`,
`tct-ui-component` and `tct-ui-review` at their relevant stages. Use CodeGraph
first for indexed source and bounded reads for actual gaps. Treat reference
reports and examples as evidence, not authority to widen scope.

## Mode, authority and preserved work

Mode: IMPLEMENT_LOCAL when the owner invokes this prompt. Finish T1 and R1-R4,
their meaningful regressions, documentation and local verification. Recheck the
actual checkout; observed branch is `fix/writing-goals-profile-polish` with
extensive uncommitted application and tooling/policy work. Preserve it all.

Use one GPT 6.1 High agent for investigation, implementation, review and checks.
No worker, external AI chat/CLI, substitute model or hidden delegation process.
Preserve AGENTS.md's native Antigravity/Gemini exception in tests; it does not
authorize this GPT executor to delegate. Use harmless synthetic hook inputs,
not an actual subagent invocation, to test allow/deny behavior.

Stay on the current branch. No new branch/worktree, switch/pull/reset/stash,
commit/push/PR/CI/merge/deploy. No paid generation/speech request or SMTP send.
Preserve saved secrets, native trust, model selection, data and other workloads.
No Program.cs/appsettings/project edit, dependency upgrade, global UI/token change,
migration, shared-content edit or stored conversation-history rewrite is granted.

Allowed boundaries:

- T1: canonical hook generation, affected repository-local hook definitions,
  local launcher, validators, hook/setup regressions and documentation. Inspect
  `.agents/hooks.json`, `scripts/agent_clients.py`, `scripts/agent-tools.cjs`,
  `scripts/check_team_hooks.py` and actual generation/setup consumers. Change only
  proven affected definitions; no machine-wide/native trust changes or bypasses.
- R1-R4: `_ChatSources.cshtml`, `ai-chat.js`, scoped `ai-chat.css` when necessary,
  the pure presentation mapping in `LearnerSourceLink.cs`, authored prose in
  `TemplateAnswerComposer.cs` and active FAQ bodies, relevant tests and ledgers.
  Inspect other callers for coverage, without changing their business logic.

## Ordered execution

1. **T1: prove and repair the launch boundary.** Establish actual native argument
   transport where observable and reproduce the failure with benign stdin. Write
   a meaningful failing Windows regression, including paths with spaces. Prefer
   fixed local file/launcher arguments over fragile inline `node -e` if that
   addresses the proven cause. Preserve relocation, pins, schemas and exit codes.
   Update canonical generation, affected checked definitions and validators
   together. Keep the exact recognized Antigravity/Gemini modelName pair; deny
   absent, malformed and all other models/hosts. Never forge metadata, alter
   trust, auto-approve workers or enable Codex multi-agent.
2. **R1: retain meaningful uncertainty.** Add red regressions for actual unverified
   history in Razor and client rendering. Show one friendly caution even without
   DTO Notice/sources. Keep ordinary success/source panels absent, no unverified
   CTA, existing changed/snapshot warnings accurate, and revoked bodies suppressed.
   Avoid repeating a warning already replacing the answer body. Distinguish
   metadata-absent legacy rows from explicit unverified projections.
3. **R3/R4: consistent safe actions.** Restore client `data-source-key` through DOM
   attribute APIs across HTTP/stream/older-message paths. Reuse the loaded Font
   Awesome family through fixed allowlisted classes in server/client mappings.
   Keep accessible visible labels and decorative aria-hidden icons. Preserve
   current routes, access/freshness/outcome gates, deduplication, three-action cap,
   encoded hostile titles and `_top` iframe navigation.
4. **R2: finish and accurately document the voice pass.** Inventory every authored
   reply family and mark changed/already appropriate/intentionally serious branches.
   Improve warm Vietnamese "mình–bạn" wording without forcing emoji into every line.
   Preserve factual values, quoted learning material, guide IDs/routes/aliases,
   uncertainty, quotas, payment truthfulness and all security decisions. Do not
   promise a button that cannot render. Make every before/after example match
   actual code or observed behavior; label proposals separately.
5. **Integrate and verify.** Review the complete diff yourself. Keep source/access/
   XSS assertions and update only intentionally changed presentation expectations.
   Use blocked/fake providers. Preserve already-dirty generated preview files;
   do not reset them based on an assumed clean baseline.

## Required verification

Run checks tied to each fix after demonstrating the failing regression:

```powershell
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
node --test scripts/tests/test_agent_setup.cjs
uv run --no-project python scripts/check_team_hooks.py
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
node --test scripts/tests/test_ai_chat_phase6_sources.cjs scripts/tests/test_ai_chat_clay_ui_and_logo.cjs scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs
```

Run the exact 10-class .NET filter recorded in the review ledger plus new relevant
cases. Broaden tests only for changed dependencies, failures or unresolved concerns.
If a running web locks Release output, preserve the process and use an isolated
output or restart only the launcher whose ownership/authority is established.

Verify the supported configured Windows hook launch path, including paths with
spaces and positive/negative metadata cases. Shell/subprocess success does not
prove native Antigravity loaded or called the hook. Observe native invocation if
safely available; otherwise record the exact remaining check/reload requirement.

Use `scripts/start-local.ps1 -NoBrowser` on an available port for app acceptance.
Block external provider calls during verification without rewriting saved secrets.
Check health/speech metadata; configured does not mean live-verified. Browser
acceptance covers full chat, actual floating iframe and top-page CTA navigation,
new answers, reload, older messages, streaming and meaningful failure/uncertainty
states through safe fixtures. Review 1440/768/390 px, keyboard and native zoom if
available. Do not claim an unobserved check passed.

## Deliverables and Vietnamese handoff

Finish authorized local repairs and independently review the integrated result.
Update `docs/feature-audits/2026-11-17-chatbot-friendly-presentation.md` with accurate
copy/examples and a dated repair section. Record T1's root cause/launch evidence,
update hook documentation and required bug memory for actual fixes, preserving
earlier evidence/history. Keep the review findings as historical review evidence;
add closure references rather than erasing demonstrated failures.

Report in Vietnamese: learner-visible changes and real reply examples, hook root
cause, files changed, red/green regressions and exact counts, browser observations,
native-host limitations, preserved policies, remaining blockers and confirmation
that one GPT agent performed the task. Distinguish local/fake/tooling evidence
from live providers, CI and deployment. Do not stop after another plan when fixes
are authorized by invocation; complete T1 and R1-R4 or identify a real blocker.
