# Independent review of friendly chatbot presentation

Repair closure added 2026-11-17: owner-authorized local implementation has addressed
R1-R4 and T1's reproduced Windows launch failure. See the
[corrected repair ledger](2026-11-17-chatbot-friendly-presentation.md) for red/green
regressions, exact counts, copy inventory and browser observations. The findings
below remain the historical pre-repair evidence. Native Antigravity transport/
activation and native 200% browser zoom remain unobserved; local launch tests
are not proof of a successful native subagent invocation.

Date: 2026-11-17. Branch: `fix/writing-goals-profile-polish`.
HEAD: `9bc9fc885f52de2b44c526c6c56dac71ed033a62`.

## Scope and authority

The owner supplied Antigravity's implementation report. This pass independently
reviewed current source, reran the reported focused tests, and inspected the local
browser. It did not implement application/tooling repairs, rewrite the submitted
ledger, publish Git changes, or perform deployment. Existing uncommitted source,
policy changes and email-preview files were preserved.

The reviewer used one Codex agent. Repository permission for optional native
Antigravity/Gemini 3.8 Flash High subagents does not apply to Codex.

## Findings

### R1 — P2: unverified history loses its meaningful caution

`GroundingCitationService.ApplyUnverified` preserves stored display content and
sets `Status = "unverified"` with no source actions. The prior presentation showed
an uncertainty label. `_ChatSources.cshtml:5` and `ai-chat.js:862` now only render
notices for `changed`/`snapshot` and otherwise return when there are no CTAs.
Consequently corrupt/future/unverifiable grounding metadata leaves the old answer
looking like a normal verified answer, without a visible caution.

This is a presentation regression, not proof of an authorization leak. Revoked
sources are a different case: backend projection replaces their answer body with
`GroundingNotices.Unavailable`. Preserve that protection and avoid duplicate
unavailable-body notices.

Correction: show one friendly uncertainty sentence for `unverified` in both Razor
and client paths. Keep ordinary success labels/source lists absent. Preserve
grounding enums, source checks and the ban on CTAs for unverified data.

### R2 — P2: reported copy coverage exceeds implemented coverage

The submitted ledger's before/after matrix claims changes that current source
does not contain. Examples in `TemplateAnswerComposer.cs`:

- Line 63 still starts `Bạn hiện có {totalCount} bộ từ vựng:`.
- Line 173 still starts `Mình tìm thấy từ ... trong bộ từ ...`, while the report
  presents `Mình tìm thấy từ **term** rồi nè 📖` as implemented.
- Line 260 still starts `Một số bài speaking phù hợp với bạn:`.
- Line 417 still starts `Dựa trên dữ liệu học của bạn, mình gợi ý:`; the report's
  friendly personal-recommendation example is absent.

Starter replies and several other branches did change. Neutral security text is
appropriate; adding emoji to every line is not the goal. Finish the authored-copy
inventory required by the original plan, explicitly classify branches retained
on purpose, and make every audit example match an actual branch/test/browser
observation. Do not rewrite literal lesson sentences, definitions or metrics.

### R3 — P3: client-rendered CTAs drop source identity

Razor retains `data-source-key` at `_ChatSources.cshtml:25`. The links created in
`ai-chat.js:900-912` omit it. Fresh HTTP/stream/older-message rendering therefore
differs from reloaded Razor history. The supplied report incorrectly says all
CTA anchors preserve this attribute. Navigation still works; this finding concerns
the explicit presentation/traceability contract.

Correction: assign the source key using the DOM attribute API and test parity
for HTTP, stream completion, older messages and Razor history. Never expose keys
as visible text.

### R4 — P3: a new SVG icon set departs from the agreed UI boundary

The original plan requires the already-loaded icon family. `ActionIcon` and
`actionIconSvg` introduce duplicated inline SVG definitions instead. These are
constant server/client-owned strings; this review does not claim an XSS flaw in
them. Reuse existing Font Awesome icons through a static allowlisted mapping,
keep decorative icons hidden from assistive technology, and keep text labels.
Do not load a new font, package or CDN.

### T1 — P2, separate tooling follow-up: Windows hook invocation failure

