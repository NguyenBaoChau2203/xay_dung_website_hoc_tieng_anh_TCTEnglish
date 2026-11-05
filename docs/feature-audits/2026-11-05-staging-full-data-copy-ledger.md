# Dữ liệu production đã được sao chép sang staging — 05/11/2026

Châu yêu cầu sao chép toàn bộ dữ liệu ứng dụng, thay cho phạm vi học liệu ban đầu.
Staging tại https://staging.tctenglish.site đã nhận snapshot production ngày
05/11/2026. Database production chỉ được đọc/xuất; không đổi dữ liệu, schema,
source, cấu hình hoặc bật deploy production. Một agent thực hiện và kiểm tra.

## Dữ liệu đã có trên staging

| Nhóm | Snapshot production / kết quả staging |
| --- | --- |
| Tài khoản | 60 tài khoản nguồn + 3 tài khoản test được giữ lại = 63 |
| Lớp học | 15 lớp, 14 thành viên, 6 yêu cầu tham gia, 35 tin nhắn |
| Từ vựng | 28 thư mục, 36 bộ, 162 thẻ, cùng bookmarks và tiến độ |
| Reading | 12 bài, 29 câu hỏi, 116 lựa chọn, lịch sử và bản dịch |
| Listening | 13 bài, 2.035 dòng transcript, 15 quiz, lịch sử; giữ 136 câu hỏi legacy |
| Speaking | 14 video, 1 playlist, 1.569 câu và tiến độ cũ |
| Writing | 20 bài nguồn + 1 bài test = 21; 153 câu nguồn + 2 câu test = 155 |
| Goals | 7 mục tiêu nguồn + 2 mục tiêu test = 9 |
| Notifications | 3.520 thông báo nguồn + 3 thông báo test = 3.523 |
| AI | 38 hội thoại + 1 hội thoại test; 129 tin nhắn + 2 tin nhắn test; request logs |
| Billing | 86 đơn, 174 sự kiện, 4 hành động admin, 18 subscriptions, 2 plans |
| Những bảng còn lại | Đã chuyển toàn bộ rows: badges, activity/time logs, attempts, histories… |

Tổng cộng **9.209 rows được chèn**. 12 badge cùng Code và cùng toàn bộ nội dung
được dùng lại, không nhân đôi. Không DELETE/TRUNCATE/REPLACE dữ liệu staging cũ.
ID trùng được ánh xạ sang ID mới và cập nhật các FK, gồm cả ConfirmedByUserId là
tham chiếu logic chưa có FK. Bảng ánh xạ chỉ lưu private.

Quyền sở hữu vẫn được giữ. Bài chung đã xuất bản hiển thị cho mọi người; bài
riêng, lớp và tiến độ vẫn thuộc tài khoản tương ứng. Sao chép dữ liệu không tự
chuyển bài riêng thành bài chung hoặc cấp Admin cho thành viên nhóm.

## Schema và kiểm tra bảo toàn

Production có 52 bảng/5 migration; staging có 57 bảng/12 migration trước copy.
Không chạy schema/history cũ đè lên bản mới. Thêm đúng 3 bảng legacy từ DDL nguồn
(`listeningexercises`, `listeningexercisesentences`, `listeningquestions`) và 3
cột nullable legacy (`EaseFactor`, `Interval`, `Repetitions`) vào learningprogress
để giữ cả dữ liệu ngoài EF. Tổng sau copy: **60 bảng**, vẫn **12 migration**.
Không sửa entity, DbContext, migration đã áp dụng hoặc snapshot của EF.

Đã restore hai backup vào database disposable mới trên Docker MariaDB10.5.23,
case1; không reset hay ghi vào tctenglish_dev. Chạy thử SQL transactional:

- Mọi giá trị của mọi row được chèn và mọi row staging có sẵn đều khớp.
- Kiểm tất cả FK trước COMMIT, không có orphan.
- Lỗi giả lập ngay trước COMMIT làm toàn bộ DML rollback.
- Chạy lại bị guard từ chối trước khi ghi, dữ liệu không bị nhân đôi.

Trên hosting: kiểm DB/user đúng staging, khóa cùng deploy.lock, backup staging
mới và app_offline riêng, kiểm SHA256 payload trước nhập. SQL chỉ INSERT và guard,
không copy production connection/config. Giao dịch commit thành công.

