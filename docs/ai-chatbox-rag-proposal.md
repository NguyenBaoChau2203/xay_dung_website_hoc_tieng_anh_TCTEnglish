# Đề Xuất Cập Nhật: Xây Dựng Chatbox AI Nội Bộ Dựa Trên Dữ Liệu TCT English

> **Tác giả:** AI Assistant  
> **Ngày cập nhật:** 2026-04-13  
> **Trạng thái:** Đề xuất đã được đánh giá và bổ sung yếu tố Machine Learning (ML.NET) để đạt chuẩn "AI thực sự" theo yêu cầu giảng viên
> **Đánh giá kỹ thuật:** Đã qua code-level review lần 2 (13/04/2026) - bổ sung phân tích impact lên streaming, migration strategy, và chi tiết hóa integration points
>
> ---

## 1. Bối cảnh và yêu cầu mới

Giảng viên không chấp nhận hướng triển khai hiện tại vì AI chatbox đang phụ thuộc
vào **Gemini API bên ngoài**. Mục tiêu mới không còn là "chat với AI chung chung"
mà là:

1. Xây dựng một chatbox thuộc hệ thống của chính TCT English.
2. Câu trả lời phải bám vào dữ liệu và tính năng thật của website.
3. Không trả lời lan man kiến thức ngoài phạm vi website.
4. Có thể giải thích rõ cho giảng viên phần "trí tuệ" mà nhóm tự xây dựng.

Điều này đồng nghĩa với việc kế hoạch cũ theo hướng **Gemini + RAG** không còn phù
hợp làm phương án chính, dù về mặt kỹ thuật nó vẫn tốt hơn gọi API thuần.

---

## 2. Đánh giá kế hoạch cũ

### 2.1 Điểm hợp lý nên giữ lại

| Phần | Đánh giá |
|---|---|
| Tách `retriever` theo từng domain | Hợp lý, dễ mở rộng, phù hợp kiến trúc service của repo |
| Tận dụng `AiController`, `AiChatService`, UI chat, lịch sử hội thoại | Rất hợp lý, giúp không phải làm lại toàn bộ chatbox |
| Query dữ liệu bằng EF Core theo `userId` | Phù hợp với rule anti-IDOR của dự án |
| Dùng intent detection để chọn nguồn dữ liệu | Đúng hướng và đủ tốt cho đồ án nếu làm đơn giản |

### 2.2 Điểm chưa phù hợp với yêu cầu mới

| Vấn đề | Mức độ |
|---|---|
| Vẫn phụ thuộc `GeminiProviderClient` | Không đạt yêu cầu của giảng viên |
| Vẫn cho phép fallback sang kiến thức tiếng Anh chung | Trái với mục tiêu chỉ trả lời trong phạm vi web |
| Gợi ý dùng Gemini để detect intent ở bước nâng cao | Không nên giữ |
| Tư duy "augment prompt rồi gửi LLM" | Phù hợp với RAG, nhưng chưa phải "AI nội bộ" theo mong muốn hiện tại |
| Token budget, model config, retry provider | Làm kế hoạch nặng hơn mức cần thiết nếu không còn external LLM |

### 2.3 Kết luận đánh giá

Kế hoạch cũ **có nền tảng tốt về tư duy truy xuất dữ liệu**, nhưng **không đạt yêu
cầu cốt lõi** là bỏ Gemini API bên ngoài. Vì vậy nên đổi hướng từ:

`Gemini + RAG`

thành:

`Internal Retrieval Assistant = phân loại câu hỏi + truy xuất dữ liệu web + tạo câu trả lời theo rule/template`

---

## 3. Đánh giá mức độ khả thi

### 3.1 Khả thi tổng thể

| Hướng | Khả thi | Dễ thực hiện | Phù hợp yêu cầu | Đánh giá |
|---|---|---|---|---|
| Giữ Gemini + thêm RAG | Cao | Trung bình | Không phù hợp | Giảng viên đã từ chối. |
| Dùng local LLM/Ollama | Trung bình | Khó | Phù hợp một phần | Nặng máy, khó deploy, dễ gặp lỗi môi trường khi demo. |
| Tự train LLM từ đầu | Rất thấp | Rất khó | Không thực tế | Vượt quá khả năng và tài nguyên của đồ án. |
| **P/A 1:** Retrieval + Rule/Regex/Template | Rất Cao | Dễ | Hợp lý | Dễ bị giảng viên bắt bẻ là "Code bằng if-else/regex chứ chưa phải AI". |
| **P/A 2:** Retrieval + **ML.NET** để phân loại Intent | **Cao** | **Trung bình** | **Đạt tuyệt đối** | **(ĐỀ XUẤT CHÍNH)** Sử dụng Machine Learning để hiểu ý định người dùng. Hoàn toàn nội bộ, nhẹ, không gọi API, mang đậm tính "tự xây dựng AI". |

### 3.2 Nhận định thực tế theo codebase hiện tại

Repo đã có sẵn các phần rất có giá trị:

- `TCTEnglish/Controllers/AiController.cs` — 211 dòng, endpoint `/AI/Chat/Send` đã production-ready
- `TCTEnglish/Services/AI/AiChatService.cs` — 415 dòng, quản lý conversation, rate-limit, rollback
- `TCTEnglish/Services/AI/AiStreamingService.cs` — 181 dòng, fake-streaming qua SignalR chunks
- Bảng `AiConversations`, `AiMessages`, `AiRequestLogs` đã có schema ổn định
- Giao diện chat (`Views/Ai/Chat.cshtml`, `ChatEmbed.cshtml`) và JS client (`ai-chat.js`)
- Rate limit (`AiRequestRateLimiter`), observability (`AiObservabilityService`)
- Conversation guard (`AiConversationExecutionGuard`) chống duplicate request

Vì vậy, nhóm **không phải xây lại chatbox từ số 0**. Phần cần thay chủ yếu là
"bộ não trả lời":

