# Stage 07 — Kết quả tích hợp Writing → Goals → huy hiệu → Profile

Ngày 14/11/2026. Nhánh `fix/writing-goals-profile-polish`, HEAD `c6367a0`.
Một agent điều tra, sửa, review và kiểm chứng. Giữ toàn bộ phần việc chưa commit
của stage 01–06. Không commit, push, PR, remote CI, merge, deploy hoặc gọi API trả phí.

**Kết quả:** đã sửa các lỗi trong phạm vi được tái hiện; các kiểm tra local đã
chạy đều có lượt cuối đạt. Ma trận còn **native browser zoom 200% chưa xác minh**;
live paid-provider bị bỏ qua theo quyền được cấp. Không gọi đây là nghiệm thu
100% mọi môi trường/thiết bị hoặc một bản đã phát hành.

## 1. Các lỗi đã sửa

| Lỗi tái hiện | Nguyên nhân và thay đổi | Bằng chứng hồi quy |
| --- | --- | --- |
| Badge filter thiếu Home/End; nhóm URL/storage cũ có thể ẩn mọi thẻ | Normalize về `all`, hỗ trợ Home/End và roving tabindex; giữ filter theo display group đã redacted | 2 case Node đỏ trước sửa, xanh sau sửa; browser End → `bi-an`, chỉ còn hai mystery cards |
| Writing lên 1/8 nhưng header vẫn “Chưa bắt đầu / 0 lượt gửi” | Hai nhãn chưa có JS target; normalizer bỏ tổng server. Bind status/attempt/completed totals từ response đã lưu, gồm tiến độ của tab khác | 2 case trạng thái đỏ trước sửa; revision/429/offline/draft/retry đạt; browser “Đang luyện / 1 lượt gửi / 1/8” → “Hoàn thành / 9 lượt gửi / 8/8” |
| Hai label Goals không có control tương ứng | `asp-for` sinh `for` theo ViewModel trong khi control dùng ID riêng cho JS. Gắn `for` đúng `goalAreaSelect` / `goalTargetInput` | Razor đỏ trước sửa; browser xác nhận cả hai accessible names và Escape trả focus về nút mở |
| Target 0 bị trình bày là hoàn thành 0%, 0/0 hoặc chưa đặt | Giữ target/history; hiển thị “Đang tạm dừng”, không render numeric progress cho target 0; all-paused nhất quán với Profile | 6 case render đỏ trước sửa, xanh sau sửa; browser không còn 0/0 hoặc câu hoàn thành 0% |
| Legend Memory Strength chen chữ ở 390 px | Ba cột quá hẹp trong nửa card. Dùng Bootstrap `col-12 col-sm-4`, giữ desktop | Browser đo hai nhãn giao nhau trước sửa, không giao nhau sau sửa ở 1440/768/390/375 |
| Practice có lịch sử làm trang rộng 422 px ở 375–390 px | Min-content của cột trái và dòng metadata không wrap. Cho cột co bằng min-width 0, wrap metadata với selector scoped | Browser 422 → 375/390; timestamp còn đọc được, không che tràn toàn trang bằng overflow hack |

Sản phẩm sửa trong stage 07: `TCTEnglish/wwwroot/js/{goals,writing}.js`,
`TCTEnglish/wwwroot/css/writing.css`, `TCTEnglish/Views/Goals/Index.cshtml`,
`Views/Study/WritingPractice.cshtml`, `Views/Home/Index.cshtml`.
Tests sửa/thêm: `GoalsUiRenderTests.cs`, `WritingUiRenderTests.cs`,
`WritingContentMariaDbAcceptanceTests.cs`, `test_goals_ui.cjs`,
`test_writing_draft_isolation.cjs`. Không sửa Program.cs, appsettings, project
file, shared layout/tokens, entity/mapping hoặc migration đã áp dụng.

## 2. Các lượt kiểm tra và kết quả chính xác

Artifact ignored: `.appdata/local/test-results/writing-goals-stage07/`.
Mỗi dòng là một lượt; không cộng các dòng thành tổng test duy nhất.

