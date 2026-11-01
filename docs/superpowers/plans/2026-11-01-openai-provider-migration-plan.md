# Plan 2 — chuyển Gemini sang OpenAI GPT-6 Luna low, audio sang Whisper

Ngày lập: 01/11/2026, UTC+7. Baseline khảo sát: master `03c9c5c`.
Trạng thái: **đã triển khai/review/verify local theo quyền owner; code/offline và live gates xem
[execution ledger](../../feature-audits/2026-11-01-openai-provider-migration-ledger.md)**. Theo chỉ đạo mới nhất của owner ngày
01/11/2026, ứng dụng dùng `gpt-6-luna` với `reasoning.effort=low` cho văn bản;
Whisper cho audio. Owner dùng **Codex với GPT-6.1 Sol high để thực thi plan**;
model của agent lập trình không phải model API của website. Lời đính chính này
thay thế cách hiểu nhầm Sol high là target ứng dụng trong bản nháp trước.

Review follow-up và launcher/key hiện hành:
[review closeout](../../feature-audits/2026-11-01-openai-migration-review-closeout.md).
Owner đã cho phép fallback OPENAI_API_KEY khi app env/User Secrets chưa có key;
giữ đúng precedence và chỉ map trong child environment của launcher.

Phụ thuộc: [Plan 1](2026-11-01-whisper-local-lifecycle-plan.md) và
[kết quả chốt review](../../feature-audits/2026-11-01-whisper-plan1-final-closeout.md).
Chỉ thực thi khi owner giao Plan 2. Prompt:
[codex-openai-provider-migration.md](../../../.ai/prompts/codex-openai-provider-migration.md).

## 1. Mục tiêu, phạm vi và quyền

Toàn bộ runtime Gemini trong hệ thống được thay thế: text→OpenAI Responses,
audio fallback→Whisper. Sau triển khai, code/config chung được chia sẻ qua Git;
mỗi máy đã có prerequisites local chỉ cần nhập API key của mình và chạy launcher.
Lần setup đầu có thể tải artifact đã khóa pin; không yêu cầu đồng đội sửa source,
model, endpoint hoặc tự cài thủ công các bước chưa được tài liệu hóa.

- Một agent Codex (owner chọn GPT-6.1 Sol high) triển khai/review/verify inline. Chỉ bắt đầu khi được
  owner giao prompt này sau review Plan 1. Giữ nhánh/code Plan 1 đã review;
  không tự checkout/pull làm mất thay đổi chưa commit. Gợi ý nhánh `codex/openai-local-provider`.
- Quyền local bao gồm Program.cs, appsettings.json/appsettings.Development.json
  cho provider/defaults/DI liên quan trực tiếp. Không đổi framework/csproj chỉ vì
  muốn thêm SDK; ưu tiên HttpClient+System.Text.Json đang có. Dependency mới thực
  sự cần cho media phải có provenance/pin/license, ghi lý do và kiểm tra riêng.
- Chỉ sửa provider của ứng dụng; giữ nguyên model/account/config của Cursor,
  Codex, Claude Code, Antigravity. Không đổi Google OAuth, YouTube, SMTP, Pixabay,
  payment, ML.NET classifier hoặc dữ liệu học để đạt mục tiêu bỏ Gemini.
- Không commit/push/remote CI/PR/merge/deploy hoặc thay Plesk/gate nếu owner chưa
  cấp bước đó. Source defaults mới không có nghĩa hosting đã chuyển provider.
- Không đọc/in key có sẵn. Không cần key để hoàn thành adapter và tests giả lập.
  Hoàn tất phần code/offline rồi báo owner nhập key cục bộ; live API còn thiếu key
  phải ghi Not run. Không hỏi key trước và bỏ dở phần có thể làm độc lập.

## 2. Nguồn API và quyết định đã xác minh

Tra tài liệu chính thức ngày01/11/2026, thực thi phải kiểm lại nếu API thay đổi:

