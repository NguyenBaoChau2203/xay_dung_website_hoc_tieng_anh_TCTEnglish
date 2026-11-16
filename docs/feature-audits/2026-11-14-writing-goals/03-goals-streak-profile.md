# Stage 03 — Ngày học, streak, XP, Goals và Profile

Ngày 14/11/2026. Nhánh thực tế: `fix/writing-goals-profile-polish`.
Phạm vi IMPLEMENT_LOCAL; một agent đọc source, sửa, tự review và kiểm chứng.
Đã đọc prompt03, README/kế hoạch03, hai bàn giao01 và bàn giao02. README nằm ở
`docs/superpowers/plans/2026-11-14-writing-goals/README.md`; đường dẫn README trong
thư mục prompt không tồn tại. Không commit/push/PR/CI/deploy/gọi API trả phí.

## Kết quả và lỗi xác nhận

| Phát hiện | Bằng chứng / xử lý |
| --- | --- |
| Accepted giữa bài không ghi ngày học | Regression đầu thất bại: streak mong1, thực0. Nối attempt đã lưu vào reward/outbox trong chính Writing transaction, trước first-completion branch |
| Profile đọc raw streak, Goals giữ timestamp tương lai | Regression đầu có2 case expected0/actual5. Dùng chung `StudyDayProjection.CurrentStreak`, giữ stored history khi GET |
| Goals dùng clock static khác Streak | Dùng clock injectable cho Goals/target/badge/summary; operation scope giữ một UTC instant xuyên await và nửa đêm |
| Chấm ngày và XP dashboard được suy diễn | Source có `CardCount*5+Streak*20` và số chấm từ `Streak%7`. Chuyển sang daily XP đã lưu và Goals study-day snapshot; bỏ level/next-level không có rule thật |
| Profile chỉ tổng hợp Vocabulary | Tổng hợp các target dương cùng công thức Goals, giữ đơn vị riêng; dashboard cũng lấy Goals snapshot |
| Weekly vocabulary bị dùng thay study/goal presence | `StudyDays` typed tách presence khỏi nullable goal attainment; chart `WeeklyActivity` vẫn đếm thẻ |
| Profile recently-unlocked khác ngày/recency Goals | Cả hai nghĩa là mở khóa hôm nay UTC+7; không xóa/thu hồi earned history |
| Active-day badge/habit nhận empty/generic XP rows hoặc chỉ browser time | Chỉ counters confirmed/bonus và distinct Accepted attempt dates xác nhận ngày; telemetry giữ vai trò giờ/thời gian ước tính |
| Target mới làm lịch sử ngày cũ mất known state | Test target-edit phát hiện trước-sửa: expectedtrue/actualnull. Phân biệt ngày tạo goal mới với baseline revision của goal legacy |
| Daily challenge tách ghi counter và cập nhật streak | Dùng `RecordLearningActivityAsync` để có chung transaction/clock, tránh hai ngày khác nhau nếu qua nửa đêm |
| Acceptance fixture bỏ sót UserBadges khi reset | Provider mở rộng fail1/24 do awards test trước còn lại. Thêm UserBadges vào allowlist dọn của fixture sentinel-guarded; không tác động DB ứng dụng |

Hợp đồng thực tế, field/unit/null và retry policy ở
[03-learning-contract.md](03-learning-contract.md). Dry-run và ranh giới đối soát
ở [03-history-dry-run.md](03-history-dry-run.md).

## Persistence, atomicity và preservation

- `PersistWritingAttemptAsync` trả entity/Id và timestamp clock. Mọi `Passed=true`
  gọi internal practice recorder; recorder xác minh đúng user, attempt đã lưu,
  Accepted và ngày hiện tại. Không reprocess một attempt cũ sang ngày mới.
- Practice event dùng existing attempt làm durable evidence, không tạo completion
  credit. Partial/replay chỉ có bonus10 XP một ngày; first exercise completion
  giữ credit20 XP và WritingCompletedCount1 một lần trong đời.
- Row lock Users được lấy trước attempt/credit/counters, chung thứ tự với Goals/
  Streak/account mutation. CompletionCredit giữ gate cũ khi nested, intent/outbox
  cùng transaction. 1062 chỉ recover đúng unique conflict;1213/1205/cancellation
  trong ambient unit propagate. Request mới có thể retry sau rollback.
