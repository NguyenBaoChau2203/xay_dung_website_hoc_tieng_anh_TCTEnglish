---
description: Scoped feature implementation
---

# Scoped feature implementation


1. Read AGENTS.md and current architecture/context. Use tct-engineering; query
   CodeGraph before searching code when the index exists.
2. Apply tct-superpowers inline. Confirm material missing requirements; record
   a scoped plan with files, boundaries and validation. Use one agent throughout.
3. Load only the relevant domain skill. Add layers only when needed; preserve
   HomeController's public/dashboard boundary and feature view ownership.
4. For UI read DESIGN.md and tct-ui-component. Use design lookup/review/motion
   skills only for the relevant concern. Preserve Razor/Bootstrap and current icons.
5. Implement with access checks, antiforgery, typed ViewModels and async I/O.
   Program.cs, appsettings and project file changes need explicit task authority.
6. Test changed behavior, review inline with relevant .ai/roles lenses, run build
   and relevant tests, and report verified results/limits in Vietnamese.
7. Draft a commit message if requested; do not automatically commit, push or deploy.
