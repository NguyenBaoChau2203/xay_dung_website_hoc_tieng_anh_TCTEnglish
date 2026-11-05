# Review lần hai — final polish của Châu — 04/11/2026

Kết luận: **R1–R5 về hành vi app đã đạt các kiểm tra local trong lượt này; không
tái hiện hai lỗi P1 cũ.** Còn hai finding P2 ở kiểm thử (RR1/RR2) và R6 nghiệm
thu browser/demo/measurement. Chưa đóng toàn bộ B0–B7 hoặc nhận production-ready.

Một agent review trực tiếp trên `fix/local-launcher-preserve-user-secrets`, giữ
dirty work. Không sửa app/test code trong lượt review; chỉ tài liệu và diagnostic
probes ignored. Không commit/push/PR/merge/remote CI/deploy hoặc gọi paid API.

## R1–R6 sau lượt sửa Cursor

| Finding trước | Kết quả kiểm tra lại |
| --- | --- |
| R1 concurrent save10/5 | Đã có parent exercise FOR UPDATE trong save và bulk Edit; clear tracker rồi load fresh. MariaDB HTTP test đạt. Probe riêng có barrier trước hai lock requests: empty/existing list đều chỉ một save thành công, đúng5rows và một list nguyên vẹn |
| R2 stale Hello→Goodbye | Token có exercise/sentence/VI/EN/variants; form cũ bị từ chối,0variant được thêm. SQLite HTTP stale test và MariaDB controller probe đạt |
| R3 cloud-feedback status reload | Local giữ decision source; cloud provenance nằm OptionalFeedbackSource; legacy local+ai-feedback được map đúng. Fake-provider HTTP submit/Data/Razor và focused tests đạt |
| R4 structure/full suite fail | Test marker được cập nhật. Full suite mới0fail; vẫn là source-string test nên phụ thuộc tên method, chưa phải runtime transaction proof |
| R5 comparison cũ | Clear trước mic request và nhánh mic denied; production-JS probe cũ nay panel ẩn/0children/0provider requests. Renderer thực cũng đạt probe hai assessment giả lập, nhưng tracked render test còn RR2 |
| R6 fixture/provider | AcceptanceTables đã có bảng con; upgrade có sentinel attempts/sentence progress/exercise progress và repeat sau thêm variants. Test DB mới đạt; còn guard RR1, chưa credit sentinel/full browser/demo/measurement |

Thêm MariaDB controller/EF checks: save lại danh sách không đổi thành công;
save variants cạnh tranh với bulk Edit cho kết quả nhất quán. Lượt đã quan sát:
save thắng, bulk Edit bị guard, canonical Hello và5variants giữ nguyên. Đây không
phải chứng minh mọi lịch chạy hoặc nghiệm thu HTTP authorization từ probe riêng.

## RR1 — P2: upgrade test vẫn có thể rollback database acceptance đã nâng cấp

File: `TCTEnglish.Tests/WritingAcceptedAnswersMariaDbUpgradeTests.cs`, dòng27.
Sau offline/online identity, test gọi MigrateAsync về migration03/11 ngay, không
kiểm lịch sử migration rỗng/schema mới trước bước baseline như repair plan yêu cầu.
Sentinel purpose/run chỉ xác nhận target; không chứng minh target còn trống.

Nếu chạy lại test với cùng acceptance DB đã có AddWritingAcceptedAnswers, EF
sẽ chạy Down và DROP WritingAcceptedAnswers trước các assertion preservation.
Dry-run bằng migrator hiện tại trên DB riêng xác nhận:
latestAlreadyApplied=true, downgradeWouldDropAcceptedAnswers=true.
**Không thực thi downgrade** trong lượt review. Đây là rủi ro mất dữ liệu test
và hỏng repeat-run trên lab, không phải phát hiện app gọi Down khi deploy và
không phải bằng chứng hosting/dev bị tác động.

Sửa: reject target không còn fresh trước MigrateAsync baseline; kiểm lịch sử và
app schema ngoài sentinel bằng target đã guard, hoặc tạo DB mới cho từng run.
Regression phải chứng minh rerun trên DB đã nâng cấp dừng trước DDL/DML và giữ
variants/attempts/progress; không chỉ gọi MigrateAsync lần hai ở latest.

