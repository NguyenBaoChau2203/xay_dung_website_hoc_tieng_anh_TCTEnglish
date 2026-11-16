# Stage 04 — Actual badge contract for stage 06

Ngày 14/11/2026. Backend đã triển khai và kiểm chứng local sau stage 03.
Đọc cùng `03-learning-contract.md`; frontend không tính lại eligibility hoặc cấp
thưởng. Tài liệu này chứa quy tắc phát triển, không được gửi nguyên cho learner.

## Consumers and actual members

- `IGoalsService.GetGoalsAsync(userId)` → `GoalsViewModel.Badges` là
  `List<GoalsBadgeViewModel>`. `/Goals` vẫn render Razor; không thêm badge JSON API.
- `IGoalsService.GetEarnedBadgesForProfileAsync(userId)` →
  `IReadOnlyList<GoalsBadgeViewModel>`. `ProfilePageViewModel.EarnedBadges`
  nhận danh sách này qua `AccountProfileService`; chỉ có grants đã lưu của user.
- Hai đường dùng chung `TCTEnglish.Services.Learning.BadgeProjection.Build`.
  GET không grant hoặc reconciliation. Không suy earned từ progress100%.
- Các tên dưới đây là tên **C# PascalCase**, không phải một JSON endpoint mới.
  Nếu stage06 cần JSON, dùng cấu hình serializer của endpoint thực tế; không tự
  giả định PascalCase/camelCase từ ví dụ `JsonSerializer.Serialize` trong test.

| Member | Type | Semantics |
| --- | --- | --- |
| `Code` | string | Stable code khi được phép thấy; **empty string** với locked mystery; không có BadgeId trong VM |
| `Name`, `Description`, `IconClass` | string | Đã lọc trên server; Razor tiếp tục encode |
| `IsUnlocked` | bool | Có persisted `UserBadge`; không đồng nghĩa đủ metric hiện tại |
| `IsRecentlyUnlocked` | bool | `AwardedAt` thuộc **hôm nay UTC+7**, cùng clock ở Goals/Profile |
| `AwardedAt` | DateTime? | UTC storage timestamp; null trước khi earned; đổi UTC+7 khi format ngày cho người dùng |
| `IsRetroactive` | bool | Được ghi nhận từ explicit catch-up; thời gian grant hiện tại, không giả ngày đạt trong quá khứ |
| `DifficultyKey`, `DifficultyLabel` | string?, string | Bốn difficulty thường; null/empty với locked mystery |
| `DisplayGroupKey`, `DisplayGroupLabel` | string, string | Nhóm trình bày/filter; mystery luôn `bi-an`, kể cả khi earned |
| `IsSecret` | bool | Thuộc nhóm khám phá bí mật; độc lập difficulty |
| `IsRedacted` | bool | Locked mystery đã bị xóa toàn bộ thông tin có thể tiết lộ rule |
| `ProgressValue`, `TargetValue`, `ProgressPercent` | int? | **Nullable mới**; null khi redacted/unsupported; earned luôn100% |
| `HasKnownProgress` | bool | Có cả current/target số, không ép null thành0 |
| `MetricLabel` | string | `XP`, `video`, `thẻ`, `bài`, `ngày`; empty khi redacted/unsupported |
| `ProgressLabel` | string | Copy server; không tự nối current/target khi không có số |
| `IsAvailable` | bool | Có supported positive rule mà learner được phép thấy; false trên locked mystery là trạng thái presentation |
| `CanRecommend` | bool | Chỉ badge locked, không redacted và available; dùng cho **mọi** next-badge hint |
| `StatusText` | string | Earned/catch-up/hidden/unsupported copy server |

## Exact display keys and labels

| Key | Label |
| --- | --- |
| `chill` | Nhập môn chill |
| `cuon` | Bắt đầu cuốn |
| `chien-than` | Hệ chiến thần |
| `trum-cuoi` | Trùm cuối server |
| `bi-an` | Ủa, bí mật nè! |
| `other` | Thành tích |

`other` là fallback trình bày an toàn cho tier tương lai/null; không ghi đè dữ
liệu gốc. `bi-an` chỉ là display group; difficulty bên dưới của hai award mới là
`chien-than`, chỉ được trả ra khi đã earned. Không phân loại bằng tên/code hoặc
threshold. Không tải catalog/entity/seed ra JavaScript để lấp dữ liệu đã redacted.

## Locked mystery example

Ví dụ chọn các members thực tế; các field mặc định khác vẫn không mang thông tin
bí mật. Hai mystery locked có cùng generic copy, không cần browser nhận biết ID.