| Kiểm tra | Kết quả / receipt |
| --- | --- |
| Full .NET Release đầu | 1.565 pass / 3 fail / 49 skip; `stage07-full.trx` |
| Recheck các trường hợp liên quan 3 failure | 10 pass / 0 fail / 0 skip; `stage07-failure-recheck.trx` |
| Full rerun sau sửa header/filter | 1.569 pass / 0 fail / 50 skip; `stage07-full-final.trx` |
| Full cuối sau sửa hành vi Razor | **1.575 pass / 0 fail / 50 gated skip**, tổng 1.625; `stage07-release-final.trx` |
| Focused cuối, gồm 6 case target tạm dừng | **98 pass / 0 fail / 0 skip**; `stage07-final-ui-repair-green.trx` |
| Node Writing/Goals/Account/draft/status sau CSS cuối | **46 pass / 0 fail / 0 skip**; `node-reviewed-final.log` |
| Disposable MariaDB 10.5.23: Goals03/04, completion, concurrency, variants, account, worker | **29 pass / 0 fail / 0 skip**; `stage07-mariadb.trx` |
| Toàn bộ 108 package trong cùng recipient MariaDB mới | **1 pass / 0 fail / 0 skip**; `stage07-content-mariadb-final.trx` |
| Release build output riêng ở cuối | **0 errors, 42 project warnings**; `release-build.log` |
| EF pending-model | Clean; connection local chỉ qua child environment |
| Canonical Validate | 379 immutable releases / 17.904 record revisions |
| Canonical Sync 2 lượt | 0 record operations mỗi lượt, exit 0, receipt khớp catalog |
| Git boundary/immutable history | Pass; `immutable-history.log` |
| Diff whitespace/protected-file check | Pass; chỉ còn cảnh báo EOL thông thường của Git |

Full cuối chạy trước thay đổi CSS wrap history/whitespace cuối; sau đó đã build,
chạy Node và kiểm browser lại. Không có thay đổi C# hoặc hành vi Razor sau full
cuối. SDK10 vẫn build target **net9.0**, EF **9.0.4**, Pomelo **9.0.0**.

Ba failure đầu là timeout audio-import admission, Writing optional slow budget
5,612s vượt 5,5s và AI homework-refusal copy. Không nới assertion hoặc sửa AI
ngoài phạm vi. Isolated và hai full rerun đạt; chưa chứng minh root cause của
failure không ổn định ban đầu. Giữ nguyên receipt đầu, không gọi đó là bug đã sửa.
Test content mới từng fail do fixture thiếu PreviewText NOT NULL; đã bổ sung
Topic/PreviewText cho fixture, không đổi schema để né lỗi. Một lượt build gặp
file lock từ app do stage07 mở; chỉ dừng tiến trình đã xác minh thuộc stage07,
rồi chạy lại thành công. Không dừng workload của dự án khác.

50 skip trong full không phải 50 pass. Chúng gồm các case provider cần opt-in
và live/external acceptance; 29+1 provider liên quan đã chạy riêng như bảng trên.
Không chạy live OpenAI text/audio hoặc kiểm account/quota bằng request trả phí.

## 3. Schema, 108 bài và dữ liệu hiện có

- Stage07 không đổi persisted model nên không cần migration mới. Migration
  `20261114081306_AddBadgeTiersAndDiscoveryAwards` cùng Designer/snapshot của
  stage04 còn nguyên. Provider kiểm upgrade/repeat, 12 ID/code cũ, grants/progress/
  dates; collision dừng trước DDL và Down từ chối khi có earned history mới.
- Verify lại beginner/intermediate/advanced: **108 bài mới / 1.152 cặp song ngữ /
  108 variants**, đúng 6 bài mới mỗi ô trong 18 ô. Mọi manifest slot hoàn tất,
  không dùng draft làm quota. Mỗi import rerun tạo **0 lesson / 0 sentence /
  0 variant**, no-op36. Log `content-<level>-verify.log` và `content-<level>-import.log`.
