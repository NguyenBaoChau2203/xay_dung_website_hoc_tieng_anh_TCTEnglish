# Cursor Auto — sửa findings review final polish 04/11/2026

Khi owner giao prompt này để sửa, thực thi local repair plan:
`docs/superpowers/plans/2026-11-04-chau-final-polish-review-repair.md`.
Review/evidence: `docs/feature-audits/2026-11-04-chau-final-polish-review.md`.
Checklist B0–B7 gốc là scope, không lấy các checkbox done làm bằng chứng.

Một agent duy nhất theo AGENTS.md; giữ dirty work/branch/dữ liệu/key. Không
subagent hoặc AI chat/CLI khác. Dùng CodeGraph trước indexed source, relevant
skills tct-superpowers/engineering/security/admin/speaking/api/ef/UI; đọc DESIGN
trước UI edits. Không auto rebuild index, không thay trust hoặc model/provider.

Triển khai và tự review, không chỉ viết plan mới:

1. **R1 P1:** save variants hiện không atomic. MariaDB probe chứng minh hai Admin
   cùng submit5khác nhau từ empty state →cả hai success,10rows. Dùng protocol
   lock/CAS thật trên parent, fresh state trong transaction, chung với bulk Edit.
   Re-query Include với tracking không phải lock/refresh. Contender phải stale;
   không union/overwrite/partial save. Viết red→green concurrency barrier tests.
2. **R2 P1:** expected token hiện không bind sentence text. Form Hello rỗng →
   Admin khác đổi Goodbye →submit stale Hi there vẫn accepted. Bind identity/
   canonical/Vietnamese/mapping +variant state, so lại trong protocol R1.
   Giữ validation input khi lỗi. Test stale list/content, race bulk Edit, role/
   CSRF/cross-ID/max5/length/duplicates/rollback, accepted history/XP không đổi.
3. **R3 P2:** Local+cloud feedback đổi source sang local+ai-feedback; resolver
   reload false về NeedsRevision dù response NeedsReview. Sửa mapping/decision
   provenance tương thích; fake-provider evaluate→Data→Razor/reload tests. Không
   đoán SummaryTitle hoặc biến mọi ai failed lịch sử thành NeedsReview.
4. **R4 P2:** full .NET review908pass/1fail/13skip. Structure test vẫn tìm
   `private void SyncWritingExerciseSentences(` đã đổi thành TrySync. Sửa test
   ổn định mà giữ execution-strategy/transaction invariant; không skip/xóa test.
5. **R5 P2:** comparison Speaking cũ còn hiện trong new recording/mic denied.
   Clear đúng đầu epoch và error/cancel paths, giữ Stop/stale-response guards.
   Thay regex-only test bằng production-JS DOM behavior; fixture phải có đúng
   firstChild/removeChild semantics. Render2lần không dồn, text encoded, no extra
   ASR/paid request, không đổi điểm/receipt/credit.
6. **R6:** hoàn tất provider preservation/fixture reset, demo, benchmark và UAT
   theo plan. AcceptanceTables còn thiếu bảng con WritingAcceptedAnswers. Upgrade
   chỉ dùng fresh guarded DB; sentinel attempts/progress/credits phải được giữ
   và repeat sau khi thêm variants. Không downgrade/reset dev hoặc reuse DB
   diagnostic đã có data để chạy baseline. B5/B6/B7 thiếu evidence thì pending.

R1–R5 dự kiến không cần schema mới; không sửa Program.cs/appsettings/.csproj,
applied migration/Designer/snapshot hiện tại. Nếu thực sự cần persisted version,
thực hiện assessment +follow-up additive migration theo tct-ef-migration và guard
provider trong cùng scope, không sửa lịch sử migration đã áp dụng.

Chạy build/focused rồi full .NET cuối cùng, JS relevant/all và MariaDB upgrade/
repeat/concurrency/data preservation; exact commands/totals/skips. Chỉ chạy
tooling suite nếu đổi tooling. Cập nhật ledger, bug log sau fix, known issues;
review diff và secret/artifact scan. Giữ các lô sửa nhỏ để owner review.

Local web dùng `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`, free
port nếu process khác đang nghe; key đã lưu tự đọc, không rewrite store/argv/log.
Giữ gpt-6-luna/low và gpt-transcribe, không Whisper/Colab. Fake provider cho các
test; không paid API/mic owner nếu chưa opt-in. Không dừng workload khác.

Chưa commit/push/PR/merge/remote CI/deploy, không đổi Plesk/gate. Báo tiếng Việt:
R1–R6 đã làm gì, tests/evidence, browser/live acceptance còn chờ; không gọi
skipped/unrun là pass hoặc tuyên bố production-ready từ local tests.
