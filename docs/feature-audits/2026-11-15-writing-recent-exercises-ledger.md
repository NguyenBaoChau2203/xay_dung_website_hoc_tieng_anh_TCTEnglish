# Lịch sử bài luyện Writing cá nhân — kiểm chứng local 15/11/2026

Nhánh thực tế: `fix/writing-goals-profile-polish`. Một agent thực hiện toàn bộ.
Giữ nguyên các thay đổi Writing/Goals/badge/UI và 108 gói nội dung có trước.
Không commit, push, CI từ xa, staging, production, thay release gate hoặc gọi API trả phí.

## Kết quả triển khai

`/Home/Writing/Exercises` có mục **Luyện viết gần đây**, ngay dưới header,
độc lập bộ lọc và phân trang. Server render thẻ bằng ViewModel; khách không có
mục cá nhân hoặc request API cá nhân. Làm mới dùng API no-store, cùng projection.
Mỗi link dùng level/contentType/ID của chính bài; không mang topic/status của list.
Nội dung thẻ được Razor encode hoặc dựng bằng textContent, không dùng innerHTML.

`UserWritingExerciseVisits` lưu khóa chính `(UserId, WritingExerciseId)`,
`LastOpenedAtUtc datetime(6)`, index user/thời gian/ID và hai FK cascade.
Migration `20261114191845_AddUserWritingExerciseVisits`, Designer và snapshot
được sinh bằng EF repo-local 9.0.4. Timestamp tên migration là UTC; ngày bàn giao
theo UTC+7 là 15/11. Up chỉ tạo bảng/index, không backfill hoặc sửa dòng cũ.
Down chỉ bỏ bảng mới, có nghĩa là mất lịch sử truy cập nếu sau này chủ động rollback;
không chạy Down trong nhiệm vụ này.

Service dùng LearningBusinessClock, giữ timestamp lớn nhất tại độ chính xác
microsecond. Khóa dòng user qua UserMutationScope trước kiểm tra quyền và ghi;
AccountDeletionService dùng cùng khóa và xóa visit thật cả khi xóa lẫn ẩn danh.
Sau nhánh first-insert luôn conditional UPDATE lại, kể cả có INSERT chen vào
giữa UPDATE và kiểm tra tồn tại. Chỉ phục hồi lỗi unique đúng entity; lỗi DB khác
được giữ nguyên. Quyền dùng chung predicate với practice, kiểm tra trạng thái
tài khoản và giữ ngoại lệ Admin hiện hữu. Public thường phải published; private
của đúng chủ Premium/Admin không bắt buộc published. Loại bài rỗng trước Take(6).
Projection chỉ đọc metadata/count của current user; không tải đáp án/feedback.

API trên StudyController dùng absolute route:

- GET `/api/writing/recent`: authorize, no-store, `{success, ownerUserId, data}`.
- POST `/api/writing/{exerciseId}/recent`: authorize + antiforgery, no-store,
  user/time lấy từ server, trả 404 nếu không có quyền.

GET practice/data/hint/catalog không ghi visit. Editor phát `writing:initialized`
sau parse payload và render thành công; module riêng `writing-recent.js` ghi POST
có keepalive và timeout, không phụ thuộc form AI Premium. Không gọi evaluation
hoặc dịch vụ thưởng khi ghi visit. Lỗi đọc recent ban đầu vẫn cho catalog render
với trạng thái lỗi và nút retry. Lỗi POST báo chưa lưu, không báo thành công giả.

Pagehide/blur/visibility-hidden che dữ liệu cá nhân trước snapshot. Khi activate,
kiểm tra lại owner; practice kiểm tra thêm quyền hiện tại qua GET data. Dedupe
focus/visibility, bỏ response cũ, chặn HTML login redirect. Practice từ persisted
pageshow reload để nhận đúng user/progress; không dùng cached đáp án của tài khoản cũ.
Lịch sử và bài đã gửi ở DB; nháp chưa gửi vẫn ở localStorage theo owner/exercise/
sentence, chỉ trong trình duyệt tương ứng, không phải backup cloud.

## Bằng chứng và phạm vi

Biên bản lệnh/log ở thư mục ignored `.tmp/writing-recent*`; backup DB và cấu hình
fixture cũng ignored. Không đưa credential, dump hoặc learner data vào Git.
Lượt test đầu dùng output mặc định bị EXE của web cũ khóa; agent giữ nguyên web
cũ PID18540 và chuyển Release output sang `bin/RecentRelease/net9.0`, sau đó
`bin/RecentFinal/net9.0`. Một lượt artifacts-path bị fixture tính content-root
theo độ sâu thư mục cũ; không sửa fixture để che lỗi, dùng output cùng độ sâu.
Các lỗi setup này không được tính là baseline failing-feature test.

