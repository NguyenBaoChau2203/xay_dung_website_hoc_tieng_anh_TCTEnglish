# Stage05b — intermediate36 local content delivery

Ngày14/11/2026, nhánh `fix/writing-goals-profile-polish`. Một agent soạn, rà và
kiểm chứng tại local; dùng lại manifest/importer/receipts và giữ dirty work của
các stage trước. Không commit, push, PR, CI, deploy hoặc gọi API AI trả phí.

## Kết quả

Hoàn tất36 bài public intermediate mới:6 emails,6 diaries,6 essays,6 articles,
6 stories và6 reports. Mỗi bài12 cặp Việt–Anh có thứ tự, ba đoạn và1 đáp án thay
thế được rà; tổng432 cặp/36 biến thể/5.629 từ tiếng Anh,145–174 từ mỗi bài.
Metadata, mục đích giao tiếp và văn phong phân biệt rõ sáu dạng bài. Sáu báo cáo
ghi rõ tình huống và số liệu giả định, không quy cho nghiên cứu có thật.

Đã dùng đúng36 khóa intermediate được reserved từ đợt beginner, không đổi danh
tính, tạo lại baseline hay đếm bài cũ vào quota. Manifest hiện72 complete/36
reserved; advanced chưa có file nội dung và dành cho chat riêng. Public catalog
tăng46→82; lũy kế72 bài mới trên mục tiêu108. Xem [matrix](05-content-matrix.md).

Giữ nguyên22 ID Writing ban đầu,36 bài beginner,11 bài riêng và1 bài hệ thống
chưa công bố. Hash multiset từng dòng giữ nguyên42.409 dòng ban đầu/72 bảng và
43.165 dòng của mốc trước intermediate. Bao gồm users, bài làm, tiến độ, huy hiệu,
credits và nội dung/câu/đáp án beginner. Không gửi bài làm vào tài khoản dev thật
hoặc tự tạo XP/credit/badge.307 package version trước intermediate và
`production.json` giữ nguyên bytes; cả36 package beginner còn nguyên.

## Nội dung, công cụ và gói

- `data/writing/2026-11-14/intermediate.json`: nguồn soạn36 bài; manifest và
  `intermediate-receipt.json` ghi hash, trạng thái và kiểm chứng công khai, không
  ID local hoặc dữ liệu người học. Beginner JSON/receipt được giữ nguyên.
- `scripts/writing-content.cs`: giữ importer hiện có; bổ sung mốc trước từng level
  ở ignored `<level>-before.json`, kiểm chứng cả baseline ban đầu lẫn mốc mới,
  và đối chiếu trùng với các level đã có. Không sửa evaluator, UI, entity,
  DbContext, Program.cs, appsettings hay project file; không cần migration mới.
- `TCTEnglish.Tests/WritingContentBatchTests.cs`: chạy lại cả hai level cho
  completeness/assessment, package privacy và six-format practice/completion;
  thêm kiểm tra danh tính/hash manifest và trùng câu/bài xuyên batch.
-36 gói canonical version1 tại
  `data/shared/packages/team.writing.20261114.intermediate.*/1/package.json`.
  Mỗi gói có1 WritingExercise,12 sentences,1 WritingAcceptedAnswer; không
  dependency, delete hoặc media. Chỉ có alias `owner:system` cho người duyệt;
  không xuất user/email/ID người duyệt, private lesson, progress hoặc rewards.
  Catalog307→343 releases,17.400 record revisions.

Các snapshot, receipt ID, candidate/catalog backup và log riêng nằm trong ignored
`.appdata/local/writing-content-20261114/` và `.appdata/local/writing-intermediate-*.log`.
Không thay hoặc xóa baseline/receipt cũ. Public receipt không thay canonical
package; importer và exporter chuẩn vẫn là đường nhập/xuất dữ liệu.

## Nguồn và rà ngôn ngữ

Tự soạn và rà mọi432 cặp/36 biến thể: chủ thể, thời gian/thì, phủ định, số lượng,
mức độ yêu cầu, văn phong, nghĩa tương đương và liên kết đoạn. Lượt rà cuối làm
rõ chỉ dẫn bàn giao, sửa cách diễn đạt về cuốn sách mua tháng trước/phản ánh ở
lớp gốm và loại bỏ thay đổi quan hệ nguyên nhân ở một biến thể truyện; tất cả
được sửa trước import. [Editorial review](05-intermediate-editorial-review.md)
liệt kê36 bài, mục đích và word count.

