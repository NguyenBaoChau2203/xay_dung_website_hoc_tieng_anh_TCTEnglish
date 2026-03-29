# AI Chatbot Phase Execution Checklists

Checklist này phản ánh implementation hiện có trong repo sau vòng hardening
follow-up. Trạng thái dùng 3 mức:

- `Done`: đã có trên code + có baseline verification phù hợp
- `Partial`: đã có nền tảng nhưng chưa đủ để coi là hoàn tất production-ready
- `Todo`: chưa làm

## Phase 1 - Data Layer + Context Engine

Status: `Done`

- [x] Có `AiConversation`
- [x] Có `AiMessage`
- [x] Có `AiRequestLog`
- [x] Có EF mapping trong `DbflashcardContext`
- [x] Có `IAiContextBuilder` + `AiContextBuilder`
- [x] Có `IAiTokenCounter` + `SimpleAiTokenCounter`
- [x] Có test cho context builder
- [x] Có test ownership cho conversation service

Ghi chú:

- Token counting hiện vẫn là heuristic, nhưng phase data/context cơ bản đã có.

## Phase 2 - Provider Client + AiChatService

Status: `Done`

- [x] Có `IAiProviderClient`
- [x] Có `OpenAiProviderClient`
- [x] Có `IAiChatService`
- [x] Có `AiChatService`
- [x] `AiChatService` lưu user message + assistant message
- [x] Có request log khi success/failure
- [x] Provider selection đi qua `AiOptions.Provider`
- [x] Không còn self-`new` provider trong controller/hub
- [x] Có service tests cho happy path / ownership / provider failure
- [x] Có concurrent guard trên cùng conversation

Ghi chú:

- `Azure OpenAI` chưa implement, nhưng DI/provider resolution đã có chỗ trung tâm.

## Phase 3 - MVC Endpoint + UI MVP

Status: `Done`

- [x] Dùng MVC controller thay vì Razor Pages
- [x] Có `AiController`
- [x] Có `Views/Ai/Chat.cshtml`
- [x] Có AJAX send endpoint `/AI/Chat/Send`
- [x] Có observability endpoint `/AI/Observability`
- [x] Frontend dùng `fetch` + antiforgery token
- [x] Controller dùng `BaseController.GetCurrentUserId()`

Không còn đúng:

- [ ] `Pages/AI/Chat.cshtml`
- [ ] `Chat.cshtml.cs`
- [ ] `?handler=Send`

## Phase 4 - Security + Hardening

Status: `Done` cho baseline, `Partial` cho rollout rộng hơn

Đã done:

- [x] `[Authorize]` cho AI chat
- [x] antiforgery cho POST send
- [x] ownership check cho conversation
- [x] request rate limiting abstraction qua DI
- [x] daily token budget
- [x] provider failure -> `503`
- [x] concurrent request guard -> `409`
- [x] observability snapshot per user
- [x] integration tests cho auth / anti-IDOR / provider failure

Vẫn partial:

- [ ] business timezone cho daily budget
- [ ] retention policy cho `AiConversation` / `AiMessage` / `AiRequestLog`
- [ ] distributed rate limiting / scale-out safe throttling

## Phase 5 - SignalR Streaming

Status: `Partial`

Đã có:

- [x] reuse `ClassChatHub` cho AI stream events
- [x] `AiStreamStarted`
- [x] `AiStreamChunk`
- [x] `AiStreamCompleted`
- [x] `AiStreamFailed`
- [x] nút stop gọi `StopAiStream`
- [x] fallback từ realtime sang AJAX
- [x] orchestration stream được tách khỏi hub qua `IAiStreamingService`

Chưa done:

- [ ] token streaming end-to-end từ provider
- [ ] reconnect/resume semantics thật sự cho stream đang dở
- [ ] stream-specific automated tests cho cancel/reconnect/fallback matrix

Lưu ý:

- Hiện tại là simulated streaming, không phải real provider streaming.

## Kiểm tra release tối thiểu

Trước khi gọi AI chat là “phase ổn định”, cần giữ các mục sau ở trạng thái xanh:

- [x] AI service wiring đi qua DI
- [x] Controller/hub không tự dựng object graph AI
- [x] Ownership check + antiforgery hoạt động
- [x] AI tests baseline pass:
  - `AiContextBuilderTests`
  - `AiConversationServiceTests`
  - `AiChatServiceTests`
  - `AiPhase4HardeningIntegrationTests`
- [x] Streaming docs mô tả đúng là simulated

Vẫn cần follow-up thêm trước rollout rộng hơn:

- [ ] tokenizer thật
- [ ] distributed limiter
- [ ] business timezone
- [ ] retention/privacy runbook
