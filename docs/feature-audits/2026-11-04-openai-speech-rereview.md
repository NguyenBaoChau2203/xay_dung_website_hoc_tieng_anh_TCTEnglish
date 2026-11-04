# Review lại Cursor và sửa trực tiếp — OpenAI Speech, 04/11/2026

**Kết luận:** các sửa R1–R7 của Cursor đã có trong source và vượt qua kiểm tra
offline. Reviewer tìm thêm phần còn thiếu ở R3/R4/R7 và bằng chứng completion,
đã sửa trực tiếp theo yêu cầu owner. Kết quả này xác nhận phạm vi local/offline;
không phải nghiệm thu paid live, mic thật hoặc production.

Một agent; branch `feature/chau-ui-performance-audit`, HEAD `210bf2d`. Giữ toàn
bộ dirty work UI/logic/speech trước đó. Không commit/push/remote CI/PR/merge,
không chạy migration hay thay dữ liệu dev/hosting. Không trả phí OpenAI.

## 1. Cursor đã làm đúng

| Finding cũ | Source và bằng chứng đã kiểm lại |
| --- | --- |
| R1 | Union các PCM interval overlap trước hard split; importer validate toàn bộ windows trước send. Regression 21s/pause100ms và real-planner/fake-provider xanh. |
| R2 | Threshold không còn vượt peak trong dải dynamic đã tái hiện; giữ energy floor. Bổ sung quiet-tone và near-silence-noise synthetic. |
| R3 | Live silence và live import cùng dùng `OpenAiSpeechLiveBudget.Shared` ở HTTP boundary; reviewer bổ sung xác minh WAV bên dưới. |
| R4 | Resolver fail-closed cho shared explicit-empty/invalid; credential ToString redacted. Reviewer đồng bộ launcher bên dưới. |
| R5 | IOException khi đọc response sau headers200 → unavailable; test lease và request kế tiếp xanh. |
| R6 | Evict expired users; cap16384, không evict active buckets để bỏ qua giới hạn10/min. Các regression expiry/capacity xanh. |
| R7 | Đã có Enabled/Model/BaseUrl/env precedence và sửa active Admin copy; reviewer sửa thêm status bên dưới. |

Text model vẫn `gpt-6-luna` / `low`; audio vẫn `gpt-transcribe`. Ordinary startup
không chạy local Whisper và không gửi paid speech request. Learner captions và
SRT/VTT vẫn không gọi audio cloud. Không thay persisted model trong lần review này.

## 2. Phần còn thiếu đã sửa

### S1 — Budget phải xác minh WAV trước khi tính giây

Helper mới của Cursor dùng `dataBytes / 32000` mà chưa kiểm RIFF/fmt/sample rate,
và lấy file part cuối nếu multipart có nhiều file. Trường hợp header bị cắt còn
fallback sang `wav.Length - 44`. Nó chưa đáp ứng yêu cầu duration được xác minh.

Regression trước sửa: **6 fail /6 pass** trong budget suite — wrong sample rate,
wrong container, truncated data, duplicate data chunks, wrong byte rate và
duplicate multipart file vẫn đến fake transport.

Đã yêu cầu đúng một file, kiểm RIFF size, chunk bounds bằng long, một fmt PCM16
mono16k/byte rate/block align hợp lệ và một data chunk aligned. Sai → chặn trước
reservation/send. Đọc buffer HttpContent rồi gửi chính content cũ, giữ bytes,
headers và ownership/disposal. Positive streamed-payload test xác nhận không mất
body. Cross-client test xác nhận đúng120s được gửi, request vượt tổng bị chặn.

Files: `TCTEnglish.Tests/Infrastructure/OpenAiSpeechLiveBudget.cs`,
`TCTEnglish.Tests/OpenAiSpeechLiveBudgetTests.cs`.

Đây là budget của **live test trong một process**, không phải quota chi phí toàn
hệ thống hoặc qua nhiều lần chạy dotnet test. Runtime giữ các cap process/user/
request/import hiện có. Không chứng minh guard bằng gọi API thật.

### S2 — Launcher vẫn fallback khi User Secrets có key explicit-empty

`Initialize-LocalAiEnvironment` dùng IsNullOrWhiteSpace để rơi từ User Secrets
sang OPENAI_API_KEY, rồi ánh xạ AI__ApiKey. Như vậy nó override được resolver
fail-closed mới của Cursor. Regression fixture tái hiện **1 fail**.

