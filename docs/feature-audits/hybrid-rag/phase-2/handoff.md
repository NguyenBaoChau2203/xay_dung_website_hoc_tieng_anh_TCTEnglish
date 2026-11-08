# Phase 2 — handoff corpus và BM25

Ngày 2026-11-06. Cursor Auto thực hiện `.ai/prompts/hybrid-rag/phase-2-corpus-bm25.md` và `docs/superpowers/plans/hybrid-rag/phase-2-corpus-bm25.md`. Kết quả là **AWAITING_REVIEW**. Phase 3–7 giữ LOCKED. Agent không tự duyệt phase này.

## Danh tính và phạm vi

- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Chưa commit.
- Trước khi sửa, fingerprint Phase 1 `764d370dc1b2482cd1b78954a8959dfd4706b16b5156ed00659a9301b729e7a9` khớp 48/48 file. Phase 1 APPROVED và Phase 2 READY theo review-state.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json), **60 entry**, fingerprint `ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a`.
- So với Phase 1, chỉ `TCTEnglish/Program.cs` và `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` đổi hash. Phần mới là corpus BM25, trang rebuild admin và `AiRagCorpusBm25Tests`.
- Grant DI hẹp đã có được dùng để đăng ký store/corpus/lexical retrieval. `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không sửa appsettings, `.csproj`, model, migration, shared data, không gọi API trả phí, không commit/push/PR/CI/deploy.
- CodeGraph MCP không có trong catalog phiên này. Kết luận lấy từ source publication/access và ma trận Phase 0.

## Hành vi

Chỉ mục chung chỉ chứa guide đã review và học liệu đủ điều kiện:

- Guide: 39 mục trong `website-guides.json`. Loại `study-read` vì claim chế độ Read không có trong source, và `ai-chatbox-usage` vì claim trợ lý đã trả lời từ dữ liệu người học chưa được ma trận Phase 0 xác nhận. Route `/Admin` bị loại.
- Reading: `IsPublished`, người xem đã đăng nhập, route `/Reading/Study/{id}`.
- Writing: `UserId == null && IsPublished`, route practice yêu cầu đăng nhập.
- Listening: `OwnerUserId == null && IsPublished`, catalog/practice công khai.
- Speaking: video `OwnerUserId == null` và `ImportStatus` ready; mỗi câu là `speaking-sentence:{id}`. Bản nháp và video của user không vào snapshot.

Card, user, chat, payment, recording, upload, goal, notification, billing và class không được đưa vào snapshot. Câu tra thẻ và fact cá nhân vẫn đi đường live Phase 1.

BM25 là Okapi theo field title/keyword/body, k1=1.2, b=0.75, trọng số 1.5/1.2/1.0. Đây là giả thuyết ban đầu, không chỉnh trên held-out. Chuẩn hóa chỉ dùng cho đối sánh: gấp dấu tiếng Việt và giữ cụm từ khóa. Text hiển thị giữ nguyên. Token xấp xỉ là độ dài/4, không phải tokenizer của model.

Rebuild là `POST /Admin/AiManagement/RebuildRetrievalIndex`, role Admin, antiforgery, có dry-run. `GET` chỉ đọc trạng thái. Không có hosted service và không build lúc startup/chat. Snapshot nằm ở `{contentRoot}/.appdata/local/ai-rag`, ngoài wwwroot, bị gitignore. Manifest chỉ đổi sau khi checksum và schema hợp lệ. Lỗi ghi, hủy, checksum sai hoặc build khác giữ bản manifest hợp lệ trước đó.

Trước khi evidence rời server, nguồn được đọc lại. Gỡ publish, đổi nội dung hoặc user không còn tồn tại thì evidence cũ không được trả. Chỉ mục thiếu hoặc hỏng với câu hướng dẫn website trả thông báo checksum riêng, không dùng câu ngoài phạm vi. Câu giá vàng vẫn ngoài phạm vi.

## Kiểm tra

| Kiểm tra | Lệnh | Kết quả |
| --- | --- | --- |
| Corpus/BM25/snapshot/chat | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2Corpus --filter FullyQualifiedName~AiRagCorpusBm25Tests` | **7 pass**, 0 fail/skip |
| Hồi quy Phase 1 bị ảnh hưởng | filter boundary, phase 2/4 AI, cancel, quota, closeout, OpenAI consumer, baseline, website guide, study recommendation, cùng configuration | **148 pass**, 0 fail/skip, khoảng 25 s |
| Baseline development | cùng test `DevelopmentGuideBaseline_DoesNotScoreHeldOutCases` | 17 câu guide development, top-1 **15/17**, held-out scored **0**. Corpus `lexical-v1`, chunker `paragraph-sentence-v1`, normalizer `vi-en-fold-v1`. 39 document, 86313 byte, build khoảng 460 ms, query khoảng 205 ms, RAM ước lượng khoảng 5.7 MB |
| Schema | không đổi entity/DbContext | không có migration |
| Browser admin | không chạy | chưa bấm dry-run trên trình duyệt |

Hai miss top-1, không chỉnh trọng số: `HR-NAV-D04` (“How do I change my password?”) ra `guide:study-flashcard` thay vì `guide:change-password`; `HR-NAV-D12` (“Daily challenge ở đâu trên dashboard?”) ra `guide:daily-challenge` thay vì `guide:view-streak`. Câu held-out không được chấm.

## Giới hạn

Đây là retrieval từ vựng, chưa phải Hybrid RAG. Chưa có embedding, dense retrieval, câu trả lời model tự do, citation bền hay UI chat mới. Trần nguồn là 2000 dòng mỗi loại. Snapshot cũ không tự xóa. Trang admin chưa được kiểm trên browser. Không chạy MariaDB disposable vì không có thay đổi schema. Không có CI hay production deploy.

## Việc Codex cần xem

Đường rebuild và containment, recheck sau khi gỡ publish, loại guide chưa review, và việc boundary Phase 1 không bị corpus thay thế khi đã có fact live. Manifest 60 entry ở trên là snapshot cần đối chiếu.

Phase 2 đang **AWAITING_REVIEW**. Không mở Phase 3.
