# Execution Prompt: Verify PR 145 Class integration

Use AGENTS.md and the PR 145 integration review plan. Recheck the exact PR head,
base and CI before any integration step. Preserve existing Class auth, chat and
antiforgery contracts. Confirm that unaffiliated users cannot view or join a
private class by ID, that invited learners can join through the new form, and
that a visibility change invalidates the former public code. Verify the legacy
class backfill, large IDs, unique index and migration rerun on disposable
MariaDB 10.5.23. Check Class, Browse, Create and Detail in the browser at desktop
and mobile widths. Report CI, provider and browser evidence in Vietnamese.
Follow the owner's current merge and deployment authority and preserve existing
local data and production gates.
