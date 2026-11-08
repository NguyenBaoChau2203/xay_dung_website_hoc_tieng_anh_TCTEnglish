# Phase 0 — hợp đồng đề xuất cho Hybrid RAG

Tên kiểu dưới đây là đề xuất. Chúng chưa được duyệt và chưa được thêm vào runtime. Phase 0 không sửa DI, schema, lesson thật hay cấu hình model.

## 1. Đường đang chạy và đường được phép thêm

Production chat:

1. `POST /AI/Chat/Send` hoặc `ClassChatHub.StartAiStream`.
2. `AiChatService.SendAsync` lưu message user, reserve quota, dựng context system/user/history, gọi `IAiProviderClient`.
3. Bind hiện tại là `OpenAiProviderClient`. Payload Responses API không có tool và không có snippet.
4. Stream SignalR phát lại text đã có, không phải token stream từ provider.

`InternalKnowledgeProvider` chỉ là implementation khác của cùng interface. Test host gắn nó vào `IAiProviderClient`. Production không gắn. Phase sau không được đổi bind toàn cục vì VocabSuggest, Writing generation, Writing evaluation, Reading translation và Listening vẫn dùng interface này.

Đường trả lời chat mới phải là một service chat riêng, gọi provider text hiện tại sau khi đã có evidence. Các consumer kia giữ client text thuần.

## 2. Evidence

Tên đề xuất: `HybridRagEvidence`. Một evidence là một câu hoặc một trường có kiểu, không phải cả lịch sử chat.

| Trường | Ý nghĩa |
| --- | --- |
| `SourceKey` | Khóa ổn định, ví dụ `guide:create-set`, `card:{id}`, `speaking-sentence:{id}` |
| `SourceType` | guide, card, speaking-sentence, reading-passage, writing-exercise, listening-lesson, goal, progress, notification, billing, class, folder |
| `SentenceId` | `{SourceKey}#s{n}` với n là thứ tự câu trong text chuẩn. Câu speaking đã có id DB thì `SourceKey` dùng id đó và vẫn lưu hash nội dung |
| `ContentHash` | SHA-256 UTF-8 của câu chuẩn |
| `CorpusVersion` | Phiên bản tập public đã build |
| `ChunkerVersion` | Phiên bản cách tách câu |
| `EmbeddingModel` | Null với đường lexical/live. Có giá trị thì kèm dimension |
| `AuthorizedRoute` | Route server tạo từ allowlist. Model không được bịa path |
| `Visibility` | `public`, `authenticated`, `owner`, `class-member`, `owner-premium` |
| `OwnerUserId` | Null nếu public |
| `CapturedAtUtc` | Lúc lấy evidence cho lượt đó |
| `Text` | Nguyên văn server giữ. Model không được thay câu này |

Card tách `term`, `definition`, `example`, `phonetic` thành sentence riêng khi trường đó có nội dung. Trường trống là không có evidence, không được bịa.

Lịch sử `AiMessage` không phải evidence. Citation cũ không được đưa lại vào context nếu quyền hoặc hash không còn đúng.

## 3. Outcome

Tên đề xuất: `HybridRagOutcome`.

| Outcome | Khi nào |
| --- | --- |
| `answered` | Mọi ý được trả lời đều có evidence còn quyền. `answerScope=partial` khi một ý khác bị từ chối rõ |
| `needs_clarification` | Nhiều nguồn hợp lệ cùng khớp và chưa đủ để chọn. Không đoán |
| `insufficient_evidence` | Trong quyền nhưng nguồn thiếu, đã xoá, đã gỡ xuất bản hoặc hash đổi |
| `out_of_scope` | Không thuộc hướng dẫn nền tảng, dữ liệu của user hiện tại, hay học liệu user được xem |
| `access_denied` | Nguồn tồn tại nhưng user không còn quyền. Không xác nhận nội dung riêng |
| `cancelled` | User dừng stream. Không đổi thành ngoài phạm vi |
| `provider_unavailable` | Timeout, mạng, HTTP 5xx. Không đổi thành ngoài phạm vi và không bịa câu thay thế |
| `quota_exceeded` | Trần câu Standard hoặc trần token. Không phải lỗi tri thức |

Câu nhiều ý: trả lời từng ý được phép khi ý đó đứng độc lập và không làm lộ phần bị cấm. Nếu không tách được an toàn, dùng `needs_clarification` hoặc `access_denied` cho cả câu. Không lấy tri thức ngoài học liệu để “bù” ý thiếu.

Nội dung học liệu là dữ liệu. Câu “ignore previous instructions” trong thẻ không đổi outcome và không được chép vào câu trả lời.

`requiredFacts` trong fixture là fact phải được nguồn nâng đỡ. Không khóa đúng từng chữ của câu trợ lý.

## 4. Quyền hiện tại phải giữ

