# Phase 3 — handoff embedding, vector và RRF

Ngày 2026-11-06. Cursor thực hiện `.ai/prompts/hybrid-rag/phase-3-hybrid-retrieval.md` và `docs/superpowers/plans/hybrid-rag/phase-3-hybrid-retrieval.md`. Kết quả là **AWAITING_REVIEW**. Phase 4–7 giữ LOCKED. Agent không tự duyệt phase này.

## Danh tính và phạm vi

- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Chưa commit.
- Trước khi sửa, fingerprint Phase 2 `62a864787561df4164da82030b24b590564fdce804d097a49b66c4ddc5a4dbdf` khớp 70/70 file. Phase 2 APPROVED và Phase 3 READY theo review-state.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json), **81 entry**, fingerprint `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`. Fingerprint này là danh tính bàn giao, chưa phải phê duyệt.
- So với 70 entry Phase 2, sáu file đổi hash: `Program.cs`, `AiManagementController.cs`, `AiManagementViewModel.cs`, `Areas/Admin/Views/AiManagement/Index.cshtml`, `CorpusModels.cs`, `RetrievalCorpusService.cs`. Mười một file mới trong digest: client/options embedding, cosine, RRF, identity, vector store/service, hybrid retrieval, `AiProviderException.cs`, `AiRagHybridRetrievalTests.cs`.
- Grant DI hẹp đã có được dùng để đăng ký options, HttpClient `OpenAIEmbeddings`, client embedding, vector store/service và hybrid retrieval. `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không sửa `appsettings.json`, `.csproj`, model text, migration, shared data. Không gọi API trả phí. Không commit, push, PR, CI hay deploy.
- CodeGraph MCP không có trong phiên này. Hợp đồng embedding lấy từ schema OpenAPI tĩnh của `POST /v1/embeddings`: `model`, `input`, `encoding_format=float`, `dimensions`, response `object=list`, `data[].index`, `data[].embedding`, `usage.prompt_tokens` và `usage.total_tokens`. Không gửi `reasoning`, `temperature` hay id người dùng.

## Hành vi

Embedding dùng model `text-embedding-3-small`, 1536 chiều, client và options riêng. Key và BaseUrl vẫn theo thứ tự đã có của text client. BaseUrl kết thúc bằng `/responses` hoặc `/embeddings` được cắt hậu tố rồi nối `/embeddings`. Chỉ HTTPS, hoặc HTTP trên loopback. Không redirect, không query/fragment/userinfo. Deadline một lần bao gồm body, retry và backoff. Tối đa một retry. Log chỉ có status, mã lỗi và số lần gọi.

Response bị từ chối khi sai model, sai số chiều, thiếu hoặc trùng index, vector rỗng, chuẩn L2 bằng 0, NaN/Infinity, hoặc usage không khớp. 401/403 là authentication. 404 là model không dùng được, không đổi model. 429 `insufficient_quota` là hết quota. 429 khác được retry có hạn. 5xx là provider unavailable. JSON hỏng và redirect bị từ chối là lỗi riêng. Hủy từ caller không gửi request.

Chỉ `POST /Admin/AiManagement/RebuildRetrievalIndex` với operation `embed` mới được gọi embeddings, và chỉ trong ngân sách form `maxRequests` 1–12 cùng `maxInputTokens` 1–40000. `embed-dry-run` chỉ ước lượng. `dry-run` và `build` vẫn là lexical. Form vector tách khỏi form BM25. Không có ô đường dẫn. Field lạ bị bỏ qua. Role Admin và antiforgery bắt buộc. Một job embed được nhận trong 5 giây, quá hạn trả `busy`.

Cache và index nằm cạnh snapshot lexical, dưới `{contentRoot}/.appdata/local/ai-rag`, ngoài wwwroot. Khóa cache là hash của model, chiều, phiên bản chunker và hash đoạn cần embed. Snapshot lexical vẫn không chứa vector. Promote chỉ sau checksum. Lỗi ghi xóa bản mới và giữ manifest cũ. Cache của batch hợp lệ có thể còn để lần sau không gọi lại API.

Hybrid retrieval có ba mode: `hybrid`, `lexical_degraded`, `unavailable`. Chat thường gọi retrieve không bật embedding câu hỏi, nên trả `lexical_degraded` / `query-embedding-deferred` và không gọi API. `AllowsUnconstrainedGeneration` luôn false. RRF dùng k=60, trọng số bằng nhau, điểm là tổng `1/(60+hạng)`. Điểm này không phải xác suất đúng. Nguồn trùng giữ một đoạn. Evidence tối đa 3. Nguồn bị gỡ publish hoặc đổi hash thì không được embed và không được trả.

## Ví dụ đã kiểm

Fixture toán học, vector 3 chiều do test tạo, không phải output của model:

- Cosine query `[1,0,0]` xếp `a`, `c`, `b`. Hòa cosine thì khóa nguồn `reading:a` đứng trước `reading:b`.
- RRF lexical `a,b` và dense `b,c` ra `b,a,c`. Điểm đầu là `1/61 + 1/62`.
- Cùng nguồn `reading:same` chỉ còn một đoạn, chunk `s2`.
- Không chồng nhánh: `only-dense` rồi `only-lexical` vì hòa điểm và khóa `reading:a` đứng trước `reading:b`.

Fixture SQLite hai reading, vector giả 1536 chiều:

- Query `alpha marker` với trục lexical chọn `Alpha note`. Cùng câu với trục dense chọn `Beta note`.
- Gỡ publish Beta thì evidence không còn Beta. Gỡ cả Alpha thì `source-revoked`, evidence rỗng, không gọi thêm API.
- Ngân sách 1 token bị từ chối trước HTTP. Probe lỗi promote lần đầu, lần sau dùng cache và không gọi thêm.
- Sai model, file JSON hỏng, và snapshot lexical đổi trong lúc embed không promote index mới.

## Kiểm tra

| Kiểm tra | Lệnh hoặc thao tác | Môi trường | Kết quả |
| --- | --- | --- | --- |
| Embedding, cosine, RRF, vector, admin | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug --filter "FullyQualifiedName~AiRagHybridRetrievalTests\|FullyQualifiedName~AiRagCorpusBm25Tests\|FullyQualifiedName~AiRagPhase2FixRound1Tests\|FullyQualifiedName~AiRagPhase2FixRound2Tests\|FullyQualifiedName~AiRagBoundaryAccessTests\|FullyQualifiedName~OpenAiProviderClientTests" --nologo --no-restore` | HTTP giả, SQLite test host, không gọi OpenAI | **119 pass**, 0 fail, 0 skip, Duration 32 s, exit 0. Lệnh này cũng build `TCTEnglish` và `TCTEnglish.Tests` |
| Đo fixture vector | cùng test `VectorRebuild_ReusesCache_RejectsBudgetDriftAndPromotionFailure`, logger console detailed | SQLite, vector giả | dry-run `chunks=2 misses=2 estimatedTokens=15 estimatedRequests=1`; index promote `6791` byte, `2` chunk. Số millisecond in ra gồm cả setup SQLite |
| Admin từ chối | test host `AdminEmbeddingMaintenance_RejectsUnauthorizedAndOverBudgetWithZeroApiCalls` | SQLite | anonymous không được 200; Standard không được Admin; thiếu antiforgery là 400; operation lạ, 13 request và 40001 token redirect từ chối; field `root` không tạo thư mục; câu giá vàng vẫn `OutOfScope`; số lần gọi embedding = 0 |
| `/health/ready` test host | cùng test, ngay sau khởi tạo host | SQLite | 503 vì host test báo migration pending. Đây là hành vi host test có sẵn. Assertion chỉ dùng để thấy request xảy ra và số lần gọi embedding vẫn 0. Không tính là ready |
| `/health/ready` local | `GET http://localhost:5100/health/ready` sau `scripts/start-local.ps1 -NoBrowser` | MariaDB `tctenglish_dev` | **200** `{"status":"ready"}` |
| Browser admin | Đã đăng nhập sẵn `TCT Local Admin`, mở `/Admin/AiManagement`. Bấm **Dry-run vector**. Không bấm **Tạo vector** | localhost:5100, viewport 390 và 1440 | Trang có form `vectorIndexForm`, model `text-embedding-3-small / 1536`, không có field `root`. Kết quả `embed-dry-run: embed-dry-run chunks=487 cacheHits=0 cacheMisses=487 estimatedTokens=27610 estimatedRequests=8 apiCalls=0`. Không tạo `vector-manifest.json`. 390 px: hai nút xếp dọc, mỗi nút 310 px, không tràn ngang. 1440 px: hai nút cùng hàng. Enter giả của công cụ browser không kích hoạt nút đang focus, nên đường bàn phím của form mới chưa được xác nhận |
| BM25 cũ trên browser | Bấm **Dry-run** của form lexical | cùng phiên | `dry-run: dry-run documents=471 chunks=487 exclusions=30`. Hai nút lexical bị disable lúc gửi. Hai nút vector không bị disable theo |
| Log startup và dry-run | log của `start-local.ps1` | local | Speech `configured`, không có request speech lúc startup. Không thấy chuỗi `embeddings` hay `api.openai.com` |
| Schema | không đổi entity/DbContext | local | không có migration |
| Whitespace | `git diff --check` trên các file Phase 3 | local | không báo lỗi |
| So sánh semantic live | không chạy | chưa có grant API trả phí | **chưa làm**, để Phase 7. Vector giả không được dùng làm điểm chất lượng ngôn ngữ |

