# Stage 03 — Learning and UI contract

Ngày 14/11/2026. Ghi trước khi triển khai stage 03; các kết quả kiểm chứng thực tế
được bổ sung trong ledger `03-goals-streak-profile.md`.

## Quy tắc học tập

- Login, GET/refresh, hint, draft, chỉnh mục tiêu và thời gian hiện diện không tự
  cấp streak/XP. Câu Writing chỉ đủ điều kiện khi server nhận Accepted (`Passed`)
  và transaction lưu attempt/progress/rewards/outbox commit thành công.
- Accepted giữa bài hoặc luyện lại bài cũ xác nhận ngày học. Một ngày UTC+7 chỉ
  tăng streak một lần và nhận tối đa10 XP bonus streak. Ngày thiếu làm chuỗi hiện
  tại hết hạn; lần học tiếp khởi động1. Kỷ lục dài nhất và huy hiệu đã đạt giữ lại.
- WritingCompletedCount đo **bài hoàn thành lần đầu trong đời**, không đếm câu,
  lượt nộp hay bài luyện lại. Mỗi exercise có một lifetime credit20 XP. Partial
  hoặc replay có ngày học nhưng không tăng mục tiêu Writing/20 XP lần nữa.
- Mục tiêu0 là tạm dừng;1–500 là mục tiêu hợp lệ cho năm kỹ năng. Tổng phần trăm
  là trung bình các phần trăm kỹ năng có target>0, mỗi kỹ năng được chặn100%.
  Không cộng các đơn vị thẻ/video/bài với nhau.
- Một clock UTC+7 injectable xác định ngày. Timestamp lưu UTC; LastStudyDate và
  ActivityDate là ngày nghiệp vụ. Một unit ghi nhận dùng một instant, kể cả unit
  chạy qua nửa đêm. Không đổi đồng hồ hệ điều hành.

## Nguồn và ngữ nghĩa dữ liệu

- `WeeklyActivity.ActivityCount` vẫn là số **thẻ từ vựng đã ôn**; không suy ra
  ngày học chung hoặc hoàn thành mọi mục tiêu từ số thẻ này.
- Tín hiệu ngày học chung lấy từ daily counters có hoạt động thật/bonus streak
  và persisted Accepted Writing attempts; không từ empty row, generic XP,
  login hay browser duration. Không dựng N chấm từ độ dài streak.
- Ngày không có bằng chứng được diễn đạt là chưa ghi nhận; không khẳng định người
  học chắc chắn nghỉ. Lịch sử mục tiêu không đủ trả null/unknown, không trả false.
- Thành tích mục tiêu ngày quá khứ dùng revision có hiệu lực cuối ngày; ngày
  baseline đầu tiên thiếu đủ lịch sử được bỏ qua. Hôm nay dùng target đang có.
  Profile habit rate giữ đơn vị **mục tiêu kỹ năng/ngày**, không phải tỷ lệ ngày
  mà tất cả mục tiêu cùng đạt. Thời gian/giờ/kỹ năng thường luyện vẫn từ telemetry
  segments riêng và không phải authority streak.
- Goals/Profile/dashboard dùng cùng phép chiếu streak không ghi dữ liệu khi GET;
  timestamp tương lai không chứng minh chuỗi hiện tại. Badge recently-unlocked
  nghĩa là mở khóa **hôm nay UTC+7** ở cả Goals và Profile.

## Dữ liệu cũ và giới hạn

Không tự backfill XP/streak/award từ login. Accepted attempts cũ là bằng chứng
ngày học hiển thị được, không tự sinh thưởng bù bằng GET. Lịch sử streak đã lưu
được giữ, không tự xây lại toàn bộ chuỗi. Huy hiệu mới/catch-up thuộc stage04.
Không thay đổi lesson content, tài khoản thật hoặc đồng bộ dữ liệu cá nhân.

## Hợp đồng thực tế sau triển khai

