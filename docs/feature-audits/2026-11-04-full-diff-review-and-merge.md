# Review toàn bộ Git changes trước khi merge — 04/11/2026

Owner cho phép commit theo nhóm, push, PR và merge sau review. Một agent thực
hiện trên `feature/chau-ui-performance-audit`, baseline `210bf2d`. Repository
dùng `master`; không có nhánh `main`. Không bật deploy production.

## Phạm vi và kết luận review

Kiểm kê cả diff tracked, file mới, file xóa và dependencies; xem lại source,
contracts và bằng chứng các vòng review trước, bổ sung kiểm chứng mới bên dưới.
Các nhóm thay đổi gồm:

- UI Account/Profile/Settings, AI full/embed, Speaking/Writing, Admin và shared
  navigation: labels/focus/reduced-motion, submit guard, filter/paging/load older.
- Logic/quota AI: reserve/settle/recovery, context theo token budget, ownership
  history, SignalR cancel và không gửi HTTP retry sau outcome chưa rõ.
- SQL catalogs, profile badges chỉ đọc, SMTP bounded/cancellable và conditional
  unlock trước notification; worker mặc định vẫn off.
- Hai bảng quota mới và đủ3 migration + Designer/snapshot; không chỉnh migration
  đã áp dụng. Up chỉ tạo bảng/bỏ index dư; không drop bảng/cột hiện hữu. Down của
  migration tạo bảng có drop bảng mới theo EF convention, không được chạy trên
  dữ liệu cần giữ nếu chưa có phê duyệt/backup. Không chạy Down trong phiên này.
- OpenAI speech thay Whisper, key resolver/metadata status, audio-window limits,
  access/admission trước provider, giữ completion/replay. Ordinary startup chỉ
  MariaDB + web; không tự gọi paid speech hoặc chạy engine local.
- Launcher/tooling/onboarding, child TEMP/TMP trên ổ checkout, cảnh báo kế hoạch
  Whisper cũ, cache/storage audit và host-key incident đã được ghi trung thực.

Finding mới cần sửa trước merge: **DOMPurify3.2.6 có advisory đã công bố** trong
bundle dự kiến thêm. Đã cập nhật3.4.17 từ signed release commit upstream,
đối chiếu byte hash với npm, thay references full/embed và license/provenance.
Thêm regression kiểm toàn bộ vendor hashes/manifest/Razor paths; `.gitattributes`
giữ byte gốc qua Windows/Linux. Không khẳng định đã tái hiện exploit cụ thể trong
ứng dụng. [Advisory](https://github.com/advisories/GHSA-v8jm-5vwx-cfxm),
[upstream release](https://github.com/cure53/DOMPurify/releases/tag/3.4.17).

Trong phạm vi review/kiểm chứng đã thực hiện, chưa còn finding P0/P1 được xác nhận
chặn merge sau bản sửa này. Điều đó không đồng nghĩa toàn hệ thống không còn bug,
đã kiểm toán bảo mật toàn diện hoặc đã đủ điều kiện deploy production.

## Kiểm chứng mới, không cộng dồn ledger cũ

Artifacts được ignore trong `.tmp/release-review/`.

| Check | Kết quả |
| --- | --- |
| Full .NET Release | 890 pass /0 fail /12 skip, tổng902; full.trx |
| Build Release sau đổi asset reference | 0 error;47 warning nullable/analyzer hiện có; build-final.log |
| EF9.0.4 pending-model check | không có model changes chưa migration; model.log |
| MariaDB10.5.23 upgrade/repeat | 1 pass, giữ fixture user/history/quota, đủ3 migration; upgrade.trx |
| MariaDB quota/completion/notification concurrency | 6 pass /0 fail /0 skip; mariadb.trx |
| JavaScript toàn bộ test_*.cjs | 44 pass /0 fail, gồm setup/policy/chat/Speaking/recorder; node.log |
| Python final gồm vendor checks | Ran101,100 pass /1 skip; python-final.log |
| Vendor integrity/path regressions | 2 pass; hashes và Razor references hợp lệ |
| agent_tooling.py | verified working tree; không thay thế app checks |
| check_team_hooks.py |4 native host/event contracts +malformed input đạt; không khẳng định IDE tự invoke |
| Chromium focused production hydration | Marked +DOMPurify3.4.17 +ai-chat.js; bold tiếng Việt giữ đúng, không script/onerror/javascript URL,0 page error |
| Gitleaks8.30.1, candidate snapshot | 0 secret findings,~3.24MB; executable đối chiếu checksum upstream, output redacted100% |
| Kiểm private artifact paths/token patterns | không có SQL/backup/key/runtime store trong candidate; một URL fixture giả được review riêng |
| git diff --check | đạt; chỉ cảnh báo LF/CRLF của các file UI |

MariaDB được tạo mới trên loopback3338 bằng container/run ID riêng với sentinel,
image pin đúng hosting. Không dùng connection dev/hosting, không reset dev DB,
không import backup thật. Test thắng race có1 completion credit; worker dedup
batch201/450 và lease; quota overlapping scopes/historical backfill đạt. Đã
kiểm label run trước khi xóa đúng container disposable và volume của phiên.

12 skip của full suite vẫn là skip. Provider tests trên được bật riêng có guard;
không gọi paid text/speech, SMTP thật hoặc tạo mic recording của owner. Chromium
probe chỉ kiểm pipeline render chat cô lập; không phải browser acceptance toàn
bộ site. Những lượt UI/scale trước đó vẫn là evidence lịch sử của closeout03/11.

## Bảo mật và giới hạn vận hành

User Secrets/OPENAI_API_KEY, backup SQL/ZIP, Docker volume, .appdata/.tmp,
recording, native trust và cache máy không được đưa vào Git. Không in giá trị
key trong scan/log. Credential directory không thuộc cache relocation được phép.

**AI key local vẫn cần owner nhập lại** bằng `scripts/set-local-ai-key.ps1`, như
owner đã chủ động hoãn. Không lấy key từ chat/copy sang repo; không coi offline
test xanh là account/quota/model access đã được nghiệm thu. File EFS ở D vẫn
được giữ riêng, không dùng làm active store hoặc giảm bảo vệ để đọc.

Whisper runtime đã retired; old artifact archive chỉ nằm trong ignored data
trên D. Khác biệt cấu hình speech mới phải được cài đúng môi trường trước một
release sau này. Worker unlock default-off và các technical debt đã ghi trong
known-issues không bị coi là đã đóng bởi PR này.

## GitHub/production

Trước push đã đọc trực tiếp `PLESK_DEPLOYMENT_ENABLED=false`. Workflow release
còn điều kiện chỉ master +gate true. Push/PR/merge được owner cho phép; CI là
build/test/publish artifact, không tự mở gate. Snapshot tài liệu này được chuẩn
bị trước merge; kết quả CI, PR/head/merge SHA và release-job skipped được xác
nhận trực tiếp trên GitHub/PR và báo lại owner sau khi thao tác hoàn tất.
