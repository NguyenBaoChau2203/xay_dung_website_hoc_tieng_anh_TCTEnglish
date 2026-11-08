# Hybrid RAG — kế hoạch theo phase cho Cursor Auto

Ngày lập: **06/11/2026**. Demo: **25/11/2026**, local trước.
Nhánh được kiểm tra khi lập bộ tài liệu: `feature/hybrid-rag-chatbot`, HEAD `6af768b`.
Tiến độ cập nhật 07/11/2026: **Phase 0–4 đã APPROVED; Phase 5 AWAITING_REVIEW**,
Phase 6–7 LOCKED. Sửa vòng 2 đã khép phần còn của F02/F05 và F06, đang chờ Codex review;
[bàn giao sửa vòng 2](../../../feature-audits/hybrid-rag/phase-5/fix-round-2-handoff.md).
[Báo cáo đóng Phase 4](../../../feature-audits/hybrid-rag/phase-4/review-round-2.md).
Trạng thái gate chính thức nằm tại [review-state.md](review-state.md).

## Cách dùng

1. Bắt đầu **Phase 0**. Mở prompt tương ứng trong Cursor Auto rồi yêu cầu thực hiện
   đúng một phase. Prompt và checklist thực thi dùng tiếng Anh; báo cáo cho bạn dùng
   tiếng Việt theo quy định dự án.
2. Cursor làm code/tài liệu và kiểm tra trong phạm vi phase, tạo bàn giao, rồi dừng
   ở `AWAITING_REVIEW`. Không tự làm phase tiếp theo, không tự duyệt kết quả.
3. Gửi lại chat này số phase, báo cáo bàn giao và những thay đổi trong cùng checkout.
   Mình review source/diff, regression và bằng chứng; báo cáo của Cursor là đầu vào,
   không thay thế kiểm chứng. Nếu dùng checkout khác, gửi cả diff và file mới đã làm
   sạch thông tin nhạy cảm; không chỉ gửi vài ảnh hoặc phần tóm tắt.
4. Nếu có lỗi trong phạm vi: mình tạo **plan fix + prompt fix riêng cho đúng lỗi và
   đúng vòng review**, bạn chạy Cursor Auto sửa rồi gửi lại. Không chuyển phase.
5. Chỉ khi review ghi `APPROVED`, phase kế tiếp mới được `READY`. Bạn chủ động khởi
   chạy prompt kế tiếp; lần chạy Cursor trước không được tự tiếp tục cả chuỗi.

Đây là điểm dừng do **owner yêu cầu ngày 06/11**, không phải yêu cầu xin lại quyền
cho từng thao tác nhỏ bên trong phase. Khi đã được phép thực thi một phase, Cursor
hoàn thành mọi việc cần thiết trong phạm vi đó, chỉ hỏi khi thiếu dữ kiện/quyền thật.

## Danh mục

| Phase | Kết quả cần đạt | Plan | Prompt Cursor Auto | Dự kiến |
| --- | --- | --- | --- | --- |
| 0 | Chốt nguồn, quyền, hợp đồng và baseline 120 ca | [Plan 0](phase-0-discovery.md) | [Prompt 0](../../../../.ai/prompts/hybrid-rag/phase-0-discovery.md) | 06–07/11 |
| 1 | Đường trả lời riêng cho chat; sửa quyền và tra thẻ | [Plan 1](phase-1-chat-boundary-access.md) | [Prompt 1](../../../../.ai/prompts/hybrid-rag/phase-1-chat-boundary-access.md) | 08–09/11 |
| 2 | Corpus, provenance, chỉ mục BM25 và rebuild an toàn | [Plan 2](phase-2-corpus-bm25.md) | [Prompt 2](../../../../.ai/prompts/hybrid-rag/phase-2-corpus-bm25.md) | 10–11/11 |
| 3 | Embedding, vector, RRF và so sánh ba cách tìm | [Plan 3](phase-3-hybrid-retrieval.md) | [Prompt 3](../../../../.ai/prompts/hybrid-rag/phase-3-hybrid-retrieval.md) | 12–14/11 |
| 4 | Grounding nghiêm ngặt, hội thoại, ngân sách và cancel | [Plan 4](phase-4-grounded-answers-budget.md) | [Prompt 4](../../../../.ai/prompts/hybrid-rag/phase-4-grounded-answers-budget.md) | 15–17/11 |
| 5 | Lưu nguồn, migration, lịch sử và thu hồi quyền | [Plan 5](phase-5-durable-citations.md) | [Prompt 5](../../../../.ai/prompts/hybrid-rag/phase-5-durable-citations.md) | 18–19/11 |
| 6 | Giao diện nguồn, cache, log và browser local | [Plan 6](phase-6-ui-resilience.md) | [Prompt 6](../../../../.ai/prompts/hybrid-rag/phase-6-ui-resilience.md) | 20–21/11 |
| 7 | Eval cuối, chạy live có giới hạn, diễn tập và freeze | [Plan 7](phase-7-demo-acceptance.md) | [Prompt 7](../../../../.ai/prompts/hybrid-rag/phase-7-demo-acceptance.md) | 22–24/11 |

