# Repair OpenAI Speech replacement sau review03/11

Trạng thái: ĐÃ THỰC THI (offline + fake browser) 2026-11-03. Scope chỉ R1–R7
và các gate liên quan trong
[review](../../feature-audits/2026-11-03-openai-speech-review.md).
Owner giao prompt này mới cho phép fix. Một agent, giữ branch/dirty work, không
reset/stash/commit/push/PR/CI remote/merge/deploy. Model audio gpt-transcribe,
text gpt-6-luna/low; no paid API mặc định, production gatefalse.
Paid live vẫn unverified. Evidence:
[ledger](../../feature-audits/2026-11-03-openai-speech-replacement-ledger.md).

## A. Chuẩn bị

- [ ] Snapshot branch/dirty source; giữ các Plan UI/logic trước, schema/history,
  caches/private keys và web cũ PID7084:5112.
- [ ] Đọc AGENTS, original STT Plan, ledger/review, skill engineering/superpowers/
  speaking; API/security/tooling khi chạm đúng batch.
- [ ] CodeGraph source-first; chuyển reviewer probes trong .tmp/stt-review thành
  regression chính thức có assertions; không commit generated probe bin/obj.
- [ ] Baseline reviewer870/0/12 chỉ là evidence cũ. Mỗi fix có red→green riêng.

## B. PCM/timecodes — R1(P1) rồi R2(P2)

Files: PcmSpeechWindowPlanner.cs, AudioTranscriptImporter.cs,
PcmSpeechWindowPlannerTests.cs, AudioTranscriptImporterTests.cs.

- [ ] Red21s: voiced0–10, pause100ms, voiced10.1–21. Assert windows ordered/
  disjoint, <=10s, sourcebounds, không bỏ speech và không duplicate samples.
- [ ] Normalize overlap trước hard split: có thể union overlapping intervals
  không bị giới hạn10s, rồi split kết quả; positive-gap merge vẫn có cap phù hợp.
  Không sửa bằng drop nguyên cửa sổ/cắt mất tiếng hoặc relaxed overlap tolerance.
- [ ] Validate mọi window (monotonic/nonoverlap/range/cap/overflow-safe) trước
  cloudcall đầu. Test fake planner trả overlap →0HTTP; real planner case trên
  phải import thành công với provider fake, không publish partial.
- [ ] R2 red:400Hz tone10s, mỗi10frame20ms có9frame amplitude1400,1frame3500;
  assert nonempty candidate windows. Không gọi fixture này là giọng người thật.
- [ ] Sửa threshold để có ý nghĩa với observed range, không vượt peak do
  noise*3.5 trong khoảng ratio đã tái hiện. Test pure silence/quiet voice/noise/
  varied-volume/continuous/multiple phrases; không coi mọi energy là speech.
- [ ] Giữ measured sample offsets/WAV bytes, <=15s app input, max10s/window,
  <=300s total/180 requests, English usability và captions/subtitle0cloud.

Gate: R1/R2 regressions green và importer integration dùng real planner+fake
provider đạt; không chỉ unit test bằng fake planner.

## C. Shared live budget — R3(P1), chưa bật paid live

Files: Infrastructure/OpenAiSpeechLiveFactAttribute.cs,
OpenAiSpeechLiveAcceptanceTests.cs, AudioImportLiveAcceptanceTests.cs,
new shared live-budget helper/DelegatingHandler + offline tests.

- [ ] Một budget instance cấp process/live run dùng chung mọi live audio path.
  Enforce ngay trước HTTP send, không private counter riêng từng test class.
- [ ] Atomic reserve count+audio seconds, đếm cả failed/canceled attempt sau
  reservation. Max8POST/120s cho cả đợt; không reset theo test method/client.
- [ ] Handler kiểm đúng duration của WAV/manifest được xác minh, không tin
  caller khai duration nhỏ tùy ý. Không đọc lại stream làm gửi body rỗng.
- [ ] Fake transport tests:8được gửi,9bị chặn;120sđược gửi, vượt bị chặn; import
  và direct/app assessment chia cùng budget; parallel calls không overshoot.
