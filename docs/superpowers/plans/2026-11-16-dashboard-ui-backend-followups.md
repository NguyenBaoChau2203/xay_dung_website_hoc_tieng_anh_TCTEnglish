# Dashboard UI → Backend Review & Follow-up Handoff

**Date:** 2026-11-16  
**Document status:** REVIEW_ONLY / ANALYSIS_ONLY — awaiting owner authorization before any backend edit.  
**Source context:** UI changes from `Index.cshtml`, `_DailyChallenge.cshtml`, `Dashboard.css` traced against `HomeController.cs`, `StudyController.cs`, `ReadingController.cs`, `GoalsService.cs`, and `DbflashcardContext`.

---

## A. UI changes shipped (inventory)

| UI block | User action | Target route / API | Data shown |
|---|---|---|---|
| **Header Greeting** | View only | None | `FullName` (fallback `User.Identity.Name`), `Role` badge ("Admin", "Premium", "Standard") |
| **Command Strip (Study Next)** | Click primary CTA | If `TodayFolders.Any()`: `Folder/FolderDetail/{id}`<br>If empty: `Folder/Index` | Folder name, `SetCount` bộ từ, `CreatorName`, `Goal` từ/ngày (if `> 0`) |
| **Streak Card** | View only | None | `Streak` (số ngày liên tiếp), week dots (T2–CN) tính từ ngày hiện tại |
| **Stats Strip & Tiến độ từ vựng** | View only | None | `CardCount` (Thẻ từ), `SetCount` (Bộ từ), `FolderCount` (Folder), Thanh tỷ lệ & số lượng: `MasteredCount` (Đã thuộc), `LearningCount` (Đang học), `NewCount` (Mới) |
| **Weekly Time Chart** | Hover bar tooltip | None (Pure CSS chart) | `FeatureTimeBreakdown` (7 ngày T2–CN × 7 tính năng: Flashcard, Quiz, Speaking, Reading, Listening, Writing, Grammar), tổng phút mỗi ngày |
| **Truy cập nhanh (Quick Access)** | Click feature link | `/Vocabulary/Index`<br>`/Speaking/Index`<br>`/Home/Reading`<br>`/Home/Listening`<br>`/Home/Writing`<br>`/Vocabulary/Index` | 6 biểu tượng và nhãn tính năng tĩnh |
| **Daily Challenge** | Click chọn đáp án | `POST /Home/CheckAnswer` (AJAX kèm CSRF)<br>`GET /Home/RefreshDailyChallenge` (AJAX) | `Term`, 4 `Options` (`CardId`, `Definition`), token `ChallengeToken`. Hiệu ứng correct/wrong, tự refresh sau 1200ms |
| **Đang đọc dở (Recent Reading)** | Click "Tiếp tục đọc" | `asp-controller="Study" asp-action="Reading" asp-route-id="{Id}"` | `Title`, `Level`, `Topic` từ `RecentInProcessReading` (chỉ hiện khi có bài đọc dở) |
| **Lịch sử ôn tập (Heatmap)** | Hover cell xem tooltip | None (CSS grid 7 cols) | 28 ô từ `HeatmapCells` (`DayLabel`, `WeekLabel`, `ReviewedCards`), mức độ màu 0–3 |

---

## B. Trace findings

