# Hoàn tất local Plan 1 và Plan 2 — 03/11/2026

Owner đã cho phép sửa trực tiếp các vấn đề còn lại và kết thúc hai Plan. Một
agent thực thi, giữ nhánh feature/chau-ui-performance-audit cùng toàn bộ dirty
work, dữ liệu và UI hiện hữu. Không commit/push/PR/CI remote/merge/deploy; gate false.

- [x] 1. Snapshot, môi trường Docker/MariaDB/Whisper, baseline dữ liệu và benchmark
  tổng hợp trước khi tối ưu. Không reset dev DB; chỉ dùng fixture có identity riêng.
- [x] 2. Khép lifecycle AI/quota: cancellation, transaction isolation, known usage,
  recovery có caller thật, retry/idempotence, history nhiều trang và context bounded.
- [x] 3. Hoàn thiện Speaking pagination ở API/UI: bảo toàn thao tác học/xóa/khóa,
  filters/count/continuation/error states, chống DOM injection và disabled bị kẹt.
- [x] 4. Writing SQL filters/paging/aggregates, không tải toàn bộ attempts/feedback
  vào trang danh sách. Giữ status/resume/history/receipt/credit và ownership.
- [x] 5. SMTP timeout/caller cancellation/result semantics và AutoUnlockWorker
  consistency/idempotency bằng fake transport; worker mặc định off.
- [x] 6. Profile badges/launcher usage: request scoped reuse, kiểm user isolation;
  assets pin/provenance/SRI, giảm imports trùng khi giữ cascade và initialization.
- [x] 7. Fresh app/JS tests, disposable MariaDB migrate/repeat/preservation và
  concurrency, benchmark scale với warmup5 +30samples/SQL EXPLAIN; so trước–sau.
- [x] 8. Chạy web bằng launcher chuẩn để test local; browser Standard/Premium/Admin,
  narrow/desktop, AI full/embed và speaking/writing; readiness và ASR synthetic.
- [x] 9. Review diff, cập nhật bug log/ledger/closeout. Nêu rõ các nghiệm thu cần
  mic/người thử/máy khác/hosting chưa có môi trường; không đổi chúng thành pass.

Mỗi lỗi phải có chẩn đoán và regression phù hợp trước sửa; kiểm focused theo
batch, full suite cuối. Program chỉ DI/flags cần thiết, schema mới chỉ additive
qua EF procedure khi cần. Không key/log/backup/data thật vào Git. Website giữ
gpt-6-luna/low; model IDE độc lập. Không paid cloud smoke hoặc production drill.

Đã đóng triển khai/kiểm chứng local03/11; evidence và các gate ngoài local:
[báo cáo cuối](../../feature-audits/2026-11-03-plan1-plan2-local-closeout.md).