The configured PreToolUse command in `.agents/hooks.json` embeds a JavaScript
program inside `node -e`. A local diagnostic read that exact configured command,
provided a synthetic permitted PreToolUse payload on stdin, and compared wrappers:

- Python `subprocess.run(['cmd.exe', '/d', '/s', '/c', command], ...)`: exit 1,
  Node `[eval]:1`, `"const`, `Unterminated string constant`.
- Python `subprocess.run(['pwsh', '-NoProfile', '-Command', command], ...)`: exit 0,
  output `{"decision": "allow"}`.

No native subagent was spawned by this test. It reproduces the reported symptom
through one Windows command wrapper; it does not establish which shell/argument
transport the live Antigravity host uses. The failure is JavaScript argument
quoting before policy evaluation, rather than evidence that JSON data or the
recognized-model rule is wrong. Investigate the actual native launcher before
changing definitions. Do not weaken the gate, forge modelName, or edit trust.

## Verification performed in this review

- Release compilation through the exact focused .NET test invocation succeeded.
  The 10-class filter covering ChatLearningAssistant, TemplateAnswerComposer,
  AiBaselineRegression, WebsiteGuideRetriever, AiRagBoundaryAccess,
  AiRagPhase2FixRound1/2, AiDeterministicBaselineIntegration,
  AiProductionClassifierIntegration and AiRagPhase6UiResilience:
  **225 passed, 0 failed, 0 skipped**.
- Node command below: **24 passed, 0 failed**.
- Two additional in-memory probes reused the production-JS fixture without
  changing tracked test files. Original Phase 6 cases: 11 pass. Both added probes
  fail: missing unverified caution and missing client `data-source-key`.
- Browser: a new exact starter question returned friendly prose and three safe
  `_top` CTAs. No `.ai-chat-state` or `.ai-chat-source-list`. All three source-key
  attributes were absent immediately after response and present after reload.
- Actual rendered full chat at 1440/768/390 px: document width matched viewport,
  three CTAs stayed in bounds. A 1440px screenshot was visually inspected.
- Existing older starter history showed the changed-source notice and suppressed
  stale CTAs after the FAQ text change, as expected.
- Actual dashboard launcher: loaded this review's own saved conversation in its
  418px iframe, observed the three safe CTAs without technical/source panels or
  horizontal overflow, and clicked Writing. The top page navigated to
  `/Home/Writing/Exercises` and displayed `Danh sách bài tập viết`.
- Native 200% zoom and a fresh complete streaming/older-history browser matrix
  were not performed. Those paths have automated coverage, not new manual proof.

```powershell
node --test scripts/tests/test_ai_chat_phase6_sources.cjs scripts/tests/test_ai_chat_clay_ui_and_logo.cjs scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --filter 'FullyQualifiedName~ChatLearningAssistantTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiRagPhase2FixRound1Tests|FullyQualifiedName~AiRagPhase2FixRound2Tests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiProductionClassifierIntegrationTests|FullyQualifiedName~AiRagPhase6UiResilienceTests' --logger 'console;verbosity=minimal' -- xUnit.MaxParallelThreads=2
```

Ignored evidence: `.appdata/local/chat-friendly-review-dotnet.log`,
`chat-friendly-review-probes.log`, `chat-friendly-review-web.log`.

The local launcher was started at port 5102 for this review, with only its child
text-provider base URL set to loopback `http://127.0.0.1:9/v1/`. `/health/ready`
returned `ready`; speech status was configured but not live-verified. No paid
provider or SMTP test was initiated. No secret/configuration file was rewritten.
The temporary review launcher was stopped through its owning exec session after
browser checks. No unrelated server was terminated.

## Next work

Owner steering after this review: use **GPT 6.1 High**, not Gemini, for the
remaining repairs and include the hook syntax failure. Use
[the combined repair plan](../superpowers/plans/2026-11-17-chatbot-friendly-review-repair.md)
and `.ai/prompts/gpt61-chatbot-friendly-and-hook-repair.md` in one single-agent
execution. T1 is part of that repair scope. Existing passing tests do not close
the two demonstrated presentation gaps; this review has not implemented repairs.
