# Chau UI ownership and design-system adoption scope

Date: 2026-11-08 (Asia/Bangkok).
Branch: `feature/chau-ui-design-system`.
Baseline: `eed4e2c95c79ef708dc9af77c8b9326186835122` on `master`, equal to
`origin/master` after fetch. The working tree was clean before branch creation.

## Outcome and current authority

The owner requested an inventory of Chau's responsibilities and UI screens, then
a local branch for future UI work. This task creates the branch and this scope
document. It does not implement a redesign or grant commit, push, PR, merge,
hosting, production, paid-AI or database-content authority for future work.

- [x] Read the team assignment and historical ownership clarification.
- [x] Verify the shared design system is already in the current checkout.
- [x] Check current controller routes and view-file inventory.
- [x] Create the local topic branch from current master.
- [x] Save the screen inventory, collaboration boundaries and next-work checklist.
- [ ] Implement and browser-verify individual screen batches when requested.

## Evidence and interpretation

- [README team assignment](../../../README.md): Chau owns account/auth/security,
  SMTP, Speaking, Writing, Goals/badges, AI Chat, Admin dashboard, user management,
  Speaking/Writing content administration and AI administration.
- [Historical ownership clarification](../../feature-audits/2026-10-30-bao-chau-branch-closeout.md):
  reliability improvements to Notifications did not transfer its ownership from
  Trong to Chau. A commit author's identity alone is not screen ownership.
- [Earlier UI inventory](2026-10-29-bao-chau-ui-audit-plan.md) is supporting history.
  The current scope adds the existing AccessDenied view and lists Contact
  separately because README does not explicitly assign that page to Chau.
- Design-system PR #130 is merged at `f716e4e`; the current baseline also includes
  Hybrid RAG PR #131. Older ledger statements saying the foundation is local-only
  describe their original verification time, not the current Git publication.
- This is a source-based inventory. No screen was opened in a browser during
  this task, and adoption/completion is not inferred from a file's existence.

## Core screen inventory: 25 view files

Paths below are relative to the repository root. Login and registration are two
states of one view; modal dialogs and shared partials are not counted as separate
pages. Routes identify source entry points, not browser-acceptance evidence.

| ID | Screen / states | Route or entry flow | Current view |
| --- | --- | --- | --- |
| AU01 | Đăng nhập / đăng ký | `/Account/Login`, `/Account/Register` | `TCTEnglish/Views/Account/Auth.cshtml` |
| AU02 | Quên mật khẩu | `/Account/ForgotPassword` | `TCTEnglish/Views/Account/ForgotPassword.cshtml` |
| AU03 | Xác nhận yêu cầu quên mật khẩu | Result of the recovery flow; do not assume a standalone GET | `TCTEnglish/Views/Account/ForgotPasswordConfirmation.cshtml` |
| AU04 | Đặt lại mật khẩu | `/Account/ResetPassword` with valid recovery state | `TCTEnglish/Views/Account/ResetPassword.cshtml` |
| AU05 | Hồ sơ, ảnh đại diện, huy hiệu | `/Account/Profile` | `TCTEnglish/Views/Account/Profile.cshtml` |
| AU06 | Cài đặt tài khoản, bảo mật, liên kết | `/Account/Settings` | `TCTEnglish/Views/Account/Settings.cshtml` |
| AU07 | Không đủ quyền truy cập | `/Account/AccessDenied` | `TCTEnglish/Views/Account/AccessDenied.cshtml` |
| SP01 | Thư viện Speaking, video riêng, import status | `/Speaking`, `/Speaking/Index` | `TCTEnglish/Views/Speaking/Index.cshtml` |
| SP02 | Luyện nói video, shadowing, dictation | `/Speaking/Practice/{id}` | `TCTEnglish/Views/Speaking/Practice.cshtml` |
| SP03 | Luyện nói theo bộ thẻ | `/Home/Speaking/{id}` with an accessible set | `TCTEnglish/Views/Study/Speaking.cshtml` |
| WR01 | Chọn nội dung / cấp độ Writing | `/Home/Writing` | `TCTEnglish/Views/Study/Writing.cshtml` |
| WR02 | Danh sách bài, bộ lọc, bài riêng / AI | `/Home/Writing/Exercises` | `TCTEnglish/Views/Study/WritingExercises.cshtml` |
| WR03 | Làm bài, gợi ý, kết quả, bản nháp | `/Home/Writing/Practice?exerciseId=...` | `TCTEnglish/Views/Study/WritingPractice.cshtml` |
| GO01 | Mục tiêu, streak, XP, huy hiệu, goal editor | `/Goals` | `TCTEnglish/Views/Goals/Index.cshtml` |
| AI01 | AI Chat đầy đủ, lịch sử, nguồn tham chiếu | `/AI/Chat` | `TCTEnglish/Views/Ai/Chat.cshtml` |
| AI02 | AI Chat nhúng | `/AI/Chat?embed=true` | `TCTEnglish/Views/Ai/ChatEmbed.cshtml` |
| AD01 | Dashboard quản trị | `/Admin/Dashboard` | `TCTEnglish/Areas/Admin/Views/Dashboard/Index.cshtml` |
| AD02 | Quản lý người dùng, quyền, khoá / mở khoá | `/Admin/UserManagement` | `TCTEnglish/Areas/Admin/Views/UserManagement/Index.cshtml` |
| AD03 | Danh sách video Speaking | `/Admin/SpeakingVideoManagement` | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml` |
| AD04 | Thêm video Speaking | `/Admin/SpeakingVideoManagement/Create` | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Create.cshtml` |
| AD05 | Sửa video Speaking | `/Admin/SpeakingVideoManagement/Edit/{id}` | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Edit.cshtml` |
| AD06 | Danh sách bài Writing | `/Admin/WritingExerciseManagement` | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml` |
| AD07 | Thêm bài Writing | `/Admin/WritingExerciseManagement/Create` | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Create.cshtml` |
| AD08 | Sửa bài Writing, đáp án được duyệt | `/Admin/WritingExerciseManagement/Edit/{id}` | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Edit.cshtml` |
| AD09 | Quản trị AI, classifier, retrieval / vector index | `/Admin/AiManagement` | `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml` |

