# GPT 6.1 High: friendly chatbot and Windows hook repair plan

Prepared: 2026-11-17. Current branch: `fix/writing-goals-profile-polish`.
Review evidence: [independent review](../../feature-audits/2026-11-17-chatbot-friendly-review.md).
Executor: GPT 6.1 High, one agent. Owner steering includes hook repair T1.

Execution status updated 2026-11-17: steps 1-5 completed locally by one GPT agent.
Current evidence is in the [corrected repair ledger](../../feature-audits/2026-11-17-chatbot-friendly-presentation.md).
The review baseline below is historical. Native Antigravity activation/transport
and native 200% browser zoom remain explicitly unverified.
After successful browser acceptance, the final launcher restart was blocked by
Docker Desktop startup timeout/absent Linux engine pipe. Port 5102 is not currently
serving; the ledger distinguishes earlier observations from final environment state.

## Outcome, authority and dependencies

Close T1 and R1-R4 from the independent review while preserving the completed
starter guidance/CTA work. Repair the Windows hook launch boundary, make
uncertainty understandable, finish the authored-copy pass, keep client/history
CTA identity consistent, and use the project's icon family.

This document is the handoff requested after review; it does not claim repairs
were implemented. The owner's selected next executor is GPT 6.1 High. The linked
combined prompt covers both scoped tooling repairs and application repairs when
invoked. No separate Gemini stage or subagent workflow is needed.

Stay on the actual current branch. Preserve all uncommitted app/policy/test work.
No branch/worktree switch, reset/stash/pull, commit/push/PR/CI/merge/deploy. Keep
Program.cs, appsettings, project files, global UI, data and saved secrets protected.
Use blocked/fake provider transports; no paid generation, speech or SMTP request.
No schema/shared-content change or conversation-history rewrite is required.

Use one GPT 6.1 High agent for diagnosis, implementation, review and verification.
The Antigravity/Gemini exception in AGENTS.md does not apply to this executor.
Preserve that exception in hook policy tests, but do not spawn workers, change
models, use an external AI process or enable Codex multi-agent.

## Step 1 — T1, P2: repair Windows hook invocation

Files: inspect `.agents/hooks.json`, `scripts/agent_clients.py`,
`scripts/agent-tools.cjs`, canonical configuration generation/validators,
`scripts/check_team_hooks.py`, relevant setup/hook tests and hook documentation.
Only change definitions proven affected by the repaired canonical launch path.

1. Read T1's exact reproduction. Establish the native Antigravity argument
   transport where observable. Reproduce with harmless synthetic stdin; do not
   actually invoke a subagent. Distinguish the observed Windows wrapper failure
   from an unverified claim about the live native host.
2. Add meaningful failing regressions at the Windows launch boundary, including
   checkout paths with spaces. Prefer fixed local file/launcher invocation over
   fragile inline `node -e` when supported and necessary for the proven cause.
3. Maintain relocation behavior, pins, schemas, exit codes and the exact recognized
   Antigravity/Gemini modelName rule. Deny missing/malformed/other host/model inputs.
   Do not forge metadata, change trust, approve all workers or weaken the gate.
4. Update canonical generation, affected checked definitions and validators
   together. Keep unrelated native hosts unchanged unless the shared generator
   requires a demonstrated compatibility update.
5. Run tooling validation, Python tests, Node setup tests and the hook checker.
   Native host loading is a separate check. If it cannot be observed, report the
   remaining acceptance and reload requirement without claiming native success.

Acceptance:

- [x] The supported Windows configured launch path reaches policy evaluation
  without the reported JavaScript parse error, including a path with spaces.
- [x] Allowed and denied synthetic payloads preserve exact policy/output behavior.
- [x] No machine-wide settings, trust records or model/delegation permissions change.

## Step 2 — R1, P2: retain meaningful uncertainty

Files:

- `TCTEnglish/Views/Ai/_ChatSources.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js` (`attachGrounding`)
- Relevant `ChatLearningAssistantTests`, `AiRagPhase6UiResilienceTests` and
  `scripts/tests/test_ai_chat_phase6_sources.cjs` cases.

Work:

1. Add behavioral red regressions for actual `status=unverified` projections,
   including corrupt/future metadata and an older stored answer body. Test Razor
   reload and client rendering. Keep metadata-absent legacy rows distinct.
2. Add one concise friendly warning when an answer cannot be verified, even if
   the DTO has no Notice or sources. Example copy (reference, not a fixed fixture):
   `Mình chưa kiểm tra lại được thông tin của câu trả lời này. Bạn hỏi lại mình nhé.`
3. Preserve existing changed/snapshot notices. Preserve the backend replacement
   of revoked content; avoid repeating the same unavailable warning in a footer.
