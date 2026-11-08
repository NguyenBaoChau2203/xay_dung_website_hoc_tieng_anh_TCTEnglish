# Phase 3 — Codex review vòng 1

Ngày 2026-11-06. **CHANGES_REQUESTED**. Có **4 finding P1** đã tái hiện trên
snapshot trả về. Phase 4–7 tiếp tục LOCKED. Lượt này REVIEW_ONLY với application
code; chỉ tạo tài liệu review/repair và cập nhật gate, không sửa implementation.

## Danh tính và phạm vi

- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- Manifest Phase 3 **81/81 entry khớp**, fingerprint tính lại
  `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`.
- So với Phase 2: sáu entry thay đổi và 11 entry mới đúng như handoff. Đọc source
  hiện tại qua CodeGraph, mở giới hạn các phần bị lược bỏ; kiểm transport, DI,
  operator/controller/view, identity, store, cosine/RRF, hybrid và assertions.
- Scope manifest trả về được giữ nguyên để lần sửa đối chiếu. Tài liệu review,
  plan/prompt mới và state không phải phê duyệt source này.

## HR-P3-R1-F01 — P1: retry vượt trần request đã nhập

Source: `VectorIndexService.BuildAsync`, lines **105–122**;
`OpenAiEmbeddingClient.EmbedAsync`, lines **45–62, 113–128**.

Probe dùng corpus một chunk, client embedding production và recording HTTP handler.
Budget `maxRequests=1`, handler trả 429 có Retry-After=0 rồi response hợp lệ:

| Quan sát | Kết quả |
| --- | --- |
| Trần operator | 1 request |
| HTTP POST thực sự qua handler | **2** |
| ApiCalls trả về | **1** |
| Build | **promoted** |

Root cause đã xác nhận: service đếm lời gọi `EmbedAsync`, còn client retry bên
trong mà không nhận phần ngân sách request còn lại. Vì vậy trần POST không được
enforce tại điểm gửi; báo cáo còn đếm thiếu. Không cần API thật để chứng minh.
Đây là lỗi ranh giới ngân sách Phase 3, chưa nói đến quota chat của Phase 4.

Repair: một ledger attempt cho toàn job, kiểm/tiêu thụ trước mỗi HTTP send,
bao gồm retry và failure; không retry khi hết request. Giữ deadline/cancel/error
mapping và báo đúng số attempt. Không giải quyết chỉ bằng đổi nhãn trên form.

## HR-P3-R1-F02 — P1: kiểm token sau chi phí và làm mất usage đã biết

Source: `VectorIndexService.BuildAsync`, lines **93–98, 130–136**;
`PlanAsync`, lines **284–297**; `EmbeddingBudget` và `EmbeddingBatch`.

Probe source synthetic tiếng Việt `Tôi học tiếng Việt mỗi ngày.`: dry-run ước
lượng **7** token; budget **10**; fake provider trả vector hợp lệ cùng
`prompt_tokens=total_tokens=20`. Service nhận response rồi trả:

```text
Status=failed, Message=usage-exceeded, ActualPromptTokens=0
```

Đây là usage do recording fake khai báo, không phải phép đo tokenizer hoặc chi
phí tài khoản thật. Hai lỗi được chứng minh: ước lượng ký tự/4 được dùng làm
điều kiện đủ để gửi trong hard ceiling; và usage hợp lệ đã nhận bị bỏ vì nhánh
so trần chạy trước phép cộng vào `actualTokens`. Kết quả 0 sai ngay cả khi phải
dừng job do provider trả usage vượt dự kiến.

Repair: tách estimate khỏi ceiling an toàn trước send, dùng tokenization được
kiểm chứng hoặc upper bound có lý do rõ, chọn batch theo ngân sách còn lại.
Luôn giữ toàn bộ usage đã biết trên cả failure/validation/promotion/cancel path;
outcome chưa biết phải ghi là chưa biết, không giả 0. Chỉ cần ledger/report của
maintenance job, không thêm quota DB hoặc bật paid chat thuộc Phase 4.

## HR-P3-R1-F03 — P1: admission lock không chia sẻ giữa request

Source: `Program.cs` line **168**, `VectorIndexService` line **47**, `BuildAsync`
line **80**. Store chỉ khóa giai đoạn commit ở `VectorIndexStore:234`.

Probe tạo hai instance service giống lifetime scoped đang đăng ký, mỗi instance
có DbContext SQLite riêng, cùng lexical snapshot và cùng singleton vector store.
Provider của job 1 được giữ bằng barrier. Job 2 vẫn vào provider **trước khi job
1 được thả**; cuối cùng hai job đều promoted, tổng hai lần gọi provider.

Root cause: mỗi request có `SemaphoreSlim` riêng. Khóa commit không bao phủ
plan/cache lookup/paid work, nên không thực hiện được quy tắc một build đang chạy
trên cùng index; frontend chống double-submit cũng không chặn hai tab/request.