Related UI surfaces must be reviewed with their owning page:

- AI: `Views/Ai/_ChatShell.cshtml`, `_ChatSources.cshtml`, the floating launcher
  and its embedded host. Preserve RAG source links, retrieval controls, usage,
  pending/retry states and durable turn behavior introduced by PR #131.
- Writing Admin: `_ExerciseForm.cshtml`, `_ExerciseFormScripts.cshtml` and
  `_AcceptedAnswersPanels.cshtml` in `Areas/Admin/Views/WritingExerciseManagement`.
- Profile/Goals: badge and editor states belong to existing views; there is no
  additional standalone badge page in this inventory.
- SMTP, auth authorization and infrastructure are responsibilities but are not
  additional UI pages. A UI pass does not authorize rewriting those services.

## Collaboration boundaries

| Category | Treatment |
| --- | --- |
| Contact: `/Home/Contact`, `Views/Home/Contact.cshtml` | Previously improved in Chau's UI scope and tied to SMTP, but not explicitly assigned in README. Coordinate before including it as a redesign batch. |
| Trong's screens | Landing, Listening, Vocabulary, sets/cards/folders, payment/Premium, Notifications and their corresponding Admin screens stay outside this branch's default UI scope. |
| Tien's screens | Learner dashboard, shared learner shell/sidebar/footer, Reading, Class, global search and class chat stay outside this branch's default UI scope. |
| Shared foundation | `tct-learning-ui.css`, `layout.css`, `site.css`, both layouts and shared partials require a separate shared-impact review. Chau's Admin pages do not imply ownership of every Admin module. |

## Use the existing design system

Read [DESIGN.md](../../../DESIGN.md) and
[team UI workflow](../../design-system/team-ui-workflow.md). Use the existing
[page execution prompt](../../../.ai/prompts/implement-page-with-team-design-system.md)
for a later implementation request. The actual token/component source is
`TCTEnglish/wwwroot/css/tct-learning-ui.css`; the
[gallery](../../design-system/preview.html) loads it directly.

Reuse `.tct-theme`, `--tct-*`, Bootstrap, `_PageHeader` and `_StateMessage` where
appropriate. Keep page CSS scoped. Do not create a replacement design system,
palette, font family, icon dependency or global theme change. Preserve selectors,
form/antiforgery fields, access controls and HTTP/JSON/SignalR contracts.

## Suggested implementation batches (pending)

1. Account: AU01-AU07. Include standalone recovery, validation, blocked-account
   states and account dialogs; preserve the current OAuth and security flows.
2. Goals: GO01. Preserve actual progress, reward semantics and editor validation.
3. Writing: WR01-WR03. Keep local drafts, previous attempts, accepted/review
   outcomes and Premium/quota states visible and accurate.
4. Speaking: SP01-SP03. Preserve media/recording controls, import jobs,
   cancellation, assessment and authoritative progress. Keep Whisper retired.
5. AI Chat: AI01-AI02 plus their shared partials and embedded surface. Preserve
   the current Hybrid RAG behavior rather than following pre-RAG screenshots.
6. Admin: AD01-AD09, subdivided into dashboard/users, Speaking, Writing and AI.
   Keep Standard/Premium/Admin behavior and explicit maintenance safeguards.
7. Contact only after ownership coordination; shared-shell proposals separately.

For each requested batch: inspect the exact current views/callers with CodeGraph,
capture the before state, implement locally, run relevant build/regressions and
verify actual pages at 1440, 768 and 375/390 widths, 200% zoom, keyboard navigation,
long text, and loading/empty/error/success/pending states. Use synthetic test data
and ignored screenshots. Record unobserved states honestly. Update the adoption
tracker only for screens actually migrated and verified.

When local web startup is requested, use `scripts/start-local.ps1 -NoBrowser`
with the saved local key and Docker MariaDB; preserve existing listeners. Do not
send paid AI/audio requests, send mail or alter shared content just to obtain a
screenshot. UI-only changes need no empty shared-data export package.

## Verification for this preparation task

Current assignment, merged design-system availability, controller entry points,
view-file existence, branch base and Git diff were checked. No application source,
database, key, hosting or deployment gate was changed. Application build/tests
and browser checks are deferred to the actual UI implementation; this document
does not claim those checks passed.
