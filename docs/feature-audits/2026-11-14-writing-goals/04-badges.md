# Stage 04 — Local implementation and evidence

Ngày 14/11/2026. Hoàn thành local trên `fix/writing-goals-profile-polish`, HEAD
`c6367a00269bc23749a9a02a7d26bcc89001fac8`. Giữ các thay đổi chưa commit stage01–03.
Một agent điều tra, triển khai, review và verify. Không commit/push/PR/remote CI,
deploy, paid call hoặc sửa Program.cs/appsettings/project files.

## Outcome and scope

Đã thêm10 server-earned awards đúng tên/code/ngưỡng README; đủ22 reference badges.
Metadata persisted `Badge.DifficultyKey`, `Badge.IsSecret`,
`UserBadge.IsRetroactive`. Giữ nguyên12 ID/code/enum1–7 và earned history. Enum8/9
tách distinct Writing/confirmed longest khỏi aggregate legacy.

| Legacy IDs / codes | Rule unchanged | New difficulty |
| --- | --- | --- |
|1 `first-session` |1 active date |chill |
|2 `three-day-streak` |current streak3 |cuon |
|3 `seven-day-peak` |longest streak7 |cuon |
|4 `active-week` |7 active dates |cuon |
|5 `xp-collector` |50 XP |chill |
|6 `xp-champion` |200 XP |cuon |
|7 `speaking-first-video` |1 speaking video |chill |
|8 `speaking-five-videos` |5 speaking videos |cuon |
|9 `vocabulary-first-mastered` |1 vocabulary completion |chill |
|10 `vocabulary-ten-mastered` |10 vocabulary completions |cuon |
|11 `writing-first-exercise` |1 legacy Writing completion |chill |
|12 `writing-five-exercises` |5 legacy Writing completions |cuon |

Goals/Profile dùng projection chung; locked mystery không có identity/rule/numeric
progress. Hai next-hint predicates dùng `CanRecommend`; gần-unlock worker không
chọn secrets. Không thay CSS/shared layout hoặc UI trước đó. Frontend contract:
[`04-badges-contract.md`](04-badges-contract.md).

Natural earning lưu grant + durable outbox cùng transaction; existing direct
delivery và dispatcher cùng event key. Completion replay không mint thêm credit,
XP hoặc badge intent. Explicit catch-up silent, đánh dấu retroactive, read GET
không làm reconciliation. Atomic account mutation có bounded full-unit deadlock
retry khi sở hữu transaction; ambient errors vẫn trả về owner rollback.

## Diagnosis and source review

- CodeGraph trước các đường Goals/Badge/DbContext/credit/notification/worker; đọc
  trực tiếp các đoạn bị cắt và source đang sửa. Không init/rebuild index.
- Dùng hợp đồng03, bug memory mới nhất và skills superpowers/engineering/EF,
  ASP.NET data/services; UI component/review cho predicate và rendered checks.
- Baseline02 tests fail: catalog12 thay vì22; unknown metric threshold0 và valid
  metric negative threshold vẫn grant. Đã chặn invalid/unsupported rules.
- Run đầu95 tests:94 pass/1 fail ở legacy daily schema thiếu area columns.
  Nguyên nhân badge intent đọc full goal counters. Badge-only enqueue bỏ đọc
  counters; broad final bao gồm legacy schema đã pass.
- Provider run đầu:3 pass/2 fail. Một sentinel fixture thiếu PasswordHash được
  sửa ở test. Một deadlock thật khi hai users đồng thời insert grants vào unique
  index gaps: user lock riêng chưa đủ; sửa retry toàn owned unit sau rollback,
  không nuốt lỗi hoặc retry insert lẻ. Sau sửa17/17; final29/29.

## Migration/provider proof

Migration `20261114081306_AddBadgeTiersAndDiscoveryAwards`, generated bằng repo
EF9.0.4; kèm Designer và snapshot. App net9, EF9.0.4, Pomelo9.0.0; provider
`10.5.23-MariaDB-1:10.5.23+maria~ubu2004`.

