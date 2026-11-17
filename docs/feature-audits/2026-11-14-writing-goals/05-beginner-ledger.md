# Stage05a — beginner36 local content delivery

Ngày14/11/2026; nhánh `fix/writing-goals-profile-polish`. Một agent trực tiếp soạn,
rà, triển khai và kiểm chứng. Giữ nguyên dirty work của các stage trước. Quyền
thực hiện chỉ local: không commit/push/PR/CI/deploy, không gọi API AI trả phí.

## Kết quả và dữ liệu

- Hoàn tất36 bài public mới:6 emails,6 diaries,6 essays,6 articles,6 stories,6
  reports. Mỗi bài8 cặp Việt–Anh có thứ tự, hai đoạn, metadata và1 đáp án thay thế
  có nghĩa tương đương; tổng288 cặp/36 biến thể,72–92 từ tiếng Anh/bài.
- Baseline có22 bài:10 public beginner/email,11 private,1 system chưa công bố.
  Sau nhập có46 public; mỗi format beginner tăng đúng6. Intermediate/advanced
  không tăng. Xem toàn bộ18 ô ở [matrix](05-content-matrix.md).
- Manifest108 slot ổn định;36 beginner complete,72 slot còn lại reserved.
  Chưa tạo nội dung intermediate hoặc advanced. Không tính bài cũ vào quota.
- 36 gói canonical version1, mỗi gói chỉ có1 WritingExercise,8 sentences,1
  WritingAcceptedAnswer. Chỉ xuất alias `owner:system`, không ID/email người duyệt.
  Không users, private lessons, attempts, progress, rewards, payment hoặc media.
  Catalog tăng271→307 phiên bản, tổng16.896 record revisions.
- Giữ nguyên toàn bộ42.409 dòng gốc trên72 bảng bằng đối chiếu hash multiset từng
  dòng, gồm22 Writing ID cũ/câu/đáp án, tài khoản, tiến độ, bài làm, huy hiệu và
  credits.271 package version cũ và production.json giữ nguyên bytes/hash.

## Tệp và khả năng tiếp tục

`data/writing/2026-11-14/` giữ authoring JSON, manifest, baseline inventory chỉ có
số liệu tổng, README và receipt công khai đã loại ID riêng. Canonical packages ở
`data/shared/packages/team.writing.20261114.beginner.*/1/package.json` và catalog.

`WritingContentBatch.cs` định nghĩa kiểu và kiểm tra completeness/quota/order/
paragraph/duplicates/variants cùng assessment hiện tại. Công cụ tường minh
`scripts/writing-content.{cs,ps1}` có baseline, validate, dry-run, pilot, import,
export, verify, roundtrip. Không đăng ký startup, không sửa Program.cs/appsettings/
project file. Không đổi entity/mapping nên batch này không cần migration mới.

Import dùng một transaction/batch và cùng MariaDB advisory lock với shared-data.
Receipt stage trước commit rồi công bố bằng rename; nếu gián đoạn, chạy lại chỉ
phục hồi unique exact-content của batch. Không tái sử dụng22 ID baseline, không
cập nhật nội dung cũ. Hash/receipt/content conflict sẽ dừng; không có force/reset.

Baseline/content snapshot/ID receipts/log riêng nằm trong ignored
`.appdata/local/writing-content-20261114/` và `.appdata/local/writing-*.log`.
Không xóa/thay baseline khi chat sau tiếp tục. Source digest trong manifest và
receipt công khai khớp content digest canonical của importer; ID local chỉ nằm
trong receipt ignored. Export lại không sinh version mới khi nội dung không đổi.

## Nguồn và rà ngôn ngữ

Tự soạn toàn bộ nội dung; không sao chép lesson body và không gọi dịch vụ sinh
nội dung. Đã mở public Writing selector của EnglishDatpmt và overview beginner
writing của British Council, chỉ dùng ý tưởng về format/mục đích. URL, ngày truy
cập và cách xử lý quyền được ghi trong README dữ liệu. Không bypass đăng nhập.

Agent đã rà toàn bộ288 cặp và36 biến thể: chủ thể, thời gian, phủ định, số lượng,
văn phong, ý nghĩa và chức năng của từng format. Reports ghi rõ tình huống/số liệu
giả định. [Editorial review](05-beginner-editorial-review.md) liệt kê36 bài và
word count. Đây là rà của chính agent soạn bài, chưa phải đánh giá độc lập của
giáo viên. Không có normalized exact sentence duplicates trong batch; max Jaccard
shingle5 từ0.0069 nội bộ/0.0139 so baseline. Không nới evaluator để ép bài đạt.

## Kiểm chứng đã chạy