## RR2 — P2: render regression đang tự thao tác DOM, không chạy renderer thật

File: `scripts/tests/test_speaking_word_comparison.cjs`, dòng281–310.
Test mang tên renderWordComparison twice nhưng chỉ tự append/removeChild/append
hai span trong fixture. Nó không gọi renderWordComparison hoặc flow assess
để production gọi helper. Xóa clear khỏi renderer thật vẫn không làm test này đỏ.

Reviewer đã chạy production JS qua recorder/fetch giả:2assessment và2commit
giả, chip count2→1, markup được giữ literal bằng textContent;0paid API requests.
Vì vậy chưa thấy bug runtime renderer ở code mới, nhưng test trong Git chưa bảo
vệ hành vi đó. Hai tests Record/mic-denied mới có chạy production lifecycle và đạt.

Sửa: drive hai assessment liên tiếp qua recorder/fetch fake hoặc harness gọi
production helper thực; assert đúng DOM/count/text, encoding, request count,
stale/cancel. Không chép thuật toán renderer hoặc thêm exports app chỉ để test
trông như gọi được. Mutation bỏ clear/đổi textContent thành innerHTML phải làm
test fail; restore production file ngay, không để mutant vào Git.

Test concurrent empty saves mới cũng chỉ khởi hai task, không có barrier điều
phối. Cần đưa evidence mạnh hơn từ probe thành tracked regression: empty/
existing list và save-vs-edit, timeout hữu hạn, loser thực sự conflict, không
dùng sleep làm bằng chứng hai request đã đến điểm cạnh tranh.

## Kết quả reviewer chạy mới

| Kiểm tra | Kết quả |
| --- | --- |
| Build Debug qua focused tests | Đạt |
| Focused Writing variants/cloud/policy/Admin structure |40pass/0fail/0skip |
| Full .NET không bật provider/live flags |913pass/0fail/14skip/927total |
| MariaDB upgrade trên DB mới |1pass/0fail/0skip |
| MariaDB HTTP concurrent save |1pass/0fail/0skip |
| Node toàn bộ test_*.cjs |54pass/0fail |
| Extra MariaDB probes | stale rejected; empty/existing race→5,1winner; unchanged list save; save-vs-edit consistent |
| Extra production JS probes | new record/mic deny cleared; renderer2→1, literal markup |
| Repo-local EF pending-model check | Không có pending changes; explicit DB review connection |
| git diff --check | Đạt |

Không cộng hai provider tests vào full-run rồi gọi14skips đã chạy hết; các skip
khác như paid live/scale/provider suites vẫn chưa thực hiện trong lượt này.
Con số trong báo cáo Cursor dùng cấu hình khác; kết quả trên là run reviewer
thực sự quan sát, không thay bằng con số trong bug log.

MariaDB10.5.23 target mới: `tct_acceptance_chau_review_1793795917400`, loopback3330
trên container lab đã xác minh labels/purpose/run. Thứ tự: fresh upgrade test →
HTTP concurrency fixture →extra probes. Không reset DB làm việc, không mở container
mới hoặc dừng workload. Synthetic DB credential chỉ ở ignored config, không key
OpenAI, không đưa vào argv/report/Git. Không thay User Secrets/EFS/ACL.

Evidence ignored:

- `.tmp/chau-rereview-testresults/chau-rereview-{focused,full,mariadb-upgrade,mariadb-concurrency}.trx`.
- `.tmp/chau-rereview-probes/Program.cs`, `mariadb-observations.json`.
- `.tmp/chau-rereview-probes/speaking-observations.json`, `render-flow-observations.json`.
- Migration dry-run stdout chỉ là bool; SQL downgrade không được execute.

## Phần còn lại để đóng

RR1/RR2 sửa test; cập nhật checklist/các claim cũ cho đúng evidence. Sau đó B5/B6:
hai video/hai bài Writing có IDs/routes/captions/approved answers thật đã kiểm,
cold/warm startup có điều kiện đo, browser Admin/Premium/Standard390/1440,
A→B cùng profile, các luồng chính/error/reload/permission/dedup. Paid live/mic
không tự chạy; không đồng nhất fake-provider pass với quality mic/accent thật.

Plan focused: `docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`.
Prompt Cursor: `.ai/prompts/chau-final-polish-final-checks.md`.
