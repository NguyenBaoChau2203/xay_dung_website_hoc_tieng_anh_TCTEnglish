# PR140 — review và sửa trước khi hợp master

Phạm vi được chủ dự án cho phép: review PR của `hoanganh-279`, sửa lỗi/xung
đột, commit/push vào nhánh PR hiện có, merge master và đưa workspace về master
bằng `scripts/pull-local.ps1`. Một Codex agent thực hiện trực tiếp. Báo cáo này
ghi bằng chứng local tại thời điểm chuẩn bị PR; kết quả CI/merge/staging được
ghi trong PR và bàn giao sau khi quan sát, không suy ra từ kiểm thử local.

Nguồn đối chiếu: member head `c3836c3`, master `c641c3e`. Nhánh member chậm
178 commit; ghép master tạo 30 đường dẫn xung đột. Giữ nguyên commit của thành
viên và lịch sử master. Không reset/stash/force push, đổi secrets, chạy AI/SMTP
live, thay gate production hoặc đồng bộ dữ liệu riêng lên hosting.

## Findings và bản sửa

| Mức | Finding đã xác nhận | Sửa và bằng chứng hồi quy |
| --- | --- | --- |
| P1 | Migration thu hẹp `ReadingPassages.Title/Topic/Level` có thể lỗi sau khi đã thêm cột. Ca MariaDB với Title 501 ký tự để lại `Users.ReadingLevel` trước sửa. | Thêm migration kiểm tra `20261109091315_GuardLegacyReadingLengths` trước migration Reading cũ. Ca thật xác nhận dừng trước DDL/history mới; sau sửa dữ liệu fixture hợp lệ, nâng cấp và chạy lại thành công. Không sửa migration đã có. |
| P1 | Lookup và Notebook đọc từ vựng của bài chưa Published/vượt CEFR hoặc ghép passage/vocabulary khác nhau. | Kiểm tra user, publication, CEFR và quan hệ nguồn trong service; thiếu/không có quyền cùng trả 404. Các ca controller gọi service thật đã đỏ trước sửa. |
| P1 | SaveAnswer nhận question/option thuộc bài khác, không kiểm tra lại khi bài bị thu hồi. | Kiểm tra attempt owner, nguồn hiện còn truy cập được, question thuộc passage và option thuộc question. HTTP giữ auth/antiforgery. |
| P1 | Submit lưu completion trước Goals; lỗi ghi thưởng để lại trạng thái hoàn thành, gửi lại không lấy được kết quả, có nguy cơ cộng lặp đồng thời. | Dùng `UserMutationScope`/transaction của master; Goals phải trả Success; retry trả score đã lưu. SQLite và MariaDB xác nhận rollback; hai context MariaDB gửi đồng thời chỉ ghi một completion credit. |
| P1 | `MatchKey` xuất hiện trong learner DTO/DOM và JSON đáp án đã lưu của định dạng PR ban đầu. | Learner chỉ nhận map option ID; chấm bằng key riêng ở server. Đọc được đáp án legacy mà không trả key về trình duyệt; kiểm tra ID, cặp thừa, payload lỗi và đáp án sai. |
| P1 | Daily Challenge không lưu cờ hoàn thành cùng XP; code nhánh cũ còn cộng streak riêng. | Giữ một lần ghi learning của master, thêm cờ trong cùng user transaction, dùng clock ngày Việt Nam. Ca replay, ngày clock giả lập và lỗi sau ghi thưởng đã tái hiện trước sửa. |
| P1 | PR viết lại package `team.onboarding/1`; SHA khai báo của v2 cũng không khớp tệp. Adapter mới bắt buộc các trường Reading mà v1 chưa có. | Phục hồi chính xác bytes v1 trên master; giữ v2 và sửa SHA của phiên bản chưa phát hành. Các trường mới có default tương thích, ref evidence mới có thể vắng trong bản cũ. Validate 380 release bất biến. |
| P1 | Khi v2 thêm trường optional, engine so row với hình dạng incoming và gắn nhầm local-edit; local launcher đã dừng tại `team.onboarding.reading`. | So với đúng record có digest đang được binding, rồi cập nhật baseline qua từng phiên bản trong batch. Ca không sửa local nâng cấp được; ca sửa local thật vẫn dừng và giữ dữ liệu. Launcher sync đã thành công qua cơ chế chuẩn. |
| P2 | CSV Notebook chứa công thức bảng tính. | Quote CSV và thêm apostrophe cho prefix công thức/control; ca `=1+1` và tab + `@SUM` đã đỏ trước sửa. |
| P2 | ZIP giải nén toàn bộ ảnh vào RAM trước kiểm tra; XLSX mở workbook trước giới hạn tài nguyên và đọc DB đồng bộ. | Giới hạn ảnh 5MB, ZIP 200 entry/50MB mở rộng, XLSX 10MB nén/64MB mở rộng, 16MB/entry và giới hạn row/cell; copy có chặn, truyền cancellation, DB async. Ca ZIP 6MB và XLSX 65MB đã đỏ trước sửa. |
| P2 | Xóa bài Published/có lịch sử làm mất dữ liệu learner. Service Archive có sẵn nhưng thiếu route và thao tác UI. | Chặn hard delete đối với bài đã công bố/có history/attempt/translation. Nối Archive vào controller Admin POST + antiforgery và menu; HTTP test xác nhận ẩn bài nhưng giữ score/history. |
| P2 | Excel bỏ qua evidence paragraph, export dùng database ID nên không mang được sang máy khác. | Dùng ParagraphNo portable, resolve vào ID của passage đích và kiểm tra thuộc bài. Ca đích đã có paragraph ID 9901 vẫn import/export đúng. Chỉ đổi ViewModel/service, không thêm persisted field. |
| P2 | Assist không dùng rate limiter chung; lỗi thiếu key hướng dẫn người học chạy script máy chủ. | Áp limiter trước service, giới hạn một provider attempt/input/output, trả thông báo phù hợp người học. Test limiter từ chối trước khi gọi service. Không gọi provider thật. |
| P2 | Dashboard mới mất Goals/XP của master, tự suy ngày học bằng streak modulo; shared loading mất spinner. | Giữ bố cục member, dùng StudyDays/XP/Goals/clock thật của master và spinner trạng thái Loading. Bốn regression tích hợp cũ đã báo lỗi; được sửa và kiểm lại. |
| P2 | Create/Edit Reading gọi jquery.validate trong Admin layout không tải jQuery; browser xác nhận ReferenceError. | Áp mẫu sửa đã ghi trong bug log 16/11: bỏ partial không hoạt động, thêm native required và giữ server validation. HTTP regression và browser sau restart xác nhận không còn lỗi này. |

