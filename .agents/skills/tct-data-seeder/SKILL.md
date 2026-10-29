---
name: tct-data-seeder
description: Use when creating or updating TCT English seed data with safe rerun behavior.
---

Follow `AGENTS.md` and the current source before applying examples. Paths below
are relative to `TCTEnglish/` unless explicitly prefixed. Use the same agent for
implementation and review. Examples do not authorize changing protected config.


# Data Seeder

## When to Use
Use when creating seed data for vocabulary sets, speaking videos, admin users, or any initial data.

## Critical Rule — IDEMPOTENCY
Every seeder MUST have a guard condition:
```csharp
if (await context.SpeakingVideos.AnyAsync()) return;  // Already seeded
```

## Seeder Pattern
```csharp
public static async Task SeedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

    // Guard condition — prevent duplicate seeding
    if (await context.<Entity>.AnyAsync()) return;

    // Seed data...
    await context.SaveChangesAsync();
}
```

## Seeder Types

### Vocabulary Seeder
- Create Set owned by Admin user
- Guard: check if specific set name exists
- Cards with Term, Definition, Phonetic, Example

### Speaking Video Seeder (JSON)
- Read from `wwwroot/data/speaking-videos.json`
- Create Playlist → Video → Sentences
- Guard: `if (await context.SpeakingVideos.AnyAsync()) return;`

### Admin User Seeder
- Guard: `if (await context.Users.AnyAsync(u => u.Role == "Admin")) return;`
- BCrypt hash with `workFactor: 12`
- Development/first-time setup only

## Registration in Program.cs
```csharp
// After app.Build(), before app.Run():
await SeedClass.SeedAsync(app.Services);
```

## Output
1. Seeder class with full idempotency guard
2. JSON data file (if needed) with sample entries
3. Program.cs registration line
4. Dependency notes (e.g., Admin user must exist before Set seeder)

A table-wide AnyAsync guard is only suitable for all-or-nothing bootstrap data.
For incremental seed updates, check stable per-item keys and verify reruns do
not duplicate or overwrite user data. Program.cs registration needs explicit
authorization under AGENTS.md; never create default production credentials.