```json
{
  "Code": "",
  "Name": "Ủa, bí mật nè!",
  "Description": "Một điều bất ngờ đang chờ bạn khám phá.",
  "IconClass": "fas fa-lock",
  "IsSecret": true,
  "IsRedacted": true,
  "IsUnlocked": false,
  "DifficultyKey": null,
  "DifficultyLabel": "",
  "DisplayGroupKey": "bi-an",
  "DisplayGroupLabel": "Ủa, bí mật nè!",
  "ProgressValue": null,
  "TargetValue": null,
  "ProgressPercent": null,
  "MetricLabel": "",
  "ProgressLabel": "Chưa khám phá",
  "AwardedAt": null,
  "HasKnownProgress": false,
  "IsAvailable": false,
  "CanRecommend": false,
  "StatusText": "Chưa khám phá"
}
```

Không gắn name/rule/threshold/code thật vào HTML comment, `data-*`, aria, title,
tooltip, hidden DOM, filter index hoặc serialized state. Server đã redacted trước
Razor. `data-badge-code=""` hiện tại là chủ ý; không dùng làm DOM ID hay unique key.
Stage06 có thể dùng vị trí hiển thị cho card generic; không cần identity thật.

Khi earned, `Code/Name/Description/DifficultyKey/MetricLabel/TargetValue` trả chi
tiết thật; `IsRedacted=false`, `IsUnlocked=true`, `ProgressPercent=100`, có ngày
nhận. Dù current streak giảm, earned vẫn giữ. Profile chỉ hiện các earned này.
Catch-up dùng `StatusText="Đã ghi nhận thành tích trước đây"` và
`IsRetroactive=true`; không diễn đạt thành hoạt động học vừa làm. RecentlyUnlocked
vẫn theo ngày grant thực tế; UI nên dùng cờ retroactive để chọn copy phù hợp.
MariaDB có thể materialize `AwardedAt.Kind=Unspecified` dù giá trị lưu là UTC.
Razor cần coi giá trị này là UTC trước khi đổi UTC+7. Nếu thêm JSON endpoint,
normalize UTC rõ ràng khi serialize; không để browser tự hiểu chuỗi thiếu `Z`
theo timezone của máy. Stage04 không thêm endpoint hoặc đổi timestamp đã lưu.

## Server eligibility and durable evidence

Enum `BadgeMetricType` giữ nguyên giá trị1–7; thêm8
`DistinctWritingExercisesCompleted`,9 `ConfirmedLongestStreak`.

- Distinct Writing: hợp của exercise IDs trong completed progress có
  `CompletedAt<=now`, và completion credits `Feature="Writing"` có canonical
  `exercise:<id>` hoặc legacy `writing-exercise:<id>`, timestamp/date không tương
  lai. Một ID chỉ tính một lần; credit còn tồn tại vẫn giữ bằng chứng khi lesson
  bị xóa. Aggregate `WritingCompletedCount` **không** chứng minh distinct cho award
  mới. Legacy Writing badges vẫn dùng aggregate cũ để không đổi hợp đồng12 badge.
- Active dates: `StudyDayProjection.ReadAsync`, distinct UTC+7 ngày có daily
  counters/bonus confirmed hoặc Passed persisted Writing attempts. Empty row,
  generic XP-only, login, browser duration và ngày tương lai không tạo active day.
- Confirmed longest: longest consecutive run của tập ngày có bằng chứng trên;
  không tạo ngày giả từ `Users.LongestStreak`. Không viết lại streak đã lưu.
- XP: tổng XP lưu trong daily rows tới hôm nay, giữ semantics stage03. Không tạo
  XP bù từ credit hoặc lịch sử không đủ provenance.
- Unsupported metric hoặc threshold<=0 không grant; projection không khuyến nghị.

| ID / Code | Name | Difficulty / group | Rule |
| --- | --- | --- | --- |
|13 `writing-mood` | Bút mới lên mood | chill |3 distinct first completions |
|14 `study-checkin` | Check-in bằng bài học | chill |3 confirmed dates |
|15 `writing-deadline` | Deadline cũng phải nể | cuon |10 distinct first completions |
|16 `xp-mood` | XP lên mood | cuon |500 persisted XP |
|17 `keyboard-form` | Bàn phím vào form | chien-than |25 distinct first completions |
|18 `steady-flame` | Giữ lửa không seen | chien-than |14 confirmed consecutive dates |
|19 `keyboard-boss` | Trùm cuối bàn phím | trum-cuoi |50 distinct first completions |
|20 `monthly-flame` | Giữ lửa xuyên tháng | trum-cuoi |30 confirmed consecutive dates |
|21 `secret-plot-twist` | Plot twist thứ 13 | chien-than / bi-an |13 distinct first completions |
|22 `secret-quiet-effort` | Hệ chăm ngầm | chien-than / bi-an |21 confirmed dates |

