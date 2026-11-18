# Plan: Admin pages of Trọng's modules — vocabulary sets, listening lessons, payments, notifications

Save as `docs/superpowers/plans/2026-11-18-admin-content-complete.md`.
Execution prompt: `.ai/prompts/claude-code-admin-content-complete.md`.

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Phạm vi:** 4 khu Admin của bạn — Bộ từ vựng (`/Admin/SetManagement`), Bài nghe (`/Admin/ListeningManagement`), Thanh toán (`/Admin/BillingManagement`), Thông báo (`/Admin/NotificationManagement`).
> - **Lỗi tìm được (mục 3), nặng nhất:**
>   1. **Admin có thể đặt bộ thẻ vào thư mục của người khác.** Theo luật chia sẻ thư mục, chủ thư mục, người đã lưu và cả lớp học sẽ xem được bộ thẻ riêng tư đó: lộ dữ liệu.
>   2. **Admin tạo bộ thẻ thì mặc định là "Công khai",** kể cả khi tạo cho người dùng.
>   3. **Xóa bộ thẻ trong Admin bỏ qua dịch vụ xóa chuẩn** (không xử lý người đang theo học, liên kết chia sẻ), có thể lỗi 500.
>   4. **Sửa hoặc đổi tên bộ từ vựng hệ thống có thể bị ghi đè hoặc nhân đôi** khi đồng bộ từ `system-vocabulary.json`.
>   5. **Quiz bài nghe:**
>      - để trống phương án A/B hoặc phần nghĩa trong từ vựng là lỗi 500;
>      - có thể chọn đáp án đúng là một phương án đang trống, khiến học viên không trả lời đúng được.
>   6. **Mã YouTube sai vẫn được lưu** (ảnh bìa 404 đang thấy ở trang Nghe); trình độ không được kiểm tra ở máy chủ.
>   7. **Xuất bản được bài nghe chưa có lời thoại;** nhập lời thoại từ YouTube ghi đè bản cũ không hỏi.
>   8. **Vào trang với `page=0` hoặc số âm là lỗi 500.**
>   9. **Admin sửa được bài nghe riêng của học viên.**
>   10. **Trang Thanh toán còn chữ tiếng Anh hoặc không dấu** ("Provider Health", "Don pending qua han"), tiền ghi "399,000 VND".
> - **Giao diện mới (6 mockup ở `Claude outputs/admin-mockups/`):** bám đúng mẫu Admin Châu đã làm (trang Users): tiêu đề có biểu tượng, 4 thẻ số liệu, khung lọc, bảng có số đếm, nút biểu tượng. Thêm:
>   - trang thẻ của một bộ với cột "Kiểm tra" (thiếu ví dụ/IPA);
>   - trang sửa bài nghe gom 4 tab (Thông tin / Lời thoại / Quiz / Từ vựng), báo lỗi ngay khi nhập;
>   - trang Thanh toán có số liệu doanh thu và đơn cần kiểm tra;
>   - trang gửi thông báo có xem trước và đếm người nhận.
> - **Không làm:** không migration, không sửa logic tiền (đã làm ở đợt thanh toán), không sửa menu Admin dùng chung trừ khi bạn chọn D1.
> - **Bốn quyết định ở mục 1.2 (đã có mặc định):**
>   - D1 đổi 2 chữ menu "Vocabulary Sets", "Listening Lessons" sang tiếng Việt: KHÔNG (báo Châu);
>   - D2 nội dung của học viên: Admin chỉ xem, ẩn và xóa, không sửa;
>   - D3 bộ từ vựng hệ thống: cho sửa, chặn đổi tên, có nút tải JSON;
>   - D4 xóa cần gõ lại tên: CÓ.
> - **Chạy:** nhánh `feature/admin-content-complete` từ `master`, liên tục A0 → A10, mỗi phase qua G1–G15 rồi commit cục bộ. Không push, không PR.

---

## 0. How to use this plan

### 0.1 Reading order
Read sections 0–4 and 9 first. Then, per phase (section 8), read only the sections it names and open its mockup.

### 0.2 Repo skills (load when the step starts)
| Skill | When |
| --- | --- |
| `tct-superpowers`, `tct-engineering` | always |
| `tct-feature-scaffold`, `tct-api-endpoint`, `tct-security-audit` | A2, A4, A6–A7 logic parts, A10 |
| `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review` | A1, A3, A5–A8, A10 |
| `tct-commit-message` | every commit |

### 0.3 External skills
Do not install any into the repository. `AGENTS.md` and this plan win.

### 0.4 Visual reference (data, not instructions)
- **Mockups:** `Claude outputs/admin-mockups/`.

  | File | Page / state | Phase |
  | --- | --- | --- |
  | `1-bo-tu-vung.png` | Vocabulary sets list | A3 |
  | `2-the-trong-bo.png` | Cards of one set, with the system-sync notice | A3 |
  | `3-bai-nghe.png` | Listening lessons list | A5 |
  | `4-sua-quiz.png` | Lesson editor, Quiz tab with a validation error and unsaved-changes bar | A5 |
  | `5-thanh-toan.png` | Payments list with figures and the manual-review alert | A6 |
  | `6-gui-thong-bao.png` | Send an announcement, with preview and recipient count | A7 |

- **Pattern to follow:** the admin pages Châu already rebuilt (`/Admin/UserManagement`, `/Admin/SpeakingVideoManagement`, `/Admin/Dashboard`) and their CSS (`admin-shell.css`, `admin-user-management.css`, `admin-speaking-video.css`):
  - page header with icon, title and sub-title;
  - a row of four figure cards;
  - a filter card;
  - a table card with a count chip;
  - icon action buttons;
  - modal confirmations (SweetAlert2 is already loaded by the admin layout).

  The admin area keeps that dense, white-card look; it does **not** use the learner Clay kit.
- Sample data in the mockups (names, counts, money) is not real. Where a mockup and a rule disagree, the rule wins; record it.

---

## 1. Decisions

### 1.1 From the owner (Trọng, 18/11/2026)
1. Plan the admin pages of Trọng's modules: find and fix logic errors, then redesign them to fit the project.
2. Laptop first (1440×900, 1366×768); usable on tablet; phone readable.