1. Bỏ luồng gọi `GeminiProviderClient`.
2. Thay bằng pipeline nội bộ để hiểu câu hỏi, lấy dữ liệu và ráp câu trả lời.
3. Giữ nguyên UI chat, lưu hội thoại, quota và các endpoint hiện có càng nhiều càng tốt.

### 3.3 Độ khó thực hiện

Đánh giá công bằng:

- Không còn là bài toán "chỉ gọi API rồi hiển thị".
- Nhưng vẫn **dễ hơn rất nhiều** so với việc chạy một LLM local hoặc tự train mô hình.
- Nếu chấp nhận phạm vi hợp lý và không cố làm AI "biết mọi thứ", đây là hướng **khả thi cao** cho đồ án.

Ước lượng thực tế:

| Phạm vi | Thời gian |
|---|---|
| Bản demo đủ dùng cho đồ án (Dùng Regex/Rule) | 3-5 ngày |
| Bản nâng cao (Sử dụng ML.NET để train model AI) | 5-7 ngày |

### 3.4 Nhận xét thêm dưới góc độ đánh giá học thuật (Dành cho báo cáo)

Nếu hệ thống chỉ dừng lại ở việc dùng **Regex (Biểu thức chính quy)** hoặc **Keyword Matching** để hiểu câu hỏi của user, giảng viên hoàn toàn có cơ sở để nhận xét: *"Đây chỉ là lập trình cơ bản (hardcode rule), không phải là Trí tuệ nhân tạo (AI)"*.

Để giải quyết triệt để điểm yếu này, bản đề xuất đã được nâng cấp bằng việc tích hợp thêm **ML.NET (Machine Learning cho .NET - công nghệ chính chủ Microsoft)**. Nhóm sẽ tự chuẩn bị một tập dữ liệu nhỏ (Dataset - file CSV chứa các câu hỏi mẫu) và dùng ML.NET để huấn luyện (train) một mô hình **Text Classification (Phân loại văn bản)**. Mô hình này sẽ dự đoán `UserIntent` một cách thông minh dựa trên xác suất, đáp ứng hoàn hảo tiêu chí "Xây dựng hệ thống AI thực thụ của riêng nhóm" mà không phụ thuộc hạ tầng mạng hay API bên ngoài.

### 3.5 ⚠️ Rủi ro kỹ thuật của ML.NET cần lưu ý trước khi bắt đầu

> **MỤC NÀY ĐƯỢC BỔ SUNG SAU REVIEW LẦN 2** — ML.NET là lựa chọn đúng hướng nhưng có một số gotcha cần biết trước:

| Rủi ro | Giải thích | Cách giảm thiểu |
|---|---|---|
| **Dataset quá nhỏ** | ML.NET cần tối thiểu ~20-30 mẫu **mỗi intent** để cho kết quả đáng tin. Viết chỉ 5-10 mẫu/intent sẽ bị overfit hoặc predict sai nhiều. | Mỗi intent nên có **≥ 50 mẫu** — vd: 50 cách hỏi khác nhau cho `MyVocabulary`. Có thể dùng paraphrasing thủ công hoặc nhờ AI tạo mẫu ban đầu rồi review lại. |
| **Tiếng Việt tokenization** | ML.NET sử dụng whitespace tokenizer, tiếng Việt tách từ khác tiếng Anh. Ví dụ: "từ vựng" là 2 token nhưng mang 1 nghĩa. | Chấp nhận giới hạn này ở phase đầu. Bổ sung thêm **synonym normalization** (thay "bộ từ", "bộ thẻ", "set từ" → "vocabulary") trước khi đưa vào model. |
| **Thời gian train ban đầu** | Train lần đầu trên máy dev mất 5-30 giây. Nếu train trong `Program.cs` startup, app sẽ khởi động chậm. | **Train offline** → lưu model ra file `.zip` → load model khi app start. Không train lúc runtime. |
| **Model stale** | Nếu thêm intent mới, phải re-train. Nếu quên re-train thì intent mới luôn bị classify sai. | Viết script/command train riêng. Thêm unit test verify tất cả enum intent đều có trong dataset. |
| **Confidence threshold** | ML.NET trả về `Score` (probability). Nếu không set threshold, câu hỏi lạ vẫn bị map vào intent nào đó với confidence thấp thay vì `OutOfScope`. | Bắt buộc thêm logic: **nếu max score < 0.55 → fallback về `OutOfScope`**. |

---

## 4. Giải pháp đề xuất mới

### 4.1 Tên đúng cho giải pháp

Không nên tiếp tục gọi đây là "Gemini RAG" trong tài liệu chính.

Tên nên dùng:

**TCT English Internal Knowledge Chat Assistant**

hoặc ngắn hơn:

**Chat assistant nội bộ dựa trên dữ liệu website**

### 4.2 Ý tưởng cốt lõi

Chatbox sẽ trả lời theo pipeline sau:

1. Nhận câu hỏi của user.
2. Phân loại user đang hỏi về domain nào.
3. Truy xuất dữ liệu đúng từ database hoặc knowledge file của website.
4. Ghép câu trả lời bằng template/rule nội bộ.
5. Nếu câu hỏi ngoài phạm vi website, từ chối lịch sự và hướng user về các chủ đề được hỗ trợ.

Điểm quan trọng:

- Không dùng external AI provider.
- Không fallback sang kiến thức tiếng Anh chung.
- Không "đoán" khi không có dữ liệu.

### 4.3 Kiến trúc tổng thể đề xuất

