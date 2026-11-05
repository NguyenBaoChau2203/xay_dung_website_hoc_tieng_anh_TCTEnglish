# Review đóng phần code/test — Châu final polish — 04/11/2026

Kết luận: **không còn finding code cần sửa trong phạm vi lượt C1.1; đóng các
finding R1–R5/RR1/RR2/C1.1 đã review. Có thể chuyển sang UAT tổng thể local.**
R6 vẫn partial; chưa đánh dấu toàn bộ B0–B7 hoàn thành hoặc production-ready.

Một agent review trên `fix/local-launcher-preserve-user-secrets`. Giữ dirty work;
không sửa app/test code, commit/push/PR/merge/remote CI/deploy trong lượt này.

## C1.1 đã đạt

RequireFreshBaselineTargetAsync hiện đọc toàn bộ TABLE_NAME của DATABASE() và
chỉ cho sentinel +history metadata rỗng. Mọi bảng khác đều bị từ chối trước
baseline; history có rows cũng bị từ chối. Không còn denylist8tên bảng.

Unit tests có sentinel-only, empty-history positive, applied-history negative
và Sets/Cards/SpeakingVideos/custom-leftover/WritingAcceptedAnswers negative,
assert0mutating statements. Provider leftover test trên DB riêng thực sự tạo
Sets/Cards/SpeakingVideos có rows, gọi guard và kiểm các rows/schema còn nguyên;
không chạy migration ngược để chứng minh lỗi. Fresh upgrade/repeat giữ users,
attempts, progress, variants và completion-credit sentinel; guard target đã
nâng cấp/concurrency vẫn đạt.

Không phát hiện actionable code finding mới. Không tạo repair plan rỗng hoặc
yêu cầu làm lại các tính năng đã đạt; active scope chuyển về C3/R6.

## Checks reviewer chạy mới

| Check | Kết quả |
| --- | --- |
| Build Debug +focused guard/Writing cloud/stale form |10pass/0fail/0skip (gồm8guard cases) |
| Full .NET, không bật provider/live opt-in |921pass/0fail/18skip/939total |
| Fresh MariaDB upgrade/repeat/preservation |1pass/0fail/0skip |
| MariaDB leftover schema rejected, data preserved |1pass/0fail/0skip |
| MariaDB concurrency3 +upgraded guard1 |4pass/0fail/0skip |
| Repo-local EF pending-model check | Không có pending model changes |
| git diff --check | Đạt |

Các provider runs riêng không biến18skip của full run thành pass. Paid live/
real mic/scale hoặc các provider suites ngoài filter chưa được chạy ở lượt này.
Node56pass ở review trước là evidence cũ, không ghi thành run mới; thay đổi hiện
tại tập trung guard/.NET tests. Không dùng tooling check thay app/browser proof.

Target MariaDB10.5.23, loopback3331 trên lab `chau1104r` đã kiểm image/labels:

- Fresh upgrade: `tct_acceptance_chau_review_1793803990830`.
- Leftover: `tct_acceptance_chau_review_1793803993772`, riêng biệt với upgrade.
- Sau upgrade,4regressions chạy trên target reviewer đã nâng cấp, không chạy
  baseline lại trên target này. Chỉ dữ liệu synthetic của review.

Evidence ignored:
`.tmp/chau-guard-closure-testresults/chau-guard-closure-{focused,full,upgrade,leftover,regressions}.trx`.
Setup/synthetic DB config ở `.tmp/chau-guard-closure-{upgrade,leftover}`; không đưa
credential/config này vào Git. Không reset DB làm việc, không đổi key/User Secrets/
EFS/ACL hoặc khởi động/dừng workload khác. Lab c cũ đã stopped; lần setup đầu
không thành công, hai provider attempts đầu bị skip vì thiếu config. Reviewer
đổi sang lab r đang chạy, setup hoàn tất rồi chạy lại: kết quả1+1+4pass ở trên;
không tính các attempt đầu là evidence đạt hoặc app failure.

## R6: đã bổ sung và còn cần nghiệm thu

Nguồn Cursor: ignored `.tmp/chau-schema-guard-uat-ledger.md` và
`.tmp/chau-final-polish-demo-manifest.md`. Reviewer đọc mới; không tự tái hiện
toàn bộ browser matrix. Chrome tool ở lượt này chỉ có about:blank, không có
session Admin của lượt Cursor để xác nhận lại. Không coi HTTP200 là rendered UI.

Đã có IDs/routes của Speaking53/55 và Writing1/22, số đo HTTP đầu/warm và working
set; ledger ghi Admin390/1440 partial Writing/Speaking index và canonical Accepted.
Đây là tiến triển; còn các khoảng trống cụ thể:

- Premium/Standard và A→B cùng browser profile chưa đủ evidence. Cần cả main/
  negative/error/reload/cancel/access/dedup theo B6 gốc.
- Chưa có case demo UI approved variant: manifest ghi local variants count0;
  canonical Accepted chưa chứng minh Admin duyệt→learner dùng biến thể trên UI.
- Video53 được chính manifest ghi timecode overlap/đoạn rất dài; phải kiểm lại
  playback/câu phù hợp trước khi coi cả hai video là học liệu demo đã duyệt.
- Số đo331ms/891ms trong manifest là HTTP ready/login và warm request latency;
  chưa chứng minh thời gian cold/warm khởi động từ launcher đến ready. Cần ghi
  metric đúng tên và đo startup nếu muốn đóng tiêu chí startup của B5.
- Paid/live mic quality là acceptance riêng cần opt-in, không tự chạy và không
  lấy fake-provider pass thay cho mic/accent thật.

Đây là phần nghiệm thu còn pending, không phải các finding code mới. Học liệu/
test accounts mới dùng namespace synthetic riêng; owner đã cho phép test account
trong session, không cần yêu cầu gửi password/key vào chat. Không đổi role người
dùng thật hoặc seed đè dữ liệu để hoàn tất matrix.

Plan đang dùng: `docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`,
chỉ tiếp C3/R6. Prompt `.ai/prompts/chau-final-polish-schema-guard-uat.md` đã đổi
scope sang R6 để agent không tiếp tục sửa guard đã đóng.
