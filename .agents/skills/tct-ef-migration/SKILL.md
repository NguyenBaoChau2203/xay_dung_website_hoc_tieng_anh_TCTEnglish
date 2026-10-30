---
name: tct-ef-migration
description: Use when reviewing or generating authorized EF Core schema migrations for TCT English.
---

Follow `AGENTS.md` and the current source before applying examples. Paths below
are relative to `TCTEnglish/` unless explicitly prefixed. Use the same agent for
implementation and review. Examples do not authorize changing protected config.


# EF Core Migration

## When to Use
Use when adding new database tables, columns, indexes, or modifying schema for TCT English.
Also trigger automatically for persisted EF entity, DbContext, relationship or
mapping changes, even if the feature prompt never says "migration" or "deploy".
Follow AGENTS.md's "Database, PR and Release Rules (Automatic)".

## Migration Protocol

### Step 1 — Impact Assessment
Before generating any migration code, output:
```
## Migration Impact: <migration-name>
**Type**: [Additive | Schema Change | Data Migration | Destructive ⚠️]
**Tables affected**: [List]
**Backward compatible**: [Yes/No]
**Data loss risk**: [None / Low / HIGH ⚠️]
```

### Step 2 — Entity Update
```csharp
// TCTEnglish/Models/<EntityName>.cs
// New columns MUST be nullable (safe for existing rows):
public string? Topic { get; set; }
public int? Level { get; set; }
```

### Step 3 — DbContext Configuration (if needed)
```csharp
// In DbflashcardContext.OnModelCreating:
modelBuilder.Entity<SpeakingVideo>(entity =>
{
    entity.Property(e => e.Topic).HasMaxLength(100);
    entity.HasIndex(e => e.Topic);
});
```

### Step 4 — Migration Command
Run from the repository root; replace `AddYourFeatureSchema` with a meaningful name.
Use the repository tool manifest (EF 9.0.4), not a different global EF version.

```powershell
dotnet tool restore
dotnet ef migrations add AddYourFeatureSchema --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
```

### Step 5 — Review Checklist
- [ ] `Up()`: operations match the reviewed impact; no destructive DROP/truncating ALTER without explicit approval
- [ ] `Down()`: correctly reverses `Up()`
- [ ] New nullable columns have no `NOT NULL` constraint without `defaultValue`
- [ ] Indexes named: `IX_TableName_ColumnName`
- [ ] Replacement indexes support existing foreign keys before old indexes are removed
- [ ] Already-applied migrations/history remain unchanged; no invented baseline

### Step 6 — Validate and Include in the Feature PR

- Include the migration, generated Designer metadata and model snapshot together
  with the dependent code; never defer generation to the reviewer or release job.
- Run build/relevant tests, the pending-model check, and a disposable MariaDB
  upgrade/repeat check. Preserve affected rows/relationships; record evidence.
- Describe tables/columns/indexes, compatibility/data risks, validation and any
  blocker in the PR. If a persisted-model change needs no migration, explain why.
- Use local Docker/User Secrets; do not test against the live hosting database.
- The enabled production release applies committed pending migrations after
  backup. Do not generate migrations during deploy or sync the dev database to
  hosting. Check the actual gate/release result before saying it was deployed.

## Safety Rules (NEVER VIOLATE)
1. **NEVER** `DROP TABLE` without explicit user approval
2. **NEVER** change nullable to NOT NULL without `defaultValue`
3. For renames, inspect generated operations and use RenameColumn where appropriate; never accept a DROP + ADD as a data-preserving rename
4. **ALWAYS** make new columns nullable unless there's a valid default
5. **ALWAYS** backup production DB before applying migrations
