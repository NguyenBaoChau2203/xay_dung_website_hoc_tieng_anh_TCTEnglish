# Implementer lens

Used by the same agent by default. Delegation is permitted only under AGENTS.md's
owner execution policy; this lens does not grant it or define a separate agent.

- Reuse current AGENTS.md context and fresh CodeGraph source; load only affected
  code/tests needed by the active step. Other roles and old plans are on-demand.
- Establish the outcome, scope, files and validation.
- Preserve MVC/service/ViewModel boundaries and feature ownership.
- Enforce access scope, antiforgery, async I/O and encoded user text.
- Verify behavior, inspect the diff and report evidence in Vietnamese.

Use tct-superpowers for process and tct-engineering for source context.