## Quyết định xung đột và phạm vi dùng chung

- Giữ Landing night, Listening/Speaking đã hoàn thiện, account recovery, admin
  shell, chat grounding/streaming và security mutation của master; không chép
  lại bản cũ từ nhánh chậm 178 commit. Dashboard learner/Admin và Reading là
  phần triển khai của member được tích hợp.
- DbContext/csproj/StudyController/registry giữ cả phần Reading mới và các trường,
  dependency, service contract đang có trên master. `Program.cs` chỉ thêm bốn
  đăng ký Reading của PR; dependency mới là ClosedXML 0.104.2 của PR.
- Avatar Tico được nối đồng nhất vào chat shell, launcher/header và JS fallback;
  cập nhật fixture theo asset có thật, giữ các assertion về IDs, theme, motion,
  nguồn, submit và streaming. Asset SVG cũ vẫn tồn tại.
- `_StateMessage` phục vụ các trang learner/Admin; `_Layout`, `layout.css`,
  `tct-learning-ui.css` và image-upload partial ảnh hưởng shell/trạng thái chung.
  Đã so với master, giữ hooks/IDs của JS. Kiểm thử toàn ứng dụng và các route
  dưới đây là bằng chứng có giới hạn, không thay cho UAT mọi module.
- Hai skill UI trỏ lại mục canonical trong DESIGN, tránh link docs không nằm
  trong inventory tooling. Quy tắc delegation/security/Git/release giữ nguyên;
  chỉ Antigravity + Gemini 3.8 Flash High có ngoại lệ đã được chủ dự án ban hành.

## Dữ liệu và migration