Đã mở [EnglishDatpmt Writing](https://english.datpmt.com/writing),
[British Council B1 overview](https://learnenglish.britishcouncil.org/free-resources/writing/b1)
và [B2 overview](https://learnenglish.britishcouncil.org/free-resources/writing/b2)
ngày14/11/2026. Chỉ tham khảo format/mục đích/độ liên kết; không lấy lesson body,
không bypass đăng nhập, không sao chép bộ bài không có quyền tái sử dụng. Không
thêm taxonomy A1–C2 hoặc khẳng định chứng nhận CEFR. Đây là rà của chính agent
soạn bài, chưa có rà độc lập bởi giáo viên.

Không có full reference sentence trùng sau chuẩn hóa trong432 câu của batch
hoặc720 câu của hai đợt. Max Jaccard shingle5 từ0.003534 nội bộ intermediate và
0.009346 so beginner; kiểm tra importer so với catalog cũ/các level trước cũng
đạt. Threshold từ chối0.75 không đổi. Chỉ số trùng/độ dài và việc reference
đạt assessment không tự chứng minh tính đúng ngữ nghĩa; agent đã rà riêng.

## Kiểm chứng thực tế

| Kiểm tra | Kết quả |
| --- | --- |
| Baseline intermediate | Read-only snapshot trước import; giữ baseline gốc;46 public/307 releases/43.165 dòng |
| Canonical Status trước export | Không local-edit hoặc conflict; giữ mapping hiện có |
| Validator |36 lessons/432 pairs/36 variants; đúng quota/order/paragraph/length; reference và alternatives đạt local policy |
| Dry-run | Sẽ thêm36, hiện có0; không ghi DB |
| Six-format pilot tests trước dev import | Cả beginner/intermediate; public HTTP catalog, order, variants, progress, completion/replay; cloud tắt |
| Dev import | Một transaction thêm36 bài/432 câu/36 variants |
| Dev import rerun | Thêm0/0/0;36 no-op |
| Local pilot command | Chạy sau full import,0 tạo/6 no-op; không báo sai thành6 pilot mới rồi30 bài |
| Canonical export | Tổng36 immutable v1;17 thành công,1 candidate được phục hồi,18 còn lại xuất tiếp |
| Export rerun |0 version mới/36 có sẵn |
| Canonical Validate |343 releases/17.400 record revisions hợp lệ |
| Sync1 rồi Sync2 |504 record operations rồi0; hai receipt cùng catalog SHA-256 hiện tại, last-sync cập nhật |
| Fresh provider roundtrip | MariaDB10.5.23, migration→36 packages→36 lessons/432 pairs/36 variants; text/order/paragraph/answers khớp; reference/variant assessment đạt |
| Roundtrip repeat |0 changed rows theo hash toàn bộ DB; chỉ tạo1 system owner bị khóa trong DB disposable |
| Verify intermediate sau Sync/export rerun |18 ô đúng quota;42.409 dòng gốc và43.165 dòng trước intermediate nguyên vẹn;307 package cũ và production.json nguyên bytes |
| Reverify beginner sau intermediate |36/288/36 vẫn khớp receipt và persisted content; quota toàn bộ hai đợt đúng |
| Focused application tests cuối |109 pass/0 fail/0 skip; application/test build thành công |
| Final manifest/package recheck sau khi ghi complete |3 pass/0 fail/0 skip; validator36/432/36 đạt với hash manifest cuối |
| `git diff --check` | Pass; chỉ cảnh báo CRLF→LF ở các tệp bug memory có sẵn |

Lệnh test cuối:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~WritingContentBatchTests|FullyQualifiedName~WritingStage02RegressionTests|FullyQualifiedName~WritingLocalAssessmentTests|FullyQualifiedName~WritingCompletionTransactionTests|FullyQualifiedName~SharedDataPackageTests|FullyQualifiedName~SharedDataLifecycleTests|FullyQualifiedName~WritingAcceptedAnswersIntegrationTests' --logger 'console;verbosity=minimal'
```

Các lệnh nội dung dùng `scripts/writing-content.ps1 -Action <action> -Level intermediate`.
Canonical Status/Validate/Sync dùng `scripts/shared-data.ps1` với `-DatabaseReady
-SkipBuild -Configuration Debug`, sử dụng application binary đã build trong test.
Không khởi động web/worker/speech hay ordinary-startup seeder.

Roundtrip dùng container mới `tct-writing-20261114-intermediate-roundtrip`,
loopback3316, DB `tct_acceptance_writing_intermediate_20261114`, không mount/copy
dev volume. Credential ngẫu nhiên chỉ qua environment, không argv/log/Git.
Container được dừng sau kiểm chứng; Docker dev/workload khác được giữ nguyên.

## Phục hồi export và giới hạn

Windows từ chối `File.Move(..., overwrite:true)` khi exporter thay catalog sau
gói thứ18; DB binding đã commit, package v1 và `.catalog-export.tmp` còn nguyên.
Chưa xác định tiến trình giữ file; không kết luận lỗi quyền/ACL cố định. Đã lưu
bản catalog trước phục hồi trong ignored state, kiểm tra candidate chỉ thêm đúng
release `intermediate.essays.06` và SHA-256 của mọi package, rồi hoàn tất rename
đúng bước exporter. Resume kiểm tra binding/content và bỏ qua18 gói có sẵn;
không xóa package, không sửa bytes, không tạo version thay thế. Validate, Sync,
provider roundtrip và re-export sau đó đều đạt. Không sửa exporter dùng chung
hoặc quyền/trust của hệ thống để xử lý sự cố tạm thời này.

Chưa nghiệm thu giao diện live, keyboard/native zoom hoặc luồng đăng nhập bằng
browser trong batch nội dung này. Build/test còn warnings nullable/analyzer ở
tệp có sẵn, không build error. Không chạy CI/staging/production hoặc paid calls.
Không có blocker còn lại cho intermediate. Advanced36 chưa bắt đầu; lũy kế
**72/108**, public hiện82 bài. Nội dung công khai đã đủ về số lượng cho các mốc
13/25/50 bài khác nhau, nhưng không tự cấp thưởng hoặc hạ ngưỡng.
