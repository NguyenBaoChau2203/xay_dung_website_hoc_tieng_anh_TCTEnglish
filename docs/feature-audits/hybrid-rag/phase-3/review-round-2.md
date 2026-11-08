# Phase 3 — Codex review vòng 2 và closeout

Hoàn thiện ngày 07/11/2026, sau lượt sửa bắt đầu 06/11. **Phase 3 APPROVED;
Phase 4 READY; Phase 5–7 LOCKED.** Các kiểm tra bắt buộc trong phạm vi local đã đạt.
Owner yêu cầu Codex kiểm lại và sửa trực tiếp phần còn lỗi để chuyển Phase 4.
Một agent thực hiện toàn bộ; không tự triển khai Phase 4.

## Snapshot và quyền

- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- Manifest trả về khớp **86/86 file**, fingerprint
  `408779fa6c5d9139fa40d8021693c4b76fa6a3b860bbf1478804b368cec9e720`.
- Plan closeout: [phase-3-closeout.md](../../../superpowers/plans/hybrid-rag/phase-3-closeout.md).
- Lượt này sửa `EmbeddingOperationBudget`, `IOpenAiEmbeddingClient`, `OpenAiEmbeddingClient`,
  `VectorIndexService` và thêm `AiRagPhase3CloseoutTests`. Không đổi Program.cs,
  appsettings/.csproj, schema, nguồn học liệu, shared package hoặc model.
- Quyền sửa trực tiếp đến từ yêu cầu owner mới, thay review-only của vòng 1.
  Không có quyền API trả phí/remote Git/CI/release được thêm.

## Kết quả review từng finding

| Finding | Kiểm chứng và kết quả |
| --- | --- |
| HR-P3-R1-F01 | Bản Cursor đã sửa đúng: một budget request không gửi retry thứ hai; bộ regression production HTTP fake tiếp tục đạt |
| HR-P3-R1-F02 | Còn lỗi ở nhánh lỗi response/cancel/unknown spend và cache I/O; Codex đã tái hiện đỏ rồi sửa trực tiếp, xem dưới |
| HR-P3-R1-F03 | Shared admission và scoped DbContext đúng; hai request scope không chồng, scope sau busy rồi tái sử dụng cache |
| HR-P3-R1-F04 | Recheck trước batch/retry/promotion giữ publication/hash/snapshot; các ca unpublish, private transition, hash và snapshot thay đổi đều đạt |
| HR-P3-R2-F05 | P2 mới, đã sửa: tổng safe byte bound vượt trần một job khiến corpus từ cache rỗng không bao giờ tiến tới vector index dù từng chunk đều phù hợp |

### F02 còn sót trên bản trả về

Đã viết regression trước implementation:

- Response có usage hợp lệ nhưng vector sai chiều: parse vector thất bại trước
  khi ghi usage, nên lượng đã biết bị mất.
- JSON hỏng, caller cancel hoặc timeout sau send: ledger có attempt nhưng còn
  báo usage hoàn chỉnh/0; không thể coi response bị mất là đã biết không tốn token.
- Network/response I/O thất bại rồi retry: upper bound cho attempt chưa rõ chi phí
  không được giữ lại; retry hoặc batch tiếp có thể dùng lại toàn bộ token allowance.
- Cache write lỗi sau response thành công: exception thoát khỏi service, mất
  báo cáo attempt/usage của job ở ranh giới controller.
- Thiếu key trước HTTP: service suy diễn thêm một attempt/usage unknown dù client
  chưa gửi gì. Regression riêng xác nhận phải giữ 0 request, 0 usage hoàn chỉnh.

Nhóm đầu gồm **7 test đỏ**, trong đó có regression capacity F05. Cache I/O có
**1 test đỏ riêng**, rồi ca preflight thêm **1 test đỏ** nữa. Tổng cộng **9 regression**.
Dữ liệu và usage là synthetic; không có phép đo API thật.

Sửa: giữ upper bound của attempt đang gửi, chuyển thành dự phòng chưa xác định
khi thiếu receipt; lượng đó tiếp tục chiếm budget qua retry/batch sau. Receipt
hợp lệ được ghi trước validation vector và trước kiểm cancel sau khi đọc đủ body.
Mọi failure/cancel đánh dấu đúng phần còn chưa biết. Service trả kết quả thất bại
có usage đã biết khi cache/index I/O lỗi; lease luôn được thả.
Default interface adapter sở hữu ledger cho các implementation một lần gọi;
production client tự quản lý từng HTTP attempt. Service không suy diễn thêm send
từ exception, nên lỗi cấu hình trước HTTP được báo chính xác là 0 request.

### F05 — khả năng tiến triển của corpus lớn

Dry-run hiện tại có **487 chunk**, estimate **27.610 token**, safe UTF-8 byte
bound **113.887**. Trước closeout, kiểm trần tổng ở đầu job từ chối trước mọi send
khi vượt 40.000; vì cache vẫn rỗng, chạy lại cũng không thể tiến triển. Đây là lỗi
khả dụng của rebuild, không phải yêu cầu tăng ngân sách hay bật live API.