Các khoảng ngày là dự kiến, gồm cả thời gian review/sửa; không được bỏ nghiệm thu
để kịp lịch. Các phase đã đạt được ghi trong review-state. Nếu bị chậm, điều chỉnh phạm vi rõ ràng thay vì
đánh dấu pass cho kiểm tra chưa chạy.

## Tài liệu điều phối

- [Trạng thái duy nhất của các phase](review-state.md).
- [Quy tắc review, sửa và mở phase](review-gates.md).
- [Mẫu bàn giao và bằng chứng](handoff-template.md).
- [Prompt để Codex review](../../../../.ai/prompts/hybrid-rag/codex-phase-review.md).
- [Mẫu plan fix](fix-plan-template.md) và [mẫu prompt fix](../../../../.ai/prompts/hybrid-rag/cursor-auto-fix-template.md).
- [Nghiên cứu nền](../../../feature-audits/2026-11-05-hybrid-rag-research.md).

Không tạo trước báo cáo review/fix chứa lỗi giả. Các file thật sẽ được sinh khi có
kết quả Cursor và phát hiện cụ thể. Mẫu không phải bằng chứng đã review.

## Phạm vi quyền

Lượt chat hiện tại chỉ tạo bộ kế hoạch/prompt. Khởi chạy phase triển khai là một
chỉ dẫn sau đó của owner. `Program.cs` vẫn cần quyền rõ cho DI RAG; quyền đã cấp
sẽ được giữ xuyên các phase trong phạm vi được cấp. Không tự sửa `appsettings.json`
hoặc `.csproj`. Prompt không tự cấp quyền commit/push/PR/CI/merge/deploy hay gọi API
trả phí. Các file chuẩn bị có ghi rõ phần nào cần owner cho phép khi thực sự dùng.

Không cần gửi key trong chat. Dùng kho local đã lưu theo AGENTS.md. Local vẫn cần
mạng cho embedding/generation thật; fallback BM25/template có trạng thái suy giảm
rõ, không được gọi là nghiệm thu Hybrid RAG đầy đủ offline.

## Tin nhắn bắt đầu Phase 0 trong Cursor Auto

```text
Read and execute .ai/prompts/hybrid-rag/phase-0-discovery.md only. Follow the review gate, finish the Phase 0 handoff in Vietnamese, and stop at AWAITING_REVIEW. Do not start Phase 1.
```

## Tin nhắn gửi lại Codex sau mỗi phase

> Cursor Auto đã xong Phase N. Hãy review theo
> `.ai/prompts/hybrid-rag/codex-phase-review.md`, kiểm tra code và bằng chứng.
> Nếu chưa đạt, tạo plan fix và prompt fix cho các lỗi thật rồi giữ khóa phase
> kế tiếp. Nếu đạt, cập nhật review-state và cho phép tôi bắt đầu phase kế tiếp.

Thay `N` bằng số phase thực tế. Nếu Cursor còn báo thiếu quyền/kiểm tra chưa chạy,
gửi nguyên phần đó; không coi báo cáo “xong” là đã qua review.
