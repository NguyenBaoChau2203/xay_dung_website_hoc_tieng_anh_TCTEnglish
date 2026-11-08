# Phase 0 — kết quả baseline

Ngày chạy đầu: 2026-11-06. Máy local Windows, checkout `feature/hybrid-rag-chatbot`.
Cấu hình build tách để không ghi đè output Debug/Release đang có thể bị process khác giữ.

Đây là kiểm tra fixture và quan sát bộ phân loại/retriever hiện tại. Không phải điểm chất lượng Hybrid RAG, không phải lượt gọi OpenAI, không phải CI và không phải production.

## Revision 1 — số lịch sử

Các số dưới đây đo trên fixture revision 1, hash raw CRLF `0abf1e31fc781c0ba7b87d5916cda23e69564033b19efdebfdfd9e7ef2bb4116`. Chúng không phải quan sát của revision 2.

| Mục | Kết quả lúc đó |
| --- | --- |
| Schema | `hybrid-rag-eval-1`, đóng băng `2026-11-06` |
| SHA-256 | Raw byte CRLF, không phải fingerprint canonical |
| Ca | 120. Development 80, held-out 40 |
| Nguồn synthetic | 49. Actor synthetic 3 |
| Cấu hình | `Phase0Eval` |
| Test gộp fixture và retriever | Passed 93 |
| Composer và regression | Passed 38 |
| Quan sát | `baseline-observation cases=120 guideHits=58 intents=CardLookup:10,ClassInfo:2,MyProgress:6,OutOfScope:78,WebsiteGuide:24` |

## Revision 2 — đo lại sau khi sửa oracle

Hash canonical là SHA-256 của byte UTF-8 sau khi đổi CR và CRLF thành LF. Hash raw của scope manifest là SHA-256 của byte file, không qua bước chuẩn hóa đó.

| Mục | Kết quả |
| --- | --- |
| File | `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json` |
| Schema | `hybrid-rag-eval-1`, revision 2, đóng băng gốc `2026-11-06` |
| SHA-256 canonical | `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f` |
| SHA-256 raw revision 1 | `0abf1e31fc781c0ba7b87d5916cda23e69564033b19efdebfdfd9e7ef2bb4116`, chỉ để truy vết |
| Ca | 120. Development 80, held-out 40. ID và tỷ lệ từng nhóm giữ nguyên |
| Nguồn synthetic | 51, gồm thêm `src-guide-quiz` và `src-guide-matching`. Actor synthetic 3, email chỉ `@example.test` |
| Guide thật | `TCTEnglish/wwwroot/data/ai/website-guides.json`, 41 mục, SHA-256 `ccf4545f3304f3ce6375b5f3bf97c16eaef53f178f99fc8dbecc13d34b0948d6` |

Ca held-out nằm trong file đã đóng băng. Lượt quan sát không sửa điểm ngưỡng, không sửa fixture và không dùng held-out để chỉnh hành vi.

## Lệnh và kết quả revision 2

```text
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixReview --filter "FullyQualifiedName~HybridRagEvaluationBaselineTests" --logger "console;verbosity=detailed"
```

Exit code 0. Total tests: 15. Passed: 15. Failed: 0. Skipped: 0. Total time khoảng 6.3 giây sau khi build xong. 15 test gồm khóa canonical, LF/CRLF, bốn đột biến fact/ID/outcome/split, và tám clone scenario hỏng. Build có các cảnh báo nullable có sẵn của project; không có lỗi mới từ file sửa.

Lệnh thứ hai, cùng cấu hình, `--no-build`:

```text
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixReview --no-build --filter "FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests"
```

Exit code 0. Passed: 129. Failed: 0. Skipped: 0. Duration khoảng 3 giây. Các lớp này dùng retriever, composer và regression nội bộ, không gọi OpenAI.

Dòng quan sát đo lại trên revision 2:

```text
baseline-observation cases=120 guideHits=58 intents=CardLookup:10,ClassInfo:2,MyProgress:6,OutOfScope:78,WebsiteGuide:24
```

Câu hỏi không đổi nên các số trùng lượt revision 1. Đây vẫn là một lượt đo mới. Cách đọc:

- `DeterministicIntentClassifier` chạy trên 120 câu synthetic. Tổng intent bằng 120.
- `WebsiteGuideRetriever` trả 0 hoặc 1 snippet. 58 câu có một guide. Retriever này không đọc DB và không gọi mạng.
- Các số trên không được so với `expectedOutcome`. Không có vector, không có điểm semantic, không có câu trả lời model.

`OutOfScope:78` là phân loại từ khóa hiện tại trên bộ câu synthetic, gồm cả câu cá nhân, bài học và câu từ chối. Nó không có nghĩa 78 câu nằm ngoài sản phẩm.

## Việc không chạy

| Kiểm tra | Trạng thái | Lý do |
| --- | --- | --- |
| Browser chat, guide, Premium, revocation | not-run | Route và claim đối chiếu từ source. Không mở launcher, không đụng process/DB/key hiện có |
| OpenAI text, embedding, speech | not-run | Phase 0 không cấp quyền gọi API trả phí |
| Test host web `AiDeterministicBaselineIntegrationTests` | not-run | Factory đó đổi bind provider. Không cần để chứng minh fixture. Production vẫn là `OpenAiProviderClient` theo `Program.cs` |
| CI GitHub | not-run | Không có quyền remote CI |
| Production / staging | not-run | Không deploy |

## Round 2 — kiểm tra nhất quán scenario

Ngày chạy: 2026-11-06. Cấu hình `Phase0FixRound2`. Fixture revision 2 và hash canonical `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f` không đổi. Không có quan sát classifier mới trên một bộ câu khác.

Trước khi thêm đối chiếu outcome/source, bốn clone trong bộ nhớ trả `errors=[]` và 4 test fail. Sau khi sửa validator, cùng bốn clone báo đúng lỗi và toàn bộ lớp baseline pass.

```text
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixRound2 --filter "FullyQualifiedName~HybridRagEvaluationBaselineTests" --logger "console;verbosity=detailed"
```

Exit code 0. Total tests: 19. Passed: 19. Failed: 0. Trong đó có LF/CRLF, bốn đột biến fact/ID/outcome/split, tám clone schema cũ và bốn clone nhất quán mới. Dòng quan sát in lại vì câu hỏi không đổi: `guideHits=58`; intent `CardLookup:10,ClassInfo:2,MyProgress:6,OutOfScope:78,WebsiteGuide:24`.

Lệnh `--no-build` retriever, classifier, provider, composer và regression: Passed 129, Failed 0.

## Kết luận được phép rút

Fixture revision 2 hợp lệ theo hash canonical và validator scenario. Bộ phân loại từ khóa cùng retriever guide hiện tại chạy được offline trên 120 câu. Đường chat production vẫn là `OpenAiProviderClient` không kèm evidence. Chưa có chỉ số recall, faithfulness hay chi phí. Pass test không thay cho đối chiếu oracle trong [oracle-validation.md](oracle-validation.md).
