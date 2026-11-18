# Step 01 — Contact backend execution ledger

Ngày thực hiện: 2026-11-16. Chế độ: `IMPLEMENT_LOCAL`, một agent.
Cập nhật kiểm chứng/bàn giao khi tiếp tục ngày 2026-11-17.
Nhánh giữ nguyên: `fix/writing-goals-profile-polish`.
HEAD baseline: `2a20c6185c38b9f975ddf555b7dc73147e2bf1ca`.

## Baseline và nguyên nhân

Working tree ban đầu chỉ có hai thư mục chưa theo dõi:
`.ai/prompts/contact-refresh/` và
`docs/superpowers/plans/2026-11-16-contact-refresh/`. Giữ nguyên nội dung gốc.
Đã đọc prompt, README/bước 01, skill quy trình/engineering/security, bug memory
SMTP Unknown và renderer email 11/11. CodeGraph được dùng trước để đọc nguồn;
chỉ đọc trực tiếp phần bị lược bỏ hoặc văn bản vừa sửa. Không init/rebuild index.

Các lỗi xác nhận từ nguồn và regression:

1. POST đổi chủ đề giả thành `general`, vẫn gọi SMTP.
2. SMTP thất bại vẫn redirect, làm mất nội dung form.
3. `bool` làm mất phân biệt giữa thư bị từ chối và mất xác nhận sau dispatch.
4. Thiếu giới hạn gửi/replay phía server; JS disabled button không đủ.
5. ModelState giữ query Subject chưa chuẩn hóa, ghi đè lựa chọn general trên GET.
6. Preheader Contact bị encode hai lần, CR đơn trong nội dung không thành ngắt dòng.
7. Thông tin liên hệ/giờ làm việc/cam kết 24 giờ trong phần Contact chưa có xác minh.

## Thay đổi đã thực hiện

- `HomeController`: giữ anonymous GET/POST, appeal links và antiforgery; giới hạn
  request/form/header riêng Contact; PRG chỉ khi SMTP accepted; render lại model
  theo trạng thái, giữ nội dung và nonce đúng quy tắc; reconcile ModelState.
- `ContactFormViewModel`: trim trước validate, giới hạn trường cũ, email đơn,
  subject allowlist, CR/LF rejection, hidden ContactNonce, State không bind từ POST
  và thông điệp tiếng Việt tương ứng.
- `IAppEmailSender` / `SmtpAppEmailSender`: API Contact có kiểu và default fail
  closed tương thích fake cũ; routing cấu hình chặt; reuse transport/deadline/
  renderer cũ; Unknown bảo thủ, không retry tự động; không đưa credentials ra view.
- Mới `Services/Contact/ContactOperationGate.cs`, `ContactInput.cs`: nonce bảo vệ
  15 phút, 3 operation/10 phút/client, 4096 operation/2048 client; khóa admission
  đồng thời, cache hết hạn và không chứa raw form PII, không evict active send.
- `Views/Home/Contact.cshtml`: thêm nonce/state và maxlength; bỏ thông tin mẫu,
  cam kết phản hồi chưa xác minh trong phần Contact. Không thiết kế lại CSS/JS.
- `TctEmailRenderer`: Contact preheader encode một lần, giữ CR/LF/CRLF dễ đọc.
  Fixture `docs/design-system/email-preview/support-contact-edge.html` được cập
  nhật bằng test renderer thật; chỉ thay preheader tương ứng.
- Mới `ContactBackendHttpTests.cs`, `ContactMailAndGateTests.cs` với fake SMTP,
  HTTP TestServer, fake thời gian và concurrency barrier; không gọi SMTP thật.
- Contract độc lập cho Gemini: [backend-contract.md](backend-contract.md).

Không sửa persisted entity/DbContext/mapping: không cần migration. Không sửa nội
dung DB/shared packages. Không sửa Program.cs, appsettings, project files, shared
layout/style hay hosting; không đổi secret. Không commit/push/CI/deploy.

## Kiểm chứng

