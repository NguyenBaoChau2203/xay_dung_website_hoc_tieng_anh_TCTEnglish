---
description: End-to-end workflow for building a complete new feature from spec to production-ready code
---

# Workflow: New Feature — End-to-End

## Phase 0 — Pre-Flight Checklist
Before writing any code:
1. Read `docs/project-structure.md` — confirm current folder boundaries and controller ownership
2. Read `.ai/context/known-issues.md` — check for existing related debt or warnings
3. Identify the right controller for the new feature (never grow `HomeController` with domain logic)

## Phase 1 — Feature Analysis
Understand requirements before writing any code:
1. Ask clarifying questions: "Does this need a new entity?", "Which roles can access?", "Real-time (SignalR)?", "Email notification?"
2. Output Feature Spec with: What, Who, Entities, External deps, Complexity estimate

## Phase 2 — Database Design
If new entity needed: design properties, navigation FK, add to `DbflashcardContext.cs`.
Checklist: Primary key `Id`, nullable new columns, `[Index]` attributes.

## Phase 3 — Service Layer
Create `Services/I<Feature>Service.cs` + `Services/<Feature>Service.cs`.
Checklist: all methods async, `.AsNoTracking()` on reads, ownership check on writes, register in `Program.cs`.

## Phase 4 — Controller Layer
Create `Controllers/<Feature>Controller.cs` or `Areas/Admin/Controllers/<Feature>ManagementController.cs`.
Checklist: `[Authorize]`, inherits `BaseController`, `GetCurrentUserId()`, `[ValidateAntiForgeryToken]` on POST, thin actions.

> **Do not add new domain flows to `HomeController`.** Each new feature domain belongs in its own dedicated controller.

## Phase 5 — ViewModel Layer
Create `ViewModels/<Feature>ViewModel.cs` with Data Annotations: `[Required]`, `[MaxLength]`, `[Range]`, `[Display]`.

## Phase 6 — View Layer
Create Razor Views in `Views/{Feature}/` with: `@model`, Bootstrap 5, `asp-validation-for`, `@section Scripts`, empty state handling.
Add navigation link in `_Layout.cshtml`.

> **Do not add feature views to `Views/Home/`.** Feature screens belong in `Views/{Controller}/`
> e.g. `Views/Study/`, `Views/Class/`, `Views/Folder/`.

## Phase 7 — JavaScript Layer
Create `wwwroot/js/<feature>.js`: anti-forgery token in POST, error handling, loading states, debounce search.

## Phase 8 — Security Review
Run security audit: Anti-IDOR, RBAC, CSRF, XSS, no sensitive data in URLs/logs.

## Phase 9 — Manual Testing
Test scenarios: happy path, empty state, unauthorized access, IDOR attempt, validation failure.

## Phase 10 — Commit & PR
Generate conventional commit message with proper type and scope.