Ước lượng 27610 token và 8 request là `ký tự/4` trên corpus public hiện có, không phải tokenizer của nhà cung cấp. Con số này nằm trong trần 12 request / 40000 token, nhưng trần đó không phải quyền được gọi API. Chưa đo RAM tiến trình. File index giả 2 đoạn là 6791 byte. 487 đoạn nhân 1536 float nhân 4 byte là khoảng 2,9 MB dữ liệu thô; đó là phép tính, không phải file index đã ghi.

## Giới hạn

Chat thường vẫn đi BM25 Phase 2. Hybrid có embedding câu hỏi chỉ chạy khi caller truyền `AllowQueryEmbedding=true`. Phase 4 mới được nối đường đó vào trả lời trả phí và quota bền. Chưa có citation bền, UI chat mới, hay ngưỡng nghiệm thu.

Tài khoản embedding chưa được kiểm live. Key đã cấu hình không chứng minh còn quota. Không có CI và không có production deploy. Snapshot lexical cũ không tự xóa. Host SQLite của test vẫn 503 ở `/health/ready`; host MariaDB local trả 200.

## Việc Codex cần xem

Tách client embedding khỏi `IAiProviderClient`, chặn gọi API khi từ chối quyền/CSRF/ngân sách/corpus lệch, và việc chat không nhận client embedding. Manifest 81 entry ở trên là snapshot cần đối chiếu. Fingerprint trả về là `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`.

Phase 3 đang **AWAITING_REVIEW**. Không mở Phase 4.

## Tin nhắn dán cho Codex

Phase 3 embedding/vector/RRF đã xong local và đang AWAITING_REVIEW. Fingerprint 81 entry `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`, tiền nhiệm Phase 2 `62a864787561df4164da82030b24b590564fdce804d097a49b66c4ddc5a4dbdf`. 119 test tập trung pass. Dry-run admin thật: 487 đoạn, ước lượng 27610 token / 8 request, 0 lần gọi API. Chưa có embedding live và chưa có so sánh semantic. Phase 4–7 vẫn LOCKED. Nhờ review, không triển khai phase sau.
