---
name: tct-feature-scaffold
description: Use when implementing a new TCT English feature that needs MVC, service or ViewModel scaffolding.
---

Follow `AGENTS.md` and the current source before applying examples. Paths below
are relative to `TCTEnglish/` unless explicitly prefixed. Use the same agent for
implementation and review. Examples do not authorize changing protected config.


# Feature Scaffold

## When to Use
Use when creating a new feature from scratch that needs Model, Service, Controller, ViewModel, and View layers.

## Pre-Scaffold Step
Before generating any files, read `docs/project-structure.md` to verify current folder boundaries and avoid placing files in the wrong location.

## Steps

### Step 1 — Feature Spec
Output a mini spec before writing code:
```
## Feature: <Name>
**What it does**: [1-sentence description]
**Affected entities**: [List EF entities created or modified]
**New files**: [List files to be created]
**Modified files**: [List existing files to modify]
**Auth requirement**: [Roles that can access]
```

### Step 2 — Database Layer (if new entity needed)
```csharp
// TCTEnglish/Models/<EntityName>.cs
public class <EntityName>
{
    public int Id { get; set; }
    // Properties with Data Annotations
    // Navigation properties with FK
}
```

Add to `DbflashcardContext.cs`:
```csharp
public DbSet<<EntityName>> <EntityNamePlural> { get; set; }
```

### Step 3 — Service Layer
```csharp
// TCTEnglish/Services/I<Feature>Service.cs
public interface I<Feature>Service
{
    Task<List<<Feature>ViewModel>> GetAllAsync(int userId);
    Task<<Feature>ViewModel?> GetByIdAsync(int id, int userId);
    Task<OperationResult> CreateAsync(<Feature>ViewModel model, int userId);
    Task<OperationResult> UpdateAsync(<Feature>ViewModel model, int userId);
    Task<OperationResult> DeleteAsync(int id, int userId);
}
```

> Use `OperationResult` (in `Services/OperationResult.cs`) as the return type for all mutating
> service methods. Do not use raw `bool` or re-throw exceptions to controllers.

### Step 4 — Controller Layer
```csharp
// TCTEnglish/Controllers/<Feature>Controller.cs
[Authorize]
public class <Feature>Controller : BaseController
{
    private readonly I<Feature>Service _service;
    // Constructor injection, thin actions, [ValidateAntiForgeryToken] on POST
}
```

### Step 5 — ViewModel Layer
```csharp
// TCTEnglish/ViewModels/<Feature>ViewModel.cs
// Data Annotations for validation: [Required], [MaxLength], [Range]
```

### Step 6 — Razor View
```cshtml
@model <Feature>ViewModel
<!-- Bootstrap 5 layout, asp-validation-for, @section Scripts -->
```

### Step 7 — Registration (only when explicitly authorized)
```csharp
// Program.cs: builder.Services.AddScoped<I<Feature>Service, <Feature>Service>();
```

## Output Rules
- Output ALL files in order: Model → Service → Controller → ViewModel → View → Registration
- Include file path header before each code block
- All DB queries: async + `.AsNoTracking()` on reads + ownership check on mutations
- All POST actions: `[ValidateAntiForgeryToken]`
- All sensitive actions: `[Authorize]` with appropriate role

## TCT English Constraints
- **Placement**: Controllers → `Controllers/` | Admin → `Areas/Admin/Controllers/` | Views → `Views/{Feature}/` | ViewModels → `ViewModels/`
- User identity: always use `GetCurrentUserId()` from `BaseController`
- Never pass raw EF entities to Views — always use ViewModels
- Service return type for mutations: use `OperationResult` not raw `bool`
- New entities: add to `DbflashcardContext` AND create EF migration instruction
- Bootstrap 5 layouts with scoped product CSS following DESIGN.md

Create only the layers the feature needs. Check the requested scope before editing
Program.cs, appsettings or project files; scaffolding alone is not permission.