- Snapshot multiset/hash giữ **42.409 dòng baseline trên 72 bảng**, đồng thời giữ
  43.165 dòng trước intermediate và 44.209 dòng trước advanced. Package cũ và
  production.json không đổi bytes. Kiểm preservation được thực hiện trước startup
  web cuối; jobs bình thường của web có thể cập nhật operational rows sau đó.
- Test provider mới nhập **cả 108 package vào một DB disposable**: exact metadata,
  bilingual/order/paragraph/variants, published/system ownership, 18 ô và assessment
  mọi reference/variant. Các hash dòng có sẵn, gồm bài private và accepted practice
  fixture, được giữ. Import repeat giữ nguyên hash toàn DB, không duplicate.
- Browser trên web dev thật đọc cả 18 ô: beginner/emails16, 17 ô còn lại6, tổng
  **118 public ID khác nhau = 10 cũ + 108 mới**. `catalog-browser.json` là receipt.
- Catalog SHA256: `9F3E56EA5EF4A96DFD09E9555C8FDD987F0D1CFCF6B11460A587F2F88C30198F`.
  `.appdata/local/shared-data/last-sync.json` hiện khớp hash này; launcher cuối cũng
  sync no-op. Không export users, progress, grants, credentials hoặc SQL dump.

Đây là kiểm chứng kỹ thuật và đối chiếu editorial đã có từ ba batch05, không
phải teacher review độc lập/chứng nhận CEFR. Bản dịch khác reference/approved
variant vẫn có thể NeedsReview theo local policy, không tự nhận pass/thưởng.

## 4. Ma trận luồng tích hợp đã quan sát

| Kịch bản | Bằng chứng và kết quả |
| --- | --- |
| Login/GET/target-only | UserB đăng nhập cookie thật vẫn XP0/streak0/grants0; GET và đổi target không mint reward |
| Partial + UTC+7 midnight | Clock fixture14/11 23:59:59: Accepted giữa bài →10XP/streak1/credit0. Sang00:00 ngày15, replay + hoàn thành →40XP tổng,30XP hôm nay,streak2/credit1/counter Writing1 |
| Final/reload/concurrent replay | Header/status phục hồi đúng; hai replay HTTP200/200 vẫn40XP/credit1. Provider kiểm concurrent first-final và different-final sentences, rollback/retry, durable intent uniqueness |
| Missed day/later replay | Bỏ ngày16, GET ngày17 hiển thị current0/longest2. Replay bài cũ →current1,50XP tổng,credit1 và counter Writing ngày mới0; 3 confirmed dates mở study-checkin |
| Units/paused/history | Writing13/10 và Speaking0/2 →50% ở Goals/Profile; target0 không đo. Target hôm nay đổi thành5 vẫn giữ ngày15 “Đã đạt mọi mục tiêu”; XP/credits không đổi |
| All ten badge boundaries | 30 regression cases dưới/đúng/trên ngưỡng cho 10 award; real Writing journey và provider29 kiểm grant persistence, concurrent catch-up, silent retroactive grants, outbox replay/rollback |
| Mystery locked → earned | Cookie fixtureA có12 completion thật qua service: sau7/8 câu bài13 vẫn không có secret code/rule; sau câu8 commit →13 credits/270XP, grant21, Goals/Profile hiện “Plot twist thứ 13”. Secret còn lại vẫn redacted |
| Profile/dashboard | Cùng streak business-date; Profile earned-only, đúng ngày UTC+7; dashboard đọc persisted XP, không suy từ số thẻ |
| Account switch + Back | A draft → logout thật → B login thật, B không thấy draft/grants/progress. Back về entry A đã đánh dấu vẫn ownerB/draft trống. A login lại còn draft riêng. B mở bài A đã hoàn thành có0 attempts/history rows/credits/XP |
| Errors/security | Browser controlled429/offline giữ draft, không false pass; retry đạt. Wrong-ID404 và missing-CSRF400 không ghi learning state. Private/unpublished/ownership/antiforgery còn được kiểm bằng application suite |
| Keyboard/modal | Home/End/arrows và một tab stop; modal Tab giữ focus; Escape trả về trigger/thẻ mystery earned sau hidden.bs.modal |