- Up: thêm nullable varchar30 difficulty, bool visibility defaultfalse, bool
  retroactive defaultfalse; update metadata12 cũ và insert IDs13–22 bằng HasData.
- Guard trước DDL: legacy identity mismatch, occupied reserved ID hoặc duplicate
  new code đều SIGNAL và dừng; không overwrite/adopt custom catalog. Test3 loại
  conflict chứng minh rows giữ nguyên, cột DifficultyKey chưa được thêm, migration
  history chưa ghi mới. Guard dùng MariaDB SQL, đã chạy trên đúng provider.
- Down: refuse trước mọi mutation nếu có grant IDs13–22, tránh cascade mất earned
  history. Không chạy downgrade local thật. Không sửa applied migrations/history.
- Disposable acceptance riêng `tctenglish-mariadb-acceptance-badges041114`,
  loopback3340, label/run/sentinel đúng guard. Upgrade scenarios tạo DB riêng có
  UUID sau kiểm identity, migrate từ immediate previous migration, không downgrade
  fixture đã dùng. Giữ sentinel user, badge3/date, daily XP/counters/streak; repeat
  `MigrateAsync` không thêm dữ liệu. Có earned21 thì Down phải từ chối.
- Pending model check sạch bằng provider model và repo-local EF command.
- Badges không phải shared content allowlist; không SQL dump, không exporter mới,
  không sửa production manifest/content package.

## Automated checks actually run

Artifacts ignored: `.appdata/local/test-results/goals-stage04/`.

| Check | Result / artifact |
| --- | --- |
| Baseline meaningful regression |2 fail, `stage04-red.trx` |
| Initial focused app |94 pass/1 fail, `stage04-initial.trx` |
| Focused review after compatibility correction |114 pass/2 provider skip, `stage04-reviewed.trx` |
| Broad Goals/Writing/completion/Profile/notification/account |**361 pass,0 fail,24 gated skip**, `stage04-final.trx` |
| Initial MariaDB after retry/fixture correction |17 pass,0 fail,0 skip, `stage04-mariadb-reviewed.trx` |
| Final MariaDB including account/worker/answers regressions |**29 pass,0 fail,0 skip**, `stage04-mariadb-final.trx` |
| Existing Goals/Writing Node suites |25 pass,0 fail,0 skip, `node-final.log` |
| Application build --no-restore |pass,0 warnings/0 errors on final incremental build, `build-final.log` |
| EF has-pending-model-changes |no model changes, `pending-model.log` |
| Git diff --check |pass |

New regression coverage:30 boundary cases (−1/exact/+1 cho mỗi award), dry-run,
apply/replay, reload, isolation, old awards/date retention, unsupported/invalid
rules, unknown tier, duplicated legacy/canonical completion keys, future evidence,
empty/XP-only days, stored longest không đủ confirmed dates, Profile earned-only,
locked serialized fields + actual Razor HTML. Worker/direct-secret refusal được
kiểm riêng. Natural journey tạo50 synthetic published one-sentence lessons, chạy
50 first completions qua WritingService và29 ngày replay:50 lifetime credits,
50 completion counters,1300 XP, đủ10 awards/unique intents. Broken streak về1
không revoke grants. Không tạo50 lesson vào Docker dev/content packages.

Provider concurrency:6 natural learning requests +6 catch-up requests của hai
users, đủ10 grants/user; natural có một intent/badge, catch-up silent. Drain
dispatcher hai lượt vẫn một notification/key. Trigger outbox failure rollback
grant/activity/streak; retry thành công một lần. Các regression03 chạy lại gồm
three-feature concurrent practice5 rounds, replay/midnight/credit/outbox failure.

## Existing local users: actual dry-run and bounded apply

Docker local trước migration:63 users,12 badges,42 grants; schema ở
`20261113125249_AddSpeakingVideoVisits`. Chỉ áp dụng migration mới. So sánh exact
old grants `Id/UserID/BadgeID/AwardedAt` và aggregates user/streak/dailyXP/counters/
credits trước/sau bằng ignored TSV; không thay đổi.

