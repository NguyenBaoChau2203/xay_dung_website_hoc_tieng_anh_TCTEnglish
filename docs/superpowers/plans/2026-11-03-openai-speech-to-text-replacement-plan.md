# Thay toàn bộ Whisper bằng OpenAI Speech-to-Text

Ngày03/11/2026. Trạng thái: **IMPLEMENTED OFFLINE (Cursor Auto)**; live/browser
owner gates còn mở. Ledger:
`docs/feature-audits/2026-11-03-openai-speech-replacement-ledger.md`. Mục tiêu báo
cáo25/11/2026. Plan này là công việc mới sau hai Plan UI/logic đã đóng local,
không mở lại toàn bộ scope của chúng.

Nhánh lúc lập Plan: feature/chau-ui-performance-audit, HEAD210bf2d; khoảng129
dirty/untracked entries của các Plan trước. Đây là công việc cần giữ, không phải
checkout sạch. Một agent thực hiện tuần tự, không worker/AI chat/CLI khác.

## 1. Kết quả cần đạt và quyền thực thi

Sau khi owner giao prompt thực thi:

- Web dùng OpenAI Speech-to-Text cho mọi chỗ hiện chạy Whisper: assessment mic,
  audio transcript fallback trong flow hiện có, kiểm thử và hướng dẫn vận hành.
- Không tải/khởi động/probe model Whisper khi chạy web, setup, build, CI hoặc IDE.
  Không có fallback runtime về Whisper, Colab hoặc dịch vụ ASR khác.
- Docker MariaDB vẫn dùng như hiện tại, không cần SSMS. FFmpeg vẫn phục vụ decode
  media import; ghi âm PCM từ browser không cần chạy FFmpeg/model AI trên máy.
- Một key local có thể dùng chung text/audio theo quyền tài khoản. Key đã có
  trong User Secrets được dùng lại; không sao chép giá trị vào file source/prompt.
- Model text giữ gpt-6-luna, reasoning low. Model audio cấu hình riêng theo mục2.
- Giữ dữ liệu cũ, IDOR404, antiforgery, roles, receipt/XP/credit/replay, timestamps,
  polling/job/lease và UI của hai Plan vừa đóng.

Cho phép thay DI trong Program.cs, cấu hình không secret trong appsettings.json,
scripts/active agent instructions/docs cần cho thay ASR. Không cần thêm SDK/NuGet
mới: HttpClient/.NET9 và phân đoạn PCM bằng C# là thiết kế mặc định.
Nếu cần đổi persisted model/mapping thì tự kích hoạt tct-ef-migration; dự kiến
không có schema change. Không sửa .csproj/package/target framework ngoài nhu cầu
đã chứng minh; không nâng net9/EF9/Pomelo9.

Không commit/push/PR/remote CI/merge/deploy/Plesk. Giữ
PLESK_DEPLOYMENT_ENABLED=false. Không reset dev DB, chạy Down về baseline hoặc
chạm hosting. Không tự gọi API trả phí; live acceptance là opt-in riêng ở mục8.

## 2. Quyết định API đã đối chiếu tài liệu

Đã kiểm nguồn chính thức03/11:

