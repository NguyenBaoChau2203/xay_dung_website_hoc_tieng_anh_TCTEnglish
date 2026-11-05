# Gỡ Whisper đã cài trên máy — 04/11/2026

Owner yêu cầu xóa luôn các thành phần Whisper đã cài. Một agent; giữ web/key/DB,
workloads và dirty work hiện có. Không commit/push/CI/deploy.

- [x] Kiểm kê runtime cache, PATH, process/autostart/task, Python/cache và Docker.
- [x] Xác định43 native artifacts109780875bytes và unused Whisper Docker image
      sha256:13d0e7c873c59a262dca621b57ad28de40e9927ac883d936dfd8459142c90db4.
- [x] Xác minh absolute paths/reparse/process trước xóa đúng artifacts; giữ6
      audio/log evidence files992647bytes. Nếu policy chặn, không đổi tool để bypass.
- [x] Gỡ đúng Docker image không có container sử dụng; không prune image/volume chung.
- [x] Kiểm lại cache/command/process/image, evidence hashes và web/key metadata.
- [x] Ghi kết quả thực tế, dung lượng và giới hạn vào audit; không gọi partial là done.

Tool xóa artifacts bị policy chặn. Owner tự chạy lệnh và xác nhận “Đã xóa thư mục”;
đã kiểm artifacts absent. Engine/model/image được gỡ xong. Sáu audio/log evidence
giữ nguyên; hai metadata state/lock1426bytes còn vì tool xóa cũng bị policy chặn,
không có binary/model/installer trong archive. Không đổi phương pháp để bypass.
