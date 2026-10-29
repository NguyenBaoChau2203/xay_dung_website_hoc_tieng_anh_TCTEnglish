---
name: EF Core Migration
description: Generate a safe, reversible EF Core migration for TCT English with impact assessment and rollback awareness
---

# EF Core Migration

## When to Use
Use when adding new database tables, columns, indexes, or modifying schema for TCT English.

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
```bash
dotnet ef migrations add <MigrationName> --context DbflashcardContext
```

### Step 5 — Review Checklist
- [ ] `Up()`: only ADD operations (no DROP TABLE, no ALTER that truncates)
- [ ] `Down()`: correctly reverses `Up()`
- [ ] New nullable columns have no `NOT NULL` constraint without `defaultValue`
- [ ] Indexes named: `IX_TableName_ColumnName`

## Safety Rules (NEVER VIOLATE)
1. **NEVER** `DROP TABLE` without explicit user approval
2. **NEVER** change nullable to NOT NULL without `defaultValue`
3. **NEVER** rename columns — EF generates DROP + ADD (data loss)
4. **ALWAYS** make new columns nullable unless there's a valid default
5. **ALWAYS** backup production DB before applying migrations