Source hiện có 26 migration: 22 của master, ba migration Reading/Daily Challenge
của member và một guard mới. Guard được đặt ID ngay trước Reading schema để
kiểm tra trước mọi DDL của migration đó, kể cả checkout đã áp các migration
master có ngày muộn hơn. EF tự áp migration và ghi history; không tạo history
giả, không sửa migration member đã có. Guard chỉ đọc độ dài và SIGNAL khi không
an toàn; không truncate, backfill mất dữ liệu, tạo routine, DROP hay reset DB.
Ngữ pháp anonymous block dùng khả năng
[BEGIN NOT ATOMIC của MariaDB](https://mariadb.com/docs/server/reference/sql-statements/programmatic-compound-statements/using-compound-statements-outside-of-stored-programs).

Fixture MariaDB 10.5.23 riêng có sentinel, tên/run/loopback được xác minh. Đã
áp đúng 22 migration master, seed user/password/security version, reading,
question/option/history, daily XP và private Writing; thử chặn title quá dài;
nâng cấp 26 và chạy lại; kiểm tra dữ liệu cũ, rollback Goals và submit đồng thời.
Không thao tác fixture lên Docker dev/hosting. Đọc trực tiếp MySQL staging trước
deploy không kết nối được; guard và pipeline sẽ dừng nếu dữ liệu không đạt,
không suy rằng các giá trị staging đã được kiểm tra bằng truy vấn đó.

ViewModel Notebook/evidence và các sửa service không đòi thêm schema. Không
xuất users, khóa, chat, thanh toán hay tiến độ cá nhân. Gói v2 có typed adapters
ReadingParagraph/ReadingVocabularyItem và test đủ 21 loại nội dung; v1/media đã
phát hành được giữ nguyên. Cập nhật workflow dữ liệu cho hai loại con mới.

## Kiểm chứng đã quan sát

- Full .NET lần cuối trước phần chỉnh UI nhỏ/guard: **3.161 pass, 54 skip,
  3.215 total**, không fail. Đây không phải số của một lần full chạy lại sau
  mọi chỉnh sửa cuối; CI trên head cuối là kiểm chứng full tiếp theo.
- Bộ focused sau các sửa: **130 pass, 2 skip / 132**; hai HTTP test native
  validation/Archive: **2 pass**. Các lượt này có phần trùng nhau, không cộng
  thành tổng độc lập. Kết quả targeted cuối được ghi thêm sau khi kết thúc.
- MariaDB guard/upgrade/repeat/preservation/concurrency: **1 fact pass**, gồm
  nhiều assertion; EF không có pending model change tại bước kiểm tra model.
- Node: **498 pass / 498**. Python tooling: **150 pass, 3 skip / 153**.
- Native hook launch qua cả pwsh và cmd.exe: pass cấu hình bốn client/bốn event,
  model gate và malformed input. Đây là script/contract checks, không chứng minh
  một IDE khác đã tự tải hook hay trust record.
- `agent_tooling.py` pass; package immutability guard so với master pass;
  shared-data validate **380 releases / 17.908 record revisions**. NuGet audit
  không báo vulnerable package từ các nguồn cấu hình tại thời điểm chạy.
- Local launcher chuẩn ở 5100; `/health/ready` 200 `ready`; key Speech chỉ kiểm
  tra cấu hình, không thử live. Các ca yêu cầu live AI/SMTP không được bật.

## Bằng chứng browser và giới hạn

Chrome thực tế, một tài khoản Admin local đã có sẵn, qua shell learner và Admin.
Ma trận **30 lượt**: 10 route ở 1440, 768, 390 px; không tràn ngang, không ảnh
hỏng trong các lượt đó. Route: Home Index/Reading, Reading Study/Notebook/Progress,
Admin Dashboard, ReadingManagement Index/Create/Edit/Import. Dashboard còn được
kiểm tra sidebar mở/đóng. Giảm chuyển động được bật bằng media emulation.

Đã chọn đáp án Reading và reload: vẫn option 129, cùng attempt 1. Download
template và POST ImportPreview trả 200, có bài mẫu và form Confirm; không bấm
Confirm để tạo thêm học liệu trên DB dev. Form Create sau sửa có Title/Content
required, empty form không hợp lệ và không còn lỗi console jQuery. Tab đi tới
skip link. Không gửi câu hỏi AI hoặc audio trong browser.

Không xác nhận native browser zoom 200%: tổ hợp Ctrl++ qua công cụ không đổi
DPR/viewport, nên không ghi pass. Browser dùng Admin, chưa đăng nhập Standard/
Premium riêng; role/ownership/CEFR và antiforgery được kiểm bằng test. Không có
bài Matching được tạo thêm trên DB dev; reload/chấm matching/legacy format được
kiểm bằng regression. Không gọi AI/SMTP live hay xác minh account/quota cloud.
Archive được kiểm trên HTTP fixture có dữ liệu riêng; không archive bài thật
trên Docker dev. CI/staging/production có trạng thái riêng trong bàn giao.
