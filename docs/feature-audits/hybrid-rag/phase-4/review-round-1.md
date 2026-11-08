# Phase 4 — Codex review vòng 1

Ngày 07/11/2026. Kết quả **CHANGES_REQUESTED**. Phase 5–7 tiếp tục **LOCKED**.
Review theo yêu cầu owner, một agent; chưa sửa application/test source của bản bàn giao.

## Danh tính và phạm vi

- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, không commit.
- Đã tính lại **109/109 hash** và fingerprint ordinal:
  `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`.
- Tiền nhiệm Phase 3 là 89 entry,
  `66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`.
  Không yêu cầu các file chung của manifest cũ vẫn giống sau Phase 4.
- Các sửa được review: pipeline/composer/selector mới, transport constrained-output,
  quota stages/lease/recovery, model/migration/metadata và test/caller/DI liên quan.
- Review ghi tài liệu và probe synthetic trong ignored `.tmp`; không sửa Program.cs,
  cấu hình, schema, test bàn giao hoặc nguồn học liệu. Manifest bàn giao giữ nguyên.

## Findings

### HR-P4-R1-F01 — P1: mất usage qua retry và response lỗi trên đường chat

**Nguồn**: `HybridRetrievalService.cs:132–148`,
`GroundedChatPipeline.cs:61–74, 105–122`, `OpenAiProviderClient.cs:154–195`.

Chat gọi overload embedding cũ, không truyền ledger theo attempt đã có từ Phase 3.
Result chỉ chứa usage của lần thành công sau cùng. Probe dùng client HTTP production:
lần đầu mất response sau send, retry thành công với usage 7; thực tế **2 POST** nhưng
journal chỉ giữ **known=7, unknown=0**, bỏ dự phòng 22 của lần đầu.

HTTP 200 có vector sai chiều và usage 7 thì query result báo **known=0/unknown=true**.
Generation HTTP 200 `status=incomplete`, usage input 100/output 5/total 105 thì journal
báo **KnownGeneration=0, UnknownGeneration=64**. Parse output lỗi trước khi receipt
được chuyển tới journal. Receipt hợp lệ không được coi là mất chỉ vì answer bị từ chối.

**Tác động**: thiếu chi phí đã biết và chưa biết trong ledger; retry có thể vượt
allowance. Sửa receipt/attempt callback hoặc ledger chat có giới hạn cho từng send,
giữ cumulative known và uncertainty riêng, kể cả error/cancel. Thay đổi transport
phải opt-in cho chat, giữ payload/hành vi non-chat.

### HR-P4-R1-F02 — P1: ngân sách không bao phủ payload generation cuối

**Nguồn**: `ChatTurnBudget.cs:36–52`, `GroundedChatPipeline.cs:85–102`,
`OpenAiChatSentenceSelector.cs:21–35`.

Admission dùng PlannedTotalTokens ước lượng của một context, nhưng selector tự xây
context khác với prefixes/history/evidence/schema. Không có kiểm trần trên payload
cuối. `GenerationUpper` chỉ bằng max output và cũng được dùng làm unknown charge.

Probe với một user-history gồm 1.000 ký tự CJK: planned **328**, request budget và
reservation **700**, selector vẫn gửi một POST chứa **3.228 byte UTF-8 nội dung input**,
sau response loss chỉ giữ **64 unknown generation**. Byte count là quan sát payload,
không phải phép đo 3.228 token; nó chứng minh phép tính admission chưa kiểm upper
bound của chính payload được gửi. Ca F01 cũng cho receipt tổng 105 lớn hơn bound 64.

**Sửa**: chuẩn bị context/schema cuối có giới hạn trước paid stage; tính cả input,
framing/schema, output và allowance retry trong cùng quyết định. Shrink/refuse trước
HTTP. Unknown bound phải bao phủ toàn stage/attempt có thể đã tiêu tốn.

### HR-P4-R1-F03 — P2: coi chunk là sentence và làm mất provenance

**Nguồn**: `GroundedChatPipeline.cs:85, 143–168, 181–182`,
`HybridRetrievalService.cs:310–319`, `GroundedAnswerComposer.cs:143–194, 224–241`.

Materializer trả nguyên `chunk.EmbedText`; ToSentence dùng ChunkId như SentenceId.
Probe với corpus SQLite thật có chunk `The bell is red.\nThe door is blue.` và
follow-up `is that true?` cho câu user trước `what color is the bell` trả
**InsufficientEvidence**, dù câu trả lời tồn tại. Grammar kiểm cả chunk như một câu.
Test Phase 4 hiện cấp fake evidence một câu nên không phát hiện mismatch này.

ToAnswer còn tạo lại hash từ text, đặt visibility `public`, bỏ document hash/route/
version thay vì giữ provenance của nguồn authenticated. Đây là contract transient
ngay trong Phase 4; sửa không cần triển khai citation bền của Phase 5.

Probe unit bổ sung: `Completed` với `{"sentenceIds":[]}` vẫn trả **Answered** từ
catalog một câu. Đây là lỗi validation của composer; probe này không tuyên bố đã
chạy nhánh selector production với catalog một câu (pipeline thường bỏ qua selector).

**Sửa**: materialize các sentence nguyên văn, có ID/hash/route/visibility/version đúng;
kiểm support từng facet trên sentence, chỉ copy selection hợp lệ. Empty/unknown/
unrelated selection phải fail closed; không fallback âm thầm khi Completed rỗng.

### HR-P4-R1-F04 — P1: nguồn bị thu hồi trong lúc chọn câu vẫn thành câu trả lời

**Nguồn**: `GroundedChatPipeline.cs:95–140`, sau recheck cuối của
`HybridRetrievalService.MaterializeAsync`.

