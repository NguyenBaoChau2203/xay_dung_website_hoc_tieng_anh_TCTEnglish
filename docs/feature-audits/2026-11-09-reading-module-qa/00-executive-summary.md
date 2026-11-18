# Reading Module QA — Executive Summary

**Date:** 2026-11-09  
**Scope:** Local verification only (MariaDB Docker + `http://localhost:5100`). Not CI. Not production.  
**Baseline:** Locked decisions in Reading Module Plan + implementation ledger (SRS Reading 1.1 file not present in repo).  
**Unit evidence:** `ReadingModuleTests` — 11 passed.

## Verdict

Module Reading **đã triển khai đủ luồng nghiệp vụ chính** (Admin CRUD/workflow/Excel, student library CEFR gate, study/submit/explanation, notebook/progress). Các kiểm soát Critical đã **xác minh trên browser/HTTP** trong phiên này:

| Critical check | Result |
|---|---|
| Anonymous → Admin/Study | 302 → Login |
| Student → Admin Reading | AccessDenied |
| Self-approve author | Rejected: "Authors cannot review their own readings." |
| Other Admin approve + publish | Passage #4 → Approved → Published (`PublishedByUserId=4`) |
| CEFR over-level Study | A1 student → B2 passage #5 → **404**; library ẩn B2 |
| Answer key before submit | Study HTML/API không chứa `IsCorrect` / explanation |
| Submit + explanation | Study #4 → `1/1`, hiện explanation/evidence/paraphrase; `SaveAnswer`/`SubmitAttempt` 200 |
| Excel template/export | Download/Export 200 + xlsx content-type |
| Excel valid import | Preview OK → Confirm → Draft #4 |
| Excel invalid file | Rejected, không ghi DB |

## Limits

1. `scripts/start-local.ps1` **fail** tại Shared-data Sync (`team.onboarding.reading`). Web được boot thủ công (MariaDB sẵn + `dotnet run`) — **app chạy ≠ Sync sạch**.
2. File SRS Reading 1.1 không có trong repo → gap map theo plan đã khóa.
3. Viewport mobile 390 quan sát library (sidebar collapsed). Desktop study verified; không chạy đủ ma trận zoom/keyboard toàn bộ.
4. Tài khoản `admin2@tct.local` tạo tạm trên local DB cho two-admin review (cùng hash mật khẩu admin local; không ghi secret vào báo cáo).
5. Dữ liệu QA prefix `QA-READ-*` và passage B2 #5 chỉ trên local Docker.

## Deliverables

| File | Content |
|---|---|
| [A-project-audit.md](A-project-audit.md) | Project / architecture / feature status |
| [B-website-inspection.md](B-website-inspection.md) | Browser/HTTP inspection evidence |
| [C-security-permission.md](C-security-permission.md) | AuthZ matrix |
| [D-test-scenarios.md](D-test-scenarios.md) | Scenario list |
| [E-test-cases.md](E-test-cases.md) | Detailed test cases |
| [F-risk-assessment.md](F-risk-assessment.md) | Risks |
| [G-test-execution-plan.md](G-test-execution-plan.md) | How to run formal testing |
| [H-open-issues.md](H-open-issues.md) | Blockers / unknowns |
