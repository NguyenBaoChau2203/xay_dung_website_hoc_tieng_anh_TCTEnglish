# OpenAI text + Whisper audio — local setup

Cập nhật01/11/2026. Website dùng **gpt-6-luna**, Responses API,
**reasoning.effort=low** cho văn bản; Whisper local cho audio. Model Codex/IDE
của lập trình viên độc lập với model API website.

## Nhập key và chạy

Prerequisites một lần: Windows x64, PowerShell7, .NET9 SDK/runtime, Docker,
Git/Node theo [quickstart](local-development-quickstart.md) và [team setup](../team-agent-setup.md).
Không sửa source/model/endpoint mỗi máy.

```powershell
pwsh -NoProfile -File scripts/set-local-ai-key.ps1
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Nếu bản Debug cũ đang chạy và cần giữ lại workload, có thể chạy instance kiểm tra
riêng: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5102`.
Launcher từ chối cổng đã có process trước khi build; không dừng process đó hoặc
coi app cũ đã nhận key/code mới. Chọn cổng trống hoặc dừng đúng launcher do mình
quản lý rồi chạy lại. Key/options mới cần một process web mới. Tham số này
không đổi default Debug/5100 của đồng đội.

Helper hỏi key có che ký tự, lấy UserSecretsId từ csproj, merge atomic vào .NET
User Secrets ngoài repo; không đưa key vào argument/history/chat. Giữ các secret
OAuth/SMTP/DB/billing/speech; nested/flat conflict bị từ chối trước khi ghi.
User Secrets là file local không mã hóa theo cơ chế .NET hiện có. Không chia sẻ
file đó hoặc output `dotnet user-secrets list` để nhờ hỗ trợ.

App vẫn boot khi thiếu key: Writing Local và speech local tiếp tục hoạt động;
feature cloud báo chưa cấu hình. Key/account cần credit, quyền model và network.

## Defaults, override và budget

Git defaults: `AI:BaseUrl=https://api.openai.com/v1`, `AI:Model=gpt-6-luna`,
`AI:ReasoningEffort=low`, key rỗng. Bearer header, `store:false`, không gửi
temperature khi low. Không tự chuyển model/effort.

Owner đã cho phép dùng `OPENAI_API_KEY` cho TCT English. Thứ tự launcher chọn:
**`AI__ApiKey` → User Secrets `AI:ApiKey` → `OPENAI_API_KEY`**. Fallback chỉ nối
vào `AI__ApiKey` của phiên launcher và các process con, không ghi lại key vào
User Secrets/source/argv/log. Launcher khôi phục env khi kết thúc hoặc lỗi.
Nếu máy đã có `OPENAI_API_KEY` thì chạy launcher trực tiếp, không cần nhập lại
bằng helper. Key riêng của TCT vẫn được ưu tiên. Nếu vừa đổi biến môi trường
Windows sau khi mở IDE, mở lại IDE/terminal để nó nhận giá trị mới.

Log chỉ báo `AI: key configured | source=... | model=gpt-6-luna | reasoning=low`
hoặc key missing, không in giá trị. “Configured” chứng minh có key đúng dạng,
chưa chứng minh quyền model/quota. Startup không tự gọi paid API. Chạy thẳng
`dotnet run` không có bước nối OPENAI_API_KEY của launcher; AI Agent dùng lệnh
chuẩn ở trên. Nếu dùng env, đặt qua secret injection, tránh literal key trong
command history. Helper nhắc khi env đang override key vừa lưu.
Model/effort sai bị từ chối; custom HTTPS endpoint được giữ, người quản lý phải
xác minh độ tin cậy và hợp đồng Responses.

Launcher chỉ normalize đúng legacy model `gemini-2.5-flash-lite` và Google v1beta
đã biết trong local store; không in key hoặc đổi OAuth. Không suy đoán custom
config. Credential prefix `AIza` bị adapter từ chối trước HTTP; dùng helper nhập
OpenAI key để thay. Không xóa key hiện có hoặc chép key vào database.env/front end.
Nếu env còn override Google, launcher báo tên biến cần sửa cục bộ.

| Workload | max_output_tokens (gồm reasoning) | Deadline tổng |
| --- | --- | --- |
| Chat | 2048 | 30s |
| Writing feedback | 2048 | adapter30s; optional caller3s |
| Writing generation | 6144 | 90s |
| Vocabulary | 1536 | 10s |
| Reading review | 2048 | 30s |
| Listening quiz | 4096 | 20s |
| Listening translation batch30 | 6144 | 30s/batch |

Cap hữu hạn ban đầu dành chỗ cho reasoning/JSON visible; **chưa calibration live**.
Incomplete/refusal/invalid schema không thành success. Retry cùng deadline với
send/body/backoff; quota/auth/model-access không retry vô ích.
Writing mặc định Local, `CloudEnhancementEnabled=false`; quá3s giữ kết quả Local,
attempt/progress và XP do local quyết định. Không hứa cloud luôn về trong3s.

## Media

