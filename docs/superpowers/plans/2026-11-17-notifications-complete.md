# Plan: Notification centre — logic fixes and a redesign that fits the whole app

Save as `docs/superpowers/plans/2026-11-17-notifications-complete.md`.
Execution prompt: `.ai/prompts/claude-code-notifications-complete.md`.

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Mình đọc toàn bộ phần thông báo và thấy 20 vấn đề** (mục 3). Đáng kể nhất:
>   1. **Giờ hiển thị không thống nhất:** trang Thông báo tự tính giờ trong view (cộng cứng 7 tiếng), còn chuông thì dùng cách khác.
>   2. **"Đọc tất cả" chỉ cập nhật một nửa:** số trên menu bên trái không đổi, tab "Chưa đọc" vẫn còn danh sách, nút ở chuông bị khóa luôn.
>   3. **"Tải thêm" ở chuông có thể lặp hoặc sót thông báo** khi có thông báo mới đến giữa chừng.
>   4. **Thông báo không có liên kết bấm vào không làm gì**, admin không gắn được liên kết.
>   5. **Thông báo của admin:**
>      - gửi cả cho tài khoản đã xóa;
>      - bấm gửi hai lần thì người dùng nhận hai lần;
>      - lịch sử phải tải toàn bộ thông báo lên bộ nhớ và số người nhận bị giảm dần;
>      - không biết ai đã gửi.
>   6. **Thanh toán Premium xong không có thông báo,** vì chưa có hàm tạo thông báo cho một người.
>   7. **Chữ:** có "Streak" tiếng Anh, emoji trùng với biểu tượng.
>   8. **Giao diện:** CSS cũ với hơn 70 mã màu cứng, chưa theo Clay, bàn phím dùng chuông chưa tốt.
> - **Giao diện mới (4 mockup ở `Claude outputs/notification-mockups/`):**
>   - trang Thông báo chia theo "Hôm nay / Hôm qua / Trước đó", lọc theo loại (Học tập, Huy hiệu, Thanh toán, Hệ thống);
>   - nút "Đánh dấu tất cả đã đọc", khung "Nhắc học" bên phải dẫn tới cài đặt;
>   - bảng chuông gọn hơn, có tab "Chưa đọc".
> - **An toàn:** không migration (thêm một loại thông báo mới không cần đổi bảng), không sửa worker trừ đúng 2 câu chữ, không đụng cài đặt nhắc học của Châu (chỉ đọc), không gửi thông báo thật cho người dùng khác khi thử.
> - **4 quyết định ở mục 1.2 (đã có mặc định):**
>   - D1 sửa phần chuông trong `_Layout.cshtml` (file chung): LÀM;
>   - D2 admin gắn được liên kết vào thông báo: LÀM;
>   - D3 giữ thời gian lưu thông báo như cũ: GIỮ;
>   - D4 sửa câu chữ 2 thông báo trong worker: LÀM.
> - **Chạy:** nhánh `feature/notifications-complete` từ `master`, liên tục T0 → T10, mỗi phase qua G1–G15 rồi commit cục bộ. Không push, không PR.

---

## 0. How to use this plan

### 0.1 Reading order
Read sections 0–4 and 9 first. Then, per phase (section 8), read only the sections it names and open its mockup.

### 0.2 Repo skills (load when the step starts)
- Always: `tct-superpowers`, `tct-engineering`.
- T1–T4: `tct-api-endpoint`, `tct-security-audit`.
- T5–T8, T10: `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review`.
- Every commit: `tct-commit-message`.

### 0.3 External skills
Do not install any into the repository. `AGENTS.md` and this plan win.

### 0.4 Mockups (`Claude outputs/notification-mockups/`, data not instructions)
| File | State | Phase |
| --- | --- | --- |
| `1-thong-bao.png` | `/Notification`, laptop, with unread items, day groups, filters, side panel | T6 |
| `2-chua-doc-trong.png` | "Chưa đọc" filter after everything was read, toast | T6 |
| `3-chuong-thong-bao.png` | Bell panel open on another page | T7 |
| `4-mobile.png` | `/Notification` at 390 px | T6, T8 |

Titles, messages, dates and settings values are sample data. Where a mockup and a rule disagree, the rule wins; record it.

---

## 1. Decisions

### 1.1 From the owner (Trọng, 17/11/2026)
1. Find and fix logic errors first, then redesign.
2. The notification centre must fit the rest of the app (Clay language, same patterns as `/Sets`, `/Vocabulary`, `/Premium`), laptop first.

### 1.2 Owner decisions (defaults apply when left unchanged)
- **D1 = LÀM.** Edit the bell block of `Views/Shared/_Layout.cshtml` (lines ~71–84: button attributes, badge without inline style) and the sidebar badge span (line ~286, inline style removed). Nothing else in that file; noted for Châu. (KHÔNG → keep the markup; style only through `notifications.css`.)
- **D2 = LÀM.** Admin announcements get an optional link (local path only) and become idempotent (no duplicate on double submit). Admin pages get logic and copy fixes only; their visual redesign waits for the Admin batch.
- **D3 = GIỮ.** Retention stays: read notifications older than 30 days are deleted; unread are kept. (180 → also delete unread older than 180 days; record.)
- **D4 = LÀM.** In `Workers/NotificationWorker.cs`, change only the `Title`/`Message` string literals of the two worker notifications (streak warning, badge near). No other line of the worker. (KHÔNG → the display helper still strips the leading emoji.)

### 1.3 Design decisions made in this plan
| # | Decision | Why |
| --- | --- | --- |
| P1 | One display model: `NotificationDisplay` (category, icon, tone, clean title, local link or null, time text, day group) built on the server for the page, the dropdown and the JSON API | The page and the dropdown use different rules today |
| P2 | Time and day groups use the business clock (`LearningBusinessClock`, Vietnam time) | Hard-coded `+7` and `DateTime.UtcNow` in the view |
| P3 | Categories, not raw types, for filters: Học tập (StreakWarning, StreakRecord, GoalProgress, GoalCompleted), Huy hiệu (BadgeEarned, BadgeNear), Thanh toán (PremiumActivated), Hệ thống (AdminAnnouncement) | Learner words |
| P4 | Keyset paging for the dropdown API (`before` cursor = CreatedAt+Id); the page keeps numbered pages | No duplicates or gaps when new items arrive |
| P5 | Links are local paths only (`/…`, not `//…`, no scheme), validated when created and when displayed | Defence in depth before admins can add links |
| P6 | New `NotificationType.PremiumActivated = 8`; stored as int, so no migration; `has-pending-model-changes` must stay empty | Lets billing notify the buyer |
| P7 | Announcement batches use `EventKey = "announce:{senderId}:{batchGuid}"` | Idempotency, correct history and sender without a schema change |
| P8 | Each notification item is a real link (when it has a URL) or a static card with its own "Đánh dấu đã đọc" button; no `div role="button"` | Accessibility and clear behaviour |
| P9 | Styles in `notification-page.css` (page, scoped `.tn-page`) and `notifications.css` (bell panel), tokens only; page and panel scripts in files | No inline style/script |
| P10 | The page reads reminder settings through the existing preference service (read only) and links to `/Account/Profile#section-notifications` | Settings stay in Châu's account area |

---

## 2. Scope

### 2.1 May add
- `Services/Notifications/NotificationDisplay.cs` (pure mapping and formatting), `NotificationLinks.cs` (local-path rule)
- `Views/Notification/_NotificationItem.cshtml`, `_NotificationSidePanel.cshtml`
- `wwwroot/js/notification-core.js` (pure), `wwwroot/js/notification-page.js`
- Tests: `NotificationDisplayTests.cs`, `NotificationServicePagingTests.cs`, `NotificationPageIntegrationTests.cs`, `AdminAnnouncementTests.cs`, `scripts/tests/test_notification_core.cjs`