Các ngày mô phỏng dùng clock fixture, không đổi OS clock hay ghi giả lịch sử
người dùng dev. Fixture đầu dùng identity header; lượt sau dùng login/logout
cookie thật qua AccountController. Precondition12/13 bài được tạo bằng
IWritingService/Accepted transactions trong DB synthetic, không insert badge
hoặc credit giả. Optional providers bị thay/tắt trong fixture; workers fixture
không gửi ra ngoài. Các lifetime fixture khác nhau được dùng khi build lại.

## 5. Browser/resolution và giới hạn

Chrome DevTools viewport emulation, chờ550ms cho layout/chart resize, reduced
motion bật. Kiểm cả state có earned badges và completed Writing history.

| Route |1440|768|390|375|
| --- | --- | --- | --- | --- |
| /Goals |Pass|Pass|Pass|Pass|
| /Account/Profile |Pass|Pass|Pass|Pass|
| /Home/Index |Pass|Pass|Pass|Pass|
| /Home/Writing |Pass|Pass|Pass|Pass|
| /Home/Writing/Exercises |Pass|Pass|Pass|Pass|
| /Home/Writing/Practice |Pass|Pass|Pass|Pass|

Mỗi case cuối có một h1 và scrollWidth bằng innerWidth; legend không giao chữ,
history metadata wrap. Receipt: `responsive-final-reviewed.json`. Các log thử
`responsive.json`/`responsive-final.json` trước đó có minimum window500 hoặc
history overflow, không phải bằng chứng pass cuối. Chart có resize debounce;
ma trận đo sau khi ổn định, không phải chứng nhận không có mọi frame jank.

**Native200% zoom chưa xác minh:** 5 lần Control++ qua công cụ không đổi
innerWidth1440/DPR1; không lấy viewport/reduced-motion/CSS thay cho native zoom.
Người dùng còn cần kiểm trong Chrome thật ở200%. Screenshot trả trực tiếp qua
MCP đã được xem; không tuyên bố các yêu cầu pathD: tạo file PNG local thành công.

## 6. Web local và phạm vi phát hành

Web dùng launcher chuẩn, Release, `http://localhost:5114`; readiness200.
Writing cloud enhancement, grammar và local-LLM feedback được tắt bằng child
process environment cho phiên test này; không sửa appsettings/User Secrets.
Speech status:configured, gpt-transcribe, key có sẵn trong User Secrets,
**VerifiedLive=false**. Không gọi paid provider hoặc khởi động Whisper.

Giữ web5114 để chủ dự án kiểm thủ công; fixture8897/8898 được dừng khi bàn giao.
Không có commit/push/PR/remote CI/staging/production hoặc thay release gate.

## 7. Thứ tự manual test cho chủ dự án

Dùng web5114, hai tài khoản test local mới A/B, ví dụ địa chỉ @example.invalid
và mật khẩu tự chọn. Giữ các tài khoản/dữ liệu học chính. ID25 dưới đây đã đối
chiếu từ dev catalog hiện tại; máy nhận khác mở link từ card thay vì giả định ID.

1. **Đăng nhập và trạng thái gốc:** `/Account/Register`, `/Account/Login`, rồi
   `/Goals`, `/Account/Profile`, `/Home/Index`. Tài khoản mới chưa học có0XP,
   streak0 và empty earned badges; xem trang/refresh không tạo thưởng.
2. **Đặt chỉ tiêu:** tại `/Goals`, mở modal, chọn Writing=1. Hai label liên kết
   đúng control; Tab đến được input, Escape trả về nút mở. Save không tăng XP/streak.
3. **Chọn bài:** `/Home/Writing?level=beginner` →
   `/Home/Writing/Exercises?level=beginner&contentType=emails`. Lọc chủ đề Thư viện,
   mở “Hỏi giờ mở cửa thư viện”,8 câu. Đổi cấp độ/thể loại có đúng catalog tương ứng;
   beginner/emails16 bài, các ô khác6 trong dev hiện tại.
