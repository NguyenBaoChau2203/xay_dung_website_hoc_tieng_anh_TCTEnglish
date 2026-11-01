# Plan 2 execution ledger — 01/11/2026

Một agent triển khai, review và kiểm thử inline. Website: `gpt-6-luna`,
`reasoning.effort=low`; audio: Whisper. Không Git publish hoặc production.

## Baseline và bảo toàn

- Nhánh kế thừa `codex/whisper-local-lifecycle`, HEAD `03c9c5ce8d30e84a5320985b90255bbbccf922ec`.
- Diff/hash Plan 1 trước sửa: `.tmp/plan2/plan1-baseline.diff` và
  `.tmp/plan2/plan1-baseline-hashes.json` (ignored).
- Preflight: MariaDB cổng3307 đang chạy; web5100/Whisper8092 chưa lắng nghe.
- Không đọc/in key, reset DB, dừng workloads khác hoặc thay native trust.

## Checklist thực thi

- [x] P0: đọc prompt, plan, Plan 1 closeout; snapshot baseline; CodeGraph inventory.
- [x] P1: regressions đỏ→xanh cho Responses adapter, deadline/retry/parser/usage.
- [x] P2: DI thật + fake HTTP cho từng text consumer, giữ local Writing/quota/replay.
- [x] P3: defaults, masked key helper, legacy override, secret-store fixture.
- [x] P4: audio-only bounded download, pinned decode, Whisper timestamps/admission.
- [x] P5: UI copy và tài liệu hiện hành, scan phân loại runtime.
- [x] P6: focused/full tests, build, EF pending-model, tooling, review và handoff;
  các acceptance chưa chạy được liệt kê riêng, không đổi thành pass.

## Consumer contract map

| Consumer | Request → parser | Failure/điều cần giữ | Evidence dự kiến |
| --- | --- | --- | --- |
| AiChatService + AiStreamingService | ordered system/history/user → plain text | ownership, conversation lease, token/rate guards, app chunks | adapter + HTTP/DI chat tests |
| WritingAiEvaluationService | system/user → strict feedback object | Local default, optional3s, không đổi pass/XP | adapter/consumer + existing Local regressions |
| WritingService.Generation | bilingual generation → exercise JSON | quota, fingerprint replay, transaction, incomplete không persist | fake HTTP + PremiumWritingGeneration |
| VocabSuggestService | user → definitions object | giữ Pixabay riêng | fake HTTP consumer |
| ReadingTranslationService | system/user → score/feedback/isApproved | validation, approval và access | fake HTTP consumer + existing integration |
| ListeningService quiz | user → question array | không persist incomplete | fake HTTP consumer |
| ListeningService translation | user → ordered string array batches30 | count validation, timeout | fake HTTP consumer |
| YoutubeTranscriptService | captions first, otherwise audio-only | shared/admin fallback, learner captions-only | fake media + real decode/ASR |
| InternalKnowledgeProvider/admin intent | local ML.NET/knowledge | giữ nguyên, không phải cloud provider | existing tests |

## API reference