```text
User
  -> AiController (GIỮ NGUYÊN)
  -> AiChatService (REFACTOR — thay _providerClient bằng internal pipeline)
     -> IAiQueryClassifier (MỚI)
        -> KeywordAiQueryClassifier (Phase 1)
        -> MlNetAiQueryClassifier  (Phase 2 — ML.NET)
     -> IWebsiteKnowledgeService (MỚI)
        -> UserVocabularyRetriever
        -> LearningProgressRetriever
        -> SpeakingRetriever
        -> ClassRetriever
        -> WebsiteGuideRetriever
        -> CardLookupRetriever
     -> IAnswerComposer (MỚI)
  -> ChatReplyDto (GIỮ NGUYÊN — token fields = 0)
  -> AiStreamingService (GIỮ NGUYÊN — fake-streaming vẫn hoạt động)
  -> UI chat hiện có (GIỮ NGUYÊN 100%)
```

### 4.4 "AI" nằm ở đâu nếu không dùng Gemini?

Nếu giảng viên hỏi "AI của em ở đâu?", câu trả lời hợp lý là:

1. **Intent / Query classification**: hiểu người dùng đang hỏi về gì.
2. **Knowledge retrieval**: chọn đúng nguồn dữ liệu trong hệ thống.
3. **Ranking / filtering**: lấy dữ liệu phù hợp nhất thay vì dump toàn bộ.
4. **Answer composition**: sinh câu trả lời theo cấu trúc thông minh, có ưu tiên, có gợi ý bước tiếp theo.
5. **Refusal policy**: biết khi nào phải từ chối vì ngoài phạm vi dữ liệu.

Đây không phải "train một mô hình ngôn ngữ từ đầu", nhưng là một **hệ thống hỏi đáp
thông minh do nhóm tự xây dựng**, phù hợp hơn nhiều với yêu cầu đồ án.

---

## 5. Thiết kế kỹ thuật chi tiết

### 5.1 Query Classifier

Classifier nên đủ đơn giản để dễ làm, nhưng đủ rõ để có thể giải thích trong báo cáo.

#### Intent đề xuất

```csharp
public enum UserIntent
{
    MyVocabulary,       // Hỏi về bộ từ vựng của tôi
    MyProgress,         // Hỏi về tiến độ học
    CardLookup,         // Tra nghĩa một từ trong hệ thống
    SpeakingSuggestion, // Gợi ý bài speaking
    ClassInfo,          // Thông tin lớp học
    WebsiteGuide,       // Cách sử dụng website
    StudyRecommendation,// Gợi ý nên học gì tiếp
    Greeting,           // Chào hỏi cơ bản (THÊM MỚI)
    OutOfScope          // Ngoài phạm vi
}
```

> **BỔ SUNG:** Thêm intent `Greeting` — rất quan trọng vì đây là câu hỏi phổ biến nhất khi user mở chatbox ("Xin chào", "Hi", "Bạn là ai?"). Nếu không có intent này, lời chào sẽ bị classify thành `OutOfScope` — trải nghiệm người dùng rất tệ.

#### Lưu ý quan trọng

- `OutOfScope` là intent bắt buộc phải có.
- Nếu user hỏi kiểu "giải thích present perfect", "dịch đoạn văn này", "viết essay giúp tôi", hệ thống phải trả lời rằng hiện chỉ hỗ trợ dữ liệu và tính năng trong TCT English.
- Không nên có intent kiểu `GeneralEnglish` nữa.

#### Cách làm phù hợp và cách tạo tính "AI" thực thụ

- **Phase 1 (Nền tảng):** Sử dụng keyword matching + regex + heuristic để hệ thống có thể chạy ngay và xử lý các mẫu câu cơ bản. Rất nhanh để làm.
- **Phase 2 (Trở thành AI thực sự với ML.NET - CỰC KỲ QUAN TRỌNG):** 
  - Thay thế hoặc bổ trợ Regex bằng một mô hình **Machine Learning (ML.NET)**.
  - Xây dựng dataset gồm vài trăm mẫu câu hỏi tương ứng (ví dụ: *"Tiến độ của tôi", "Tôi học được bao nhiêu rồi"* -> `MyProgress`).
  - Dùng thuật toán Multi-class Classification của ML.NET (`SdcaMaximumEntropy`) để huấn luyện mô hình dự đoán Intent.
  - **Với vũ khí này, nhóm hoàn toàn tự tin khẳng định trước hội đồng rằng đã tự thu thập dữ liệu và tự train mô hình AI Machine Learning cho bài toán nhận diện ngôn ngữ.**
  - Không hề sử dụng LLM bên ngoài.

#### 5.1.1 Thiết kế interface cho Classifier (BỔ SUNG)

```csharp
/// <summary>
/// Kết quả phân loại intent, bao gồm cả confidence score
/// để hỗ trợ fallback logic.
/// </summary>
public sealed record IntentClassification(
    UserIntent Intent,
    float Confidence,     // 0.0 → 1.0
    string ClassifierName // "keyword" hoặc "ml.net" — hữu ích cho logging
);

public interface IAiQueryClassifier
{
    IntentClassification Classify(string userMessage);
}
```

> **BỔ SUNG:** Interface trả về `IntentClassification` thay vì chỉ `UserIntent` enum. Lý do:
> - `Confidence` cho phép áp dụng threshold (< 0.55 → `OutOfScope`).
> - `ClassifierName` giúp logging biết đang dùng keyword hay ML.NET — quan trọng cho debug và demo.
> - Phase 1 (`KeywordAiQueryClassifier`) trả `Confidence = 1.0f` khi match, `0.0f` khi không match.

### 5.2 Knowledge Retriever Layer

Khuyến nghị đổi từ kiểu trả về chuỗi text dài sang dữ liệu có cấu trúc.

```csharp
public sealed record KnowledgeSnippet(
    string Title,
    string Body,
    string Source,
    string? Route = null,
    int Priority = 0);

public interface IKnowledgeRetriever
{
    bool CanHandle(UserIntent intent);
    Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(
        int userId,
        string message,
        CancellationToken ct);
}
```

Thiết kế này hợp lý hơn `RagContext(string ContextText, string SourceDescription)` vì:

1. Dễ test.
2. Dễ xếp hạng snippet.
3. Dễ tạo câu trả lời theo template.
4. Dễ gắn link trang liên quan cho user.