| Field / surface | Ý nghĩa và đơn vị |
| --- | --- |
| `GoalsViewModel.BusinessDate` | Ngày nghiệp vụ UTC+7 tại instant bắt đầu read unit |
| `GoalsViewModel.StudyDays` | Bảy ngày liên tiếp kết thúc hôm nay, cũ đến mới; server tính từng ngày |
| `GoalsStudyDayViewModel.BusinessDate/FullLabel` | Ngày nghiệp vụ; nhãn `dd/MM/yyyy` |
| `HasConfirmedStudy/StudyLabel` | Có bằng chứng học đã lưu / chưa ghi nhận; không biến thiếu bằng chứng thành khẳng định nghỉ học |
| `ActiveGoalCount/MetGoalCount` | Số mục tiêu kỹ năng của ngày; nullable khi lịch sử thiếu |
| `AllGoalsMet/GoalLabel` | true: mọi target dương đã đạt; false: có target dương chưa đạt; null: thiếu lịch sử hoặc không có target dương |
| `WeeklyActivity.ActivityCount` | Số thẻ từ vựng đã ôn; chart giữ nguyên dữ liệu/đơn vị cũ |
| `GoalCards` / `ProgressPercent` | Counter từng kỹ năng và trung bình phần trăm target dương; 0 ở Goals khi chưa có target để đo |
| `Profile.TodayGoalText/TodayGoalPercent` | Tổng hợp mọi kỹ năng cùng công thức Goals; percent null khi chưa đặt/tạm dừng toàn bộ mục tiêu |
| `Profile.Habits.PracticeDaysText` | Ngày confirmed / số ngày quan sát từ bằng chứng đầu tiên trong cửa sổ đến hôm nay; không mặc định mẫu số luôn7/28 |
| `Profile.Habits.GoalRateText` | Tỷ lệ **mục tiêu kỹ năng/ngày** đã đạt trên những ngày đã kết thúc đủ revision; hôm nay và baseline đầu bỏ qua |
| `Dashboard.TotalXp/TodayXp` | Tổng `UserDailyActivities.XpEarned` đã lưu đến business date hiện tại / riêng hôm nay; không tính từ số thẻ hay streak |
| `Dashboard.DailyGoalPercent/ActiveGoalCount/MetGoalCount/DailyGoalSummary` | Lấy từ Goals read snapshot, hiển thị từng đơn vị riêng |
| `Dashboard.StudyDays` | Chính snapshot ngày học từ Goals, không vẽ số chấm từ streak |
| `CurrentStreak/StreakDays/Dashboard.Streak` | Cùng phép chiếu: last-study hôm nay/hôm qua còn hiệu lực; thiếu/ngày quá cũ/tương lai trả0; GET không sửa giá trị đã lưu |
| `IsRecentlyUnlocked` | Badge mở hôm nay UTC+7 ở cả Goals và Profile; danh sách đã đạt vẫn đọc từ UserBadges |

Routes giữ nguyên: `/Goals`, `/Goals/Index`, `/Goals/UpdateGoal` (POST),
`/Account/Profile`, `/Home/Index`, `/Home/Writing`, `/Home/Writing/Exercises`,
`/Home/Writing/Practice`, `/Home/Writing/Practice/Data` và
`/Home/Writing/Practice/Evaluate` (POST). Controller `Study` có prefix `Home`;
stage07 đã sửa ví dụ `/Study/EvaluateWritingSentence` sai trong bản contract trước.
Không thêm endpoint browser để tự phát practice/reward. Dashboard `/` là landing
theo routing hiện tại; kiểm tra dashboard dùng `/Home/Index`.

Stage07 làm rõ presentation khi target0: Goals ghi “Đang tạm dừng” ở kỹ năng đã
thiết lập, không gọi nó là chưa đặt chỉ tiêu và không render0/0/progressbar cho
target đó. Nếu tất cả target đều0, Goals vàProfile cùng báo các mục tiêu đang
tạm dừng, không diễn đạt thành hoàn thành0%. History dots vẫn giữ projection
server; không sửa counters, revisions, XP hoặc schema để đổi cách hiển thị.

`ICompletionCreditService.RecordWritingPracticeAsync(userId, attemptId, token)` là
điểm nối nội bộ: yêu cầu transaction hiện có, attempt đã lưu đúng user/Passed,
không ở tương lai và thuộc ngày của operation. Nó ghi streak/bonus/badge hiện có
và outbox, không thêm `LearningCompletionCredit`. Content key outbox là
`attempt:{id}`; notification event keys theo user/ngày/mốc vẫn unique.
Exercise completion giữ key `{userId}:Writing:exercise:{exerciseId}`.
`ConfirmedWritingPractice` là cờ internal của update DTO, **không phải cột EF**.

Một row lock Users dùng chung được lấy trước credit/attempt/counter. Scoped reward
unit tham gia transaction bên ngoài; credit sở hữu transaction được retry deadlock
theo chính sách cũ. Deadlock/lock-wait/cancellation trong ambient unit phải làm
caller abort; Writing không tự retry một transaction đã hỏng. Nộp request mới là
retry an toàn; server vẫn giữ bonus/ngày và lifetime credit duy nhất.

Mục tiêu mới tạo không áp vào ngày trước khi nó tồn tại. Với mục tiêu legacy có
revision bắt đầu muộn, quá khứ thiếu vẫn unknown. Target ngày đã kết thúc lấy
revision cuối trước cuối ngày; thay target hôm nay không cấp XP/streak hay rewrite
counter/huy hiệu quá khứ.

Không đổi schema/entity/mapping, không có migration mới. Existing attempts,
daily counters/bonus, goal revisions, credit và outbox đáp ứng persistence contract.

## Checklist triển khai

- [x] Regression đỏ cho partial/replay, clock, expiry và read-only.
- [x] Atomic practice event + một clock + serialization user chung.
- [x] Typed study-day/goal-day fields, giữ biểu đồ vocabulary riêng.
- [x] Goals/Profile/dashboard, habit và badge recency nhất quán.
- [x] Boundary/repeats/isolation/rollback + MariaDB independent connections.
- [x] Review diff, build/test, ledger và bug memory (xem ledger cho kết quả cuối).
