# Local key và startup safety — 04/11/2026

Owner yêu cầu kiểm key vừa nhập và mỗi lần AI Agent mở local phải dùng key đó.
Một agent; nhánh fix/local-launcher-preserve-user-secrets từ master8203e30.
Không commit/push/remote CI/PR/merge/deploy trong lượt sửa này.

## Kết quả key và incident

Đầu phiên, User Secrets AI:ApiKey configured. Probe chỉ gửi key qua Authorization
header tới api.openai.com, không log key/error message/request body. GET metadata
gpt-6-luna và gpt-transcribe đều200; một Responses request gpt-6-luna/low trả200,
completed, đúng OK;11input +5output tokens. Không gửi speech POST/audio.

Sau đó launcher thật gọi Update-LocalSecretStore để lưu connection/worker flags.
File.Move(overwrite=true) trong thư mục EFS báo “The specified file could not be
encrypted”; secrets.json không còn ở active store. Chỉ còn lock file. Đây là
lỗi destructive replacement của ordinary startup, không phải key bị OpenAI từ chối.
Đã thông báo/apologize owner ngay khi xác minh. Không yếu hóa EFS/ACL, không đọc
bản protected D bằng cách khác, không dùng fallback key khác che lỗi.

Owner chọn **“Tôi sẽ nhập lại sau”** qua async reply. Vì vậy không tuyên bố AI
hiện tại đã sẵn sàng dù key ban đầu đã hoạt động. Cấu hình/credential khác trong
file bị mất cũng không được coi là đã khôi phục. Database password vẫn ở ignored
database.env nên launcher cấu hình DB an toàn mà không cần phục hồi secret file.

## Bản sửa

- Startup không gọi hàm ghi secret nữa. Connection DB local/worker flags qua child
  environment; cả AI nonsecret overrides/TEMP/DB/worker/env đều được restore.
- Legacy model/base URL được chuẩn hóa trong bộ nhớ/child env; key thật vẫn đọc
  từ store chuẩn, không copy vào source, argv hoặc một file secrets trên D.
- Explicit key helper: existing file dùng File.Replace với backup cùng thư mục;
  missing file dùng Move không overwrite; failure giữ original/backup và cố
  phục hồi khi destination thiếu. Không ignore metadata/ACL errors.
- Shared AGENTS.md/.ai README và onboarding ghi rõ cả4 IDE dùng launcher, tái
  dùng key đã lưu và không rewrite User Secrets mỗi lần mở web. F5/watch trực
  tiếp cần cấu hình DB riêng, không kế thừa env tạm của launcher đã kết thúc.

Đối chiếu [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-9.0)
và [ReplaceFileW failure states](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew):
backup là cần thiết khi replacement thất bại sau bước đổi tên. Không tuyên bố
fixture khóa file là proof của mọi EFS failure; không thử viết lại kho EFS thật.

## Verification

| Check | Result |
| --- | --- |
| Launcher regressions trước sửa |4 fail do startup ghi secret hoặc thiếu child DB config |
| Launcher final |7 pass; bytes/mtime preserved dưới write-forbidden fixture, fresh no store, env restore success/failure/DatabaseOnly, no ASR spawn/download |
| Key/config final |14 pass; nested/flat merge, key precedence, locked Windows destination giữ original |
| Full Python scripts |Ran105,104 pass /1skip |
| Node agent setup |9/9 |
| agent_tooling.py |verified; không thay app acceptance |
| Release startup build |succeeded qua launcher/EF CLI |
| Dev schema |already up to date;0new migration applied; không reset/import |
| Bootstrap |existing users/data preserved, skipped |
| GET /health/ready |200, status ready |
| Browser /Account/Login |trang login mở trong Chrome |

Evidence ignored tại .tmp/local-key-check và .appdata/local/web-key-check.log.
Web đang chạy tại http://localhost:5100 bằng Release launcher, không có key trong
process tree của instance này: tool wrapper gỡ key env chỉ trong child trước
khi chạy, không sửa user/machine env. Log Speech not-configured/missing; không
paid startup request, không Whisper. User Secrets vẫn không được tạo lại.
Đây là nghiệm thu startup/non-AI; chưa đóng live AI/Speaking trên instance mới.

Khi owner sẵn sàng: chạy scripts/set-local-ai-key.ps1 trong PowerShell thường,
nhập key vào ô che, sau đó dừng đúng launcher của instance5100 rồi mở lại. Agent
phải đọc key hiện tại và xác minh readiness/status, không báo instance cũ đã có
key mới. Không gửi key vào chat hoặc push lên GitHub.

## Follow-up: owner đã nhập lại và restart thành công

Sau checkpoint hoãn ở trên, owner báo đã nhập key và yêu cầu khởi động lại.
Đã dừng đúng process/launcher của instance no-key do agent tạo, chạy lại cùng
scripts/start-local.ps1 -NoBrowser -Configuration Release trên5100. Bản sửa
launcher được giữ nguyên; không ghi credential store, không gửi paid API probe.

- Log text: key configured, source=User Secrets, modelgpt-6-luna/low.
- Log/status speech: configured, source=User Secrets AI:ApiKey, modelgpt-transcribe,
  VerifiedLive=false.
- /health/ready200, statusready; tab Login đã làm mới trong Chrome.
- Secret file vẫn tồn tại; length, last-write UTC ticks và protection attributes
  giữ nguyên trước/sau startup. Không in nội dung hoặc hash key ra log.
- Existing dev users/data preserved, bootstrap skipped; schema đã up to date,
  không có migration mới cần áp dụng.

Đây là **trạng thái hiện tại**, thay cho đoạn running web no-key ở checkpoint
trước. AI config đã được khôi phục; không coi đây là nghiệm thu API mới, quota
hay mic/audio chất lượng thật. Restart log ở ignored .appdata/local/web-key-restart.log.