Launcher verify Whisper b5454/tiny.en của Plan 1 và FFmpeg **8.1.1 essentials
win-x64** dưới `.appdata/local/media`, không cài/upgrade PATH. [Lock](../../tooling/media/artifacts.lock.json)
ghim archive109,282,242bytes và SHA-256 archive/executable/license. GPLv3 package
từ [Gyan8.1.1](https://github.com/GyanD/codexffmpeg/releases/tag/8.1.1),
[FFmpeg source239f2c733d](https://github.com/FFmpeg/FFmpeg/commit/239f2c733d),
retained [license](../../tooling/media/LICENSE.ffmpeg.txt). Binaries không vào Git.

```powershell
pwsh -NoProfile -File scripts/local-media.ps1 -Action Setup
pwsh -NoProfile -File scripts/local-media.ps1 -Action Verify -Offline
pwsh -NoProfile -File scripts/local-speech.ps1 -Action Status
```

Listening/Admin: captions trước, thiếu mới tải audio-only. Nguồn tối đa30phút/
100MiB; kiểm metadata trước tải audio và kiểm byte cap thực trên stream.
Decode PCM16 mono16k vào temp riêng ngoài webroot, cap57,600,000bytes, tổng300s.
FFmpeg dùng ArgumentList, local protocol/demuxer whitelist, verify SHA trước decode.
Không shell từ URL/tên file user. Chunk10s dùng cùng inference limiter của bài
Speaking rồi trả lease; queueing tiêu budget. Offset timestamps ASR thật, reject
overlap/range/order sai hoặc thiếu segments. Không bịa timecode từ plain text.
Chỉ publish khi hoàn tất toàn bộ; dọn temp trong finally. Video30phút có thể vượt
300s CPU và fail hữu hạn. Learner Speaking vẫn captions/SRT/VTT-only.

Build/test/hook thường không start Whisper. Web request không tự cài/start engine.
Deploy tương lai cần operator provision ASR/decoder/path/hash phù hợp; source local
không thay Plesk hoặc production gate.

## Live opt-in sau offline

Tests thường dùng fake HTTP, SQLite riêng và secret fixtures, không gọi cloud.
Sau nhập key local, smoke nhỏ có đúng hai requests tổng hợp text/JSON:

```powershell
Import-Module ./scripts/LocalAiConfiguration.psm1 -Force
$secretPath = Get-LocalUserSecretsPath -ProjectPath ./TCTEnglish/TCTEnglish.csproj
$previousAiKey = [Environment]::GetEnvironmentVariable('AI__ApiKey', 'Process')
$env:TCT_OPENAI_LIVE = '1'
try {
    $status = Initialize-LocalAiEnvironment -SecretPath $secretPath
    dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter FullyQualifiedName~OpenAiLiveAcceptanceTests --logger 'console;verbosity=normal'
} finally {
    Remove-Item Env:TCT_OPENAI_LIVE -ErrorAction SilentlyContinue
    if ($null -eq $previousAiKey) { Remove-Item Env:AI__ApiKey -ErrorAction SilentlyContinue }
    else { $env:AI__ApiKey = $previousAiKey }
}
```

Test báo actual model, input/output/reasoning/total usage và latency, không in key,
headers hoặc nội dung cá nhân. Explicit opt-in mà key/quota/network sai thì fail.
Không bật cờ live cho full suite/CI.

Muốn kiểm từng text consumer, đặt thêm `TCT_OPENAI_FEATURE_LIVE=1` rồi chạy filter
`FullyQualifiedName~OpenAiLiveFeatureAcceptanceTests`, sau đó xóa biến này khỏi
shell. Đây là tối đa9 requests tổng hợp qua adapter thật (retry tắt cho acceptance),
DB SQLite riêng: chat+follow-up, writing feedback/generation/replay, vocab,
reading, Listening quiz/translation và optional Writing3s. Cờ này độc lập với
smoke hai requests; tests thường không gọi API. Test không kiểm browser mic hoặc
độ đúng về sư phạm trên tập người học thật.

Sau khi launcher start Whisper, dùng media tổng hợp có provenance, M4A21–30s:

```powershell
$env:TCT_ASR_LIVE_BASEURL = 'http://127.0.0.1:8092'
$env:TCT_AUDIO_IMPORT_LIVE_MEDIA = 'C:/absolute/path/to/synthetic-english.m4a'
try {
    dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter FullyQualifiedName~AudioImportLiveAcceptanceTests
} finally {
    Remove-Item Env:TCT_AUDIO_IMPORT_LIVE_MEDIA, Env:TCT_ASR_LIVE_BASEURL -ErrorAction SilentlyContinue
}
```

Fake media HTTP handler → FFmpeg thật → Whisper thật, kiểm timestamps nhiều chunk.
Không chứng minh YouTube live, mic/accent thật hoặc nhiều người đồng thời.
Browser chạy riêng: Chat+hội thoại tiếp, generation+replay, optional feedback,
vocab, reading, Listening quiz/translation và Writing Local khi thiếu cloud.
Ghi từng ca chưa chạy; offline pass không thay live acceptance.

[Plan 2 ledger](../feature-audits/2026-11-01-openai-provider-migration-ledger.md)
ghi evidence/gates. Đợt này không commit/push/PR/CI remote/merge/deploy.