Các rule/ngưỡng/tên đúng README. Có metric riêng cho bằng chứng distinct và
confirmed streak, không dùng aggregate mơ hồ cho các milestone mới. Current local
catalog chỉ10 bài public; stage05 thêm108 bài hoặc nội dung Admin tương lai giúp
đạt các mốc cao hơn10. Không hạ threshold, nhân credit bằng replay hoặc thêm nội
dung giả vào dev DB. Tests tạo50 bài synthetic trong fixture rồi hoàn thành qua
`IWritingService` thật để chứng minh đường earning.

## Grant, notification and catch-up boundaries

- Learning transaction + user-row lock ghi unique `(UserId,BadgeId)`, rồi durable
  v2 outbox intent `EventKey="badge:{userId}:{badgeId}"`. Completion wrapper có
  thể enqueue lại transition; dedupe kiểm cả Notifications/outbox nên không thêm
  intent thứ hai. Direct notification cũ dùng cùng key; dispatcher drain/replay
  không tạo notification thứ hai.
- Direct badge path cũng có outbox; grant/outbox failure rollback learning unit.
  Owned transaction retry deadlock1213 tối đa3 lượt; ambient transaction và lock
  timeout không bị retry riêng lẻ. Không cần Program.cs/DI registration mới.
- Near-badge worker loại secrets ngay ở query. Direct secret notification cần
  persisted grant của đúng user. Không gửi hint bí mật còn khóa.
- `GoalsService.ReconcileNewBadgesAsync(IReadOnlyCollection<int> userIds,
  bool dryRun=true, CancellationToken cancellationToken=default)` là operations
  method trên concrete service, **không** là public learner route/interface.
  Tối đa100 explicit positive IDs, chỉ missing new10, bỏ user đã anonymized/mất.
- `BadgeReconciliationResult(UserId, DryRun, BadgeIds)` là receipt private.
  Apply re-evaluate dưới lock, ghi `IsRetroactive=true`, không đổi XP/activity/
  streak/history, không backdate và **không tạo notification/outbox catch-up**.
  Có thể rollback từng user; batch không hứa atomic cả100 user. Rerun an toàn.
- `scripts/reconcile-badges.cs` chạy bằng SDK10 file-based runner, reference app
  net9, pin Relational9.0.4 để khớp app. Chỉ local Docker3307/dev hoặc acceptance
  loopback3308–3399; connection qua environment, không argv. Runner không migrate,
  seed hay khởi động web/providers. Luôn lưu dry-run trước apply; receipts ignored
  tại `.appdata/local/badge-reconciliation`.

```powershell
# From repository root, with local connection inherited in the process environment:
dotnet run --file scripts/reconcile-badges.cs -- 101,102
dotnet run --file scripts/reconcile-badges.cs -- 101,102 --apply
```

IDs trên chỉ minh họa. Không paste credential vào chat/command line; không dùng
receipt chứa user IDs làm frontend payload hoặc tài liệu tracked. Schema/reference
catalog đi bằng EF migration; không phải shared-content root/export package.

## Stage 06 binding acceptance

Use `DisplayGroupKey/DisplayGroupLabel` for filters and `DifficultyKey` only when
not redacted. Bind nullable progress only when `HasKnownProgress`; use
`CanRecommend` for all next-award suggestions. Render earned date using UTC+7 and
distinguish retroactive copy. Keep Profile earned-only. Verify locked HTML/JSON,
tooltips, aria and filter state contain no secret identity/rule. Do not fetch seed
definitions or implement browser unlock actions.

Các câu English trên là yêu cầu bàn giao để stage06 dùng lại. UI final filters,
tier styling và mọi trạng thái tương tác vẫn thuộc stage06; stage04 chỉ chỉnh hai
next-badge predicates thành `CanRecommend` và dùng safe server projection.

## Đối chiếu final ở stage07

Filter dùng display group đã redacted từ server; giá trị URL/storage không còn
trong catalog trở vềall. Home/End, mũi tên và roving tabindex chỉ thay nhóm hiển
thị. Modal dùng textContent/Bootstrap, Tab giữ focus trong modal và Escape trả
focus về thẻ đã mở. Không phát sinh endpoint/thuật toán unlock ở browser.

Cookie fixture thật đã ở12 distinct credits:7/8 câu bài13 vẫn không có code/rule
secret trong HTML; sau câu8 commit, có đúng13 credits và grant21, Goals/Profile
hiện “Plot twist thứ 13”. Secret còn lại vẫn redacted. Regressions có 30 case
dưới/đúng/trên ngưỡng cho 10 award mới. Provider29 kiểm all-ten journey,
outbox replay/rollback và concurrent grants/catch-up.
Receipt cuối cùng và phần chưa quan sát nằm trong ledger07.
