# Review toàn bộ thay đổi và merge — 04/11/2026

Owner cho phép review, commit theo nhóm, push, tạo PR và merge. Một agent thực
hiện. Repository thực tế dùng `master`, không có `main`; giữ deploy gate false.
Không gọi paid AI, gửi SMTP thật, reset DB hoặc đưa dữ liệu máy owner lên Git.

- [x] Kiểm kê toàn bộ tracked/untracked changes, đối chiếu phạm vi và dependencies.
- [x] Review auth/ownership/antiforgery, quota, streaming/cancel, catalog queries,
      SMTP/unlock, OpenAI audio/credentials và startup/tooling.
- [x] Review UI/JS, migrations/metadata, tài liệu và vendor provenance; sửa pin
      DOMPurify3.2.6 có advisory thành3.4.17, thêm hash/path checks và Chromium probe.
- [x] Quét secret/artifact trên nội dung dự kiến commit, chỉ log vị trí khi có hit.
- [x] Chạy build/test .NET, JavaScript, Python, tooling và pending-model check.
- [x] Kiểm chứng migration/concurrency trên disposable MariaDB nếu Docker sẵn sàng;
      ghi rõ kiểm chứng nào bị chặn và không tác động dev DB hiện hữu.
- [ ] Commit các nhóm có liên quan, kiểm lại diff đã commit và working tree.
- [ ] Push nhánh, tạo PR vào master, theo dõi và xử lý CI; giữ release off.
- [ ] Merge khi checks đạt, đồng bộ checkout master và xác nhận gate/run cuối.

Kết quả/bằng chứng cuối ghi tại
`docs/feature-audits/2026-11-04-full-diff-review-and-merge.md`.
Các bước GitHub được xác nhận bằng PR/run thực tế; checklist trong commit là
snapshot trước khi chính commit đó được merge, không thay cho trạng thái GitHub.