Đã phân biệt absent/explicit-empty qua ContainsKey. Chỉ absent mới fallback;
explicit-empty vẫn mở web nhưng AI chưa configured, giữ nguyên secret.
Full Python discovery sau đó bắt thêm dependency: start-local tự tạo key rỗng
trên máy mới. Đã bỏ việc tạo AI:ApiKey rỗng; fresh store vẫn dùng authorized
OPENAI_API_KEY, existing explicit-empty vẫn được giữ. Launcher fixture4/4 xanh.

Files: `scripts/LocalAiConfiguration.psm1`, `scripts/start-local.ps1`,
`scripts/tests/test_local_ai_config.py`, `scripts/tests/test_local_launcher.py`.

Không chỉnh User Secrets thật. Nếu một máy có key rỗng từ setup cũ, đặt key bằng
set-local-ai-key.ps1 để thay chính override đó; không tự xóa secret của người dùng.

### S3 — Status chưa theo file config và có thể echo URL private

Status chỉ đọc secrets/env/default, bỏ appsettings và Development overrides;
boolean không hợp lệ được coi là true. BaseUrl không hợp lệ vẫn được in nguyên
vẹn, kể cả userinfo/query có credential. Fixture-only regression tái hiện
**3 fail**; không dùng secret thật trong probe.

Đã đọc đúng layers local Development: appsettings → Development → secrets → env;
trim model/URL, invalid bool → ConfigurationValid=false/Configured=false. URL
invalid chỉ in `[invalid endpoint]`. Giữ Enabled/KeyPresent/EndpointValid riêng
và VerifiedLive=false, không có network. Source key explicit-empty không fallback.

Files: `scripts/speech-status.ps1`, `scripts/tests/test_speech_status.py`.

### S4 — Test tên KeepOneCredit chưa kiểm credit thật

Test fake OpenAI assess→commit→replay trước đây chỉ assert HTTP và số calls;
provider luôn trả câu đầu cho cả3 sentences. Khi thêm DB assertions, test đỏ:
expected1 credit, actual0 — fixture không hoàn thành đủ bài.

Đã trả transcript đúng theo từng seeded sentence, assert trước replay có1 credit,
XP>0 và1 completed video; snapshot DB sau replay phải giống trước, đúng3 ASR calls.
Đây là sửa coverage/fixture, không thay scoring hoặc business logic để ép test xanh.

File: `TCTEnglish.Tests/OpenAiSpeechAssessmentAppTests.cs`.

## 3. Verification mới của reviewer

Artifacts ignored tại `.tmp/stt-rereview/`; không tái dùng số test trong ledger cũ.

| Check | Kết quả |
| --- | --- |
| Baseline focused speech optimized | 67 passed /0 failed /1 skipped |
| Focused sau S1–S4 | 75 passed /0 failed /1 skipped; sau đó2 PCM regressions được chạy trong full suite |
| Full .NET build/test, configuration SpeechReview, Optimize=true | **897 passed /0 failed /12 skipped**, tổng909;1m55s test execution |
| JS recorder/Speaking/import | **12/12** |
| Python scripts final discovery | **Ran99, OK skipped1** =98 executed pass /0 fail /1 skip |
| Native setup Node tests | **9/9** |
| agent_tooling.py | verified; chỉ chứng nhận tooling |
| git diff --check | sạch; warnings CRLF ở các file UI có sẵn |
| GitHub PLESK_DEPLOYMENT_ENABLED read-only | **false** |
| speech-status trên máy owner | configured, Enabled/EndpointValid=true; key source User Secrets AI:ApiKey; modelgpt-transcribe; VerifiedLive=false |

Full suite có warnings nullable/analyzer có sẵn, không có compile/test error.
MariaDB/live/scale suites vẫn skip theo flags; không gọi đó là pass. App-level
completion regression dùng disposable SQLite factory, không phải MariaDB proof.
Không có schema change nên không chạy migration hay đụng DB để review speech.

## 4. Giới hạn trước demo

- Chưa gọi paid API: account/key/model access/quota và transcript thực tế còn
  cần một đợt live opt-in có budget. Metadata configured chỉ xác nhận cấu hình.
- Chưa chạy mic thật hoặc browser mới trong phiên review này. Browser fake ASR
  Admin/390px là evidence Cursor ghi ngày03/11, không phải reviewer thực hiện lại.
- Energy windows là segment-window timing; không có word alignment/phoneme hay
  chứng nhận chất lượng phát âm. Synthetic quiet/noise tests không phải giọng người.
- Không có cold/warm baseline cùng điều kiện để hứa tăng tốc bao nhiêu phần trăm;
  chỉ xác nhận bỏ dependency startup local ASR qua source/launcher tests.
- Deploy vẫn off; kết quả local không mở gate release. Không có remote CI mới.

Checklist phiên này:
[rereview closeout](../superpowers/plans/2026-11-04-openai-speech-rereview-closeout.md).
