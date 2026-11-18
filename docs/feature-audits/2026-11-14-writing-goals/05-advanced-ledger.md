# Stage05c — advanced36 and final 108-lesson proof

Ngày 14/11/2026, nhánh `fix/writing-goals-profile-polish`. Một agent trực tiếp
soạn, rà và kiểm chứng; giữ dirty work của các stage trước. Chỉ thực hiện local;
không commit, push, PR, remote CI, deploy hay paid API.

## Kết quả nội dung

Hoàn tất 36 bài advanced mới, sáu bài cho mỗi format emails, diaries, essays,
articles, stories và reports. Tổng 432 cặp Việt–Anh, 36 đáp án thay thế, 9.387 từ
tiếng Anh; mỗi bài 12 câu, ba đoạn, 225–279 từ. Các bài sử dụng sắc thái, giới hạn
của lập luận, văn phong nghề nghiệp hoặc phản tư và cấu trúc phức tạp phù hợp
advanced trong taxonomy sẵn có. Không thêm level/format hoặc nới evaluator.

Tái sử dụng đúng 36 key advanced đã reserved trong manifest 108 slot, typed
importer/validator, runtime receipts và canonical exporter có sẵn. Không sửa
content JSON/receipt của beginner hoặc intermediate, không tái sử dụng ID cũ.
Manifest đánh dấu hoàn tất sau kiểm chứng; public receipt loại bỏ ID cá nhân.

Agent rà mọi cặp mới và biến thể về chủ thể, thời gian, phủ định, số lượng,
mức độ yêu cầu, văn phong, nghĩa và liên kết đoạn. Mỗi format giữ chức năng riêng;
sáu báo cáo ghi rõ tình huống/số liệu giả định. Xem
[editorial review](05-advanced-editorial-review.md) với đủ 36 bài/mục đích/word count.
Đây là rà của agent soạn bài, chưa có đánh giá độc lập của giáo viên hay chứng
nhận CEFR. Các rubric/review của 72 bài trước được giữ nguyên trong ledger gốc;
test hiện tại kiểm lại cả 108 bài/1.152 cặp/108 biến thể và manifest/assessment.

Không có câu tham chiếu trùng sau chuẩn hóa trong toàn bộ 1.152 cặp. Max Jaccard
shingle năm từ là 0.003690 nội bộ advanced và 0.007426 so hai batch trước.
Importer cũng so với bài gốc, gồm private chỉ trong local, không xuất văn bản đó.
Chỉ số độ dài/trùng và exact-answer pass không tự chứng minh chất lượng ngôn ngữ.