Probe dùng corpus/hybrid production và selector có barrier. Sau retrieval hai nguồn
hợp lệ, selector đang chờ thì gỡ publish cả hai trong SQLite. Sau nhả barrier,
pipeline vẫn trả **Answered / The bell is red.** Không còn source/hash recheck trước
output hoặc trước khi AiChatService lưu assistant.

**Tác động**: nguồn không còn được phép đọc có thể được trả và lưu như thành công.
Giữ document identity từ F03, recheck ngay trước external selection và sau await,
trước accept/commit. Nguồn đổi hash, chuyển private, unpublish hoặc snapshot thay
phải cho kết quả an toàn, giữ usage nhưng không tạo success artifact từ nguồn cũ.

### HR-P4-R1-F05 — P1: lease đã mất nhưng stage mới vẫn gửi provider

**Nguồn**: `AiDailyQuotaReservationService.cs:338–364`,
`AiTurnStageJournal.cs:44–56`, `GroundedChatPipeline.cs:54–60, 90–102`.

MutatePendingAsync coi row đã terminal/missing là no-op thành công. Caller không
biết mình đã mất quyền sở hữu lượt. Probe dùng clock điều khiển, barrier và hai
DbContext: lượt A dừng ở retrieval; tăng clock quá lease, lượt B reserve và recovery
settle A ở bound **22**. Nhả A: nó vẫn gọi selector **1 lần**, trả **Answered**;
ledger đã Settled 22, **KnownGenerationTokens=null**. Settle sau đó không ghi usage
generation vì row đã terminal.

**Sửa**: stage acquisition/renewal phải trả kết quả sở hữu được xác nhận hoặc ném
lỗi ownership; mất lease/row đã terminal thì dừng trước paid stage và không nhận
assistant success. Fencing/renewal/deadline cần bền qua scope/process, không chỉ
tăng thời gian lease hoặc đổi no-op thành success giả. Lặp lại probe trên MariaDB.

## Kiểm chứng độc lập

| Kiểm tra | Kết quả của lượt review |
| --- | --- |
| Build app/tests | Thành công trong output `Phase4Review`, warnings có sẵn |
| Focused RAG/provider/cancel/quota/consumer | 168 pass / 1 fail / 0 skip trong lần đầu, 169 tổng |
| Test fail ở lần đầu | `VectorRebuild_ReusesCache_RejectsBudgetDriftAndPromotionFailure`, line 264 expected promoted/actual failed; chạy riêng lại **1/1 pass**; chưa xác định nguyên nhân không ổn định |
| Full suite chạy sau đó | **1.078 pass / 20 skip / 0 fail**, 1.098 tổng, khoảng 2 phút; không tính skip là pass |
| Probe hiện tượng | 8 quan sát/điều kiện kỳ vọng chưa đạt, map F01–F05 ở trên; synthetic HTTP, SQLite, không paid API |
| Pending model | **No changes**, exit 0; design-time MariaDB 10.5.23 metadata, connection loopback synthetic chỉ trong child environment, không kết nối DB hoặc đổi secret store |
| Migration source | Up thêm 7 cột nullable; Designer/snapshot cùng scope; không sửa migration cũ, không có DROP trong Up |
| MariaDB upgrade/concurrency | **Chưa chạy lại được**: Docker Linux engine pipe không tồn tại. Bằng chứng trong handoff là của implementer; review không nhận nó như lần chạy độc lập |
| Tooling / whitespace | Tooling exit 0; `git diff --check` sạch |

Full suite xanh không phủ các nhánh mà probe vừa tái hiện. Các test live API,
speech, MariaDB/upgrade/benchmark opt-in vẫn có skip; không gọi paid API để review.
Không chạy lại browser vì không có UI mới trong Phase 4 và finding nằm ở service/
HTTP fake/persistence; không tuyên bố browser acceptance mới.

Lệnh chính:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Review --no-restore --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~AiCancelAndStop|FullyQualifiedName~AiQuotaReservation|FullyQualifiedName~AiCloseout|FullyQualifiedName~OpenAiConsumerPipeline'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Review --no-build --no-restore
```

TRX, console và probe source/result nằm ở ignored
`.tmp/hybrid-rag-review/phase-4/round-1/`. Probe entrypoint là `Probe/Program.cs`,
result `behavior-probes.json`, kỳ vọng `probe-expectations.json`. Lỗi setup ban đầu
của probe đã được sửa trong scratch trước khi ghi bằng chứng: seed authenticated
user và dùng hai DbContext trực tiếp; không sửa application để làm probe chạy.

Constrained payload `text.format/json_schema/strict` được đối chiếu với
[OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
JSON schema chỉ giới hạn cấu trúc; support và nguồn hiện tại vẫn cần server kiểm.

## Gate và bước tiếp

**CHANGES_REQUESTED**, F01–F05 mở. Phase 5–7 LOCKED. Không tạo approval fingerprint.
109-entry fingerprint ở đầu vẫn là danh tính bản được review, không phải duyệt.

- [Plan sửa vòng 1](../../../superpowers/plans/hybrid-rag/phase-4-fix-round-1.md).
- [Prompt thực thi vòng 1](../../../../.ai/prompts/hybrid-rag/phase-4-fix-round-1.md).
- [Review state](../../../superpowers/plans/hybrid-rag/review-state.md).

Chỉ review và chuẩn bị sửa; chưa sửa application, chưa commit/push/CI/deploy hoặc
gọi API trả phí. Khi owner khởi chạy repair, phải tạo regression cho các ca thật,
sửa trong Phase 4 và quay lại AWAITING_REVIEW. MariaDB còn là điều kiện bắt buộc
trước approval, kể cả khi toàn bộ SQLite/fake tests đã xanh.
