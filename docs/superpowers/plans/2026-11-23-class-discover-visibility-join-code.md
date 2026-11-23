# Class discovery + Public/Private + join code — ledger

**Spec plan:** owner Cursor plan `class_discover_visibility_e1b3e7ef`  
**Date:** 2026-11-23

## Done

- [x] Migration `AddClassVisibilityAndJoinCode` applied on local MariaDB
- [x] `SearchClassesAsync` Public-only + empty keyword suggest
- [x] `JoinClassByCodeAsync` + shared join pipeline (password / approval)
- [x] Class page discover + join-by-code modal
- [x] Create/Edit Public-Private + join code; create success card; Detail copy/regen
- [x] `ClassServiceTests` 14/14 passed

## Verify notes

- Local: `dotnet test --filter FullyQualifiedName~ClassServiceTests` via alternate OutputPath
- Browser multi-account UAT: run `scripts/start-local.ps1` then Public suggest + Private code join
- Responsive: CSS stacks actions at ≤575px; cover grid 3→2→1