- [File transcription](https://developers.openai.com/api/docs/guides/speech-to-text):
  hướng dẫn khuyến nghị gpt-transcribe cho recorded audio; không mặc định suy ra
  các format timestamp cũ cũng dùng được với model này.
- [Audio API reference](https://developers.openai.com/api/reference/resources/audio/subresources/transcriptions/methods/create):
  multipart file transcription; format/optional fields phụ thuộc model.
- [GPT-Transcribe](https://developers.openai.com/api/docs/models/gpt-transcribe):
  model audio riêng; quyền dùng của account/key vẫn cần xác minh live.

Target mặc định: **gpt-transcribe**, endpoint
https://api.openai.com/v1/audio/transcriptions, JSON transcript.
Cursor phải mở lại đúng tài liệu chính thức trước khi khóa request contract;
không lấy tên model từ trí nhớ hoặc tự đổi theo IDE Auto.
Không dùng whisper-1 hoặc các model transcription đã được tài liệu hiện tại
đánh dấu deprecated làm fallback để đạt test. Không gửi reasoning.effort của
model text vào Audio API. Các field hint ngôn ngữ là optional; bản đầu không cần
gửi hint. Nếu thêm phải theo schema riêng của model, không mang language=en từ
Whisper sang một cách máy móc.

**Giới hạn đã biết:** chưa có bằng chứng gpt-transcribe trả word/segment timestamps
giống adapter cũ. Vì vậy thiết kế mặc định của Plan không phụ thuộc timestamp
provider. Speaking chỉ cần transcript; import dùng cửa sổ audio được đo từ PCM.
Đây là lựa chọn của Plan để thực thi, không phải tính năng timestamp do OpenAI hứa.

## 3. Bản đồ source và contracts cần giữ

Paths trong bảng tương đối với root, dựa trên source đã kiểm.

| Vùng | File hiện tại | Điều đã xác minh |
| --- | --- | --- |
| ASR contract | TCTEnglish/Services/Speaking/ISpeechTranscriber.cs | TranscribeAsync(Stream,ct), statuses, Transcript, Segments, EngineName/ModelIdentity |
| Adapter/config | cùng thư mục: WhisperHttpTranscriber.cs, SpeechProcessingOptions.cs | /inference local, verbose_json; timeout15s gồm queue+HTTP; clip15s/2MiB |
| Admission | cùng thư mục: SpeechInferenceLimiter.cs | singleton process-local1running/4queued; lease cần nhả trong mọi nhánh |
| Assessment | cùng thư mục: SpeakingAssessmentService.cs | kiểm access trước transcribe, TextMatchScore, receipt; fluency/completeness unavailable |
| Import audio | cùng thư mục: AudioTranscriptImporter.cs, AudioImportOptions.cs, FfmpegPcmDecoder.cs | source<=100MiB/1800s, PCM16mono16k, chunks10s, total<=300s; reject thiếu timestamp |
| Transcript caller | TCTEnglish/Services/YoutubeTranscriptService.cs | English captions trước, audio fallback sau trong flow legacy/admin; source cũ whisper-audio |
| Learner import | TCTEnglish/Services/SpeakingService.cs, Workers/SpeakingImportWorker.cs | learner dùng captions-only hoặc SRT/VTT; không tự bật cloud fallback cho learner |
| DI | TCTEnglish/Program.cs | typed HttpClient ISpeechTranscriber→WhisperHttpTranscriber |
| Local | scripts/start-local.ps1, LocalAiConfiguration.psm1, local-speech.ps1, local-media.ps1 | key precedence, child env restore; Whisper đứng trước web; FFmpeg pin riêng |
| Teammate/IDE | scripts/check-local.cjs, agent_clients.py, AGENTS.md, .ai/README.md, native adapters | generated VSCode tasks có Whisper status/stop; canonical rule yêu cầu auto-start |
| Tests | WhisperHttpTranscriberTests, WhisperLive*Tests, AudioTranscriptImporterTests, AudioImportLiveAcceptanceTests, SpeakingInferenceLimiterTests | phải chuyển coverage, không xóa regression để lấy xanh |
| Tests scripts | scripts/tests/test_local_speech.py, test_local_launcher.py, test_speech_readiness_deadline.py, test_local_ai_config.py, test_local_media.py | có helper imports dùng chung; cần tách helper trước xóa lifecycle tests |

CodeGraph trước indexed source, maxFiles1–3/lượt; scripts/config không có trong
index thì đọc bounded đúng gap. Không init/rebuild index. Context7 hiện không
exposed; dùng official OpenAI docs, không đưa source/key private vào web query.

## 4. Thiết kế đích

### 4.1 Cloud adapter và cấu hình

Tạo OpenAiSpeechTranscriber trong Services/Speaking, triển khai ISpeechTranscriber.
Giữ contract chung; Transcript có thể thành công mà Segments rỗng trong assessment.
Không tạo đoạn0..clipDuration rồi giả là timestamp do provider cung cấp.

Đề xuất options:

| Key | Giá trị/ý nghĩa |
| --- | --- |
| Learning:Speech:Enabled | true; thiếu key không làm chết toàn bộ web |
| Learning:Speech:OpenAI:Model | gpt-transcribe |
| Learning:Speech:OpenAI:BaseUrl | https://api.openai.com/v1/ |
| Learning:Speech:OpenAI:ApiKey | secret optional cho speech; để trống dùng shared key đã resolve |
| Learning:Speech:OpenAI:MaxResponseBytes | 262144, chặn response không giới hạn |
| Learning:Speech:InferenceTimeoutSeconds | 15, giữ một deadline queue+upload+response+parse |
| Learning:Speech:MaxAudioSeconds / MaxPayloadBytes | 15 /2097152, giữ cap app |
| Learning:Speech:MaxConcurrentInferences / MaxQueuedInferences | 1 /4, chưa tăng để che latency |
| Learning:Speech:MaxAssessmentsPerUserPerMinute | 10, admission theo user sau access validation |
| Learning:Speech:AssessmentTtlMinutes | 10, giữ receipt semantics |
| Learning:AudioImport:MaxTranscriptionRequests | 180/windowed import, fail rõ khi vượt |

Tên class/options có thể điều chỉnh nhỏ theo coding conventions; key contract
được chốt và ghi ledger trước caller/tests. Không giữ IsConfigured dựa vào
WhisperHttpBaseUrl. ModelIdentity mới lấy từ model thực được dùng, không giữ
whisper.cpp-unconfigured hoặc một label không khớp request.

Resolve secret có thứ tự: speech env explicit → speech User Secrets →
shared AI key theo precedence hiện có (AI__ApiKey → User Secrets AI:ApiKey →
OPENAI_API_KEY). Giữ semantics explicit-empty/invalid; không âm thầm đổi sang
credential khác khi key explicit bị từ chối. Fallback chỉ resolve trong memory/
child process, không viết bản sao key vào Speech options/User Secrets/Git.
Không tự sửa OPENAI_API_KEY dùng cho công cụ khác trên máy.

Không dùng AI:BaseUrl của Responses để ghép audio endpoint. HTTPS official host
là mặc định; test dùng fake HttpMessageHandler. Nếu hỗ trợ loopback endpoint cho
fixture thì opt-in test rõ, chặn redirect dẫn Authorization sang host khác.
Không cho client gửi URL/model/key. Không ToString/log record chứa credential.

Adapter: multipart WAV đúng filename/content-type + model; Bearer header phía
server. Parser strict object+string text; response/error body bounded. Missing
text/wrong type/error object dù HTTP200 là invalid_schema, không phải silence.
Text rỗng hợp lệ là NoSpeech. Không dùng reference sentence làm prompt/keywords,
không sửa transcript bằng LLM rồi dùng để chấm, tránh bias điểm khớp.

Hành vi lỗi bên ngoài giữ shape hiện hành, thông điệp tiếng Việt:

| Tình huống | Kết quả |
| --- | --- |
| thiếu key/disabled | NotReady/503, code rõ, không network |
| provider401/403/model access | Unavailable/503, category nội bộ auth/model; không trả raw error/key |
| provider429 | engine_busy/429 theo contract; không retry tự động |
| provider5xx/network outcome không rõ | unavailable/503; không chuyển provider/gửi lặp |
| deadline tổng hết | Timeout/504 |
| caller cancel | propagate OCE đúng caller, nhả lease, không receipt/XP giả |
| malformed/oversized response | unavailable invalid_schema/response_too_large |
| silence/no text | NoSpeech, không commit progress |

Giữ no-auto-retry ở HttpClient/Polly/UI và caller import; một POST có thể đã
được provider tính phí dù client không nhận được kết quả. Không hứa exactly-once
billing. Log chỉ category/status/request-id nếu có/model/audio seconds/queue/
HTTP duration, không audio/transcript/body/key/recipient/private paths.

Assessment cần guard theo user trước cloud call (mặc định10/phút), sau ownership
và validation để giữ access contract; không chỉ dùng global semaphore. Dùng clock
có thể fake và cache bounded/expiry, test429/recovery/cross-user. Đây là guard
process-local, không quota tiền cứng toàn cụm/IIS; không reuse15-question chat
quota hoặc claim exact billing. Import có cap request riêng nêu trên.

### 4.2 Speaking và timestamp cho media import

Assessment: PCM validation→bounded admission→OpenAI transcript→text match hiện
hữu→receipt→progress/credit transaction hiện hữu. Fluency/Completeness vẫn
unavailable; text recognition không phải đánh giá âm vị.

Import giữ captions/SRT/VTT hợp lệ trước. Với audio fallback hiện có:

1. Decode source bằng FFmpeg pin hiện tại sang PCM16mono16k. Giữ giới hạn source/
   decoded bytes/duration/protocol whitelist/threads1/temp-owned cleanup.
2. Tạo IPcmSpeechWindowPlanner (hoặc tên tương đương), thuần C# xử lý năng lượng
   PCM theo frame, không neural model/Python/runtime ASR. Chọn candidate speech
   windows từ sample offsets thật, có silence trim/padding/merge với giới hạn.
3. Mỗi window tối đa10s (<=cap15s), target khoảng2–10s; cắt theo khoảng nghỉ khi
   có, hard-bound khi liên tục dài. Không cắt sample lẻ, bỏ byte hay vượt duration.
   Có thể gộp các cửa sổ ngắn/gap để tránh1API/từ. Threshold/frame/padding phải có
   tests và đo voice nhỏ/noise; không coi energy detector là chứng minh có lời nói.
4. Gửi WAV của từng window đến OpenAI, tuần tự và nhả admission sau từng window.
   Mỗi window dùng deadline riêng15s nằm trong total deadline300s của import.
5. Một transcript map đúng một cửa sổ audio. Start/end=sampleIndex/16000; bao gồm
   khoảng padding đã cắt thật. Đây là mốc đoạn audio, không word alignment.
   Không tách nhiều câu rồi chia đều duration, không đoán timecode từ số chữ,
   không gắn0..10s vào mọi transcript, không dùng timestamp tự bịa của LLM.
6. Provider text-only dùng được ở đây vì importer đã sở hữu timebase của audio
   thực. Điều này không cho phép bỏ check timestamp của các caller khác.
   Native timestamps nếu được hỗ trợ sau này chỉ dùng khi có docs/test riêng;
   thiết kế hiện tại không phụ thuộc chúng.
7. NoSpeech bỏ window, lỗi giữa chừng fail toàn import, không publish partial.
   Trả ít nhất số cue hợp lệ theo usability check hiện có; không nhân bản cue
   để đủ3. Reject nan/infinity/overlap/out-of-range, kiểm tổng end<=source duration.
8. Source mới: openai-audio-vad hoặc tên rõ tương đương nằm trong độ dài cột
   hiện có. Lịch sử whisper-audio/EngineName cũ đọc được và không bị đổi hàng loạt.

Thay đổi tính chính xác thời gian phải được nói rõ: audio-window timing, không
timestamp chính xác từng từ của engine cũ. UI không quảng cáo “chấm âm vị” hay
“đồng bộ từng từ”. Nghiệm thu ba câu có pause: cue start/end chứa đúng câu audio;
với continuous speech, giữ nguyên cả transcript của window, không giả tách câu.
Nếu cửa sổ cắt mất từ/đồng bộ không đạt test browser, sửa planner trước đóng batch.

MinTranscriptSentenceCount/English usability, YouTube metadata/URL validation và
flow learner captions-only giữ nguyên. Worker hiện không gọi audio fallback:
không sửa retry của worker không liên quan hoặc tự nối nó sang cloud. Kiểm tất
cả caller trực tiếp/gián tiếp để tránh một retry ngoài adapter nhân hóa đơn.

### 4.3 Bỏ Whisper trong local/team workflow

- start-local vẫn giữ command chuẩn, busy-port guard, MariaDB ownership, key
  child env/restore, bootstrap chỉ DB rỗng. Bỏ hẳn bước setup/start/probe Whisper,
  env URL8092 và flags/log quảng cáo local engine. Không tự tải model/CUDA/Python.
- Không yêu cầu FFmpeg chỉ để mở web/ghi âm PCM. Chuẩn bị media riêng bằng
  scripts/local-media.ps1 -Action Setup; verify bằng -Action Verify. Import thiếu
  decoder trả lỗi có hướng dẫn rõ; không tải executable ở background vô hình.
  Giữ pin/hash/Windows support của decoder, không chạy một ffmpeg ngẫu nhiên từ PATH.
- Chỉ tối ưu startup có căn cứ: đo các phase; không bỏ migration/dirty-source
  checks hoặc dùng --no-build với binary cũ để lấy số nhanh. Không chạy EF target
  migration cũ. Tool restore có thể reuse tool local đã đúng pin; không đánh đổi
  consistency. Không thêm FastStart phức tạp nếu chưa có evidence cần thiết.
- Tạo scripts/speech-status.ps1 đọc config/key-source metadata, không network/
  không tính phí. “configured” khác “đã xác minh live”; app health không gọi cloud.
- Retire scripts/local-speech.ps1, tooling/whisper/artifacts.lock.json và fake
  whisper server fixtures khỏi active setup/tests sau khi chuyển đầy đủ helpers/
  consumers. Không để một lệnh Setup/Benchmark cũ có thể tải/chạy Whisper.
- Không tự xóa cache .appdata/local/speech, native trust hoặc dữ liệu người dùng.
  Old cache nằm ignored và không được dùng. Nếu engine cũ đang chạy, chỉ dừng khi
  chứng minh ownership PID/start/exe/hash/port; không kill theo process name.
- Doctor bỏ yêu cầu dành riêng cho Whisper/GPU; VC++ chỉ bỏ khi không còn dependency
  khác cần nó. Docker/.NET/Node/Pwsh vẫn theo app; FFmpeg là prerequisite của import.
- Sửa generator scripts/agent_clients.py trước generated IDE task JSON; thay
  Whisper status/stop bằng Speech configuration status. Không chỉnh trust flags.
- Sửa rule canonical AGENTS.md, .ai/README.md và native adapters thực sự có ghi
  workflow cũ cho Codex/Cursor/Claude Code/Antigravity; không tạo4 procedure copy.
  SETUP-AGENTS/folderOpen/MCP/build/hook không gọi Speech API hoặc bật app.
- Cập nhật README, quickstart, onboarding/group message, local speech,
  openai-local-setup, speaking-import và team-agent-setup. Historical audit/plan/
  bug logs giữ provenance; mark superseded nếu link đang dẫn owner vào hướng cũ.

## 5. Chuỗi thực thi và checkpoint cho Cursor Auto

Mỗi batch ghi files/checks/result/remaining vào ledger mới:
docs/feature-audits/2026-11-03-openai-speech-replacement-ledger.md.
Chỉ load batch hiện tại + relevant skill. Không dừng ở một TODO adapter.

| Batch | Priority/phụ thuộc | Công việc và gate |
| --- | --- | --- |
| STT0 | P0, đầu tiên | Snapshot dirty files; đọc Plan/rules/source; inventory Whisper callers/runtime/scripts/tests/agent docs; baseline hiện tại và startup phase measurements. Không restart web đang có khi chưa xác minh ownership/free port |
| STT1 | P0 sau0 | Khóa model/request contract/secret resolver/options; red tests precedence, missing/explicit invalid key, endpoint; key text model giữ nguyên. Không paid request |
| STT2 | P0 sau1 | OpenAI adapter fake-HTTP tests trước: multipart/auth/parse/error/timeout/cancel/queue/response cap. Gate tất cả leases nhả và0retry |
| STT3 | P0 sau2 | Gắn assessment DI; EngineName/model đúng, receipt/cancel/NoSpeech/IDOR/CSRF/credit/replay giữ; TestWebApplicationFactory mặc định fake/block outbound audio |
| STT4 | P0 sau2–3 | PCM window planner + import timing; audio fallback thật qua new adapter, offsets/caps/cleanup/cancel/no partial; captions/subtitle flows không gọi API |
| STT5 | P1 sau3–4 | Local launcher/status/doctor/media setup/keys/team agents; bỏ active Whisper dependencies. Mỗi phase log thời gian, ordinary startup0cloudcalls |
| STT6 | P1 sau5 | Chuyển tests fixtures/helpers/live flags; xóa runtime cũ đúng inventory; current docs/native tasks đồng bộ. Run tooling checks do có sửa agent generator/rules |
| STT7 | P0 sau0–6 | Fresh Release full suite + JS + scripts/tooling; provider regressions nếu chạm DB; browser/admin/member modes; benchmark before/after. Không gọi skip là pass |
| STT8 | P0 cuối | Self-review diff; update bug/known issues đúng phạm vi; pending-model check; scan secrets; ledger và báo cáo owner. Implementation/offline complete tách với live verification |

## 6. Acceptance matrix bắt buộc

### A. API/HTTP

Fake HttpMessageHandler chạy production client; không chỉ regex source:

- WAV multipart có filename/type đúng; model đúng; key chỉ ở Authorization.
  Không gửi userId/referenceText/text-model reasoning vào multipart.
- JSON text thành công; valid empty text; missing/wrong/null fields/error object/
  HTML/truncated JSON; bounded body cả khi Content-Length không có/nói sai.
- 401/403/404 model,429,5xx, connection error, deadline trước/sau admission,
  cancel upload/read/parse; slot được dùng lại sau từng failure. Assert1HTTP
  attempt, không retry/mở connection thứ2 khi outcome chưa rõ.
- Im lặng PCM thật không tạo transcript/XP giả. Noise/mic nhỏ là manual quality
  gate; test không coi sine/noise là tiếng Anh chỉ vì có năng lượng.
- Parser không log response/transcript/key. Đọc DTO cũ/historical engine metadata
  còn đúng; new result EngineName=OpenAI identifier, ModelIdentity target model.

### B. Workflow/data

- Standard/Premium/Admin/outsider: ownership trước cloudcall, IDOR404, CSRF400,
  anonymous challenge; fake handler assert0request cho truy cập bị từ chối.
- Receipt hết hạn/user khác/replay không double XP. Đổi câu/Stop/cancel không
  gắn kết quả cũ sang câu mới hoặc lưu completion khi provider fail.
- Import synthetic21s có3 phrases/pause, marker source model mới, monotonic
  measured cue offsets; đoạn sau10s cóstart>=10; không vượt source duration.
- Planner: pure silence, leading/trailing silence, tiếng nhỏ, noise, tiếng nói
  liên tục>10s, RIFF chunks không chuẩn/odd byte, frame boundary, cuối clip ngắn,
  negative/nan/overflow/cap. Assert trace window bytes thực sự gửi khớp sample
  range; không chỉ assert timecodes tăng dần.
- Lỗi cửa sổ2 sau1success → không publish video/sentences partial; temp cleanup
  chỉ owned directory; unsupported/missingdecoder/timeout/cancel ổn.
- Captions/SRT/VTT tốt và learner existing flows →0OpenAI audio call.
- Cap request/import + total deadline không reset theo chunk. Nhả singleton
  giữa chunks, queued learner không bị một import giữ slot cả video.

### C. Local/team/no-Whisper

- Fake launcher mô phỏng docker/dotnet, theo dõi mọi native/network calls:
  không spawn Whisper, không probe8092/download model/paid API. Key source metadata
  chính xác; restore env khi success/failure/Ctrl+C; explicit audio key được giữ.
- Missing key vẫn mở web/DB để code; Speaking báo chưa cấu hình, không chấm giả.
  DatabaseOnly không làm audio/media/AI work; busy port fail trước build/native work.
- Speech status đọc config không chạm mạng/argv key; hash scan không có credentials.
- New teammate DB rỗng có sample accounts theo bootstrap cũ; DB đã có user giữ
  dữ liệu. Không mang volume/backup/key của owner vào Git.
- Runtime/source/task/config inventory không còn active Whisper references.
  Exceptions chỉ là historical docs/data fixtures read-compat có giải thích;
  không dùng blanket rg=0 rồi sửa hết lịch sử hoặc disabled tests.

## 7. Commands và phương pháp kiểm chứng

Chạy focused theo batch, rồi full một lần khi không còn thay đổi cần kiểm lại:

    dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~OpenAiSpeech|FullyQualifiedName~AudioTranscriptImporter|FullyQualifiedName~SpeechInferenceLimiter|FullyQualifiedName~SpeakingAssessment"
    node --test scripts/tests/test_speech_recorder.cjs scripts/tests/test_speaking_behavior.cjs scripts/tests/test_speaking_import_behavior.cjs
    uv run --no-project python -m unittest discover -s scripts/tests -v
    uv run --no-project python scripts/agent_tooling.py
    node --test scripts/tests/test_agent_setup.cjs
    dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release
    dotnet tool run dotnet-ef migrations has-pending-model-changes --project TCTEnglish --configuration Release --no-build
    git diff --check

Nếu sửa native lifecycle hooks thì thêm scripts/check_team_hooks.py. Đọc CLI
parameters thật trước chạy generator/validator. Khi web owned giữ lock Release,
dùng config test riêng với project command hoặc dừng đúng launcher theo authority;
không kill toàn bộ dotnet/Docker.

Full baseline trước Plan: 862pass/0fail/13skip (875 tổng); số test sau thay đổi có
thể khác do chuyển provider tests. Giải thích coverage chuyển/xóa, không giữ số
đếm bằng test vô nghĩa. Tests offline không dùng key thật dù parent env có key.
Các live Whisper tests phải chuyển sang explicit TCT_OPENAI_SPEECH_LIVE=1:
các flag loopback cũ không được vô tình bật cloud có tính phí.

Browser dùng launcher chuẩn sau STT5 (mặc định5100 hoặc free port Release).
Kiểm health200, source summary, speaker recorder, cancel/change sentence/no-key/
network failure/retry chủ động/receipt reload, imports/Admin/source labels,
390px và desktop. Fake providers cho browser automation; mic người thật là gate
riêng. Không suy healthready thành cloud billing/model access đã xác minh.

Startup measurement:3cold +3warm trước/sau cùng configuration/Docker/cache state,
ghi phase ms, web readiness và CPU/RAM; engine không có process. Không restart
workload hiện hữu để lấy baseline. Sau rebuild không reuse app cũ như code mới.
Không có baseline hợp lệ thì ghi missing, vẫn có thể chứng minh0Whisper work/
0paidstartup; không tạo % tăng tốc từ suy đoán. Network latency STT khác startup.

DB: Plan dự kiến không đổi schema. Kiểm pending-model ở source cuối; tuyệt đối
không chạy database update về một migration cũ. Nếu phát sinh persisted change,
chạy provider test guarded connection + loopback + DB name + sentinel/runId trước
bất kỳ DML/migration. Không copy CLI baseline từ lịch sử khi chưa kiểm effective
connection; sự cố này đã có trong bug memory03/11.

## 8. Live API và diễn tập trước25/11

Mặc định mọi check là offline/fake, không tính phí. Tạo test/script live explicit
để owner bật sau review. Tên mới TCT_OPENAI_SPEECH_LIVE=1 và budget riêng; không
tự set flag bằng suy luận từ việc key tồn tại.

Đề xuất một đợt nhỏ tối đa8POST audio, tổng audio gửi<=120s, no retries.
Counter phải nằm ở HTTP boundary, đếm cả request fail/timeout, không reset theo
test case; live run fail-fast khi thiếu key/budget. Bounded synthetic WAV/media
đã được phép, không tự thu mic hoặc download video cá nhân. Output chỉ model,
request count, audio seconds, status và latency; không ghi key/audio/transcript
riêng tư. Không mở loop benchmark30sample trên dịch vụ trả phí.

Owner sẽ cần cho phép đợt live cụ thể hoặc chạy opt-in trực tiếp. Khi chưa có,
Cursor hoàn tất toàn bộ implementation/offline tests/docs, ghi live unverified
và handoff; không gọi là blocked implementation hoặc live pass.

Gợi ý mốc:

- 03–07/11: STT0–6, adapter/planner/launcher và fake regressions.
- 08–10/11: review, bounded live validation sau owner opt-in, sửa quality/timing.
- 11–18/11: mic/mạng/máy đồng đội, rehearsal các luồng chính.
- 19–24/11: đóng băng engine/API contract, diễn tập và chuẩn bị media hợp lệ.
- 25/11: demo bằng source đã kiểm; startup không tải model.

Cloud cần Internet và account có quyền/billing; shared key configured không
chứng minh model audio chạy được. Không dùng Colab làm dependency buổi demo.
Nếu mạng/API lỗi, UI báo đúng, vẫn xem bài/phụ đề/các nội dung đã có; không fake
điểm hay dùng transcript ghi sẵn làm kết quả nhận dạng live.

## 9. Definition of done và handoff

- [ ] Inventory tất cả active Whisper consumers và dọn runtime/deps/task/rule
  đầy đủ; cache ignored/historical provenance không được coi là runtime còn sót.
- [ ] Speaking và audio fallback hiện có đều dùng OpenAI; không leak key;
  model text giữ nguyên, no paid startup, captions/SRT/VTT không gọi audio API.
- [ ] Timestamp import là measured audio windows, công bố độ chính xác đúng,
  test bytes/timebase + browser sync đạt; không chỉ tắt missing_timestamps guard.
- [ ] Receipt/access/cancel/NoSpeech/replay/XP/ownership/atomic publish không regress.
- [ ] Teammate lưu một key local, command launcher dùng lại; doctor/docs của4AI
  clients không còn hướng dẫn tự bật Whisper; FFmpeg import setup rõ.
- [ ] Build/focused/full/JS/scripts/tooling và pending-model/diff/secret checks đạt;
  skip/live/manual/provider gates ghi đúng chứng cứ.
- [ ] Ledger ghi số liệu, source changes, removed tests→replacement coverage,
  commands, known limits, API budget, no production/remote stage.

Handoff cuối bằng tiếng Việt: kết quả/files/checks/risks; link ledger và hướng
dẫn teammate. Implementation/offline completion khác bounded-live verification
và khác production readiness. Không mở lại hai Plan đã đóng bằng scope creep;
bug mới liên quan chính thay ASR thì tự sửa và viết regression phù hợp.
