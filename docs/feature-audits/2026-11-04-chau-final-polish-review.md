# Review Cursor final polish — 04/11/2026

Kết luận: **chưa đóng B0–B7**. Hai tính năng đã có code, migration và test cơ
bản; còn hai lỗi P1 về Writing variants, ba lỗi P2 trong code/test và một khoảng
trống nghiệm thu. Review local bởi một agent, không sửa code app, không commit,
push, remote CI hoặc deploy. Bảo toàn dirty work và User Secrets.

Branch đã kiểm tra: `fix/local-launcher-preserve-user-secrets`. Review bao gồm
diff Cursor và phần startup/key local có sẵn; không coi toàn bộ dirty tree là
code mới do Cursor viết. Không dùng API trả phí, mic thật hoặc database hosting.

## Phần triển khai đã xác nhận

- B1: server current-user ID qua practice ViewModel/data attribute; namespace
  draft theo owner/exercise/sentence; bỏ qua legacy key, không persist khi thiếu
  owner. Production-JS harness đạt; chưa browser A→B cùng profile.
- B2: response/snapshot/Razor/JS có fields ba trạng thái và nhãn NeedsReview.
  Luồng không cloud cải thiện; trường hợp cloud bổ sung còn R3 bên dưới.
- B3: alignment deterministic có cap256tokens, dùng full transcript trước preview;
  không thêm lần gọi ASR, không đổi receipt/điểm/credit, render bằng textContent.
  Còn R5 về vòng đời UI; source-regex test không thay cho DOM/browser behavior.
- B4: entity/bảng con, Admin-only +antiforgery, ID ràng buộc bài/câu, grading
  server canonical/approved và passage-edit guard đã có. Migration bổ sung,
  Designer/snapshot đầy đủ; còn R1/R2 về atomicity/stale content.
- Startup/key: các sửa local có sẵn vẫn giữ child env và không ghi lại secret
  store khi startup. Focused launcher/config tests đạt; không thử thay key thật.

## Findings

### R1 — P1: hai lượt lưu đồng thời có thể vượt giới hạn5 và trộn danh sách

Source: `TCTEnglish/Areas/Admin/Controllers/WritingExerciseManagementController.cs`
SaveAcceptedAnswers497–508,557–589; transaction helper394–402; bulk Edit278–302.

Mỗi request tải tracking entity trước transaction. Bên trong transaction query
Include lại nhưng không lock parent/CAS/version; re-query không bảo đảm refresh
tracking state hoặc serialize hai request. Hai danh sách khác nhau từ trạng thái
rỗng cùng qua token check, cùng thêm5; unique sentence/hash không chặn hai tập khác.

Probe thực trên MariaDB10.5.23, hai controller/context riêng, barrier sau SELECT
thứ hai của mỗi request: `successes=[true,true]`, `persistedCount=10`, `maximum=5`.
Đây là code controller + EF thật, không phải HTTP authorization acceptance.
Bulk passage Edit cũng cần dùng cùng cơ chế lock để guard không bị race với
save variants; một transaction đơn thuần không đủ.

Sửa: một protocol transaction/lock trên sentence hoặc exercise cha, lấy dữ liệu
fresh bên trong lock, kiểm expected state rồi replace nguyên list; contender
phải stale/conflict. Lock order thống nhất với bulk Edit/Delete có liên quan.
Kiểm chứng bằng MariaDB barrier, không chỉ SQLite/sequential saves.

### R2 — P1: form cũ duyệt đáp án cho nội dung câu đã thay đổi

Source: cùng controller508–509,565–566,662–671; token chỉ có Id/hash/time của
variants, không có nội dung/mapping sentence.

Repro đã chạy trên DB riêng: AdminA mở form câu `Hello.` khi chưa có variants;
AdminB dùng bulk Edit đổi sang `Goodbye.`; A gửi variant `Hi there.` bằng token
cũ do chính Edit cấp. Save báo success; policy chấm `Hi there.` Accepted cho
canonical `Goodbye.`. Với list rỗng, token không đổi dù nội dung câu đã đổi.

Sửa: expected state gồm sentence identity/exercise, canonical/Vietnamese và
mapping quan trọng cùng list variants; so lại state fresh trong protocol R1.
Form cũ phải báo conflict và không ghi. Không dùng token thay access check.

### R3 — P2: NeedsReview có cloud feedback bị thành NeedsRevision sau reload

Source: `TCTEnglish/Services/Writing/WritingAssessmentPolicy.cs`40–53 và
`TCTEnglish/Services/WritingService.cs`1731–1737,1874–1882.

Ở Local mode có cloud enhancement, verdict vẫn NeedsReview nhưng optional
feedback đổi EvaluationSource thành `local+ai-feedback`. Snapshot reload gọi
ResolveHistoricalStatus(false, source), chỉ nhận `local-review`, nên trở thành
NeedsRevision. Pure production-policy probe trả đúng lỗi này. Đây là source/
policy evidence; chưa dựng browser/live-provider repro, không gọi API thật.

Sửa mapping theo nguồn có semantics đã xác nhận và tách verdict khỏi provenance
feedback. Không biến mọi `ai` failed row lịch sử thành NeedsReview; AI mode có
contract khác. Thêm fake-provider HTTP evaluate→Data→Razor/reload regression,
assert Passed=false, ReviewRequired=true và XP không đổi.

### R4 — P2: full .NET suite đang thất bại

