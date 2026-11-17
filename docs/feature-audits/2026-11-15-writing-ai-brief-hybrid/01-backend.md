# Biên bản bước 01 — Writing từ mô tả và fallback nội bộ

Ngày15/11/2026. Triển khai local trên `fix/writing-goals-profile-polish`.
Một agent điều tra, triển khai, tự review và kiểm chứng trực tiếp.
Recent-history đã có biên bản hoàn tất trước khi sửa các file Writing dùng chung.

## Kết quả và ranh giới

Đã thêm sáng tác thực sự từ `from_brief` qua OpenAI hiện có; luồng
`sourceText`/`from_text` cũ được giữ. Prompt mô tả yêu cầu viết bài hoàn chỉnh
rồi căn chỉnh câu Việt/Anh, kiểm các lựa chọn bắt buộc mà không dùng enum
fallback cũ để che kết quả sai. Service đã đăng ký hiện tại được mở rộng;
không sửa Program.cs, appsettings, csproj, SDK/provider hoặc global AI settings.

Fallback là catalog/generator thuần C# với7 intent ×3 cấp độ, gồm cả6 dạng bài.
Bản1 chỉ hỗ trợ bài ngắn7 câu và đúng register được công bố cho từng khung.
Không có API AI thứ hai, Ollama hay model tải về. Việc nội bộ soạn bài không
gọi mạng; lần OpenAI thất bại trước đó có thể đã phát sinh usage.

Receipt bền vững thay cache/lock trong bộ nhớ làm ranh giới correctness.
Unique user/key, hash payload, lease/fence, transaction khóa user ngắn, reservation
và settlement dùng chung cho hai mode/hai engine. Không giữ transaction khi gọi
OpenAI. Recovery, cancel và confirm kiểm owner thật; các POST có antiforgery.
Quota vẫn là5 bài thành công/ngày UTC cho Premium; Admin giữ chính sách cũ.

Exercise riêng/chưa xuất bản, câu có thứ tự, receipt, provenance, log thành công
và quota được lưu trong cùng transaction. Snapshot kiểm người dùng/reward/progress
và số recent visits trước/sau creation giữ nguyên. Sau đó regression còn dùng
practice/assessment thật để xác nhận bài template tiếp tục luyện được.

## File và hợp đồng bàn giao

- `WritingService.GenerationOperations.cs`: claim/recovery/fencing/quota/settlement,
  input/brief prompt và validation. `WritingService.Generation.cs` giữ helper
  source conversion/parse/normalization, bỏ cache và semaphore làm correctness.
- `Services/Writing/Generation/WritingTemplateCatalog.cs`: catalog version1,
  block Việt/Anh, capabilities, preview chưa điền và slot validation/rendering.
- `WritingGenerationOperation`, partial DbContext mapping và migration mới:
  private receipt + nullable origin, giữ nguyên migration badge/recent hiện có.
- `StudyController`, `IWritingService`, generation ViewModels: route cũ và các
  route capabilities/status/confirm/cancel mới. Card/practice origin được chiếu
  qua ViewModels hiện tại, không trả EF entity.
- `AccountDeletionService`: xóa receipt trong luồng xóa/ẩn danh thật đang khóa
  account; không giữ raw brief/source/slots trong receipt.
- `OpenAiProviderClient`/request options: dùng chung usage callback cho Writing
  text-only và chặn resend sau dispatched outcome chưa chắc chắn. Giữ transport,
  outage guard, model, body/deadline/retry cap hiện tại, không thêm outer retry.
- `WritingBrief*Tests.cs`: HTTP, domain/template, quota, fault/lease recovery,
  account/access và provider/multiple-process evidence.

Hợp đồng chính xác cho bước02 ở [backend-contract.md](backend-contract.md).
Offer trả200 với `success:false`, không phải đã tạo bài. Chỉ `succeeded` mới
được chuyển sang practice. `awaiting_template` cần hiển thị preview, slots,
những phần bị bỏ và nhận xác nhận; `running`/`recovery_required` cần giữ key và
phục hồi operation cũ. Label origin phải phân biệt OpenAI và khung song ngữ.

## Review ngôn ngữ 21 profile

