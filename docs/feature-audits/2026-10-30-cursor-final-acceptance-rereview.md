# Review độc lập sau Cursor — nghiệm thu 29–30/10

Ngày: 30/10/2026 (UTC+7). HEAD `186fc8a`, nhánh `feature/bao-chau-ui-audit`.
Diff review: `54f0c14..186fc8a` (22 file). Một agent đọc source, kiểm tra diff,
chạy build/tests/probe và đối chiếu báo cáo Cursor. Không sửa app/test/workflow,
không commit/push/merge/deploy, không chạy fixture xóa dữ liệu để làm review.

## Kết luận

**Chưa thể nghiệm thu hoàn tất.** Cursor đã sửa đúng các ca cụ thể ban đầu về
dấu âm/tiền tệ/phần trăm, vị trí marker và schema ASR; có thêm regression hữu ích.
Tuy nhiên vẫn còn **5 phát hiện cần xử lý**. REV01 mới chỉ đóng với một nhóm ký
hiệu; REV04 mới xử lý việc release đã tồn tại, chưa bảo đảm toàn bộ identity và
readiness của lần phát hành. Bằng chứng concurrency chưa đáng tin cậy.

Cursor có ghi Not run/Partial cho nhiều gate; giữ sự phân biệt này. “Đã làm xong”
không tương ứng với bảng nghiệm thu trong ledger hiện tại.

## CR01 — P1: Writing vẫn auto-pass một số thay đổi nghĩa của dấu và số

Vị trí: `TCTEnglish/Services/Writing/WritingAssessmentPolicy.cs:153`–165,
`IsMeaningBearingSymbol` dòng 171–174. Caller vẫn dùng quyết định Accepted để
lưu progress/completion như review trước.

Probe compile trực tiếp source mới (không dùng assembly Release cũ) cho kết quả:

| Tham chiếu | Câu gửi | Actual |
| --- | --- | --- |
| `The temperature is -5 degrees.` | `The temperature is 5 degrees.` | NeedsReview — ca cũ đã sửa |
| `I paid $50.` | `I paid €50.` | NeedsReview — ca cũ đã sửa |
| `The discount is 5%.` | `The discount is 5.` | NeedsReview — ca cũ đã sửa |
| `The value is <5.` | `The value is >5.` | **Accepted, passed=true** |
| `The distance is 1,000 meters.` | `The distance is 1.000 meters.` | **Accepted, passed=true** |

Allowlist ký hiệu vẫn bỏ `<`/`>`, và đổi cả comma/dot giữa hai chữ số thành `.`.
Trong ngữ cảnh English, `1,000` và `1.000` có thể là nghìn và một. Không được
khẳng định tương đương chỉ từ cách viết; chưa có locale/đáp án được duyệt thì
NeedsReview. Corpus mới cố định một ca `1,5` = `1.5` không đủ cơ sở để gộp mọi
numeric separator. Đây là lỗi đánh giá false-pass còn lại, không phải phản đối
thiết kế exact/local nói chung.

Bổ sung regression policy + API/persistence cho phép so sánh và numeric format,
giữ trường hợp chắc chắn tương đương. Không auto sửa XP lịch sử.

## CR02 — P1: Fixture acceptance có thể xóa nhầm database khi env trỏ sai

Vị trí: `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceFactory.cs:18`–37,
`InitializeAsync` dòng 41–80; nguồn env ở `MariaDbCompletionConcurrencyTests.cs:21`.

Connection string được dùng trực tiếp với UseMySql. Factory không kiểm tra
loopback, database acceptance riêng, identity sentinel hoặc principal trước khi
chạy DELETE hàng loạt trên Users/Sets/Cards/... Tên env/comment nói “isolated”
không tạo ràng buộc thực thi. Nếu người chạy đặt env bằng connection dev/hosting
có quyền, fixture có thể xóa dữ liệu ngoài dự kiến. Khối catch(Exception) còn
nuốt lỗi cleanup rồi tiếp tục seed.

Đây là phát hiện từ luồng source; review không thử xóa database dev/production.
Cần guard offline trước network và xác minh identity trên đúng connection trước
DML, test từ chối dev/hosting, lifecycle acceptance DB riêng. Không coi container
đã dùng đúng trong một phiên là bảo đảm cho những lần chạy sau.

## CR03 — P2: Test dual-completion không bảo đảm tạo race

Vị trí: `TCTEnglish.Tests/MariaDbCompletionConcurrencyTests.cs:31`–46.

`WorkerAsync("a")` chạy đồng bộ tới `Barrier.SignalAndWait(10s)` trước khi lần
gọi `WorkerAsync("b")` trong Task.WhenAll được đánh giá. Barrier thứ nhất timeout;
bool trả về bị bỏ qua. Không có assert hai bên đã đến trước khi vào critical path.
Probe không DB giữ cùng call order/async shape, giảm timeout xuống 250ms:
`a.barrierSucceeded=false`, `b.barrierSucceeded=false`; b bắt đầu sau a timeout.

Vì vậy test pass chỉ chứng minh một lần credit trong lịch chạy có thể gần tuần tự,
không chứng minh race đã được exercise. Test còn chấp nhận bất kỳ !Applied nào
và chỉ đếm credit; chưa assert loser là replay hợp lệ, XP một lần hoặc outbox đúng.
Không suy từ đó production có lỗi race; bằng chứng hiện tại chưa đủ.

Sửa async rendezvous/task scheduling, fail khi timeout, chứng minh hai context/
connection cùng tới điểm tranh chấp. Sau CR02 mới chạy lại provider tests.

## CR04 — P2: Reuse release bỏ qua digest thiếu và không resolve tag SHA

