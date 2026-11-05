# Cursor Auto — chốt hai finding test và R6 sau review lần hai

Khi owner giao prompt này để thực thi, đọc:
`docs/feature-audits/2026-11-04-chau-final-polish-rereview.md` và làm checklist
`docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`.

R1–R5 về hành vi app đã được reviewer xác minh local; không triển khai lại hai
tính năng hoặc sửa lại protocol đang đúng. Scope lần này chỉ các điểm còn lại:

1. RR1 P2: upgrade test gọi baseline03/11 mà chưa kiểm target fresh, nên rerun
   DB đã latest sẽ chạy Down xóa bảng variants. Thêm fail-before-DDL/DML guard
   lịch sử/schema sau offline/online identity, và regression DB đã nâng cấp có
   dữ liệu bị reject nguyên vẹn. Fresh upgrade dùng DB mới riêng; không rollback
   acceptance cũ, dev hoặc hosting. Không sửa migration để che lỗi test.
2. RR2 P2: renderWordComparison twice hiện tự append/remove DOM. Thay bằng
   production speaking.js flow recorder/assess/commit fake hai lượt; assert DOM
  2→1, encoding, stale/error/cancel và request count. Mutation bỏ clear/unsafe
   render phải làm test fail. Không chép thuật toán hoặc thêm app test-only exports.
   Củng cố MariaDB concurrency bằng barrier +existing list/save-vs-edit; không
   chỉ khởi hai Tasks rồi giả định contention đã được thử.
3. R6: demo2video/2bài có IDs/routes/captions/answers đã kiểm, cold/warm measurements,
   browser3roles390/1440 và B6 gốc. Chưa chạy được thì pending, không tick xong.

Một agent theo AGENTS; CodeGraph trước source, load active skills; giữ dirty
work/branch/dữ liệu/User Secrets. Không sửa Program/appsettings/csproj/models/
migrations hoặc thay provider/model; không cần schema mới cho scope test này.
Giữ web text gpt-6-luna/low, audio gpt-transcribe; không Whisper/Colab.

Local launch dùng scripts/start-local.ps1 -NoBrowser theo AGENTS; key đã lưu
được đọc tự động, không ghi lại store/argv/log. Nếu port bận giữ process và chọn
port trống. Không dừng app/container dự án khác. Không paid API hoặc mic thật
nếu chưa owner opt-in; fake provider trong tests.

Chạy focused/Node all và .NET full khi thay .NET test/helper, provider upgrade/
repeat/concurrency trên DB đúng guard. Exact commands/results/skips, review diff
và secret/artifact check. Update ledger/known issues; bug log chỉ ghi fix đã đạt.
Chưa commit/push/PR/merge/remote CI/deploy, không đổi Plesk/gate. Báo tiếng Việt
phần đã chứng minh và phần nghiệm thu còn pending, không nhận production-ready.