### 2.2 May edit
- `Controllers/NotificationController.cs`; `Services/NotificationService.cs`; `Services/INotificationService.cs` (additive)
- `Models/Notification.cs`: **only** add the enum member `PremiumActivated = 8` (and its comment)
- `ViewModels/NotificationViewModel*.cs` (additive)
- `Views/Notification/Index.cshtml`; `Views/Shared/_NotificationDropdown.cshtml`
- `wwwroot/css/notification-page.css`, `wwwroot/css/notifications.css`, `wwwroot/js/notifications.js`
- `wwwroot/css/study-kit.css`: add `.tn-page` to the kit scope lists only
- `Areas/Admin/Controllers/NotificationManagementController.cs`, `Areas/Admin/ViewModels/*Announcement*`, `Areas/Admin/Views/NotificationManagement/*.cshtml` — logic, the link field and copy only (D2)
- `Services/Billing/PaymentConfirmationService.cs`: replace the "no per-user create method" comment with the call of 4.6 (best effort, after commit)
- `Views/Shared/_Layout.cshtml`: only the blocks of D1
- `Workers/NotificationWorker.cs`: only the string literals of D4
- `scripts/tests/test_notification_behavior.cjs`: update file paths and selectors where the script moved or the markup changed; keep every behaviour assertion; record each change
- Docs: tracker row "Thông báo", `.ai/context/known-issues.md`, `bug-fix-log.md`, `project-structure.md`; this plan's section 12

### 2.3 Forbidden
- `Program.cs`, `appsettings*.json`, `.csproj`, migrations.
- `Models/**` except the enum member of 2.2.
- Notification infrastructure built by Châu, beyond D4:
  - the rest of `NotificationWorker.cs`;
  - `Services/Notifications/{LearningOutboxDispatcher, OutboundDeliveryProcessor, OutboundReminderWorker, NotificationPreferenceService, WebPushSubscriptionService, LibWebPushTransport, OutboundDeliveryOptions, LearningBusinessClock}.cs`.
- `GoalsService`, `StreakService`, account code and views (including `_NotificationPreferences.cshtml`).
- `layout.css`, `site.css`, `tct-learning-ui.css`, other shared partials.
- Other owners' tests: `NotificationReliabilityIntegrationTests`, `NotificationWorker*Tests`, `MariaDbNotificationWorkerAcceptanceTests`, Goals and account tests.
- New packages, fonts or CDNs.

### 2.4 Must stay unchanged
- Routes and JSON shape used by others:
  - `GET /Notification` (and `/Notification/Index`, used by AI guide tests);
  - `GET api/notifications/unread-count` → `{ count }`;
  - `GET api/notifications` → `{ success, data: { items, unreadCount, totalCount, currentPage, hasMore } }`; fields may be added, none removed;
  - `POST api/notifications/{id}/mark-read`;
  - `POST api/notifications/mark-all-read`.
