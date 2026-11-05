# Review final checks — 04/11/2026

Kết luận: **RR2 đã đạt; RR1 chặn downgrade khi có history đã đạt, còn một case
schema trong C1.1; R6 UAT/demo/measurement vẫn pending.** Không tái hiện các lỗi
P1/R1–R5 cũ; chưa đóng toàn bộ B0–B7. Review local một agent trên
`fix/local-launcher-preserve-user-secrets`; không sửa app/tests, commit, push,
remote CI hoặc deploy trong lượt này.

## Finding còn lại — P2: fresh-baseline guard chỉ kiểm một phần tên bảng

Source: `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceConnectionGuard.cs`295–329.
RequireFreshBaselineTargetAsync kiểm lịch sử rồi duyệt danh sách8bảng. Khi history
rỗng/không tồn tại, các bảng khác của app như Sets/Cards/SpeakingVideos không nằm
trong danh sách nên không được phát hiện. Target có thể được nhận là fresh dù
vẫn có schema; baseline migration sẽ chạy trên target không đúng điều kiện mới.

Đây là phạm vi test/acceptance guard, không phải lỗi luồng Writing/Speaking.
Nhánh lịch sử đã nâng cấp đã được bảo vệ đúng, nên finding này không khẳng định
target có history vẫn bị downgrade như phiên trước.

Reviewer gọi **production helper hiện tại** trên FakeAcceptanceConnection của
chính guard test, với đúng purpose/run, history0 và existingTables=[Sets]. Kết quả:
`acceptedAsFresh=true`, `mutatingStatements=0`. Đây là fixture/helper reproduction;
không tạo Sets trong DB thật và không thực thi migration ngược để tái hiện.

Sửa nhỏ: kiểm toàn bộ TABLES của đúng current schema, chỉ cho sentinel và history
metadata rỗng được cho phép rõ. Không duy trì denylist tên một số bảng. Thêm case
Sets/Cards/SpeakingVideos/custom-leftover và sentinel-only/empty-history positive;
assert reject trước DDL/DML và giữ nguyên mọi rows/history trên MariaDB riêng.

Scope sửa chi tiết là C1.1 tại
`docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`.

## Phần đã kiểm đạt mới

- Upgrade test gọi RequireFreshBaselineTargetAsync trước baseline. Có flag riêng
  TCT_WRITING_ANSWERS_UPGRADE=1 và fresh target riêng; DB đã nâng cấp bị từ chối,
  variants/attempts giữ nguyên. Fresh upgrade/repeat giữ attempts/progress và
  completion-credit sentinel; không sửa migration đã áp dụng.
- Speaking test chạy recorder→assess→render→commit production JS hai lượt2→1,
  markup literal, NoSpeech không commit. In-memory mutation bỏ clear làm lỗi
  tích lũy được nhận diện; source production không bị sửa. RR2 renderer đóng.
- MariaDB tests có empty/existing list và save-vs-edit;4provider regressions đạt.
  Note giới hạn: contention hook vẫn có Delay80/30ms và signal do test gửi, nên
  không tuyên bố đã khảo sát mọi lịch chạy. Các outcome/state assertions đạt;
  đây không phải phát hiện regression runtime mới.
- R1–R5 code app giữ kết quả kiểm chứng ở lượt trước; các thay đổi mới tập trung
  test helper/tests. Protected Program/appsettings/csproj không có diff.

## Kết quả reviewer chạy trong lượt này

| Check | Kết quả |
| --- | --- |
| Build Debug +focused guard/Writing/cloud/structure |41pass/0fail |
| Full .NET mặc định, không provider/live opt-in |915pass/0fail/17skip/932total |
| Node toàn bộ test_*.cjs |56pass/0fail |
| Fresh MariaDB Writing upgrade/repeat/credit preservation |1pass/0fail/0skip |
| MariaDB concurrency3 +already-upgraded guard1 |4pass/0fail/0skip |
| Extra fresh-schema fixture probe | Sets +history0 vẫn được nhận fresh: finding C1.1 |
| Repo-local EF pending-model check | Không có model changes còn thiếu migration |
| git diff --check | Đạt |

Không cộng5provider tests vào full-run rồi coi17skip đã chạy hết. Paid speech/
text, real mic và scale checks chưa chạy;56Node tests không phải full browser UAT.
Tooling validation không thay bằng chứng app/provider/browser.

Target reviewer mới: `tct_acceptance_chau_review_1793797650385`, MariaDB10.5.23
loopback3330 trên lab container đã kiểm identity. Thứ tự fresh upgrade →provider
regressions. Không tạo container, không reset DB làm việc, không thay key/User
Secrets/EFS/ACL hoặc dừng workload. Synthetic credential chỉ ở ignored config.

Evidence ignored:
`.tmp/chau-final-review-testresults/chau-final-review-{focused,full,mariadb-upgrade,mariadb-regressions}.trx`.
Probe mode `fresh-other-table` trong ignored `.tmp/chau-rereview-probes/Program.cs`
chỉ dùng fixture; không dùng key hay DB thật. Setup/config ở .tmp không đưa Git.

## Bàn giao

C1.1 còn cần sửa trước khi xác nhận fresh-target guard đủ; RR2 không còn finding
mới. R6 hiện vẫn thiếu manifest2video/2writing có IDs/routes/captions/answers,
controlled cold/warm measurement và browser Admin/Premium/Standard390/1440,
A→B cùng profile, Writing submit/reload/AI fallback, Speaking lifecycle và core
Account/Goals/Badges/AI/notifications/access/dedup matrix. Các phần này được plan
để unchecked đúng thực tế; chưa có evidence mới để đóng trong lượt review.

Prompt scope còn lại: `.ai/prompts/chau-final-polish-schema-guard-uat.md`.
Reviewer chỉ viết audit/checklist/prompt/known-issues; không triển khai code fix.