### 5.3 Các retriever nên có trong phase đầu

| Retriever | Nguồn dữ liệu | Ghi chú |
|---|---|---|
| `UserVocabularyRetriever` | `Sets`, `Cards`, `LearningProgress` | Trả lời "tôi có set nào", "set nào gần hoàn thành" |
| `LearningProgressRetriever` | `LearningProgress`, streak/goals nếu có | Trả lời tiến độ học |
| `ClassRetriever` | `Classes`, `ClassMembers` | Trả lời thông tin lớp học |
| `SpeakingRetriever` | `SpeakingPlaylists`, `SpeakingVideos`, `UserSpeakingProgress` | Gợi ý bài speaking |
| `WebsiteGuideRetriever` | file JSON/Markdown nội bộ hoặc dữ liệu hardcoded giai đoạn đầu | Hướng dẫn dùng web |
| `CardLookupRetriever` | `Cards` thuộc set của user hoặc nguồn cho phép | Trả lời nghĩa/từ có trong hệ thống |

> **BỔ SUNG — Chi tiết EF query pattern cho Retriever:**
>
> Mỗi retriever phải tuân thủ:
> - `.AsNoTracking()` cho mọi query (read-only)
> - `.Where(x => x.UserId == userId ...)` (anti-IDOR)
> - `.Take(N)` để giới hạn kết quả (tránh dump toàn bộ DB vào câu trả lời)
>
> Ví dụ `UserVocabularyRetriever`:
> ```csharp
> var sets = await _context.Sets
>     .AsNoTracking()
>     .Where(s => s.UserId == userId)
>     .OrderByDescending(s => s.CreatedDate)
>     .Take(10)
>     .Select(s => new { s.SetId, s.SetName, CardCount = s.Cards.Count })
>     .ToListAsync(ct);
> ```

### 5.4 Website Guide Knowledge Base

Phần này rất nên có vì:

- Dễ demo.
- Không phụ thuộc dữ liệu người dùng.
- Trả lời đúng trọng tâm tính năng website.

Khuyến nghị:

1. Phase 1 có thể hardcode nhỏ trong code để làm nhanh.
2. Phase 2 chuyển sang `JSON` hoặc `Markdown` nội bộ để dễ cập nhật.
3. Để tăng cảm giác tương tác AI, có thể dùng các thuật toán tính độ tương đồng chuỗi (vd như `Fuzzy matching / Jaro-Winkler distance`) để tìm kiếm file hướng dẫn có mức độ khớp cao nhất với câu người dùng, thay vì tìm kiếm chính xác từng chữ (exact match).

Ví dụ nội dung:

- cách tạo set
- cách tạo lớp học
- cách tham gia lớp
- cách học flashcard/quiz/write/matching
- cách mở phần speaking

### 5.5 Answer Composer

Đây là thành phần thay vai trò "generate câu trả lời", nhưng không cần LLM.

Khuyến nghị tạo interface riêng:

```csharp
public interface IAnswerComposer
{
    /// <summary>
    /// Compose answer from retrieved knowledge snippets.
    /// Returns the reply text (có thể chứa markdown formatting).
    /// </summary>
    Task<string> ComposeAsync(
        UserIntent intent,
        string userMessage,
        IReadOnlyList<KnowledgeSnippet> snippets,
        CancellationToken ct);
}
```

> **BỔ SUNG:** Interface đổi thành `async Task<string>` thay vì `string Compose(...)` đồng bộ. Lý do:
> - Phase 2 có thể cần tra thêm dữ liệu bổ trợ (vd: lấy tên set khi compose câu gợi ý)
> - Nhất quán với async-first rule của project
> - Dù Phase 1 implementation không cần async thật, vẫn nên design interface async để không phải breaking change sau

#### Nguyên tắc tạo câu trả lời

1. Trả lời thẳng vào câu hỏi trước.
2. Chỉ dùng dữ liệu đã truy xuất được.
3. Nếu không có dữ liệu, nói rõ là hiện chưa tìm thấy trong hệ thống.
4. Có thể gợi ý 1-2 hành động tiếp theo trên website.
5. Không bịa thêm kiến thức ngoài dữ liệu nội bộ.

#### Mẫu format nên dùng

```text
Kết quả chính

- Thông tin 1
- Thông tin 2
- Thông tin 3

Bạn có thể tiếp tục:
- Hành động A
- Hành động B
```

### 5.6 Chính sách từ chối câu hỏi ngoài phạm vi

Đây là phần bắt buộc nếu muốn chatbox "không trả lời lan man".

Ví dụ phản hồi chuẩn:

> Hiện tại mình chỉ hỗ trợ câu hỏi liên quan đến dữ liệu và tính năng của TCT English như bộ từ vựng, tiến độ học, lớp học, speaking và cách sử dụng website.  
> Bạn có thể hỏi theo các dạng như:
> - Tôi có những bộ từ vựng nào?
> - Tiến độ học của tôi ra sao?
> - Gợi ý bài speaking phù hợp
> - Cách tạo lớp học hoặc tạo set

### 5.7 Phạm vi memory hội thoại

Kế hoạch cũ ngầm giả định LLM sẽ hiểu toàn bộ history. Khi bỏ Gemini thì cần nói rõ:

| Mức | Khuyến nghị |
|---|---|
| Phase 1 | Lưu lịch sử để hiển thị và mở lại conversation, nhưng trả lời theo từng lượt là chính |
| Phase 2 | Cho phép nhớ ngữ cảnh ngắn như domain vừa hỏi gần nhất |
| Không nên làm ngay | Coreference phức tạp kiểu "cái thứ hai thì sao", "so sánh với cái ở trên" |

Nói thẳng điều này trong kế hoạch sẽ làm tài liệu thực tế hơn.

### 5.8 Logging và observability

Nên giữ observability hiện có, nhưng đổi trọng tâm metric:

- request count
- latency
- intent distribution
- out-of-scope rate
- no-data-found rate

