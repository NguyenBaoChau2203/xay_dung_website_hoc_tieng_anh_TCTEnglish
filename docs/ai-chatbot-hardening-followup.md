# AI Chatbot Hardening And Optimization Follow-up

Tài liệu này tổng hợp các việc cần làm tiếp sau đợt triển khai AI chatbot và
đánh dấu phần nào đã được xử lý trong vòng hardening hiện tại.

## Mục tiêu

1. Đồng bộ tài liệu với kiến trúc MVC thực tế của repo
2. Cứng hóa AI chat về DI, test baseline và runtime safety
3. Tách rõ phần nào là blocker nền tảng, phần nào là optimization về sau

## Trạng thái sau vòng triển khai này

### Đã xử lý

- Đồng bộ docs AI sang MVC controller + Razor view thay vì Razor Pages
- Dọn phần AI mới về namespace `TCTEnglish.*`
- Đưa AI services về DI trong `Program.cs`
- Bỏ self-`new` service/provider trong `AiController`
- Bỏ self-`new` service/provider trong `ClassChatHub`
- Thêm provider resolution tập trung bằng `AiOptions.Provider`
- Thêm `IAiStreamingService` để hub gọn hơn
- Thêm `IAiRequestRateLimiter` injectable
- Thêm concurrent guard trên cùng conversation
- Sửa baseline AI tests để build/pass trở lại
- Sửa `TestWebApplicationFactory` để test override service không cần subclass riêng
- Sửa các chuỗi tiếng Việt lỗi encoding ở AI view / JS / hub

### Đã có nhưng vẫn chỉ ở mức partial

- Token counting vẫn là heuristic (`SimpleAiTokenCounter`)
- SignalR streaming vẫn là simulated streaming
- Rate limiting vẫn là in-memory cục bộ
- Daily budget vẫn dùng UTC day boundary

### Chưa xử lý trong vòng này

- Tokenizer thật
- Distributed rate limiter hoặc middleware/policy production-grade
- Business timezone abstraction cho AI budget
- Retention policy cho `AiConversation` / `AiMessage` / `AiRequestLog`
- Real provider streaming end-to-end
- Streaming-specific reconnect/cancel/fallback automated test matrix

## Phân nhóm ưu tiên cập nhật

### Priority 0 - Baseline nền tảng

Status: `Done`

- Docs phản ánh đúng MVC boundary
- AI tests baseline pass
- Namespace AI mới dùng `TCTEnglish.*`

### Priority 1 - Service / DI hardening

Status: `Done`

- Controller/hub inject abstraction thay vì tự tạo object graph
- Provider resolution đi qua DI + config
- Streaming orchestration tách khỏi hub

### Priority 2 - Governance / cost control

Status: `Partial`

- Có request budget, daily budget, request log
- Chưa có tokenizer thật
- Chưa có business timezone

### Priority 3 - Runtime hardening

Status: `Partial`

- Có concurrent guard trên cùng conversation
- Có request rate limiter injectable
- Chưa có distributed limiter
- Chưa có retention policy

### Priority 4 - Streaming

Status: `Partial`

- Có SignalR flow, stop, fallback, chunk replay
- Chưa có real token streaming

### Priority 5 - Frontend / UX

Status: `Partial`

- Có state machine cơ bản `idle/sending/error/done`
- Có fallback stream -> AJAX
- Có sửa message lỗi rõ hơn cho `409` / `429` / `503`
- Chưa có conversation list/history panel
- CDN markdown libs vẫn còn là follow-up riêng

### Priority 6 - Observability

Status: `Partial`

- Có snapshot per-user
- Có request success/error logging
- Chưa tách metric vận hành đầy đủ cho admin/alerting

## Đề xuất thứ tự tiếp theo

### Sprint kế tiếp

- Thay `SimpleAiTokenCounter` bằng tokenizer thật
- Chuyển AI budget sang business timezone abstraction
- Quyết định retention policy cho conversation/log

### Sprint sau đó

- Chọn chiến lược rate limit production-friendly
- Nếu product thực sự cần, triển khai real streaming thay cho simulated replay
- Bổ sung streaming test matrix cho cancel/reconnect/fallback

## Definition Of Done cho vòng hardening này

Chỉ nên coi vòng hardening nền tảng là đạt khi:

- docs AI khớp với MVC hiện tại
- AI tests baseline pass
- controller/hub không còn tự dựng AI object graph
- provider selection đi qua config + DI
- concurrent request trên cùng conversation được chặn rõ ràng
- streaming được mô tả đúng là simulated streaming