- [ ] Flag off/old loopback flags khôngnetwork. Flag on thiếu key/fixture fail
  trước paid send. No retry; output không key/private audio/transcript.
- [ ] Bổ sung positive speech-through-app receipt/commit/replay coverage bằng
  fake OpenAI HTTP. Paid variant chỉ owner cho phép sau offline review.

Gate: Có proof budget cross-client/cross-import trước khi tài liệu hướng dẫn
owner bật flag. Không gọi OpenAI để chứng minh guard.

## D. Credentials/error handling — R4/R5(P2)

Files: OpenAiSpeechCredentialResolver.cs, OpenAiSpeechTranscriber.cs và tests.

- [ ] R4 red cho AI__ApiKey explicit-invalid/empty + lower config/global valid;
  shared AI:ApiKey explicit-invalid/empty + OPENAI_API_KEY valid. Không fallback.
- [ ] Missing vẫn fallback theo contract. Không thêm ApiKey="" vào config mặc
  định làm chặn fallback cho mọi teammate. Test source/key selection, không in key.
- [ ] Đồng bộ với launcher/local key behavior; giữ text model/key authority.
  Redact credential ToString/diagnostics, không log record chứa ApiKey.
- [ ] R5 fake stream trả headers200 rồi throw IOException khi đọc. Kết quả
  unavailable/503,1send, lease nhả, request tiếp theo thành công.
- [ ] Giữ OCE của caller; internal deadline timeout; malformed/oversized JSON
  vẫn đúng; không broad catch biến programming bug thành silent NoSpeech.

## E. Admission và status/copy — R6/R7(P2)

Files: SpeakingAssessmentAdmission.cs/tests, speech-status.ps1,
test_local_launcher.py/test_local_ai_config.py hoặc test speech-status riêng.

- [ ] R6 red10.000users, fake clock+2h,1active user; hết hạn phải được evict,
  không chỉ remove timestamps khi đúng user quay lại.
- [ ] Cache/bucket count bounded thật, active bucket không bị evict để bypass
  guard; xử lý capacity exhaustion rõ. Giữ10/min/user, time-boundary/parallel/
  cross-user tests và access-before-admit; không thêm distributed DB quota.
- [ ] R7 status dùng effective precedence: env>secrets>defaults cho Enabled/
  Model/BaseUrl/key. Test Enabled=false, custom env Model, invalid BaseUrl,
  explicit invalid key, no key. Có thể tách KeyPresent/Enabled/EndpointValid/
  Configured; VerifiedLive luônfalse khi metadata-only.
- [ ] Không network trong status/launcher; không echo key/argv. Sửa active
  Whisper wording ở Admin Create và limiter comment; history/DB provenance giữ.

## F. Verification/handoff

- [ ] Focused tests theo batch; full app optimized config mới hoặc Debug khi
  Release binary đang bị old web khóa. Không kill theo process name.
- [ ] Full JS12cases hiện tại + regressions; Python discover/tooling validators
  khi script/rule đổi. Ghi Ran92/skip1 đúng91executed pass, không92+1.
- [ ] Browser bằng launcher Debug ở free port với **fake audio endpoint/key**
  process-local khi cần tránh paid provider; không sử dụng key thật trong harness.
  Kiểm Standard/Premium/Admin, speech submit/cancel/replay, import/playback
  timecodes,390px. Fake UI không được gọi live receipt proof.
- [ ] Ordinary startup/API health0paidcalls; không Whisper spawn/download;
  no secret; pending-model nếu cần nhưng không DB migration/reset.
- [ ] git diff --check; update ledger STT4/STT7/STT8 theo actual proof, bug log
  sau fix, known issues phù hợp. Không giữ blanket pass khi browser/live chưa chạy.
- [ ] Handoff Vietnamese: R1–R7 trạng thái, files, tests/red-green, browser/
  live limits, remaining gates. Dừng local, không remote stage.

Nếu owner chưa cho phép live: hoàn tất code+offline/browser fake, giữ paid live
unverified. Không lấy việc thiếu live authority làm lý do bỏ các sửa offline.
