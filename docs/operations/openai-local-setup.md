# OpenAI text + Speech-to-Text — local setup

Cập nhật03/11/2026. Website dùng **gpt-6-luna**, Responses API,
**reasoning.effort=low** cho văn bản; **gpt-transcribe** (Audio API) cho speech.
Model Codex/IDE của lập trình viên độc lập với model API website.

## Nhập key và chạy

Prerequisites một lần: Windows x64, PowerShell7, .NET9 SDK/runtime, Docker,
Git/Node theo [quickstart](local-development-quickstart.md) và [team setup](../team-agent-setup.md).
Không sửa source/model/endpoint mỗi máy.

```powershell
pwsh -NoProfile -File scripts/set-local-ai-key.ps1
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
pwsh -NoProfile -File scripts/speech-status.ps1
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
.NET User Secrets không tự cung cấp mã hóa; Windows có thể áp dụng EFS/ACL riêng.
Giữ nguyên bảo vệ của OS. Không chia sẻ file đó hoặc output `dotnet user-secrets list`
để nhờ hỗ trợ. Helper dùng replace với backup cùng thư mục khi file đã tồn tại;
nếu thay file lỗi, giữ original/backup để phục hồi, không delete-then-move overwrite.
Ordinary startup chỉ đọc kho key. Connection Docker/worker flags và chuẩn hóa
nonsecret AI options vào environment của launcher/child, không sửa file User Secrets.

App vẫn boot khi thiếu key: Writing Local và các tính năng offline tiếp tục;
Speaking cloud ASR báo chưa cấu hình (không fake điểm). Key/account cần credit,
quyền model audio/text và network — “configured” ≠ verified live.

## Defaults, override và budget

Git defaults: `AI:BaseUrl=https://api.openai.com/v1`, `AI:Model=gpt-6-luna`,
`AI:ReasoningEffort=low`, key rỗng. Speech: `Learning:Speech:OpenAI:Model=gpt-transcribe`,
`BaseUrl=https://api.openai.com/v1/` (không ghép từ AI:BaseUrl). Bearer header
chỉ phía server. Không gửi reasoning.effort vào Audio API.

Owner đã cho phép dùng `OPENAI_API_KEY` cho TCT English. Thứ tự launcher chọn:
**`AI__ApiKey` → User Secrets `AI:ApiKey` → `OPENAI_API_KEY`**. Speech cũng
resolve speech explicit key trước, rồi shared AI key theo cùng precedence.
Fallback chỉ nối vào process launcher/con, không ghi lại key vào User Secrets/
source/argv/log. Launcher khôi phục env khi kết thúc hoặc lỗi.

Log chỉ báo nguồn/model metadata, không in giá trị. Startup không tự gọi paid
Speech/Text API. Chi tiết speech: [local-speech](local-speech.md).

## Media (FFmpeg pin — import only)

Ordinary web/mic PCM không cần FFmpeg. Admin/legacy audio fallback:

```powershell
pwsh -NoProfile -File scripts/local-media.ps1 -Action Setup
pwsh -NoProfile -File scripts/local-media.ps1 -Action Verify -Offline
```

Listening/Admin: captions trước, thiếu mới decode audio-only → PCM window planner
→ OpenAI từng cửa sổ → map transcript vào sample offsets (`openai-audio-vad`).
Không bịa timecode từ plain text; historical `whisper-audio` vẫn đọc được.
Learner Speaking vẫn captions/SRT/VTT-only (0 cloud audio).

## Live opt-in sau offline

Tests thường dùng fake HTTP, không gọi cloud dù máy có key.

Text smoke (đã có):

```powershell
$env:TCT_OPENAI_LIVE = '1'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter FullyQualifiedName~OpenAiLiveAcceptanceTests
Remove-Item Env:TCT_OPENAI_LIVE -ErrorAction SilentlyContinue
```

Speech (mới, budget đề xuất ≤8 POST / ≤120s audio, fail-fast, no retry):

```powershell
$env:TCT_OPENAI_SPEECH_LIVE = '1'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter FullyQualifiedName~OpenAiSpeechLiveAcceptanceTests
Remove-Item Env:TCT_OPENAI_SPEECH_LIVE -ErrorAction SilentlyContinue
```

Import live (cần FFmpeg + media fixture + speech live flag):

```powershell
$env:TCT_OPENAI_SPEECH_LIVE = '1'
$env:TCT_AUDIO_IMPORT_LIVE_MEDIA = 'C:/absolute/path/to/synthetic-english.m4a'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter FullyQualifiedName~AudioImportLiveAcceptanceTests
Remove-Item Env:TCT_OPENAI_SPEECH_LIVE, Env:TCT_AUDIO_IMPORT_LIVE_MEDIA -ErrorAction SilentlyContinue
```

Cờ loopback Whisper cũ (`TCT_ASR_LIVE_BASEURL`) không được bật cloud.
Không bật live cho full suite/CI. Offline pass không thay live acceptance.

Đợt speech replacement không commit/push/PR/CI remote/merge/deploy; giữ
PLESK_DEPLOYMENT_ENABLED=false.