Source: `TCTEnglish.Tests/WritingAdminControllerStructureTests.cs`33–37.
Test tìm end marker `private void SyncWritingExerciseSentences(` trong khi
Cursor đã đổi method thành `private bool TrySyncWritingExerciseSentences(`.

Full current suite: **908pass /1fail /13skip /922total**. Fail là
WritingAdmin_TransactionHelper_WrapsManualTransactionInsideExecutionStrategy.
Test chỉ đọc source nên không phải lỗi transaction runtime đã được chứng minh,
nhưng change set hiện không có full suite xanh và sẽ vướng test gate tương ứng.
Sửa test để vẫn kiểm invariant execution strategy/transaction mà ít phụ thuộc
tên method kế tiếp; không bỏ/skip assertion để làm xanh.

### R5 — P2: Speaking giữ comparison cũ trong lượt ghi mới/mic denied

Source: `TCTEnglish/wwwroot/js/speaking.js`733–769,961–1002,1039.
Clear hiện chỉ ở lúc assess, đổi câu và một số response; startRecording và
nhánh từ chối mic không clear panel của lần đánh giá trước.

Probe chạy production JS với DOM harness và recorder giả: cả lúc lượt ghi mới
bắt đầu và sau mic denied, `previousComparisonVisible=true`, child cũ còn1,
providerRequests=0. Chưa phải rendered mobile/browser acceptance.

Sửa clear khi tạo lượt ghi mới trước khi xin mic; error/cancel/reset cũng không
giữ kết quả cũ. Không ảnh hưởng nút Stop của lượt đang ghi hoặc epoch guards.
Test hiện tại test_speaking_word_comparison.cjs chỉ tìm strings, không chạy flow.

### R6 — P2 acceptance/reporting: B5–B7 chưa đủ bằng chứng để đánh dấu xong

Plan ghi B0–B7 đã triển khai và tick B6 trong khi chính checkbox nói browser
ba roles chờ owner. Demo manifest chỉ nêu fixtures tổng quát; chưa chỉ ra hai
video có captions/timecodes và hai bài Writing đã kiểm, cũng chưa có số đo
cold/warm startup theo điều kiện đã ghi. Nhiều checklist chi tiết vẫn chưa tick.

MariaDB upgrade test hiện chứng minh user/exercise/sentence và unique hash;
chưa tạo/assert sentinel attempts/progress/credits, chưa test concurrent admin
save/bulk Edit hoặc repeat sau khi thêm variants. Không gọi các check đó là pass.
Acceptance fixture reset list cũng chưa có WritingAcceptedAnswers trước parent
tables; cần xử lý để không giữ orphan rows khi FOREIGN_KEY_CHECKS tạm tắt.

Sửa: phân biệt implemented/automated-tested/browser-pending/live-not-run, cập
nhật checklist đúng evidence; hoàn tất offline/browser phần được phép, ghi rõ
paid/live-mic và thao tác cần owner tự làm còn pending. Không đánh dấu đóng khi
còn R1–R5 hoặc acceptance bắt buộc chưa thực hiện.

## Kiểm chứng trong lượt review

| Kiểm tra | Kết quả |
| --- | --- |
| Build Debug qua focused dotnet test | Đạt |
| Focused Writing/variants/schema/alignment/assessment |54pass,1MariaDB skip ở lần chạy chưa cấu hình |
| Full .NET hiện tại |908pass,1fail,13skip; không phải full pass |
| WritingAcceptedAnswersMariaDbUpgradeTests trên DB mới |1pass,0skip; MariaDB10.5.23 |
| Repo-local EF has-pending-model-changes | Không có pending model changes; explicit child connection vào DB review |
| Node toàn bộ test_*.cjs |51pass,0fail |
| Python launcher/config focused |7+14pass bằng unittest discover đúng import path |
| Hai controller/EF MariaDB probes | Tái hiện R1:10/5 và R2:Hi there Accepted cho Goodbye |
| Production JS fake-recorder probe | Tái hiện R5;0provider requests |
| Production historical-status helper | Tái hiện R3: local+ai-feedback +false→NeedsRevision |
| git diff --check | Đạt tại thời điểm review |

Evidence ignored: `.tmp/chau-review-testresults/chau-review-{focused,full,mariadb}.trx`,
`.tmp/chau-review-probes/{Program.cs,mariadb-observations.json,speaking-stale.cjs,speaking-observations.json}`.
DB mới `tct_acceptance_chau_review_1793792491266`, loopback3330 trên lab container
đã kiểm label/purpose/run; không tạo container mới hoặc reset DB có sẵn. Credential
DB synthetic nằm trong ignored config, không đưa vào report/prompt/Git. Không
đổi database local/hosting, key, native trust hoặc dừng workload khác.

Lần gọi Python theo dotted modules ban đầu sai import path; chạy lại discover
7+14 đạt. Lần EF check đầu thiếu explicit connection bị từ chối trước thao tác
DB; chạy lại với DB review đã kiểm identity thành công. Không coi hai lần đầu
là lỗi app hoặc là check đạt.

## Bàn giao

Repair plan: `docs/superpowers/plans/2026-11-04-chau-final-polish-review-repair.md`.
Cursor prompt: `.ai/prompts/chau-final-polish-review-repair.md`.
Chỉ review/tạo tài liệu và ignored diagnostic probes trong lượt này. Owner chưa
yêu cầu sửa code ở lượt review, vì vậy chưa áp dụng các sửa R1–R5.
