# Sao chép dữ liệu production sang staging — 05/11/2026

Châu yêu cầu nhóm dùng dữ liệu hiện có trên production để xem các bài nghe, đọc
và học liệu khác hiển thị thế nào, rồi tiếp tục phát triển. Một agent thực hiện.
Production chỉ SELECT/SHOW/export; không đổi source/config/schema/data tại đó.
Châu đã mở rộng yêu cầu: sao chép toàn bộ dữ liệu ứng dụng, bao gồm Users,
payment, chat, lịch sử/tiến độ và bài riêng. Snapshot chứa thông tin cá nhân,
password hash và payload: chỉ lưu private/ignored, không đưa vào Git hoặc log.
Giữ tài khoản test/key/config staging; kiểm SMTP/payment thật và worker có tác
động ngoài hệ thống đang tắt. Production vẫn chỉ đọc. Đây là bản sao tại thời
điểm xuất, không phải replication hoặc kết nối chung với production.

- [x] Kiểm inventory và quan hệ toàn bộ production/staging, schema hiện hành,
      quyền/owner; xác định ảnh/audio là URL ngoài hay file uploads.
- [x] Lưu snapshot đầy đủ và backup staging mới trong private/ignored;
      lập danh sách bảng/cột, số lượng và media, không ghi nội dung riêng vào Git.
- [x] Chuẩn bị import có guard đúng DB, map khóa ngoại/owner và idempotency; không
      copy migration history/schema production cũ hoặc đè database staging mới.
- [x] Chạy thử trên MariaDB10.5.23 disposable, kiểm quan hệ/đếm/hash và chạy lại,
      giữ users/goals/progress/config hiện có. Không reset Docker local.
- [x] Copy media ứng dụng cần thiết, gồm avatar/attachment/recordings nếu tồn tại;
      kiểm đích/path/hash, loại trừ config/key/source/log và toàn bộ httpdocs.
- [x] Áp dữ liệu chỉ staging trong transaction/batch đã thử; backup và recovery
      có sẵn, kiểm no orphan và snapshot/import markers. Không tự overwrite edits.
- [x] Mở catalog/practice của Reading/Listening/Speaking/Writing/Vocabulary bằng
      tài khoản nhóm, kiểm ảnh/media và dữ liệu chung, progress riêng còn nguyên.
- [x] Ghi audit/runbook kết quả thực tế và giới hạn media vốn đã hỏng ở production.
      Không push/merge/deploy production; thao tác Git mới theo quyền của task.

Nghiệm thu: staging có toàn bộ dữ liệu production cùng bài con/phụ đề/câu hỏi,
tài khoản test vẫn dùng được, key giữ riêng, readiness200, production gatefalse.
Đồng bộ là snapshot nội dung có kiểm soát, không có live replication hai database.

## Phương án đã nghiệm thu trên lab

Giữ toàn bộ rows staging và chèn snapshot production với bảng ánh xạ ID; không
DELETE/TRUNCATE/DROP/REPLACE dữ liệu có sẵn. Map cả ConfirmedByUserId của đơn hàng
(tham chiếu logic chưa có FK). 12 badge cùng Code/nội dung trùng chính xác được
dùng lại. Thêm 9.209 rows, giữ 3 tài khoản test và 12 migration hiện hành.

Phục hồi thêm 3 bảng legacy ngoài EF (listeningexercises,
listeningexercisesentences, listeningquestions) bằng DDL nguồn và 3 cột nullable
legacy của learningprogress (EaseFactor, Interval, Repetitions). Chỉ bổ sung vào
staging để giữ toàn bộ dữ liệu cũ; không đổi entity/model hoặc tạo baseline giả.
RepetitionCount đã có trong schema hiện hành. Không chạy schema cũ đè schema mới.

MariaDB10.5.23: đối chiếu từng giá trị của mọi row được chèn và mọi row staging
cũ; tất cả FK hợp lệ trước COMMIT. Lỗi giả lập ngay trước COMMIT hoàn tác dữ liệu;
chạy lại bị guard từ chối, không nhân đôi. Chuẩn bị checksum 60 bảng để đối chiếu
trên host trong lúc app_offline của riêng thao tác còn hiệu lực.

## Hoàn tất trên hosting

Đã nhập 9.209 rows, có 63 users/60 bảng/12 migration. Đối chiếu 60 bảng sau khi
giải thích đúng chuyển Online→Offline của 3 tài khoản test do SignalR ngắt khi
bảo trì; không sửa lại users trên host hoặc re-import. Copy 27 tệp có thật, ghi
94 tệp nguồn thiếu. Web mở lại: ready200, anonymous401; login3roles, catalog và
practice5kỹ năng đạt smoke, Goals vẫn1/2 và0/5. Hai link Admin Classes/Messages
404 tồn tại ở code hiện hành, đã ghi known issues; dữ liệu hai nhóm đã copy đủ.

Chi tiết: [biên bản sao chép](../../feature-audits/2026-11-05-staging-full-data-copy-ledger.md).
