---
name: tct-superpowers
description: Use when a TCT English feature, bug fix or multi-step change needs a scoped plan, systematic diagnosis and evidence before completion.
---

# Superpowers for inline project work

Follow AGENTS.md and the owner's single-agent policy. Source snapshots below
are installed references. Their delegation, model routing, separate reviewer,
mandatory commit/push and installer instructions do not apply here. Use the
same agent for planning, implementation, review and verification.

Choose only the needed process:

| Situation | Reference | Project adaptation |
| --- | --- | --- |
| New behavior | [brainstorming](../../../.ai/vendor/superpowers/brainstorming.md) | Inspect current code; clarify material missing requirements. Detailed owner instructions authorize routine implementation choices. |
| Multi-step work | [plans](../../../.ai/vendor/superpowers/writing-plans.md) | Save a scoped checklist under docs/superpowers/plans with files and checks. |
| Plan execution | [execution](../../../.ai/vendor/superpowers/executing-plans.md) | Work inline in the current suitable checkout; maintain a short ledger. |
| Bug/test failure | [debugging](../../../.ai/vendor/superpowers/systematic-debugging.md) | Read bug memory; CodeGraph first; diagnose before editing. |
| Behavior regression | [TDD](../../../.ai/vendor/superpowers/test-driven-development.md) | Write meaningful failing regressions; scale checks to impact. |
| Review feedback | [review](../../../.ai/vendor/superpowers/receiving-code-review.md) | Verify the condition before applying changes. |
| Completion | [verification](../../../.ai/vendor/superpowers/verification-before-completion.md) | Run build and relevant tests; separate verified results from blocked checks. |

Do not create worktrees, agents, commits or PRs solely because a reference suggests
it. Follow the owner's scope. Finish with a concise Vietnamese outcome, changed
files, checks and limits.
