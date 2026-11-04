# Review Cursor — OpenAI Speech replacement, 03/11/2026

**Verdict: REQUEST CHANGES.** Đã chuyển được phần lớn runtime/workflow sang
OpenAI, nhưng chưa đủ đóng Plan hoặc bật live demo. Full suite xanh được xác minh
lại; các probe ngoài suite đã tái hiện những lỗi bên dưới.

Review một agent, local, không sửa application runtime/config/ledger Cursor,
không paid API/DB migration/commit/push/remote CI/merge/deploy. Giữ web cũ
PID7084 port5112 và dirty work. GitHub deploy variable đọc lại là false.

## 1. Đối chiếu ledger với việc đã làm

Đã có OpenAiSpeechTranscriber, credentials/options/DI, response-size cap,
ResponseHeadersRead, single-attempt HTTP, no redirect; per-user guard; PCM
window planner; import source openai-audio-vad. Launcher bỏ Whisper và FFmpeg
auto-setup; status metadata-only, doctor/native tasks/docs đã được chuyển.
Test factory chặn credentials mặc định để ordinary tests không đi cloud.
Text model vẫn Luna low; learner captions/SRT/VTT flow giữ nguyên.

Ledger nói rõ live/browser chưa chạy, startup không có baseline tương đương và
Release cũ còn khóa file; đây là các giới hạn được trình bày đúng. Tuy nhiên
STT4/STT8 chưa thể gọi hoàn tất vì lỗi import và các thiếu sót đã tái hiện.

## 2. Findings

### R1 — P1: padding sinh overlap khi tổng hai cửa sổ vượt10s

File: TCTEnglish/Services/Speaking/PcmSpeechWindowPlanner.cs, MergeWindows
lines183–194; importer validation cuối lines117–130.

ApplyPadding nới mỗi phía100ms. Khi hai raw windows có pause100ms, padding chồng
nhau. MergeWindows chỉ gộp nếu tổng length<=10s; trường hợp dài hơn giữ cả hai
cửa sổ chồng nhau, rồi SplitHardBound cũng không loại overlap.

Probe PCM21s: tone0–10s, pause10–10.1s, tone10.1–21s. Production planner trả:

    [0,10], [10,10.1], [10,20], [20,21]

Production importer với fake source/decoder/provider đã gọi provider **4 lần**
rồi trả invalid_timestamps/không publish. Với cloud thật có thể tốn phí rồi
không tạo được bài. Cần normalize windows thành sorted/disjoint trước hard
split, bảo toàn bytes speech; importer phải validate toàn bộ windows trước API
đầu tiên để không charge cho planner output sai.

### R2 — P2: threshold có thể lớn hơn peak và loại toàn bộ audio hữu ích

File: cùng planner, ResolveThreshold lines98–113.

Nếu percentile10/peak nằm khoảng0.286..0.45, nhánh flat-continuous không chạy,
nhưng noise*3.5 lại lớn hơn peak. Không frame nào đạt threshold.

Probe tone400Hz liên tục10s, các frame20ms có amplitude1400; mỗi frame thứ10
amplitude3500. Planner trả **0windows** dù audio liên tục có năng lượng.
Đây là probe tín hiệu synthetic về thuật toán, không tuyên bố đã kiểm giọng người
thật. Threshold phải hợp lý với range quan sát, không đạt fix bằng coi mọi noise
là speech. Bổ sung dynamic range/quiet/noise/continuous samples.

### R3 — P1: live import không dùng budget8POST/120s được quảng bá

Files:
TCTEnglish.Tests/AudioImportLiveAcceptanceTests.cs lines23–35;
TCTEnglish.Tests/OpenAiSpeechLiveAcceptanceTests.cs lines19–55.

BudgetGate/_posts/_audioSeconds là private trong class live-silence, được tăng
trong helper của chính class đó. Live import tạo DI factory/real transcriber
trực tiếp, không đi qua helper/HTTP budget handler. Một media dài có thể gọi đến
cap180 của importer, không bị chặn ở8POST hoặc120s của cả đợt test.

Kết luận từ call path/source; reviewer không bật live để tái hiện bằng tiền thật.
Phải có shared budget tại HTTP boundary cho mọi live audio client; fake tests
chứng minh request thứ9 và tổng seconds vượt120 không được gửi. Không cho owner
bật lệnh live hiện tại với lời hứa budget đã được bảo đảm.

### R4 — P2: key shared explicit-invalid bị bỏ qua sang key ưu tiên thấp

File: OpenAiSpeechCredentialResolver.cs lines64–81.

Speech-specific key có FinishExplicit/fail-closed, nhưng shared AI__ApiKey và
AI:ApiKey chỉ được chọn nếu usable; invalid/empty có thể rơi tiếp xuống
OPENAI_API_KEY. Điều này trái key contract của Plan và có thể dùng nhầm
credential/account của công cụ khác.