1. Explicit bounded scope:63 current non-anonymized local user IDs, dưới limit100.
   Runner chỉ đọc connection qua process environment từ local store; không in key,
   không argv credential, không host/web/provider startup.
2. Dry-run receipt:7 missing eligible awards, gồm5 badge14 và2 badge16. So sánh
   preservation sau preview không đổi. Receipts tối thiểu chỉUserId/BadgeIds/DryRun,
   ignored `.appdata/local/badge-reconciliation/`.
3. Apply re-evaluate dưới user locks:7 retroactive grants, total49. Không backdate,
   không XP/study-history rewrite, không revoke42 grants cũ.
4. Rerun dry-run/apply:0 candidates/0 inserts. Exact old grant comparison và XP/
   activity/streak/credit aggregates vẫn khớp. Query notification/outbox theo
   keys7 retroactive awards:0/0. Không có notification flood.

File-based runner SDK10 ban đầu chọn transitive Relational9.0.0 khác app9.0.4;
đã pin9.0.4 trong runner, không đổi app csproj. Lượt lỗi dừng trước data operation;
reviewed dry-run/apply/replay exit0. Runner không auto-apply migrations.

## Rendered acceptance and limits

- Chrome xem actual `/Goals` Razor capture từ isolated synthetic test host, phục
  vụ loopback8894 cùng assets hiện tại; **không phải login/live dev-DB session**.
- DOM22 cards,2 secret generic; `data-badge-code` empty, tooltip generic. Không có
  code/tên/rule thật trong DOM; next hints không chọn hidden cards. Xem screenshot
  khu vực badges; không đổi thiết kế trước đó.
- Width1440/768/390/375: `innerWidth==scrollWidth`. Resize window375 ban đầu bị
  Chrome minimum500; sau đó dùng viewport emulation và đo đúng375/390, không báo
  nhầm500 là mobile acceptance.
- Reduced motion bật. Enter mở editor/focus `goalTargetInput`, Escape đóng/trả CTA.
  CSS text zoom200% tại1440 không tràn ngang. Native browser zoom200%, full live
  login/roles, earned-secret visual/detail và tier filters final vẫn cho stage06.
- Local catalog chỉ10 public Writing lessons. Engine có real earning rule cho
  mốc cao hơn, được chứng minh qua50 bài synthetic và Admin authoring boundary;
  stage05 cung cấp108 bài mới để learner có thêm nội dung. Không thay scope04
  bằng content import, không tự hạ threshold hoặc claim có50 public lessons.
- Không có bằng chứng CI/staging/production trong lượt này. Không chạy paid
  acceptance. Các skip của broad suite không được gọi là passed.

## Main changed files

Models `Badge.cs`, `UserBadge.cs`, mapping trong `DbflashcardContext.cs`; migration/
Designer/snapshot; `GoalsService.cs`, `BadgeProjection.cs`,
`BadgeReconciliationResult.cs`, `UserMutationScope.cs`,
`LearningNotificationIntentWriter.cs`, `NotificationService.cs`,
`Workers/NotificationWorker.cs`, `ViewModels/GoalsViewModel.cs`; chỉ hai next-hint
predicates của `Views/Goals/Index.cshtml`. Thêm `GoalsStage04RegressionTests.cs`,
`GoalsStage04MariaDbTests.cs`, `scripts/reconcile-badges.cs` và hai tài liệu04.
Đã review diff inline; các diff chung với stage03 vẫn được giữ nguyên.

Final local evidence is complete for stage04. Stage05 content and stage06 final
visual binding remain separate authorized stages.

Đã dừng capture server8894 và acceptance container do stage04 tạo. Giữ nguyên
Docker dev3307 và các workload khác; không dừng Docker daemon/WSL hoặc xóa volume.
Capture server chỉ phục vụ HTML/assets, nên các background SignalR/API requests
của layout không phải live acceptance và có lỗi404/501 như dự kiến; không suy
ra chat/notification realtime đã được nghiệm thu từ capture này.
