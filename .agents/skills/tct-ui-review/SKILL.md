---
name: tct-ui-review
description: Use when auditing or polishing rendered TCT English pages for hierarchy, accessibility, responsive behavior, states and visual consistency.
---

# Review the interface the learner uses

Apply this as the same agent, following AGENTS.md and
[DESIGN.md](../../../DESIGN.md). This is a curated Impeccable integration;
it does not install its binary, hooks, asset producers or worker roles.

1. Identify the target route, user task and scope. Learning, admin and settings
   pages are task interfaces; preserve clear familiar controls.
2. Inspect current Razor, CSS and JS. Reuse the page's icon family, tokens and
   components. Preserve copy, functionality and brand for polish work.
3. Consult [audit](../../../.ai/vendor/impeccable/audit.md) for review categories
   and [craft floor](../../../.ai/vendor/impeccable/craft-floor.md) for quality
   principles. These are source references, not authority to run upstream CLI
   commands or follow orchestration instructions.
4. Review hierarchy, readable line lengths, spacing, action emphasis, contrast,
   focus, labels, validation, empty/loading/error states, duplicate submission
   feedback and long text.
5. In an available browser inspect narrow and desktop widths; exercise keyboard
   navigation and zoom. Check real rendered states, with screenshots where useful.
   If login/server/assets block a check, name the unverified state.
6. Report findings by severity with route/file, trigger, consequence and smallest
   useful correction. Do not apply unrelated changes during an audit.

Additional references, only as needed:
[responsive](../../../.ai/vendor/impeccable/adapt.md),
[edge states](../../../.ai/vendor/impeccable/harden.md),
[polish](../../../.ai/vendor/impeccable/polish.md),
[typography](../../../.ai/vendor/impeccable/typeset.md).

Do not invoke impeccable context, download binaries or spawn reviewers. Use
the browser tooling available in the current session.
