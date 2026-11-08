# Phase 3 — handoff sửa vòng 1

Ngày 2026-11-06. Cursor thực hiện `.ai/prompts/hybrid-rag/phase-3-fix-round-1.md` và `docs/superpowers/plans/hybrid-rag/phase-3-fix-round-1.md`. Kết quả là **AWAITING_REVIEW**. Phase 4–7 giữ LOCKED. Agent không tự duyệt và không coi bốn finding là đã được Codex đóng.

## Danh tính

- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Chưa commit.
- Trước khi sửa, fingerprint trả về `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b` khớp 81/81. Fingerprint Phase 2 vẫn là `62a864787561df4164da82030b24b590564fdce804d097a49b66c4ddc5a4dbdf`.
- Manifest tích lũy sau sửa: [scope-manifest.json](scope-manifest.json), **86 entry**, fingerprint `408779fa6c5d9139fa40d8021693c4b76fa6a3b860bbf1478804b368cec9e720`. Đã đọc lại file và tính lại: 86 hash khớp, fingerprint khớp. Đây là danh tính bàn giao, chưa phải phê duyệt.
- Năm file mới trong digest: prompt và plan của vòng sửa này, `EmbeddingOperationBudget.cs`, `VectorIndexAdmission.cs`, `AiRagPhase3FixRound1Tests.cs`. Bảy file cũ đổi hash: `bug-fix-log.md`, `AiManagementController.cs`, `Program.cs`, `AiProviderException.cs`, `IOpenAiEmbeddingClient.cs`, `OpenAiEmbeddingClient.cs`, `VectorIndexService.cs`.
- Không sửa `appsettings.json`, `.csproj`, model text, schema hay shared data. `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không gọi API trả phí. Không commit, push, PR, CI hay deploy.

## Từng finding

Review round 1 là bằng chứng đỏ, chạy trên HTTP giả và SQLite. Bộ test mới được chạy sau khi mã sửa đã nằm trong cây; lần chạy hoàn chỉnh đầu tiên đã xanh. Không có lần đỏ riêng của file test mới vì lần Debug bị khóa file bởi tiến trình web cũ và không chạy được assertion.

| ID | Đỏ, theo review | Xanh sau sửa |
| --- | --- | --- |
| F01 | Budget 1 request, 429 rồi response hợp lệ: **2 POST**, ApiCalls 1, **promoted** | `F01_OneRequestBudget_DoesNotSendTheRetry`: 1 POST, ApiCalls 1, không promoted, index Missing |
| F02 | Câu “Tôi học tiếng Việt mỗi ngày.” ước lượng 7 token vẫn được gửi khi trần là 10; fake trả 20 token nhưng báo ActualPromptTokens **0** | Cùng câu có 28 ký tự, ước lượng 7, **38 byte UTF-8**. Trần 10 từ chối trước HTTP, 0 call. Chuỗi ASCII ngắn hơn trần byte nhưng fake trả 20 thì failed `usage-exceeded`, ActualPromptTokens **20**, không promote |
| F03 | Hai scope cùng index: job 2 vào provider trước khi job 1 được thả; cả hai promoted, 2 lần gọi | `F03_SeparateScopes_DoNotOverlapTheSameIndex`: job 2 trả busy, tổng 1 call; sau khi nhả lease, lần sau dùng cache và vẫn 1 call |
| F04 | 65 nguồn, batch `[64,1]`, nguồn đã gỡ publish vẫn được gửi ở batch sau và **promoted** | Gỡ publish, đổi hash, chuyển writing public sang có UserId, và thay snapshot lexical: batch chỉ `[64]`, không promoted. Retry 429 sau khi gỡ publish không gửi POST thứ hai |

Hủy sau một batch đã có usage được giữ: status `cancelled`, ActualPromptTokens bằng usage đã nhận, index cũ không bị thay. Snapshot lexical đổi trong lúc embed vẫn trả `lexical-snapshot-changed`, không promote. Lệch nội dung hoặc publication trả `corpus-drift`.

## Cách sửa

Một ledger cho cả job. Client trừ một request trước mỗi POST, kể cả retry, và không gửi thêm khi hết allowance. Usage đã biết được cộng ngay khi response hợp lệ. Vượt trần token thì dừng, không ghi cache và không promote, nhưng không xóa usage đã biết. Response không có usage dùng được thì báo `usage=unknown`.

Trần trước khi gửi là số byte UTF-8. Model `text-embedding-3-small` dùng BPE theo byte, merge chỉ làm giảm số token, nên số token không thể lớn hơn số byte. Đây là trần an toàn, không phải tokenizer và không phải ước lượng ký tự/4. Ước lượng ký tự/4 vẫn hiện là `estimatedTokens`.

Admission là singleton theo thư mục index đã duyệt. `VectorIndexService` và DbContext vẫn scoped. Sau khi nhận lease, plan và cache được đọc lại. Hết thời gian chờ thì `busy`, không gọi provider.

Trước mỗi batch, trước mỗi retry có thể gửi, và ngay trước promote, job đối chiếu snapshot lexical đang phát hành với tập chunk public hiện tại. Lệch thì dừng, giữ index hợp lệ lần trước và usage đã biết. Không tự rebuild corpus.

## Kiểm tra

| Kiểm tra | Lệnh hoặc thao tác | Kết quả |
| --- | --- | --- |
| Regression vòng 1 | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase3FixRound1 --filter FullyQualifiedName~AiRagPhase3FixRound1Tests` | **7 pass**, 0 fail, 0 skip, khoảng 12 s, exit 0 |
| RAG và text client | cùng project, `-c Phase3FixRound1`, filter `FullyQualifiedName~AiRag\|FullyQualifiedName~OpenAiProviderClientTests` | **126 pass**, 0 fail, 0 skip, Duration 40 s, exit 0. Lần trước đó 125 pass và 1 fail vì snapshot đổi bị gán nhầm `corpus-drift`; đã trả lại `lexical-snapshot-changed` rồi chạy lại |
| Tương thích | cùng configuration, `--no-build`, filter cancel/quota/closeout/consumer/baseline/classifier/evaluation | **84 pass**, 0 fail, 0 skip, Duration 1 phút, exit 0 |
| Bộ app | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase3FixRound1` | **1058 pass**, **19 skip**, 1077 total, 0 fail, Duration 3 phút 48 giây, exit 0. 19 skip là test MariaDB opt-in có sẵn, không tính là pass |
| Tooling | `uv run --no-project python scripts/agent_tooling.py` | exit 0, “Agent tooling verified (working tree).” |
| Python | `uv run --no-project python -m unittest discover -s scripts/tests -v` | Ran 135, OK skipped=1, exit 0. Skip là contract “Non-Windows rejection” trên Windows |
| Whitespace | `git diff --check` | không báo lỗi |
| `/health/ready` | `GET http://localhost:5102/health/ready` sau `scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5102` | **200** `{"status":"ready"}`. Speech `configured`, không có request speech lúc startup. Log không có `embeddings` hay `api.openai.com` |
| Bàn phím Dry-run vector | Playwright Chromium, đã đăng nhập admin local, không bấm **Tạo vector** | Tab từ ô token tới **Dry-run vector**. Shift+Tab về `maxInputTokens`. Tab trở lại **Dry-run vector**. Enter gửi. Enter lần hai không tạo thêm POST. Kết quả `embed-dry-run: embed-dry-run chunks=487 cacheHits=0 cacheMisses=487 estimatedTokens=27610 safeTokens=113887 estimatedRequests=8 apiCalls=0 usage=0` |
| Form lexical | Enter trên nút **Dry-run** của form BM25 | `dry-run: dry-run documents=471 chunks=487 exclusions=30`. Tổng cộng hai POST tới action rebuild, đúng một cho mỗi form |
| Layout | viewport 1440 và 390 | 1440: hai nút vector cùng hàng, y=1034. 390: xếp dọc, mỗi nút rộng 310 px, không tràn ngang |
| Manifest vector | `TCTEnglish/.appdata/local/ai-rag/vector-manifest.json` và bản ở gốc checkout | không có trước và không có sau dry-run |
| Schema | không đổi entity/DbContext | không có migration, không export shared data |

