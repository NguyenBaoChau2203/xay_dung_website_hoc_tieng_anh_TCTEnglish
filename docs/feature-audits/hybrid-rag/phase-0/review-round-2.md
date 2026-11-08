# Review Phase 0 — vòng 2, 06/11/2026

**Kết quả: CHANGES_REQUESTED. Còn một finding P2; Phase 1–7 tiếp tục LOCKED.**

Review trực tiếp bản sửa round 1, không tự sửa validator/test/fixture. Nhánh
`feature/hybrid-rag-chatbot`, HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`.
Toàn bộ 11 digest entry khớp snapshot bàn giao; fingerprint tính lại là
`b4205acda7cc68a5e0ec94a1c68410b6d7ee3181381bdebfec09f69d8ea65ce2`.
Không có application runtime, schema hoặc protected config thay đổi.

## Kết quả đối với các finding vòng 1

| Finding | Kết quả vòng 2 | Bằng chứng |
| --- | --- | --- |
| HR-P0-R1-F01 | Đã xử lý | Nguồn Quiz/Matching riêng đúng facet; Grammar có route; Premium thiếu dữ kiện được ghi insufficient; remaining cards có phép trừ 10−4=6; partial có phần chưa trả lời. 120 ID, split 80/40 giữ nguyên. |
| HR-P0-R1-F02 | Đã sửa một phần, chưa đóng | Có typed scenario và 8 negative clone tests, nhưng thiếu cross-field consistency. Xem HR-P0-R2-F01. |
| HR-P0-R1-F03 | Đã xử lý | Canonical UTF-8/LF, LF/CRLF equivalence, 4 mutation regression và LF pin đúng fixture; không chỉ thay constant raw. |
| HR-P0-R1-F04 | Đã xử lý | Tài liệu tách Pending→Settled và Pending→Released, giữ phân tích token race mà bỏ kết luận trả lại lượt Standard khi settle. |
| HR-P0-R1-F05 | Đã xử lý | Sửa đúng sự tồn tại `.codegraph`, tách khả năng MCP và ghi rõ phương thức CLI/fallback, không coi là bằng chứng browser. |

Các kết luận này chỉ áp dụng artifact Phase 0. Chưa nghiệm thu RAG runtime/model.

## HR-P0-R2-F01 — P2: validator chấp nhận setup mâu thuẫn với ca kiểm thử

Đây là phần kiểm tra tính nhất quán còn thiếu của R1-F02, không phải yêu cầu thêm
feature hay mở rộng phạm vi sang state machine của application.

**Source và nguyên nhân:** `TCTEnglish.Tests/HybridRagFixtureValidator.cs:262`
gọi ValidateScenario nhưng không truyền outcome và declared sourceIds của case.
Nhánh `none` tại `:312–314` được chấp nhận độc lập với outcome. ValidateQuota tại
`:375–390` chỉ kiểm actor, ngày và số dương/không âm; không kiểm exhaustion cho
case kỳ vọng quota_exceeded. ValidateTombstone tại `:400–410` kiểm source tồn tại
toàn corpus nhưng không kiểm nó có phải nguồn được khai báo cho case đó.

**Tái hiện:** sau build, dùng PowerShell load test assembly, clone JSON trong bộ
nhớ và gọi chính `HybridRagFixtureValidator.Validate`. Không sửa source hoặc file
fixture. Kết quả lưu ignored trong
`.tmp/hybrid-rag-review/phase-0/round-2/validator-probes.json`:

| Probe | Thay đổi duy nhất trên clone | Kết quả thực tế | Kỳ vọng |
| --- | --- | --- | --- |
| Original | Không đổi | errors=[] | Hợp lệ |
| quota-not-exhausted | HR-OPS-D05 dùng 0/15 câu, 0/60000 token; vẫn quota_exceeded | errors=[] | Báo setup không chứng minh hết quota |
| provider-fault-replaced-with-none | HR-OPS-H03 scenario thành `{type:none}`; vẫn provider_unavailable | errors=[] | Báo thiếu fault setup |
| cancel-replaced-with-none | HR-OPS-D01 scenario thành `{type:none}`; vẫn cancelled | errors=[] | Báo thiếu cancel action |
| tombstone-wrong-declared-source | HR-OPS-D04 chuyển tombstone sang invoice với hash hợp lệ, trong khi sourceIds của case vẫn là zephyr-window | errors=[] | Báo scenario source không thuộc case |

**Ảnh hưởng:** thay đổi world state không còn tái hiện được điều kiện của case
vẫn qua validator độc lập. Hash freeze chặn thay bytes ngoài ý muốn, nhưng không
thay kiểm tra logic khi tạo revision hoặc test clone. Bộ 8 negative tests hiện
tại chưa bao phủ các mâu thuẫn cụ thể trên.

**Cách sửa hẹp:** kiểm tra tính nhất quán giữa setup đã cho với outcome và các nguồn
được khai báo; không dùng outcome/question/ID để tạo hoặc tự sửa setup. Thêm đúng
các regression tái hiện trên, giữ fixture revision 2 hợp lệ và canonical hash cũ.
Không cần audit lại toàn bộ oracle hoặc chỉnh application quota/provider.

## Kiểm tra độc lập

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0Review2 --filter 'FullyQualifiedName~HybridRagEvaluationBaselineTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests' --logger 'console;verbosity=normal' --results-directory .tmp/hybrid-rag-review/phase-0/round-2 --logger 'trx;LogFileName=phase0-review2.trx'
```

**144 passed, 0 failed**, 7,7313 giây test sau build. TRX ở thư mục ignored trên.
Các probe mâu thuẫn gọi validator thật trả errors=[]; test xanh không loại bỏ
finding đó. Manifest khớp cả 11 file và fingerprint. Fixture có 120 ca, 80/40,
51 nguồn; canonical hash `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.

Tooling validator đạt. Python suite chạy lại **135 test: 134 pass, 1 skip**,
48,288 giây; skip là nhánh non-Windows. Các bài test staging chỉ dùng fixture,
không phát hành hosting. Link/UTF-8/whitespace của artifact review và gate đã kiểm.
Không chạy browser, provider trả phí, CI hoặc deployment.

## Gate và bàn giao sửa

Giữ Phase 0 CHANGES_REQUESTED, Phase 1–7 LOCKED. Các finding R1-F01/F03/F04/F05
đã được kiểm chứng xử lý; R1-F02 còn phần mô tả trong HR-P0-R2-F01.

- [Plan fix vòng 2](../../../superpowers/plans/hybrid-rag/phase-0-fix-round-2.md).
- [Prompt Cursor Auto vòng 2](../../../../.ai/prompts/hybrid-rag/phase-0-fix-round-2.md).

Cursor sửa xong phải nộp lại để review vòng 3. Không có approval chỉ từ việc tạo
plan; reviewer không thay validator/test/fixture trong lượt này.
