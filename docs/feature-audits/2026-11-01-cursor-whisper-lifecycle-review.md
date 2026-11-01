# Review Cursor — Whisper lifecycle Plan 1

Ngày01/11/2026 UTC+7. Một agent review trực tiếp. Owner xác nhận yêu cầu ban đầu
ghi nhầm Plan 2; phần cần review là **Whisper Plan 1**. Checkout hiện tại:
`codex/whisper-local-lifecycle`, HEAD `03c9c5c`, implementation còn local/uncommitted.
Ledger Cursor: [Whisper lifecycle](2026-11-01-whisper-local-lifecycle-ledger.md).

**Kết luận: cần sửa trước khi nghiệm thu Plan 1.** Happy paths và live silence
hoạt động, nhưng có4 phát hiện cần xử lý và các gate E2E/performance chưa đủ.
Lượt này chỉ review/tái hiện, không sửa implementation, không commit/push/deploy.

## Phát hiện theo mức ưu tiên

### WH01 — P1: ownership chấp nhận process sai executable

Vị trí: `scripts/local-speech.ps1:361–370`, caller Stop ở812 trở đi.
Get-OwnedProcessIdentity luôn trả CommandLine=null; Test-OwnedSpeechProcess lấy
commandLine từ state. Khi đường dẫn thật không khớp, fallback lại chấp nhận nếu
chuỗi trong state chứa expected executable/runtime. runtimeExpected cũng đọc từ
state thay vì một expected runtime độc lập. Như vậy path mismatch đã được OS xác
nhận vẫn có thể bị chuỗi metadata ghi đè. Nếu Path đọc không được còn không có
nhánh từ chối. Start/status/stop dựa vào predicate này nên stop có thể chọn nhầm.

Tái hiện không phá hủy: gọi riêng hàm với PID/start time của process pwsh đang
chạy harness, expected executable=whisper-server, metadata command line chứa
đường dẫn whisper/cổng/model. Kết quả **accepted=True**, dù executable thật là
pwsh. Không gọi Stop/Kill với process đó. Log: `.tmp/whisper-plan1-review/reproduce.log`.

Sửa: expected path/runtime lấy từ cấu hình/argument đã xác minh, so với OS path;
path mismatch/missing phải từ chối. State không thay thế OS identity. Chốt cả
start time chính xác/handle và kiểm tra lại trước mọi kill, gồm failed-start
cleanup; không chỉ sửa happy-path Stop. Wrapper test cần contract riêng có guard.

### WH02 — P1: teardown test có thể kill PID không thuộc fixture

Vị trí: `scripts/tests/test_local_speech.py:105–119`, negative test331–362.
tearDown đọc PID từ state.json rồi gọi taskkill /PID /F /T trước khi dùng helper
Stop. Negative test cố ý thay state.pid=4; finally teardown vì thế yêu cầu kill
PID4/System. Lệnh có thể bị OS từ chối, nhưng ownership contract đã bị bypass;
các negative fixture khác cũng không được phép dùng PID đã hỏng để cleanup.

Tái hiện bằng mock subprocess.run, không thực thi taskkill: capture thực tế là
`['taskkill','/PID','4','/F','/T']`. Log `.tmp/whisper-plan1-review/teardown-proof.log`.
Đã chủ động không chạy case nguy hiểm/full discover trong lượt review.

Sửa cleanup dựa trên handles/identity của process fixture được ghi nhận độc lập
khi tạo; không dùng state đang test làm quyền kill. Dùng một victim child thuộc
test để chứng minh Stop từ chối nhầm process; không dùng System thật. Cleanup có
finally ngay cả khi assert fail và không nuốt lỗi che process mồ côi.

### WH03 — P2: readiness nhận HTTP200/error JSON là sẵn sàng

Vị trí: `scripts/local-speech.ps1:414–445,486`; reused start622, status795 và
benchmark859 cũng chỉ đọc probe.Ok. Probe.Ok chỉ là HTTP2xx, không kiểm schema.

Fixture HTTP loopback thật trả200 body `{"error":"model not loaded"}`. Gọi
nguyên hàm Invoke-InferenceProbe + Wait-SpeechReady từ source bằng AST; kết quả
`ProbeOk=true, ReadyAccepted=true`. Fixture không phải process Whisper của owner.
Log `.tmp/whisper-plan1-review/readiness-http.log`.

Sửa một validation chung: JSON object hợp lệ, không error, transcript/segments
đúng kiểu/contract của adapter; silence hợp lệ được chấp nhận, malformed/error
không được dùng để báo ready hay ghi số benchmark thành công. Không nới adapter
để phù hợp probe lỏng. Phân biệt HTTP, parse/schema và inference failure.

