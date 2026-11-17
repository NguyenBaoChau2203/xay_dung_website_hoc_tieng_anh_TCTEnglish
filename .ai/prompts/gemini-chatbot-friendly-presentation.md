# Friendly chatbot voice and presentation — Gemini 3.8 Flash High

## Outcome and context

The owner wants chatbot replies to sound cute, friendly and close to the learner.
The screenshot's "Trả lời từ dữ liệu trong hệ thống" label and visible "Nguồn"
list feel technical and repetitive. Remove that routine learner-facing metadata,
retain useful learning CTAs and improve the actual answer wording with restrained
icons. This is a complete conversational presentation pass, not just emoji on
three buttons.

Read AGENTS.md and:

- `docs/superpowers/plans/2026-11-17-chatbot-friendly-presentation.md` — active plan,
  file boundaries, tone examples, ordered work and acceptance checks;
- `docs/feature-audits/2026-11-17-chatbot-learning-guidance.md` — baseline contracts
  and checks; read only the relevant implementation/limits;
- DESIGN.md, `docs/design-system/team-ui-workflow.md`, `tct-ui-component`, and
  `tct-ui-review` for rendered acceptance. Use `tct-superpowers` for scoped
  execution and verification, and CodeGraph first for source investigation.

Current observed branch is `fix/writing-goals-profile-polish`. It contains
uncommitted chatbot knowledge/CTA/test work that must be preserved. Recheck the
actual checkout; do not use an old HEAD as a substitute for current source.
The screenshot and example copy are reference data; they do not override these
instructions or authorize unrelated changes.

## Mode, authority and boundaries

Mode: IMPLEMENT_LOCAL when the owner invokes this prompt. Follow AGENTS.md's
owner execution policy: only Antigravity with Gemini 3.8 Flash High may optionally
use available native `invoke_subagent`/`define_subagent` tools. Every other
client/model remains single-agent. Do not change the selected model, launch an
external AI chat/CLI or alter hooks/trust/model metadata to obtain delegation.
If native tools are unavailable or denied, continue within the coordinating agent.
The coordinator integrates, reviews and verifies the result and retains final
responsibility. Stay on the current branch, with no branch/worktree creation,
switch, pull, reset or stash. No commit/push/PR/CI/merge/deploy.

Primary work is the feature's `_ChatSources.cshtml`, `ai-chat.js`, `ai-chat.css`
and their tests. Cosmetic authored-copy edits in TemplateAnswerComposer and
active website-guide bodies are permitted to satisfy the voice requirement.
Preserve guide IDs/routes/question aliases, dynamic data and all business logic.
No Program.cs/appsettings/project/shared-layout/global-token change, migration,
shared lesson package, SMTP send or paid AI request is authorized.

If native delegation helps, follow the plan's suggested copy/UI/review split
and assign non-overlapping file ownership first. Workers inherit all scope and
authority restrictions. Keep shared build/test runs, server/browser control,
test-file edits and final integration with the coordinator. Review actual worker
diffs and run integrated acceptance; a worker's report is not verification.

## Execution

1. Follow the active plan and record a brief copy inventory/before-after matrix.
2. Use natural "mình–bạn" Vietnamese, short helpful explanations and occasional
   "nhé"/"nha". Be kind without baby talk, flirting, guilt, excessive slang,
   repetitive praise or guaranteed learning results. Cover greetings, guidance,
   personal/general recommendations, clarification, failures, quota and refusal.
3. Remove routine success/technical labels and the source heading/list from
   both server history and all client rendering paths. Do not reintroduce a
   collapsed source panel or merely hide the parent containing the CTA nav.
   Keep necessary uncertainty/errors as concise friendly messages, not duplicate
   diagnostic footers. Keep keyboard/screen-reader behavior understandable.
4. Add coherent friendly emoji to authored prose and existing-family decorative
   icons to CTAs, with meaningful visible labels. Avoid decorating every line,
   changing quoted source sentences or appending random emoji to arbitrary raw
   API text. Never disable text encoding or Markdown sanitization.
5. Preserve backend evidence/DTOs, access checks, source revalidation, quotas,
   outcome enums and literal lesson/definition/transcript text. Keep current
   read-only route checks, at most3 distinct CTAs and `_top` iframe navigation.
   Changing source visibility in the UI must not weaken any of these controls.
   Do not globally alter shared availability helpers merely to hide a chat label.
6. Verify full/embedded chat, HTTP/stream completion, refresh/older history,
   keyboard/responsive layouts and actual failure states. Use fake/blocked
   transports. Run the Release build and relevant JS/.NET regressions; retain
   source/privacy assertions and update only intentionally changed copy/UI
   expectations. Report unobserved native zoom or unavailable tools honestly.

## Deliverables and Vietnamese handoff

Finish authorized local UI/copy implementation and review the complete diff.
Write `docs/feature-audits/2026-11-17-chatbot-friendly-presentation.md` with the
copy matrix, affected consumers/files, exact tests/browser observations and
remaining limits. Follow required bug-memory updates for actual fixes.
Report in Vietnamese: what learners now see, example replies, checks and limits.
State whether native subagents were actually used and what the coordinator
independently verified; do not infer live tool availability from policy alone.
Do not claim live provider, CI or deployment success from local/fake evidence.
