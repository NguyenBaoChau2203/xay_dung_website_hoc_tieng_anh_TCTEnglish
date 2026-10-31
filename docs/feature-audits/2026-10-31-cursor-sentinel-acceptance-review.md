# Review Cursor sau sentinel và acceptance — 31/10/2026

Review baseline `12ccec5..69c6815` trên `feature/bao-chau-ui-audit`.
HEAD đầy đủ: `69c6815b151e3fa8c7fae0baa7086ec425d49785`.
Một agent, review trực tiếp source và bằng chứng; không sửa ứng dụng/test được
track, không commit/push/merge hoặc deploy trong lượt review này.

## Kết luận

Factory đã sửa thứ tự verify/reset và có run identity. Tuy nhiên chưa thể đóng
toàn bộ S01/S0–S4: bootstrap PowerShell ghi đè identity, retry deadlock chưa tôn
trọng transaction của caller và silence marker của engine được chọn chưa được
adapter xử lý đúng. Có 1 finding P1 và 2 finding P2 dưới đây.

## SA01 — P1: Không retry credit bên trong transaction đã bị deadlock

- `TCTEnglish/Services/Learning/CompletionCreditService.cs:98–115` bắt lỗi
  1213/1205 và query/retry mà không phân biệt transaction sở hữu hay tham gia.
- `ApplyOnceAsync:130–133,225–235` chỉ rollback/reset khi chính service mở
  transaction. Speaking đã mở transaction tại `SpeakingController.cs:354`;
  Writing tại `WritingService.cs:231`. Đây là đường gọi bình thường của ứng dụng.
- Deadlock InnoDB rollback toàn bộ transaction. Retry riêng phần credit không
  chạy lại progress/assessment/attempt đã ghi trước nó. Ngoài ra, thay đổi tại
  `GoalsService.cs:395–397` cũng bắt 1213 rồi increment trong transaction cũ.
  Cần truyền lỗi lên chủ transaction hoặc retry toàn bộ unit với transaction và
  tracking state sạch. Lock-wait 1205 phải có chính sách riêng, không mặc định
  đồng nhất với transaction còn dùng được.
- Probe ignored `.tmp/sentinel-rereview/AmbientRetryReviewProbe.cs` chạy production
  service qua SQLite fixture và lỗi provider giả có `Number=1213`: không mô phỏng
  rollback thì gọi Goals **2 lần với cùng transaction ID**; mô phỏng server
  rollback thì query ở `ApplyAsync:107` ném `SqliteTransaction has completed`,
  che mất lỗi gốc. Hai assertion fail được lưu trong `review-probes.trx`.
- Đây là bằng chứng về nhánh điều khiển/ownership, **không phải** một lần tái hiện
  deadlock MariaDB thật. Cần regression MariaDB qua cả Speaking và Writing trước
  khi xác nhận atomicity. Chưa quan sát hoặc khẳng định dữ liệu production bị hỏng.