Ngày 14/11/2026 đã mở [EnglishDatpmt Writing](https://english.datpmt.com/writing)
và [British Council C1 overview](https://learnenglish.britishcouncil.org/free-resources/writing/c1).
Chỉ tham khảo mô tả mức độ và mục đích thể loại; mọi lesson body, đề Việt, câu Anh,
biến thể và metadata đều tự soạn. Không sao chép văn bản có bản quyền, bypass
đăng nhập hoặc gọi dịch vụ sinh/chấm trả phí.

## Ma trận và bảo toàn

[Ma trận đầy đủ](05-content-matrix.md) ghi baseline/mới/sau cho cả 18 ô. So với
baseline gốc, mỗi ô tăng đúng sáu bài public mới; tổng **108/108**. Public tăng
10 → 118, riêng advanced 82 → 118. Beginner/email có 16 bài vì giữ 10 bài cũ;
chỉ sáu trong số đó tính vào quota mới.

Ba runtime receipts có đúng 108 stable key và 108 ID khác nhau; không ID nào thuộc
22 bài baseline, và 36 ID advanced không thuộc 94 bài có trước đợt này. Verify
từng level kiểm tra persisted metadata, cặp, thứ tự, đoạn và accepted-answer hash.

Đối chiếu multiset SHA-256 từng dòng giữ nguyên 42.409 dòng baseline và toàn bộ
44.209 dòng trước advanced trên 72 bảng. Bao gồm 11 bài riêng, một bài hệ thống
chưa công bố, tài khoản, attempts, progress, huy hiệu và completion credits. Không
tạo tiến độ/XP/badge trên tài khoản dev thật. Baseline gốc giữ nguyên SHA-256;
343 package version trước advanced và `production.json` giữ nguyên bytes.

Snapshot private, DB IDs, credential và runtime receipts/logs ở ignored
`.appdata/local/`; kiểm tra `git check-ignore` đạt. Public receipt chỉ có tổng số,
stable key, digest và thông tin package/provider; không có learner data.

## Kiểm chứng thực tế

| Kiểm tra | Kết quả |
| --- | --- |
| Additive baseline advanced | Read-only trước import; 82 public/94 Writing rows, 44.209 dòng/72 bảng, 343 package versions; baseline gốc giữ nguyên |
| Canonical Status trước export | Hoàn tất; giữ mapping/binding hiện có |
| Validator/local assessment | 36/432/36 đạt; đúng format/level/quota/paragraph/order/length; mọi reference/variant pass; empty/unrelated reject |
| Pilots trước dev import | 7 tests pass/0 fail/0 skip; cả ba level, riêng advanced đủ sáu format; anonymous HTTP catalog, pagination, ordered practice, alternatives, progress, completion/replay; cloud tắt |
| Dry-run | Sẽ tạo 36, có sẵn 0; không ghi |
| Dev import | Một transaction tạo 36 bài/432 câu/36 biến thể |
| Dev rerun | 0 bài/0 câu/0 biến thể, 36 no-op |
| Local pilot sau full import | 0 tạo, 6 no-op; không gọi đây là sáu pilot mới trong dev |
| Canonical export | 36 immutable v1 qua exporter hiện có; không hand-build package |
| Canonical re-export | 0 version mới/36 có sẵn; không có gián đoạn export hoặc phục hồi candidate trong đợt advanced |
| Package review | 36/36 checksum/scope/privacy đạt; mỗi gói chỉ 1 root + 12 câu + 1 accepted answer; alias `owner:system`; không users/private/attempts/progress/rewards/assets/dependencies/deletes |
| Canonical Validate | 379 releases/17.904 record revisions hợp lệ |
| Sync1/Sync2 | 504 unchanged operations rồi 0 operations; hai lần exit0; hai receipt và last-sync khớp catalog SHA-256 hiện tại |
| Fresh provider roundtrip | MariaDB10.5.23 mới; migrations → 36 packages → 36/432/36; persisted bilingual/order/paragraph/answers khớp; reference/alternative assessment đạt |
| Provider repeat | 0 changed rows theo hash toàn DB; chỉ tạo một system owner bị khóa trong DB disposable |
| Focused application suite | 112 pass/0 fail/0 skip; build app/test thành công trước pilot; suite cuối dùng cùng binary |
| Final all-level rerun/verify | Cả ba batch đều tạo 0/0/0, mỗi batch 36 no-op; persisted content và quota 18 ô đạt, mọi snapshot/package cũ được bảo toàn |
| Final identity proof | 108 key/108 ID mới duy nhất, không tái dùng ID baseline; advanced không tái dùng ID của 72 bài trước |
| Final manifest/package check | Sau khi ghi complete: 4 tests pass/0 fail/0 skip; 108 complete slots/0 reserved; metadata và whitespace các tệp mới đạt |
| `git diff --check` | Pass; chỉ cảnh báo CRLF→LF tại bug memory có sẵn; không sửa protected config/production approval |

Lệnh application tests:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~WritingContentBatchTests|FullyQualifiedName~WritingStage02RegressionTests|FullyQualifiedName~WritingLocalAssessmentTests|FullyQualifiedName~WritingCompletionTransactionTests|FullyQualifiedName~SharedDataPackageTests|FullyQualifiedName~SharedDataLifecycleTests|FullyQualifiedName~WritingAcceptedAnswersIntegrationTests' --logger 'console;verbosity=minimal'
```

Các lệnh content dùng `scripts/writing-content.ps1 -Action <action> -Level advanced`.
Canonical Status/Validate/Sync dùng `scripts/shared-data.ps1 -DatabaseReady
-SkipBuild -Configuration Debug`, với binary app đã build. Rerun cuối import/verify
cả beginner, intermediate và advanced; export advanced lần hai kiểm tra không
tạo thêm version. Kết quả cuối cùng và content digests nằm trong
[advanced public receipt](../../../../data/writing/2026-11-14/advanced-receipt.json).

Roundtrip dùng container mới `tct-writing-20261114-advanced-roundtrip`, loopback
3317, DB `tct_acceptance_writing_advanced_20261114`; không mount/copy dev volume.
Credential ngẫu nhiên chỉ qua child environment; không argv/log/Git. Container
được dừng sau kiểm chứng; Docker dev và workload khác giữ nguyên. Provider proof
của 72 bài trước còn trong các receipt/ledger gốc; lượt roundtrip mới này kiểm
riêng advanced36, không tuyên bố đã import108 vào một DB disposable duy nhất.

Không đổi entity/DbContext/mapping nên không phát sinh migration mới. Importer
từ chối pending DB migrations và đã xác nhận schema local cập nhật; không sửa
Program.cs, appsettings, project files hoặc global assessment.

## Giới hạn bàn giao

Chưa nghiệm thu UI live, keyboard hoặc native zoom trong batch nội dung này;
stage06/07 vẫn sở hữu việc tích hợp UI/nghiệm thu toàn ứng dụng. Build còn warning
nullable/analyzer ở các tệp có sẵn, không có build error; một utility QA scratch
ban đầu cũng báo chênh tham chiếu EF9.0.0/9.0.4, trong khi importer/provider proof
dùng pin9.0.4 và chạy đạt. Không đổi package/protected config để xử lý warning.
Các bản dịch hợp lệ ngoài danh sách chọn lọc vẫn có thể NeedsReview theo policy
hiện tại. Không chạy CI/staging/production; không commit/push/deploy/paid API.