4. **Draft/hint/resume:** `/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=25`.
   Gõ nháp, xem gợi ý, refresh. Draft của A còn; hint/typing không có XP/streak.
   Câu đầu tham chiếu: `Dear librarian, I would like to visit the library this Saturday.`
5. **Accepted giữa bài:** gửi đúng câu đầu. Header thành Đang luyện, lượt gửi
   tăng đúng server và1/8. Refresh phục hồi. Với tài khoản mới, ngày đầu có10XP,
   streak1, Writing vẫn0/1 vì bài chưa hoàn thành; chưa có lifetime completion credit.
6. **Nộp cuối đồng thời:** làm đến7/8, mở cùng bài trong hai tab A, gửi câu cuối
   hợp lệ ở cả hai. Cả hai phục hồi8/8/Hoàn thành, chỉ cộng một20XP completion.
   Nếu không có hoạt động khác: tổng30XP ngày đầu, mục tiêu Writing1/1 và một
   huy hiệu Writing đầu tiên; nhiều accepted attempts vẫn là lịch sử hợp lệ.
7. **Replay:** reload và gửi lại câu đã đạt trong cùng ngày UTC+7. Không cộng
   thêm20XP, completion count hoặc grant. Luyện lại ngày sau có thể nhận bonus
   streak10XP một lần/ngày, vẫn không thành bài Writing mới.
8. **Mixed skills/pause:** thêm Speaking=2 tại `/Goals`. Writing đạt100%, Speaking0%
   cho tổng50%, Profile cùng50%. Speaking về0 → chỉ Writing được đo. Writing
   cũng về0 → cả Goals/Profile báo tạm dừng, không0/0/false completion. Đặt lại1
   không tạo thêm XP hoặc viết lại ngày đã kết thúc.
9. **Badges:** ở `/Goals`, dùng filter/Home/End/arrows và mở modal bằng Enter.
   Locked mystery không lộ rule/name/code thật trong DOM; Escape trả focus về thẻ.
   Hoàn thành3 bài khác nhau mở writing-mood; nếu tiếp tục13 bài khác nhau,
   mystery đầu chỉ reveal sau final commit của bài13. Replay không mở lần nữa.
10. **Profile:** `/Account/Profile` có đúng earned badges, group/date UTC+7 và
    cùng streak/goal với `/Goals`/dashboard. Empty accountB không hiện fake awards.
11. **Đổi tài khoản:** A để một draft chưa gửi ở bài khác, logout, loginB trong
    cùng browser. B không thấy draft/progress/recent history của A; Back cũng
    không phục hồi dữ liệu A. Login lại A còn draft riêng và tiến độ đã lưu.
12. **Failure/retry:** Offline trước khi gửi một câu mới, rồi Online và retry.
    UI báo lỗi, giữ draft và cho gửi lại; không hiện Accepted giả. HTTP429 phải
    có trạng thái chờ/retry. Wrong-ID read404; POST thiếu antiforgery400 khi đã login.
13. **Clock/history/threshold dài:** dùng regression clock-controlled dưới đây
    hoặc quan sát ngày thật; không đổi đồng hồ Windows hay sửa history dev để
    tạo badge demo. Ngày thiếu làm current về0 nhưng giữ longest/grants; thay
    target hôm nay không làm ngày trước mất trạng thái đã đạt.
14. **Thiết bị/zoom:** xem6 route ở1440/768/390/375, đặc biệt practice có history.
    Dùng Chrome native200% kiểm chữ/control/modal và focus. Đây là phần native
    zoom còn cần chủ dự án xác nhận, không phải check đã pass qua MCP.

```powershell
# Deterministic fee-free clock/rule regression, using disposable test state:
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~GoalsStage03RegressionTests|FullyQualifiedName~GoalsStage04RegressionTests'
```

Các route Writing dùng prefix `/Home`, dù controller tên Study. Không dùng ví
dụ `/Study/WritingExercises` hoặc `/Study/EvaluateWritingSentence` đã được sửa
trong contract03/báo cáo06. CI/staging/production vẫn chưa được nghiệm thu/phát
hành trong nhiệm vụ này.