Lệnh Release mặc định ban đầu bị khóa apphost bởi web local PID 9932. Không dừng
tiến trình đó. Build/test dùng `--property:OutputPath=bin/ContactVerification/net9.0/`
để không ghi vào output đang chạy. Đây vẫn là cấu hình Release/net9.0.

Regression đỏ được chạy với ba test đầu tiên trên bản nguồn HEAD xuất bằng
`git archive` trong `.tmp/contact-refresh/baseline/`, không tạo/switch nhánh hay
worktree: **2 failed, 1 passed**. Hai lỗi là unknown subject và redirect khi SMTP
từ chối; antiforgery đã đúng trên baseline. Log tạm:
`.tmp/contact-refresh/baseline-test.log`.

Trong lượt mở rộng, test bắt được preheader encode hai lần, lựa chọn GET general
bị ModelState ghi đè và giới hạn parser quá thấp cho tiếng Việt percent-encode;
đã sửa nguyên nhân tương ứng, không nới giới hạn server toàn cục.

Build cuối:

```text
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/
```

Kết quả: **0 errors, 42 warnings** ở nguồn hiện có ngoài Contact. Log:
`.tmp/contact-refresh/build-release.log`.

Bộ tập trung trước vòng full suite:

```text
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/ --filter "FullyQualifiedName~ContactBackendHttpTests|FullyQualifiedName~ContactMailAndGateTests|FullyQualifiedName~SmtpTransportFakeTests|FullyQualifiedName~TctEmailDesignSystemTests|FullyQualifiedName~AccountReviewRepairTests|FullyQualifiedName~UnlockWorkerRegressionTests|FullyQualifiedName~AccountFunctionalCompletionTests|FullyQualifiedName~Notification" --logger "console;verbosity=minimal"
```

Kết quả: **124 passed, 0 failed, 2 skipped**. Hai skip là MariaDB Notification
acceptance opt-in. Sau đó bổ sung CR/LF Subject rejection vào cùng test biên;
full suite bên dưới kiểm tra nguồn cuối. Log: `.tmp/contact-refresh/focused-final.log`.

Full Release suite trên nguồn cuối:

```text
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/ --logger "console;verbosity=minimal" --logger "trx;LogFileName=full-release.trx" --results-directory .tmp/contact-refresh/results
```

Kết quả: **1670 passed, 2 failed, 53 skipped**, 1725 tổng, khoảng 7 phút 50 giây.
TRX xác nhận **42/42 test Contact passed**. Không gọi full suite là xanh.
Hai lỗi trong lượt toàn bộ:

- `CursorReviewProbes.SignalR_StopOnSameConnection_CancelsWaitingProviderBeforeProviderCompletes`:
  provider chưa hủy trong cửa sổ thời gian của assertion.
- `AiCancelAndStopIntegrationTests.HttpSend_AfterCancelCleanup_AllowsNextTurn`:
  chờ cancellation gặp TimeoutException thay vì OperationCanceledException.

Đọc hai test và boundaries liên quan cho thấy chúng dùng fake AI và kiểm tra
AI/SignalR cancellation, không gọi Contact. Chạy lại riêng đúng hai test bằng
`--no-build --filter "FullyQualifiedName~SignalR_StopOnSameConnection_CancelsWaitingProviderBeforeProviderCompletes|FullyQualifiedName~HttpSend_AfterCancelCleanup_AllowsNextTurn"`:
**2 passed/0 failed** trên nguồn hiện tại và **2 passed/0 failed** trên baseline
HEAD đã xuất. Điều này chứng minh lỗi không tái hiện khi chạy riêng; chưa xác định
nguyên nhân gốc trong full suite, không khẳng định một lỗi nền đã được sửa.
Không sửa AI/SignalR hay nới timeout ngoài phạm vi Contact.

Trong lượt full và hai lượt rerun, SMTP host được đặt thành whitespace chỉ trong
môi trường tiến trình kiểm thử rồi phục hồi bằng `finally`, để mail mặc định
fail closed; config in-memory của fake SMTP vẫn riêng. Không viết secret/config
store. Các live AI/speech acceptance vẫn skip theo opt-in hiện có.