Regression focus/blur đã chạy đỏ trước sửa: `writing-recent-focus-before.log`.
Trường hợp không có visibilitychange không che nội dung khi blur. Bổ sung
blur/focus activation và regression dedupe; bộ Node cuối **42 pass / 0 fail**.
MCP chuyển target/tab không phát native blur/focus/visibilitychange trong phiên
đo (`events=[]`, hasFocus vẫn true); do đó kiểm account-switch activation bằng
event được phát có kiểm soát trong browser và Node, không gọi đây là bằng chứng
native tab-focus. Native Back có `navigation.type=back_forward`; Chrome báo
notRestoredReasons `response-cache-control-no-store`, `websocket-used-with-ccns`
và `masked`, nên không có restored bfcache trong phiên này. Sau event blur/focus thực thi trên DOM thật: nội dung bị che
trước GET, owner mismatch dẫn tới reload và chỉ hiện lịch sử của user mới.

Lượt .NET liên quan ban đầu **98 pass / 0 fail**. Lượt mở rộng có một assertion
test chưa decode chuỗi Razor tiếng Việt; sửa bằng HtmlDecode. Recheck sau đó
**10 pass / 0 fail**, gồm 9 test Writing mới và test AI từng fail trong full suite.
Kết quả full/provider cuối cập nhật ở phần cuối biên bản này.

Lượt full suite đầu: **1581 pass / 1 fail / 51 skip**, tổng1633.
Lỗi duy nhất `AiProductionClassifierIntegrationTests.Send_WithProductionClassifier_UsesWebsiteGuideRoutesForQuickActions`
không có `/Home/Contact` trong fallback answer khi chạy chung. Test này pass khi
chạy lại riêng cùng 9 Writing tests; không sửa classifier hay nới assertion.
Các skip gồm paid/live/provider không được cấu hình; không tính skip là pass.

## MariaDB và dữ liệu local

Container riêng `tctenglish-mariadb-acceptance-writing-recent-1115`, image
`mariadb:10.5.23`, loopback3319, sentinel `writing_recent_1115`,
lower_case_table_names=1. Mỗi lượt upgrade có DB mới hậu tố1115/1115b/1115c;
không downgrade/reset database cũ. Guard kiểm target/sentinel trước thao tác.
Upgrade từ migration badge hiện tại, repeat, pending-model, index/FK, dữ liệu
cũ, access/order/isolation và concurrency được thực thi trên MariaDB thật.
Lượt1115b/1115c gồm visit chạy đồng thời với cả xóa thật và ẩn danh qua
IAccountDeletionService. 12 scope độc lập tranh first insert giữ đúng một dòng
và max controlled-clock timestamp; request timestamp cũ không ghi đè timestamp mới.
Lượt provider đầu và các lượt review trước phần mở rộng race đều pass; không dùng
SQLite để thay thế chứng nhận provider. Race UPDATE→INSERT→Any có regression
relational riêng; kết quả bản mở rộng chạy cả MariaDB ghi ở cuối biên bản.

Trước apply vào Docker dev đã lưu full SQL backup ignored
`.appdata/local/backups/writing-recent-before.sql`. Sau migration/startup, trước
tạo tài khoản UAT, so toàn bộ INSERT groups ngoài migration history/bảng mới:
**68/68 nhóm bằng nhau từng ký tự**. Receipt `local-preservation.json`.
Startup shared-data kiểm379 release, **0 record operations**; không sửa package,
catalog hoặc production.json. Sau đó chỉ thêm tài khoản/tiến độ tổng hợp UAT local.

Web mới dùng launcher chuẩn, Debug, `http://localhost:5115`; readiness200.
CloudEnhancementEnabled, GrammarEnabled, LocalLlmEnabled đều false bằng env của
child process; không sửa appsettings/Program/csproj/User Secrets. Chỉ restart web
do agent vừa tạo; giữ tiến trình web cũ. Speech status configured, gpt-transcribe,
VerifiedLive=false; không gọi speech API hoặc khởi động ASR.

## Ma trận R01–R20

