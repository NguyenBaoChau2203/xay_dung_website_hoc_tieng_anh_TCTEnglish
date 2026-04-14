# Kế hoạch Nâng cấp Trang Hồ Sơ Người Dùng & Cập nhật Giao diện

> **Branch**: `feature/user-profile-enhancement`
> **Base**: `feature/ai-chatbot` (đã stash changes trước khi tạo)
> **Ngày tạo**: 2026-04-14

## 1. Scope of Work

| # | Hạng mục | Trạng thái |
|---|----------|-----------|
| 1 | Merge nhánh `feature/goals-page` vào nhánh hiện tại, resolve tất cả conflict | `[ ]` |
| 2 | Xóa link "Chủ đề đã học" khỏi User Menu trong `_Layout.cshtml` | `[ ]` |
| 3 | Fix bug Streak hiển thị sai trên trang Profile (root cause: UTC vs UTC+7) | `[ ]` |
| 4 | Xóa block "Từ đã lưu" (SavedWordsCount) khỏi trang Profile | `[ ]` |
| 5 | Thêm khu vực "Trưng bày danh hiệu" (Badges) vào trang Profile | `[ ]` |
| 6 | Final Review: build, visual test, code-review checklist | `[ ]` |

## 2. Areas & Files Affected

| Area | Files |
|------|-------|
| **Git/Merge** | Merge `origin/feature/goals-page` → resolve conflicts |
| **Layout** | `Views/Shared/_Layout.cshtml` |
| **Backend Profile** | `Controllers/AccountController.cs`, `ViewModels/UpdateProfileViewModel.cs` |
| **Frontend Profile** | `Views/Account/Profile.cshtml`, `wwwroot/css/account.css` |
| **Goals Service** | `Services/IGoalsService.cs`, `Services/GoalsService.cs` (read-only, đã có từ merge) |
| **Streak** | `Services/StreakService.cs`, `Services/BusinessDateHelper.cs` (đã fix từ merge) |

## 3. Conflict Files (từ test merge thực tế)

Khi chạy `git merge origin/feature/goals-page`, các file sau có CONFLICT:

| File | Loại conflict |
|------|--------------|
| `TCTEnglish/Models/DbflashcardContext.cs` | content conflict |
| `TCTEnglish/Models/User.cs` | content conflict |
| `TCTEnglish/Views/Shared/_Layout.cshtml` | content conflict |
| `TCTEnglish/Controllers/HomeController.cs` | content conflict |
| `TCTEnglish.Tests/Infrastructure/NoOpMigrator.cs` | add/add conflict |
| `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` | content conflict |

## 4. Root Cause Analysis — Streak Bug

**Triệu chứng**: Trang Profile hiện thị Streak = 1 hoặc 0 dù user đã học nhiều ngày.

**Root cause**: `StreakService` trên nhánh hiện tại dùng `DateTime.UtcNow.Date` (UTC+0). User Việt Nam ở UTC+7 học lúc 22h tối (= 15h UTC) → bị tính là ngày hôm trước → streak bị reset về 1.

**Fix**: Nhánh `feature/goals-page` đã sửa `StreakService` sang `BusinessDateHelper.Today` (UTC+7 / Asia/Ho_Chi_Minh). Sau khi merge Phase 1 xong, bug streak sẽ được tự động fix. Phase 2 chỉ cần **verify** kết quả.

**Profile action** (`AccountController.Profile()` dòng 375) đọc `user.Streak ?? 0` trực tiếp từ DB — giá trị này đã đúng nếu StreakService ghi đúng. Không cần sửa logic đọc.

## 5. Technical Design — Badges trên Profile

### Nguồn dữ liệu
Nhánh `goals` cung cấp sẵn:
- **Entity**: `Badge` (12 badges seed), `UserBadge` (join table User↔Badge)
- **Service**: `IGoalsService.GetGoalsAsync(userId)` → `GoalsViewModel.Badges` (type `List<GoalsBadgeViewModel>`)
- **ViewModel** `GoalsBadgeViewModel` có: `Code`, `Name`, `Description`, `IconClass`, `IsUnlocked`, `IsRecentlyUnlocked`, `ProgressValue`, `TargetValue`, `ProgressPercent`, `AwardedAt`

### Thay đổi cần làm
1. **`UpdateProfileViewModel.cs`**: Xóa `SavedWordsCount`, thêm `public List<GoalsBadgeViewModel> EarnedBadges { get; set; } = new();`
2. **`AccountController.Profile()` GET**: Inject `IGoalsService`, gọi `GetGoalsAsync(userId)`, lấy `.Badges.Where(b => b.IsUnlocked).ToList()` gán vào `model.EarnedBadges`
3. **`AccountController.UpdateProfile()` POST** (error path): Thay `SavedWordsCount = ...` bằng load badges tương tự GET
4. **`Profile.cshtml`**: Xóa block HTML `Từ đã lưu` (col-6 thứ 2). Thay bằng khu vực trưng bày badges dạng grid/carousel
5. **CSS**: Thêm styling badge cards (icon + tên + mô tả, locked/unlocked state)

