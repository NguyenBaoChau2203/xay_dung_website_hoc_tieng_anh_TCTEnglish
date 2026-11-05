# Nghiệm thu gói dữ liệu chung — 05/11/2026

Owner hủy Vercel/Supabase, cho phép triển khai inline, commit/PR/merge master và
staging; production giữ OFF. Một agent điều tra, viết, review và kiểm tra.

## Đã triển khai

- CLI riêng trước app.Run, không mở HTTP/HostedServices/paid speech: validate,
  status, export, sync, map-owner, approve. Registry19kiểu/9gốc có trường/ref cụ
  thể, không nhập bảng tùy ý hoặc dữ liệu cá nhân. Owner aliases lưu riêng.
- Version/id/hash/dependency, stable-key→ID địa phương, exact-match adoption,
  transaction cả batch, advisory lock MariaDB và row locks. Local edits khác
  bản nhận dừng; không force/reset. Root không hard-delete; child không cascade.
- Media chọn theo reference, SHA256, giới hạn25MB/tệp/200tệp, header/đuôi/path/
  reparse guards, content-addressed paths; không overwrite file khác hash.
- EF9.0.4 migration `20261105070701_AddSharedDataLedger` chỉ thêm2bảng ledger và
  unique index, kèm Designer/snapshot. Không sửa applied migration/history.
- Pull wrapperff-only, post-merge hook và startup catch-up. Bootstrap máy mới
  dùng lại guard không có user. Không rewrite User Secrets, không cài/start ASR.
- CI kiểm immutable history/format, rehearse MariaDB; artifact mang App_Data
  catalog/assets. Staging nhập sau backup/migration. Production chỉ nhập
  production.json đã review; manifest ban đầu rỗng, gate vẫnfalse.
- GitHub Production environment yêu cầu Châu review, branch policy chỉ master;
  không tự duyệt/bật gate. Agent rules chung4công cụ, lifecycle reminder và docs.

## Bằng chứng local

- Full .NET suite sau sửa logger:949pass/19skip/0fail. Các skip là acceptance
  opt-in/quota/live/scale, không gọi là đã đạt. Provider shared-data chạy riêng.
- Bổ sung round-trip đủ19kiểu với ownerID111→444, entityID lệch; media export/
  import/repair; focused SharedData +Sprint4:30/30pass sau đó (hai test mới và
  guard duplicate connection aliases red→green). Guard dùng đúng MySQL parser.
- MariaDB10.5.23/case1 disposable `tct-ci-mariadb`: baseline4migration →13,
  user checksum giữ nguyên, update lặp không đổi history; CLI sync hai lần4→0
  operations. Provider test1/1: unicode/update/conflictrollback/learnerhistory/
  sameID/usercount/passwordhash giữ nguyên. Không dùng dev DB cho failure drill.
- Docker local có backup private trước nâng schema; áp migration13 và gói mẫu
 4records, chạy lại0operations.10bảng users/goals/reading/writing/speaking/
  listening/credits giữ checksum. Không in dữ liệu/key; backup ignored.
- Python134tests,133pass/1skip/0fail; Node60/60, gồm bootstrap suite; tooling
  validator và four-host/four-event checks đạt. Thay bootstrap sync sau lần
  Python full: rerun4wrapper tests đạt. Các kiểm tra hook không chứng minh IDE
  cá nhân đã trust/invoke; teammate chạy setup một lần.
- Windows PS5.1 host failure drills: shared import chạy sau backup/migration;
  conflict giữ maintenance, old app, backup, không success marker. Existing
  production deployment regressions vẫn trong Python suite.
- EF pending-model check: none. Secret pattern scan50changedfiles:0locations;
  private backup/env/uploads bị Git ignore. Cần scan staged final trước commit.

Lần build validate chồng với test build gặp CS2012; chạy tuần tự/SkipBuild sau
build đã đạt giải quyết, không đổi code để né kiểm tra. Lỗi smoke Console.WriteLine
được sửa bằng ILogger cho CLI; launcher fixture bổ sung fake sync và kiểm tra
sync lỗi dừng web/khôi phục environment/giữ nguyên User Secrets.

## Giới hạn có chủ ý

- Đồng bộ sau xuất/merge/pull, không đồng bộ realtime hoặc copy toàn DB giữa máy.
  Unsupported entity cần adapter/review/test. Personal Goal vẫn riêng.
- Gói mẫu chỉ học liệu tổng hợp. Production snapshot không vào Git. Nội dung
  cũ khác metadata/media không được match chỉ bằng title/ID.
- Export lỗi sau DBcommit nhưng trước công bố catalog giữ .catalog-export.tmp
  và versionfolder để phục hồi có kiểm tra; không tự cleanup/overwrite ledger.
- Production path chưa chạy live vì gate OFF; không tuyên bố nghiệm thu deploy
  production. Vấn đề media nguồn thiếu/Admin404 trước đây nằm ngoài thay đổi.
- Remote CI/merge/staging run và commit marker sẽ ghi ở phần bàn giao sau khi
  GitHub hoàn tất. HTTPS renewal bị owner bỏ phạm vi; không thay thiết lập này.

## Review sau merge127: binary web Windows đang bị giữ

PR127 (headfd772d7) CI Linux/application/provider/build thành công41991931626;
tooling/Windows thành công41991931499; merge94c758a. Pull thực tế phát hiện file
Release apphost đang bị giữ: code pull xong nhưng sync build MSB3021/MSB3027;
không thay data hoặc dừng process khác. Master run41992685823 được hủy khi chỉ
job build đang chạy, chưa có job staging/production, tránh phát hành hai lần.

Follow-up: sync build riêng dưới `.appdata/local/shared-data/build`, chạy DLL
với content root nguồn và child env cũ; migration local gọi EF MigrateAsync từ
chính assembly đó. Action migrate chặn mọi target ngoài local; hosting tiếp tục
EF bundle/backup/gate. Không dùng stale DLL hay kill process để né file lock.
Regression fake build giữ web output: red→green;5wrapper tests đạt. Sync thật
với Release exe bị giữ đã thành công0operations, không nhân bản hoặc ghi key.