`PromptTokens` và `CompletionTokens` có thể:

1. tạm thời giữ bằng `0` để không phải đổi DB ngay, hoặc
2. về sau đổi ý nghĩa metric nếu thật sự cần

### 5.9 ⚠️ Chiến lược Migration từ Gemini Provider (BỔ SUNG MỚI)

> **Mục này bổ sung sau khi audit code `AiChatService.cs` (415 dòng)** — service hiện tại gắn chặt với `IAiProviderClient`, cần kế hoạch cụ thể để thay thế.

#### 5.9.1 Điểm gắn chặt trong `AiChatService` cần refactor

| Dòng code | Phụ thuộc | Cách xử lý |
|---|---|---|
| `_providerClient.GenerateReplyAsync(contextResult.Messages, ct)` (L183) | `IAiProviderClient` | Thay bằng internal pipeline: classify → retrieve → compose |
| `_contextBuilder.BuildContextMessages(...)` (L122-L126) | `IAiContextBuilder` — build prompt cho LLM | Không còn cần build LLM prompt. Bỏ dependency này hoặc giữ shell rỗng. |
| Token budget checks (L128-L176) | `RequestTokenBudget`, `DailyTokenBudgetPerUser` | Bỏ hoặc đơn giản hóa — internal pipeline không tốn token. Giữ daily request limit (15 câu/ngày cho Standard user) là đủ. |
| `catch (AiProviderException ex)` (L185-L215) | Exception từ external provider | Thay bằng internal error handling. Internal pipeline không throw `AiProviderException`. |
| `AiProviderReply` (L179, L219-L266) | DTO chứa tokens, model name | Tạo 1 internal DTO mới (`InternalReplyResult`) hoặc tái sử dụng `AiProviderReply` với token = 0, model = "internal-v1". |

#### 5.9.2 Chiến lược refactor đề xuất (QUAN TRỌNG)

**Không nên** xóa sạch code Gemini ngay. Thay vào đó, áp dụng **Strategy Pattern**:

```csharp
// Tạo implementation mới của IAiProviderClient
// → internal pipeline cũng implement cùng interface
// → DI switch bằng config, không cần sửa AiChatService nhiều

public sealed class InternalKnowledgeProvider : IAiProviderClient
{
    private readonly IAiQueryClassifier _classifier;
    private readonly IWebsiteKnowledgeService _knowledge;
    private readonly IAnswerComposer _composer;

    public async Task<AiProviderReply> GenerateReplyAsync(
        IReadOnlyList<AiContextMessage> messages, CancellationToken ct)
    {
        // Lấy câu hỏi cuối cùng từ messages (user message)
        var userMessage = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";

        // Pipeline: classify → retrieve → compose
        var classification = _classifier.Classify(userMessage);
        var snippets = await _knowledge.RetrieveAsync(userId, classification.Intent, userMessage, ct);
        var answer = await _composer.ComposeAsync(classification.Intent, userMessage, snippets, ct);

        return new AiProviderReply(
            Text: answer,
            PromptTokens: 0,
            CompletionTokens: 0,
            TotalTokens: 0,
            Model: $"internal-{classification.ClassifierName}",
            RequestId: Guid.NewGuid().ToString("N"));
    }
}
```

> **Lợi ích cách này:**
> - `AiChatService`, `AiController`, `AiStreamingService` **giữ nguyên 100%** — không cần sửa
> - `AiStreamingService` fake-streaming vẫn hoạt động bình thường vì nó chỉ cần `ChatReplyDto.Text`
> - Chỉ cần đổi DI registration: `services.AddScoped<IAiProviderClient, InternalKnowledgeProvider>()` thay vì `GeminiProviderClient`
> - Muốn quay lại Gemini (ví dụ để demo so sánh) chỉ cần đổi 1 dòng DI
> - Token budget checks vẫn pass vì token = 0 luôn < budget

#### 5.9.3 Vấn đề `userId` — Cách giải quyết

Nhìn vào `IAiProviderClient.GenerateReplyAsync`, interface hiện tại **không nhận `userId`** — chỉ nhận `messages`. Nhưng internal pipeline cần `userId` để query dữ liệu cá nhân.

**Giải pháp đề xuất:** Truyền `userId` qua constructor injection hoặc thêm vào context:

```csharp
// Option A: Thêm userId vào AiContextMessage cuối cùng dưới dạng metadata
// → Hacky, không nên

// Option B (ĐỀ XUẤT): Mở rộng interface nhẹ
public interface IAiProviderClient
{
    Task<AiProviderReply> GenerateReplyAsync(
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct);

    // Thêm optional overload cho internal use
    Task<AiProviderReply> GenerateReplyAsync(
        int userId,
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct)
        => GenerateReplyAsync(messages, ct); // default implementation
}

// Option C (ĐƠN GIẢN NHẤT): Sửa AiChatService.SendAsync truyền userId
// vào _providerClient — chỉ cần sửa 1 dòng trong AiChatService + interface
```

> **Khuyến nghị:** Dùng **Option C** — sửa interface `IAiProviderClient` thêm `int userId`. Sửa ít, rõ ràng, `GeminiProviderClient` chỉ cần ignore parameter đó.

### 5.10 ⚠️ Streaming Impact Analysis (BỔ SUNG MỚI)

> **Mục này bổ sung sau khi đọc `AiStreamingService.cs`** — đây là phần kế hoạch gốc chưa đề cập.

`AiStreamingService` hiện hoạt động như sau:
1. Gọi `_chatService.SendAsync()` → nhận full reply text
2. Split text thành chunks 90 chars
3. Gửi từng chunk qua SignalR với delay → tạo hiệu ứng "typing"

**Tin tốt:** Cơ chế này **hoàn toàn tương thích** với internal pipeline vì:
- Không phụ thuộc vào real-time streaming từ Gemini
- Chỉ cần full reply text → split → emit chunks
- Internal pipeline trả về full text nhanh hơn Gemini → streaming thậm chí nhanh hơn

