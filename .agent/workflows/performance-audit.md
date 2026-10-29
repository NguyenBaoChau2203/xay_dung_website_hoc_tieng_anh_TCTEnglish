---
description: Measured performance review
---

# Measured performance review


1. Identify the actual slow route/interaction and measure before changing code.
   Read AGENTS.md; use tct-engineering and CodeGraph to trace the relevant flow.
2. Inspect EF query count/shape, projection, pagination, tracking and access scope.
   Do not add Include when a projection already selects the needed data.
3. For browser work inspect network, layout shifts and rendering. Use tct-ui-motion
   for animation jank; do not add a framework or motion library by default.
4. Change the demonstrated bottleneck within scope. Schema indexes, caching,
   background jobs or protected config changes need actual task authority.
5. Compare before/after measurements, check correctness and review inline.
   Record device/runtime, dataset and limitations instead of promising universal gains.