Logs tạm: `.tmp/contact-refresh/full-release.log`,
`.tmp/contact-refresh/results/full-release.trx`,
`.tmp/contact-refresh/unrelated-isolated-current.log`,
`.tmp/contact-refresh/unrelated-isolated-baseline.log`.

Đã review diff toàn bộ; bỏ các thay đổi chỉ khác CRLF/LF do test xuất gallery.
Chỉ giữ fixture Contact edge có thay đổi nội dung đúng với sửa preheader.
`git diff --check` được kiểm tra ở lượt bàn giao cuối.

Phạm vi chứng minh của các test Contact:

| Nhóm | Bằng chứng tự động |
| --- | --- |
| Access/CSRF | Anonymous GET, appeal và subject lạ/rỗng; authenticated POST; CSRF thiếu/sai không gửi; production DI hiện tại cấp được nonce |
| Input | Rỗng/whitespace, exact/over 100/254/2000, email đơn/list/display name/CRLF, subject giả và cả năm key; nội dung tiếng Việt percent-encode đủ 2000 ký tự |
| Routing/encoding | Support recipient và Sender fallback, From/Reply-To đúng, CC/BCC rỗng, posted destination không tác động; malformed/missing config không gửi; HTML encode, paragraphs, preheader và UA bounded |
| Delivery | Success, explicit SMTP rejection, mất kết nối, exception không rõ receipt, stall deadline, caller/request cancellation trước/sau dispatch; không retry tự động |
| Preservation/replay | Text/normalized values giữ trên 503/202, success GET xóa form, refresh không gửi, nonce giữ trên Unknown, nonce mới cho known failure, cùng nonce/payload khác và HTTP đồng thời chỉ gọi transport một lần |
| Abuse/metadata | Operation thứ tư bị chặn, IP thực thứ hai độc lập, X-Forwarded-For không vượt guard, expiry/future/tamper/wrong-client nonce, client/operation capacity, không evict active send, hết hạn giải phóng metadata |
| Privacy | Logging fake kiểm tra không chứa email/message/client raw; form model encode; không đưa config inbox ra phần Contact |

## Giới hạn và bàn giao

- Chưa gửi SMTP thật, chưa xác minh Gmail/inbox/spam, chưa gọi paid AI.
- Full suite có hai lỗi AI/SignalR ở lượt toàn bộ như trên; isolated rerun đều
  pass. Đây vẫn là giới hạn của bằng chứng full-suite, không phải full acceptance.
- Chưa nghiệm thu browser thực, responsive/zoom/keyboard; bước 02 đảm nhiệm.
  HTTP tests dùng compiled Razor/TestServer. Trần body được cấu hình bằng MVC
  endpoint metadata; test oversized chứng minh form-parser rejection, không
  thay thế kiểm thử Kestrel/IIS/reverse proxy thực.
- Guard chỉ trong singleton/một process/một nonce. Không dedup qua worker/restart,
  nonce mới hay đổi IP. Topology Plesk/proxy chưa quan sát; single-worker và client
  attribution là ràng buộc release khi dựa vào guard hiện tại. Không tự chỉnh hosting.
- Footer chung `_Layout.cshtml:389–408` còn contact email/phone/address/domain chưa
  xác minh. Đây là file shared UI được bảo vệ và chưa được sửa. Đề xuất nhỏ nhất:
  bỏ các dòng chưa xác minh, giữ navigation/IDs, liệt kê consumers và so sánh các
  trang dùng layout sau khi có quyền sửa shared UI. Không che footer bằng CSS.
- Web local đang chạy chưa restart để nạp backend mới; không dùng nó làm bằng
  chứng nghiệm thu. Công việc Writing/Goals/Speaking và tiến trình cũ được giữ.

Gemini tiếp tục bước 02 với contract thật và prompt đã có trong work pack; giữ
nonce, CSRF, appeal, states, field names/IDs và nguyên tắc không tự resend Unknown.