### WH04 — P2: CPU backend DLL không được khóa/check hash

Vị trí: `scripts/local-speech.ps1:253–256`, `tooling/whisper/artifacts.lock.json`.
Exe/model/một số DLL được check, nhưng CPU backends chỉ yêu cầu có ít nhất một
file ggml-cpu-*.dll. Setup tái sử dụng legacy folder cũng dựa vào check thiếu này.
Đây là backend của chế độ CPU được chọn, không phải file dữ liệu không chạy.

Tái hiện trên copy fixture: exe và các required DLL đúng lock, model trỏ tới
artifact thật chỉ đọc; `ggml-cpu-x64.dll` trong fixture chỉ có3 bytes. Hàm
Assert-InstalledArtifacts vẫn trả success. Không execute DLL/file hỏng và không
chạm artifact của owner. Log `.tmp/whisper-plan1-review/reproduce.log`.

Sửa pin/hash các CPU backend thực sự có thể được load, verify cả cache reuse và
pre-start; reject backend bị sửa/thừa ngoài tập đã review. Test missing/tampered
backend chứ không chỉ sai exe/model. Giữ version b5454 đã được chọn.

## Bằng chứng đã kiểm tra lại

| Kiểm tra do reviewer chạy | Kết quả |
| --- | --- |
| Tooling validator working tree | Pass |
| Team config + StartLocalContractTests |6 pass |
| Selected lifecycle Windows tests |5 pass: pin metadata, hash failure, start/status/stop, concurrent reuse, foreign listener |
| Node setup tests |9 pass |
| dotnet test Release có build, SpeakingAssessment/WhisperHttpTranscriber/InferenceLimiter |32 pass,0 fail,0 skip |
| WhisperLiveSilenceAcceptanceTests, engine owner127.0.0.1:8092 |1 pass,0 fail,0 skip |
| Ownership predicate repro | Sai executable vẫn accepted=True |
| HTTP readiness repro |200/error JSON vẫn ready=True |
| CPU backend repro | DLL3bytes vẫn vượt artifact gate |
| Teardown mock repro | Capture yêu cầu taskkill PID4; không thực thi |
| git diff --check | Pass; có cảnh báo chuyển LF/CRLF ở file hiện hữu |

Không chạy lại full app suite vì application C# không đổi trong diff này; kết
quả742/3skip trong ledger là bằng chứng Cursor, không phải số reviewer vừa chạy.
Không chạy full Python discover trước khi WH02 được sửa. Những test hiện có xanh
không phủ được negative cases vừa tái hiện. Logs reviewer dưới ignored
`.tmp/whisper-plan1-review/`. Process Whisper owner PID24532/start time và cache
thật được giữ nguyên; fixture review không dùng port8092 để inject lỗi.

## Nghiệm thu còn thiếu và độ chính xác báo cáo

- Ledger đã ghi trung thực Not run cho app speech→assessment→progress→credit,
  mic/permission và overflow; không được đổi thành pass từ silence probe.
- Cần fixture lời nói có provenance, reload/replay, đổi câu/hủy/timeout và kiểm
  credit không lặp trên tài khoản/DB local test. Không reset DB dev để chạy test.
- Benchmark hiện silence-only. `PeakWorkingSetMiB` ở script868 là WorkingSet tại
  cuối lượt, không phải peak; phải đổi nhãn thành current hoặc đo peak thực trước
  khi dùng nó kết luận về tải máy. Cần speech5/10/15s, số mẫu/p50/p95, CPU/RAM
  và có web+DB+IDE; đây là thiếu sót đo lường cần sửa trong bước acceptance.
- WMI Create không cấu hình stdout/stderr redirection; các log hiện chỉ có marker
  và dòng launcher, chưa phải stderr engine. Kiểm một startup lỗi trong bước sửa
  và cung cấp nguồn chẩn đoán thật nếu docs tiếp tục hướng dẫn đọc server logs.

Plan 2 chưa được thực thi trong checkout này: DI Program155 vẫn là Gemini client,
AiOptions vẫn default Google. Owner đã xác nhận đây là review Plan 1; không coi
việc chưa chuyển provider là lỗi implementation của Plan 1.

## Handoff

- [Plan sửa](../superpowers/plans/2026-11-01-whisper-lifecycle-remediation-plan.md).
- [Prompt Cursor Auto](../../.ai/prompts/cursor-auto-whisper-lifecycle-remediation.md).
- Review không cấp quyền sửa source hoặc publish trong chat này. Owner có thể
  gửi prompt sửa ở chat Cursor để giao scope local; sau đó Codex review lại.