| Kiểm tra | Kết quả thực tế |
| --- | --- |
| `writing-content.ps1 -Action baseline` | Chụp read-only18 cells/22 Writing rows/72 tables; baseline create-once |
| `-Action dry-run` | Sẽ thêm36, đã có0; không ghi dữ liệu |
| `-Action pilot` | Thêm6 bài/48 câu/6 variants; nguyên42.409 dòng cũ |
| `-Action import`, lần tiếp theo | Thêm30 bài/240 câu/30 variants, bỏ qua6 pilot |
| `-Action import`, rerun | Thêm0 bài/0 câu/0 variants, đối chiếu36 |
| Restart recovery | Lưu bản receipt riêng, di chuyển receipt hiện tại rồi chạy import; thêm0;36 key/ID/content hash khôi phục y nguyên |
| `-Action export` |36 gói canonical mới; không sửa version cũ |
| `-Action export`, rerun |0 version mới,36 gói hiện có |
| `shared-data.ps1 -Action Validate -DatabaseReady` |307 immutable releases/16.896 record revisions hợp lệ; build0 errors |
| `shared-data.ps1 -Action Sync -DatabaseReady`, hai lần | Lần1:360 unchanged record operations; lần2:0 operations; cả hai exit0 |
| Sync receipts | Hai receipt có catalog SHA-256 hiện tại; last-sync.json mới sau Sync2 |
| `-Action roundtrip`, DB riêng mới | MariaDB10.5.23; migration→36 packages→36 lessons/288 pairs/36 variants; so persisted text/order/paragraph/hash; đánh giá references/variants đạt; repeat0 changed rows |
| `-Action verify`, sau Sync và export rerun | Quota đúng;42.409 dòng cũ/72 bảng,271 gói cũ và production.json nguyên vẹn |
| Focused application tests, cuối cùng |105 pass/0 fail/0 skip |
| `git diff --check` | Pass; cảnh báo CRLF→LF ở tệp bug memory có sẵn không phải lỗi whitespace |

Lệnh focused tests đã chạy:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --filter 'FullyQualifiedName~WritingContentBatchTests|FullyQualifiedName~WritingStage02RegressionTests|FullyQualifiedName~WritingLocalAssessmentTests|FullyQualifiedName~WritingCompletionTransactionTests|FullyQualifiedName~SharedDataPackageTests|FullyQualifiedName~SharedDataLifecycleTests|FullyQualifiedName~WritingAcceptedAnswersIntegrationTests' --logger 'console;verbosity=minimal'
```

Test mới kiểm tra mọi reference/variant qua policy; từ chối missing/wrong-level/
empty-English/copied-Vietnamese/order/variant/near-duplicate; chống resume bằng
private/unpublished/edited lesson; rà36 package fields/paragraphs/answers/privacy.
Sáu pilot đi qua HTTP public catalog, dịch vụ practice, tiến độ từng câu, variant
assessment, completion/replay và catalog pagination trong test host với cloud tắt.
Six full completions tạo6 credits, replay không thêm credit. Không gửi bài làm
vào tài khoản local thật để tránh tạo tiến độ/XP ngoài phạm vi.

Provider roundtrip dùng container riêng `tct-writing-20261114-roundtrip`, loopback
3315, DB `tct_acceptance_writing_20261114`; không mount/copy dev volume. Canonical
engine tạo1 system owner bị khóa chỉ trong DB disposable để ánh xạ alias; không
chuyển tài khoản/người học từ dev. Receipt đối chiếu toàn bộ rows trước/sau repeat.

Các lần đầu đã phát hiện/sửa trong công cụ mới: visibility của helper lock cần
giữ trong utility riêng; raw MySqlCommand phải gắn EF transaction; validator cần
từ chối copy tiếng Việt thành reference. Regression copy-Vietnamese đã fail trước
khi sửa rồi pass. Khi mở rộng test catalog, lọc topic chữ Việt bằng SQLite không
phản ánh case-folding MariaDB; kiểm tra public theo format/pagination và giữ nguyên
code sản phẩm. Không coi SQLite là provider proof.

Docker Desktop có một lần dừng giữa batch. Hai lần gọi import sau đó không kết
nối được, chưa ghi gì. Launcher `start-local.ps1 -DatabaseOnly` khởi động lại đúng
volume và giữ User Secrets; nhập tiếp theo receipt, rồi xác nhận preservation.

## Giới hạn và bàn giao

Chưa nghiệm thu giao diện bằng live browser, native zoom hoặc tài khoản local;
batch này không sửa UI. Không chạy remote CI/staging/production. Test build còn
warnings nullable/analyzer ở tệp có sẵn; không có build error. Không chạy bất kỳ
provider AI/speech trả phí nào. Các thay đổi stage02–04 có sẵn được giữ nguyên.

Tiến độ thực tế **36/108**. Intermediate36 và advanced36 dành cho chat riêng.
Danh mục46 bài cho phép các mốc13/25 có đủ bài công khai; mốc50 vẫn cần các batch
tiếp theo hoặc nội dung public khác. Không tự cấp credit/XP/badge và không hạ ngưỡng.