Repair: lease/coordinator chia sẻ theo approved index root, bao phủ toàn rebuild,
có bounded wait/busy/cancel và release trên mọi đường thoát. Giữ service/DbContext
scoped; không đổi service thành singleton rồi capture scoped DbContext. Sau khi
nhận lease cần đọc lại plan/cache để tránh duplicate embedding từ kế hoạch cũ.
Grant DI RAG hẹp đã có cho phép registration coordinator cần thiết trong phạm vi đó.

## HR-P3-R1-F04 — P1: batch sau vẫn gửi nguồn đã bị gỡ publish

Source: `VectorIndexService.PlanAsync:258–262`, `BuildAsync:102–114, 150–155`.

Probe dùng **65** Reading published, mỗi nguồn một chunk. Batch đầu gồm 64
chunk; ngay trong fake provider của batch này, đổi toàn bộ 65 nguồn thành
unpublished và lưu SQLite. Sau đó:

```text
batchSizes=[64,1]
unpublishedSourcesWhenSecondBatchEntered=65
Status=promoted, ApiCalls=2, indexedChunks=65
```

Tức là payload của batch thứ hai vẫn rời ranh giới embedding sau khi nguồn đã
bị thu hồi. ID snapshot lexical không đổi, nên check cuối vẫn cho promote. Các
check live khi trả kết quả chat vẫn tồn tại; finding này chứng minh thiếu recheck
trước external call/promotion, không khẳng định chat đã trả dữ liệu riêng.

Repair: revalidate eligibility/publication/hash và snapshot trước từng batch/
attempt có thể gửi text, kể cả retry, và ngay trước promotion. Dừng trước send
khi nguồn đã bị thu hồi/đổi; giữ index hợp lệ trước đó và usage đã biết. Không
chỉ thêm check sau khi toàn bộ payload đã gửi xong; không tự rebuild corpus hay
bypass publication predicate.

## Verification và phần đã đạt

- Build app/tests trong configuration riêng `Phase3Review`: thành công, giữ các
  compiler warnings có sẵn; không đụng output Debug/Release đang dùng.
- Corpus/access/repair/embedding/cosine/RRF/vector/admin + text transport:
  **119 pass, 0 fail/skip**, khoảng 34 giây.
- Lifecycle/cancel/quota/closeout, non-chat consumers, deterministic/production
  classifier và fixture freeze: **84 pass, 0 fail/skip**, khoảng 48 giây.
- Probe độc lập bốn scenario ở ignored
  `.tmp/hybrid-rag-review/phase-3/round-1/Probe/`: dùng implementation thật của
  service/client và HTTP giả hoặc fake embeddings; SQLite synthetic và root
  riêng `probe-state`, không dùng dev DB. `behavior-probes.json` lưu kết quả.
- Đối chiếu request/response với [OpenAI embeddings API reference](https://developers.openai.com/api/reference/resources/embeddings/methods/create):
  model/input/dimensions/encoding và indexed vectors/usage đúng dạng. Các lỗi
  finding ở budget/lifetime/freshness, không phải yêu cầu đổi model.
- Ordinary chat và `IAiProviderClient` giữ đường Phase 2/text transport; vector
  phải được gọi rõ, không có activation paid chat trong diff.
- Không cần migration/shared export vì không đổi persisted model hoặc source
  content. Không có API trả phí, CI, commit/push/PR, staging hay production.
- Agent tooling validator **exit 0**; Python tooling suite **135 tổng,
  134 pass/1 skip**, khoảng 128 giây. Skip là contract non-Windows trên Windows;
  các staging/hosting check trong suite này là fake drills, không phải release thật.
- Kiểm cuối: `git diff --check`, liên kết review/plan/prompt/state, fingerprint
  và toàn bộ **81/81 entry giữ nguyên** đều đạt. Gate Phase 3 CHANGES_REQUESTED,
  Phase 4–7 LOCKED được đối chiếu lại. Không sửa application code/test/manifest.

Semantic quality/live account chưa được đo là giới hạn đã cho phép đến Phase 7,
không tự gán thành lỗi Phase 3. Không dùng vector synthetic làm số đo chất lượng.
Handoff đã ghi browser mouse/dry-run/responsive; Enter của form vector chưa được
xác nhận bằng native keyboard. Lượt review thấy không có listener local ở 5100,
không thay phiên web của owner để diễn lại. Repair cần hoàn tất bằng chứng bàn
phím bằng launcher local, chỉ Dry-run khi không có grant API thật. Đây là thiếu
evidence, không gán thành finding code thứ năm.

## Gate và tài liệu sửa

Phase 3 **CHANGES_REQUESTED** với F01–F04 mở, Phase 4–7 **LOCKED**. Phase 0–2 giữ
approval lịch sử. Reviewer không sửa application code hay tự thực thi repair.

- [Plan sửa vòng 1](../../../superpowers/plans/hybrid-rag/phase-3-fix-round-1.md).
- [Prompt thực thi Cursor](../../../../.ai/prompts/hybrid-rag/phase-3-fix-round-1.md).

Sau owner launch, Cursor sửa đúng vòng này, bổ sung regression đỏ/xanh, hoàn tất
evidence, cập nhật manifest/handoff và dừng AWAITING_REVIEW để Codex kiểm lại.