---

## 6. Phases

### Phase 1: Merge nhánh Goals & Resolve Conflict

> **Nguy cơ tràn context**: CAO — cần đọc và resolve 6 file conflict lớn.
> **Khuyến nghị**: Làm riêng biệt trong 1 conversation. Commit ngay sau khi merge xong.

**Prompt để thực thi Phase 1:**

```
@workspace Task: Merge branch `origin/feature/goals-page` vào nhánh hiện tại `feature/user-profile-enhancement` và resolve tất cả merge conflicts. Có 6 file conflict đã biết:
- `TCTEnglish/Models/DbflashcardContext.cs` (content)
- `TCTEnglish/Models/User.cs` (content)
- `TCTEnglish/Views/Shared/_Layout.cshtml` (content)
- `TCTEnglish/Controllers/HomeController.cs` (content)
- `TCTEnglish.Tests/Infrastructure/NoOpMigrator.cs` (add/add)
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` (content)

Quy tắc resolve: Giữ lại code từ CẢ HAI nhánh (ours + theirs). Không xóa code nào trừ khi trùng lặp hoàn toàn. Sau khi resolve, chạy `dotnet build` để verify. Commit kết quả merge.

Area hint: Cross-cutting (Git merge)

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `.ai/context/coding-conventions.md`.

Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly needed for the merge.
Complete the task end-to-end, run `dotnet build` verification, and finish with a concise Vietnamese summary.
```

**Tiêu chí hoàn thành Phase 1:**
- [ ] `git merge origin/feature/goals-page` thành công (không còn conflict markers)
- [ ] `dotnet build` pass
- [ ] Commit message: `merge: integrate feature/goals-page into user-profile-enhancement`

---

### Phase 2: Dọn dẹp Layout & Verify Streak

> **Nguy cơ tràn context**: THẤP — chỉ sửa 1 file layout, verify streak logic.

**Prompt để thực thi Phase 2:**

```
@workspace Task: Thực hiện 2 việc trên nhánh `feature/user-profile-enhancement`:

1. Mở `Views/Shared/_Layout.cshtml` — Xóa hoàn toàn thẻ `<a>` chứa "Chủ đề đã học" (khoảng dòng 100-102, có class `dropdown-item` và icon `fa-layer-group`).

2. Verify Streak fix: Mở `Services/StreakService.cs` và xác nhận rằng nó đang dùng `BusinessDateHelper.Today` thay vì `DateTime.UtcNow.Date`. Nếu vẫn dùng UTC, sửa sang `BusinessDateHelper.Today`. Ghi kết quả verify vào summary.

Area hint: Layout (`Views/Shared/`), Streak (`Services/`)

Read `AGENTS.md` first and follow it strictly.
Inspect existing local changes before editing and do not overwrite unrelated work.
Preserve UTF-8 — file layout chứa Vietnamese text.
Complete the task, run `dotnet build`, and finish with a concise Vietnamese summary.
```

**Tiêu chí hoàn thành Phase 2:**
- [ ] Link "Chủ đề đã học" đã bị xóa khỏi `_Layout.cshtml`
- [ ] `StreakService.cs` dùng `BusinessDateHelper.Today` (không còn `DateTime.UtcNow.Date`)
- [ ] `dotnet build` pass

---

### Phase 3: Làm mới Profile — Xóa "Từ đã lưu", Thêm Badges

> **Nguy cơ tràn context**: TRUNG BÌNH — sửa 4 file (ViewModel, Controller, View, CSS).

**Prompt để thực thi Phase 3:**

```
@workspace Task: Nâng cấp trang Profile (`/Account/Profile`) trên nhánh `feature/user-profile-enhancement`:

**Bước 1 — ViewModel**: Mở `ViewModels/UpdateProfileViewModel.cs`:
- Xóa property `SavedWordsCount`
- Thêm: `public List<GoalsBadgeViewModel> EarnedBadges { get; set; } = new();`
- Thêm using `TCTEnglish.ViewModels` nếu cần (GoalsBadgeViewModel nằm trong namespace này)

**Bước 2 — Controller GET**: Mở `Controllers/AccountController.cs`, hàm `Profile()`:
- Inject `IGoalsService _goalsService` qua constructor (thêm vào cạnh các service hiện có)
- Trong hàm Profile(), sau khi load user, gọi: `var goalsData = await _goalsService.GetGoalsAsync(userId);`
- Gán `model.EarnedBadges = goalsData?.Badges?.Where(b => b.IsUnlocked).ToList() ?? new();`
- Xóa dòng query `savedWordsCount` và `SavedWordsCount = savedWordsCount`