| Block | Current backend evidence | Status | Risk if left unchanged |
|---|---|---|---|
| **Header Greeting** | `HomeController.cs:127-128`<br>`user.FullName`, `user.Role` | **OK** | Không có rủi ro; backend cung cấp đúng và đủ. |
| **Command Strip (TodayFolders)** | `HomeController.cs:410-459`<br>`GetTodayFoldersAsync(userId)` lọc `f.UserId != userId` | **BACKEND GAP** | Query chỉ lấy folder của **người dùng khác** (khám phá cộng đồng), không bao giờ lấy folder của chính learner. Nếu learner muốn học tiếp folder của chính mình, khối này không phản ánh điều đó. Nếu DB local chỉ có 1 user, luôn rơi vào trạng thái rỗng. |
| **Streak & Goal** | `HomeController.cs:129-130`<br>`user.Streak`, `user.Goal` | **OK** | Hiển thị trung thực số ngày streak và mục tiêu hàng ngày đã cài đặt; không bịa tiến độ trong ngày. |
| **Stats Strip & Progress** | `HomeController.cs:81-102`<br>Đếm thẻ theo `ownedCardIds` và `LearningProgresses` | **OK** | Dữ liệu chính xác 100% theo các thẻ user sở hữu. |
| **Weekly Time Chart** | `HomeController.cs:187-251`<br>`BuildFeatureTimeBreakdownAsync` | **OK** | Khớp 7 ngày trong tuần từ T2 đến CN, tổng hợp đủ 7 cột tính năng từ `UserFeatureTimeLogs`. Biểu đồ CSS thuần hiển thị chuẩn xác không cần ApexCharts CDN. |
| **Quick Access** | Hardcoded action links trong View | **OK** | Mọi route đều tồn tại và dẫn đúng feature controller tương ứng. |
| **Daily Challenge (Security & Logic)** | `HomeController.cs:253-303`<br>`CheckAnswer` + `GetRandomChallengeAsync` | **BACKEND GAP** | 1) Answering đúng cộng 10 XP vào `GoalsService` và refresh vô tận; không có cờ "Đã hoàn thành hôm nay" khiến đây là bài tập vô hạn chứ không phải "Daily".<br>2) Nếu database thiếu card của `Role == "System"`, challenge trả về rỗng. |
| **Đang đọc dở (Continue Reading)** | `ReadingController.cs:44`<br>`[HttpGet("Reading/Study/{id}")]`<br>vs `StudyController.cs:221`<br>`[HttpGet("Reading")]` | **BACKEND BUG (Route mismatch)** | Trong `Index.cshtml`, link trỏ tới `asp-controller="Study" asp-action="Reading" asp-route-id="@r.Id"`. Route `Study/Reading` chỉ trả về danh sách thư viện (không nhận `id`). Route thực tế để học bài đọc dở là `ReadingController.Study(int id)` (`/Reading/Study/{id}`). Nhấp vào nút sẽ bị chuyển về trang danh sách bài đọc thay vì vào thẳng bài đang đọc dở! |
| **Lịch sử ôn tập (Heatmap)** | `HomeController.cs:145-185`<br>`BuildDashboardHeatmapAsync` | **OK (Minor alignment gap)** | Backend tính 28 ngày lùi từ ngày hiện tại (`today.AddDays(-27)`). Do không căn theo Thứ 2 đầu tuần, cột đầu tiên trong grid 7 cột có thể rơi vào Thứ 3, Thứ 4... thay vì cố định Thứ 2. Dữ liệu số thẻ review vẫn chính xác. |

---

## C. Backend request list (prioritized)

### Request 1: Sửa route điều hướng bài đọc dở (`RecentInProcessReading`)
* **Priority:** Cao nhất (Trực tiếp gây bug điều hướng người dùng).
* **Why:** Nút "Tiếp tục đọc" trên dashboard hiện đang nhắm tới `StudyController.Reading` (trang thư viện bài đọc) thay vì `ReadingController.Study(int id)` (màn hình đọc và làm bài).
* **Suggested minimal change:**
  * Có 2 cách xử lý:
    1. *(Khuyến nghị - Sửa UI route)*: Đổi trong `Index.cshtml`: `asp-controller="Reading" asp-action="Study" asp-route-id="@r.Id"`.
    2. *(Backend route alias)*: Trong `StudyController.cs`, thêm route overload `[HttpGet("Reading/{id:int}")]` redirect sang `ReadingController.Study(id)`.
