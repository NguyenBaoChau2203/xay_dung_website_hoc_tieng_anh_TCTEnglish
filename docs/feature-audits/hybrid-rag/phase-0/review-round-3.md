# Review Phase 0 — vòng 3, 06/11/2026

**Kết quả: APPROVED. Phase 0 hoàn tất; Phase 1 READY, chưa bắt đầu.**

Không còn finding cần xử lý trong phạm vi Phase 0. Không tạo plan fix trống.
Một agent review source và kiểm chứng trực tiếp; không tự sửa application, test
hoặc fixture của Cursor trong lượt review này.

## Snapshot được duyệt

- Nhánh: `feature/hybrid-rag-chatbot`.
- HEAD: `6af768bd32d16cc6d012d484ee82df4cab805c69`; chưa commit các thay đổi local.
- Bàn giao: [fix-round-2-handoff.md](fix-round-2-handoff.md).
- Manifest: [scope-manifest.json](scope-manifest.json), đủ 13 digest entry, tất cả
  hash khớp bytes hiện tại. Tính lại theo thứ tự path, TAB và LF thật khớp:
  `1c33f6700cde76fc80558f823e2fb5699cbeae8c6fba89aeaab29bd25a2268cb`.
- Fixture revision 2 giữ nguyên: 120 ca, development 80/held-out 40, 51 nguồn.
  Canonical UTF-8/LF SHA-256:
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Review/state là artifact kết quả, không nằm trong digest để tránh hash đệ quy.
  Phase 1 phải kiểm snapshot này trước khi thực hiện thay đổi tiếp theo.

## Đóng các finding

| Finding | Kết quả kiểm chứng |
| --- | --- |
| HR-P0-R1-F01 | Đã xử lý ở vòng 2; oracle Quiz/Matching, phép trừ 6 và partial facets được giữ; fixture không bị thay để né test vòng 3. |
| HR-P0-R1-F02 | Đã đóng sau khi kiểm chứng phần còn lại HR-P0-R2-F01; scenario có setup riêng và validator đối chiếu tính nhất quán. |
| HR-P0-R1-F03 | Đã xử lý; canonical hash, LF/CRLF, mutation tests và LF pin còn nguyên. |
| HR-P0-R1-F04 | Đã xử lý; hợp đồng counter Settled/Released không thay đổi sai trở lại. |
| HR-P0-R1-F05 | Đã xử lý; provenance CodeGraph và phương thức discovery được giữ đúng như bản đã review. |
| HR-P0-R2-F01 | Đã xử lý; cả bốn probe vòng 2 bị từ chối đúng lý do, fixture hợp lệ vẫn được chấp nhận. |

Source hiện tại được đọc bằng CodeGraph. `ValidateScenario` nhận outcome và nguồn
được khai báo từ case; `CompareSuppliedSetupWithOutcome` so setup có sẵn với điều
kiện kết quả, `RequireScenarioSourceInDeclaredScope` đối chiếu target source.
Không thấy special-case ID/câu hỏi để dựng môi trường hoặc thay đổi setup từ nhãn.

## Probe độc lập trên validator đã build

Dùng PowerShell load test assembly từ cấu hình review riêng và gọi public
`HybridRagFixtureValidator.Validate` với từng clone trong bộ nhớ. Không sửa file
fixture hoặc test source. Kết quả ở ignored
`.tmp/hybrid-rag-review/phase-0/round-3/validator-probes.json`.

| Probe | Kết quả thực tế |
| --- | --- |
| Original revision 2 | Hợp lệ, errors=[] |
| Quota 0/15 và 0/60000, vẫn kỳ vọng quota_exceeded | `HR-OPS-D05 quota setup does not reach a limit` |
| Provider outcome nhưng scenario none | `HR-OPS-H03 missing provider fault` |
| Cancel outcome nhưng scenario none | `HR-OPS-D01 missing cancel action` |
| Tombstone invoice trong case khai báo zephyr-window | `HR-OPS-D04 scenario source is outside the declared scope` |
| Quota chưa hết câu nhưng đã chạm trần token | Hợp lệ; logic chấp nhận ít nhất một giới hạn đã chạm |
| Provider fault với outcome answered và fact nguồn hợp lệ | Hợp lệ; validator không ép mọi provider fault thành lỗi nếu có fallback được mô hình hóa |

Tất cả 7 probe cho kết quả như kỳ vọng. Hai probe hợp lệ bổ sung kiểm tra việc sửa
không biến thành từ chối toàn bộ trường hợp lỗi/fallback hoặc chỉ xét trần câu.
Đây là kiểm tra validator synthetic, không chứng minh fallback runtime đã có.

## Kiểm tra độc lập

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0Review3 --filter 'FullyQualifiedName~HybridRagEvaluationBaselineTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests' --logger 'console;verbosity=normal' --results-directory .tmp/hybrid-rag-review/phase-0/round-3 --logger 'trx;LogFileName=phase0-review3.trx'
```

- .NET **148 passed, 0 failed**, 6,4392 giây test sau build; gồm 19 kiểm tra baseline/
  validator và 129 kiểm tra retriever/classifier/composer/internal provider liên quan.
  TRX tại `.tmp/hybrid-rag-review/phase-0/round-3/phase0-review3.trx`.
- `uv run --no-project python scripts/agent_tooling.py`: đạt.
- `uv run --no-project python -m unittest discover -s scripts/tests -v`:
  **135 test: 134 pass, 1 skip**, 49,298 giây; skip là nhánh non-Windows.
  Test staging dùng fixture, không phát hành lên hosting.
- Kiểm bytes/hash manifest và canonical fixture đạt. `git diff --check` sạch;
  artifact kết quả được kiểm UTF-8, whitespace và link local trước bàn giao.

## Phạm vi approval và bước tiếp theo

Các deliverable Phase 0 — coverage/access matrix, hợp đồng đề xuất, fixture đóng
băng, validator và baseline offline — đủ điều kiện dùng làm đầu vào Phase 1.
Các khoảng trống runtime như truy xuất 500 thẻ, quyền Speaking, ngày nghiệp vụ,
quota nhiều bước hoặc Guide 9 vẫn là việc các phase tương ứng phải xử lý; Phase 0
là discovery nên không tuyên bố đã sửa chúng.

Không có browser/live text/embedding/speech, migration, CI hoặc deployment trong
lượt này. Approval này không phải nghiệm thu chất lượng Hybrid RAG hoặc sản xuất.

Phase 1 được READY theo [review-state](../../../superpowers/plans/hybrid-rag/review-state.md),
nhưng chỉ bắt đầu khi owner khởi chạy [prompt Phase 1](../../../../.ai/prompts/hybrid-rag/phase-1-chat-boundary-access.md).
Quyền sửa hẹp Program.cs để đăng ký DI vẫn phải có chỉ dẫn rõ của owner; approval
Phase 0 không tự tạo quyền protected-file, paid API hay Git/release mới.
Phase 2–7 tiếp tục LOCKED và Cursor phải dừng bàn giao sau Phase 1 để review.