**Bước 3 — Controller POST**: Trong hàm `UpdateProfile()`, khối catch error (dòng ~406-411):
- Xóa `model.SavedWordsCount = ...`
- Thay bằng: load badges tương tự GET action (gọi `_goalsService.GetGoalsAsync`)

**Bước 4 — View**: Mở `Views/Account/Profile.cshtml`:
- Xóa block HTML hiển thị "Từ đã lưu" (col-6 thứ 2, chứa icon `fa-book` và text "Từ đã lưu")
- Giữ lại block "Ngày liên tiếp" (Streak) nhưng cho nó chiếm full width hoặc đặt cạnh badges
- Thêm section "Danh hiệu đạt được" bên dưới block streak, hiển thị `Model.EarnedBadges` dạng grid cards
- Mỗi badge card hiển thị: Icon (`badge.IconClass`), Tên (`badge.Name`), Mô tả (`badge.Description`)
- Nếu `EarnedBadges` rỗng, hiển thị empty state: "Bạn chưa đạt danh hiệu nào. Hãy bắt đầu học để mở khóa!" kèm link đến Goals page
- Design badges theo style Premium: border-radius lớn, subtle shadow, icon có background gradient, hiệu ứng hover nhẹ

**Bước 5 — CSS**: Thêm styling cho badges vào `wwwroot/css/account.css`

Area hint: Account (`Controllers/AccountController.cs`, `Views/Account/`, `ViewModels/`)

Read `AGENTS.md` first and follow it strictly.
Then read `.ai/context/coding-conventions.md`.
Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O.
Do not modify `Program.cs`.
Preserve UTF-8.
Complete the task, run `dotnet build`, and finish with a concise Vietnamese summary.
```

**Tiêu chí hoàn thành Phase 3:**
- [ ] `UpdateProfileViewModel` không còn `SavedWordsCount`, có `EarnedBadges`
- [ ] `AccountController.Profile()` inject `IGoalsService` và load badges
- [ ] `AccountController.UpdateProfile()` POST error path cũng load badges thay vì `SavedWordsCount`
- [ ] `Profile.cshtml` không còn "Từ đã lưu", hiển thị badges grid
- [ ] Empty state khi chưa có badges
- [ ] `dotnet build` pass

---

### Phase 4: Final Review & Đóng Task

> **Nguy cơ tràn context**: THẤP — chỉ review và verify.

**Prompt để thực thi Phase 4:**

```
@workspace Task: Final review cho task "User Profile Enhancement" trên nhánh `feature/user-profile-enhancement`.

Thực hiện checklist sau:

**Build & Compile:**
- [ ] Chạy `dotnet build` — phải pass không lỗi

**Visual Verification (kiểm tra file, không cần chạy app):**
- [ ] `Views/Shared/_Layout.cshtml`: Không còn link "Chủ đề đã học" (fa-layer-group)
- [ ] `Views/Account/Profile.cshtml`: Không còn block "Từ đã lưu" / "SavedWordsCount"
- [ ] `Views/Account/Profile.cshtml`: Có section hiển thị badges với empty state
- [ ] `wwwroot/css/account.css`: Có styling cho badge cards

**Code Quality Checklist:**
- [ ] `AccountController.cs`: Hàm `Profile()` dùng `_goalsService.GetGoalsAsync()` và filter `IsUnlocked`
- [ ] `AccountController.cs`: Hàm `UpdateProfile()` POST error path không còn reference `SavedWordsCount`
- [ ] `AccountController.cs`: `IGoalsService` được inject qua constructor
- [ ] `UpdateProfileViewModel.cs`: Không còn `SavedWordsCount`, có `EarnedBadges` typed `List<GoalsBadgeViewModel>`
- [ ] `StreakService.cs`: Dùng `BusinessDateHelper.Today` (không phải `DateTime.UtcNow.Date`)
- [ ] Không có encoding issue với Vietnamese text (UTF-8)

**Security Checklist:**
- [ ] Profile GET: Có `[Authorize]` attribute
- [ ] Profile GET: Dùng `TryGetCurrentUserId()` từ BaseController
- [ ] UpdateProfile POST: Có `[ValidateAntiForgeryToken]`

**Responsive:**
- [ ] Profile.cshtml badges grid có media query hoặc dùng Bootstrap responsive grid

Nếu tất cả pass, ghi report tổng kết. Nếu có lỗi, liệt kê và sửa.

Area hint: Cross-cutting (Review)

Read `AGENTS.md` first and follow it strictly.
Complete the review and finish with a concise Vietnamese summary covering: result, files changed throughout all phases, checks run, and remaining risks.
```

**Tiêu chí đóng task:**
- [ ] Tất cả checklist items ở trên đều PASS
- [ ] Commit final: `feat(profile): add badges showcase, fix streak, remove saved words`