- Không chỉnh persisted model/DbContext/mapping/migration; cờ practice thuộc DTO
  internal. Provider kiểm tra `HasPendingModelChanges=false`, migrate repeat giữ
  history, attempts, original answers/timestamps, credits, awards và users.
- Không sửa/backfill progress thật, không dùng login để bịa lịch sử. Legacy
  Accepted dates hiển thị được nhưng GET không bù XP/streak/badge. Stage04 tiếp
  nhận đối soát badge có bằng chứng; dựng lại XP/streak quá khứ cần quyết định riêng.
- Không đổi lesson/shared content nên không có shared-data package mới. Không
  đụng User Secrets, protected config, hosting, dev DB/volume hay OS clock.

## Tệp và bảo toàn stage trước

- Services: `WritingService`, `GoalsService`, `GoalsActivityUpdate`, `StreakService`,
  `CompletionCreditService`, `LearningBusinessClock`, `LearningHabitService`,
  `Accounts/AccountProfileService`, `Accounts/UserMutationScope`.
- Thêm read projections `Services/Learning/{StudyDayProjection,GoalDayProjection}.cs`.
- Dashboard controller/ViewModel/view và Goals ViewModel/view được bind dữ liệu
  thật. Giữ CSS/layout/shared partial, DOM hooks, Clay foundation và HTTP contracts.
- Tests: thêm `GoalsStage03RegressionTests`, `GoalsStage03MariaDbTests`; cập nhật
  assertion dashboard của `GoalsPhase1IntegrationTests` theo mục tiêu thật và sửa
  allowlist fixture MariaDB. Không giảm regression stage02.
- SHA256 đối chiếu bàn giao02: Profile view, Writing index/list views và goals.js
  giữ nguyên. Goals view đổi riêng binding/copy được scope03 cho phép. Các sửa
  Writing assessment/Admin/drafts/controllers từ stage02 vẫn còn nguyên.

## Kiểm chứng thực tế

Artifact ignored: `.appdata/local/test-results/goals-stage03/`.

| Kiểm tra | Kết quả |
| --- | --- |
| Baseline regression03 |3 fail/3 pass, `stage03-red.trx` |
| Bộ rộng Goals/Writing/ambient/Profile/Account/notification |341 pass/0 fail/16 gated skip, `stage03-final.trx` |
| Review cuối:13 case03 + Goals1/3/6, deadlock/recovery/ambient/Profile/Account |53 pass/0 fail/0 skip, `stage03-final-review.trx` |
| MariaDB vòng đầu |11 pass/0 fail/0 skip |
| Provider mở rộng trước sửa fixture |23 pass/1 fail/0 skip, `stage03-mariadb-final.trx` |
| Provider sau sửa fixture, gồm account/worker/Admin variants |24 pass/0 fail/0 skip, `stage03-mariadb-reviewed.trx` |
| Node Writing/Goals/Account |38 pass/0 fail/0 skip, `node-final.log` |
| Build app cuối `--no-restore` |Thành công,0 error (incremental build0 warning), `build-final.log` |
| Diff whitespace/protected config |`git diff --check` đạt; không có diff Program.cs/appsettings/.csproj |

Cases03 gồm Accepted partial;100 same-day replays;17:00 UTC rollover; bỏ ngày;
longest; final completion+replay ngày sau; wrong/review-only; reads không thưởng;
timestamp tương lai; empty/XP-only/legacy evidence; mixed units; target0/500/
invalid cho cả5 kỹ năng; revision quá khứ; hai user/anonymized/missing user; badge
recency; clock dịch sang ngày mới ngay sau Save attempt nhưng cả unit vẫn ngày cũ;
reprocess attempt cũ bị từ chối. Không thay OS clock.