4. Keep ordinary technical success/source labels and raw source lists removed.
   No unverified/stale CTA, made-up destination or raw metadata becomes visible.

Acceptance:

- [x] Unverified retained history has a visible caution in Razor, HTTP, streaming
  completion and older-message rendering.
- [x] Changed/snapshot warnings remain accurate; revoked bodies remain suppressed.
- [x] Successful current answers retain clean presentation and safe CTAs.
- [x] Grounding DTO/enums, ownership/access checks and provider context are unchanged.

## Step 3 — R3/R4, P3: consistent safe actions and existing icons

Files: `LearnerSourceLink.cs`, `_ChatSources.cshtml`, `ai-chat.js`, scoped
`ai-chat.css` only where necessary, and relevant tests.

1. Restore `data-source-key` with a DOM attribute API on every client CTA.
2. Replace the added SVG-string maps with a fixed mapping of the existing Font
   Awesome classes. Verify actual loaded classes before selecting them. Return
   only known constants; never interpret a source title/model output as HTML or
   a CSS class list. Keep useful visible labels and `aria-hidden` decoration.
3. Keep current route allowlists aligned, deduplication, at most three actions,
   freshness/outcome gates and `target="_top"`.
4. Add source-key parity checks across server/client/history/streaming. Include
   current allowed routes with hostile titles, not only rejected malicious URLs.

Acceptance:

- [x] All four render paths keep the same source key, destination and safe target.
- [x] Keys stay invisible; titles remain text-encoded; unsafe destinations stay absent.
- [x] The existing icon family renders in both full chat and the floating iframe.

## Step 4 — R2, P2: finish and accurately document the voice pass

Files: authored prose in `TemplateAnswerComposer.cs`, active guide bodies in
`website-guides.json`, relevant authored client feedback and presentation tests.
Read other answer/error callers only to establish coverage. Do not widen logic.

1. Create an actual branch inventory: greeting; vocabulary with/without data;
   card lookup found/missing/ambiguous; personal/starter recommendations; speaking
   with/without results; class with/without membership; progress/Goals/badges;
   navigation; quota/provider/cancellation; refusal/unsupported cases.
2. Mark changed, already appropriate, and intentionally serious branches. Improve
   copy needing the agreed warm Vietnamese voice. Neutral factual content does
   not require emoji. Avoid repetitive `nhé`, pressure, baby talk or false promises.
3. Preserve factual values, literal quotes, lesson/definition/transcript content,
   guide IDs/routes/question aliases, outcome semantics, payment/quota truthfulness
   and all security policy decisions. Do not promise a button that cannot render.
4. Update meaningful copy expectations without deleting access/source/XSS checks.
5. Correct the submitted friendly-presentation ledger's examples to match actual
   implementation. Separate implemented examples, proposed examples and observed
   browser evidence; replace ellipsis-based test commands with reproducible ones.

Acceptance:

- [x] Every reply family has a documented disposition, with accurate examples.
- [x] Starter versus personal recommendations remain distinct and factual.
- [x] Security refusals are calm; uncertainty/errors stay truthful and actionable.
- [x] No authored-copy edit changes retrieval, entitlement, quotas or source access.

## Step 5 — integrated verification and handoff

1. Run a Release build and the focused Node/.NET suites recorded in the review.
   Run new regressions first as red evidence, then all relevant checks as green.
2. Use the project launcher on an available port. Preserve other workloads and
   block external provider calls during review. An existing process may serve old
   compiled code, even when static JS has changed.
3. In the real browser verify new answers, reload, older messages, streaming,
   unverified/changed/revoked cases through safe fixtures, CTA focus/keyboard, and
   actual floating-iframe navigation. Review 1440/768/390 px and native zoom if
   available; report unobserved checks honestly.
4. Review the complete integrated diff. Preserve unrelated owner changes and do
   not restore already-dirty generated preview files without an exact baseline.
5. Append a repair section to the friendly-presentation ledger with evidence and
   update hook documentation and bug memory for actual fixes. Report in Vietnamese
   with changed files, exact counts, browser observations, native-hook limits and
   confirmation of single-agent execution.

## Prompt and launch message

Select GPT 6.1 High in Codex. Combined prompt:
`.ai/prompts/gpt61-chatbot-friendly-and-hook-repair.md`.

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/gpt61-chatbot-friendly-and-hook-repair.md and implement the full repair plan locally on the current branch using one GPT 6.1 High agent. Fix the Windows Antigravity hook quoting issue T1 and chatbot findings R1-R4. Preserve all existing work and the exact delegation/security policies. Add meaningful regressions, verify full and embedded chat plus the configured hook launch path, and correct the audit to match observed results. Report in Vietnamese. No new branch, subagents, paid AI/SMTP calls, commit, push or deployment.
```