Tham chiếu nguyên tắc retry toàn transaction:
[InnoDB error handling](https://dev.mysql.com/doc/refman/8.4/en/innodb-error-handling.html).
Tài liệu MariaDB cũng minh họa rollback transaction khi deadlock:
[MariaDB transactions](https://github.com/mariadb-corporation/mariadb-docs/blob/main/server/server-management/install-and-upgrade-mariadb/migrating-to-mariadb/migrating-to-mariadb-from-sql-server/mariadb-transactions-and-isolation-levels-for-sql-server-users.md).

## SA02 — P2: Bootstrap PowerShell vẫn thay sentinel của run khác

- `scripts/bootstrap-mariadb-acceptance.ps1:32` chỉ yêu cầu label acceptance khác
  rỗng và purpose đúng; không so label run với `RunId`, không kiểm port binding.
- Dòng37 reuse DB bằng `IF NOT EXISTS`; dòng43 DROP sentinel, dòng50–51 INSERT
  identity mới. Vì vậy chạy lại với DB/run không khớp có thể biến DB cũ thành
  target được factory cho phép DELETE, dù C# bootstrap không UPSERT nữa.
- Probe PowerShell 5.1 shadow toàn bộ `docker`, không gọi Docker thật: container
  label `foreign_run`, yêu cầu `new_run` vẫn được báo bootstrapped; SQL sinh ra có
  DROP sentinel và INSERT new_run, không đọc identity trước write. Artifact:
  `.tmp/sentinel-rereview/bootstrap-probe.log`, `bootstrap-generated.sql`.
- Sửa đường PowerShell thực sự được vận hành: DB mới phải có lifecycle ownership;
  DB đã tồn tại chỉ reuse khi identity khớp; mismatch/missing identity phải reject
  trước write. Kiểm exit code Docker/client, không in thành công sau lỗi native.
  Test C# helper không chứng minh script PowerShell tuân theo cùng contract.

## SA03 — P2: Silence marker bị coi là transcript hợp lệ

- Notes ASR mới ghi silence WAV trả `text=[BLANK_AUDIO]`, `no_speech_prob≈0.905`.
- `WhisperHttpTranscriber.cs:207–235` chỉ nhận NoSpeech khi text rỗng; marker này
  có ký tự nên kết quả thành `Ok`, transcript `[BLANK_AUDIO]`.
- Probe tái dựng payload từ text/probability đã ghi, HTTP giả nhưng dùng adapter
  thật: expected `NoSpeech`, actual `Ok`. Đây là ca thứ3 fail trong
  `.tmp/sentinel-rereview/review-probes.trx`. Không phải replay đầy đủ response
  engine gốc vì Cursor chưa lưu raw response tương ứng trong tài liệu.
- Không dùng riêng HTTP200 làm acceptance. Cần lọc marker không phải lời nói theo
  contract engine đã pin, giữ transcript speech thật và kiểm qua assessment/
  progress để silence không tạo receipt có thể dùng/thưởng. Chưa chứng minh có
  lỗi cấp XP từ marker này; finding xác nhận sai status/transcript.

## Phần được cải thiện và giới hạn bằng chứng

| Phần | Đánh giá |
| --- | --- |
| Factory verify trước reset | Đã sửa đúng thứ tự, same connection, run id, FK finally |
| Bootstrap C# | Không overwrite purpose/run; không thay cho review PowerShell |
| Race test | Tăng5 lượt; thêm daily XP và unique outbox key assertions |
| Credit error classification | Đã yêu cầu unique signal thay vì mọi lỗi constraint |
| ASR binary/model | Có binary và hash/model pin; notes ghi cold/warm, RAM |
| G03 provider matrix | Chưa đủ: chỉ dual-completion dùng MariaDbAcceptanceFactory; NotificationWorkerBatchTests và SpeakingImportJobLifecycleTests dùng TestWebApplicationFactory/SQLite |
| G01 ASR application E2E | Chưa đủ: silence HTTP probe không thay WAV→assessment→receipt→progress→credit/reload; còn SA03 |
| G04 UI | Login screenshot/Tab và public/302 matrix; chưa25 view gốc AU/CO/SP/WR/GO/AI/AD sau đăng nhập |
| G06 staging | Chỉ tới guard thiếu backup client; chưa backup/migrate/copy/health/failure/restore |

Ledger `2026-10-30-sentinel-acceptance-ledger.md:9–11` ghi S1/S2/G01 Done vượt quá
bằng chứng trên. Plan cũ đánh toàn bộ checkbox xong trong khi UI/staging còn
partial/blocked. Cần chỉnh báo cáo theo từng case/provider; không đóng các gate
bằng cùng tổng số test. Account bcrypt synthetic và backup client local là các
bước setup cần giải quyết, chưa mặc nhiên là đầu vào phải lấy từ owner.

## Kiểm tra lần review này

| Check | Kết quả |
| --- | --- |
| App Debug build, output riêng | 0 errors, 35 warnings |
| Focused guard/completion/goals/worker/import tests | 73 pass, 0 skip |
| Full tracked .NET suite | 717 pass, 1 skip, total718; MariaDB acceptance skip |
| Python scripts suite, Windows | 55/55 pass |
| Agent tooling validator | Pass; không chứng minh app/UI đã nghiệm thu |
| Review-only regressions | 3 fail có chủ đích: 2 transaction ownership, 1 silence |
| PowerShell bootstrap probe | Xác nhận overwrite path; Docker hoàn toàn giả |
| Standard test build sau probe | Đã build lại không include review-only probes |
| Diff whitespace | Pass |

Các log/TRX dưới `.tmp/sentinel-rereview/`, ignored. Docker daemon hiện không kết
nối được (missing `dockerDesktopLinuxEngine` pipe), nên review không chạy lại
provider. Local718/718 trong Cursor ledger là báo cáo trước đó, không phải kết
quả được review tái xác nhận. Không SQL dev/hosting, không tạo/reset DB thật.

CI [41099068127](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/41099068127)
đã kiểm qua GitHub: SHA `dc92bcc`, build success, **717 pass/1 skip**;
Python deploy checks12 pass/2 Windows-only skip trên Linux. `69c6815` chỉ cập nhật
docs so với SHA này; runtime diff bằng0. Job release skipped và biến
`PLESK_DEPLOYMENT_ENABLED=false`; không có release hosting từ run này.

Lệnh khởi động lại whisper-server loopback8091 để replay trực tiếp bị automatic
approval review từ chối với lý do `blocked by policy`; không thử đường vòng.
Do đó ASR review dùng fixture tái dựng và source, không tuyên bố live replay.

Plan tiếp: [transaction/bootstrap/ASR](../superpowers/plans/2026-10-31-transaction-bootstrap-asr-remediation-plan.md).
Prompt thực thi: [Cursor Auto](../../.ai/prompts/cursor-auto-transaction-bootstrap-asr-remediation.md).
