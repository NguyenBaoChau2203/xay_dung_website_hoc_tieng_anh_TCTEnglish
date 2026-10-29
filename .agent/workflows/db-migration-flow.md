---
description: Safe EF migration review
---

# Safe EF migration review


1. Read AGENTS.md and tct-ef-migration. Verify actual target/packages: currently
   net9.0 and EF Core 9.0.4. Inspect entity/configuration and intended data changes.
2. Record impact, existing-row compatibility, deployment assumptions and rollback.
   Add nullable columns or an explicitly reviewed default/backfill where required.
3. Generate only when requested. Inspect Up and Down for destructive operations;
   never generate DROP TABLE/COLUMN without explicit owner approval. For a rename,
   inspect RenameColumn instead of assuming a generated DROP/ADD preserves data.
4. Test against an isolated appropriate database; verify data retention and query
   behavior. Applying a migration to shared/production data is a separate action
   requiring authority; this workflow does not authorize it automatically.
5. Review inline, run build/relevant tests and document the limits of rollback.
   Never run database update or production backup commands merely from a template.
