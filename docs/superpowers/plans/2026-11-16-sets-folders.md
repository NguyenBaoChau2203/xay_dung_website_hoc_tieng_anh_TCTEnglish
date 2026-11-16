# Plan: Folders inside "Bộ thẻ của tôi" and a simpler sidebar

Save as `docs/superpowers/plans/2026-11-16-sets-folders.md`.
Execution prompt: `.ai/prompts/claude-code-sets-folders.md`.

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Menu bên trái:** bỏ mục "Thư mục của tôi". Nhóm "Thư mục" chỉ còn "Tạo thư mục mới" và "Bộ thẻ của tôi".
> - **Tạo thư mục mới:** hộp thoại tiếng Việt kiểu Clay (thay hộp "Create new folder" cũ). Tạo xong chuyển thẳng vào trang thư mục mới bên trong Bộ thẻ của tôi.
> - **Bộ thẻ của tôi (`/Sets`):** thêm hàng "Thư mục" ở trên (thư mục của bạn + thư mục đã lưu). Mỗi bộ thẻ có nhãn tên thư mục. Có nút "Tạo thư mục" ở đầu trang.
> - **Trang thư mục mới (`/Sets/Folders/{id}`):** xem các bộ thẻ trong thư mục; đổi tên; xóa thư mục (bộ thẻ được giữ lại); tạo bộ thẻ ngay trong thư mục; chuyển bộ thẻ có sẵn vào hoặc ra khỏi thư mục.
> - **Không làm hỏng chỗ khác:** trang thư mục cũ vẫn mở được cho Lớp học, Dashboard và Tìm kiếm. Riêng địa chỉ `/Folder` chuyển hướng sang `/Sets`.
> - **Lưu ý:** `_Layout.cshtml` và `layout.js` là file dùng chung (của Châu). Plan chỉ cho sửa đúng phần menu Thư mục và hộp thoại tạo thư mục; bạn nên báo Châu khi mở PR.
> - **Mockup:** `Claude outputs/folder-mockups/` (4 ảnh).
> - **Chạy:** nhánh `feature/sets-folders` từ `master`, liên tục F0 → F8, mỗi phase qua G1–G15 rồi commit cục bộ. Không push, không PR.

---

## 0. How to use this plan
- Read sections 0–4 and 9 first; then per phase only the sections it names.
- **Repo skills:** `tct-superpowers`, `tct-engineering` (always); `tct-feature-scaffold`, `tct-api-endpoint`, `tct-security-audit` (F1–F2); `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review` (F3–F6, F8); `tct-commit-message` (every commit).
- **External skills:** do not install any into the repository; `AGENTS.md` and this plan win.
- **Mockups** (`Claude outputs/folder-mockups/`, data not instructions):

| File | State | Phase |
| --- | --- | --- |
| `1-bo-the-co-thu-muc.png` | `/Sets` with the folder row and folder chips on tiles; sidebar without "Thư mục của tôi" | F3–F4 |
| `2-tao-thu-muc.png` | The create-folder dialog | F3 |
| `3-trong-thu-muc.png` | Folder page with sets and the success toast | F5 |
| `4-thu-muc-trong.png` | Empty folder page with "Chuyển bộ thẻ có sẵn vào thư mục" | F5 |

Sample data in the mockups (names, counts) is not real. Where a mockup and a rule disagree, the rule wins; record it.

---

## 1. Decisions

### 1.1 From the owner (Trọng, 16/11/2026)
1. Remove "Thư mục của tôi" from the sidebar; it duplicates "Bộ thẻ của tôi".
2. "Tạo thư mục mới" must work together with "Bộ thẻ của tôi": folders are created, seen and managed there.