* **Data that must NOT be invented:** Không bịa thêm tiến độ % (ví dụ: "đã đọc 65%") vì backend `UserReadingHistory` chỉ lưu `IsCompleted` (bool) và `ViewedAt`.
* **Acceptance check:** Người dùng có bài đọc dở bấm "Tiếp tục đọc" chuyển hướng đúng đến URL `/Reading/Study/{id}` và tải bài đọc.
* **Status:** Chờ Owner phê duyệt.

---

### Request 2: Làm rõ và mở rộng logic khối học chính ("Study Next" vs "Community Discovery")
* **Priority:** Trung bình - Cao.
* **Why:** Khối nổi bật nhất trên Dashboard hiện chỉ lấy `TodayFolders` từ người dùng khác (`f.UserId != userId`). Người học quay lại dashboard muốn học tiếp các folder/bộ từ của **chính mình** trước, hoặc ôn tập các từ đến hạn SRS, chứ không chỉ khám phá folder của người khác.
* **Suggested minimal change:**
  * Trong `HomeController.BuildDashboardViewModelAsync`:
    * Bổ sung logic ưu tiên: Nếu người dùng có folder/set đang học gần nhất (hoặc có thẻ cần review), hiển thị folder/set của người dùng (với nút "Tiếp tục học" trỏ đến `Study/Study/{setId}` hoặc `Folder/FolderDetail/{folderId}`).
    * Nếu người dùng chưa có nội dung nào, fallback sang lấy folder từ cộng đồng như hiện tại.
* **Data that must NOT be invented:** Không tự tạo số liệu "thẻ cần ôn hôm nay" trừ khi có query đếm từ `LearningProgresses` với điều kiện NextReviewDate.
* **Acceptance check:** Khi user có folder/set riêng, dashboard ưu tiên hiển thị nội dung của chính họ; khi là tài khoản mới, hiển thị gợi ý cộng đồng.
* **Status:** Chờ Owner phê duyệt.

---

### Request 3: Chuẩn hóa quy tắc Daily Challenge (Daily vs Endless Flashcard)
* **Priority:** Trung bình.
* **Why:** Hiện tại người dùng có thể trả lời challenge liên tục không giới hạn, mỗi câu đúng được cộng 10 XP vào `GoalsService`. Tên gọi là "Daily Challenge" nhưng bản chất là "Quick Practice".
* **Suggested minimal change:**
  * Nếu giữ là **Daily Challenge**:
    * Trong `UserDailyActivity` hoặc `User`, kiểm tra xem hôm nay user đã hoàn thành Daily Challenge chưa (`TodayDailyChallengeCompleted`).
    * Nếu đã hoàn thành: UI hiển thị trạng thái "Bạn đã hoàn thành thử thách hôm nay! 🎉 (+10 XP)" kèm thời gian hồi hoặc nút "Luyện tập thêm".
  * Nếu giữ là **Quick Practice**:
    * Giữ nguyên logic backend, chỉ điều chỉnh nhãn UI thành "Thử thách nhanh" / "Quick Quiz" để trung thực với hành vi vô hạn.
* **Data that must NOT be invented:** Không thêm countdown giờ giả nếu backend không khóa theo ngày.
* **Acceptance check:** Quy tắc tính điểm XP và tần suất challenge thống nhất giữa nhãn UI và xử lý backend.
* **Status:** Chờ Owner phê duyệt.

---

### Request 4: Căn chỉnh 28 ngày của Heatmap theo tuần cố định (T2 → CN)
* **Priority:** Thấp (Cải thiện hiển thị).
* **Why:** CSS Grid trên Dashboard chia 7 cột đại diện cho 7 ngày trong tuần. Backend `BuildDashboardHeatmapAsync` hiện lấy `today.AddDays(-27)` đến `today`, khiến ngày bắt đầu có thể là bất kỳ thứ nào trong tuần, làm lệch trục cột thứ.
* **Suggested minimal change:**
  * Tính `startDate` là Thứ 2 của 3 tuần trước:
    ```csharp
    int daysSinceMonday = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
    if (daysSinceMonday < 0) daysSinceMonday += 7;
    var startDate = today.AddDays(-daysSinceMonday - 21); // Thứ 2 của 3 tuần trước
    ```
  * Lấy từ `startDate` đến `startDate.AddDays(27)` (hoặc ngày hiện tại) để 7 cột luôn khớp đúng từ T2 đến CN.
