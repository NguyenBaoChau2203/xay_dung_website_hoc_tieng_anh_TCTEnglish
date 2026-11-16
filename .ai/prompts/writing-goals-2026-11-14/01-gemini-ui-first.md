# Execution prompt 01: Writing, Goals and Profile UI first

Selected model: Gemini 3.8 Flash High. Use the owner's selected model; do not
substitute another model or launch another agent/process to perform the task.

## Outcome and context

Polish the existing Writing/Goals/learning-Profile experience before backend
expansion. Preserve the current attractive Goals design and make data claims
truthful. Read AGENTS.md, `.ai/prompts/README.md`, the work-pack README and
`docs/superpowers/plans/2026-11-14-writing-goals/01-ui-first.md`.
Inspect current branch/HEAD/dirty work. Expected branch is
`fix/writing-goals-profile-polish`; preserve earlier local changes. Use CodeGraph
first for indexed source, with bounded reads for missing Razor source.

## Mode and boundaries

IMPLEMENT_LOCAL: this invocation authorizes only stage 01 UI changes, relevant
tests/browser verification and its documentation. Follow README authority.
Read DESIGN.md, team UI workflow and UI skills. No backend/schema/content imports,
protected config changes, paid API calls, commit/push/PR/remote CI/merge/deploy.
One agent performs implementation/review/checks inline.

## Execute

1. Inventory current routes, selectors, forms, ViewModels and real states. Use
   available Chrome DevTools MCP; if unavailable, use a real-browser fallback
   and report which tool was used. Do not invent observations.
2. Implement the ordered01 plan. Keep current HTTP/JSON/DOM hooks, antiforgery,
   server assessment states and draft isolation. Preserve the Goals composition;
   use shared Clay tokens and scoped page CSS, not another global design system.
3. Remove Profile's invented locked-badge examples. Keep real earned cards and
   honest empty state. Clarify vocabulary-only weekly signals and refresh claims.
   Do not show unsupported secret/tier data in the live interface.
4. Record missing backend fields/semantics in `01-ui-data-contract.md`; visual
   proposals may be clearly labeled docs/demo fixtures, not fake production UI.
5. Run relevant render/Node tests and Razor build after changes. Verify desktop,
   tablet/mobile, keyboard, long text and native 200% zoom or mark it unverified.
   Verify fee-free configuration before any real Writing submission.

## Deliverables and completion

Implement the actual UI and write stage 01 ledger plus data-contract handoff under
`docs/feature-audits/2026-11-14-writing-goals/`. Record routes, screenshots, exact
checks, changed files and missing dependencies. Report in Vietnamese. Complete
this stage only; leave backend expansion to the later separate prompts.