Tiến trình web cổng 5102 có working set khoảng 192 MB sau dry-run. Đó là cả tiến trình, không phải RAM riêng của retrieval. Cổng 5100 của tiến trình cũ không bị dừng. Configuration test là `Phase3FixRound1` để không ghi đè output Debug đang bị khóa.

`safeTokens=113887` lớn hơn trần form 40000. Ước lượng ký tự/4 vẫn là 27610 và nằm trong trần, nhưng trần byte của cả corpus không nằm trong 40000. Embed live cả corpus với form mặc định sẽ bị từ chối trước HTTP. Chưa bấm **Tạo vector** và chưa đo file index thật. 487 đoạn nhân 1536 float nhân 4 byte vẫn là khoảng 2,9 MB dữ liệu thô, chỉ là phép tính.

## Giới hạn

Chat thường vẫn đi BM25. So sánh chất lượng semantic vẫn để Phase 7. Key đã cấu hình không chứng minh còn quota. Không có CI và không có production deploy. Host SQLite của test vẫn có thể trả 503 ở `/health/ready`; host MariaDB local trên cổng 5102 trả 200.

Lệnh cài Playwright cho kiểm tra bàn phím đã gỡ các bản Chromium cũ không còn được bản Playwright vừa dùng trong `D:\TCTEnglishLocal\cache\playwright`. Đây là cache local, không phải file Git. Nếu một công cụ local còn trỏ vào các bản đó thì cần cài lại đúng phiên bản Playwright của công cụ đó.

## Việc Codex cần xem

Ledger một request không gửi POST thứ hai. Usage đã biết không bị đưa về 0. Hai scope không chồng công việc trả phí trên cùng index. Nguồn bị gỡ, đổi hash hoặc đổi snapshot thì batch sau không gửi và không promote. Manifest 86 entry ở trên là snapshot cần đối chiếu. Fingerprint trả về là `408779fa6c5d9139fa40d8021693c4b76fa6a3b860bbf1478804b368cec9e720`.

Phase 3 đang **AWAITING_REVIEW**. Không mở Phase 4.

## Tin nhắn dán cho Codex

Phase 3 repair round 1 đã xong local và đang AWAITING_REVIEW. Fingerprint 86 entry `408779fa6c5d9139fa40d8021693c4b76fa6a3b860bbf1478804b368cec9e720`, tiền nhiệm `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`. F01–F04 có test xanh: 7 regression, 126 RAG/provider, 84 tương thích, bộ app 1058 pass và 19 skip opt-in. Dry-run vector bằng bàn phím: 487 đoạn, ước lượng 27610, trần byte 113887, 0 lần gọi API, không tạo vector manifest. Chưa có embedding live và chưa có so sánh semantic. Phase 4–7 vẫn LOCKED. Nhờ review, không triển khai phase sau.
