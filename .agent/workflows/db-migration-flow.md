---
description: Safe database schema change process with impact assessment and rollback plan
---

# Workflow: Database Migration — Safe Change Process

## Pre-Flight Safety Check
1. Is this additive only? (Add column/table) → SAFE
2. Does this change existing columns? → REQUIRES REVIEW
3. Does this remove columns/tables? → REQUIRES APPROVAL + BACKUP

**Rule**: If migration contains `DropTable` or `DropColumn` → STOP and ask user.

## Phase 1 — Entity Design
- New entity: `[Key]`, Data Annotations, nullable optional properties, FK + navigation
- New column on existing entity: ALWAYS nullable (safe for existing rows)

## Phase 2 — DbContext Registration
Add `DbSet<>` to `DbflashcardContext.cs`. Configure in `OnModelCreating` if needed (indexes, FK behavior).

## Phase 3 — Generate Migration
```bash
dotnet ef migrations add <DescriptiveName> --context DbflashcardContext
```
Naming: `Add<Entity>`, `Add<Column>To<Table>`, `AddIndexOn<Table><Column>`

## Phase 4 — Review Migration (Critical)
Checklist:
- [ ] No `DropTable`/`DropColumn` unless approved
- [ ] New nullable columns have no NOT NULL constraint
- [ ] Index names: `IX_TableName_ColumnName`
- [ ] `Down()` correctly reverses `Up()`
- [ ] No raw SQL with user data

## Phase 5 — Apply to Staging
Verify: app starts, new columns visible, existing data preserved, seeders don't re-seed.

## Phase 6 — Apply to Production
1. BACKUP production DB first
2. Apply migration
3. Restart application
4. Monitor for 5 minutes

## Rollback Plan
```bash
dotnet ef database update <PreviousMigrationName>
# WARNING: Only safe if no DROP operations were applied
# If destructive: restore from backup
```