Agent trực tiếp đọc toàn bộ147 cặp câu gốc trong source, so nghĩa hai vế, đại từ,
trình tự, thì động từ, từ nối, định dạng và độ tăng của cấu trúc câu theo cấp độ.
Đây là review biên tập nội bộ, chưa phải chứng nhận CEFR hay review của giáo viên
độc lập. Bảy câu/template gồm cả lời chào nếu có; không được mô tả là7 đoạn văn.

| Intent | Beginner | Intermediate | Advanced | Điểm đã review |
|---|---|---|---|---|
| leave-request | reviewed | reviewed | reviewed | Lịch nghỉ, lý do choice dịch riêng, bàn giao, lời đề nghị; tên là proper-name, ngày định dạng riêng theo ngôn ngữ |
| meeting-follow-up | reviewed | reviewed | reviewed | Cùng cuộc họp giả định, thử nghiệm nhỏ, trách nhiệm trước thứ Sáu và phản hồi; không tự tạo nội dung cuộc họp thật |
| study-reflection | reviewed | reviewed | reviewed | Cùng tiến trình đọc–gặp từ khó–ghi chú–đọc lại–kế hoạch; thì quá khứ và dự định không lệch nghĩa |
| transport-opinion | reviewed | reviewed | reviewed | Lập luận lợi ích/hạn chế/độ tin cậy; không đưa số liệu hoặc sự kiện hiện thời cần tra cứu |
| study-habit | reviewed | reviewed | reviewed | Thói quen10 phút, chuẩn bị tài liệu, tóm tắt và quay lại sau gián đoạn; không hứa hiệu quả chắc chắn |
| weekend-story | reviewed | reviewed | reviewed | Linh, khu vườn, cây đổ, nhờ người làm vườn, dựng cây/tưới nước và kết thúc; thứ tự nhân quả nhất quán |
| community-cleanup | reviewed | reviewed | reviewed |10 tình nguyện viên và kết quả đều giả định, phân loại rác, vấn đề thùng rác, đề xuất; advanced nói rõ chưa chứng minh hiệu quả dài hạn |

Regression render cả21 profile,7 cặp hoàn chỉnh/không trùng, paragraph boundary,
không còn placeholder sau điền. Cả3 lý do cho phép được source quản lý song ngữ;
regression từ chối lý do tự do, markup, ngày sai/đảo thứ tự, slot dư và length ngoài
capability. Keyword chỉ gợi ý khung tương thích, không chứng minh hiểu mô tả tự do.

## Migration và bằng chứng provider

`20261115010152_AddWritingGenerationOperations` là migration additive: bảng mới,
5 cột nullable trên `WritingExercises`, unique user/key, index reservation/expiry,
FK user cascade và exercise SET NULL. Không sửa applied history, không tạo baseline
giả, không chạy Down. Down chỉ đảo schema mới; không phải phương án rollback dữ
liệu đã sinh. Badge/recent Designer/migration ban đầu giữ nguyên byte hash.

MariaDB thực tế là image `mariadb:10.5.23`, container riêng
`tctenglish-mariadb-acceptance-writing-brief-1115`, loopback3320,
`lower_case_table_names=1`. Database có prefix `tct_acceptance_` và sentinel/run
được guard kiểm trước thao tác. Credential chỉ ở file ignored; không nằm trong
source/argv/log biên bản. Không reset Docker dev hoặc database cũ.

Scenario cuối trên DB mới `tct_acceptance_writing_brief_1115c` đã **pass1/1,
0 fail/skip**; đây là một test orchestration chứa các kiểm sau:

1. Upgrade từ migration recent hiện tại; chèn user sentinel, lưu snapshot toàn
   bộ giá trị reward/users và metadata các bài cũ; migrate latest rồi repeat.
   Snapshot giữ nguyên, origin cũ null, không pending migration/model.
2. Index unique user/key, reservation và cả2 FK thật tồn tại. Xóa bài chưa luyện
   giữ receipt FK null và recovery trả `result_deleted`.
3. Failure có known10/unknown upper1000 → offer → một template riêng → replay
   không gọi provider thêm. Kiểm owner, practice và assessment qua service thật.
4.9 scopes/connection độc lập tranh quota: đúng5 thành công,4 quota-exceeded,
   5 provider dispatch,5 bài/log, không reservation sót.
