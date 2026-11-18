# Execution prompt 02: Two creation modes and honest fallback UI

Selected model: Gemini 3.8 Flash High. One agent; preserve the selected model.

## Outcome and context

Make it immediately clear that the learner can describe a new Writing task,
instead of only pasting text to translate. Preserve the current source flow and
make the selected offline template fallback useful and truthful.

Read AGENTS.md, `.ai/prompts/README.md`, DESIGN.md/team UI workflow,
`docs/superpowers/plans/2026-11-15-writing-ai-brief-hybrid/README.md`,
`docs/superpowers/plans/2026-11-15-writing-ai-brief-hybrid/02-interface.md` and
the actual `docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/backend-contract.md`.
Inspect the current branch/worktree and current Writing recent-history code.
Preserve all earlier uncommitted work. Do not pull/reset/stash/switch branches.

## Mode and authority

IMPLEMENT_LOCAL for step 02 UI/binding/browser/regression work. Backend step 01 must
already supply real behavior/contracts. No invented engine/quota logic, new AI
provider, Ollama, schema/content rewrite, protected config edits, paid provider
calls or remote Git/release actions. Use current UI skills and Clay tokens.

## Execute

1. Inspect the actual form, script, routes, ViewModels and retained selectors.
   Use CodeGraph first where indexed and bounded reads for missing Razor source.
2. Build accessible "Mô tả bài muốn luyện" / "Dán bài có sẵn" tabs. Brief fields
   use real level/format/length/tone options, meaningful editable examples and
   character/validation feedback. Keep both modes' typed input on tab switches.
3. Bind the real backend payloads and errors, maintaining old source conversion,
   personal cards, filters/paging/create/delete and Writing recent history.
4. During outage show actual internal frame capabilities and missing constraints.
   Let the learner inspect/complete supported slots and explicitly accept an
   adapted frame. Do not silently discard free-text details or translate arbitrary
   source text offline. Unsupported requests remain editable with honest choices.
5. Render real persisted origin:OpenAI versus internal bilingual frame, with a
   truthful unknown state for legacy content. Catalog recommendations are existing
   lessons, not fake generation success. Display the actual shared quota.
6. Keep one logical idempotency identity across retries/confirmation, invalidate it
   on a new edited request and recover unknown outcomes before another creation.
   Prevent double submits, stale callbacks and cross-account state leakage.
   Pageshow/visibility must never automatically resubmit generation.
7. Navigate using the returned exercise's actual metadata and preserve the normal
   practice/progress/draft flow. Recent history is recorded by authorized practice
   opening, not by a template preview or creation success toast.
8. Test both modes and all principal outcomes with a verified fake OpenAI host:
   success/outage/circuit/rate/billing/config/quota/refusal/cancel/unknown response,
   unsupported frame, confirmation, replay, account switch and return navigation.
   Do not spend the saved real key to prove UI behavior.
9. Verify desktop/laptop 1440/1366 and 768/390, long copy, keyboard, reduced motion,
   native 200% zoom or mark it unverified. Run relevant Node/render/UI tests and a
   Release build; recheck Writing/recent/Goals contracts after integration.

## Handoff

Write `docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/02-interface.md` with
actual tools/screenshots/checks/limits and a short manual test order. Report in
Vietnamese. Identify real backend contract gaps rather than filling them with
frontend fixture data. Complete the scoped UI and stop before ungranted release.