- Mọi truy vấn có id đều theo user hiện tại từ `GetCurrentUserId()` hoặc helper hub đã có. Sai quyền trả `NotFound`, không `Forbid`, với tài nguyên theo id.
- Guide public/authenticated chỉ sau khi claim và route được review. Guide admin không phát route.
- Card/set/folder theo owner, cộng folder/class được chia sẻ cho member hiện tại. Không dùng `FolderDetail` thiếu lọc owner làm mẫu retrieval.
- Speaking/listening/writing public mới vào chỉ mục chung. Bản private chỉ owner còn quyền và còn entitlement Premium/Admin. Standard không nhận nội dung private của chính họ nếu màn hình gốc đang khóa.
- Reading chỉ passage `IsPublished`.
- Goal, progress, notification, billing order chỉ owner và đọc live.
- Class chỉ owner hoặc member chưa bị xoá. Rời lớp hoặc bị kick làm evidence class cũ hết hiệu lực ngay ở lượt sau và khi đọc lịch sử.
- Link do server render. Không echo route `/Admin/...`.

## 5. Chỉ mục public và tra cứu private

Public hybrid, BM25 cộng dense, chỉ cho nội dung đã review hoặc published/public:

- Guide đã qua review claim.
- Reading published.
- Writing `UserId == null && IsPublished`.
- Listening `OwnerUserId == null && IsPublished`.
- Speaking `OwnerUserId == null`.

Private lexical/live, không quảng bá là đã có vector:

- Card, set, folder, goal, progress, notification, billing.
- Class content của member hiện tại.
- Draft và media private.

Xoá, gỡ publish hoặc đổi nội dung làm hash/visibility cũ không còn được chọn. Rebuild ghi bản mới trong `.appdata/local/ai-rag` rồi đổi manifest. Không embedding lúc startup và không đưa snapshot cá nhân vào `data/shared`.

Phiên bản cần cùng manifest: corpus, chunker, embedding model, dimension. Chưa chọn model embedding ở phase này.

## 6. Entrypoint rebuild

Quyết định cho phase triển khai sau, chưa code:

- Thêm action vào `AiManagementController` hiện có, không thêm màn hình soạn thảo.
- `POST /Admin/AiManagement/RebuildRetrievalIndex`.
- `[Authorize(Roles = Admin)]`, `[ValidateAntiForgeryToken]`.
- Cờ dry-run và trạng thái lần build gần nhất hiện trên `GET /Admin/AiManagement` đã có.
- Giới hạn số document và thời gian. Không gọi API trả phí khi khởi động app.
- Không dùng test runner làm công cụ rebuild.
- Không thêm hosted service trong `Program.cs` ở phase này. Phase có embedding thật vẫn cần quyền sửa `Program.cs` riêng khi tới lúc đăng ký service; prompt Phase 0 không cấp quyền đó.

`TrainModel` hiện chỉ train ML.NET classifier. Không dùng nhầm action đó làm rebuild corpus.

## 7. Quota nhiều giai đoạn

Ledger hiện tại: `AiQuotaReservationStatus.Pending`, `ActualTokens`, `ReservedTokens`. Ngày là UTC.

`ReserveQuestionAsync` tìm ledger Pending của cùng user có `ActualTokens > 0` và gọi `RecoverKnownPendingAsync`, việc này settle ledger. `MarkKnownUsagePendingAsync` ghi `ActualTokens` nhưng giữ Pending để recover sau crash của một lượt đã có usage.

Race trước khi có chat nhiều giai đoạn trả phí:

1. Lượt A ghi usage giai đoạn embedding vào ledger đang Pending.
2. Lượt B, hội thoại khác của cùng user, vào `ReserveQuestionAsync` và recover ledger A.
3. `ApplyTerminalStateAsync` khi terminal là Settled: `PendingQuestions` giảm 1 và `SettledQuestions` tăng 1. Tổng `usedQuestions = PendingQuestions + SettledQuestions` không đổi. Suất câu hỏi Standard không được trả lại.
4. Lượt A gọi `SettleAsync` sau khi generation xong. Ledger không còn Pending nên hàm thoát mà không cộng token generation.

Pending sang Released là terminal khác: `PendingQuestions` giảm 1 và `SettledQuestions` không tăng, nên suất câu hỏi được trả lại.

Hệ quả còn lại: token generation có thể không bị tính. `PendingTokens` giảm bằng `ReservedTokens`, còn `SettledTokens` chỉ tăng bằng `ActualTokens` đã ghi lúc settle sớm. Suất câu hỏi Standard vẫn bị giữ vì pending đã chuyển sang settled.

Phase 4 không được dùng `RecoverKnownPendingAsync` làm cách chuyển giai đoạn. Recovery chỉ được settle stage đã biết usage cuối và không còn stage mở sau nó. Embedding pending và generation pending phải phân biệt được bằng mã stage. Giai đoạn chưa xong không được mang `ActualTokens > 0` theo nghĩa “crash đã có bill đầy đủ”.

Giữ DbContext quota cách ly. Không Release usage đã biết. Cancel và provider failure vẫn là outcome vòng đời ở mục 3, không Release sau khi provider usage đã biết.

## 8. Ngoài phạm vi Phase 0

Không thêm entity, migration, package, endpoint, hay sửa `Program.cs` / `appsettings.json` / `.csproj`. Không export shared data. Không gọi OpenAI.