59/60 bảng khớp checksum ngay. Users khác vì 3 tài khoản test Online→Offline khi
IIS đóng các kết nối SignalR lúc bảo trì. Đã đối chiếu từng cột của 63 users bằng
hash, xác nhận **chỉ Status của 3 tài khoản test** thay đổi 1→0. Tái hiện chuyển
trạng thái đó trong lab cho checksum Users khớp hosting; cả 60 bảng được xác minh.
Không ghi lại trạng thái/hashes tài khoản trên hosting, không nhập lại snapshot.
Chỉ gỡ app_offline sau khi đọc đúng nội dung xác nhận thuộc thao tác này.

## Tệp và giới hạn của dữ liệu nguồn

Đã copy/đối chiếu SHA256 **27 tệp, 7.794.316 bytes**, gồm tệp uploads và ảnh ứng
dụng có trên production. Không overwrite tệp khác nội dung trên staging, không
copy source, log, config, key hoặc cả httpdocs. Một ảnh chat đã kiểm qua HTTPS:
HTTP200 và body hash khớp nguồn. URL ảnh/video ở dịch vụ ngoài giữ nguyên.

**94 đường dẫn có trong dữ liệu nhưng không có tệp tại production**: ảnh từ vựng,
avatar, ảnh lớp và một số ảnh uploads/chat/Reading. Danh sách cụ thể giữ private
vì có thể chứa tên tệp của người dùng. Không tạo ảnh giả hoặc thay dữ liệu để che
các tệp thiếu. Đây là việc cần bổ sung từ nguồn ảnh/backup gốc nếu nhóm muốn sửa
toàn bộ nội dung; bản sao database và các tệp thực sự còn tồn tại đã hoàn tất.

## Kiểm tra qua web và các việc tồn tại sẵn

- Admin/Standard/Premium đều đăng nhập lại bằng thông tin test đã lưu.
- Catalog và một trang luyện tập của cả 5 kỹ năng trả200, có dữ liệu. Học viên
  thấy 6 bài nghe chung, 12 bài đọc, 5 video nói chung; Writing có các bài nguồn
  cùng bài synthetic. Đã mở Writing nguồn có 14 câu. Không nộp bài hoặc gọi AI.
- Admin Listening có 13 rows; User/Set/Writing/Premium management trả200.
- Standard và Premium bị chặn ở Admin bằng403. Goals giữ riêng: Standard1/2,
  50%; Premium0/5, 0%.
- Hai link menu có sẵn `/Admin/ClassManagement` và `/Admin/MessageManagement`
  trả404; checkout không có controller tương ứng. Dữ liệu lớp/tin nhắn đã copy
  đủ và FK hợp lệ. Không gọi hai trang quản trị này đã nghiệm thu hoặc triển khai
  feature mới trong thao tác sao chép database.
- `/health/ready` có Basic Auth trả200; không xác thực trả401. Production root200.
- Release marker vẫn commit27c4299616930f7bb1d05e400c136630b7d1727b,
  run41969285593. Không thay source đang chạy trong lần copy này.
- Key AI riêng staging còn cấu hình. SMTP, MoMo, VNPay, cleanup/expiry/
  reconciliation và startup migrate/seed vẫn tắt. Không sao chép key local hay
  thông số kết nối/key dịch vụ của production. Không phát audio thật, thử mọi video hoặc nghiệm thu
  lại chất lượng AI/micro trong lần chuyển dữ liệu này.

## Bàn giao và vận hành

Backup, SQL, payload, mapping và evidence chi tiết nằm trong
`.appdata/local/staging/production-copy` đã bị Git ignore; hosting lưu trong
`private/tctenglish-staging-state`, ngoài document root. Snapshot chứa password
hash, dữ liệu cá nhân và payment payload nên không đưa lên GitHub hoặc gửi nhóm.
Nhóm chỉ cần dùng web staging với tài khoản riêng.

Post-deployment action chuẩn của repo staging đã được khôi phục; tác vụ copy có
dấu attempt/done để không tự chạy lại. Bản ghi done giải thích việc kiểm checksum
Users nói trên; không rerun dựa vào failure record cũ. Production gate vẫn
`PLESK_DEPLOYMENT_ENABLED=false`, staging gate true.

Đây là **snapshot một lần**, không đồng bộ hai chiều. Các lần CI/CD tiếp theo
nâng schema/code và giữ dữ liệu nhóm, không tự lấy production ghi đè staging.
Muốn refresh sau này: backup staging mới, đối chiếu các thay đổi nhóm và lập lại
mapping/guard từ trạng thái mới; không chạy lại SQL hay restore dump cũ tùy tiện.