Probe child-process: AI__ApiKey='invalid shared credential', config AI:ApiKey
là fixture valid. TryResolve trả configured=true/source=User Secrets AI:ApiKey;
expected not-configured cho explicit override invalid. Không in giá trị key.
Cần phân biệt missing và explicit empty/invalid cho mọi nấc có authority.

### R5 — P2: response body bị reset thoát IOException, thành500

File: OpenAiSpeechTranscriber.cs lines96–104 và145–151.

Sau HTTP200 headers, ReadAsync có thể throw IOException khi connection reset.
Adapter bắt HttpRequestException/JsonException/OCE nhưng bỏ IOException.
Probe fake handler/stream: kết quả THREW:IOException thay vì unavailable503.
Lease vẫn nhả và request kế tiếp Ok; lỗi nằm ở mapping, không phải deadlock.
Catch đúng response I/O failure, giữ caller cancellation, không retry POST,
không ghi raw response/key vào log.

### R6 — P2: guard theo user không evict state đã hết hạn

File: SpeakingAssessmentAdmission.cs lines17 và29–30.

RemoveAll chỉ dọn timestamps của user đang truy cập. Dictionary singleton
không xóa user đã rời đi và không có size bound. Probe10.000 user, advance clock
hai giờ, thêm một user → **10.001 entries**, dù chỉ1 user hiện hành.
Memory tăng theo tổng user đã từng dùng và global lock còn bao mọi bucket.
Cần expiry/eviction và giới hạn số buckets thật; đây là guard process-local,
không cần thêm distributed quota hoặc schema.

### R7 — P2: speech-status không phản ánh effective config của app

File: scripts/speech-status.ps1 lines48–58.

Helper không xét Enabled/BaseUrl và bỏ qua env Model. Probe chỉ metadata:

    Learning__Speech__OpenAI__ApiKey=fixture
    Learning__Speech__Enabled=false
    Learning__Speech__OpenAI__Model=fixture-override-model

Output vẫn configured=true/model=gpt-transcribe, trong khi resolver app disabled
và model env có ưu tiên cao hơn User Secrets. Cần status phản ánh cùng config
precedence hoặc tách rõ KeyPresent/Enabled/EndpointValid/Configured.
Không biến status thành paid health request.

### P3 follow-up cùng repair

Admin Create view còn mô tả gọi Whisper:
TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Create.cshtml:98.
SpeechInferenceLimiter comment cũng còn tên engine cũ. Sửa active copy;
historical provenance/DB rows không rewrite. Credential container đang là record
chứa ApiKey nên ToString tự sinh cần được redacted trước khi dùng cho diagnostics.

## 3. Evidence độc lập của reviewer

Artifacts ignored: .tmp/stt-review/.

| Check | Kết quả |
| --- | --- |
| Fresh optimized build/full app tests, configuration SpeechReview | 870pass /0fail /12skip, tổng882, 4m11s |
| JS recorder/Speaking/import | 12/12 pass |
| Python script discovery | Ran92, OK skipped1 (91 executed pass) |
| git diff --check | sạch; CRLF warnings riêng |
| deploy variable read-only | false |
| R1/R2/R4/R5/R6 source-linked offline probes | tái hiện, probes.log |
| R7 metadata helper probe | tái hiện, không network |
| R3 live budget call path | source verified; không paid execution |

Source-linked harness gọi implementation hiện tại, fake decoder/source/HTTP;
một stub exception type chỉ để thỏa catch-filter dependency không được sử dụng
trong kịch bản. Không duplicate thuật toán planner trong probe.

Python unittest báo Ran92/skip1 không đồng nghĩa92pass+1skip; ledger nên sửa
cách ghi để tránh tổng93. Debug của Cursor và SpeechReview optimized của reviewer
khác configuration; không gọi đây là Release chuẩn hoặc CI.

Chưa chạy browser/mic từ source mới, paid live và baseline cold/warm. Web5112 cũ
không chứng minh source/ASR mới hoạt động. Release file lock không ngăn một
build/test config riêng; browser có thể dùng Debug ở free port theo launcher,
giữ process cũ. Nên khôi phục positive live-app receipt/replay coverage đã bị xóa,
qua fake OpenAI HTTP trước, rồi owner opt-in live dưới shared budget sau.

## 4. Thứ tự sửa và quyền

[Repair plan](../superpowers/plans/2026-11-03-openai-speech-review-repair-plan.md)
và [Cursor prompt](../../.ai/prompts/cursor-auto-openai-speech-review-repair.md).
Ưu tiên R1/R3, rồi R2/R4–R7 và browser/offline acceptance còn thiếu. Không bật
live trước khi budget thực sự được enforce. Review này chưa sửa runtime.