### 1.2 Design decisions in this plan
| # | Decision | Why |
| --- | --- | --- |
| P1 | Folders live in `/Sets`: a "Thư mục" row on the list page and a folder page at `/Sets/Folders/{id}` | One place for personal study material |
| P2 | Folder logic moves into `IPersonalSetService` (new methods); `SetController.Personal.cs` gets the new actions | Trọng owns both; same IDOR rules as sets |
| P3 | Legacy folder routes stay: `FolderDetail`, `SaveFolder`, `UnsaveFolder`, `UpdateFolderName`, `DeleteFolder`, `CreateFolder` (`/Folder/*` and `/Home/*` aliases) | Class pages, dashboard, search, breadcrumbs and tests use them |
| P4 | `GET /Folder` (folder list) returns a redirect to `/Sets` (302) | Every old link to the list lands on the new place |
| P5 | Folders are flat in the new UI; nested legacy folders are listed as normal folders | The new UI has no use for nesting; no data change |
| P6 | Deleting a folder keeps its sets (they become "no folder"); a folder with child folders cannot be deleted from the new UI and says why | The model uses `ClientSetNull` for sets and `Restrict` for children |
| P7 | Folder names: trimmed, 1–100 characters, unique per owner ignoring case | Clear rules, no duplicate names in the row |
| P8 | Saved folders (other people's folders the user saved) appear in the row with "Đã lưu"; their page is read-only with "Bỏ lưu" | Nothing the user had disappears from view |
| P9 | No migration, no model change | Not needed |
| P10 | `_Layout.cshtml` and `layout.js` get the smallest possible edit (sidebar items, dialog markup, footer link) | Shared files of another owner |

---

## 2. Scope

### 2.1 May add
- `Views/Set/Folder.cshtml` (folder page), `Views/Set/_FolderTile.cshtml`, `Views/Shared/_CreateFolderDialog.cshtml` (or inline in the layout; record the choice)
- `ViewModels/Sets/SetFolderViewModels.cs`
- `TCTEnglish.Tests/SetFoldersIntegrationTests.cs`, `TCTEnglish.Tests/SetFolderServiceTests.cs` (or one file)
- `scripts/tests/test_folder_dialog.cjs` (if dialog JS is added)

### 2.2 May edit
- `Services/Sets/IPersonalSetService.cs`, `PersonalSetService.cs`, `PersonalSetService.Write.cs` (additive folder methods)
- `Controllers/SetController.Personal.cs` (new actions); `Controllers/SetController.cs` only the two legacy redirects to `"Folder", "Folder"` (lines ~130 and ~264) **if** no existing test asserts that redirect path
- `Controllers/FolderController.cs`: only `Index` (P4) and `CreateFolder` (redirect to the new folder page)
- `ViewModels/Sets/SetViewModels.cs` (additive: folder fields on tiles and the index model)
- `Views/Set/Index.cshtml`, `Views/Set/_SetTile.cshtml`, `Views/Set/CreateSet*`/info form views only to accept `?folderId=` and show the folder
- `wwwroot/css/personal-sets.css`, `wwwroot/js/personal-sets-common.js` (additive)
- **Shared, minimal:** `Views/Shared/_Layout.cshtml` (sidebar items lines ~293–317, dialog lines ~457–485, footer link line ~431) and `wwwroot/js/layout.js` (dialog open/close block lines ~132–160)
- Trọng's own tests where behaviour changes on purpose (record why)
- Docs: tracker rows "Bộ thẻ cá nhân" and "Lớp học, thư mục", `known-issues.md`, `bug-fix-log.md`, `project-structure.md`; this plan's section 12

### 2.3 Forbidden
`Program.cs`, appsettings, `.csproj`, migrations, `Models/**`; `Views/Folder/**` and `Class-Detail.css` (legacy pages stay as they are); `Views/Class/**`, `Views/Home/**`, `Views/Study/**`, `Views/Vocabulary/**`; `ClassService`, Goals, AI, account code; `layout.css`, `site.css`, `tct-learning-ui.css`, `study-kit.css` (except additive scope, recorded), `_PageHeader`, `_StateMessage`; any other part of `_Layout.cshtml` and `layout.js`; other owners' tests; new packages, fonts, CDNs.

### 2.4 Must stay unchanged
- `/Folder/FolderDetail/{id}`, `/Home/FolderDetail/{id}`, `SaveFolder`, `UnsaveFolder`, `UpdateFolderName`, `DeleteFolder` behaviour and their tests (`CriticalFlowSqliteIntegrationTests`, `FolderSetIdorRegressionTests`, `PersonalSetsIntegrationTests`, `Sprint1/3SmokeTests`).
- Class folder sharing, saved folders, set visibility rules, antiforgery and 404 rules.
- Existing data: no folder or set is renamed, moved or deleted except by the user's own action in the new UI.

---

## 3. Findings (static review 2026-11-16 on `master` `22c53ab`; re-verify at F0)

| # | Finding |
| --- | --- |
| A1 | Sidebar (`_Layout.cshtml` ~293–317): dropdown "Thư mục" → "Tạo thư mục mới" (button `#newFolderBtn`), "Thư mục của tôi" (`/Folder`), "Bộ thẻ của tôi" (`/Sets`). |
| A2 | Create dialog (`_Layout.cshtml` ~457–485, `#folderModal`) is in English ("Create new folder", "Folder name", "Decline", "Confirm"); posts `folderName` to `FolderController.CreateFolder`; JS in `layout.js` ~132–160. |
| A3 | `CreateFolder` redirects to the legacy `FolderDetail`; empty name redirects to `/Folder` silently; no length or duplicate check. |
| A4 | `/Sets` (`Views/Set/Index.cshtml`) shows no folders; `SetTile` has no folder name. The create/edit set form already offers a folder select (`AvailableFolders`, `ManageableFolders`). |
| A5 | `/Folder` (`Views/Folder/Folder.cshtml`) is the legacy list (English headings "Your folders", "Saved folders"). Links to it: dashboard (`Views/Home/Index.cshtml` 172, 242, 270), layout footer (431), redirects in `SetController` (130, 264), `FolderController` (62, 105), `StudyController` (101). |
| A6 | Links to `FolderDetail` from class detail, dashboard, search, set breadcrumbs (`Views/Set/Detail.cshtml` line 10), `Views/Study/Speaking.cshtml`. |
| A7 | `Folder` → `Set` FK is `ClientSetNull`; `ParentFolder` is `Restrict`. `DeleteFolder` includes sets, so sets survive with `FolderId = null`. |
| A8 | Folder access for sets already exists in `PersonalSetService` (owner, saved folder, class folder). |

---

## 4. Specification

### 4.1 Service (`IPersonalSetService`, additive)
```csharp
Task<IReadOnlyList<SetFolderTileViewModel>> ListFoldersAsync(int userId, CancellationToken ct);           // own + saved, ordered by name
Task<SetFolderPageViewModel?> GetFolderPageAsync(int folderId, int userId, bool isAdmin, CancellationToken ct); // null → 404
Task<OperationResult<int>> CreateFolderAsync(int userId, string? name, CancellationToken ct);               // returns new id
Task<OperationResult> RenameFolderAsync(int folderId, int userId, bool isAdmin, string? name, CancellationToken ct);
Task<OperationResult> DeleteFolderAsync(int folderId, int userId, bool isAdmin, CancellationToken ct);
Task<OperationResult> MoveSetToFolderAsync(int setId, int? folderId, int userId, bool isAdmin, CancellationToken ct); // null = remove from folder
```
Use the repo's existing `OperationResult` / `OperationStatus` types (discover the exact generic form).
- **Name rule (P7):** trim; empty → `Validation` "Hãy nhập tên thư mục."; > 100 → "Tên thư mục tối đa 100 ký tự."; duplicate among the owner's folders (case-insensitive, trimmed) → "Bạn đã có thư mục tên này." Rename to the same name is a no-op success.
- **Access:** manage = owner (or admin, as `ManageableFolders` does); view = owner, saved follower, or member of a class the folder is shared with (reuse the existing rule; do not invent a new one). Anything else → `NotFound`.
- **Delete:** owner only; folder with child folders → `Conflict` "Thư mục này còn thư mục con. Hãy mở trang thư mục cũ để xử lý trước." (with a link to the legacy page); otherwise set every set's `FolderId` to null and remove the folder (and its `SavedFolders` rows) in one transaction.
- **Move:** the set must be owned by the user (or admin); the target folder must be manageable by the user; `folderId` null removes it from its folder.
- **Folder tile data:** id, name, own/saved flag, owner display name (saved only), set count (visible sets only), word count, due count for the user (same due rule `/Sets` already uses). One grouped query; no per-folder queries.

### 4.2 Endpoints (`SetController.Personal.cs`, `[Authorize]`, POSTs with `[ValidateAntiForgeryToken]`)
| Method + route | Action | Success | Errors |
| --- | --- | --- | --- |
| `GET /Sets/Folders/{id:int}` | folder page | 200 | 404 |
| `POST /Sets/Folders/Create` (`name`, optional `returnUrl` local only) | create | redirect `/Sets/Folders/{newId}` + TempData toast "Đã tạo thư mục \"{tên}\"." | validation → redirect back to `returnUrl` (or `/Sets`) with TempData error and the typed name; for `Accept: application/json` return 200 `{ ok, id, url }` / 400 `{ ok:false, error }` |
| `POST /Sets/Folders/{id}/Rename` (`name`) | rename | redirect folder page + "Đã đổi tên thư mục." | 400 message / 404 |
| `POST /Sets/Folders/{id}/Delete` | delete | redirect `/Sets` + "Đã xóa thư mục. Các bộ thẻ vẫn còn trong Bộ thẻ của tôi." | 409 message / 404 |
| `POST /Sets/{setId}/MoveToFolder` (`folderId` or empty) | move | redirect back (local `returnUrl`) + "Đã chuyển bộ thẻ vào \"{tên}\"." / "Đã bỏ bộ thẻ khỏi thư mục." | 404 |
| `POST /Sets/Folders/{id}/Unsave` | unsave a saved folder | redirect `/Sets` + "Đã bỏ lưu thư mục." | 404 |

`returnUrl` must pass `Url.IsLocalUrl`. Routes must not clash with existing `/Sets/{id}` routes (check route constraints at F0).

**Legacy adjustments (P3, P4):**
- `FolderController.Index` (`GET /Folder`) → `RedirectToAction("Index", "Set")` (302). Keep the action name so existing `asp-action="Folder"` links still resolve.
- `FolderController.CreateFolder` → delegate to `CreateFolderAsync`; on success redirect to `/Sets/Folders/{id}`; on validation error redirect to `/Sets` with the message. Keep route, parameter name `folderName` and antiforgery.
- `SetController` redirects after legacy create/edit (~130, ~264): folder → `/Sets/Folders/{id}`, none → `/Sets`, unless an existing test asserts the old path (then leave and record).
- Everything else in `FolderController` unchanged.

### 4.3 Sidebar and dialog (shared files, minimal)
- `_Layout.cshtml`: delete the "Thư mục của tôi" `<a>`; keep "Tạo thư mục mới" and "Bộ thẻ của tôi". The dropdown is active (`IsActive`) for `Set` as well as `Folder` so it opens on `/Sets` and `/Sets/Folders/*`. Footer link "Thư mục" → `/Sets` with text "Bộ thẻ của tôi" (or keep the text; record).
- Replace the `#folderModal` content with the Clay dialog (mockup 2), Vietnamese copy:
  - title "Tạo thư mục mới"; text "Dùng thư mục để gom các bộ thẻ cùng chủ đề. Thư mục hiện ngay trong Bộ thẻ của tôi.";
  - label "Tên thư mục"; input `name="name"`, `maxlength="100"`, `required`, `autocomplete="off"`; live counter "n/100";
  - buttons "Hủy" and "Tạo thư mục" (pending text "Đang tạo…", disabled while sending);
  - posts to `/Sets/Folders/Create` with antiforgery and `returnUrl` = current path.
- Prefer a native `<dialog>` element (`showModal`, `Esc`, focus trap by the browser) with a fallback to the current overlay class if the layout's CSS requires it. Focus goes to the input on open and back to the trigger on close. Keep the ids `newFolderBtn`, `folderModal`, `cancelFolderBtn` so any other script keeps working (search the repo for them first).
- `layout.js`: only the dialog block changes.
- Styles: the dialog appears on every page, so `personal-sets.css` (loaded only on `/Sets`) cannot style it. Use the shared modal classes and the Clay utilities already loaded on every page. If a style is still missing, add a scoped `<style>` inside the dialog partial with tokens only, and record it as a proposal for Châu.
- Also open the same dialog from the `/Sets` header button "Tạo thư mục" and the dashed tile "Tạo thư mục mới" (mockup 1), via `data-open-folder-dialog`.

### 4.4 `/Sets` list (mockup 1)
- Header actions: secondary "Tạo thư mục" (`fa-folder-plus`, opens the dialog) and the existing primary "Tạo bộ thẻ".
- New section **"Thư mục"** with count, above "Của tôi", shown on the "Tất cả" and "Của tôi" filters (saved folders also on "Được chia sẻ"). Grid of `_FolderTile` (4 per row at 1440, 2 at 768, 1 at 390): coloured folder icon (hue picked from the folder id over the four kit hues), name (ellipsis, full name in `title`), meta "n bộ thẻ · m từ" or for saved "Đã lưu" chip + "n bộ thẻ"; a due chip "x cần ôn" when > 0; whole tile is one link. Last tile: dashed "Tạo thư mục mới" button.
- No folders: the section shows only the dashed tile with "Tạo thư mục đầu tiên để gom các bộ thẻ cùng chủ đề." (no empty heading noise).
- `_SetTile`: a small chip with folder icon and folder name when the set is in a folder (own and subscribed sets). The empty-state of `/Sets` keeps its current actions and adds "Tạo thư mục".

### 4.5 Folder page `/Sets/Folders/{id}` (mockups 3, 4)
- Breadcrumb "Bộ thẻ của tôi / Thư mục"; header card: icon, folder name (`h1`), "n bộ thẻ · m từ · x từ cần ôn" (omit zero parts).
- **Owner actions:** "Đổi tên" (inline form or small dialog with the same name rules), "Xóa thư mục" (confirm dialog: "Xóa thư mục \"{tên}\"? Các bộ thẻ bên trong không bị xóa, chúng chỉ ra khỏi thư mục." buttons "Hủy" / "Xóa thư mục"), primary "Tạo bộ thẻ trong thư mục" → `/Sets/Create?folderId={id}` (the form preselects the folder; check the existing create route name).
- **Saved folder:** owner name, "Bỏ lưu" button; no edit actions.
- Section "Bộ thẻ trong thư mục" with count, the existing `_SetTile` grid (only sets the viewer may see), and, for the owner, on each tile a menu or small button "Bỏ khỏi thư mục".
- Section actions: "Luyện x từ đến hạn" only if `/Practice` already accepts a list of set ids or a folder scope (check at F0; if not, omit and record); "Thêm bộ thẻ có sẵn" opens a panel listing the owner's sets that are not in this folder with "Chuyển vào" per row (shows the current folder name, mockup 4).
- Empty folder: state card "Thư mục này chưa có bộ thẻ nào", text "Tạo bộ thẻ mới ngay trong thư mục, hoặc chuyển bộ thẻ có sẵn vào đây.", primary button and the move list inline (mockup 4). No own sets at all → only the create button.
- Success and error messages through the existing `_SetAlerts` / toast pattern of `/Sets`.

### 4.6 Create-set flow
- `GET /Sets/Create?folderId=` preselects the folder when manageable; otherwise ignore the parameter (no error).
- After creating a set inside a folder, the existing redirect to "add words" stays; the breadcrumb of the set detail (`Views/Set/Detail.cshtml` line 10) points to `/Sets/Folders/{id}` for folders the viewer can open in the new UI (owner/saved), else keeps the legacy link.

### 4.7 Accessibility and style
Clay kit and tokens only (`k-*`, `--tct-*`); no hex, `rgb()`, `:root`, `!important`; focus visible; dialog and menus keyboard-complete (`Esc`, focus return); tiles are links with readable names ("Thư mục Công việc, 2 bộ thẻ, 74 từ"); counts and states not by colour alone; forced-colours and reduced-motion handled; touch targets ≥ 44 px; long Vietnamese names wrap or ellipsis without hiding meaning.

### 4.8 Tests
- **Service:** create (trim, empty, 100/101 chars, duplicate ignoring case); rename (same name, duplicate, foreign → not found); delete (sets kept with null folder, saved rows removed, child folder → conflict, foreign → not found); move (own set into own folder, out of folder, foreign set, foreign folder); list (own + saved, counts only visible sets, due counts).
- **Integration:** sidebar no longer contains "Thư mục của tôi" and still contains "Tạo thư mục mới" and "Bộ thẻ của tôi"; dialog markup has the Vietnamese strings and posts to `/Sets/Folders/Create` with a token; `GET /Folder` → 302 `/Sets`; legacy `POST /Folder/CreateFolder` → redirect `/Sets/Folders/{id}`; `/Sets` shows the folder row and the folder chip; folder page 200 for owner and saved follower, 404 for a stranger; all POSTs reject a missing token; JSON variant of create.
- **Unchanged:** all existing folder, set, class and smoke tests green without edits (except Trọng's own tests listed in section 12 with a reason).
- **JS:** if the dialog has logic beyond open/close (counter, pending, JSON submit), a node test for it.

---

## 5. Phases

Run mode: continuous, autonomous, one agent. After each green gate: gate record into section 12 → commit → next phase.

| Phase | Work | Read |
| --- | --- | --- |
| **F0 Baseline** | Clean-tree check (leave `appsettings.Development.json` and `Claude outputs/` unstaged); `git switch master`; verify `22c53ab` or later; create `feature/sets-folders`; build; full + node tests (record counts); save plan and prompt; confirm A1–A8; search for `newFolderBtn`, `folderModal`, `cancelFolderBtn`, `"/Folder"` assertions, route constraints on `/Sets/*`, Practice folder scope, `OperationResult` shape; start the app (`scripts/start-local.ps1`); before-screenshots of the sidebar, `/Sets`, `/Folder`, a legacy folder page at 1440 and 390 into `.tmp/sets-folders/before/` | 0–4 |
| **F1 Service** | Tests first; folder methods | 4.1, 4.8 |
| **F2 Endpoints and legacy redirects** | Tests first; new actions; `FolderController.Index`/`CreateFolder`; `SetController` redirects | 4.2, 4.8 |
| **F3 Sidebar and dialog** | `_Layout.cshtml` + `layout.js` minimal edits; dialog partial; open from `/Sets` | 4.3 |
| **F4 `/Sets` list** | Folder row, folder tiles, set tile chip, empty state | 4.4 |
| **F5 Folder page** | Page, rename, delete, unsave, move in/out, empty state | 4.5 |
| **F6 Create-set flow and breadcrumbs** | 4.6 | 4.6 |
| **F7 Docs** | Tracker rows, known-issues (note the shared-file edit for Châu), bug-fix-log, project-structure | 2.2 |
| **F8 Full gate** | Whole gate on every changed page and every legacy folder route; end-to-end run (5.4); after-screenshots into `.tmp/sets-folders/after/` | all |

### 5.1 Phase gate
| Gate | Check |
| --- | --- |
| G1 | build; warnings ≤ F0 |
| G2 | area tests (`Set*`, `Folder*`, `PersonalSets*`, `CriticalFlow*`, `Sprint*Smoke*`) |
| G3 | full suite; counts not below F0 plus new tests |
| G4 | node tests; only F0 failures may remain |
| G5 | contracts: legacy folder routes and their responses, class/dashboard/search links, antiforgery, 404 rules |
| G6 | tokens-only grep on changed CSS; no inline colours; `git diff --check` |
| G7 | file scope: changed paths only from 2.1/2.2; diff of `_Layout.cshtml` and `layout.js` limited to the listed blocks |
| G8 | render at 1440×900, 1366×768, 1024, 768, 390, 320, 200% zoom; console and network clean |
| G9 | mockup comparison; consistency with the rest of `/Sets` |
| G10 | behaviour checklist (5.3) by mouse and keyboard only |
| G11 | accessibility: names, focus, dialog, forced colours, reduced motion |
| G12 | scripted contrast on new pairs |
| G13 | regression: dashboard folder links, class detail folder link, search results, set breadcrumbs, legacy folder page, practice |
| G14 | Clay conformance grep |
| G15 | security: IDOR tests, antiforgery tests, local `returnUrl` only, no credentials/screenshots/`.tmp/` in the diff |

### 5.2 Failure and stop rules
- A failed item: fix and rerun the whole gate; at most 3 attempts per item; then "deferred" (phase-caused, reverted) or "blocked" (out of scope, evidence).
- **Stop** only if green needs a forbidden file, a migration, or a broader change to `_Layout.cshtml`/`layout.js`, or if the base state is unsafe. Never skip, weaken or delete a test.

### 5.3 Behaviour checklist
- **Sidebar:** "Thư mục của tôi" gone at every size (desktop and the mobile menu); the "Thư mục" group opens on `/Sets` and folder pages; "Tạo thư mục mới" opens the dialog from any page.
- **Dialog:** focus in the input; counter; `Esc` and "Hủy" close and return focus; empty, 101-character and duplicate names show the Vietnamese messages and keep the typed text; double click creates one folder; success lands on the new folder page with the toast.
- **`/Sets`:** folder row shows own and saved folders with correct counts; dashed tile opens the dialog; set tiles show the folder chip; filters still work.
- **Folder page:** rename (rules), delete (sets remain, toast, back to `/Sets`), child-folder conflict message, create set with the folder preselected, move a set in and out, saved folder read-only with "Bỏ lưu", stranger gets 404.
- **Legacy:** `/Folder` → `/Sets`; legacy folder detail, save/unsave, rename, delete still work; dashboard and class links open.

### 5.4 End-to-end (F8)
With a synthetic learner from `.appdata/local/access.json` (never print the password): create folder "Công việc" from the sidebar on `/Home/Index` → land on its page → create a set inside → back to `/Sets`, see the folder tile and the chip → move another set in → rename the folder → delete it → both sets still listed without a chip. Remove the throwaway data afterwards; never reset the database.

---

## 9. Constraints
- One agent, local only. Granted: local commits on `feature/sets-folders`, one per phase, via `tct-commit-message`. Not granted: push, PR, merge, rebase, reset, deploy.
- Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, screenshots.
- Never reset or reseed the database; no paid AI calls; never print credentials.
- Plan and code comments in English; UI copy in Vietnamese exactly as written here.
- `feature/landing-night` is still unmerged and does not touch these files; no conflict expected.

## 10. Definition of Done
- [ ] Sidebar shows only "Tạo thư mục mới" and "Bộ thẻ của tôi" under "Thư mục".
- [ ] The Vietnamese Clay dialog creates folders with the name rules and lands on the new folder page.
- [ ] `/Sets` shows own and saved folders and the folder chip on set tiles.
- [ ] Folder page supports rename, delete (sets kept), create set inside, move in/out, unsave.
- [ ] `/Folder` redirects to `/Sets`; every legacy folder route and every other owner's link still works.
- [ ] All new behaviour tested; all existing tests green and unedited (except listed own tests).
- [ ] Shared-file edits limited to the listed blocks and noted for Châu.
- [ ] Sizes, keyboard, forced colours, reduced motion, contrast recorded; docs and section 12 updated.

## 11. Risks
| Risk | Mitigation |
| --- | --- |
| Shared layout edit conflicts with Châu's work | Smallest diff; G7 checks the blocks; note in known-issues and the handoff |
| A test asserts `/Folder` as a redirect target | Check at F0; keep the old target where asserted |
| Nested legacy folders | Listed flat; delete blocked with a clear message and a legacy link |
| Saved/class folders confuse ownership | Read-only page for non-owners; reuse existing access rules |
| Route clash with `/Sets/{id}` | Literal `Folders` segment and `int` constraints; checked at F0 |

## 12. Execution notes
(The agent appends F0 discovery notes and one gate record per phase here.)


### F0 — Baseline and discovery (2026-11-16) — `docs(sets-folders): save the plan and execution prompt`
- Base: `master` at `97c1b54` (contains `22c53ab`, includes the landing work); branch `feature/sets-folders` created. Unrelated `appsettings.Development.json` change and `Claude outputs/` left unstaged. `feature/landing-night` is merged into `master` and does not touch these files.
- Baseline figures are those measured on the identical tree (`97c1b54`) a few minutes earlier, so they were not repeated: clean rebuild 0 errors and 58 warnings; .NET 1760 passed, 35 skipped, 0 failed; node 369 / 370 (the one failure is the known `test_speaking_catalog_packages.cjs`).
- Findings A1–A5, A7, A8 confirmed in source (sidebar `_Layout.cshtml` 293–318; dialog 457–486; `layout.js` 130–160; `FolderController.CreateFolder` has no length or duplicate check and redirects empty names to the list; `PersonalSetService.ViewableSets` and `ManageableFolders` hold the access rules). A6 (links to `FolderDetail`) is re-checked in F6.
- **Answers to the unknowns:**
  1. `newFolderBtn`, `folderModal`, `cancelFolderBtn`: used by `_Layout.cshtml` (303, 458, 475), `layout.js` (131–159) and, with duplicate ids, by the legacy `Views/Folder/Folder.cshtml`, which will no longer be rendered once `GET /Home/Folder` redirects. `tct-learning-ui.css:152` only mentions `#folderModal` in a comment. No test or script uses them.
  2. The folder-list route is `/Home/Folder` (`FolderController` has `[Route("Home")]`); a bare `/Folder` is not a route. Tests that touch it: `FolderSetIdorRegressionTests` asserts that legacy `DeleteFolder` redirects to `/Home/Folder` (line 243) and that legacy rename, `EditSet` and set deletion in a folder redirect to `/Home/FolderDetail/{id}` (197, 211, 269), and it fetches antiforgery tokens from `/Home/Folder`; `IntegrationTestClientHelper.GetAntiForgeryTokenAsync` follows one relative redirect, so a `302 /Sets` keeps those tests valid. AI guide data and tests only quote `/Home/Folder` as text. No test asserts `CreateFolder` or the old dialog or sidebar strings.
  3. `/Sets` routes use `{id:int}` or literal segments (`Cards/{cardId:int}`, `Subscribe`, `Clone`, `AddSamples`); a literal `Folders` segment does not clash.
  4. `/Practice` takes one `set` or a word list; there is no folder or multi-set scope, so "Luyện x từ đến hạn" is omitted from the folder page (plan 4.5).
  5. `OperationStatus` has only `Success`, `NotFound`, `Invalid` and `Services/OperationResult.cs` is not an allowed file. The "folder has child folders" case therefore returns `Invalid` with the field-error key `ChildFolders`, which the controller maps to 409.
  6. Create-set: the form is `GET /Home/CreateSet?folderId=` (already accepts the folder and ignores a non-manageable one through `GetNewSetFormAsync`); `POST /Sets/Create` creates and redirects to `/Sets/{id}/Add`. There is no `GET /Sets/Create`, so the folder page links to `/Home/CreateSet?folderId={id}`.
  7. Decision: `SetController.cs` is not edited. Its folder branches are asserted by tests (`/Home/FolderDetail/{id}`), and its no-folder branches go to `/Home/Folder`, which now redirects to `/Sets`.
  8. Dialog styling: `layout.css` already carries `.modal-overlay`, `.tct-modal`, `.modal-title`, `.modal-input`, `.modal-actions`, `.btn-decline`, `.btn-confirm`; the new dialog is a native `<dialog>` with a small scoped style block of tokens (proposal for Châu, F3).
- Before-screenshots (Standard learner, 1440 and 390; dashboard, `/Sets`, legacy folder list, legacy folder page, sidebar group open, the old English dialog): `.tmp/sets-folders/before/`; console and network clean on all. The sidebar group today reads "Tạo thư mục mới", "Thư mục của tôi", "Bộ thẻ của tôi".


### F1 — Folder service methods (2026-11-16) — `feat(sets): add the folder rules to the personal-set service`
- Work: seven additive methods on `IPersonalSetService` and `PersonalSetService` (`ListFoldersAsync`, `GetFolderPageAsync`, `CreateFolderAsync`, `RenameFolderAsync`, `DeleteFolderAsync`, `MoveSetToFolderAsync`, `UnsaveFolderAsync`), new view models in `ViewModels/Sets/SetFolderViewModels.cs`, and additive fields (`FolderId`, `FolderName` on the set tile, `Folders` on the index model). `GetSetIndexAsync` now returns the folder row per filter (all: own and saved; own: own; shared: saved; subscribed: none) and the folder chip data. No migration and no model change.
- Tests first: `TCTEnglish.Tests/SetFolderServiceTests.cs` (27 tests) was written before the code; the test project did not compile against the old service (missing members), then all 27 pass.
- Deviations from plan 4.1, all additive and recorded: (1) `MoveSetToFolderAsync` returns `OperationResult<string?>` (the target folder name, or null when the set left its folder) so the toast can name the folder; (2) an extra `UnsaveFolderAsync` backs the new unsave endpoint; (3) a set can only be moved into a folder of the same owner (even for an admin) and the folder page lists only the folder owner's sets as movable, so a folder never mixes owners; (4) `OperationStatus` has no conflict value (not an allowed file), so a folder with child folders returns `Invalid` with the field-error key `ChildFolders`; (5) delete also removes the folder's class links (`ClassFolders`) with its saves, in the same transaction; (6) the duplicate-name check runs in memory with `OrdinalIgnoreCase`, because SQLite's `lower()` only handles ASCII and the rule must not depend on the database collation; (7) names sort by a case, accent and "đ" insensitive key, so they do not depend on ICU; (8) a folder name is shown on a set tile only to someone who owns or saved the folder, so a subscriber never sees the name of a private folder of the set's owner.
- G1 pass: 0 errors, 58 warnings (the clean-rebuild baseline). G2 pass: area tests inside the full run. G3 pass: 1787 passed (1760 + 27), 35 skipped, 0 failed. G4 pass: node 369 / 370 (known Speaking failure only).
- G5 pass: no controller, view, route or JSON contract changed yet. G6 pass: no CSS; `git diff --check` clean (apart from the unrelated, unstaged `appsettings.Development.json`). G7 pass: changed paths are `IPersonalSetService.cs`, `PersonalSetService.cs`, `PersonalSetService.Write.cs`, `SetViewModels.cs`, the new `SetFolderViewModels.cs`, the new test file and this plan, all in 2.1 or 2.2.
- G8 to G12, G14 n-a (no UI). G13 pass: existing set, folder and smoke tests unchanged and green. G15 pass: IDOR cases are tested at the service (foreign folder or set, a saved follower or a stranger renaming, deleting or moving gets NotFound; a stranger gets no folder page; a class member gets a read-only page).
- Not observed: the new methods against MariaDB (only the in-memory SQLite used by the existing service tests); `ExecuteUpdate` and `ExecuteDelete` are already used by this service, no schema is involved.


### F2 — Endpoints and legacy create (2026-11-16) — `feat(sets): add the folder endpoints and route the legacy create through the service`
- Work: six actions in `SetController.Personal.cs` (`GET /Sets/Folders/{id}`; `POST /Sets/Folders/Create`, `/{id}/Rename`, `/{id}/Delete`, `/{id}/Unsave`; `POST /Sets/{setId}/MoveToFolder`), all under the controller's `[Authorize]` and `[AutoValidateAntiforgeryToken]`; `returnUrl` is honoured only when `Url.IsLocalUrl`; a minimal `Views/Set/Folder.cshtml` so the page answers (the full page is F5). `FolderController.CreateFolder` now calls `CreateFolderAsync` (same rules as the new dialog), lands on `/Sets/Folders/{id}` on success and on `/Sets` with the message on a validation error; its route, parameter name and antiforgery are unchanged.
- Tests first: `SetFoldersIntegrationTests.cs` (17 tests) was run red (17 of 17 failed: the routes did not exist), then green.
- **Order change.** Plan 4.2 and the F2 row put `GET /Home/Folder` → `/Sets` in this phase. It moves to F3: the existing `FolderSetIdorRegressionTests` read their antiforgery token from `/Home/Folder` and the helper follows only one redirect, so the target must render a token for every signed-in user. `/Sets` only does so in some states today; the create-folder dialog that F3 adds to the layout (a form on every page) guarantees it. `SetController.cs` stays untouched (see F0 item 7).
- Behaviour notes: for plain forms a rename or delete that fails validation redirects back to the folder page with the message (and the typed name) in TempData; the plan's 400 and 409 are returned when the request sends `Accept: application/json`. The create dialog reads the TempData keys `FolderDialogError` and `FolderDialogName` (F3). A folder with child folders answers 409 for JSON clients.
- G1 pass: 0 errors, 58 warnings. G2 pass. G3 pass: 1804 passed (1787 + 17), 35 skipped, 0 failed. The first full run had one failure, `OpenAiConsumerPipelineTests.LocalWriting_CloudFailureOrThreeSecondBudget…(cloud: "slow")`, a test with a three-second timing budget in the AI writing code, unrelated to folders; it passed 3 of 3 when run alone and the whole suite passed on the rerun. G4 pass: node 369 / 370 (known Speaking failure only).
- G5 pass: legacy routes and their redirects unchanged (`DeleteFolder` still redirects to `/Home/Folder`, rename and set edits still to `/Home/FolderDetail/{id}`); only the legacy `CreateFolder` result changed. G6 pass; G7 pass: `SetController.Personal.cs`, `FolderController.cs` (only `CreateFolder` and one `using`), the new view and test file, this plan.
- G15 pass: IDOR (stranger and saved follower get 404 on rename, delete, move; a stranger gets 404 on the page; a class member gets the read-only page), antiforgery (all six POSTs, including the legacy one, answer 400 without a token), local-only `returnUrl` (absolute and `//host` URLs fall back to `/Sets`). G8 to G14 n-a (no styled UI yet).


### F3 — Sidebar and the create dialog (2026-11-16) — `feat(sets): drop "Thư mục của tôi" and add the Vietnamese create-folder dialog`
- Work, **shared files** (`_Layout.cshtml` and `layout.js`, both Châu's) with the smallest diff:
  - `_Layout.cshtml` (4 insertions, 36 deletions): (1) the "Thư mục" group button is also `active` for the `Set` controller (`@(IsActive("Folder") == "active" || IsActive("Set") == "active" ? "active" : "")`), so the group opens on `/Sets` and the folder pages; (2) the `Thư mục của tôi` link is removed; (3) the footer link "Thư mục" now points to `Set/Index` (text kept); (4) the old English `#folderModal` overlay block is replaced by `<partial name="_CreateFolderDialog" />`. Nothing else in the file changed.
  - `layout.js`: only block "3." (the old modal handler) is replaced by the dialog logic (open from `#newFolderBtn` and any `[data-open-folder-dialog]`, counter, blank-name guard, one click one folder, restore on bfcache, reopen after a failed create, focus return with a fallback to the menu button when the phone drawer is inert). No `innerHTML`.
- New `Views/Shared/_CreateFolderDialog.cshtml` (choice recorded: a shared partial rather than inline markup): native `<dialog id="folderModal">` (Esc, focus trap and backdrop from the browser), Vietnamese copy from plan 4.3, posts to `/Sets/Folders/Create` with the antiforgery token and a local `returnUrl`; the ids `newFolderBtn`, `folderModal`, `cancelFolderBtn` are kept. Styles: a small scoped `<style>` of shared `--tct-*` tokens (the dialog is on every page, `personal-sets.css` is not) plus the Clay `.btn` and `.form-control` classes; **proposal for Châu**: move it to a shared stylesheet.
- `FolderController.Index` (`GET /Home/Folder`) now redirects to `/Sets` (302, relative Location); moved here from F2 (see F2). `BuildFolderPageViewModelAsync` is left in place (unused; outside the allowed edits).
- Tests: 8 more integration tests in `SetFoldersIntegrationTests.cs` (sidebar items; group active only on set and folder pages; Vietnamese dialog markup, token, return URL; reopen once with message and typed name; every signed-in page carries a token; redirect) and `scripts/tests/test_folder_dialog.cjs` (13 node tests on `layout.js` against a fake DOM).
- **Defects found by the tests and in the browser, fixed in this phase:** (1) Razor rendered `data-open-on-load=""` for a null value, which `hasAttribute` reads as true, so the dialog would have opened on every page load; the attribute name is now emitted conditionally. (2) The shared reset `margin: 0` pinned the modal to the top-left corner; the dialog sets `inset: 0; margin: auto`. (3) The check icon touched the button text; buttons are inline-flex with a gap. (4) On a phone the drawer closes and becomes inert behind the dialog, so focus fell to the page body; it now returns to the menu button.
- G1 pass: 0 errors, 58 warnings. G2/G3 pass: 1812 passed (1804 + 8), 35 skipped, 0 failed. G4 pass: node 382 / 383 (369 + 13 new; known Speaking failure only).
- G5 pass: legacy `DeleteFolder`, rename, `EditSet` and set-deletion redirects unchanged and their tests green; `/Home/Folder` redirect keeps the token-reading tests green. G6 pass: no hex, `rgb()`, `:root` or `!important` in the new partial. G7 pass: `_Layout.cshtml`, `layout.js`, the new partial, `FolderController.cs`, tests, plan.
- G8/G10/G11 (headless Chrome, 23 checks at 1440 and 390 plus 21 at 1366, 1024, 768, 320 and 200% zoom, on `/Sets`, the dashboard and a folder page): the group shows only "Tạo thư mục mới" and "Bộ thẻ của tôi" and is open on `/Sets`; the dialog opens with focus in the field and 0/100; the counter follows typing; `Esc` and "Hủy" close it and return focus; an empty name shows "Hãy nhập tên thư mục." and stays on the page; Tab never leaves the dialog; a real create lands on `/Sets/Folders/{id}` with the name and the message; a duplicate reopens the dialog with the message and the typed text and does not come back on the next load; at every size the dialog is centred, inside the viewport, buttons are at least 40 px high and there is no horizontal scroll; console and network are clean. Throwaway folders created by the run were deleted through the new endpoint.
- G9 mockup: matches `2-tao-thu-muc.png` (icon tile, title, text, label, counter, "Hủy" and "Tạo thư mục"); the sidebar matches `1-bo-the-co-thu-muc.png`. G12 pass: title and label 17.10, description and counter 5.87, error 6.46, folder icon on its tile 6.84 (UI part, min 3). G13 pass: dashboard, class and legacy folder pages unchanged; `/Home/Folder` lands on `/Sets`. G14 pass (tokens only). G15 pass: the create POST needs the token; `returnUrl` must be local; no `innerHTML`.
- Not observed: the last CSS touch-ups (forced-colours border, `nowrap` on the dialog buttons) were checked by build and tests only, not by a second browser pass; the final F8 run covers them.


### F4 — `/Sets` folder row and the folder chip (2026-11-16) — `feat(sets): show folders and folder chips on Bộ thẻ của tôi`
- Work: `Views/Set/Index.cshtml` (folder section above the set groups, header link "Tạo thư mục", empty-state button), new `Views/Set/_FolderTile.cshtml`, `Views/Set/_SetTile.cshtml` (folder chip), additive rules in `wwwroot/css/personal-sets.css` (tokens only, every selector under `.ts-page`, a forced-colours block). The folder row shows on "Tất cả" (own and saved) and "Của tôi" (own) and, for saved folders only, on "Được chia sẻ"; "Đang theo học" has none. With no folder the section shows only the dashed tile "Tạo thư mục đầu tiên để gom các bộ thẻ cùng chủ đề." (no empty heading).
- **Header action.** `_PageHeader` only renders navigation links (shared file, not editable), so "Tạo thư mục" is a link to `/Sets#tao-thu-muc`: it works without JavaScript (the page loads and the dialog opens from the hash), and `layout.js` opens the dialog on click, on load and on `hashchange`, then removes the hash. This adds three small pieces to the dialog block of `layout.js` (the selector `a[href$='#tao-thu-muc']`, `clearFolderHash`, the load and `hashchange` checks) on top of F3; nothing else in `layout.js` or `_Layout.cshtml` changed.
- Tile accessibility: one link names the whole tile ("Thư mục Công việc, 2 bộ thẻ, 74 từ", plus due words, plus "đã lưu từ {tên}" for a saved folder) and the visible text is hidden from assistive technology so nothing is read twice; the whole tile is the click target; names clamp to two lines with the full name in `title`.
- Tests first: 6 integration tests were run red (6 of 6 failed), then green; 4 more node tests for the header link, the hash on load, dropping the hash and `hashchange` (17 node tests in total for the dialog).
- G1 pass: 0 errors; the incremental build printed 0 warnings (clean-rebuild baseline stays 58). G2/G3 pass: 1818 passed (1812 + 6), 35 skipped, 0 failed. G4 pass: node 386 / 387 (known Speaking failure only).
- G5 pass: legacy routes untouched; the set list keeps its groups, filters and query behaviour (all 54 existing personal-set integration tests pass unedited). G6 pass: no literal colour, `:root` or `!important` in the added CSS; weights use tokens. G7 pass: `Index.cshtml`, `_SetTile.cshtml`, `_FolderTile.cshtml`, `personal-sets.css`, `layout.js` (dialog block), tests, plan.
- G8/G10/G11 (headless Chrome, 28 checks on `/Sets` as a Standard learner, plus the Admin empty state): 1440 shows four columns, 1366 three, 1024 and 768 two, 390 and 320 one, 200% zoom two; no horizontal scroll, tiles at least 88 px high, no overflow with a 90-character folder name; console and network clean at every size; set tiles show the folder chip; the header link, the dashed tile and `/Sets#tao-thu-muc` all open the dialog and `Esc` drops the hash; Tab reaches a folder tile with a visible ring and Enter opens its page; filters behave as specified; the empty state keeps "Thêm bộ thẻ mẫu" and "Tạo bộ thẻ" and adds "Tạo thư mục". The F3 browser checks (23) were repeated after the `layout.js` change and pass. The throwaway folder created for the long-name check was deleted.
- G9 mockup: matches `1-bo-the-co-thu-muc.png` (folder row with icon tile, name, "n bộ thẻ · m từ", chevron, dashed tile; folder chip on set tiles; "Tạo thư mục" beside "Tạo bộ thẻ"). Differences: the saved-folder chip and the owner name are shown as specified but were not part of this learner's data in the browser run (they are covered by integration tests; saved state is seen in F5); the header "Tạo thư mục" uses the shared outline-primary style of `_PageHeader` rather than the mockup's white pillow.
- G12 pass: saved chip 6.84, folder chip 6.84, muted meta 5.87, dashed tile text 9.31 and on hover 6.91, folder name 17.10, chevron 5.87 (UI part). G13 pass: Practice and set pages untouched; the other pages that include the layout still pass their tests. G14 pass. G15 pass: no `innerHTML`; no data other than the viewer's own and saved folders is listed.
- Not observed: a saved folder tile in the browser (data for it exists only in the tests); Safari and Firefox; real keyboard focus ring colours in forced-colours mode (screenshot taken, not inspected closely).


### F5 — Folder page (2026-11-16) — `feat(sets): add the folder page with rename, delete, create-in and move in and out`
- Work: `Views/Set/Folder.cshtml` (breadcrumb, header card with icon, name, facts and actions, the set grid with a "Bỏ khỏi thư mục" form under each own set, a dashed "Tạo bộ thẻ trong thư mục" tile, a `<details>` panel "Thêm bộ thẻ có sẵn", the empty state with the inline "Chuyển bộ thẻ có sẵn vào thư mục" list, the rename and delete dialogs), a small partial `_FolderMoveRows.cshtml` (not in plan 2.1, added to avoid duplicating the rows; a Razor local function cannot hold tag helpers), additive CSS in `personal-sets.css`, two small additive helpers in `personal-sets-common.js` (autofocus a modal field; reopen a modal marked `data-ts-open-on-load`), and `SampleTerm` and `CoverHue` on the movable-set row model.
- Dialogs use the module's existing Bootstrap-modal pattern (the same one as "Xóa bộ thẻ"): "Đổi tên thư mục" (field, `maxlength` 100, "Hủy" and "Lưu tên") and "Xóa thư mục “{tên}”?" with "Các bộ thẻ bên trong không bị xóa, chúng chỉ ra khỏi thư mục." and "Hủy" / "Xóa thư mục". A failed rename redirects back and the dialog reopens with the message and the typed text (the top alert is suppressed so the message is not shown twice). A folder with child folders shows a note with a link to the legacy page instead of the delete button.
- Roles: the owner (and an admin) gets every action; a saved follower gets "Đã lưu", the owner's name and only "Bỏ lưu"; a class member sees the sets and nothing editable; a stranger gets 404.
- Omitted as the plan allows: "Luyện x từ đến hạn" on the folder page, because `/Practice` takes one `set` or a word list and has no folder scope (F0 item 4).
- Tests first: 8 integration tests were run red (6 of 8 failed against the minimal view from F2; the class-member read-only guard and the success-message test already held), then green; 3 node tests for the modal helpers (20 in `test_folder_dialog.cjs`).
- G1 pass: 0 errors, 58 warnings (clean-rebuild baseline). G2/G3 pass: 1826 passed (1818 + 8), 35 skipped, 0 failed. G4 pass: node 389 / 390 (known Speaking failure only).
- G5 pass: legacy `FolderDetail`, `SaveFolder`, `UnsaveFolder`, rename and delete routes untouched and their tests green; `/Home/FolderDetail/1` still opens in the browser. G6 pass: no literal colour, `:root` or `!important` in the new CSS and views. G7 pass: `Folder.cshtml`, `_FolderMoveRows.cshtml`, `personal-sets.css`, `personal-sets-common.js`, the service and view models (additive), tests, plan.
- G8/G10/G11 (headless Chrome, Standard learner plus the Premium account for a saved folder, 33 checks): empty folder shows the name, "Chưa có bộ thẻ nào", the empty state, four rows to move in and the three owner actions; "Chuyển vào" lands back on the page with the message, one tile, a remove button and the facts; the tile on `/Sets` carries the new folder name; the panel is closed at first and opens with Enter; "Bỏ khỏi thư mục" empties the folder again; Enter on "Đổi tên" opens the dialog with the name focused and selected, `Esc` closes it and focus returns to the button; a duplicate name reopens the dialog with "Bạn đã có thư mục tên này." and the typed text; a valid name shows "Đã đổi tên thư mục." and the new title; the delete dialog names the folder and says the sets are kept, "Hủy" keeps the folder, confirming returns to `/Sets` with the message, the folder is gone and all four sets are still listed; a stranger gets 404; a saved folder shows "Đã lưu", "Thư mục của Local Premium" and only "Bỏ lưu", appears in the `/Sets` row (on "Tất cả" and "Được chia sẻ", not on "Của tôi") and "Bỏ lưu" returns to `/Sets` with its message. At 1440, 1366, 1024, 768, 390, 320 and 200% zoom there is no horizontal scroll, actions are 40 px high, one column below 768 and two above, and console and network are clean. Throwaway folders were deleted through the app after each run (a first run that stopped on a typo in my script left one folder, found and deleted before the final run).
- One check in my script failed once (the saved folder in the `/Sets` row) because it searched the raw HTML, where Razor writes Vietnamese letters as numeric entities; the DOM check in a separate run passed.
- G9 mockups: matches `3-trong-thu-muc.png` (breadcrumb, header card with the three actions, "Bộ thẻ trong thư mục", dashed create tile) and `4-thu-muc-trong.png` (empty state, create button, "Chuyển bộ thẻ có sẵn vào thư mục" rows with "Chuyển vào"). Differences: the success message is the module's alert at the top, not the mockup's dark toast (plan 4.5: use the existing alert pattern); "Luyện x từ đến hạn" omitted (above); the move-row cover shows the first three letters of the first word (the mockup shows short words) and the first browser run showed long words clipped, fixed before this commit; the empty-state card is narrower than the mockup's.
- G12 pass: move meta 5.61, move name 16.35, panel summary 7.65, facts 5.87, child-folder note 6.40, rename error 6.46. G13 pass: sets, practice and class pages unaffected; the legacy folder page opens. G14 pass (tokens only). G15 pass: every POST needs the token (tests), a stranger and a follower cannot rename, delete or move (tests), `returnUrl` stays local; no `innerHTML` in the new JS.
- Not observed: a class member's page in the browser (covered by an integration test); Safari and Firefox; the third-letter cut of the move-row cover after the final CSS-free view edit was verified by build and tests, not by a screenshot.


### F6 — Create a set inside a folder (2026-11-16) — `feat(sets): create a set from a folder and link set breadcrumbs to the folder page`
- Work: `Views/Set/CreateSet.cshtml` shows the preselected folder in the breadcrumb (only a folder the form lists, i.e. one of the owner's) and `Views/Set/Detail.cshtml` links the folder crumb to `/Sets/Folders/{id}` instead of the legacy page (still owner-only). No controller or service change: `GET /Home/CreateSet?folderId=` already preselects a manageable folder and ignores a foreign or unknown one; `POST /Sets/Create` already stores the set in the folder and goes to `/Sets/{id}/Add`.
- Tests first: 6 integration tests (preselect and crumb; three ignore cases; POST stores in the folder and the folder page lists it; detail breadcrumb for owner vs member). 2 were red before the view edits, all green after.
- G1 pass: 0 errors. G2/G3: 1831 passed, 35 skipped, 1 failed in the full run: `SmtpTransportFakeTests.DeliveryDeadline_CancelsStalledTransport_AndReturnsFailure` (timing-sensitive, unrelated to folders); it passed in two separate reruns of its class (6/6). G4: node 389 / 390 (known Speaking failure only). `git diff --check` clean.
- Browser (headless Chrome, Standard learner, 7 of 8 checks passed): the folder page "Tạo bộ thẻ trong thư mục" opens `/Home/CreateSet?folderId=…` with the folder selected and crumbs `Bộ thẻ › {folder} › Tạo bộ thẻ`; saving lands on `/Sets/{id}/Add`; the new set is listed on the folder page; the set detail crumb links to the folder page; no horizontal scroll at 390 and 768; console and network clean. The 8th check (a "foreign" id) used folder 1, which is in fact the learner's own folder, so the script's expectation was wrong, not the product; a foreign id is covered by the integration test (202 and 987654). Throwaway set and folder deleted through the app.
- Not observed: a foreign folder id in the browser.


### F7 — Docs (2026-11-16) — `docs(sets): record the folder work in the tracker, known issues and bug log`
- Work: `docs/design-system/team-ui-workflow.md` (tracker rows "Bộ thẻ cá nhân" and "Lớp học, thư mục"), `.ai/context/known-issues.md` (TD-017 no folder scope in Practice; TD-018 the two shared-file edits for Châu, the unused `FolderController.BuildFolderPageViewModelAsync` and the legacy `Views/Folder/*` pages), `.ai/context/bug-fix-log.md` (one entry), `docs/project-structure.md` (controller ownership and the new routes, checked against the attributes in `SetController.Personal.cs`).
- G pass: documentation only, no code or test change; `git diff --check` clean. No tooling file was changed, so the agent-tooling checks are not required.


### F8 — Full gate and end to end (2026-11-16)
- Gate: no code changed after the F6 gate (F7 is documentation only). Last full run: .NET 1831 passed, 35 skipped, 1 failed — `SmtpTransportFakeTests.DeliveryDeadline_CancelsStalledTransport_AndReturnsFailure`, a timing-sensitive test that passed in two separate reruns (6/6); node 389 / 390 (known Speaking failure only); warnings 58 at the F5 clean rebuild (baseline) and the F6 change touched two views only.
- Pending EF model changes (repo-local tool, `dotnet tool run dotnet-ef migrations has-pending-model-changes`): "No changes have been made to the model since the last migration." No migration, no `Models/**` change, no schema or data-package change in this branch.
- File scope of the whole branch (`git diff --name-status master..HEAD`, 29 paths): no `Program.cs`, appsettings, `.csproj`, migrations, `Models/**`, `Views/Folder|Class|Home/**`, shared CSS or `_PageHeader`. The only shared files are `Views/Shared/_Layout.cshtml` (40 lines changed) and `wwwroot/js/layout.js` (109 lines changed), limited to the blocks listed in plan 2.2, noted in TD-018 for Châu. `Views/Shared/_CreateFolderDialog.cshtml` and `Views/Set/_FolderMoveRows.cshtml` are new partials (the first is in the plan, the second is an addition recorded under F5).
- End to end (headless Chrome, Standard synthetic learner, 9 of 9): the sidebar "Tạo thư mục mới" opens the dialog with the name field focused → creating lands on the new folder page → "Tạo bộ thẻ trong thư mục" creates a set inside it → `/Sets` shows the folder tile and the chip on the new set → another set is moved in (2 bộ thẻ · 9 từ) → rename → delete → both sets are still listed without a chip; `/Sets` at 390 has no horizontal scroll; console and network clean. The throwaway set and folder were removed through the app.
- Data honesty: during the F5 browser run the learner's sample set "Công nghệ hằng ngày" (id 10, originally in "Từ vựng mẫu") was moved into a throwaway folder and the folder was deleted, which left it without a folder in the local dev DB (synthetic account, not the owner's). It was found at the start of the F8 run and moved back to "Từ vựng mẫu" through the app; the final state of the Standard account is the original one (three sets in "Từ vựng mẫu", one in "Local practice"). The database was not reset or reseeded.
- After-screenshots (ignored): `.tmp/sets-folders/after/` (dialog, new folder, `/Sets` at 1440 and 390, after delete); per-phase shots in `.tmp/sets-folders/f3|f4|f5|f6`.
- Not observed: Safari and Firefox; a real class member's folder view in the browser (integration-tested); the shared-file blocks reviewed by Châu; production or staging (nothing pushed, no PR, no CI, no deploy).
- Definition of Done (plan 10): all eight boxes met; the "existing tests unedited" box holds except the listed folder test files, which are this branch's own.
