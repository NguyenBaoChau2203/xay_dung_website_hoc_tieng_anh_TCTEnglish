---
description: Systematic bug investigation
---

# Systematic bug investigation


1. Read .ai/context/bug-fix-log.md newest first and known-issues.md.
2. Use tct-superpowers debugging guidance; query affected symbols with CodeGraph.
   Reproduce the failure and capture actual errors before proposing a fix.
3. Trace route, controller, service, query, ViewModel, Razor and JS boundaries
   as needed. Historical symptom tables are hypotheses, not proven root causes.
4. Write a meaningful regression for behavior changes; confirm failure, apply
   the smallest root-cause fix and verify the regression plus relevant coverage.
5. Review inline for access scope, CSRF and contract regressions. Run build/tests;
   report existing failures separately, without claiming the entire suite passes.
6. Add the verified fix to the bug log in its required format; update known issues
   only when the resolved issue was listed. Summarize in Vietnamese.
