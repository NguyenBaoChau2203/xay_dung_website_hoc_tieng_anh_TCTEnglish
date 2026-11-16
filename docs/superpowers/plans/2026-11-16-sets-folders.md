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