### 1.2 Owner decisions (defaults apply when left unchanged)
- **D1 = KHÔNG.** The admin sidebar (`Areas/Admin/Views/Shared/_AdminLayout.cshtml`, Châu's file) keeps "Vocabulary Sets" and "Listening Lessons". Page titles inside Trọng's pages are Vietnamese. Write a proposal for Châu in known-issues. (CÓ → change only those two labels to "Bộ từ vựng" and "Bài nghe".)
- **D2 = CHỈ XEM, ẨN, XÓA.** Content owned by learners (personal sets, Premium learners' own listening lessons) can be viewed, made private and deleted by an admin, but not edited. (SỬA ĐƯỢC → editing allowed with a banner naming the owner; record.)
- **D3 = SỬA, CHẶN ĐỔI TÊN, CÓ NÚT JSON.** System vocabulary sets (owner `system@tct.local`) can be edited in the admin; renaming a system set is blocked, because the seeder matches sets by name. A "Tải JSON" action exports the set in the exact `system-vocabulary.json` shape so the file can be updated by hand. (CHO ĐỔI TÊN → allowed with a warning; record.)
- **D4 = CÓ.** Deleting a set or a lesson asks for the exact name to be typed in a confirmation dialog (same pattern as learner "Xóa bộ thẻ").

### 1.3 Design decisions made in this plan
| # | Decision | Why |
| --- | --- | --- |
| P1 | Admin logic moves out of the two controllers into `IAdminVocabularyService` and `IAdminListeningService` (`Services/Admin/`) | Testable rules; thin controllers (repo architecture rules) |
| P2 | Set deletion reuses `IPersonalSetService.DeleteSetAsync(isAdmin: true)` | One correct deletion path |
| P3 | A set's folder must belong to the set's owner | Folder sharing grants access (F-S1) |
| P4 | New system sets are owned by the system user and are Public; admins no longer pick an arbitrary owner | Admin content = system content |
| P5 | URL rules in one place (`Services/Admin/AdminInputRules.cs`): images `https://…` or local `/…`; YouTube id parser for watch, short, embed, shorts and nocookie links | Bad data never reaches learner pages |
| P6 | The lesson editor is one page with four tabs; each tab saves itself; unsaved changes are guarded | Today four separate pages each redirect to the index |
| P7 | Styles in one new file `wwwroot/css/admin-content.css`, scoped `.ac-page`, using the admin-shell tokens and the `--tct-*` tokens, no hard-coded colours | Matches Châu's admin pages without editing them |
| P8 | Money in admin as `399.000 ₫`; times in Vietnam time (existing `BillingTime` / business clock) | Same as learner pages |
| P9 | No migration; no change to payment logic or notification logic | Both were just hardened |

---

## 2. Scope

### 2.1 May add
- **Services:** `Services/Admin/IAdminVocabularyService.cs`, `AdminVocabularyService.cs`, `IAdminListeningService.cs`, `AdminListeningService.cs`, `AdminInputRules.cs`, `SystemVocabularyExport.cs`.
- **View models and views:**
  - `Areas/Admin/ViewModels/*` additions;
  - new partials under `Areas/Admin/Views/SetManagement/`, `ListeningManagement/` and `Areas/Admin/Views/Shared/_Ac*.cshtml` (only new files whose names start with `_Ac`).
- **Front end:** `wwwroot/css/admin-content.css`, `wwwroot/js/admin-content.js` (shared helpers: confirm-by-name, unsaved-changes guard, char counters), `wwwroot/js/admin-listening-editor.js`.
- **Tests:** `AdminVocabularyServiceTests.cs`, `AdminListeningServiceTests.cs`, `AdminInputRulesTests.cs`, `AdminContentPagesIntegrationTests.cs`, `scripts/tests/test_admin_content.cjs`.

### 2.2 May edit
- `Areas/Admin/Controllers/SetManagementController.cs`, `ListeningManagementController.cs`.
- **Billing and notification admin, presentation only:**
  - `BillingManagementController.cs`: only the figures and listing query of `Index`, plus messages;
  - `NotificationManagementController.cs`: only the recipient count and preview data for `Create`;
  - no payment or notification state logic.
- `Areas/Admin/Views/{SetManagement,ListeningManagement,BillingManagement,NotificationManagement}/**`.
- `Areas/Admin/ViewModels/*` for these modules (additive).
- `Program.cs`: **only** DI registration lines for the two new services, next to existing registrations.
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`: only the two labels, and only with D1 = CÓ.
- **Tests:** Trọng's own admin tests (`AdminCatalogPagingTests` if it covers these pages, `AdminBillingManagementTests`, `AdminBillingStateRulesTests`, `AdminAnnouncementTests`) where markup or copy changes on purpose (record each).
- **Docs:** tracker row "Admin của các module còn lại" (split into the four modules), `.ai/context/known-issues.md`, `bug-fix-log.md`, `project-structure.md`, this plan's section 12.

### 2.3 Forbidden
- `appsettings*.json`, `.csproj`, migrations, `Models/**` (including `JsonVocabularySeeder.cs` and `wwwroot/data/system-vocabulary.json`), and the rest of `Program.cs`.
- **Payment logic:** `Services/Billing/**` except read-only use; `BillingManagementController` actions other than `Index` presentation.
- **Notification logic:** `Services/NotificationService.cs` and the worker.
- `PersonalSetService*` (call it, do not change it), `ListeningService` (learner side).
- **Shared CSS and layouts:** `admin-shell.css`, `admin-user-management.css`, `admin-speaking-video.css`, `tct-learning-ui.css`, `layout.css`, `site.css`, `_AdminLayout.cshtml` (except D1).
- Other owners' admin areas (Users, Classes, Speaking, Writing, Reading, AI, Messages, Dashboard) and their tests.
- New packages, fonts or CDNs.

### 2.4 Must stay unchanged
- Admin routes and action names (bookmarks and links from the dashboard).
- `[Authorize(Roles = Admin)]` and antiforgery on every POST.
- Learner pages read the same data:
  - system vocabulary (`/Vocabulary`);
  - listening catalog (`/Home/Listening`, C2 level contract, `OwnerUserId == null && IsPublished` for the catalog);
  - personal sets.
- Billing state transitions, audit records and notification batches.

---

## 3. Findings (static review of `master` at `c641c3e`, 2026-11-18; reproduce or disprove each at A0)

Severity: **A** data exposure or data loss, **B** wrong behaviour or crash, **C** polish.

### 3.1 Vocabulary sets (`SetManagementController`)
| # | Sev | Finding |
| --- | --- | --- |
| S1 | A | `Create`/`Edit` accept any `FolderId` of any user. `PersonalSetService.CanView` grants access to sets in a folder the viewer owns, saved, or that is shared with their class, so placing a private set in another user's folder exposes it to that user, their followers and class members. |
| S2 | A | `Create` never sets `Visibility`, so the model default `"Public"` applies, including for sets created for a learner via the `OwnerId` dropdown. The dropdown lists every user (including blocked and anonymised accounts) and folders of every user. |
| S3 | B | `Delete` removes progress, cards and the set by hand. It skips subscriptions, share slug and clone links, and the single transaction that `PersonalSetService.DeleteSetAsync` uses. Possible FK error (500) and inconsistent data. |
| S4 | B | The seeder (`JsonVocabularySeeder`, when `Database:SeedOnStartup` is on) matches system sets by **name** and deletes cards not in the JSON. Admin card edits on system sets are lost; renaming a system set creates a duplicate on the next sync. The page gives no warning. |
| S5 | B | `Index?page=0` or a negative page → negative `Skip` → 500 (same in `ListeningManagement`). A page beyond the last shows an empty table. |
| S6 | B | Card fields are not trimmed or length-checked (`Term` 255 and other column limits) → database error 500 on long input. `ImageUrl` accepts any string (hot-links, `http:` mixed content, `javascript:`). |
| S7 | B | `EditCard` redirects with the posted `SetId` (not the card's). The admin form cannot edit fields the learner pages use: `Emoji` (word picture), `PhoneticUk`, `Cefr`, `PartsOfSpeech`, `MeaningEn`. |
| S8 | C | The cards page loads every card without search or paging; there is no quality signal (missing example, IPA or meaning). |
| S9 | C | Copy: "No Name", mixed English; no structured log of who deleted what. |

### 3.2 Listening lessons (`ListeningManagementController`)
| # | Sev | Finding |
| --- | --- | --- |
| L1 | A | The index mixes system lessons with Premium learners' private lessons (`OwnerUserId != null`), and the admin can edit their content and publish flag (D2). |
| L2 | B | `NormalizeYoutubeId` falls back to the raw input, so a wrong value (full URL of an unknown shape, typo) is stored; `/embed/`, `/shorts/`, `m.youtube.com` and `youtube-nocookie` links are not parsed. Broken embeds and 404 thumbnails follow (see SWEEP-LISTENING-YOUTUBE-THUMB-404). |
| L3 | B | `Level` is only limited by the `<select>`; the server accepts any string, breaking the A1–C2 catalog contract. |
| L4 | B | `ManageQuiz`: `OptionA.Trim()` / `OptionB.Trim()` throw on empty values (500). `CorrectAnswer` is not checked: it can be empty, not A–D, or point at an empty option C/D, making the question unanswerable. Lengths (500) not checked. |
| L5 | B | `ManageVocab`: `Definition.Trim()` throws when empty (500); `ImageUrl` unchecked. |
| L6 | B | `ManageTranscript`: start/end times not checked (negative, end before start, overlapping); every save redirects to the index, losing the editing place. |
| L7 | B | `ImportTranscript` replaces existing lines without confirmation; a missing YouTube id returns a plain-text 400; the exception message is shown to the admin; double click runs two imports. |
| L8 | B | A lesson can be published with no transcript (empty lesson in the catalog); no warning when the quiz or vocabulary is empty. |
| L9 | C | Deleting a lesson silently deletes every learner's progress for it; the confirmation does not say how many. |
| L10 | C | `ThumbnailUrl`, `AudioUrl` unchecked; `Duration` free text ("4:12", "04:12", "4 phút"). |

### 3.3 Payments admin (`BillingManagement`, presentation only)
| # | Sev | Finding |
| --- | --- | --- |
| P1 | C | English or unaccented copy: "Payment Provider Health", "Provider Health", "Quản lý subscription", "Danh sách payment orders", the button "Don pending qua han"; money as "399,000 VND". |
| P2 | C | No figures (revenue, active Premium, pending, manual review); orders in `manual_review` are not surfaced on the list. |
| P3 | C | Visual style differs from the rebuilt admin pages (button colours, table density, wrapped action buttons). |

### 3.4 Notifications admin (presentation only)
| # | Sev | Finding |
| --- | --- | --- |
| N1 | C | No preview of how the announcement will look, no recipient count before sending, no confirmation step; character counters missing. |

### 3.5 Checked and correct
- Admin role on all four controllers.
- Antiforgery on POSTs.
- The billing state machine, single confirmation path and audit (from the billing work).
- Announcement batching, recipient filter and history (from the notifications work).

---

## 4. Backend specification

### 4.1 `AdminInputRules` (pure, unit-tested)
- `TryParseYoutubeId(string? input, out string? id)`:
  - accepts an 11-character id;
  - also accepts `youtube.com/watch?v=`, `m.youtube.com/watch?v=`, `youtu.be/{id}`, `youtube.com/embed/{id}`, `youtube.com/shorts/{id}` and `youtube-nocookie.com/embed/{id}`, with extra query parameters allowed;
  - otherwise returns false.
- `IsSafeImageUrl(string? url)`: empty, `https://…` (absolute, valid host), or a local path starting with a single `/`; nothing else.
- `IsSafeMediaUrl` (audio): same rule.
- `NormalizeDuration(string? input)`: `m:ss` / `mm:ss` / `h:mm:ss` → normalised `mm:ss` or `h:mm:ss`; otherwise null with an error.
- `CefrLevels = { A1, A2, B1, B2, C1, C2 }`.
- `NormalizeText(string? s, int max)`: trims, collapses inner whitespace runs in single-line fields, and reports when the text is over the limit.

### 4.2 `IAdminVocabularyService`
```csharp
Task<AdminSetListResult> ListSetsAsync(AdminSetQuery query, CancellationToken ct);       // source=system|user|all, folder, visibility, q, sort, page (clamped)
Task<AdminSetFigures> GetFiguresAsync(CancellationToken ct);                              // system sets, system cards, user sets, system cards missing example or IPA
Task<AdminSetForm?> GetSetFormAsync(int? setId, CancellationToken ct);
Task<OperationResult<int>> CreateSystemSetAsync(AdminSetInput input, int adminId, CancellationToken ct);
Task<OperationResult> UpdateSetAsync(int setId, AdminSetInput input, int adminId, CancellationToken ct);
Task<OperationResult> SetVisibilityAsync(int setId, string visibility, int adminId, CancellationToken ct); // used for learner sets (D2)
Task<OperationResult> DeleteSetAsync(int setId, string confirmName, int adminId, CancellationToken ct);
Task<AdminCardListResult?> ListCardsAsync(int setId, AdminCardQuery query, CancellationToken ct); // search, topic, quality filter, page 50
Task<OperationResult<int>> AddCardAsync(int setId, AdminCardInput input, int adminId, CancellationToken ct);
Task<OperationResult> UpdateCardAsync(int cardId, AdminCardInput input, int adminId, CancellationToken ct);
Task<OperationResult> DeleteCardAsync(int cardId, int adminId, CancellationToken ct);
Task<SystemVocabularyExportResult?> ExportSystemSetAsync(int setId, CancellationToken ct);
```
Rules:
- **System set** = owner email `system@tct.local`. **Learner set** = any other owner.
- **Create** (S2, P4):
  - owner = the system user;
  - `Visibility = "Public"`;
  - folder must be a folder owned by the system user, or none;
  - name 1–200 characters (check the column limit at A0), unique among system sets ignoring case;
  - `CreatedAt` / `UpdatedAt` set.
- **Update:**
  - **System set:** description and folder (system folders only) can change. The name is read-only (D3), with the message "Không đổi tên bộ từ vựng hệ thống: tệp dữ liệu đồng bộ theo tên. Hãy tạo bộ mới nếu cần.".
  - **Learner set:** only `SetVisibilityAsync` to `Private` is allowed (D2); `UpdateSetAsync` returns `Forbidden` with "Không sửa nội dung bộ thẻ của học viên." and the controller answers 403 → friendly message.
  - The folder, if any, must belong to the set's owner (S1); otherwise "Thư mục không thuộc chủ sở hữu của bộ thẻ.".
- **Delete:**
  - the typed name must match exactly (D4);
  - the service calls `IPersonalSetService.DeleteSetAsync(setId, adminId, isAdmin: true, confirmName, ct)` (S3);
  - writes one structured log line (admin id, set id, owner id, card count).
- **Cards:**
  - Term 1–255, Definition 1–(column limit), Phonetic/PhoneticUk ≤ 100, Emoji ≤ 16 (one grapheme cluster recommended), Topic ≤ column limit, Example/ExampleTranslation ≤ column limit;
  - trimmed;
  - `ImageUrl` through `IsSafeImageUrl` (S6, S7).
  - Learner sets: cards are read-only (D2).
  - The redirect after save uses the card's real set id.
- **Quality flags per card:** `MissingExample`, `MissingPhonetic`, `MissingPicture` (no emoji and no image).
- **Export (D3):**
  - JSON of one set in the exact shape of `wwwroot/data/system-vocabulary.json` (read the file at A0 and mirror its property names and order);
  - `Content-Disposition: attachment; filename="system-vocabulary-{slug}.json"`;
  - system sets only.
- **Paging:** page < 1 → 1; page > last → last (S5).

### 4.3 `IAdminListeningService`
```csharp
Task<AdminLessonListResult> ListLessonsAsync(AdminLessonQuery q, CancellationToken ct); // source=system|learner|all (default system), level, status=published|draft|incomplete, q, page clamped
Task<AdminLessonFigures> GetFiguresAsync(CancellationToken ct);                         // system, published, draft or incomplete, learner-owned
Task<AdminLessonEditor?> GetEditorAsync(int id, CancellationToken ct);                  // info + lines + quiz + vocab + owner + learner progress count
Task<OperationResult<int>> SaveInfoAsync(int? id, AdminLessonInfoInput input, CancellationToken ct);
Task<OperationResult> SaveTranscriptAsync(int id, IReadOnlyList<AdminTranscriptLineInput> lines, CancellationToken ct);
Task<OperationResult<int>> ImportTranscriptAsync(int id, bool replaceExisting, CancellationToken ct);
Task<OperationResult> SaveQuizAsync(int id, IReadOnlyList<AdminQuizInput> questions, CancellationToken ct);
Task<OperationResult> SaveVocabAsync(int id, IReadOnlyList<AdminVocabInput> items, CancellationToken ct);
Task<OperationResult> SetPublishedAsync(int id, bool published, CancellationToken ct);
Task<OperationResult> DeleteAsync(int id, string confirmTitle, int adminId, CancellationToken ct);
```
Rules:
- **Learner-owned lessons (D2, L1):**
  - only `GetEditorAsync` (read-only), `SetPublishedAsync(false)` and `DeleteAsync`;
  - every save returns `Forbidden` "Không sửa bài nghe riêng của học viên.";
  - the editor shows a banner "Bài nghe riêng của {tên học viên}. Admin chỉ xem và có thể xóa.".
  - Their `IsPublished` does not affect the catalog (catalog requires `OwnerUserId == null`); record the verified catalog rule at A0.
- **Info (L2, L3, L10):**
  - title 1–(limit), topic 1–(limit);
  - level in `CefrLevels`;
  - YouTube id parsed or error "Mã hoặc liên kết YouTube không hợp lệ.";
  - audio and thumbnail through the URL rules; thumbnail defaults to the YouTube image when empty;
  - duration normalised or error "Thời lượng dạng mm:ss, ví dụ 04:12.";
  - speakers trimmed.
  - New lessons start as drafts (`IsPublished = false`) unless the admin ticks "Xuất bản ngay" and the publish rules pass.
- **Publish rules (L8):**
  - at least one transcript line is required ("Cần ít nhất một dòng lời thoại trước khi xuất bản.");
  - warnings (not blocking) when there is no quiz or no vocabulary, or when a YouTube lesson has no timed lines;
  - `status=incomplete` in the list = published or draft lessons missing a transcript, or any quiz question failing validation.
- **Transcript (L6):**
  - lines with text 1–(limit);
  - speaker default "Speaker 1";
  - times either both empty or `0 ≤ start < end`, and in non-decreasing order of start; errors per line ("Dòng 3: thời điểm kết thúc phải sau thời điểm bắt đầu.").
  - Save is all-or-nothing in one transaction.
- **Import (L7):**
  - `replaceExisting` must be true when lines exist (the UI asks first: "Thay {n} dòng hiện có bằng lời thoại từ YouTube?");
  - missing YouTube id → result error, not a raw 400;
  - provider exceptions → logged with detail; the admin sees "Không lấy được lời thoại từ YouTube lúc này. Hãy thử lại sau hoặc nhập tay.";
  - one import per lesson at a time (in-memory per-lesson guard; a second click gets "Đang nhập lời thoại, vui lòng chờ.").
- **Quiz (L4):**
  - question text 1–500;
  - A and B required (1–500); C and D optional (≤ 500), D only if C is filled;
  - correct answer ∈ {A, B, C, D} and its option filled;
  - explanation ≤ limit;
  - errors per question ("Câu 2: đáp án đúng C đang để trống.");
  - all-or-nothing.
  - `UserListeningProgress` stores only flags and the score, so replacing questions is safe; record the check.
- **Vocabulary (L5):** word 1–(limit), definition 1–(limit), example optional, image through the URL rules; all-or-nothing.
- **Delete (L9):**
  - the typed title must match (D4);
  - the confirmation shows the number of learner progress rows that will be removed ("{n} học viên đã học bài này; tiến độ của họ sẽ bị xóa.");
  - one structured log line.
- **Paging:** clamped (S5).

### 4.4 Controllers
- **Thin actions** calling the services; same routes and action names.
- **New actions:**
  - `SetManagement.Export` (GET, system sets);
  - `SetManagement.SetVisibility` (POST);
  - `ListeningManagement.Editor/{id}` (GET; tab via `?tab=info|transcript|quiz|vocab`);
  - `ListeningManagement.Publish` / `Unpublish` (POST).
- **Old routes:** `Create` and `Edit/{id}` render the editor's Info tab; `ManageTranscript`, `ManageQuiz` and `ManageVocab` redirect (302) to the matching tab.
- **POST results:**
  - success redirects back to the same tab with `TempData` "Đã lưu {lời thoại|quiz|từ vựng|thông tin}.";
  - validation errors re-render the tab with the posted values and per-row messages;
  - `Forbidden` results answer with the friendly message (no raw 403 page for admins).
- **Payments `Index`:**
  - figures, computed in one grouped query each:
    - revenue of paid orders in the last 30 days (Vietnam-time window);
    - active Premium users;
    - pending orders;
    - `manual_review` orders;
  - an alert linking to the filtered list when `manual_review` > 0;
  - a time-range filter (7 / 30 / 90 days / all; default all, to keep today's behaviour);
  - no change to the order-state actions.
- **Notifications `Create`:**
  - pass the recipient count (same filter as sending: not blocked, not anonymised) to the view;
  - nothing else changes.

---

## 5. UI specification

### 5.1 Common rules
- **Root:** every page in scope uses `<div class="ac-page {module}">` inside the admin layout. Styles live only in `admin-content.css`:
  - use the admin-shell / `--tct-*` tokens: no hex, `rgb()`, `:root` or `!important`;
  - match the look of `/Admin/UserManagement`: white cards with a small shadow, 10 px radius, 14–15 px text, compact tables.
- **Shared partials:**
  - `_AcPageHeader` (icon, title, sub-title, actions, optional breadcrumb);
  - `_AcFigures` (four cards);
  - `_AcFilterBar` (search, selects, "Lọc", clear);
  - `_AcTableCard` (title, count chip, optional sort select, table slot, pager "Hiển thị a–b trong n");
  - `_AcStatusTag` (text tag with tone);
  - `_AcConfirmDelete` (SweetAlert2 dialog that asks for the exact name, with the impact sentence).
- **Icons:** Font Awesome only. Replace the Bootstrap Icons in these four modules with the Font Awesome equivalents and record any missing glyph.
- **Copy:** Vietnamese with diacritics. Keep technical words where admins expect them: "VNPay", "MoMo", "YouTube", "IPA", "CEFR".
- **Formats:** money `399.000 ₫`; dates `dd/MM/yyyy HH:mm` in Vietnam time.
- **Tables:**
  - first column holds the name and a muted second line;
  - action buttons are 32 px icon buttons with `aria-label` and `title`;
  - no wrapping of action cells;
  - horizontal scroll inside the table card below 1100 px (never page scroll).
- **Feedback:** `TempData` success and error messages render as dismissible alerts at the top with `role="status"` / `role="alert"`.
- **Forms:**
  - labels above fields;
  - helper text below;
  - character counters for limited fields;
  - errors under each field and a summary at the top;
  - the primary button keeps its label and gets a pending state.
- **Unsaved changes:** the editor tabs warn before leaving ("Bạn có thay đổi chưa lưu. Rời trang?") and show the bar "Có thay đổi chưa lưu" with "Hủy thay đổi" and "Lưu …" (mockup 4).
- **Accessibility:**
  - keyboard reachable;
  - focus visible;
  - tabs use `role="tablist"` with arrow keys;
  - dialogs trap focus and return it;
  - status not by colour alone.

### 5.2 Vocabulary sets (mockups 1, 2)
- **List** (`Index`):
  - header "Quản lý bộ từ vựng" / "Bộ từ vựng hệ thống (trang Từ vựng) và bộ thẻ người dùng tạo." with the action "Tạo bộ từ vựng hệ thống";
  - figures: "Bộ từ vựng hệ thống", "Thẻ hệ thống", "Bộ thẻ người dùng", "Thẻ thiếu ví dụ/IPA";
  - filters: search; "Nguồn" (Tất cả / Từ vựng hệ thống / Của người dùng); "Thư mục" (system folders only when Nguồn = hệ thống, otherwise disabled); "Hiển thị" (Tất cả / Công khai / Có liên kết / Riêng tư); sort ("Mới tạo trước", "Tên A–Z", "Nhiều thẻ nhất", "Nhiều lượt học nhất");
  - columns: "Bộ từ vựng" (name + "Bài 1–n · m chủ đề" for system sets, "Bộ thẻ cá nhân" otherwise), "Nguồn", "Chủ sở hữu" ("TCT English (hệ thống)" for system), "Thư mục", "Hiển thị", "Thẻ", "Lượt học", "Ngày tạo", "Hành động" (Thẻ / Sửa / Xóa; learner sets: Thẻ (xem) / Đặt riêng tư / Xóa).
- **Set form** (create/edit system set):
  - fields "Tên bộ từ vựng" (read-only on edit with the D3 note), "Mô tả", "Thư mục" (system folders);
  - the visibility is shown as "Công khai (bộ hệ thống luôn công khai)".
- **Cards page:**
  - breadcrumb "Quản lý bộ từ vựng / {tên}";
  - title + source tag; sub-line with folder, owner, visibility;
  - actions "Xem như học viên" (opens `/Vocabulary/Detail?setId=` for system sets, `/Sets/{id}` for public learner sets), "Sửa thông tin", "Tải JSON" (system), "Thêm thẻ";
  - warning alert for system sets (D3 text from mockup 2: "Bộ hệ thống được đồng bộ từ tệp dữ liệu. Khi bật đồng bộ lúc khởi động, thẻ không có trong tệp sẽ bị xóa. Hãy sửa đồng thời system-vocabulary.json nếu muốn giữ thay đổi.");
  - learner sets show "Bộ thẻ của {tên}. Admin chỉ xem."
  - figures: "Tổng số thẻ", "Bài học" (lesson count by the lesson builder), "Thiếu ví dụ hoặc IPA", "Học viên đã học";
  - table: picture (emoji, image or initial), term + IPA, meaning, topic, example, "Kiểm tra" tags ("Đủ thông tin" / "Thiếu ví dụ" / "Thiếu IPA" / "Thiếu hình"), actions;
  - search, topic filter, quality filter, 50 per page.
- **Card form** (add/edit):
  - two columns on laptop;
  - "Từ" (Term), "Nghĩa tiếng Việt", "Phiên âm (US)", "Phiên âm (UK)", "Từ loại", "Cấp độ CEFR", "Emoji", "Ảnh (liên kết https hoặc /…)", "Chủ đề (bài)", "Ví dụ", "Dịch ví dụ", "Nghĩa tiếng Anh";
  - live preview of the learner word card (emoji/image, term, IPA, meaning, example) on the right.

### 5.3 Listening lessons (mockups 3, 4)
- **List:**
  - header "Quản lý bài nghe" / "Bài nghe hệ thống theo trình độ A1–C2 và bài riêng do học viên Premium thêm." + "Thêm bài nghe";
  - figures: "Bài hệ thống", "Đã xuất bản", "Bản nháp / thiếu nội dung", "Bài riêng của học viên";
  - filters: search (title, topic, YouTube id), "Trình độ", "Nguồn" (default Hệ thống), "Trạng thái" (Đã xuất bản / Bản nháp / Thiếu nội dung);
  - columns:
    - "Bài nghe" (thumbnail with a fallback icon when the image fails, title, topic · duration);
    - "Trình độ", "Nguồn", "Trạng thái";
    - "Lời thoại" (n dòng + "Có mốc thời gian" / "Chưa có");
    - "Quiz" (n câu), "Từ vựng" (n từ);
    - "Hành động" (Sửa / Xem / Xóa).
- **Editor** (`Editor/{id}`):
  - breadcrumb; title with level and status tags; sub-line topic · duration · YouTube id;
  - actions "Xem như học viên" (system lessons), "Xuất bản" / "Chuyển về bản nháp";
  - tabs "Thông tin", "Lời thoại {n}", "Quiz {n}", "Từ vựng {n}".
  - **Thông tin:**
    - fields of 4.3;
    - YouTube field with a preview thumbnail that updates once the id parses;
    - the publish checklist (✓/✗ rows: "Có lời thoại", "Có mốc thời gian", "Có quiz", "Có từ vựng").
  - **Lời thoại:**
    - table-like rows: #, speaker, text, Vietnamese meaning, start, end (`mm:ss.s` inputs), move up/down, delete;
    - "Thêm dòng"; "Nhập từ YouTube" (with the replace confirmation);
    - per-row errors.
  - **Quiz:** cards per question as mockup 4 (radio for the correct option, A/B required, C/D optional, inline errors, move/delete), "Thêm câu hỏi".
  - **Từ vựng:** rows word, meaning, example, image URL with a small preview, move/delete, "Thêm từ".
  - Learner-owned lessons: all inputs read-only, banner, only "Chuyển về bản nháp" (if published) and "Xóa".
  - `admin-listening-editor.js` handles add/move/delete rows, re-indexes field names, counters, dirty state and client-side checks mirroring the server rules (the server stays authoritative). Pure helpers in `admin-content.js` are node-tested.

### 5.4 Payments (mockup 5)
- Header "Quản lý thanh toán" / "Đơn Premium, trạng thái cổng thanh toán và lịch sử xác nhận."; actions "Người dùng Premium", "Tình trạng cổng", "Đóng đơn chờ quá hạn" (existing action, with a confirmation dialog).
- Figures:
  - "Doanh thu 30 ngày";
  - "Premium đang hoạt động";
  - "Đơn đang chờ";
  - "Đơn cần kiểm tra".
- Manual-review alert when > 0.
- Filters: search, "Cổng", "Trạng thái", "Thời gian".
- Table: "Mã đơn", "Người dùng" (+ email), "Gói", "Cổng", "Số tiền" (right-aligned), "Trạng thái" (shared billing mapping), "Ngày tạo", view action.
- Restyle Details, Subscriptions, PremiumUsers, PremiumUserDetails and Health with the same partials and Vietnamese copy:
  - "Tình trạng cổng thanh toán";
  - "Gói đăng ký";
  - "Người dùng Premium";
  - "Lịch sử thanh toán";
  - "Nhật ký xử lý".

  Keep every form, field name, action and confirmation that exists.

### 5.5 Notifications (mockup 6)
- **Create:**
  - two columns: form left, preview right;
  - counters "n/200", "n/1000"; the link field and its hint as built in the notifications work;
  - the sticky bar "Sẽ gửi tới {n} tài khoản đang hoạt động" + "Gửi thông báo";
  - a confirmation dialog "Gửi thông báo tới {n} tài khoản? Không thể thu hồi sau khi gửi.";
  - the preview mirrors the learner bell item (icon, title, message, "Vừa xong", link label).
- **History:** restyled table with the existing columns ("Tiêu đề", "Người gửi", "Người nhận", "Đã đọc", "Thời gian") and pager.

---

## 6. Tests
- **Rules:**
  - YouTube parsing (all link shapes, invalid ones);
  - image/media URL rule;
  - duration normalisation;
  - CEFR set;
  - text limits.
- **Vocabulary service:**
  - system create (owner, public, system folder only, duplicate name);
  - rename of system set blocked;
  - folder of another user rejected (S1);
  - learner set update forbidden;
  - learner set made private;
  - delete through the personal-set service with subscriptions present, wrong confirm name rejected;
  - card limits and URL rule;
  - card redirect uses real set;
  - quality flags;
  - export shape equals a set read from the JSON file;
  - paging clamp.
- **Listening service:**
  - learner lesson save forbidden, unpublish and delete allowed;
  - info validation (level, YouTube, duration);
  - publish blocked without transcript, warnings otherwise;
  - transcript time rules and ordering;
  - quiz rules (each error case), empty A/B no longer throws;
  - vocab rules;
  - import requires replace confirmation, provider failure message, concurrent guard;
  - delete confirm and progress count;
  - paging clamp.
- **Integration:**
  - each page renders 200 for an admin and 403/redirect for others;
  - old routes redirect to the editor tabs;
  - `page=0` and `page=-5` render page 1;
  - POSTs reject a missing token;
  - no `bi bi-` and no English page titles in the four modules' views;
  - money and date formats;
  - payments figures equal the database counts;
  - notification create shows the same recipient count as the send filter.
- **Node:** helpers in `admin-content.js` (dirty state, counters, row re-indexing, confirm-name match, YouTube id parse mirror).
- **Unchanged and green:**
  - billing and notification logic tests;
  - learner vocabulary, listening and personal-set tests;
  - other owners' admin tests.

---

## 7. Data used while verifying
- Use the local synthetic data only.
- Create throwaway content with names starting "[Thử nghiệm]" (a system set with three cards, a draft listening lesson) and delete it at the end through the admin pages themselves.
- Never delete or edit real seeded system sets or published lessons, except to read them.
- Do not send announcements (the notification page is checked up to the confirmation dialog, then cancelled).
- Do not touch payment orders except reading them.
- Record created ids in section 12.

---

## 8. Phases

Run mode: continuous, autonomous, one agent. After each green gate: gate record into section 12 → commit → next phase.

| Phase | Work | Read |
| --- | --- | --- |
| **A0 Baseline** | Setup and discovery; detailed steps below the table | 0–4, 7 |
| **A1 Admin UI foundation** | `admin-content.css`, shared `_Ac*` partials, `admin-content.js` + node tests, `AdminInputRules` + tests | 4.1, 5.1 |
| **A2 Vocabulary logic** | Tests first: `AdminVocabularyService`; thin controller; DI lines | 4.2, 4.4 |
| **A3 Vocabulary UI** | List, set form, cards page, card form with preview, export | 5.2 |
| **A4 Listening logic** | Tests first: `AdminListeningService`; thin controller; old-route redirects | 4.3, 4.4 |
| **A5 Listening UI** | List and the four-tab editor | 5.3 |
| **A6 Payments admin** | Figures and filter in `Index`; restyle and copy of the six views | 4.4, 5.4 |
| **A7 Notifications admin** | Recipient count, preview, confirmation; restyle History | 4.4, 5.5 |
| **A8 Responsive and accessibility pass** | All pages at every size; keyboard; dialogs | 5.1 |
| **A9 Docs** | Tracker rows (four modules), known-issues (D1 proposal for Châu, seeder rename rule, learner-content policy), bug-fix-log, project-structure | 2.2 |
| **A10 Full gate** | Whole gate on every page; end-to-end run (8.5); after-screenshots into `.tmp/admin-content/after/`; final self-review against section 10 | all |

**A0 Baseline, step by step:**
1. Clean-tree check: leave `appsettings.Development.json` and `Claude outputs/` unstaged.
2. `git switch master`; pull once with `scripts/pull-local.ps1`; verify `c641c3e` or later.
3. Create `feature/admin-content-complete`.
4. Build; run the full and node tests; record counts, warnings and known failures.
5. Save the plan and the prompt.
6. Read D1–D4.
7. Reproduce or disprove S1–S9, L1–L10, P1–P3 and N1. For S1, prove with an integration test sketch that a set placed in another user's folder becomes visible to that user.
8. Discover:
   - column limits for `Sets`, `Cards`, `ListeningLessons`, `ListeningTranscriptLines` and `ListeningVocabItems`;
   - the exact JSON shape of `system-vocabulary.json`;
   - the catalog rule for learner-owned lessons;
   - which tests cover these admin pages (`AdminCatalogPagingTests` and others);
   - whether SweetAlert2 is available on every admin page;
   - the Font Awesome version in the admin layout.
9. Start the app; take before-screenshots of every page in scope at 1440 and 768 into `.tmp/admin-content/before/`.

### 8.1 Phase gate
| Gate | Check |
| --- | --- |
| G1 | build; warnings ≤ A0 |
| G2 | area tests: `Admin*`, `AdminVocabulary*`, `AdminListening*`, `AdminInputRules*`, `AdminContentPages*`, `PersonalSets*`, `Vocabulary*`, `Listening*`, `Billing*`, `Notification*` |
| G3 | full suite; counts not below A0 plus new tests |
| G4 | node tests; only A0 failures may remain |
| G5 | contracts of 2.4; learner pages `/Vocabulary`, `/Home/Listening`, `/Sets` unchanged; `has-pending-model-changes` none |
| G6 | tokens-only grep on `admin-content.css`; no inline `style`/`<script>` blocks in changed views (partials may carry `data-*` only); `git diff --check` |
| G7 | file scope; `Program.cs` diff = DI lines; `_AdminLayout.cshtml` untouched (or two labels with D1 = CÓ); no change in `Services/Billing/**` or `NotificationService.cs` |
| G8 | render at 1440×900, 1366×768, 1024, 768, 390 and 200% zoom; console and network clean |
| G9 | mockup comparison and side-by-side with `/Admin/UserManagement` for consistency |
| G10 | behaviour checklist (8.4) by mouse and keyboard only |
| G11 | accessibility (5.1) incl. tabs, dialogs, focus return |
| G12 | scripted contrast on new tags and buttons |
| G13 | regression: learner vocabulary lesson, listening practice, personal sets, payments flow pages, notification bell; other admin pages open |
| G14 | consistency grep: same partials and classes used across the four modules; no `bi bi-`, no English page titles |
| G15 | security: admin-only access, antiforgery tests, S1 test, URL rules, no `innerHTML` with data in new JS, no credentials or `.tmp/` in the diff |

### 8.2 Failure and stop rules
- **A failed item:** fix and rerun the whole gate.
- **Attempts:** at most 3 per item; then mark it "deferred" (caused by the phase, reverted) or "blocked" (out of scope, with evidence).
- **Stop the run** if green requires any of:
  - a forbidden file;
  - a migration;
  - a change to payment or notification logic;
  - editing real seeded content.
- **Tests:** never skip, weaken or delete a test; never call a skipped check passed.

### 8.3 Gate record (append to section 12 before each commit)
- Phase, date, commit subject.
- G1–G15, each with pass / n-a / deferred / blocked and one line of evidence.
- Decisions taken.
- Differences from the mockups.
- Tests changed, and why.
- Ids of throwaway content.

### 8.4 Behaviour checklists
- **A2/A4 (tests and UI):**
  - S1–S9 and L1–L10 each have a passing test;
  - learner content is view, private and delete only;
  - deleting needs the exact name;
  - old routes land on the right tab.
- **A3:**
  - filters and sort combine;
  - "Thư mục" lists system folders only;
  - system set name is read-only with the note;
  - "Tải JSON" downloads the right shape;
  - card form preview updates;
  - URL and length errors show in Vietnamese;
  - quality tags and filter;
  - paging at 50;
  - "Xem như học viên" opens the learner page.
- **A5:**
  - tab keyboard navigation;
  - add, move and delete rows in each tab;
  - per-row errors (times, empty correct option, empty definition) keep the typed values;
  - unsaved-changes bar and leave warning;
  - import with replace confirmation and the guarded double click;
  - publish blocked without transcript;
  - learner lesson read-only with banner;
  - the thumbnail fallback.
- **A6:**
  - figures match the database;
  - the manual-review alert filters the list;
  - every existing action still works;
  - all copy Vietnamese;
  - money format.
- **A7:**
  - counters;
  - preview updates live;
  - confirmation dialog shows the count, and cancel sends nothing;
  - history restyled.
- **A8:**
  - no page scroll at 768;
  - table scroll inside cards;
  - 200% zoom;
  - focus visible on dark sidebar and white content.

### 8.5 End-to-end run (A10)
As the synthetic admin from `.appdata/local/access.json` (never print passwords):
1. Create the system set "[Thử nghiệm] Bộ từ vựng". Add three cards (one missing IPA), see the quality figure change, export JSON, and try to rename (blocked).
2. Check that the set appears on `/Vocabulary` for a learner (if the vocabulary home lists all public system sets; otherwise record).
3. Try to move a learner's set into another user's folder → rejected.
4. Make a learner's public set private → the learner's share link stops working for others.
5. Create the draft lesson "[Thử nghiệm] Bài nghe":
   1. try to publish (blocked);
   2. add two transcript lines with bad times (errors), then fix them;
   3. add a quiz with an empty correct option (error), then fix it;
   4. publish, and see it in the catalog for a learner;
   5. unpublish.
6. Open payments; the figures match the database.
7. Open the notification form; the count matches the database; cancel at the dialog.
8. Delete the throwaway lesson and set through the admin pages with typed names.

Record database evidence.

---

## 9. Constraints
- One agent, local only.
- **Granted:**
  - local commits on `feature/admin-content-complete`, one per phase, via `tct-commit-message`;
  - one `pull-local.ps1` at A0.
- **Not granted:** push, PR, merge, rebase, reset, deploy.
- **Never stage:** `appsettings*.json`, `Claude outputs/`, `.tmp/`, screenshots.
- **Data and secrets:**
  - never reset or reseed the database;
  - never edit real seeded content;
  - no announcements sent;
  - no paid AI calls;
  - never print credentials.
- **Language:** plan and code comments in English; UI copy in Vietnamese exactly as written here.

## 10. Definition of Done
- [ ] S1–S9, L1–L10, P1–P3 and N1 fixed, mitigated as decided, or disproved with evidence.
- [ ] Admin set and lesson logic lives in two tested services; controllers are thin; set deletion uses the personal-set service.
- [ ] No folder/owner mismatch possible; system sets public and system-owned; learner content handled per D2.
- [ ] Listening editor with four tabs, validation without crashes, publish rules, safe import.
- [ ] The four modules share one admin style that matches the rebuilt admin pages; Vietnamese copy; Font Awesome icons; money and time formats.
- [ ] Payments and notification admin restyled without logic changes; figures and recipient counts correct.
- [ ] Laptop, tablet, phone, keyboard, dialogs and contrast verified.
- [ ] No migration; `Program.cs` limited to DI lines; shared layouts untouched (unless D1); other owners' tests unedited and green.
- [ ] Tracker, known-issues and section 12 updated.

## 11. Risks
| Risk | Mitigation |
| --- | --- |
| Admins relied on renaming system sets | D3 message explains the seeder; new set path; JSON export |
| Restricting admins on learner content blocks moderation | D2 keeps private/unpublish/delete; owner can still edit |
| Old links to `ManageQuiz` etc. | 302 to the editor tabs; tested |
| Seeder overwrites admin card edits | Warning on system sets + JSON export; recorded in known-issues |
| `Program.cs` DI edit against the team rule | Limited to registration lines (G7); recorded for Châu |
| Payment pages lose an action during the restyle | Checklist A6 covers every existing action; existing admin billing tests stay green |
| Consistency drift with Châu's admin pages | G9 side-by-side; shared partials only in Trọng's modules |

## 12. Execution notes
(The agent appends A0 discovery notes — including the verdict on each finding — and one gate record per phase here.)

### A0 — Baseline and discovery (2026-11-18)

**Setup.** Branch `feature/admin-content-complete` from `master` at `c641c3e` (already current after the owner's pull; `origin/master` unchanged). Tree clean apart from `appsettings.Development.json` (one blank line, left unstaged) and `Claude outputs/`. Plan and prompt copied in from the Desktop copies.

**Baseline (Debug, `dotnet 9.0.318`, Node 24.19).**
- Build: 0 errors, 16 warnings (all pre-existing, in tests).
- `dotnet test` (whole solution): 3152 total, 3098 passed, 53 skipped, **1 failed** — `AiRagPhase5FixRound2Tests.ProjectionDeadline_EndsAWaitingDisplayCallWithoutReleasingTheBarrier` (timing test of the AI module, not in scope; counts as the known A0 failure).
- `node --test scripts/tests/*.cjs`: 499 tests, 498 passed, **1 failed** — `test_speaking_catalog_packages.cjs` (catalog hash comparison, not in scope).
- `dotnet test` regenerates `docs/design-system/email-preview/*`; restored with `git checkout` before each commit.

**Verdict on each finding.**

| # | Verdict | Evidence |
| --- | --- | --- |
| S1 | Confirmed (static + rule) | `SetManagementController.Create/Edit` store any `FolderId`; `PersonalSetService.ViewableSets` grants view to any user who owns the folder (`_db.Folders.Any(f => f.FolderId == s.FolderId && f.UserId == userId)`), saved it, or reaches it through a class. Regression test written in A2. |
| S2 | Confirmed | `Create` never sets `Visibility`; `Set.Visibility` defaults to `"Public"`. `OwnerId` dropdown lists all users. |
| S3 | Confirmed | `Delete` removes progress, cards and the set by hand; skips subscriptions, share slug, clones; `IPersonalSetService.DeleteSetAsync(isAdmin: true)` exists. |
| S4 | Confirmed | `JsonVocabularySeeder` matches by `(OwnerId, SetName, FolderId)` (line 105); renaming or moving a system set duplicates it on the next sync. |
| S5 | **Reproduced live** | `/Admin/SetManagement?page=0` and `?page=-5` → 500; `/Admin/ListeningManagement?page=0` and `?page=-3` → 500; `page=999` → 200 with an empty table. Billing and notification history clamp already (200). |
| S6 | Confirmed | No trim or length check on card fields; `ImageUrl` free text. `Cards.Term` max 255; `Definition`, `Example`, `Topic` have no configured limit (no column cap in the model), so the service uses its own sane caps. |
| S7 | Confirmed | `EditCard` redirects with the posted `SetId`; form lacks `Emoji`, `PhoneticUk`, `Cefr`, `PartsOfSpeech`, `MeaningEn`. |
| S8 | Confirmed | `Cards` loads every card (up to 20 in the dev data) without search or paging. |
| S9 | Confirmed | `"No Name"` in owner dropdowns; no structured delete log. |
| L1 | Confirmed (static) | Index lists all lessons, editing allowed for any. Dev DB has 91 system lessons and 0 learner lessons, so the learner path is covered by tests, not by the UI. |
| L2 | Confirmed | `NormalizeYoutubeId` falls back to the raw input; no `/embed/`, `/shorts/`, nocookie parsing. |
| L3 | Confirmed | `Level` unchecked on the server. |
| L4 | Confirmed | `OptionA.Trim()` throws on null; `CorrectAnswer` unchecked. |
| L5 | Confirmed | `Definition.Trim()` throws on null. |
| L6 | Confirmed | Times unchecked; every save redirects to the index. |
| L7 | Confirmed | Import replaces lines without asking; 400 plain text; `ex.Message` shown. |
| L8 | Confirmed | Publish has no content check. Dev DB: 1 of 91 system lessons is published. |
| L9 | Confirmed | Delete cascades learner progress with no count in the dialog. |
| L10 | Confirmed | `ThumbnailUrl` and `AudioUrl` unchecked; `Duration` free text (column max 20). |
| P1 | **Reproduced live** | Billing Index contains "Provider Health", "payment orders", "VND", "subscription"; Health `<h1>` = "Payment Provider Health". |
| P2 | Confirmed | No figures on the list. |
| P3 | Confirmed | Visual comparison with the before screenshots. |
| N1 | Confirmed | No preview, count or confirmation on the form. |

Admin pages currently use Bootstrap Icons almost everywhere (counts in the main area: sets 77, listening 209, billing 35, notification 13) with 1–6 Font Awesome icons from the layout. Bootstrap Icons 1.11.3 and Font Awesome 6.5.1 are both loaded by `_AdminLayout.cshtml`; SweetAlert2 11.26.25 is loaded locally by the layout, so it exists on every admin page.

**Discovery answers.**
- **Column limits.** `Sets.SetName` 255, `Sets.Visibility` 20; `Cards.Term` 255, `PhoneticUk` 100, `Emoji` 16, `Cefr` 2, `PartsOfSpeech` 120, `MeaningEn` 400, `PersonalNote` 1000; `ListeningLessons` Title 255, Level 10, Topic 100, YoutubeId 50, AudioUrl 500, ThumbnailUrl 500, Duration 20, speaker names 150, countries 100; transcript Speaker 100 (Text required, no cap); quiz options 500 each, `CorrectAnswer` 1 (`QuestionText` required, 500 in the model attribute); vocab Word 200, ImageUrl 500 (Definition required, no cap).
- **`system-vocabulary.json` shape.** An array of folders: `{ FolderName, Sets: [ { SetName, Description, Cards: [ { Term, Definition, Topic, Phonetic, Example, ExampleTranslation, ImageUrl } ] } ] }`. The export for one set returns the same shape: one folder object with one set inside.
- **Catalog rule.** `ListeningService` lists the catalog with `OwnerUserId == null && IsPublished`; a learner's own lesson is visible only to its owner.
- **Tests covering these pages.** `AdminCatalogPagingTests`, `AdminBillingManagementTests`, `AdminBillingStateRulesTests`, `AdminAnnouncementTests`, `PersonalSetServiceTests`, `PersonalSetsIntegrationTests`, `ListeningIntegrationTests`, `ListeningCatalog*`, `VocabularyIntegrationTests`.
- **Namespaces.** The admin controllers still use the legacy `TCTVocabulary.*` namespaces; services under `Services/Sets` and `Services/ListeningService` use `TCTEnglish.*`. New admin services use `TCTEnglish.Services.Admin`.

**Pre-existing noise recorded (not caused by this work).** `/Admin/SetManagement/Cards/11` requests ten `/images/animals/*.jpg` files that return 404 (the seed JSON points at images that are not in `wwwroot`). The card list therefore shows broken pictures; the new cards page gets a fallback (A3) and this stays in known-issues.

**Before screenshots.** `.tmp/admin-content/before/` (1440 and 768 wide): 15 pages each, including `/Admin/UserManagement` as the style reference.

**Gate record — A0 (2026-11-18, docs only).** G1 pass (0 errors, 16 warnings = baseline). G2/G3 pass at baseline (1 known failure, see above). G4 pass at baseline (1 known failure). G5 pass (no code change; `has-pending-model-changes` not applicable). G6 pass (`git diff --check`). G7 pass (only the plan and the prompt). G8–G15 n/a (no UI or logic change yet). Decisions: D1–D4 defaults kept. Throwaway content: none created yet.

### A1 — Admin UI foundation (2026-11-18)

**Built.**
- `Services/Admin/AdminInputRules.cs` (YouTube id parser for watch, short, embed, shorts, live and nocookie links, with look-alike hosts rejected; https/local URL rule; `m:ss`/`h:mm:ss` duration; CEFR set; text normalisation and limits; page clamping) with 91 unit tests in `AdminInputRulesTests.cs`, written before the code.
- `Services/Admin/AdminResult.cs`: `AdminResult` / `AdminResult<T>` with `Success`, `NotFound`, `Invalid` (+ per-row messages), `Forbidden` and non-blocking warnings. **Deviation from section 4.2:** the shared `OperationResult` (legacy namespace, outside the allowed files, no `Forbidden` status) is left untouched; the admin services return this new type instead.
- `Areas/Admin/ViewModels/AdminContentShared.cs` (kit models) and eight partials `_AcPageHeader`, `_AcFigures`, `_AcStatusTag`, `_AcAlerts`, `_AcFilterBar`, `_AcTableHeader`, `_AcPager`, `_AcConfirmDelete`. **Deviation from 5.1:** Razor has no content slots, so `_AcTableCard` is split into `_AcTableHeader` (title, count chip, sort) and `_AcPager` (footer "Hiển thị a–b trong n"); the table itself is written in each page inside `<div class="ac-card">`.
- `wwwroot/css/admin-content.css` (scoped `.ac-page`, tokens only) and `wwwroot/js/admin-content.js` (pure helpers + counters, type-the-name delete via SweetAlert2 `text`, unsaved-changes guard, pending state, auto-submit selects) with 12 node tests in `scripts/tests/test_admin_content.cjs`.

**Gate record — A1.**
- G1 pass: 0 errors; full rebuild gives 58 warnings, none from the new files. *Correction to the A0 record:* "16 warnings" was an incremental build that only recompiled the test project; the whole-solution baseline is 58.
- G2/G3 pass: 3243 tests (3152 + 91 new), 0 failed, 53 skipped. The A0 failure (`AiRagPhase5FixRound2Tests…`) did not fail this time, so it is a timing flake.
- G4 pass: 511 node tests, 510 passed; only the A0 speaking-catalog hash failure remains.
- G5 pass: no learner page or contract touched; `dotnet ef migrations has-pending-model-changes` → "No changes".
- G6 pass: no hex, `rgb()`, `:root` or priority overrides in `admin-content.css`; every `var(--tct-*)` exists in `tct-learning-ui.css`; no inline `style` or `<script>` in the partials; `git diff --check` clean for the changed files.
- G7 pass: only new files under `Services/Admin/`, `Areas/Admin/ViewModels/`, `Areas/Admin/Views/Shared/_Ac*`, `wwwroot/css|js`, the two test files. `Program.cs`, `_AdminLayout.cshtml`, `Services/Billing/**`, `NotificationService.cs` untouched.
- G8–G11 n/a: nothing renders the kit yet (first consumer is A3); the partials compile with the Razor build. G9 n/a for the same reason.
- G12 pass: scripted contrast over the kit colour pairs (tags, buttons, alerts, counters, muted text, table head, link) is 5.15–17.10 against the 4.5 target; control border 3.66 and focus ring 5.28 against the 3.0 non-text target. The disabled-state pair is 4.24, which WCAG exempts for inactive controls.
- G13 pass: the full suite above covers learner pages and other admin pages; they are unchanged.
- G14 pass: the new partials use Font Awesome only (0 `bi bi-`).
- G15 pass: no `innerHTML`/`eval` in `admin-content.js` (asserted by a node test); no credentials, `.tmp/` or screenshots staged.
- Decisions: `TryNormalizeDuration` treats an empty duration as "not set" (import fills it later) and rejects `00:00`; the delete confirmation compares the typed name exactly after trimming what the admin typed.
- Throwaway content: none.

### A2 — Vocabulary admin logic (2026-11-18)

**Built (tests first).**
- `Services/Admin/AdminVocabularyModels.cs` (DTOs, messages, `IAdminVocabularyService`) and `AdminVocabularyService.cs`; 71 unit tests in `AdminVocabularyServiceTests.cs` (SQLite in memory, real `PersonalSetService`), plus the DI test in `AdminContentPagesIntegrationTests.cs`.
- `Program.cs`: exactly one added line, `AddScoped<IAdminVocabularyService, AdminVocabularyService>()`, next to `IPersonalSetService`.

**Findings now covered by a passing test.**

| # | Test (class `AdminVocabularyServiceTests`) | Outcome |
| --- | --- | --- |
| S1 | `S1_TheSharingRule_GrantsViewToWhoeverOwnsTheFolder…` (characterises the rule), `S1_CreateSystemSet_RejectsAFolderOfAnotherUser`, `S1_UpdateSystemSet_RejectsAFolderOfAnotherUser_AndAMissingFolder` | Fixed: the folder must belong to the set's owner |
| S2 | `S2_CreateSystemSet_IsOwnedBySystemUser_AndPublic_AndStamped` and the name, length, duplicate and no-system-user cases | Fixed: owner = system user, `Public`, no owner dropdown |
| S3 | `S3_Delete_GoesThroughThePersonalSetService…`, `S3_Delete_NeedsTheExactName…` | Fixed: deletion uses `IPersonalSetService.DeleteSetAsync(isAdmin: true)` in one transaction; typed name required |
| S4 | `S4_RenamingASystemSet_IsBlocked…`, `S4_MovingASystemSetToAnotherSystemFolder_Works_WithAWarning…` | Mitigated (D3): rename blocked; a folder move warns that the seeder also matches by folder; JSON export added |
| S5 | `S5_ListSets_ClampsAPageBelowOne`, `…ShowsTheLastPage…`, `…OnAnEmptyDatabase…` | Fixed in the service (controller switches in A3) |
| S6 | `S6_AddCard_*` (17 invalid-input cases, limits, trimming, blank → null) | Fixed |
| S7 | `S7_UpdateCard_ReturnsTheCardsRealSet…` | Fixed: returns the card's real set id; Emoji, UK IPA, CEFR, parts of speech, English meaning are editable |
| S8 | `S8_ListCards_*` (paging at 50, search, topic, quality flags and filters, set summary) | Fixed in the service |
| S9 | "No Name" is gone with the owner dropdown; every write logs admin id, set id, owner id and card count (`ILogger`) | Fixed |
| D2 | `D2_*` | Learner sets: update and card edits `Forbidden`; "private" turns the share link off too; cannot be made public |

**Decisions and deviations.**
- **The controller switches to the service in A3, together with the views**, not in A2. The old views post `OwnerId` and use an owner dropdown that no longer exists, so an adapter would be throwaway code; keeping the old controller for one local commit keeps every page working at every commit. The "thin controller" acceptance item therefore closes in A3.
- Making a learner set private also clears `ShareSlug`, because `SubscribeAsync(slug)` grants access whatever `Visibility` says. Subscribers and folder/class sharing are not affected (recorded for known-issues).
- Limits chosen where the column has none (`Cards.Definition`, `Example`, `Topic` are unbounded text): Definition 1000, Example and translation 1000, Topic 100, ImageUrl 500; set description 1000.
- Search uses `Contains`: case-insensitive on MariaDB (collation), case-sensitive on SQLite, so the unit test uses the exact case.
- The seeder also matches cards by `Term`: renaming a term in the admin can look like a new card to the next sync (goes to known-issues with the rename rule).

**Gate record — A2.**
- G1 pass: 0 errors, 58 warnings (= A1; none from the new files).
- G2/G3 pass: 3315 tests (3243 + 71 + 1), 0 failed, 53 skipped.
- G4 pass: unchanged since A1 (511 node tests, the same 1 known failure; no JS touched).
- G5 pass: learner code and contracts untouched; `has-pending-model-changes` → "No changes".
- G6 pass: no CSS/views changed; `git diff --check` clean for the changed files.
- G7 pass: `Program.cs` diff = 1 DI line; new files only under `Services/Admin/` and `TCTEnglish.Tests/`; `_AdminLayout.cshtml`, `Services/Billing/**`, `NotificationService.cs`, `PersonalSetService*`, the seeder and `system-vocabulary.json` untouched.
- G8–G12 n/a (no UI change in this phase).
- G13 pass (full suite incl. `PersonalSet*` and `Vocabulary*`).
- G14 n/a. G15 pass: service has no I/O beyond the database, every write goes through validation, URL rule `IsSafeImageUrl`, folder ownership check (S1), no credentials in the diff.
- Throwaway content: none.

### A3 — Vocabulary admin UI (2026-11-18)

**Built.**
- `SetManagementController` is now thin: every action calls `IAdminVocabularyService` (routes and action names unchanged: `Index`, `Create`, `Edit`, `Delete`, `Cards`, `AddCard`, `EditCard`, `DeleteCard`; new `SetVisibility` POST and `Export` GET). Learner sets redirect from `Edit`, `AddCard` and `EditCard` to the cards page with the "Không sửa nội dung bộ thẻ của học viên." message; `DeleteCard` ignores any posted set id and redirects to the card's real set.
- `SetManagementViewModel.cs` rewritten (list, set form, cards page, card form); the old owner picker and its models are gone.
- Views: `Index` (header, 4 figures, filter bar with source / system folder / exposure, count chip, sort, table with source and exposure tags, 32 px icon actions, pager "Hiển thị a–b trong n"), `Create`/`Edit` through `_SetForm`, `Cards` (breadcrumb, source tag, "Xem như học viên", "Sửa thông tin", "Tải JSON", "Thêm thẻ", the seeder warning, 4 figures, search + topic + "Kiểm tra" filters, quality tags, paging at 50), `AddCard`/`EditCard` through `_CardForm` (two columns, per-field messages, counters, live learner-card preview).
- `admin-content.js`: live preview (`data-ac-bind`), broken-picture fallback to the initial, `previewInitial` (+1 node test). `admin-content.css`: word cell, preview card, tag stack.
- A card picture whose local file is not in `wwwroot` is not requested (the seed JSON points at `/images/animals/*.jpg` files that do not exist); the emoji or the initial is shown instead, so the page makes no 404 requests.

**MariaDB finding (provider proof).** The first run against the dev MariaDB returned 500 on the list page although the SQLite tests were green: a correlated `COUNT(DISTINCT …)` over a nested derived table fails with `Unknown column 's0.SetID'` on MariaDB 10.5. Fixed by counting topics per page in one separate grouped query. All read paths of this module were then exercised on the dev MariaDB through the pages (list with every filter and sort, cards page with every filter, paging, export). Write paths run on MariaDB in the A10 end-to-end run with throwaway data.

**Gate record — A3.**
- G1 pass: 0 errors; the web project builds with 42 warnings, the same as before this phase (58 whole-solution = 42 web + 16 tests), none from the changed files.
- G2/G3 pass: 3328 tests (3315 + 13 new: 14 page tests replace the 1 DI test), 0 failed, 53 skipped. The first attempt at this gate was stopped by the host (low memory) and was rerun in separate steps: build, dotnet tests, node tests, model check.
- G4 pass: 513 node tests, 512 passed; only the A0 speaking-catalog hash failure remains. Two existing source-level tests in `scripts/tests/test_owner_admin_views_sweep.cjs` asserted the old Set list and Set edit markup (owner-picker field, dropdown row menu); they were rewritten on purpose to the new markup (labels through `_AcFilterBar`, `aria-label` on every icon action, labelled read-only name field) and still guard the same accessibility intent. The Listening assertions in the same file are unchanged until A5.
- G5 pass: learner `/Vocabulary/Index`, `/Home/Listening`, `/Sets` still 200; `has-pending-model-changes` → "No changes".
- G6 pass: no hex, `rgb()`, `:root` or priority overrides in `admin-content.css`; every token exists; no `style=`, inline `<script>`, `onsubmit` or `bi bi-` in the changed views; `git diff --check` clean.
- G7 pass: changed files are the controller, its view models, the five views plus two new partials, `admin-content.*`, the service and tests; `Program.cs`, `_AdminLayout.cshtml`, `Services/Billing/**`, `NotificationService.cs`, `PersonalSetService*`, the seeder and the JSON file untouched.
- G8 pass: rendered as admin at 1440×900, 1366×768, 1024×768, 768×1024 and 390×844 for the 7 pages (list, create, edit, cards of a system set, cards of a learner set, add card, edit card): no page-level horizontal overflow at any size, 0 failed requests, 0 console errors. (200% zoom is covered in A8 with the other modules.)
- G9: matches mockups 1 and 2 (header with icon and sub-title, 4 figure cards, filter card, table card with count chip and sort, tags, icon actions, pager, system-sync notice, 4 cards-page figures). Differences: no "Bài 1–n" range under a set name (the list shows "n chủ đề" because lessons are computed per set only on the cards page); the page header icon box uses the soft token, so it is faint on the canvas; the "Kiểm tra" column can show several "Thiếu …" tags at once.
- G10 pass (browser, scripted): system filter 8 rows; folder select enabled only for system and lists the 3 system folders; user + link 1 row; sort by cards; search; `page=0`, `-5`, `99` fine; learner set of 60 cards pages 50 + 10 with "Hiển thị 51–60 trong 60"; learner set shows no edit, add, export; quality filters partition the set (9 + 11 = 20); topic and text filters; card preview updates while typing (term, meaning, emoji) and does not preview an `http://` image; counters show `20/1000`; a server error shows "Hãy nhập từ." and keeps the typed values; the delete dialog opens, names the target, refuses a wrong name ("Tên chưa khớp"), cancel changes nothing and focus returns to the delete button. One scripted expectation was wrong (the dev data has 3 private user sets, not 4); the page is correct.
- G11 pass: every control inside `.ac-page` on the list, create, cards and edit-card pages (57, 23, 57 and 40 focus stops) shows a visible focus ring with real Tab key presses; labels, `aria-invalid`/`aria-describedby` on errors, `role="alert"` on messages, tables with `scope="col"`. One stop in the admin layout (outside this module) has no ring; it is Chau's shell.
- G12 pass: no new colour pairs beyond A1's (the preview and picture box reuse the tag pairs).
- G13 pass: as admin, Dashboard, Users, Speaking, Writing, Reading, AI, Listening, Billing, Notifications and the learner pages answer 200; as a standard user `/Admin/SetManagement` is refused.
- G14 pass: Font Awesome only; page titles Vietnamese ("Quản lý bộ từ vựng", "Tạo bộ từ vựng hệ thống", "Sửa bộ từ vựng", "Thêm thẻ", "Sửa thẻ", "Thẻ của …"). The sidebar label "Vocabulary Sets" stays (D1 = KHÔNG, proposal for Chau).
- G15 pass: antiforgery required on all 7 POSTs (tested, 400 without a token); admin role on every action (403 for a standard user); the exact name is required to delete; folder ownership rule; `IsSafeImageUrl`; no HTML built from data in the new JS.
- Tests changed: `AdminContentPagesIntegrationTests.cs` (14 tests, new this phase). No existing test edited.
- Throwaway content: none (all writes are in the in-memory test databases; the browser checks only read, and the delete dialog was cancelled).

### A4 — Listening admin logic (2026-11-18)

**Built (tests first).**
- `Services/Admin/AdminListeningModels.cs` (DTOs, Vietnamese messages, `IAdminListeningService`) and `AdminListeningService.cs`; 87 unit tests in `AdminListeningServiceTests.cs` (SQLite in memory, a fake `IYoutubeTranscriptService` that can fail, block and count calls); the host DI test now checks both admin services.
- `Program.cs`: one more DI line, `AddScoped<IAdminListeningService, AdminListeningService>()`; the file's diff against `master` is now exactly the two registration lines.

**Findings now covered by a passing test.**

| # | Tests (class `AdminListeningServiceTests`) | Outcome |
| --- | --- | --- |
| L1 | `L1_ALearnersLesson_IsReadOnlyForAnAdmin_ButCanBeUnpublishedAndDeleted`, `L1_TheEditor_ShowsTheOwnerAndTheKindOfLesson`, `L1_ListLessons_ShowsSystemLessonsByDefault…` | Fixed: the list defaults to system lessons; a learner's lesson is read-only except unpublish and delete; the editor names the owner |
| L2 | `L2_L3_L10_SaveInfo_RejectsBadInput…` (YouTube cases) | Fixed: only ids and watch / youtu.be / embed / shorts / nocookie links are accepted and stored as the 11-character id; anything else is an error |
| L3 | same theory, `Level` cases | Fixed: level must be A1–C2 on the server |
| L4 | `L4_SaveQuiz_RejectsAnUnanswerableOrOversizedQuestion…` (16 cases), `L4_TheMessageNamesTheQuestionAndTheEmptyOption`, `L4_SaveQuiz_StoresTrimmedQuestions…` | Fixed: empty A or B no longer throws; the right answer must be A–D and filled; D only after C; limits; messages like "Câu 2: đáp án đúng C đang để trống."; saves are all-or-nothing and learner progress is untouched |
| L5 | `L5_SaveVocab_*` | Fixed: an empty definition no longer throws; limits and URL rule |
| L6 | `L6_SaveTranscript_*` | Fixed: times both empty or `0 ≤ start < end`, starts not going backwards, per-row messages ("Dòng 3: thời điểm kết thúc phải sau thời điểm bắt đầu."), all-or-nothing, a published lesson cannot lose its last line |
| L7 | `L7_Import_*` (9 tests) | Fixed: replacing existing lines needs `replaceExisting`; missing YouTube id and provider failures return friendly messages (the detail is only logged); a per-lesson guard answers "Đang nhập lời thoại, vui lòng chờ." to a second click and is released after success or failure |
| L8 | `L8_*` | Fixed: publishing needs at least one line; warnings (not blocking) for no quiz, no vocabulary, YouTube lesson without timed lines; `incomplete` = no transcript or an unanswerable question |
| L9 | `L9_Delete_*` | Fixed: the exact title is required; content and every learner's progress are removed in one transaction; the editor reports how many learners have progress (for the dialog) |
| L10 | info theory (`AudioUrl`, `ThumbnailUrl`, `Duration`) | Fixed: URLs follow the https/local rule; duration is normalised to `mm:ss` or rejected with "Thời lượng dạng mm:ss, ví dụ 04:12." |
| S5 (listening) | `S5_ListLessons_*` | Fixed in the service (controller switches in A5) |

**Decisions and deviations.**
- As in A3, the controller switches to the service together with the views in **A5**, so every local commit keeps all pages working. This includes the old routes `ManageTranscript`, `ManageQuiz`, `ManageVocab` redirecting (302) to the editor tabs.
- Free-text lesson fields collapse repeated whitespace (a learner sees one line); only quiz explanations keep line breaks. Limits chosen where the column has none: line text 1000, meaning 1000, definition 1000, example 1000, explanation 1000; 2000 lines, 100 questions and 200 vocabulary items per lesson.
- The importer stores a normalised duration (`03:07`) only when the lesson has none.
- A duplicate YouTube id among system lessons is a warning, not an error (the catalog importer keys lessons by video id, so the admin is told).

**Gate record — A4.**
- G1 pass: 0 errors; the web project still builds with 42 warnings (full rebuild), none from the new files.
- G2/G3 pass: 3415 tests (3328 + 87), 0 failed, 53 skipped.
- G4 pass: no JavaScript changed; node suite unchanged from A3 (513 tests, the same 1 known failure).
- G5 pass: learner `ListeningService` and the catalog rule (`OwnerUserId == null && IsPublished`) untouched; `has-pending-model-changes` → "No changes".
- G6 pass: no CSS or views changed; `git diff --check` clean.
- G7 pass: only new files under `Services/Admin/` and `TCTEnglish.Tests/`, plus the second DI line in `Program.cs`.
- G8–G12 n/a (no UI change in this phase). G13 pass (full suite incl. `Listening*`). G14 n/a. G15 pass: every write validates server-side, learner lessons are forbidden, URL rules, provider errors never reach the admin screen.
- Throwaway content: none.


### A5 — Listening admin UI (2026-11-18)

**Built.**
- `ListeningManagementController` is now thin and calls `IAdminListeningService`. Same routes and action names; new `Editor/{id}?tab=…`, `Publish`, `Unpublish`. `Create` renders the editor's Info tab; `Edit/{id}` and the old `ManageTranscript`, `ManageQuiz`, `ManageVocab` GET routes answer 302 to the matching tab; the POST saves keep their names (`CreateEdit`, `ManageTranscript`, `ManageQuiz`, `ManageVocab`, `ImportTranscript`, `Delete`) and now redirect back to the same tab, or re-render it with the typed rows and per-row messages. Learner lessons redirect with "Không sửa bài nghe riêng của học viên."
- Views: `Index` (4 figures, filter bar with level / source / status, thumbnails with a fallback, lines / quiz / vocabulary counts, incomplete tags, icon actions) and `Editor` with four tabs (`role="tablist"`, arrow keys, counts, create mode with the other tabs disabled), a read-only banner and `<fieldset disabled>` for learner lessons, the publish checklist, the "Xem như học viên" / publish / unpublish / delete toolbar, and the row partials `_TranscriptRow`, `_QuizRow`, `_VocabRow`. The four old views were removed.
- `admin-listening-editor.js`: add / move / remove rows with contiguous field names (`lines[0]…`), radio selections preserved while rows are renumbered, empty state, YouTube preview, vocabulary image preview, the "Thay {n} dòng hiện có bằng lời thoại từ YouTube?" confirmation and a double-click guard on the import. `admin-content.js`: tab keyboard navigation, and a discard link that does not trigger the leave warning.
- Transcript times are typed as seconds or `m:ss.s`; `AdminInputRules.TryParseClock` / `FormatClock` (+39 tests) parse and show them, and a value that is not a time is reported on its own field.

### A6 — Payments admin, presentation only (2026-11-18)

- `BillingManagementController.Index` only gained the figures and a `range` filter (7 / 30 / 90 days, default all); `Details` and every state action are untouched. Figures use one query each: revenue of paid orders in the last 30 days, active Premium users (distinct users with an active, unexpired subscription), pending orders that can still be paid (plus the number overdue), and orders in manual review. A manual-review alert links to `?status=manual_review`.
- The list uses the kit (header with the four actions, figures, `_AcFilterBar`, status tags from `PaymentOrderLabels.StatusLabel`, provider names, money as `399.000 ₫`, pager). The five other views (Details, Subscriptions, PremiumUsers, PremiumUserDetails, Health) keep **every** form, field name, action and confirmation and gain the kit header and alerts, Font Awesome icons, Vietnamese copy ("Tình trạng cổng thanh toán", "Gói đăng ký", "Người dùng Premium", "Đơn thanh toán", subscription statuses through the new read-only `AdminBillingLabels`) and `BillingTime.FormatMoney`. The header gained POST actions with a confirmation (`data-ac-confirm-message`) for "Đóng đơn chờ quá hạn".
- Decision: the five secondary pages keep their Bootstrap card and table markup inside `.ac-billing`, which restyles `.card`, `.table` and `.badge` to the kit look, instead of rewriting about 1,200 lines of forms.

### A7 — Notifications admin, presentation only (2026-11-18)

- `NotificationManagementController` gained a read-only recipient count for `Create` (the same filter as the send: not blocked, not anonymised), also shown again on a refused post; sending, batches and history are untouched.
- `Create`: two columns, counters `n/200` and `n/1000`, the link field and its hint, a preview that mirrors the learner bell item (icon, title, message, "Vừa xong", unread dot), a sticky bar "Sẽ gửi tới {n} tài khoản đang hoạt động" and the confirmation dialog "Gửi thông báo tới {n} tài khoản? … Không thể thu hồi sau khi gửi." with a preview of what will be sent. The previous inline script and inline styles are gone (generic `data-ac-bind`, `data-ac-show-if` and `data-ac-submit-form` in `admin-content.js`). `History` keeps its columns, caption and legacy / retention notes in the kit style.

### Gate record — A5, A6, A7 (one run for the three phases)

The host stopped two attempts at the whole suite for low memory (at A3 and at A5). After closing the leftover .NET build servers, the gate ran in two groups with low parallelism (`xUnit.MaxParallelThreads=2`), so A5, A6 and A7 were verified together and are committed together; the remaining phases return to one commit each.
- G1 pass: 0 errors; the web project builds with 40 warnings (42 before: the removed old listening views carried two).
- G2/G3 pass: 3483 tests, 0 failed, 53 skipped: group 1 (Admin, PersonalSet, Vocabulary, Listening, Billing, Notification) 1136 total, 1130 passed, 6 skipped; group 2 (everything else) 2347 total, 2300 passed, 47 skipped.
- G4 pass: 514 node tests, 513 passed; only the A0 speaking-catalog hash failure remains. The owner source tests in `test_owner_admin_views_sweep.cjs` were rewritten in A3 and again here (Listening list, speaker fields, Billing list) to the kit markup, keeping their accessibility intent.
- G5 pass: learner `ListeningService`, the catalog rule, `Services/Billing/**`, `NotificationService.cs`, models and migrations untouched; `has-pending-model-changes` → "No changes".
- G6 pass: no hex, `rgb()`, `:root` or `!important` in `admin-content.css`; every token exists; no `style=`, inline `<script>`, `onsubmit` or `bi bi-` in the four modules' views or the `_Ac*` partials; `git diff --check` clean.
- G7 pass: the `Program.cs` diff against `master` is exactly the two DI lines; `_AdminLayout.cshtml`, the shared admin CSS files, `tct-learning-ui.css`, `Services/Billing/**`, `NotificationService.cs`, `PersonalSetService*`, `ListeningService`, `Models/**`, migrations, appsettings and the .csproj are not in the diff.
- G8 (A5 and A6/A7 screenshots at 1440×900, 1366×768, 1024×768, 768×1024 and 390×844): no page-level horizontal overflow at any size after two fixes (the transcript time fields at 1366 and the Health workers table at 390); 0 failed requests and 0 console errors, except one external image: the listening fixture's YouTube thumbnail (`img.youtube.com/vi/9F8o8eH_Hms/hqdefault.jpg`) answers 404 because the fixture video id is not real; the preview falls back to an icon.
- G10 (listening, browser): 40 scripted checks pass: tab keys, add / move / remove rows with renumbering and kept answers, unsaved bar, leave warning, discard, Vietnamese per-row errors with kept values and nothing saved, YouTube preview, import confirmation (cancel only), delete dialog, and focus rings on every control of 6 pages. Billing and notification interactions are covered by the integration tests (figures against the database, range filter, every existing action still present, the cleanup POST, the recipient count, a refused post).
- G11/G12 pass for the new components (same colour pairs as A1; tabs with roving tabindex; dialogs return focus).
- G13 pass: the full suite above; as admin every other admin page and the learner pages answer 200.
- G14 pass: Font Awesome only; Vietnamese titles; the same partials and classes across the four modules. English kept on purpose: technical config names inside the Health page (BaseUrl, ReturnUrl, IPN, TmnCode) and raw provider payloads.
- G15 pass: antiforgery required on every POST of the four modules (tests); admin role and 403 for a standard user on every page; learner lessons and sets are read-only; no HTML built from data in the new JS (asserted by a node test); no credentials or `.tmp/` in the diff.
- MariaDB: every list filter, the editor and the figures run on the dev MariaDB without error (the earlier provider-only failure was fixed in A3). Billing and notification pages rendered against the dev data (40 real orders) with correct figures and money format.
- Not observed: Safari and Firefox; 200% zoom (A8); a real transcript import from YouTube (no live provider call was made on purpose); the billing and notification pages after the last copy fixes (Health labels, subscription statuses) in a browser — A8 re-renders them.
- Throwaway content: none created so far (the browser checks only read, cancelled dialogs, or submitted invalid forms that save nothing).