| Quyết định | Căn cứ |
| --- | --- |
| Model ứng dụng | [gpt-6-luna](https://developers.openai.com/api/docs/models/gpt-6-luna), effort low được hỗ trợ; audio/video không được hỗ trợ |
| API text | [Responses migration](https://developers.openai.com/api/docs/guides/migrate-to-responses); dùng POST /v1/responses, không bê JSON Gemini sang endpoint mới |
| Tham số | [GPT-6 migration quickstart](https://developers.openai.com/api/docs/guides/latest-model#update-api-and-model-parameters): effort khác none thì bỏ temperature/top_p/top_logprobs |
| Output | Duyệt output items theo type, lấy message/output_text; reasoning item có thể đứng đầu; JSON/usage khác Gemini |
| Budget | [Reasoning guide](https://developers.openai.com/api/docs/guides/reasoning): max_output_tokens gồm reasoning và visible output; không đổi model để né output thiếu |

Base URL mặc định `https://api.openai.com/v1`, model `gpt-6-luna`, effort `low`,
API key rỗng trong Git; Authorization Bearer trong header, không đưa key vào URL.
Chọn `store:false`, gửi context đang được ứng dụng quản lý; không thêm dịch vụ
conversation/file/tool của OpenAI trong scope này. Không coi tùy chọn store là
cam kết không có mọi loại lưu log ở nhà cung cấp.

## 3. Inventory bắt buộc — đã tìm thấy các đường đi sau

| Nhóm | File hiện tại | Việc phải làm/giữ contract |
| --- | --- | --- |
| Client chung | Services/AI/GeminiProviderClient.cs, IAiProviderClient.cs, AiOptions.cs, AiProviderRequestOptions.cs, AiProviderReply.cs, Program.cs | OpenAiProviderClient và DI; giữ abstraction/reply/exception để consumers không bị rewrite không cần thiết |
| AI Chat | Services/AI/AiChatService.cs, AiStreamingService.cs, AiContextBuilder.cs, AiObservabilityService.cs | Hội thoại, token budget, lỗi/cancel, rate/concurrency guard và giao thức stream hiện có |
| Writing feedback | WritingAiEvaluationService.cs, WritingService.cs | Giữ Local mặc định và optional feedback3s; AI không đổi pass/XP trong Local |
| Writing generation | WritingService.Generation.cs | JSON song ngữ, quota, fingerprint/replay, transaction, timeout và output cap theo tác vụ |
| Vocabulary suggestions | VocabSuggestService.cs | Dữ liệu từ/nghĩa và parser; không đụng Pixabay |
| Reading translation review | ReadingTranslationService.cs | Shape score/feedback/approval và quyền sở hữu; giữ validation nghiệp vụ |
| Listening quiz/translation | ListeningService.cs | JSON quiz/dịch theo batch, lỗi/retry, translation count và timeout; bỏ log ghi nhầm tên Gemini |
| Audio gọi Google trực tiếp | YoutubeTranscriptService.cs: GetTranscriptForSpeakingImportAsync→ProcessAudioWithGeminiAsync | Bắt buộc xử lý riêng ở P4; đổi DI client chung không đụng được luồng này |
| Speaking learner import | SpeakingService.cs | Vẫn caption/SRT/VTT-only; không tự mở thêm audio fallback cho người học Premium |
| Default config/team bootstrap | appsettings.json, appsettings.Development.json, scripts/start-local.ps1, quickstart, README | Default OpenAI chung, local key riêng; xử lý Google endpoint/model cũ trong override |
| Nội dung hiện hành | Views/Home/landing.cshtml, README, active runbooks | Bỏ thông tin sản phẩm đang tích hợp Gemini; không sửa các audit/ledger lịch sử để xóa dấu vết |
| Tests | GeminiProviderClientTests, AiPhase* tests, Writing*, PremiumWriting*, SpeakingImport*, fixtures provider | Adapter tests mới, cập nhật fixture/default assertions có liên quan, giữ coverage hành vi |

Đường dẫn dịch vụ tính từ TCTEnglish/. Khi thực thi, CodeGraph truy call sites
IAiProviderClient và bounded search raw Google endpoint/config aliases để hoàn tất
inventory. Không coi bảng này là danh sách đóng; phát hiện mới phải được ghi lại.
Điểm quan trọng: trang Admin AI đang quản lý ML.NET intent classifier, không phải
trang nhập API key. Không gỡ classifier hoặc đổi model ML.NET thành GPT-6 Luna.

## 4. Các bước theo thứ tự

### P0 — Preflight và contract map (P0)

- [ ] Kiểm review/ledger Plan 1 và code thực tế, không dựa vào status cũ. Ghi
  branch/SHA/dirty files/ports; bảo toàn speech lifecycle và dữ liệu local.
- [ ] Tạo bảng consumer→request shape→result parser→failure contract→tests. Kiểm
  cả DI chung lẫn HttpClient raw, file JS/view/docs/config, không chỉ grep Gemini.
- [ ] Đọc skill liên quan từng bước; CodeGraph đầu tiên. Thay text UI cần DESIGN
  và tct-ui-component; không thiết kế lại layout. Không tạo migration nếu không
  có persisted model change; nếu có, áp dụng tct-ef-migration theo AGENTS.
- [ ] Không gửi backup/dữ liệu production làm smoke API. Test bằng nội dung tổng
  hợp và tài khoản local test; không ghi đè lịch sử request/model trong DB.

### P1 — Shared options và OpenAI client, test đỏ trước (P0, sau P0)

File dự kiến: AiOptions.cs, AiProviderRequestOptions.cs nếu cần, mới
OpenAiProviderClient.cs, mới OpenAiProviderClientTests.cs, DI Program.cs sau tests.

- [ ] Reuse `AI:*` để đồng đội chỉ thêm AI:ApiKey. Thêm `ReasoningEffort=low` vào
  options; model ID chính xác gpt-6-luna, không gửi tên UI như "Luna Low".
- [ ] Dùng HttpClient đã có; dựng URI chính xác một lần /v1/responses, giữ role
  và thứ tự system/user/assistant. Gửi reasoning.effort, max_output_tokens và
  store:false. Không gửi temperature từ defaults hoặc per-request overrides khi
  low; không phát sinh tools/background/prompt-cache config mới ngoài scope.
- [ ] Parse message/output_text đúng thứ tự; không lấy output[0] làm text; xử lý
  empty/malformed/refusal/failed/incomplete kể cả HTTP200. Nội dung bị cắt không
  được parse/lưu như JSON thành công. Không đưa reasoning text vào UI/log.
- [ ] Map usage input/output/total và returned model/request ID vào AiProviderReply.
  Output usage đã bao gồm reasoning thì không cộng lần hai. Không thay counter
  chars/4 thành lời hứa đếm chính xác; usage thực dùng cho observability/quota.
- [ ] Giữ lỗi theo AiProviderException; phân biệt invalid config, auth/model
  access, rate limit, quota, server, timeout/network. Không lộ body/header/key.
  Retry lỗi tạm thời có giới hạn và respect Retry-After/remaining deadline;
  insufficient quota/400/401/403 không retry vô ích. Caller cancel phải propagate.
- [ ] Deadline bao gồm send/body/parse/delay. Không lấy timeout riêng mỗi retry
  làm vượt budget3s của Writing optional. Không tự fallback sang Gemini/model khác.
- [ ] Unit matrix: request URI/header/roles/model/low/absence temperature, per-task
  cap/timeout; response reasoning-first/multi-text/JSON/refusal/incomplete/invalid;
  usage/missing usage, 400/401/403/404/429 quota-vs-rate/5xx/network, cancellation
  khi send/read/backoff; fake handler đảm bảo không có live HTTP trong tests thường.

### P2 — Tích hợp tất cả text consumers (P0, sau P1)

- [ ] Đổi DI default thành OpenAiProviderClient; xóa runtime Gemini client nếu
  hết caller, thay tests của adapter tương ứng. Không giữ Google fallback ngầm.
- [ ] Giữ HTTP/JSON/SignalR/stream contract của app. Stream hiện tại đi qua chat
  service rồi chia chunks; không tự đổi sang protocol native streaming của OpenAI.
- [ ] Chạy từng consumer bằng fake OpenAI HTTP pipeline qua DI thật, không chỉ
  stub IAiProviderClient để bỏ qua serializer/parser mới.
- [ ] Bảo toàn JSON contracts của Writing feedback/generation, Vocab, Reading,
  Listening. Structured outputs nếu dùng phải theo schema thực và text.format,
  không ép mọi tác vụ thành cùng một object khi consumer nhận array/plain text.
- [ ] Kiểm budget theo tác vụ: chat hiện600, generation2400, vocab300, Listening
  dịch1500 là mốc cũ, không phải cam kết đủ cho reasoning model. Tách cap có giới
  hạn theo workload; kiểm truncated JSON và visible output trong live smoke.
  Ghi giá trị cuối/lý do, không tự nâng tất cả lên128K hoặc đổi effort/model.
- [ ] Luna low vẫn cần đo latency và reasoning-token usage thật; không giả định các
  cap300/600 hoặc deadline cũ đủ cho mọi tác vụ. Thiếu output phải báo incomplete,
  không retry vô hạn hay âm thầm đổi model/effort sang none. Giữ ngân sách3s của Writing
  optional: nếu cloud chưa trả kịp thì dùng kết quả Local, không kéo chậm thao tác
  lưu bài hoặc tuyên bố nhận xét nâng cao luôn có trong3s. Chưa có key thì ghi
  calibration/live latency Not run, không tự tiêu tiền để benchmark mọi feature.
- [ ] Giữ Local Writing + CloudEnhancementEnabled=false mặc định. Test API tắt,
  lỗi/mất key/quá3s vẫn giữ attempt/progress theo local; cloud không đổi pass/XP.
- [ ] Chat/context/budget/rate guard, internal knowledge và admin intent training
  vẫn hoạt động; log/model mới phản ánh đúng provider, không sửa lịch sử cũ.

### P3 — Key/config chung và trải nghiệm đồng đội (P0, sau P2)

File dự kiến: appsettings*.json, scripts/start-local.ps1,
scripts/set-local-ai-key.ps1 (mới), docs/operations/openai-local-setup.md (mới), README.

- [ ] Default không bí mật được commit cho mọi máy: BaseUrl OpenAI, model GPT-6 Luna,
  effort low, key rỗng. Tất cả môi trường source hết default Gemini. Chưa deploy
  production; không đổi secret/app pool trên hosting.
- [ ] start-local giữ key đã có, không chép key vào `.appdata/local/database.env`,
  generated JSON public hoặc front end. Chỉ normalize legacy Google model/base
  override ở local khi xác minh đúng giá trị cũ, báo tên setting thay đổi, không
  dump toàn bộ User Secrets. Custom non-Google overrides phải validate/báo rõ.
- [ ] Helper nhập key bằng masked prompt, ghi đúng User SecretsId từ csproj;
  không echo key, không đưa key vào argv/history/log hoặc ghi lại key giả để test
  trên secrets thật. Dùng fixture secret store khi test; atomic merge giữ các
  OAuth/SMTP/DB/billing/speech keys, không serialize hỏng kiểu nested/flat JSON.
- [ ] Hỗ trợ AI__ApiKey qua env nếu team dùng env. Không tự coi OPENAI_API_KEY
  được .NET bind vào AI:ApiKey nếu chưa có mapping rõ và test precedence.
- [ ] App vẫn boot khi key trống để học local; chỉ cloud feature báo chưa cấu
  hình. Credential không hợp lệ hoặc model không có quyền cần lỗi actionable,
  không âm thầm chạy model khác hoặc báo "key nhập là chắc chắn dùng được".
- [ ] Cold teammate checkout: prerequisites PS7/.NET9 runtime+SDK/Node/Git/Docker
  là setup một lần hiện hành. Launcher khôi phục các artifact pin cần thiết,
  giữ DB riêng; không cần sửa code. API credit/quyền model/network là điều kiện
  bên ngoài, một key bất kỳ không bảo đảm đủ quyền hay số dư.

Lệnh bàn giao dự kiến, chỉ chạy được sau khi helper được triển khai:

```powershell
pwsh -NoProfile -File scripts/set-local-ai-key.ps1
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Không yêu cầu owner gửi key vào chat. Nếu dùng cùng key cho nhóm, mỗi máy tự nhập
cục bộ; quota dùng chung phải ghi rõ. Không cần mỗi người sửa model/endpoint.

### P4 — Thay audio Gemini trực tiếp bằng Whisper (P0, sau P1 + Plan 1)

Đây là hạng mục bắt buộc để kết luận **toàn bộ runtime đã bỏ Gemini**; không thể
POST inline_data audio của Google vào GPT-6 Luna. Consumer raw hiện tải audio YouTube
vào RAM rồi gọi Google và chia thời gian ước lượng; cần giữ đường đi captions
trước và xử lý audio bằng ASR có timestamps thật.

File dự kiến: YoutubeTranscriptService.cs, một audio transcript adapter/options
trong Services/Speaking/, DI Program.cs, tests transcript fallback mới; helper
bootstrap media/lock khi cần. Giữ learner Speaking captions-only ở SpeakingService.

- [ ] Inspect toàn bộ call chain GetTranscriptAsync/GetTranscriptForSpeakingImportAsync
  và từng caller trước khi sửa. Giữ transcript result contract/ownership và
  validation tiếng Anh. Không âm thầm xóa audio fallback của các caller dùng chung.
- [ ] Captions tốt→return ngay, không download audio/call ASR. Không captions→
  download chỉ audio với cancellation, byte/duration deadline; không tải video.
  Decode ra PCM16 mono16k bằng FFmpeg path đã xác minh; không build shell từ URL
  hoặc tên file người dùng. Dùng ArgumentList, temp owned ngoài webroot, cleanup
  chính xác sau success/fail/cancel; không log audio/base64.
- [ ] Máy Châu có FFmpeg8.1.1 trên PATH lúc khảo sát. Xác minh resolved executable,
  SHA/provenance/license; không coi PATH là pin. Team cần lock artifact FFmpeg
  cụ thể và setup local có checksum nếu thiếu phiên bản tương thích. Chốt URL/hash
  khi thực thi; đây là dependency chưa khóa trong repo, phải hoàn tất trước khi
  tuyên bố teammate chỉ thêm key là dùng được. Không cài/upgrade FFmpeg toàn máy.
- [ ] Không đưa video dài vào clip transcriber15s/2MiB. Tách adapter bounded
  audio import: chunk nếu cần, offset/merge segment timestamps thật, xác minh
  range/order/overlap và thời lượng. Không chia đều plain transcript để giả timecode.
- [ ] Đặt rõ giới hạn trước khi network: đề xuất max nguồn30min/100MiB, PCM stream
  ra disk có cap, tổng xử lý300s và chunk deadline phù hợp. Đo trên fixture và ghi
  giới hạn cuối; không nâng MaxAudioSeconds của bài nói để nhận video dài. Nếu
  vượt giới hạn, báo unavailable/too-large/cancelled đúng contract, không lưu bài
  nửa vời. Không coi mọi YouTube/network failure là thiếu API key.
- [ ] Long-audio và bài nói dùng cùng engine một inference: có admission chung
  hoặc chunk nhường lượt, không bypass limiter gây bão CPU. Không auto start
  server trong web request/production; dùng lifecycle Plan 1 trên local.
- [ ] Test caption success không ASR, valid audio+timestamps, missing engine,
  invalid/no-speech response, download/codec error, limit/cancel/timeout, cleanup,
  no duplicate/partial publish. Một probe WAV không chứng minh download/decode path.
- [ ] Live fixture hữu hạn có provenance cần chạy; YouTube live có thể bị network
  chặn phải ghi riêng Not run, vẫn hoàn thành fake download/real decode+ASR test.
  Không thêm OpenAI transcription model khác; audio theo owner đã chọn Whisper.

### P5 — Nội dung hiện hành và kiểm tra không còn runtime Gemini (P1, sau P2–P4)

- [ ] Đổi landing/README và runbook đang hướng dẫn key/provider sang OpenAI cho
  text, Whisper cho audio; không ghi mô hình local giải quyết mọi nhu cầu AI.
- [ ] Bỏ method/parser/aliases Google audio không còn dùng; cập nhật lỗi của
  learner Speaking thành lời hướng dẫn phụ đề hữu ích thay vì tên provider cũ.
- [ ] Search có phân loại: runtime endpoint/default/key alias/DI phải hết Gemini;
  Google OAuth/YouTube/Pixabay được giữ; audit/plan cũ và test legacy migration
  được phép nhắc Gemini. Không replace toàn repo làm sai lịch sử/license.
- [ ] Update tests dùng old model strings nếu kiểm default/runtime. Test lịch sử
  hoặc migration từ legacy vẫn giữ fixture Google có chú thích. Scan không in
  credential/backup; kiểm không có payload audio gửi api.openai.com GPT-6 Luna.

### P6 — Verification và bàn giao key (sau P5)

1. Focused unit/HTTP/DI và consumer regression trước. Chạy script-key tests với
   fixture secrets; launcher compatibility và speech lifecycle không bị regress.
2. Build/full app suite; EF pending-model check. Không reset DB dev cho test;
   MariaDB acceptance chỉ trên fixture được guard/owned bootstrap như quy định.
3. Key chưa có: hoàn thành code/offline, ghi cloud-live Not run và báo owner
   lệnh nhập key. Không khẳng định OpenAI đã live hoạt động. Có key được owner đặt
   cục bộ thì chạy smoke tối thiểu text/JSON bằng dữ liệu tổng hợp, bounded quota,
   kiểm actual returned model/usage và log redaction; không bắn toàn bộ suite lên API.
4. Dùng browser test local: chat+hội thoại tiếp, writing generation+feedback
   optional bật có chủ đích rồi trả default, vocab suggestion, reading review,
   Listening quiz/translation. Writing Local và speech phải chạy khi cloud key
   thiếu/endpoint mất. Các tương tác chưa chạy ghi riêng từng ca.
5. Cold teammate fixture: config Git sạch + secret store trống→nhập một key→
   launcher→cloud text+Whisper. Mô phỏng model/account failure bằng fake handler;
   không xóa secrets máy Châu để làm demo. Ghi rõ prerequisite còn thiếu nếu có.

```powershell
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --no-build
dotnet tool run dotnet-ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --configuration Release --no-build
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
node --test scripts/tests/test_agent_setup.cjs
git diff --check
```

Tests live opt-in không được tự skip sau khi owner đã bật mà engine/API lỗi.
Không chạy live cloud trong CI/test discovery. Build/tests do thay đổi adapter,
config và media path, không phải chỉ vì thay tên model.

## 5. Acceptance và ledger

Tạo `docs/feature-audits/2026-11-01-openai-provider-migration-ledger.md` khi thực thi,
theo từng gate: Implemented / Offline pass / Live pass / Not run / Blocked.

- A1: mọi text consumer sử dụng adapter OpenAI thực qua DI, model GPT-6 Luna low,
  JSON/usage/cancel/budget đúng; không còn runtime Google endpoint/key fallback.
- A2: audio Gemini đã thay Whisper với timestamps và download/decode hữu hạn;
  learner import không bị mở rộng ngoài caption/SRT/VTT; lifecycle Plan 1 giữ được.
- A3: teammate không sửa source; defaults chung, key local, prerequisite/artifact
  setup đầy đủ, restart giữ key/DB/worker safety. Không hứa chỉ nhập key nếu FFmpeg
  hoặc dependency còn chưa provision.
- A4: lỗi/mất cloud vẫn học Writing Local và dùng speech local được; không fake
  score/credit, không bỏ thông báo cấu hình engine. Chất lượng Writing free-form
  không tự trở thành hoàn thiện chỉ vì đổi nhà cung cấp.
- A5: full regressions xanh, pending-model check không đổi; secrets/artifact không
  vào Git, UI copy đúng, schema impact ghi rõ, production chưa deploy.
- A6: live cloud từng ca có bằng chứng hoặc ghi blocker key/account/network;
  hoàn thành offline không được đổi nhãn thành toàn hệ thống đã nghiệm thu live.

Báo owner khi code sẵn sàng để nhập key và kiểm local; không tự commit/push/merge.
Review/fix log theo AGENTS. Nếu có blocker thật, ghi phần đã xong và cách giải quyết
nhỏ nhất; không quay về Gemini để che lỗi hoặc đổi model ngoài GPT-6 Luna low.

## 6. Rollback local

Ghi nonsecret defaults/DI thay đổi trong ledger và giữ diff có thể review. Nếu cần
rollback, revert riêng source/config của đợt sau khi bảo toàn thay đổi khác, không
reset branch/DB. Giữ Whisper Plan 1 và key cục bộ của từng máy; không đưa key
OpenAI vào endpoint Google. Production gate vẫn false, rollback local không là
lệnh deploy. Không sửa migration/history hoặc request logs cũ để đổi tên provider.
