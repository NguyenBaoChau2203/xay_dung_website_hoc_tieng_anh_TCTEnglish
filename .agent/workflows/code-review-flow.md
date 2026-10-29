---
description: Evidence-based inline code review
---

# Evidence-based inline code review


1. Read AGENTS.md, the request and complete diff; use tct-engineering.
2. Review as the same agent using .ai/roles/implementer.md and the relevant UI or
   security lens. Do not spawn a reviewer or hardcode a model.
3. Trace access scope, antiforgery, validation, async behavior and contracts.
   Read callers before reporting missing checks; projections may avoid Include.
4. Check null handling, duplicate submissions, query shape, pagination and states.
5. Use tct-ui-review for rendered interface changes and tct-tooling-review for
   imported skills, hooks or MCP. Keep review within the requested scope.
6. Report findings with priority, exact file/line, trigger, impact and correction.
   Separate observed failures, plausible risks and checks that were not run.