Vị trí: `scripts/publish-plesk-release.py:43`–48, `ensure_release` dòng 61–72.

- Khi remote digest vắng, điều kiện `if remote_digest and ...` cho qua mà không
  tải/đối chiếu asset. Probe offline: remote asset chỉ có tên vẫn được reuse.
- Khi `target_commitish` là branch, `release_target_matches` chỉ cần assets khác
  rỗng; không resolve actual tag/annotated tag tới SHA. Probe branch `master`
  cũng được chấp nhận mà không có thao tác resolve tag.

Đây vi phạm yêu cầu reuse đúng SHA/digest. Guard checksum phía Plesk có thể ngăn
bytes sai được áp dụng, nhưng không làm bước xác minh publisher trở thành đúng;
release có thể được trigger rồi thất bại muộn vì asset khác. Tests hiện mock luôn
asset_sha256 nên chưa phủ đường thiếu digest.

Cần resolve tag thực, xác minh digest bằng metadata hợp lệ hoặc tải/hash asset
qua HTTPS được kiểm tra; thiếu bằng chứng thì dừng trước update_branch/webhook.
Không clobber/xóa tag hoặc ghi đè asset để vượt qua.

## CR05 — P2: Readiness có thể xác nhận marker cũ của cùng commit

Vị trí: `scripts/publish-plesk-release.py:110`–125, gọi từ 155–157.

`wait_for_deployed_commit` chỉ so commit và health200; bỏ runNumber dù manifest/
marker có trường đó. Probe HTTP offline đưa marker `{commit:sha, runNumber:1}`
và health200: helper trả true ngay; nó không được truyền expected run hiện tại.
Một workflow mới phát hành lại cùng SHA có thể báo success trong khi Plesk đang
chạy hoặc sau đó fail. Header cache sai cũng chỉ bị bỏ qua ở nhánh `pass`.

Ngoài ra, webhook timeout ném SystemExit ngay, trước reconcile/poll: remote có
thể đã nhận POST nhưng publisher chưa kiểm tra marker đúng của lần đó. Đây là
phần REV04 chưa được test đầy đủ. Không cần auto retry migration để xử lý timeout.

Sửa release identity tuple (commit/run/digest hoặc tương đương), cache handling,
reconcile khi webhook response mơ hồ, và test stale same-SHA/different-run.

## Trạng thái các gate và tính chính xác báo cáo

| Hạng mục | Đánh giá sau review |
| --- | --- |
| REV01 cũ | Ba ca ban đầu pass regression; chưa đóng tổng quát vì CR01 |
| REV02 | Vị trí wwwroot + HTTP/no-store đã có regression; full enabled release chưa diễn tập |
| REV03 | Các schema lỗi cũ đã chuyển invalid_schema; focused tests pass |
| REV04 | Có reuse, nhưng CR04/CR05 cần sửa trước khi đóng |
| G01 ASR thực | Not run; notes chỉ ghi candidate v1.8.x, chưa pin/artifact. Việc pin chưa có là việc cần làm, không phải bằng chứng không thể làm trong phạm vi model <=500 MiB đã giao |
| G02 Writing corpus | 40 case có labels và chạy; chưa có các biên CR01; optional engines Deferred là hợp lệ |
| G03 MariaDB | Partial; cần CR02/CR03 trước khi tin dual-completion. Notification/import/lease matrix Not run |
| G04 UI | Chỉ matrix nhóm, chưa đủ 25 dòng view; chưa khởi tạo acceptance host/browser |
| G05 CI | Đã xác minh SUCCESS trên đúng source SHA ff7f703; **694 pass, 1 skip**, không phải mọi provider test đã chạy |
| G06 release/recovery | Full enabled Windows staging Not run; production gate false, rotation/provider còn thiếu |

“Chưa có host 5110” là bước setup acceptance còn chưa làm, không tự thành blocker
do owner. Plan cũ đã giao quyền DB/container synthetic riêng; không phải dừng app
5100 hay chạm volume dev để tạo acceptance host. Nếu thiếu tool/quyền cụ thể,
cần ghi command và lỗi thực, không chỉ đánh Not run chung chung.

## Kiểm chứng của lượt review này

- Fresh app Debug build: 0 lỗi, 35 warning sẵn có, 24,70s.
- Focused tests Writing local/corpus/persistence, Whisper và marker: **84/84 pass**.
- Deploy Python/PowerShell checks hiện có: **10/10 pass** trên Windows.
- Tooling validator và toàn bộ Python/script tests: **51/51 pass**; ba tài liệu
  review/plan/prompt kiểm tra links, code fences và whitespace thành công.
- Probe source thật tái hiện CR01; probe scheduling không DB tái hiện CR03;
  publisher/HTTP mocks tái hiện CR04/CR05. Tất cả trong `.tmp/cursor-final-rereview/`.
- CR02 là source-evidenced risk; không execute cleanup để chứng minh bằng mất dữ liệu.
- CI [41085699736](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/41085699736):
  source SHA ff7f703, build/MariaDB migration/publish pass; app tests **694 pass,
  1 skipped** (MariaDbCompletionConcurrencyTests); deploy script suite **8 pass,
  2 skipped** (Windows-only). Release job skipped. HEAD186fc8a chỉ thêm ledger docs.
- Không chạy lại full suite khi chưa sửa runtime code và đã xác minh CI cùng tree.
- Gate GitHub kiểm tra vẫn false. Không chạy DB cleanup/ASR/production deploy.

Plan sửa tiếp: [round 2](../superpowers/plans/2026-10-30-final-acceptance-round2-plan.md).
Prompt Cursor: [round 2](../../.ai/prompts/cursor-auto-final-acceptance-round2.md).
