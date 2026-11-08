# Review Phase 1 — vòng 1, 06/11/2026

**Kết quả: CHANGES_REQUESTED. Có 4 finding tái hiện được; Phase 2–7 tiếp tục LOCKED.**

Review trực tiếp source/call paths, diff và các file untracked. Không tự sửa code,
test hoặc dữ liệu của Cursor. Một agent thực hiện; không có API trả phí/hosting.

## Snapshot và phần đã xác minh

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`.
- Cả 32 digest entry của manifest Phase 1 khớp bytes; tính lại với TAB/LF thật khớp
  `781f92c523d57554a0b96acfd9c704986e8c7ac4e74cf47c9fcd39a2d9ef483f`.
- Program.cs chỉ thêm scoped IAiChatAnswerService → AiChatAnswerService; global
  IAiProviderClient không đổi. Đây là phạm vi DI đã được owner cấp khi chạy Phase 1.
- Đường chat HTTP/stream đi qua AiChatService và boundary nội bộ, không có LLM
  fallback tự do. Test không-chat vẫn dùng transport riêng như trước.
- Các sửa query Speaking public/owner/Premium, ngày nghiệp vụ, đếm đơn pending và
  regression liên quan đều có bằng chứng source/test. Không có migration vì entity/
  DbContext không đổi. Không đánh đồng baseline này với Hybrid RAG hoàn chỉnh.

## HR-P1-R1-F01 — P1: câu ngoài phạm vi được nâng thành Answered/full chỉ vì trùng thẻ

**Source:** `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs:46–50,106–115`.

Khi classifier trả OutOfScope, service vẫn thêm CardLookup và WebsiteGuide vào các
intent chạy retrieval. Bất kỳ CardLookupResult nào sau đó được ưu tiên làm câu trả
lời, không kiểm tra fact có trả lời đúng yêu cầu ngoài phạm vi hay không.

**Tái hiện:** actor Standard sở hữu một thẻ term `vang`, definition synthetic
`SCOPED_CARD_DEFINITION`. Hỏi “Giá vàng hôm nay là bao nhiêu?”. Classifier trả
OutOfScope, nhưng boundary trả Answered, scope full, 1 evidence và định nghĩa thẻ.
Đây là câu trả lời lạc đề được đóng dấu trả lời đầy đủ, không phải giá vàng thật.

**Ảnh hưởng:** vi phạm ranh giới “chỉ hỗ trợ nền tảng/học liệu” và việc từ chối yêu
cầu không có bằng chứng. Matching từ khóa không đủ để chứng minh đã đáp ứng ý hỏi.

**Cần sửa:** tách câu rõ ràng ngoài phạm vi khỏi câu chưa nhận diện intent nhưng có
thể tra được học liệu. Giữ fallback cho term hợp lệ như zephyr-window; không sửa
bằng cách chặn tất cả low-confidence/OutOfScope. Thêm control hỏi nghĩa từ trong
thẻ để phân biệt với hỏi giá thị trường và kiểm tra cả outcome/text/evidence.

## HR-P1-R1-F02 — P1: Take(100) trước xếp hạng vẫn loại thẻ khớp chính xác

**Source:** `Services/AI/Internal/Retrievers/CardLookupRetriever.cs:47–71`.

Query lọc broad token rồi OrderBy Term/CardId và Take(100), sau đó mới tính Score.
Thẻ chính xác có priority 100 không còn cơ hội được xét khi bị loại trước đó.

**Tái hiện:** 120 thẻ authorized term aaa-001…aaa-120 có definition nhắc
zephyr-window; thẻ thứ 121 có term chính xác zephyr-window. Truy vấn zephyr-window
trả ba thẻ aaa-001/002/003, priority 40; thẻ chính xác biến mất.

**Ảnh hưởng:** lỗi cửa sổ 500 đã đổi thành cửa sổ 100 trên tập match lỏng; người dùng
có thể nhận các định nghĩa khác dù thẻ cần tìm tồn tại và còn quyền. Handoff có nêu
giới hạn này, nhưng chưa đáp ứng yêu cầu chọn match phù hợp trước khi giới hạn.

**Cần sửa:** ưu tiên match chính xác/phrase phù hợp trước cap bằng query có thể dịch
sang SQL hoặc các tầng truy vấn có giới hạn. Giữ bounded I/O; không thay bằng tải
toàn bộ corpus. Regression phải dùng hơn 100 broad matches, không chỉ 500 filler
hoàn toàn không liên quan như test hiện tại.

## HR-P1-R1-F03 — P2: không thể tra các từ tiếng Anh hai ký tự

**Source:** `CardLookupRetriever.cs:23–30`.

Parser vẫn cung cấp term hợp lệ hai ký tự, nhưng bộ token mới loại mọi term có
Length<3 rồi trả rỗng. Bộ lookup trước thay đổi không có điều kiện này.

**Tái hiện:** user sở hữu thẻ go, up và forecast. Tra go/up đều trả 0 snippet; tra
forecast cùng context trả đúng 1 snippet, chứng minh probe setup hoạt động.

**Ảnh hưởng:** từ cơ bản có trong học liệu bị báo thiếu. Đây là regression của
candidate filter, không phải dữ liệu nguồn thiếu hay classifier kém tự tin.

**Cần sửa:** hỗ trợ exact lookup cho term ngắn hợp lệ, giữ stopword và giới hạn broad
search. Không chỉ hạ độ dài tối thiểu cho mọi token khiến query LIKE mở rộng vô hạn.

## HR-P1-R1-F04 — P2: dùng title làm identity làm mất phân biệt hai thẻ cùng term

**Source:** `AiChatAnswerService.cs:153–161,237–250`.

NeedsClarification chỉ được xét khi hai title khác nhau; SourceKey cũng được dựng
từ Source + Title thay vì identity thẻ. Hai thẻ cùng term nhưng khác set/meaning
không được phân biệt đúng.

**Tái hiện:** actor có match trong hai set, nghĩa “a sporting contest” và “to pair
two items”. Hỏi bare term match. Kết quả Answered/full, không có câu hỏi chọn nguồn;
cả hai evidence cùng key `card-lookup-result:match`.

**Ảnh hưởng:** trái với hợp đồng ca ambiguity đã duyệt của Phase 0; identity nguồn
không duy nhất, không ổn định theo entity, làm đầu vào provenance cho phase sau sai.
Chưa có citation bền hoặc cache dùng key này ở runtime; không kết luận đã xảy ra
cross-user leak từ collision.

**Cần sửa:** giữ identity thực của thẻ/set trong kết quả retrieval và dùng nó làm
source key. Với một term chưa rõ nguồn, nhiều nghĩa hợp lệ phải trả outcome và
text làm rõ tương ứng. Không đánh đồng mọi cặp kết quả khác title với ambiguity,
và không gọi một câu trả lời so sánh hai nguồn có chủ đích là mơ hồ.

## Bằng chứng kiểm chứng độc lập

Probes chạy trên production CardLookupRetriever/AiChatAnswerService đã build, với
EF InMemory và dữ liệu synthetic riêng. Resolver chỉ nạp dependency ASP.NET từ
runtime local để PowerShell gọi assembly; không boot web hay đọc DB thật. Probe
đầu tiên thiếu dependency đã bị loại khỏi bằng chứng; kết quả dưới dùng runner
hoàn tất thành công, có control forecast.

- Script ignored: `.tmp/hybrid-rag-review/phase-1/round-1/Probe.ps1`.
- Kết quả: `.tmp/hybrid-rag-review/phase-1/round-1/behavior-probes.json`.
- Chạy lại: `pwsh -NoProfile -File .tmp/hybrid-rag-review/phase-1/round-1/Probe.ps1`.
- Đây là bằng chứng logic service/LINQ, không phải chứng nhận query MariaDB.

Hai đợt test .NET không trả phí, cùng cấu hình riêng Phase1Review:

1. Boundary, Phase 0 fixture, retriever, history/quota/cancel/hardening và consumer:
   **173 passed, 0 failed**, 33,5665 giây sau build.
2. AiChatServiceTests/AiContextBuilderTests/AiConversationServiceTests,
   OpenAiProviderClientTests và MlNet: **82 passed, 0 failed**.

Không cộng hai con số thành số test duy nhất vì filter có thể chồng nhau. TRX ở
`.tmp/hybrid-rag-review/phase-1/round-1/{phase1-review1,phase1-extra-regressions}.trx`.
Các probe lỗi nằm ngoài những assertion hiện có, nên test xanh không đóng findings.

Các lệnh test đã chạy, không phải lệnh đề xuất cho một đợt chưa thực hiện:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1Review --filter 'FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiPhase2ServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=normal' --results-directory .tmp/hybrid-rag-review/phase-1/round-1 --logger 'trx;LogFileName=phase1-review1.trx'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1Review --no-build --filter 'FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet' --logger 'console;verbosity=minimal' --results-directory .tmp/hybrid-rag-review/phase-1/round-1 --logger 'trx;LogFileName=phase1-extra-regressions.trx'
```

Lệnh thứ hai bổ sung các class thật trong file AiPhase2ServiceTests, thay vì mặc
nhiên coi tên file là tên class. Tooling validator đạt; Python suite chạy lại
**135 test: 134 pass, 1 skip**, 74,312 giây. Skip là nhánh non-Windows; các test
staging dùng fixture, không phát hành hosting. Link/UTF-8/whitespace của artifact
review và trạng thái gate đã được kiểm tra.

## Gate và bàn giao sửa

Giữ Phase 1 CHANGES_REQUESTED; Phase 0 giữ APPROVED, Phase 2–7 LOCKED. Không mở
Phase 2 để chuyển các lỗi đang tồn tại sang công việc phase sau.

- [Plan fix vòng 1](../../../superpowers/plans/hybrid-rag/phase-1-fix-round-1.md).
- [Prompt Cursor Auto fix](../../../../.ai/prompts/hybrid-rag/phase-1-fix-round-1.md).

Không sửa app/test trong lượt review. Chưa có browser, paid API, CI hoặc deployment.
Các rủi ro khác được ghi trong handoff vẫn cần xem lại theo đúng phase; không coi
các giả định về scale/semantic quality chưa đo là lỗi đã tái hiện.