Sửa: chọn batch vừa allowance còn lại; khi đã làm được một phần và chạm trần,
trả **partial / budget-paused**, giữ cache hợp lệ và index đang dùng. Job rõ ràng
tiếp theo đọc lại nguồn/cache rồi tiếp tục. Chỉ promote khi tất cả chunk của
snapshot hợp lệ đã đủ. Một chunk tự nó vượt budget vẫn bị từ chối trước send.
Không tự khởi chạy job tiếp theo, không tăng cap 12 request/40.000 token.

Regression từ cache rỗng dùng ba nguồn, mỗi job tối đa 1 request/250 token. Hai
job đầu partial, không có index published; job thứ ba promoted đúng ba chunk,
không embed lại chunk đã có cache. Đây là HTTP/interface fake, không phải đo
chi phí hay chất lượng ngôn ngữ của model.

## Verification

- Build app/tests trong **Phase3Closeout** qua dotnet test thành công; compiler
  warnings đã có giữ nguyên.
- Bản Cursor trước closeout: **126/126** RAG/provider pass.
- Sau sửa: **135/135** RAG/provider pass, gồm đủ **9 regression closeout**.
- Toàn bộ suite bản cuối: **1.067 pass, 0 fail, 19 skip / 1.086 tổng**, khoảng
  4 phút 11 giây. 19 skip gồm live OpenAI/Speech, disposable MariaDB, upgrade và
  benchmark opt-in; không phải tất cả đều chỉ là MariaDB. Không coi skip là pass.
- Fixture evaluation/held-out không sửa hoặc tune. Chưa có semantic live;
  Phase 7 vẫn phải nghiệm thu phần đó bằng grant có giới hạn.
- Tooling validator exit **0**. Python **135 test: 134 pass / 1 skip**, đúng
  contract non-Windows trên Windows. `git diff --check` không báo lỗi.
- HTTP transport/token contract được đối chiếu với
  [OpenAI embeddings guide](https://developers.openai.com/api/docs/guides/embeddings)
  và [tokenization examples](https://developers.openai.com/cookbook/examples/how_to_count_tokens_with_tiktoken).
  UTF-8 bound là upper bound bảo thủ, không phải tokenizer thực hoặc chi phí thật.

Lệnh chính:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase3Closeout --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~OpenAiProviderClientTests'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase3Closeout --no-build --no-restore --logger 'trx;LogFileName=phase3-closeout-full.trx' --results-directory .tmp/hybrid-rag-review/phase-3/round-2
```

TRX đỏ/xanh và console nằm ở ignored
`.tmp/hybrid-rag-review/phase-3/round-2/`. Không ghi source riêng/secrets vào report.

## Browser và bảo toàn workload

Release build thử thêm qua canonical launcher ở cổng 5112 dừng vì apphost đang
bị process owner **19632** giữ. Build chẩn đoán xác nhận MSB3027/MSB3021 do file
lock, không phải lỗi compile. Không dừng process đó hoặc thay cấu hình để vượt
lock. Build/test bản code cuối đã dùng output Phase3Closeout riêng và đạt.

Chrome mở được phiên owner hiện có ở `127.0.0.1:5102`; kiểm native keyboard lại:

- Tab từ maxInputTokens đến Dry-run vector; Shift+Tab quay lại đúng ô token;
  Tab/Enter gửi đúng **một POST embed-dry-run**.
- Feedback: 487 chunk, estimate 27610, safeTokens 113887, estimatedRequests 8,
  **apiCalls=0 / usage=0**. Vector manifest không đổi/không được tạo.
- `/health/ready` **200** với ready. Viewport 390: nút xếp dọc, rộng 310 px,
  không tràn ngang. Không bấm Tạo vector; mọi embed thật bị chặn trong harness.
- Phiên browser này dùng runtime owner trước closeout. Razor/controller và
  DryRun/Plan không đổi trong closeout; nó xác nhận lại form/key behavior.
  Các sửa ledger/transport/bounded rebuild được chứng minh bằng binary test mới.

## Gate và giới hạn

Không còn finding chưa xử lý trong phạm vi Phase 3 đã kiểm. Codex ghi **Phase 3
APPROVED**, chỉ mở **Phase 4 READY**; Phase 5–7 vẫn LOCKED. Phase 4 cần owner
khởi chạy riêng. Không có migration/shared export vì không đổi persisted
model hoặc shared content. Không có API trả phí, CI, Git remote hay deployment.

## Danh tính snapshot được duyệt

[Manifest cuối](scope-manifest.json): **89/89 hash khớp**, fingerprint
`66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`. Branch/HEAD không đổi. Manifest, báo cáo và review-state
được loại khỏi digest theo protocol để tránh vòng hash. Snapshot trả về 86 entry
được giữ làm tiền nhiệm; approval này áp dụng các byte sau closeout.