* **Acceptance check:** Cột 1 luôn là T2, cột 7 luôn là CN trong lưới heatmap.
* **Status:** Chờ Owner phê duyệt.

---

### Request 5: Tùy chọn hiển thị tiến độ Goal trong ngày (CardsReviewedToday / Goal)
* **Priority:** Thấp (Feature enhancement).
* **Why:** UI hiện chỉ hiển thị mục tiêu tĩnh: "Mục tiêu: 20 từ/ngày". Người học thường muốn biết hôm nay mình đã đạt được bao nhiêu (ví dụ: "Đã ôn 8 / 20 từ").
* **Suggested minimal change:**
  * Trong `BuildDashboardViewModelAsync`, thêm trường `CardsReviewedToday` vào `DashboardViewModel` lấy từ `UserDailyActivity` của ngày hôm nay.
  * Hiển thị dạng "Hôm nay: 8/20 từ".
* **Data that must NOT be invented:** Không tự tính bằng công thức giả trong Razor.
* **Acceptance check:** Chỉ số hiển thị khớp với `CardsReviewed` thực tế trong ngày của bảng `UserDailyActivities`.
* **Status:** Chờ Owner phê duyệt.

---

## D. Explicit non-requests (Tuyệt đối KHÔNG đưa vào backend)

Các thông số và logic giả mạo sau đây đã được **xóa bỏ triệt để** khỏi giao diện Dashboard. Backend **KHÔNG** được tự ý bổ sung hoặc tái tạo lại các trường này:

1. **Công thức XP giả (`CardCount * 5 + Streak * 20`)**:
   * Tuyệt đối không lưu công thức này vào DB hoặc ViewModel. XP thực tế của hệ thống được quản lý bởi `GoalsService` qua `UserDailyActivities` (XpEarned, StreakXpAwarded).
2. **Cấp độ người dùng giả mạo ("Lv Explorer")**:
   * Hệ thống chưa có tính năng chia cấp độ người dùng (Level 1, Level 2... hay Explorer/Master). Không thêm field level giả vào ViewModel.
3. **Thanh tiến độ cấp độ giả ("N XP đến level tiếp theo")**:
   * Đã loại bỏ thanh tiến độ cấp độ bịa đặt.
4. **Phần trăm hoàn thành bài đọc giả ("Đã đọc 65%")**:
   * `UserReadingHistory` chỉ lưu cờ `IsCompleted` (true/false). Không sinh ra tỷ lệ phần trăm đọc bài khi chưa có tính năng tracking vị trí đọc.
5. **Countdown giờ kết thúc Daily Challenge giả ("Còn 14 giờ")**:
   * Backend dùng token 15 phút chống replay, không có phiên challenge khóa 24h.

---

## E. Tóm tắt & Trạng thái phân định

* **Có thể deploy UI-only ngay:**
  * Toàn bộ Dashboard mới với Command Strip, Streak, Stats Strip, Biểu đồ học tập CSS thuần, Daily Challenge, Heatmap và Token styling đã hoàn toàn độc lập, sạch sẽ và an toàn.
* **Cần quyết định backend:**
  1. Fix điều hướng `RecentReading` (`ReadingController.Study(id)`).
  2. Quyết định hướng phát triển cho `TodayFolders` (ưu tiên nội dung của chính user vs khám phá cộng đồng).
  3. Quyết định luật chơi của `DailyChallenge` (giới hạn 1 lần/ngày kèm XP vs bài tập quiz nhanh không giới hạn).
* **Trạng thái thực thi:**
  * Dừng tại đây. Không chỉnh sửa code backend nào cho đến khi Owner cấp lệnh `IMPLEMENT_BACKEND`.