Đã fetch lại tài liệu OpenAI chính thức: [Luna](https://developers.openai.com/api/docs/models/gpt-6-luna),
[Responses](https://developers.openai.com/api/docs/guides/migrate-to-responses),
[GPT-6 parameters](https://developers.openai.com/api/docs/guides/latest-model),
[reasoning budget](https://developers.openai.com/api/docs/guides/reasoning).
Không có Context7 trong phiên. Luna low hỗ trợ; bỏ temperature; output cap gồm
reasoning, `incomplete` không phải JSON thành công. Model/account access chưa live.

## Gates (cập nhật sau kiểm chứng)

| Gate | Trạng thái | Bằng chứng/giới hạn |
| --- | --- | --- |
| A1 text | Implemented / Offline pass / Live synthetic pass | Tất cả text consumers qua Responses adapter thật; actual gpt-6-luna/low |
| A2 audio | Implemented / Offline pass / Live local pass | Fake media HTTP → pinned FFmpeg thật → Whisper thật; YouTube live Not run |
| A3 teammate/key | Implemented / Offline fixture pass | Empty secret store, nested/flat preservation, cold media restore; full clean-machine end-to-end Not run |
| A4 local resilience | Implemented / Offline pass / Live optional pass | Missing/auth/slow/off/success giữ Local attempt/progress; cloud không đổi pass/XP |
| A5 regressions/schema | Offline pass | 805 pass/0 fail/8 skipped of813, scripts99, Node9, pending-model không đổi |
| A6 cloud/browser live | Live backend pass / Browser limited | 11 requests tổng hợp; landing desktop/mobile pass, authenticated cloud browser journeys Not run |

## Implementation và review inline

- `OpenAiProviderClient`: HttpClient/System.Text.Json hiện có; không thêm SDK hay
  sửa csproj/framework. URI Responses một lần, ordered roles, Bearer header,
  store:false, Luna low, không sampling/audio. Typed message/output_text parsing
  bỏ reasoning/tool items; refusal/incomplete/malformed/empty không success.
- Usage input/output/total + returned model/request ID được map, reasoning_tokens
  là diagnostics đã nằm trong output, không cộng hai lần. Deadline chung gồm
  send/body/parse/backoff; body cap1MiB, retries tối đa3; Retry-After không vượt
  deadline. Auth/model/quota không retry, caller cancellation propagate.
- DI và mọi consumer giữ parser/route/HTTP/JSON/SignalR app stream. App stream vẫn
  chunk90 ký tự, có real-adapter regression. Output caps: Chat2048, feedback2048,
  generation6144, vocab1536, reading2048, quiz4096, translation6144/batch30. Đây là
  cap hữu hạn có chỗ cho reasoning, chưa benchmark toàn bộ độ dài/nội dung.
- Review đã sửa Vocabulary WhenAll nằm ngoài catch; cloud lỗi trả failure hữu ích.
  Listening count mismatch không success; regression batch31 đỏ→xanh xác nhận
  không để bản dịch batch trước trong EF tracking khi batch sau lỗi. Staging
  translations xong tất cả mới apply/save. Writing generation incomplete không
  persist, replay/quota giữ nguyên; Local Writing/cloud3s vẫn giữ attempt/progress.
- `YoutubeTranscriptService` bỏ toàn bộ Google audio/base64/parser và timecode
  ước lượng. Captions trước, shared fallback Whisper; learner captions/SRT/VTT.
  Audio source kiểm metadata30min/100MiB, stream cap thực; FFmpeg hash check,
  ArgumentList/protocol+demuxer whitelist, temp ngoài webroot, PCM cap57,600,000bytes,
  tổng300s. Chunk10s trả lease qua singleton limiter bài nói, offset ASR timestamps
  và reject missing/order/overlap/range sai. Không publish partial.
- FFmpeg8.1.1 essentials win-x64: lock archive109,282,242bytes + SHA archive/exe/license,
  source full commit, retained GPLv3 license; bootstrap chỉ launcher, không PATH,
  machine-wide install hay hook/folderOpen. Plan1 Whisper lifecycle không sửa.
- Key helper masked, không argv/history, đọc UserSecretsId từ csproj; khóa file,
  atomic merge, nested/flat conflicts fail trước write. Normalize đúng legacy
  Google overrides, giữ custom endpoint có validation, không tự đổi model khác.
  AI__ApiKey có precedence; OPENAI_API_KEY không map. Legacy Google credential
  prefix bị reject trước HTTP. Missing key vẫn boot/local học được.
- Không thay Google OAuth, ML.NET/admin intent training, SMTP/payment/Pixabay,
  AI IDE settings hoặc lịch sử request/model cũ. Không persisted-model/mapping/
  relationship change; không có migration mới. Runtime scan chỉ còn tên Google
  trong guard từ chối legacy endpoint; docs/helper/test legacy migration được giữ
  có chủ đích. Audit lịch sử không replace.

## Evidence cuối — actual commands/results

Evidence dưới `.tmp/plan2/` ignored, không credentials/audio/binaries vào Git.

| Check | Kết quả | Evidence |
| --- | --- | --- |
| Provider red | 8 fail/20 pass trên Gemini adapter cũ | provider-red.log/trx |
| Provider/consumer/media focused | 55/55 pass; text focused102/102 ở giai đoạn tương ứng | provider-media-focused.*, text-focused.* |
| Listening later-batch regression | red1/1 → green trong consumer suite20/20 | translation-red.*, consumer-final-green.* |
| Local Writing off/missing/auth/slow/success | 5/5 pass, assert persisted attempt + sentence progress, Local pass giữ nguyên | local-writing-final.* |
| Release build cuối | 0 errors, 5 warning có sẵn ở fixture/nullability/EF1002 | build-live-features.log |
| Full app suite cuối không opt-in live | **805 passed, 0 failed, 8 skipped / total813**, 2m22s | application-final.log/trx |
| Python scripts full sau launcher changes | **99 passed**, 228.968s | python-verified.log |
| Node bootstrap | **9 passed** | node-final.log |
| Tooling + native hooks | pass; không thay cho app/browser tests | tooling-final.log, hooks-final.log |
| EF pending-model | No changes since last migration | ef-pending.log |
| FFmpeg Verify + cold empty media fixture | checksum pass, repeat/offline/tamper tests pass | cold-media.log, media-script-focused.log |
| Launcher Release/5102 | app/DB health200, Whisper ready; no migrations applied | launcher-plan2.log |
| Plan1 live ASR + real media pipeline | **3 passed, 0 skipped**, 28s | local-media-live.log/trx |
| OpenAI live smoke | **1 passed**, 2 requests text/JSON | openai-live-smoke.log/trx |
| OpenAI live consumer matrix | **1 passed**, 9 calls/replay no extra call | openai-live-features.log/trx |
| Browser landing | 1440px/390px, OpenAI copy, no Gemini, no horizontal overflow; keyboard focus-visible; native screenshots inspected | browser tool results in chat; screenshot file write denied by MCP root config, native capture used |
| Diff whitespace | git diff --check pass | final shell check |

Lượt full đầu fail assertion copy cũ ở Speaking (caption/Gemini); đã cập nhật
assertion hướng dẫn phụ đề/SRT/VTT, giữ HTTP400 và no audio fallback, focused5/5
và full cuối xanh. Không xóa test để scan/pass.

Tám skip trong full thường: hai OpenAI live, hai Whisper live, một media live,
ba MariaDB acceptance. Năm bài cloud/ASR/media đã chạy riêng explicit và pass;
ba MariaDB acceptance vẫn Not run (dual completion, badge batch, expired lease).

## Live OpenAI — owner cấp key sau khi offline hoàn tất

Offline code/build/full suite hoàn tất trước. Owner sau đó cung cấp test key và
cho phép live. Dùng masked stdin → environment của process kiểm thử, retries0;
không ghi key vào source/User Secrets/argv/history/log. Các process đã thoát và
key env đã clear; owner nên thu hồi key đã đưa vào chat. Không dùng key khác sẵn
trong môi trường hoặc tự map OPENAI_API_KEY.

Post-scan wrapper lần đầu gặp lock của log full suite đang chạy; không phải
test/API failure. Scan lại riêng completed live logs/TRX và temporary helper
scripts đã pass, không chứa credential; Scan mode không gọi thêm API.

Tất cả response dưới đây trả actual model **gpt-6-luna**; requests effort=low.
Reasoning count đã nằm trong output. Latency là một mẫu nhỏ cho từng workload.

| Tác vụ | Input | Output | Reasoning (included) | Total | Latency ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Smoke text | 11 | 5 | 0 | 16 | 4025 |
| Smoke JSON | 24 | 15 | 0 | 39 | 1584 |
| Chat first | 329 | 9 | 0 | 338 | 3917 |
| Chat follow-up | 350 | 11 | 0 | 361 | 1240 |
| Writing feedback | 523 | 76 | 0 | 599 | 1564 |
| Vocabulary | 128 | 127 | 0 | 255 | 2300 |
| Reading review | 281 | 140 | 68 | 421 | 1889 |
| Writing generation | 440 | 193 | 22 | 633 | 2716 |
| Listening quiz | 208 | 582 | 72 | 790 | 6031 |
| Listening translation | 77 | 82 | 45 | 159 | 1774 |
| Optional Writing3s | 515 | 106 | 36 | 621 | 2250 |

**11 bounded requests**, tổng usage4232tokens (input2886/output1346; reasoning243
đã included). Không chạy suite qua paid API. Writing generation replay không
gọi provider lần hai. Optional live feedback về trong3s; Local passed=true,
cloudFeedback=true, lưu attempt/progress xong sau3046ms (gồm I/O DB/logic ngoài
thời gian provider). Không suy ra mọi request sẽ nhận cloud feedback trong3s.

## Media fixture, local runtime và dữ liệu

- SAPI Microsoft David Desktop en-US, “Good morning everyone”, lấy WAV Plan1
  hash8c7f20e48d19fbcbe3f3f9025d799c4a68a23766c62db4a7e8b372069372d757.
  Lặp9 lần, chèn silence0.3s mỗi lần →21.51s. WAV hash
  06907c034c96be71a07ea632271326f6440bc4f3e60b4e57dde0efc5fd454d12.
- M4A tạo bằng pinned FFmpeg AAC48k,94,259bytes, SHA256
  ed1ee014825ecddba436706eac274470155bd31d71d81e69b2a361440315152b.
  Files/provenance dưới `.tmp/plan2/`; không dùng paid TTS hoặc media người học.
- Real test fake media download HTTP handler → actual pinned decode → Whisper
  qua DI thật, timestamps nhiều chunk (có start>=10s), ordered/positive/range,
  transcript có “morning”. Silence và speech-through-app giữ completion credit1.
- Launcher mặc định Debug từng bị MSB3027 vì app Plan1 PID2748 giữ executable;
  không kill process đó. Thêm Configuration/ WebPort opt-in, dùng Release5102;
  health200, browser copy check. Instance Plan2 do phiên này mở đã dừng sau test
  để lần nhập key local tiếp theo khởi động process mới; 5102 không còn nghe.
- App Plan1 ở5100 vẫn health200; Whisper PID13092 ready loopback8092 (PID chỉ
  là snapshot, dùng Status khi cần); MariaDB giữ chạy. Không reset/restore/seed DB.
  Counts trước/sau launcher: users58, sets36, cards162, speakingvideos14 giữ nguyên;
  đây là count check, không tuyên bố checksum mọi bảng. Tests học/credit dùng SQLite riêng.
- Hash baseline Plan1: giữ core lifecycle, tests, lock/artifacts và native config;
  chỉ mở rộng launcher/runbooks/bug log và cập nhật trạng thái Plan2 theo scope.

## Phần chưa chạy và giới hạn

- Authenticated **browser** journeys cho Chat, Writing generation/feedback,
  vocab, reading, Listening quiz/translation: **Not run**. Backend từng ca đã live
  synthetic pass; không đổi nhãn này thành browser acceptance.
- Real YouTube network/download/captions acceptance, mic/accent/noise người thật,
  concurrency/load/accuracy và các payload dài để calibration cap/latency: **Not run**.
- Cold teammate: empty secrets + defaults, fixture secret preservation,
  locked archive → empty media root/repeat/offline verified. Full clean-machine
  prerequisite install → launcher → cloud+ASR end-to-end: **Not run**.
- Ba MariaDB application acceptance kể trên, staging recovery/remote CI và
  production: **Not run**. Không có schema change cần upgrade acceptance mới.
- Chưa commit/push/PR/merge/deploy; Plesk/gate và production credentials/config
  không đổi. Không có guarantee về mọi key/account hoặc chất lượng mọi bài học.

## Bàn giao local

Thu hồi test key đã đưa vào chat. Để dùng website với key của máy, nhập key mới
local bằng masked helper, rồi khởi động process mới:

```powershell
pwsh -NoProfile -File scripts/set-local-ai-key.ps1
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5102
```

5102 dùng cho checkout hiện tại để giữ app Plan1 ở5100. Checkout bình thường có
thể dùng default `-NoBrowser` (Debug5100) khi cổng/executable không bị app cũ giữ.
`AI__ApiKey` env ưu tiên User Secrets; không gửi key vào chat. Xem
[runbook](../operations/openai-local-setup.md) cho opt-in live nhỏ, media pins và
giới hạn. Key vừa lưu không tự cập nhật DI/options của process đã chạy.

## Owner follow-up — review và hoàn thiện launcher

Kết quả hiện hành: [review closeout](2026-11-01-openai-migration-review-closeout.md).
Owner đã cho phép dùng OPENAI_API_KEY; launcher nay map fallback theo thứ tự
AI__ApiKey → app User Secrets → OPENAI_API_KEY, chỉ trong child environment.
Chính sách này thay intentional no-mapping của phiên triển khai ở trên.
Busy port fail trước build; environment restore có regression success/failure.
Quy định launcher chung đã cập nhật cho cả4AIclients; không thêm auto-start hook.

Key env cũ bị401invalid_api_key. Owner cung cấp key thay thế; nhập bằng helper
masked, lưu User Secrets ngoài repo theo quyền local, không Git. Chỉ restart
review-owned Release instance; mới nhất ở5102, health200, nguồn key User Secrets,
Whisper ready. Smoke mới2requests thành công actual gpt-6-luna/low, tổng55tokens.
Full app808pass/0fail/5skip; scripts107/107, Node9/9,tooling/hooks/EFpass;
các check cuối và giới hạn xem closeout. Source/key
scan clean; không ghi credential trong ledger. Production/Git publish chưa đổi.