- Antiforgery on POSTs; owner-only reads and writes (404 for another user's id).
- Idempotent generation by `EventKey`; existing rows; the worker's batching and outbox behaviour.
- Element ids used by other scripts or tests: `notificationBtn`, `notificationBadge`, `notifPanel`, `sidebarNotifBadge`. Search the repo before changing any id.

---

## 3. Findings (static review of `master` at `2bbbc48`, 2026-11-17; reproduce or disprove each at T0)

Severity: **B** wrong behaviour users notice or data quality, **C** polish and hardening.

| # | Sev | Where | Finding |
| --- | --- | --- | --- |
| F1 | B | `Views/Notification/Index.cshtml` `FormatTimeAgo` vs `NotificationService.BuildTimeAgo` | Two time rules. The view uses `DateTime.UtcNow` and `createdAt.AddHours(7)` (ignores the business clock, wrong if `CreatedAt` is not UTC), shows a date after 7 days. The service shows "n ngày/tháng trước". The page and the bell disagree for the same item. |
| F2 | B | Page mark-all script | After success: the sidebar badge keeps its number; the bell list is stale; on the "Chưa đọc" tab the items stay listed; the button text becomes "Đã đọc tất cả ✓" and stays disabled even when new notifications arrive. The button is shown when there is nothing unread (`Items.Any()`). |
| F3 | B | `notifications.js` `handleMarkAllClick` | On success the button stays disabled until reload; no visible confirmation. |
| F4 | B | `notifications.js` load more + `GetNotificationsPageAsync` (offset paging) | When a new notification arrives between "Tải thêm" clicks, the next page repeats an item; deletions by cleanup can skip items. |
| F5 | B | Page and bell items | A notification without `RelatedUrl` (every admin announcement) is a `role="button"` that does nothing visible except mark read; there is no explicit "Đánh dấu đã đọc" action, and a read item without a link is a dead button. |
| F6 | B | `CreateAdminAnnouncementAsync` | Recipients = all users not `Blocked`, including anonymised (deleted) accounts (`AnonymizedAt != null`). |
| F7 | B | Admin create | No idempotency: a double submit or a refresh after an error sends the announcement twice. All rows go in one `SaveChanges`. |
| F8 | B | Admin `History` | Loads every announcement row into memory and groups by title+message+minute; two different sends of the same text within a minute merge; recipient counts shrink as read rows are cleaned up after 30 days; sender always "Admin"; the success message counts recipients with a second query that can differ from the rows written. |
| F9 | B | `PaymentConfirmationService` (comment at line ~181) | Billing cannot notify the buyer: `INotificationService` has no per-user create method and no suitable type. |
| F10 | B | Generators (`NotificationService`, worker) | Copy mixes English and emoji: "🔥 Kỷ lục mới!", "Streak {n} ngày — kỷ lục mới của bạn!", "⚠️ Streak sắp mất!", "🔜 Gần mở khóa huy hiệu!", "Còn {n} bước nữa…". The emoji duplicates the icon tile. |
| F11 | C | `TryCreateWithEventKeyAsync` | Catches every `DbUpdateException` as a duplicate (debug log only), hiding real write failures. `RelationalUniqueConflict.IsUnique` exists. |
| F12 | C | `RelatedUrl` | Navigated with `window.location.href` without checking it is a local path; safe today only because all values are server-made. |
| F13 | C | Bell panel (`_Layout`, partial, JS) | Focus is not moved into the panel; `Esc` does not return focus to the bell; Tab can leave the open panel; `aria-haspopup="dialog"` on a non-modal panel; `Space` on an item scrolls the page; the badge has no accessible count ("Thông báo" only). |
| F14 | C | `Index.cshtml` | Inline `<script>` and inline styles; unused `csrfToken`; the `filter` query value is echoed into view data and an unknown value leaves no tab active; two antiforgery inputs on the page. |
| F15 | C | `notification-page.css`, `notifications.css`, `_NotificationDropdown.cshtml`, `_Layout.cshtml` | 38 + 35 hard-coded colours, inline `style` on the badges and the panel header icon; not the Clay language. |
| F16 | C | API `GET api/notifications` | `createdAt` is serialised without a time-zone marker; consumers cannot read it reliably. |
| F17 | C | Page empty states | The text mentions only streak, goals and badges; the unread-empty state has no next step. |
| F18 | C | `page` query beyond the last page | Clamped silently; the URL keeps the wrong number and the pager highlights a page the URL does not name. |
| F19 | C | Bell polling | `handleItemClick` awaits a count refresh before navigating (extra delay); polling restarts on each call. |
| F20 | C | `MarkAllAsReadAsync` | Returns success without the number updated; the UI cannot say "Đã đánh dấu n thông báo". |

Checked and correct: owner filter on every read/write; antiforgery on POST endpoints; unique `(UserId, EventKey)` index and duplicate guards; the useful index `(UserId, IsRead, CreatedAt)`; mark-read failures do not claim success (covered by node tests).

---

## 4. Backend specification

### 4.1 `NotificationDisplay` (pure, unit-tested)
```csharp
public enum NotificationCategory { Learning, Badge, Payment, System }
public sealed record NotificationDisplayItem(
    int Id, string Category, string Tone, string IconClass,
    string Title, string Message, string? Url, string? LinkLabel,
    bool IsRead, DateTime CreatedAtUtc, string TimeText, string DayGroup);
public static class NotificationDisplay
{
    public static NotificationCategory CategoryOf(NotificationType type);
    public static NotificationDisplayItem Build(Notification n, DateTime nowUtc, TimeZoneInfo zone);
    public static string CleanTitle(string title);   // strips leading emoji/symbols and spaces
    public static string TimeText(DateTime createdUtc, DateTime nowUtc, TimeZoneInfo zone);
    public static string DayGroup(DateTime createdUtc, DateTime nowUtc, TimeZoneInfo zone); // "today" | "yesterday" | "earlier"
}
```
- **Category → tone, default icon:**

  | Category | Tone | Default icon |
  | --- | --- | --- |
  | Learning — StreakWarning | streak | `fa-solid fa-fire-flame-curved` |
  | Learning — StreakRecord | streak | `fa-solid fa-fire` |
  | Learning — Goal* | success | `fa-solid fa-bullseye` |
  | Badge | warning | stored badge icon, else `fa-solid fa-medal` |
  | Payment | accent | `fa-solid fa-crown` |
  | System | primary | `fa-solid fa-bullhorn` |

  A stored `IconClass` is used only if it matches `^(fas|far|fa-solid|fa-regular) fa-[a-z0-9-]+$`; otherwise the default.
- **Link label by URL:**

  | URL | Label |
  | --- | --- |
  | `/Goals#badges` | "Xem huy hiệu" |
  | `/Goals` | "Xem mục tiêu" |
  | `/Billing/Order/…` | "Xem biên nhận" |
  | StreakWarning | "Học ngay" |
  | anything else | "Xem chi tiết" |

- **TimeText (Vietnam time):** < 1 min "Vừa xong"; < 60 min "n phút trước"; same day "n giờ trước"; yesterday "Hôm qua, HH:mm"; within 7 days "Thứ X, HH:mm" ("Chủ nhật" for Sunday); otherwise "dd/MM/yyyy".
- **Url:** `NotificationLinks.ToSafeLocal(raw)`: accept only strings starting with a single `/`; reject `//`, `/\`, control characters, schemes; trim to 500; else null.
- Tests: every type, unknown type, bad icon, emoji stripping (keeps Vietnamese letters and digits), every time band across midnight in Vietnam time, safe-link cases.

### 4.2 Service (`INotificationService`, additive changes)
- `GetNotificationsPageAsync(userId, page, pageSize, unreadOnly, NotificationCategory? category = null)`:
  - category filter in the query;
  - items built through `NotificationDisplay`;
  - `TimeAgo` stays filled (= `TimeText`) for compatibility.
- `GetNotificationsSliceAsync(userId, int take, bool unreadOnly, DateTime? beforeCreatedUtc, int? beforeId)` → items + `nextCursor` (keyset `(CreatedAt, Id) <`), for the bell (F4).
- `GetCategoryCountsAsync(userId)` → unread count per category and total (one grouped query), for the filter chips.
- `MarkAllAsReadAsync(userId, NotificationCategory? category = null)` → `OperationResult<int>` (updated count) (F20). Keep the old signature as an overload if other code calls it.
- `CreateForUserAsync(int userId, NotificationType type, string title, string message, string? relatedUrl, string? iconClass, string eventKey)`:
  - idempotent through `TryCreateWithEventKeyAsync`;
  - validates lengths (200 / 1000);
  - link through `NotificationLinks`.
- `TryCreateWithEventKeyAsync`: treat only `RelationalUniqueConflict.IsUnique(ex)` as a duplicate (detach, debug log). Other errors: detach, log a warning with type and user id (no message text), do not throw (callers in Goals must keep working) (F11).
- Generator copy (F10), Vietnamese, no emoji:
  - StreakRecord: "Kỷ lục mới: chuỗi {n} ngày học" / "Đây là chuỗi ngày học dài nhất của bạn.";
  - GoalCompleted: "Hoàn thành mục tiêu {Khu vực}" / "Bạn đã hoàn thành mục tiêu {khu vực} hôm nay. Giữ nhịp này nhé!";
  - GoalProgress: "Sắp đạt mục tiêu {Khu vực}" / "Bạn đã đạt {p}% mục tiêu {khu vực} hôm nay, cố thêm chút nữa!";
  - BadgeEarned: "Huy hiệu mới: {tên}" / "Bạn vừa nhận huy hiệu \"{tên}\".".
  - Event keys unchanged.
- D4 worker literals:
  - StreakWarning: "Chuỗi ngày học sắp mất" / "Bạn chưa học hôm nay, chuỗi {n} ngày sẽ mất nếu không học trước nửa đêm.";
  - BadgeNear: "Sắp nhận huy hiệu {tên}" / "Còn {n} bước nữa để mở khóa huy hiệu \"{tên}\".".

### 4.3 Controller
- `GET /Notification?filter=all|unread&category=learning|badge|payment|system&page=n`:
  - unknown values fall back to `all` / no category;
  - a page beyond the last redirects (302) to the last page with the same filters (F18).
- `GET api/notifications`: same contract plus fields per item (`category`, `tone`, `iconClass` sanitised, `title` clean, `url` safe, `linkLabel`, `timeText`, `dayGroup`, `createdAtUtc` ISO-8601 with `Z`) and an optional `before`/`beforeId` cursor returning `nextCursor` (F4, F16). Without a cursor it behaves as today.
- `POST api/notifications/mark-all-read`:
  - optional `category`;
  - returns `{ success, updated, unreadCount }`.
- `POST api/notifications/{id}/mark-read` returns `{ success, unreadCount }`.
- All JSON errors in Vietnamese; 404 for another user's id (unchanged).

### 4.4 Admin announcements (D2)
- **Recipients:** users with `Status != Blocked` and `AnonymizedAt == null` (F6). Admin accounts included.
- **Batch:**
  - the form carries a hidden `BatchId` (GUID made on GET);
  - `EventKey = "announce:{adminId}:{BatchId:N}"` (P7);
  - a resubmit with the same `BatchId` creates nothing new and shows "Thông báo này đã được gửi trước đó.";
  - rows are written in chunks of 500 inside one transaction;
  - the result returns the number written, used for the message "Đã gửi thông báo tới {n} người dùng." (F7).
- **Optional link:** field "Liên kết (không bắt buộc)", hint "Đường dẫn trong TCT English, ví dụ /Premium hoặc /Vocabulary"; validated with `NotificationLinks` (error "Chỉ dùng đường dẫn bắt đầu bằng / trong TCT English."); stored in `RelatedUrl`.
- Title and message trimmed; lengths 200 / 1000 as today.
- **History:**
  - group in the database by `EventKey` for rows with an `announce:` key (sender id parsed from the key, sender name looked up, falls back to "Quản trị viên");
  - legacy rows without a key keep the minute grouping, labelled "(dữ liệu cũ)";
  - paging in the database;
  - show sent time in Vietnam time, recipients counted now ("còn {n} bản lưu" note when cleanup reduced them), read count.
- Admin copy in correct Vietnamese; no layout redesign.

### 4.5 Retention (D3)
`CleanupOldNotificationsAsync` unchanged with D3 = GIỮ. With 180: also delete unread rows older than 180 days. The worker's call stays the same.

### 4.6 Billing link (F9)
In `PaymentConfirmationService`, after the transaction commits and only for the outcome `Activated`, call `CreateForUserAsync`:
- `type`: `PremiumActivated`;
- title "Premium đã được kích hoạt";
- message "Gói {tên gói} có hiệu lực đến {dd/MM/yyyy}.";
- url `/Billing/Order/{OrderCode}`;
- icon `fa-solid fa-crown`;
- key `premium:{OrderCode}`.

Wrapped so that any failure is logged and never affects the payment result. Test: activation creates exactly one notification; a repeated confirmation creates none; a notification failure does not roll back the payment.

---

## 5. UI specification

Common rules:
- Tokens only (no hex, `rgb()`, `:root`, `!important`, weight > 700); Clay kit `k-*` and Font Awesome.
- No inline `style` or `<script>` in the changed views.
- Focus visible; touch targets ≥ 44 px; forced-colours and reduced-motion blocks.
- Vietnamese strings below are literal.

### 5.1 `/Notification` (mockups 1, 2, 4)
- Root `<div class="tct-theme tct-page tn-page">`. Header: `h1` "Thông báo"; sub "Bạn có {n} thông báo chưa đọc." or "Bạn đã đọc hết thông báo."; button "Đánh dấu tất cả đã đọc" (`fa-check-double`) only when unread > 0 (on phones an icon button with that accessible name).
- **Filters** (`nav` with `aria-label="Lọc thông báo"`, links with `aria-current`): "Tất cả", "Chưa đọc" (+ count chip), "Học tập", "Huy hiệu", "Thanh toán", "Hệ thống". "Chưa đọc" is the `filter` axis; the categories are the `category` axis and combine with it. The chip row scrolls horizontally on phones without a scrollbar hiding focus.
- **List:** groups "Hôm nay", "Hôm qua", "Trước đó" (`h2` each, only non-empty). `_NotificationItem`:
  - tone-coloured icon tile;
  - title (`h3`, bold);
  - message;
  - meta line with `TimeText` and, if `Url`, a link `LinkLabel →`.
  - Unread: tinted surface, left accent bar, dot with visually hidden "Chưa đọc", and a small button "Đánh dấu đã đọc" (`fa-check`, accessible name with the title).
  - Clicking the link marks the item read first (best effort, max 1.5 s) then navigates; a failed mark still navigates (the read state is not claimed).
  - With no link the card is static, never a fake button.
- **Pager** (when > 1 page): previous, numbers (current ±2), next; real links; `aria-label="Phân trang thông báo"`.
- **Side panel** (≥ 1200 px right column 320 px; below, after the list):
  - card "Nhắc học" with three read-only rows from the preference service: "Nhắc học mỗi ngày" (Bật · HH:mm / Tắt), "Thông báo trên trình duyệt" (Bật / Tắt), "Email tổng kết tuần" (Bật / Tắt), plus a button "Đổi cài đặt" → `/Account/Profile#section-notifications`. If the read fails, show only the button.
  - Card "Bạn sẽ nhận thông báo khi": "Đạt hoặc gần đạt mục tiêu ngày", "Lập kỷ lục chuỗi ngày học", "Nhận hoặc sắp nhận huy hiệu", "Thanh toán Premium thành công", "Có thông báo chung từ TCT English"; note "Thông báo đã đọc được giữ 30 ngày." (adjust to D3).
- **Empty states:**
  - none at all: icon, "Chưa có thông báo nào", "Khi bạn học, đạt mục tiêu hay nhận huy hiệu, thông báo sẽ hiện ở đây.", button "Bắt đầu học" → `/Home/Index`;
  - unread empty: "Không còn thông báo chưa đọc", "Mọi thông báo đều đã được đọc. Hãy học tiếp để giữ chuỗi ngày học.", buttons "Học tiếp" and link "Xem tất cả thông báo";
  - category empty: "Chưa có thông báo nào trong mục này." + "Xem tất cả thông báo".
- **Mark all** (`notification-page.js`):
  - POST; on success clear every unread mark, update header and sidebar badges and the chip counts, hide the button;
  - on the "Chưa đọc" filter, replace the list with the unread-empty state;
  - show a toast "Đã đánh dấu {n} thông báo là đã đọc.";
  - on failure re-enable the button and toast "Chưa đánh dấu được. Vui lòng thử lại.".
  - The bell list is refreshed through a custom event `tct:notifications-changed` that `notifications.js` listens to.
- One antiforgery token on the page (the layout's dropdown token is reused if present; otherwise the page's own).

### 5.2 Bell panel (mockup 3)
- **Button** (`_Layout`, D1): `aria-haspopup="true"`, `aria-expanded`, `aria-controls="notifPanel"`, accessible name "Thông báo" or "Thông báo, {n} chưa đọc"; the badge text is `aria-hidden` (the name carries the count); the badge is hidden with the `hidden` attribute, not inline style.
- **Panel** (`_NotificationDropdown`):
  - header "Thông báo" + chip "{n} mới" + text button "Đánh dấu đã đọc";
  - tabs "Tất cả" / "Chưa đọc" (`role="tablist"`, arrow keys);
  - list of up to 8 items (icon tile, title, message clamped to 2 lines, time, dot);
  - footer link "Xem tất cả thông báo" → `/Notification`.
- **Scrolling:** "Tải thêm" is replaced by loading the next slice when the list is scrolled to its end, plus a visible "Tải thêm" button for keyboard users; keyset cursor (F4).
- **Items:**
  - with a URL: a link (mark read then navigate, as 5.1);
  - without a URL: a button that marks read and gives no navigation;
  - `Space`/`Enter` handled without page scroll.
- **States:** skeleton while loading; error "Không tải được thông báo." + "Thử lại"; empty "Không có thông báo nào" / "Không còn thông báo chưa đọc".
- **Focus:** opening moves focus to the panel heading; `Tab` cycles within the panel; `Esc` and outside click close it and return focus to the bell.
- **Mark all:** button re-enabled after success (hidden when 0 unread), list updated, badges updated, event dispatched (F3).
- **Polling:** unchanged rate (60 s, visibility-aware, backoff); navigation after mark-read no longer waits for the count (F19).
- **Phone (< 576 px):** the panel becomes a full-width sheet below the header with the same content.
- Pure logic in `notification-core.js`:
  - badge text and accessible label;
  - cursor handling and de-duplication by id;
  - safe-URL check (mirror of 4.1);
  - time-group labels for client-appended items;
  - state reducer for mark-read / mark-all.

  Node tests for each.

### 5.3 Admin (D2, copy and logic only)
Create form: add the link field and the hidden batch id; keep the current layout and classes. History: new columns "Người gửi", "Đã đọc", Vietnam time. Success and error messages per 4.4.

### 5.4 Accessibility
Landmarks; one `h1`; ordered headings; live region for toast and status; colour never the only signal (unread has dot text and button); 200% zoom and 320 px without overlap; contrast scripted.

---

## 6. Tests
- **Display:** 4.1 cases.
- **Service:**
  - category filter;
  - keyset paging (insert between slices → no duplicate, no gap);
  - counts per category;
  - mark-all returns the count and respects category;
  - `CreateForUserAsync` idempotent and link-safe;
  - unique-conflict detection with SQLite and MariaDB-style errors;
  - other DB errors do not throw.
- **Controller / integration:**
  - page renders each filter and category, unknown values fall back, beyond-last-page redirects;
  - JSON contract keeps the old fields and adds the new ones, cursor works;
  - mark-read / mark-all return counts;
  - another user's id → 404;
  - missing token → 400;
  - no inline `<script>`/`style` in the page;
  - only one `h1`;
  - no `role="button"` divs.
- **Admin:**
  - anonymised users excluded;
  - same batch id twice → one set of rows;
  - link validation;
  - history grouped by batch with sender;
  - legacy rows still listed.
- **Billing:** 4.6 cases.
- **Node:**
  - `notification-core.js`;
  - `test_notification_behavior.cjs` still covers mark-read and mark-all failure paths (paths updated, assertions intact).
- **Unchanged and green:** `NotificationReliabilityIntegrationTests`, `NotificationWorker*Tests`, Goals and AI guide tests.

---

## 7. Local verification data
- Create notifications only for the synthetic learner account through the service (a small test-only path in a test, or the admin page with a title starting "[Thử nghiệm]" sent while the local database has only synthetic users).
- Before sending an announcement locally, count recipients and **stop** if any non-synthetic account exists (never notify real people). Prefer service-level creation for the single learner.
- Remove throwaway rows afterwards by their event keys; never reset the database; record keys in section 12.

---

## 8. Phases

Run mode: continuous, autonomous, one agent. After each green gate: gate record into section 12 → commit → next phase.

| Phase | Work | Read |
| --- | --- | --- |
| **T0 Baseline** | See the list below the table. | 0–4, 7 |
| **T1 Display and links** | Tests first: `NotificationDisplay`, `NotificationLinks` | 4.1 |
| **T2 Service** | Tests first: paging, categories, counts, mark-all count, `CreateForUserAsync`, conflict handling, generator copy, enum member | 4.2 |
| **T3 Controller and API** | Tests first: page parameters, JSON additions, cursor, counts in responses | 4.3 |
| **T4 Admin and billing** | Tests first: recipients, batch idempotency, link, history; billing notification; D4 literals | 4.4–4.6 |
| **T5 UI foundation** | `notification-page.css` rewrite (tokens), `notifications.css` rewrite, kit scope, `_NotificationItem`, `_NotificationSidePanel`, `notification-core.js` + node tests | 5 intro, 5.2 core |
| **T6 Page** | 5.1 | 5.1 |
| **T7 Bell panel and layout blocks** | 5.2, D1 | 5.2 |
| **T8 Admin copy, accessibility and responsive pass** | 5.3, 5.4 | 5.3, 5.4 |
| **T9 Docs** | Tracker row, known-issues (shared-file note for Châu, worker literals), bug-fix-log, project-structure | 2.2 |
| **T10 Full gate** | Whole gate on the page, the panel (on three different pages), admin pages, billing activation; end-to-end run (8.5); after-screenshots into `.tmp/notifications/after/` | all |

**T0 Baseline, step by step:**
1. Clean-tree check (leave `appsettings.Development.json` and `Claude outputs/` unstaged).
2. `git switch master`; pull once with `scripts/pull-local.ps1` if the tree is otherwise clean; verify `2bbbc48` or later.
3. Create `feature/notifications-complete`.
4. Build; run the full and node tests (record counts, warnings, known failures).
5. Save the plan and the prompt.
6. Read D1–D4.
7. Reproduce or disprove F1–F20.
8. Discover:
   - the preference service read method;
   - every use of the bell ids;
   - the `OperationResult<T>` shape;
   - how `LearningBusinessClock` exposes the zone.
9. Start the app; take before-screenshots of `/Notification` (all, unread, empty) and the open bell at 1440 and 390 into `.tmp/notifications/before/`.

### 8.1 Phase gate
| Gate | Check |
| --- | --- |
| G1 | build; warnings ≤ T0 |
| G2 | area tests: `Notification*`, `AdminAnnouncement*`, `PaymentConfirmation*`, `Goals*`, AI guide tests that name `/Notification/Index` |
| G3 | full suite; counts not below T0 plus new tests |
| G4 | node tests; only T0 failures may remain |
| G5 | contracts of 2.4; `has-pending-model-changes` reports none after the enum member |
| G6 | tokens-only grep on both CSS files; no inline `style`/`<script>` in changed views; `git diff --check` |
| G7 | file scope; `_Layout.cshtml` diff limited to the D1 blocks; `NotificationWorker.cs` diff limited to the four literals; `Models/Notification.cs` diff = one enum member |
| G8 | render at 1440×900, 1366×768, 1024, 768, 390, 320 and 200% zoom; console and network clean |
| G9 | mockup comparison; consistency with `/Sets`, `/Vocabulary`, `/Premium` |
| G10 | behaviour checklist (8.4) by mouse and keyboard only |
| G11 | accessibility (5.4) incl. bell focus, forced colours, reduced motion |
| G12 | scripted contrast |
| G13 | regression: Goals completion and badge award still create notifications (existing tests); worker tests; the bell on `/Sets`, `/Vocabulary`, `/Premium`; logout/login |
| G14 | Clay conformance grep |
| G15 | security: owner 404, antiforgery 400, local-link rule (tests with `//evil`, `javascript:`, `https://…`), no `innerHTML` with data in new JS, no real users notified, no credentials or `.tmp/` in the diff |

### 8.2 Failure and stop rules
- A failed item: fix and rerun the whole gate.
- At most 3 attempts per item; then mark it "deferred" (caused by the phase, reverted) or "blocked" (out of scope, with evidence).
- **Stop the run** if green requires:
  - a forbidden file;
  - a migration (`has-pending-model-changes` not empty);
  - notifying a non-synthetic account;
  - a wider change to the layout or worker than allowed.
- Never skip, weaken or delete a test; never call a skipped check passed.

### 8.3 Gate record (append to section 12 before each commit)
- Phase, date, commit subject.
- G1–G16 each with pass / n-a / deferred / blocked and one line of evidence.
- Decisions taken.
- Differences from the mockup.
- Tests changed and why.
- Event keys of rows created.

### 8.4 Behaviour checklists
- **T1–T4:**
  - every time band and day group in Vietnam time;
  - categories filter correctly;
  - a slice after a new insert has no duplicate;
  - mark-all returns n;
  - an announcement sent twice with one batch id lands once and skips anonymised users;
  - links outside the site are rejected;
  - paying (mock flow) creates one "Premium đã được kích hoạt" notification linking to the order;
  - worker texts read in Vietnamese without emoji.
- **T6:**
  - day groups;
  - each filter and category with counts;
  - unread items show dot and button;
  - "Đánh dấu đã đọc" per item updates badges;
  - link items mark read then navigate;
  - static items have no fake button;
  - mark-all updates everything, shows the toast, empties the unread tab;
  - empty states;
  - pager and beyond-last redirect;
  - side panel values match the account settings and the button opens the right section;
  - works without JavaScript (links and pager; mark actions need JS and are hidden without it).
- **T7:**
  - bell name includes the count;
  - focus moves in and returns on `Esc`/outside click;
  - `Tab` stays inside while open;
  - tabs by arrow keys;
  - scroll and button load more without duplicates;
  - error and retry;
  - mark-all re-enables properly;
  - the page and the panel stay in sync through the event;
  - phone sheet at 390.
- **T8:** admin form link field and messages; history columns; 200% zoom.

### 8.5 End-to-end run (T10)
With the synthetic learner from `.appdata/local/access.json` (never print passwords):
1. Complete a vocabulary lesson that reaches the daily goal → a "Hoàn thành mục tiêu Từ vựng" notification appears in the bell within one poll.
2. Run the MoMo mock payment → "Premium đã được kích hoạt" with "Xem biên nhận".
3. Open `/Notification`, filter "Thanh toán", open the receipt link → item becomes read.
4. Mark all read → badges clear everywhere.
5. As admin, send a test announcement with a link to `/Vocabulary`, only when the recipient check of section 7 passes.
6. Remove the throwaway rows by key.

Record database evidence.

---

## 9. Constraints
- One agent, local only.
- **Granted:** local commits on `feature/notifications-complete`, one per phase, via `tct-commit-message`; one `pull-local.ps1` at T0.
- **Not granted:** push, PR, merge, rebase, reset, deploy.
- Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, screenshots.
- Never reset or reseed the database; no paid AI calls; never notify a real person; never print credentials.
- Plan and code comments in English; UI copy in Vietnamese exactly as written here.

## 10. Definition of Done
- [ ] F1–F20 fixed, mitigated as decided, or disproved with evidence.
- [ ] One display rule for time, icon, category and link across the page, the bell and the API; Vietnam time everywhere.
- [ ] Mark-read and mark-all keep the page, the bell, the header badge and the sidebar badge in sync.
- [ ] Bell paging has no duplicates; bell is keyboard-complete with focus return.
- [ ] Announcements: no deleted accounts, no duplicates, optional safe link, correct history with sender.
- [ ] Billing activation creates one notification.
- [ ] Page and bell match the mockups in the Clay language on laptop and phone; forced colours, reduced motion, contrast recorded.
- [ ] No migration; layout, worker and model diffs within the allowed lines; other owners' tests unedited and green.
- [ ] Tracker, known-issues and section 12 updated.

## 11. Risks
| Risk | Mitigation |
| --- | --- |
| Changing `GetNotificationsPageAsync` breaks the worker or Goals callers | Additive parameters with defaults; their tests in G2/G13 |
| Enum member triggers a model change | `HasConversion<int>`; G5 checks `has-pending-model-changes` |
| Shared layout edit conflicts with Châu | Smallest diff; G7; noted in known-issues |
| Admin test announcement reaches real people | Recipient check and stop rule (section 7) |
| Existing node test reads the inline script | Path update only, assertions kept, recorded |
| Old rows with emoji or English | Display helper cleans titles; text of old rows otherwise unchanged |
| Keyset cursor with equal timestamps | Cursor on `(CreatedAt, Id)` |

## 12. Execution notes
(The agent appends T0 discovery notes — including the verdict on each finding — and one gate record per phase here.)

### T0 — Baseline, reproduction, discovery (2026-11-17) — `docs(notifications): save the plan and prompt with the T0 baseline`

**Setup.** Cleaned tree check passed (only `appsettings.Development.json` and `Claude outputs/` untracked/unstaged). `master` pulled once with `scripts/pull-local.ps1` (fast-forward `2842ca4` → `2bbbc48`, exit 0, shared-data sync 0 record operations, `last-sync.json` current). Branch `feature/notifications-complete` created from `2bbbc48`.

**Baseline.**
| Check | Result |
| --- | --- |
| Build (`dotnet build --no-incremental`, solution) | 0 errors, **57 warnings** |
| .NET tests (`dotnet test --no-build`) | **2366 total = 2331 passed, 35 skipped, 0 failed** |
| Node tests (`node --test scripts/tests/*.cjs`) | **405 total, 404 passed, 1 failed** (known: "Speaking selection maps original roots …" hash test) |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." |
| Dev database | 10 users; **user 4 is the owner's real account** (not synthetic); 31 notification rows (types 2, 5, 6, all unread, all with event keys) |

**Before-screenshots** (ignored): `.tmp/notifications/before/` — `/Notification/Index` all and unread, bell open, at 1440 and 390 (Standard learner).

**Verdict on F1–F20** (code reading plus the local Release app):
| # | Verdict | Evidence |
| --- | --- | --- |
| F1 | Confirmed | `Index.cshtml` `FormatTimeAgo` uses `DateTime.UtcNow` and `AddHours(7)`; `NotificationService.BuildTimeAgo` is a second rule. Live: the bell showed "1 ngày trước" for an item the page rule would call "hôm qua". |
| F2, F3 | Confirmed | `Index.cshtml` script and `notifications.js` `handleMarkAllClick`: no sidebar badge update, no tab refresh, button text/disabled state stays; button shown with `Items.Any() \|\| isUnreadTab`. |
| F4 | Confirmed | `GetNotificationsPageAsync` uses `Skip/Take`; `notifications.js` asks for `currentPage + 1`. |
| F5 | Confirmed | Both lists render `role="button"` with `tabindex="0"`; no per-item read action; admin rows have no `RelatedUrl`. |
| F6 | Confirmed (code) | `CreateAdminAnnouncementAsync` filters only `Status != Blocked`. The dev DB has no anonymised user, so the regression is a test (T4). |
| F7 | Confirmed (code) | `EventKey = null`, one `SaveChanges`, no batch id. |
| F8 | Confirmed (code) | `History` does `.ToListAsync()` then groups by title+message+minute; sender literal "Admin". |
| F9 | Confirmed | Comment at `PaymentConfirmationService.cs` line ~181; the interface has no per-user create. |
| F10 | Confirmed | Generator titles/messages contain "Streak" and emoji. |
| F11 | Confirmed | `TryCreateWithEventKeyAsync` catches every `DbUpdateException`. `RelationalUniqueConflict` is `internal` in namespace `TCTEnglish.Services` (usable from the service). |
| F12 | Confirmed | `window.location.href = n.relatedUrl` / `url` with no check. |
| F13 | Confirmed (runtime) | After opening the panel `document.activeElement` is `BODY`; `aria-haspopup="dialog"` on the button; panel is `role="dialog"` with `aria-modal="false"`; `Esc` only closes. |
| F14 | Confirmed | Inline `<script>`, one inline `style`, unused `csrfToken`, two antiforgery inputs (page + dropdown); `ViewData["Filter"] = filter ?? "all"` echoes the raw value. |
| F15 | Confirmed | 41 + 37 hex/`rgb(` occurrences in `notification-page.css` / `notifications.css` (plan counted 38 + 35 with another pattern); inline styles in the dropdown header icon, the load-more button and both badges in `_Layout.cshtml`. |
| F16 | Confirmed (code) | `CreatedAt` is a plain `DateTime`; no `Z`. |
| F17 | Confirmed | Index empty text mentions only streak, goals and badges. |
| F18 | Confirmed | The service clamps `page`; the controller never redirects. |
| F19 | Confirmed | `handleItemClick` awaits `fetchUnreadCount()` before navigating; `fetchUnreadCount` always calls `scheduleNextPoll`. |
| F20 | Confirmed | `MarkAllAsReadAsync` logs `updated` and returns `OperationResult.Success()`. |
| **F21 (new)** | **Confirmed (runtime)** | `GET /Notification` answers **404**: the only conventional route is `{controller=Home}/{action=Landing}/{id?}`, so `/Notification` resolves to a missing `Landing` action. The page works at `/Notification/Index` only (the sidebar link and the AI guide use that path). Plan 2.4 expects `/Notification` to work as well. Fix in T3: `[HttpGet("Notification")]` + `[HttpGet("Notification/Index")]` on `Index` (controller only, no `Program.cs` change). |

**Discovery (the five unknowns).**
1. **Preference read:** `INotificationPreferenceService.ReadAsync(int userId, bool pushConfigured, CancellationToken)` → `NotificationPreferenceForm` (`DailyReminderEnabled`, `ReminderHour`, `ReminderMinute`, `PushOptIn`, `WeeklyEmailOptIn`, `PushState`, `EmailState`). `pushConfigured` comes from `IOptions<OutboundDeliveryOptions>.Value.PushConfigured` (as in `AccountProfileService`). Registered scoped in `Program.cs:80`; no change needed there.
2. **Bell ids** (`notificationBtn`, `notificationBadge`, `notifPanel`, `sidebarNotifBadge`): used only by `notifications.js`, `_Layout.cshtml` (lines 73, 77, 286), `_NotificationDropdown.cshtml`, `Views/Notification/Index.cshtml` and `scripts/tests/test_notification_behavior.cjs`. No C# test names them.
3. **`OperationResult<T>`:** exists (`Services/OperationResult.cs`): `Success(T)`, `NotFound`, `Invalid`, properties `Status`, `Value`, `ErrorMessage`, `IsSuccess`; implicit conversion to `OperationResult`. The mark-all signature change returns `OperationResult<int>`; the old `Task<OperationResult> MarkAllAsReadAsync(int)` stays as an overload.
4. **Business clock zone:** `LearningBusinessClock` keeps its zone private and exposes only `UtcNow`, `BusinessDate` and date helpers. It is a forbidden file, so `NotificationDisplay` resolves the same zone itself (`SE Asia Standard Time`, then `Asia/Ho_Chi_Minh`, then UTC) and `NotificationService` passes `_clock.UtcNow` as "now".
5. **Local database:** **not only synthetic users** — user 4 (`huynhphutrong2829@gmail.com`) is the owner's real account. Therefore **no admin announcement will be sent locally** (plan section 7 stop rule for that step); the admin path is verified by tests, and plan 8.5 step 5 will be recorded as "not run, by rule".

**Decisions D1–D4** read and applied as defaults (D1 = LÀM, D2 = LÀM, D3 = GIỮ, D4 = LÀM).

**Event keys of rows created:** none.

### Gate record — T1 Display and links (2026-11-17) — `feat(notifications): add the shared display model and the local-link rule`

| Gate | Result | Evidence |
| --- | --- | --- |
| G1 | pass | full `--no-incremental` rebuild: 0 errors, **57 warnings** (T0: 57) |
| G2 | pass | `NotificationDisplayTests` + `NotificationLinksTests`: 108 passed; the notification, billing, Goals and AI-guide tests run inside G3 |
| G3 | pass | **2474 total = 2439 passed, 35 skipped, 0 failed** (T0: 2366 → +108 new) |
| G4 | pass | node 405 total, 404 passed, 1 failed = the T0 speaking-catalog hash failure |
| G5 | pass | no route or JSON change; `has-pending-model-changes`: "No changes have been made to the model since the last migration." (after `PremiumActivated = 8`) |
| G6 | pass | no CSS, view or JS touched; `git diff --check` clean for the committed files (the unstaged `appsettings.Development.json` blank line is the owner's, never staged) |
| G7 | pass | three new files (`NotificationDisplay.cs`, `NotificationLinks.cs`, `NotificationDisplayTests.cs`) and **one enum member** in `Models/Notification.cs` (diff: `AdminAnnouncement = 7,` + `PremiumActivated = 8`) |
| G8–G12, G14 | n/a | no rendered surface in this phase (pure mapping and rules) |
| G13 | pass | the full suite incl. Goals, worker and reliability tests is green and unedited |
| G15 | pass | link tests reject `//evil`, `/\evil`, `https://…`, `javascript:`, `data:`, control characters; nothing is rendered yet; no real user was notified |

**Decisions.**
- The enum member moved from T2 into T1 because the T1 tests name `PremiumActivated`; it is still the single allowed line.
- A stored `IconClass` that matches the allowed shape is honoured for every type (plan 4.1 sentence), the per-type defaults apply only when it is missing or invalid. Because the existing generators store `fas fa-check-circle` and `fas fa-chart-line` for goals, those keep their stored icons until a row has none.
- `NotificationLinks.ToSafeLocal` rejects a backslash anywhere (stricter than "`/\`"), and `IsValidLocalPath` also rejects a path longer than 500 characters (for the admin form) while `ToSafeLocal` cuts a stored value to 500 for display.
- `NotificationDisplay.BusinessZone` resolves the Vietnam zone itself because `LearningBusinessClock` keeps it private (forbidden file). The unspecified-kind `CreatedAt` is read as UTC.
- `CleanTitle` strips only leading whitespace, `OtherSymbol`, `ModifierSymbol`, format characters and non-spacing marks, so "[Thử nghiệm] …", quotes, digits and `%` survive; an all-symbol title is kept as is.
- Weekday names are "Chủ nhật", "Thứ Hai" … "Thứ Bảy" (capital after "Thứ").
- `NotificationDisplay.ParseCategory` (query value → category, null when unknown) was added for T3.

**Differences from the mockup.** None (no UI).
**Tests changed.** None edited; one new file.
**Event keys of rows created.** None.

### Gate record — T2 Service (2026-11-17) — `feat(notifications): add category filters, keyset slices, counts and per-user creation to the service`

| Gate | Result | Evidence |
| --- | --- | --- |
| G1 | pass | full `--no-incremental` rebuild: 0 errors, **57 warnings** (T0: 57) |
| G2 | pass | `NotificationServicePagingTests` (37 new), `NotificationDisplayTests`, `NotificationLinksTests` and `NotificationReliabilityIntegrationTests` focused run: 152 passed, 0 failed |
| G3 | pass (after one rerun) | **2511 total = 2476 passed, 35 skipped, 0 failed** (T1: 2474 → +37 new). The first full run had **1 failure**, `AiRagPhase5FixRound2Tests.StreamCompletion_UsesTheSameProjectionDeadline` (a wall-clock deadline test of the AI module: it waits `budget + 3 s`); it passed 3 of 3 times when run alone (8 s each) and passed in the rerun of the whole suite with no code change in between. Unrelated to notifications; recorded as a load-sensitive test. |
| G4 | pass | node 405 total, 404 passed, 1 failed = the T0 speaking-catalog hash failure |
| G5 | pass | route and JSON untouched in this phase; `has-pending-model-changes`: "No changes have been made to the model since the last migration." |
| G6 | pass | no CSS, view or JS touched; `git diff --check` clean for the committed files |
| G7 | pass | files: `Services/NotificationService.cs`, `Services/INotificationService.cs`, `ViewModels/NotificationViewModel.cs` (all additive), `Services/Notifications/NotificationDisplay.cs` (T1 file, helpers added), one new test file; no forbidden file |
| G8–G12, G14 | n/a | no rendered surface |
| G13 | pass | Goals completion/badge/streak generators still create notifications with the same event keys (new tests assert the keys); worker and reliability tests green and unedited |
| G15 | pass | `CreateForUserAsync` rejects `//evil`, `https://…`, `javascript:`; a real write failure is a warning with type and user id only (test asserts the text is absent); no real user was notified |

**Decisions.**
- `GetNotificationsPageAsync` gained an optional `category` (existing named-argument callers compile unchanged); `MarkAllAsReadAsync(int)` stays and delegates to the new `MarkAllAsReadAsync(int, NotificationCategory?)` that returns `OperationResult<int>`.
- Keyset slice: `GetNotificationsSliceAsync(userId, take, unreadOnly, beforeCreatedUtc, beforeId)` orders by `(CreatedAt, Id)` descending and reads `take + 1` rows; `NextCursor = (Before, BeforeId)` (the JSON names will match the `before` / `beforeId` query parameters). Take is clamped to 1–50.
- `GetCategoryCountsAsync` returns an entry for every category (zeros included).
- `TryCreateWithEventKeyAsync` now returns Created / Duplicate / Failed: only `RelationalUniqueConflict.IsUnique` is a duplicate (MySQL 1062/1022 and SQLite 19 UNIQUE tested); any other `DbUpdateException` detaches the entity, logs a warning with type and user id and returns Failed without throwing (generators ignore it, `CreateForUserAsync` reports a failure).
- Item view models are built from `NotificationDisplay`; `RelatedUrl`/`IconClass` are always the safe/allowed values, `CreatedAt` carries `Kind = Utc`; the old private `BuildTimeAgo` was removed (F1 on the service side).
- Generator copy follows plan 4.2 exactly; event keys are unchanged.
- `PremiumActivated` is stored as an int (enum member added in T1, no migration).

**Differences from the mockup.** None (no UI).
**Tests changed.** None edited; one new file.
**Event keys of rows created.** None in the dev database (all tests use disposable SQLite).

### Gate record — T3 Controller and API (2026-11-17) — `feat(notifications): serve /Notification, add the cursor, counts and category to the notification API`

| Gate | Result | Evidence |
| --- | --- | --- |
| G1 | pass | full `--no-incremental` rebuild: 0 errors, **57 warnings** (T0: 57) |
| G2 | pass | `NotificationPageIntegrationTests` (30), `NotificationServicePagingTests`, `NotificationReliabilityIntegrationTests`: 74 passed, 0 failed in the focused run |
| G3 | pass | **2541 total = 2506 passed, 35 skipped, 0 failed** (T2: 2511 → +30 new) |
| G4 | pass | node 405 total, 404 passed, 1 failed = the T0 speaking-catalog hash failure |
| G5 | pass | contracts of 2.4: `GET /Notification/Index` still answers (and now `GET /Notification` — F21); `unread-count` → `{ count }`; `api/notifications` keeps `{ success, data: { items, unreadCount, totalCount, currentPage, hasMore } }` and every old item field (`NotificationPageIntegrationTests.Api_List_KeepsTheOldFields_AndAddsTheNewOnes`); mark-read and mark-all keep `{ success }` and add counts; `has-pending-model-changes`: none |
| G6 | pass | no CSS, view or JS touched; `git diff --check` clean for the committed files |
| G7 | pass | `Controllers/NotificationController.cs`, `Services/NotificationService.cs`, `Services/INotificationService.cs`, `ViewModels/NotificationViewModel.cs`, `Services/Notifications/NotificationDisplay.cs` (one helper made public), one new test file; no forbidden file; no `Program.cs` change |
| G8–G12, G14 | n/a | no rendered surface changed in this phase (the old page markup is untouched) |
| G13 | pass | the full suite incl. Goals, worker, reliability, AI-guide tests is green; the old Index view renders for every filter |
| G15 | pass | another user's id → 404 and nothing changes; missing antiforgery token → 400 on both POSTs; an unknown `category` on mark-all is a 400 (never widened to "everything"); a bad `before`/`beforeId`/`category` on the list is a 400 with a Vietnamese error; the cursor time is parsed as UTC (a `Z` string is not shifted to server local time); the raw `filter`/`category` query values are never put into a link; no real user was notified |

**Decisions.**
- `Index` carries `[HttpGet("Notification")]` and `[HttpGet("Notification/Index")]` (F21). The conventional route stays for every other controller.
- An unknown page `filter`/`category` falls back to "all" / no category; the raw values are not echoed. A `page` beyond the last page answers **302** to the last page with the same `filter` and `category` (F18).
- `GET api/notifications`: without `before` it pages by number as before and now also returns `nextCursor = { before, beforeId }` (null when there is no more); with `before` (ISO-8601, UTC) and `beforeId` it returns the keyset slice (`currentPage` is 0 there) and the same envelope. `category` works in both modes. `createdAt` now carries `Z`.
- `POST mark-read` returns `{ success, unreadCount }`; `POST mark-all-read[?category=]` returns `{ success, updated, unreadCount }`.
- The page still renders the old `NotificationListViewModel`; `ViewData` gained `Category` (the page view model comes with T6).
- The shared `_CreateFolderDialog` partial puts the current URL (encoded) into a hidden `returnUrl` input, so a raw query value does appear in the HTML as an encoded value; the test asserts only that this page builds no link from it. Noted for the owner of that partial; not changed.
- The test host answers an anonymous request with 401 (the real cookie scheme redirects to login); the test accepts either.

**Differences from the mockup.** None (no UI change).
**Tests changed.** None edited; one new file.
**Event keys of rows created.** None in the dev database (the tests use their own disposable databases).

### Gate record — T4 Admin, billing and worker copy (2026-11-17) — `feat(notifications): send announcements once per batch, notify the buyer on activation and plain-Vietnamese worker copy`

| Gate | Result | Evidence |
| --- | --- | --- |
| G1 | pass | full `--no-incremental` rebuild: 0 errors, **57 warnings** (T0: 57) |
| G2 | pass | `AdminAnnouncementTests` + `AdminAnnouncementControllerTests` (33) and `PaymentActivationNotificationTests` (7) green; `PaymentConfirmation*`, `NotificationWorker*`, `NotificationReliability*` run inside G3 |
| G3 | pass | **2581 total = 2546 passed, 35 skipped, 0 failed** (T3: 2541 → +40 new) |
| G4 | pass | node 405 total, 404 passed, 1 failed = the T0 speaking-catalog hash failure |
| G5 | pass | no route or JSON contract changed; `has-pending-model-changes`: none |
| G6 | pass | no CSS or JS; the admin view gained only two form fields (no inline style/script added); `git diff --check` clean for the committed files |
| G7 | pass | `NotificationWorker.cs` diff = exactly the four string literals (two `Title`, two `Message`); `PaymentConfirmationService.cs` diff = two `using`s, an optional constructor parameter, the notification call and its private helper; `Program.cs` untouched (the optional parameter is resolved by the existing registration); no other forbidden file |
| G8–G12, G14 | n/a (T8) | the admin pages get their copy/accessibility pass in T8; only two fields were added here so the form keeps working with the new controller |
| G13 | pass | the full suite (Goals, worker, reliability, billing, IPN, admin billing) is green and unedited; the nine existing `new PaymentConfirmationService(ctx, subs, logger)` calls compile unchanged |
| G15 | pass | announcements: blocked and anonymised accounts excluded, one batch id = one send, links outside the site rejected on the form and in the service, antiforgery 400, learner role refused; billing: the notification runs after the payment commit and every failure (exception or failed result) is swallowed and logged; **no notification was sent to anyone: all announcement tests use disposable SQLite or the test host; the dev database was not touched** |

**Decisions.**
- `SendAdminAnnouncementAsync(AdminAnnouncementRequest)` is the new entry point; the old `CreateAdminAnnouncementAsync(title, message, adminId)` delegates to it with a fresh batch id (so it also skips anonymised accounts and writes keys). Batch key `announce:{adminId}:{batchId:N}`; the sender's own row is the "already sent" probe (it uses the `(UserId, EventKey)` index); rows go in chunks of 500 inside one transaction; a unique conflict means a racing request wrote the batch and the whole transaction is rolled back (`AlreadySent`, test `Send_AConflictWithAnotherRequestsRows_RollsBackTheWholeBatch`).
- The announcement icon is now `fa-solid fa-bullhorn`; admin accounts receive their own announcement.
- History: keyed batches first (grouped by the key, newest first), then legacy rows grouped by `(Title, Message, CreatedAt)` — one send wrote one instant, so two sends within a minute no longer merge — paged in the database; the sender name comes from the key (`FullName`, else "Quản trị viên"); a group older than the 30-day retention is flagged `CopiesMayBeCleaned` (the original recipient count is not stored, so "còn n bản lưu" is shown for those).
- The history query orders on the group aggregates before the projection (an `OrderBy` after the record projection is not translatable); it ran on SQLite here and is exercised against the Docker MariaDB when the admin History page is opened in T8/T10.
- `PaymentConfirmationService` takes `INotificationService? notifications = null` (optional so the existing direct constructions in tests stay valid; DI supplies it). Message: "Gói {tên gói} có hiệu lực đến {dd/MM/yyyy}." in Vietnam time; key `premium:{OrderCode}`; link `/Billing/Order/{OrderCode}`; only the `Activated` outcome notifies (repeat, rejected-amount and lost-race outcomes do not). An admin confirmation also notifies the buyer.
- D4 worker copy as in plan 4.2: "Chuỗi ngày học sắp mất" / "Bạn chưa học hôm nay, chuỗi {n} ngày sẽ mất nếu không học trước nửa đêm." and "Sắp nhận huy hiệu {tên}" / "Còn {n} bước nữa để mở khóa huy hiệu \"{tên}\"." No worker test asserted the old text.
- The admin form (`Create.cshtml`) now posts a hidden `BatchId` and an optional "Liên kết" field with the plan's hint; the visual and copy pass is T8.

**Differences from the mockup.** None (admin only, no mockup).
**Tests changed.** None edited; three new files.
**Event keys of rows created.** None in the dev database.

### Gate record — T5 UI foundation (2026-11-17) — `feat(notifications): add the page styles, item and side-panel partials and the pure notification core`

| Gate | Result | Evidence |
| --- | --- | --- |
| G1 | pass | full `--no-incremental` rebuild (Razor views compile in Debug): 0 errors, **57 warnings** (T0: 57) |
| G2 | pass | no .NET behaviour changed in this phase; the notification, billing and Goals tests run inside G3 |
| G3 | pass | **2581 total = 2546 passed, 35 skipped, 0 failed** (T4: 2581, unchanged) |
| G4 | pass | node **421 total, 420 passed, 1 failed** (T0: 405 / 404 / 1): +16 new tests in `scripts/tests/test_notification_core.cjs`; the one failure is the T0 speaking-catalog hash failure |
| G5 | pass | no route or JSON change; `has-pending-model-changes`: none |
| G6 | pass | `notification-page.css`: 0 hex/`rgb()`/`hsl()`, 0 `:root`, 0 `!important`, 0 literal `font-weight`, 0 `font-family`; no `style=` or `<script>` in the two partials; no `innerHTML` in `notification-core.js`; `git diff --check` clean |
| G7 | pass | new: `notification-page.css` (rewrite), `Views/Notification/_NotificationItem.cshtml`, `_NotificationSidePanel.cshtml`, `wwwroot/js/notification-core.js`, `scripts/tests/test_notification_core.cjs`; edited: `ViewModels/NotificationViewModel.cs` (additive), `study-kit.css` (**only** `.tn-page` added to the 143 page-scope `:is(...)` lists and to the header comment; no rule added or changed) |
| G8–G12 | n/a until T6 | the partials and the stylesheet are not rendered until the page view is switched in T6 |
| G13 | pass | full suite green and unedited |
| G14 | pass (static) | Clay tokens only (`--tct-clay-surface`, `-shadow-quiet`, `-lift`, `-button`, `-overlay`, radii, spacing, type, `--tct-z-dropdown`); no new palette, font, icon family or package |
| G15 | pass | partials render only server-validated `Tone`, `IconClass`, `Url`; all text Razor-encoded; the core mirrors the local-link rule with the same rejection cases as the C# tests |

**Decisions.**
- **Deviation: `notifications.css` (bell panel) is rewritten in T7, not T5.** Its selectors and the panel markup and script change together; rewriting the stylesheet alone would leave the live bell unstyled for a commit. T7 carries it.
- **Known intermediate state:** the committed T5 `notification-page.css` replaces the old class names, so the old `Views/Notification/Index.cshtml` is unstyled between this commit and T6. Nothing is deployed from this branch; T6 switches the page to the new markup.
- Tone → token mapping (the rule beats the mockup, which uses a purple crown tile and orange flame tile; there is no such token and the plan forbids a new palette): streak = `danger-soft` / `danger-text`, goal = `success`, badge = `warning`, payment = `--tct-accent` with `--tct-on-accent`, system = `primary`.
- Item markup: `<li class="tn-item tn-tone--…">` with an `h3` title, a `<time datetime>` and, with a link, a real anchor `data-notification-link`; unread items carry a dot with visually hidden "Chưa đọc" and a `data-notification-read` button (`aria-label="Đánh dấu đã đọc: {title}"`, 44 px); a card without a link has no interactive element except that button.
- `NotificationPageViewModel` / `NotificationSidePanelViewModel` were added for T6 (list, filter, category, counts, side panel with `HasSettings`).
- `notification-core.js` (UMD, `window.TctNotificationCore`) holds: badge text, bell accessible label, "n mới" chip, page summary, mark-all toast text, `safeLocalUrl` (mirror of the C# rule), day grouping, id de-duplication, cursor query, empty-state kind, the mark-read / restore / mark-all reducer, tab-arrow index, near-bottom test and the poll back-off.

**Differences from the mockup.** Tile hues as above (rule over mockup); the rest is built in T6.
**Tests changed.** None edited; one new node test file.
**Event keys of rows created.** None.