5.2 source-mode OpenAI +3 brief/template dùng hết cùng quota5; lượt6 không gọi
   provider. Xóa thật/ẩn danh qua AccountDeletionService xóa receipt/bài riêng.
6. Fault trước Save và sau DB commit acknowledgement: recovery không gửi lại AI,
   cuối cùng đúng1 bài và1 success log. Known provider usage vẫn còn trước retry.
7. Cancel khi provider đang chờ: response muộn không xuất bản. Xóa account mới
   trong lúc provider đã dispatch: response muộn không tái tạo receipt/bài.
8. Hai **tiến trình .NET kiểm thử** đồng thời cùng key; tiến trình thứ3 sau khi
   hai tiến trình đầu đã thoát. Cùng operation, đúng1 provider-call marker,
   đúng1 receipt/bài/log. Không dùng agent hay tiến trình AI khác.

Lượt provider đầu trên DB1115 pass phạm vi ít hơn. Lượt mở rộng1115b dừng vì
fixture chọn account có lớp chung: guard xóa từ chối đúng. Sửa fixture sang user
tổng hợp mới, không sửa/nới guard ứng dụng; chạy lại toàn bộ trên DB1115c mới.
Không downgrade/reset một DB đã upgrade để tạo bằng chứng mới.

## Bằng chứng local và giới hạn

Log/TRX, marker subprocess, cấu hình DB tổng hợp và preservation receipt nằm
trong ignored `.tmp/writing-brief/`; không chứa dữ liệu thật để đưa vào Git.

- Red trước triển khai:2 fail đúng hành vi mới, brief trả400 và key/payload khác
  chưa conflict; `brief-red.trx`.
- Lượt focused liên quan: **128 pass/0 fail/8 gated skip**, `brief-related.trx`.
  Bao gồm Writing generation/private, recent, assessment, completion và transport.
- Node Writing UI/recent/draft/assessment/Goals: **44 pass/0 fail/0 skip**.
- MariaDB scenario cuối: **1 pass/0 fail/0 skip**, `brief-provider-final.trx`.
- EF9.0.4 repo-local: `No changes have been made to the model since the last migration.`
- Application Release build cuối: **0 warning/0 error**; test Release build:
  **14 warnings/0 errors** ở infrastructure/test cũ, không warning mới của feature.
- Full-suite cuối: **1.625 pass/0 fail/53 skip**, tổng1.678 trong7m13s;
  `brief-full.trx`. Không tính skip là pass. Các paid/live flags được tắt rõ
  khi chạy; MariaDB của feature được kiểm riêng bằng scenario opt-in ở trên.
- Sau full, chỉ đổi test cancel từ timer200ms sang đợi provider-entered rồi
  cancel để tránh phụ thuộc tải máy; recheck riêng **1 pass/0 fail**,
  `brief-cancel-final.trx`. Application source giống bản full và provider cuối.

Đã so SHA256 của109 file shared-content thuộc baseline dirty/untracked: không
file nào thay đổi.108 package hiện có, catalog và production manifest không được
chỉnh bởi bước này. Toàn bộ thay đổi Goals, badge, recent-history và các workload
khác được giữ; các file dùng chung chỉ nhận phần bổ sung cho feature này.
Receipt `.tmp/writing-brief/final-preservation.json` ghi rõ4 file migration/
Designer badge/recent cũ giữ nguyên hash và production manifest giống HEAD.
Full test export email preview ghi lại11 file vốn clean chỉ khác line-ending;
đã so nội dung chuẩn hóa bằng nhau và phục hồi BOM/CRLF của checkout trên đúng
11 file đó. Không phục hồi/reset/stash bất kỳ thay đổi có sẵn nào của chủ repo.

Không chạy web mới, không apply migration vào Docker dev và không restart web
hiện hữu. Các kiểm HTTP dùng test host; browser/UI của bước02 chưa được quan sát.
Full fake-provider pass không xác minh chất lượng sáng tác/dịch của model live.
Medium/long, tone khác, chủ đề ngoài7 intent và chi tiết ngoài slots không có
fallback nội bộ. UI chưa dùng API mới; không gọi API trả phí, commit, push, CI,
staging hay production.