| ID | Bằng chứng thực tế / giới hạn |
|---|---|
| R01 | HTTP guest không section/request; account mới có empty thật; browser B empty trước visit. |
| R02 | Browser mở1 không gửi → Back có1, attempts0/status not-started; regression HTTP không sinh progress. |
| R03 | Browser1→121→1 ra1,121; service/relational kiểm một dòng mỗi cặp. |
| R04 | Browser B mở1..6 và121 bằng đúng metadata:121,6,5,4,3,2; service kiểm stable ties và unique. ID7 với metadata beginner không hợp lệ trả404, không được tính visit. |
| R05 | Browser list intermediate/reports/topic missing/completed/page99 vẫn có1,121; service kiểm myPage nữa. |
| R06 | Thẻ từ advanced/stories mở đúng beginner/emails1; actual URL không có topic/status cũ. |
| R07 | Browser local-exact Accepted1/14, ResumeSentenceId2; mở qua recent khôi phục câu2 và nháp câu2 của A. |
| R08 | Browser context độc lập loginA thấy history1,121 và progress1/14/resume2, input nháp trống. |
| R09 | Browser B cùng profile: không nháp/progressA, Back thấy dữ liệuB; simulated blur/focus trên DOM thật đổi owner an toàn. Native bfcache/focus chưa quan sát được qua MCP. |
| R10 | Service SQLite và MariaDB: private đúng chủ Premium unpublished được, foreign-private/ordinary unpublished bị từ chối; Admin deliberate exception được và progress vẫn riêng. |
| R11 | Downgrade, blocked, bài rỗng/unpublished/foreign-private bị loại trước limit; FK cascade exercise deletion trên MariaDB. |
| R12 | Cleanup thật delete/anonymize; MariaDB race cleanup/visit không sót visit; không dùng delete giả trong test. |
| R13 | MariaDB12 scopes/controlled clock/max timestamp; relational exact UPDATE→competing INSERT→existence regression; genuine failure không bị nuốt. |
| R14 | Missing/bad CSRF400 và ID không truy cập404 cũng được kiểm trực tiếp trong browser; client user/time không quyết định dữ liệu, guest401; existing GET không ghi. |
| R15 | Snapshot toàn bộ giá trị Users/attempts/progress/daily/goals/badges/credits/outbox/notifications trước-sau visit bằng nhau, kể cả khi có accepted/completed progress cũ. |
| R16 | Native Back của lần mở bài đã ghi được; offline recent-read có lỗi thật/retry thành công; Node POST fail giữ editor/draft. Instant close trước initialization/request đến server vẫn best-effort. |
| R17 | Node production module không AI form, pagehide/pageshow, stale responses, focus/visibility overlap, owner mismatch và HTML redirect đều kiểm; browser native Back có recent mới. |
| R18 | Hostile/long title encode và safe metadata URL trong regression; completed CTA Luyện lại/current-user progress; browser Tab từ Làm mới đến link đầu, outline solid; loading/empty/error/retry có kiểm. |
| R19 | Browser1440/1366/768/390 có6 cards, document.scrollWidth=innerWidth,0 phần tử recent tràn; reduced-motion=reduce; đã xem ảnh laptop1366. Native200% zoom chưa xác minh. |
| R20 | MariaDB10.5.23 upgrade/repeat/data/index/FK/concurrency/cleanup thực thi; EF pending model được kiểm riêng. |

Khi không xác nhận được identity sau reactivation do offline, practice tạm che
editor và cho retry để tránh lộ tài khoản cũ; nháp vẫn giữ nguyên. POST tracking
fail lúc editor đang hoạt động không chặn soạn thảo. Không hứa phục hồi instant
close hoặc đồng bộ nháp chưa gửi giữa thiết bị.

## Thứ tự kiểm thủ công

1. Mở list bằng khách rồi đăng nhập tài khoản test mới: chỉ tài khoản có empty recent.
2. MởA, gõ nháp nhưng chưa gửi, Back: thẻA có trạng thái chưa bắt đầu.
3. MởB ở cấp độ/dạng khác, rồi mởA: thứ tựA,B, không trùng.
4. Đổi filter/page sang catalog khác, mởA từ recent: đúng bài và nháp trong browser.
5. Gửi một câu bằng local assessment, mở lại: đúng tiến độ/resume; browser khác
   chỉ nhận tiến độ server, không có nháp chưa gửi.
6. Logout/loginB, Back, đổi tab, đóng/mở lại: không có progress/draftA.
7. Offline rồi Làm mới: lỗi thật; Online retry phục hồi. Keyboard,390px và Chrome
   native200% cần xem lại bằng cửa sổ người dùng thật, đặc biệt native bfcache/focus.

## Kết quả cuối

- Full suite cuối: **1.584 pass / 0 fail / 51 skip**, tổng1.635,
  `writing-recent-full-final.trx`, thời gian5m56s. Test classifier từng fail
  không tái hiện trong lượt riêng và lượt full cuối; không tuyên bố đã sửa root cause.
- Node cuối: **42 pass / 0 fail**, gồm11 lifecycle recent và31 test Writing draft/UI/Goals.
- Repo-local EF: `No changes have been made to the model since the last migration.`
- Release build cuối: **0 errors**,14 warnings thuộc test infrastructure/các test
  cũ; incremental application build không phát warning mới của feature.
- Lượt cuối trên MariaDB DB mới hậu tố1115d: **10 pass / 0 fail / 0 skip**,
  gồm9 recent regressions SQLite/HTTP và1 scenario MariaDB đầy đủ. Scenario này
  thật sự chèn INSERT cùng transaction ngay sau UPDATE0 và trước AnyAsync trên
  provider MariaDB, rồi kiểm đúng timestamp mới. Có cả12 concurrent scopes/max
  clock, account cleanup races, upgrade/repeat/index/FK/cascade/preservation.
  Log `writing-recent-provider-final-exact.log`. Thay đổi cuối trước lượt này
  chỉ mở rộng interceptor regression; application source giống full suite cuối.
- Full suite có test export email preview ghi lại11 file chỉ khác line ending.
  Đã kiểm diff không có thay đổi nội dung và phục hồi BOM/CRLF trên đúng11 file vốn
  clean lúc bắt đầu; chúng không còn xuất hiện trong Git status.
- Web bàn giao5115 đã restart bằng launcher chuẩn sau các thay đổi C# cuối;
  `/health/ready`200. Giữ web cũ18540; cấu hình các engine tùy chọn vẫn tắt bằng
  child environment. Không có bước phát hành nào được thực hiện.
