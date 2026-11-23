# Class role nav + Teacher — ledger

**Date:** 2026-11-23  
**Plan:** Class role nav UX (Teacher create gate + browse split)

## Done

- [x] `Roles.Teacher` + `CanCreateClass` / `IsLearner` / `IsProtectedFromBillingRoleSync`
- [x] Admin UserManagement filter/edit + badges for Teacher
- [x] Billing SubscriptionService never overwrites Teacher (activate/expire/revoke/grant)
- [x] CreateClass `[Authorize(Roles = Teacher,Admin)]` + service double-check
- [x] `/Home/BrowseClasses` catalog; `/Home/Class` my classes; role-based browse visibility
- [x] Sidebar submenu: Khám phá lớp / Lớp học của tôi | Tạo lớp + Lớp tôi tạo (no Menu chính Khóa học edits)
- [x] Clay empty-state / actions / mobile stack for browse + mine buttons
- [x] Unit tests: ClassService + Teacher billing protection (22 passed via alternate OutputPath)
- [x] pstack-models.mdc: large + inherit-parent

## Verify notes

- `dotnet test …ClassServiceTests|Activate_Teacher|Expire_Teacher|GrantManual_Teacher` → **22 pass** (`-p:OutputPath=.appdata/local/test-out/class-role/`)
- Local Release `:5101`: Admin nav = Tạo lớp / Khám phá lớp / Lớp tôi tạo; Browse lists Public classes after JS onerror fix
- Student session (cookie login): submenu = Khám phá lớp + Lớp học của tôi (no Tạo); `GET /Home/CreateClass` → **403**; `SearchClass` returns Public classes (Khóa học Tiếng Anh, TOEIC 600+, Test)
- No schema migration (Role string column)

## Limits

- Production/CI/PR not run (stabilization: local only)
- Cookie renew after Admin role change relies on existing AccountSessionValidator
- `:5100` may still run old build; use `:5101` Release for this change
- Teacher end-to-end (Admin gán role rồi login) chưa làm browser — unit + Admin/Student đủ gate/catalog