**Không cần sửa gì trong `AiStreamingService`** nếu dùng Strategy Pattern ở mục 5.9.2.

---

## 6. Ảnh hưởng tới codebase hiện tại

### 6.1 Phần có thể giữ nguyên gần như hoàn toàn

- `TCTEnglish/Controllers/AiController.cs` — **giữ 100%** (nếu dùng Strategy Pattern)
- `TCTEnglish/Services/AI/AiChatService.cs` — **giữ 95%** (chỉ sửa 1-2 dòng truyền userId)
- `TCTEnglish/Services/AI/AiStreamingService.cs` — **giữ 100%**
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/Views/Ai/ChatEmbed.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`
- bảng `AiConversations`, `AiMessages`, `AiRequestLogs`
- rate limit, lịch sử chat, quota hiển thị
- `AiConversationExecutionGuard`, `AiObservabilityService`

### 6.2 Phần cần đổi hướng

| File / nhóm file | Mức thay đổi | Ghi chú |
|---|---|---|
| `IAiProviderClient.cs` | Thấp | Thêm `userId` param vào method signature |
| `GeminiProviderClient.cs` | Không sửa | Giữ nguyên nhưng không register trong DI |
| `AiChatService.cs` | Thấp | Sửa 1 dòng gọi `GenerateReplyAsync` truyền thêm `userId` |
| `AiContextBuilder.cs` + `IAiContextBuilder.cs` | Giảm vai trò | Internal pipeline không cần build LLM prompt. Có thể giữ để không break compile. |
| `AiOptions.cs` | Giảm phạm vi | `ApiKey`, `BaseUrl`, `Model`, `Temperature`, `ModelContextLimit` không còn dùng. Có thể giữ để không break config. |
| `Program.cs` | Thấp | Đổi DI registration + đăng ký services mới |
| `TCTEnglish.Tests/*AI*` | Trung bình | Test hiện tại đang gắn với provider → cần update mock |

### 6.3 Kết luận về effort

Đây là một refactor có kiểm soát, không phải viết lại toàn bộ tính năng AI chat.
Khối lượng công việc chủ yếu nằm ở **viết code mới** (classifier, retrievers, composer), **không phải sửa code cũ**.

---

## 7. Cấu trúc thư mục đề xuất sau khi đổi hướng

```text
TCTEnglish/Services/AI/
├── AiChatService.cs            (sửa nhẹ)
├── AiOptions.cs                (giữ)
├── IAiProviderClient.cs        (sửa interface nhẹ)
├── GeminiProviderClient.cs     (giữ, không register)
├── Internal/
│   ├── InternalKnowledgeProvider.cs     (MỚI — implement IAiProviderClient)
│   ├── IAiQueryClassifier.cs
│   ├── IntentClassification.cs
│   ├── KeywordAiQueryClassifier.cs
│   ├── MlNetAiQueryClassifier.cs       (Phase 2)
│   ├── UserIntent.cs
│   ├── KnowledgeSnippet.cs
│   ├── IKnowledgeRetriever.cs
│   ├── IWebsiteKnowledgeService.cs
│   ├── WebsiteKnowledgeService.cs
│   ├── IAnswerComposer.cs
│   ├── TemplateAnswerComposer.cs
│   ├── Retrievers/
│   │   ├── UserVocabularyRetriever.cs
│   │   ├── LearningProgressRetriever.cs
│   │   ├── ClassRetriever.cs
│   │   ├── SpeakingRetriever.cs
│   │   ├── WebsiteGuideRetriever.cs
│   │   └── CardLookupRetriever.cs
│   └── Data/
│       ├── intent-dataset.csv          (Phase 2 — training data)
│       └── intent-model.zip            (Phase 2 — trained model output)
└── (các file hiện có khác giữ nguyên)

TCTEnglish/wwwroot/data/ai/
└── website-guides.json                 (Phase 2)
```

Ghi chú:

- Có thể chưa cần tạo `website-guides.json` ngay ở phase đầu nếu muốn làm nhanh.
- Tên folder `Internal/` hợp lý hơn `Rag/` vì mục tiêu mới không còn xoay quanh external generation.
- Thêm subfolder `Retrievers/` để tách biệt retriever implementations — dễ navigate hơn khi có 6+ files.
- Thêm subfolder `Data/` cho ML.NET dataset và model output.

---

## 8. Ví dụ hành vi mong muốn

### 8.1 Câu hỏi trong phạm vi

**User:** Tôi có những bộ từ vựng nào?

**Trả lời mong muốn:**

> Bạn hiện có 3 bộ từ vựng trong tài khoản:
> - TOEIC Part 5: 50 thẻ
> - Daily English: 25 thẻ
> - Business Terms: 40 thẻ
>
> Nếu muốn, mình có thể tiếp tục gợi ý bộ nào nên ưu tiên học trước.

### 8.2 Câu hỏi hướng dẫn dùng web

**User:** Cách tạo lớp học?

**Trả lời mong muốn:**

> Để tạo lớp học trên TCT English:
> 1. Vào trang Lớp học.
> 2. Chọn Tạo lớp mới.
> 3. Nhập tên lớp và mô tả.
> 4. Có thể đặt mật khẩu nếu cần.
> 5. Lưu và chia sẻ cho thành viên.

### 8.3 Câu hỏi ngoài phạm vi

**User:** Giải thích thì present perfect và past simple khác nhau thế nào?

**Trả lời mong muốn:**

> Hiện tại mình chỉ hỗ trợ dữ liệu và tính năng có trong TCT English, ví dụ bộ từ vựng, tiến độ học, lớp học, speaking và hướng dẫn sử dụng website.  
> Bạn có thể hỏi lại theo các chủ đề này để mình hỗ trợ chính xác hơn.

Ví dụ này rất quan trọng vì nó thể hiện hệ thống "biết giới hạn của mình".

### 8.4 Lời chào (BỔ SUNG)

**User:** Xin chào

**Trả lời mong muốn:**

> Chào bạn! 👋 Mình là trợ lý TCT English. Mình có thể giúp bạn:
> - Xem bộ từ vựng và tiến độ học
> - Gợi ý bài speaking phù hợp
> - Tra nghĩa từ trong bộ thẻ của bạn
> - Hướng dẫn cách sử dụng website
>
> Bạn muốn hỏi về điều gì?

---

## 9. Kế hoạch thực hiện đề xuất

### Phase 0: Chốt phạm vi với giảng viên và nhóm

Thời gian: 0.5 ngày

| # | Công việc | Kết quả |
|---|---|---|
| 1 | Chốt rằng không dùng external API | Tiêu chí rõ ràng cho implementation |
| 2 | Chốt rằng hệ thống chỉ trả lời trong phạm vi website | Tránh scope creep |
| 3 | Chốt danh sách domain hỗ trợ ở bản đầu | Vocabulary, progress, class, speaking, guide |

### Phase 1: Bản chạy được cho demo

Thời gian: 1.5-2 ngày

| # | Công việc | File dự kiến | Chi tiết kỹ thuật |
|---|---|---|---|
| 1 | Tạo `UserIntent`, `IntentClassification`, `KeywordAiQueryClassifier` | `Services/AI/Internal/*` | Keyword matching + confidence = 1.0 khi match |
| 2 | Tạo `KnowledgeSnippet`, `IKnowledgeRetriever`, `IWebsiteKnowledgeService`, `WebsiteKnowledgeService` | `Services/AI/Internal/*` | Service dispatch retriever theo intent |
| 3 | Làm `UserVocabularyRetriever`, `ClassRetriever`, `WebsiteGuideRetriever` (hardcoded) | `Services/AI/Internal/Retrievers/*` | EF query với `.AsNoTracking()` + anti-IDOR |
| 4 | Làm `TemplateAnswerComposer` | `Services/AI/Internal/TemplateAnswerComposer.cs` | StringBuilder + template per intent |
| 5 | Tạo `InternalKnowledgeProvider` (implement `IAiProviderClient`) | `Services/AI/Internal/InternalKnowledgeProvider.cs` | Xem mục 5.9.2 |
| 6 | Sửa `IAiProviderClient` thêm `userId` param | `Services/AI/IAiProviderClient.cs` | Breaking change nhỏ — sửa cả `GeminiProviderClient` ignore param |
| 7 | Cập nhật DI registration trong `Program.cs` | `Program.cs` | Đổi `GeminiProviderClient` → `InternalKnowledgeProvider` |
| 8 | Bổ sung unit tests cho classifier, retriever, composer | `TCTEnglish.Tests/*` | Mock `DbflashcardContext` cho retriever tests |

### Phase 2: Mở rộng domain và huấn luyện Mô hình AI (ML.NET)

Thời gian: 2-3 ngày

| # | Công việc | Ghi chú |
|---|---|---|
| 1 | Thêm `LearningProgressRetriever`, `SpeakingRetriever`, `CardLookupRetriever` | Mở rộng tính năng trả lời đa dạng |
| 2 | Chuẩn bị Dataset Text Classification (CSV) | Tạo file `intent-dataset.csv` — **≥ 50 mẫu mỗi intent, tối thiểu 9 intents × 50 = 450 dòng** |
| 3 | Tích hợp thư viện `Microsoft.ML` (v4.0+) | Cần thêm `<PackageReference>` vào `.csproj` |
| 4 | Viết script train model offline → output `intent-model.zip` | Console app hoặc unit test project chạy train |
| 5 | Tạo `MlNetAiQueryClassifier` — load model `.zip`, predict intent | Load model 1 lần trong constructor (Singleton lifetime) |
| 6 | Thêm confidence threshold: score < 0.55 → `OutOfScope` | Tránh false positive |
| 7 | Thêm synonym normalization trước khi predict | "bộ từ" → "vocabulary", "lớp" → "class" |
| 8 | Thêm thuật toán tìm kiếm mờ (Fuzzy Search) cho `WebsiteGuideRetriever` | Nâng cao độ chính xác khi truy vấn kiến thức hướng dẫn |
| 9 | Tối ưu Template trả lời & Thêm Link gợi ý | Hướng điều hướng người dùng quay lại với chức năng của trang |

### Phase 3: Hardening trước khi nộp

Thời gian: 1 ngày

| # | Công việc | Ghi chú |
|---|---|---|
| 1 | Bổ sung integration tests cho luồng `/AI/Chat/Send` | Đảm bảo không leak dữ liệu cross-user |
| 2 | Theo dõi metric `out_of_scope` và `no_data_found` | Phục vụ demo và báo cáo |
| 3 | Verify streaming hoạt động đúng với internal provider | Test qua UI thật |
| 4 | Viết phần giải thích kiến trúc cho báo cáo đồ án | Nên làm song song |
| 5 | Tạo bộ test cases cho demo trước giảng viên | 10-15 câu hỏi mẫu cover tất cả intent |

---

## 10. Điểm mạnh của hướng mới trong đồ án

| Tiêu chí | Điểm mạnh |
|---|---|
| Phù hợp yêu cầu | Không gọi Gemini API bên ngoài |
| Tính tự xây dựng (AI Thực thụ) | Áp dụng **ML.NET** để tự train mô hình Machine Learning phân loại ý định, không dùng lệnh If-Else khô khan |
| Bám dữ liệu thật | Trả lời 100% từ DB và knowledge base của website |
| Kiến trúc rõ ràng | Dễ giải thích theo từng layer |
| Bảo mật | Dễ áp anti-IDOR vì mọi query đều đi qua service nội bộ |
| Dễ demo | Có thể demo bằng dữ liệu thật của tài khoản |
| Backward compatible | Strategy Pattern cho phép switch giữa Internal và Gemini bằng 1 dòng DI |

---

## 11. Rủi ro và cách giảm rủi ro

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Câu trả lời kém tự nhiên hơn Gemini | Trung bình | Thiết kế template rõ ràng, ngắn, có format tốt |
| Không hiểu câu hỏi quá tự do | Trung bình | Thêm từ khóa đồng nghĩa, regex, ví dụ hỏi mẫu |
| Follow-up hội thoại phức tạp khó xử lý | Trung bình | Giới hạn phase 1 theo từng lượt |
| Thiếu dữ liệu hướng dẫn website | Thấp | Tạo knowledge file nội bộ nhỏ nhưng chuẩn |
| Test cũ phụ thuộc provider | Trung bình | Tách test theo pipeline mới và giữ test regression quan trọng |
| ML.NET dataset quá nhỏ dẫn đến predict sai | **Cao** | Đảm bảo ≥ 50 mẫu/intent. Thêm unit test verify dataset coverage. |
| ML.NET confidence thấp không được handle | **Cao** | Bắt buộc threshold < 0.55 → `OutOfScope` |
| `userId` không available trong `IAiProviderClient` | Trung bình | Sửa interface thêm param (mục 5.9.3) |

---

## 12. Điều không nên làm ở giai đoạn này

1. Không cố giữ Gemini làm "fallback bí mật".
2. Không thêm local LLM nếu mục tiêu chỉ là qua đồ án với phạm vi web-focused.
3. Không hứa chatbox trả lời được kiến thức tiếng Anh tổng quát.
4. Không dùng vector database hoặc embedding khi chưa thật sự cần.
5. Không mở rộng quá nhiều domain ngay từ đầu.
6. **Không train ML.NET model lúc app startup** — train offline, load `.zip` file.
7. **Không xóa `GeminiProviderClient`** — giữ lại để có thể quay lại nếu cần demo so sánh.

---

## 13. Kết luận cuối cùng

### 13.1 Đánh giá tổng quan

Kế hoạch ban đầu **hợp lý về mặt kỹ thuật RAG**, nhưng **chưa hợp lý về mặt yêu cầu đồ án**
vì vẫn phụ thuộc Gemini API và vẫn cho phép trả lời ngoài phạm vi website.

### 13.2 Hướng nên chốt

Hướng phù hợp nhất là:

**Chat assistant nội bộ dựa trên Truy xuất dữ liệu (Retrieval) + Machine Learning (ML.NET cho Intent Classifier) + Template sinh đáp án, 100% dùng dữ liệu TCT English**

### 13.3 Mức độ dễ làm và giá trị học thuật

Kết luận thực tế:

- Dễ hơn nhiều và không lo rủi ro hệ thống bị sập như khi dùng LLM local (Ollama).
- Rất vừa sức sinh viên dù mức độ vất vả nhỉnh hơn việc đứng gọi API Gemini. 
- **Điểm sáng giá tuyệt đối:** Việc áp dụng được quy trình: *Thu thập Dữ liệu (Dataset) -> Huấn luyện mô hình (ML.NET Train) -> Dự đoán (Predict)* ngay trong lòng .NET Core giúp đồ án thoát mác "Lập trình Web bình thường" và đạt chuẩn "Hệ thống có ứng dụng Công Nghệ Trí Tuệ Nhân Tạo". Điều này giúp quá trình bảo vệ đồ án trước giảng viên trở nên tự tin và có tính giáo dục rất cao.

### 13.4 Quyết định đề xuất

Nên bỏ hướng "Gemini + RAG" làm giải pháp chính và chuyển hẳn sang hướng:

**TCT English Internal Machine Learning Chat Assistant**

Hướng này vừa giải quyết gọn gàng yêu cầu "Đóng cửa từ chối AI ngoài" của giảng viên, vừa cung cấp đất diễn về mặt học thuật để ăn điểm bảo vệ xuất sắc.

---

## 14. Đánh giá tổng kết từ Code Review (BỔ SUNG MỚI)

### 14.1 Verdict: ✅ KHẢ THI CAO — Có thể bắt tay làm ngay

| Tiêu chí | Đánh giá | Ghi chú |
|---|---|---|
| Hướng đi đúng? | ✅ Đúng | Loại bỏ Gemini dependency, tự xây pipeline nội bộ |
| Kiến trúc hợp lý? | ✅ Hợp lý | Pipeline Classify → Retrieve → Compose rõ ràng |
| Impact lên code hiện có? | ✅ Thấp | Strategy Pattern giữ AiController, AiStreamingService, UI nguyên vẹn |
| Thời gian ước tính? | ✅ Thực tế | 3-5 ngày Phase 1, 5-7 ngày full (bao gồm ML.NET) |
| Rủi ro? | ⚠️ Cần chú ý | ML.NET dataset size và confidence threshold là 2 điểm dễ sai nhất |

### 14.2 Top 5 cải thiện đã bổ sung trong review này

1. **Strategy Pattern** (mục 5.9.2) — giữ `IAiProviderClient` interface, tạo `InternalKnowledgeProvider` → giảm 90% sửa code cũ
2. **Streaming impact analysis** (mục 5.10) — xác nhận streaming hoạt động bình thường, không cần sửa
3. **ML.NET gotchas** (mục 3.5) — cảnh báo dataset size, confidence threshold, train-at-startup trap
4. **Intent `Greeting`** (mục 5.1) — thiếu intent chào hỏi sẽ gây UX tệ
5. **`userId` propagation** (mục 5.9.3) — giải quyết gap trong interface hiện tại

### 14.3 Thứ tự ưu tiên khi bắt đầu code

1. **Đầu tiên:** Sửa `IAiProviderClient` thêm `userId` + tạo `InternalKnowledgeProvider` shell → verify compile + streaming vẫn chạy
2. **Sau đó:** Implement `KeywordAiQueryClassifier` + 2-3 retriever cơ bản → có bản demo chạy được
3. **Cuối cùng:** ML.NET classifier + dataset + phần còn lại