MariaDB10.5.23 dùng DB `tct_acceptance_goals031114`, loopback3339, run ID
`goals031114`, container task riêng trên WSL daemon, không mount DB dev. Ba luồng
Writing partial/Speaking completion/Vocabulary đồng thời chạy5 vòng với ba scope/
connection, shared release barrier: đủ counters,35 XP=20+5+10,1 streak,1 bonus và
outbox keys unique. Test nộp câu cuối stage02 vẫn chạy5 vòng. SQL trigger thật
chặn outbox/credit xác minh rollback và retry; fault-injection regression giữ
1213/1205/cancellation semantics. Không tuyên bố mọi lịch thread đã được vét cạn.

## Browser: đã quan sát và giới hạn

- Browser dùng HTML Razor thật từ TestWebApplicationFactory với user synthetic101,
  sau first completion hôm trước và replay hôm nay. Static loopback8893 phục vụ
  capture cùng assets hiện tại; **không phải phiên login/web live trên dev DB**.
  Route dashboard render là `/Home/Index`; `/` của app là landing, nên capture
  dashboard đã sửa dùng đúng route và assert `Hôm nay:10 XP` (có dấu cách thực tế).
- Goals/Profile cùng2 ngày; dashboard40 XP tổng/10 XP hôm nay/2 ngày/0% goal. Chỉ
  hai ngày có Accepted được bật; vocabulary chart0; Writing goal hôm nay0/1.
- Đo `innerWidth==scrollWidth`: Goals1440/768/390/375, Profile1440/768/390,
  dashboard1440/768/390/375. Đã xem screenshot desktop Goals và mobile dashboard;
  mỗi trang một h1. Reduced motion được bật trong lượt kiểm tra.
- Enter mở Goals modal, focus `goalTargetInput`; Escape đóng và trả về CTA.
  Text zoom200% trên Goals không tràn ngang viewport; chưa kiểm native browser
  zoom200%, mọi trạng thái modal/role, full live multi-user login/logout.
- Có quan sát nhãn Memory Strength cũ trên dashboard375 sát/chồng nhau. Không đổi
  CSS/layout khu vực này trong stage03; chuyển stage06 UI polish. Không gọi toàn
  bộ responsive/UI acceptance là đạt chỉ từ đo overflow.
- Chrome screenshot lưu đường dẫn D qua MCP bị workspace-root guard từ chối;
  screenshot đã xem dạng inline, không sửa trust/bypass để ghi file.

## Lệnh tái chạy chính

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~GoalsStage03RegressionTests|FullyQualifiedName~GoalsPhase1|FullyQualifiedName~GoalsPhase3|FullyQualifiedName~GoalsPhase6|FullyQualifiedName~GoalsDeadlock|FullyQualifiedName~GoalsCompletionRecovery|FullyQualifiedName~CompletionCreditAmbientTransaction|FullyQualifiedName~ProfileBadgeRead|FullyQualifiedName~LearningTransitionIntent|FullyQualifiedName~AccountFunctionalCompletion'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~GoalsStage03MariaDbTests|FullyQualifiedName~WritingCompletionMariaDbTests|FullyQualifiedName~MariaDbCompletionConcurrencyTests|FullyQualifiedName~MariaDbAccountReviewAcceptanceTests|FullyQualifiedName~MariaDbNotificationWorkerAcceptanceTests|FullyQualifiedName~WritingAcceptedAnswersMariaDbConcurrencyTests'
dotnet build TCTEnglish/TCTEnglish.csproj --no-restore
```

Lệnh provider yêu cầu acceptance connection/run environment trỏ DB disposable có
sentinel đúng. Không dùng connection production/dev. Các test paid/live OpenAI
được loại khỏi lượt acceptance chủ động; không gọi dịch vụ chấm trả phí.

Provider cuối đạt24/24; fixture reset bắt đầu sạch UserBadges trước từng case03.
Đã dừng acceptance container, WSL Docker daemon và capture server do lượt này tạo;
mount ext4 của fixture không còn sau khi daemon dừng. Giữ artifact/data test ignored trên D, không xóa
volume hoặc process của môi trường học hiện có.
Đã tự review toàn bộ diff của stage03 và bảo toàn phần stage01/02. Các kiểm tra
app cuối không còn lỗi đã biết thuộc scope03. Browser acceptance vẫn có các giới
hạn đã nêu. Chỉ local stage03; không có bằng chứng CI/staging/production ở lượt
này. Stage04 và các stage sau chưa được thực thi.
