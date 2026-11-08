# Phase 7 sửa vòng 1 — envelope, protocol và đồng hồ provider

## Phạm vi

- Phase 7, sửa vòng 1. Prompt `.ai/prompts/hybrid-rag/phase-7-fix-round-1.md`. Plan `docs/superpowers/plans/hybrid-rag/phase-7-fix-round-1.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối đều `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, snapshot đã nộp khớp 155/155. Fingerprint `4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`. Bản đó được giữ nguyên byte tại [scope-manifest-input.json](scope-manifest-input.json). Tiền nhiệm của vòng sửa là snapshot này, không phải fingerprint Phase 6.
- Manifest mới: [scope-manifest.json](scope-manifest.json), 163 mục, fingerprint `41386db1939220cc3d9f7e3f31bf5e00ac48425a81a6896572a92101c7bd9a35`.
- F01, F02 và F03 được sửa local. Nghiệm thu live vẫn BLOCKED. Báo cáo này không tự duyệt.
- Không sửa `Program.cs`, `appsettings.json`, `.csproj`, cổng production/staging, secret, fixture đánh giá, model hay migration. Không gọi text, embedding hoặc speech. Không push, PR, CI hoặc deploy.

## Envelope đưa ra để review

Các số 60 POST / 100.000 token, phương án A/B, 169.087 và 282.974 được rút. Chúng không phải quyền chi.

Đường thành công, khi mọi response trả usage và không có unknown:

| Stage | POST | Trần byte đã biết |
| --- | ---: | ---: |
| 51 nguồn synthetic | 1 | 3.565 |
| Corpus local 487 chunk | 10 | 113.887 |
| 80 câu development | 2 | 3.807 |
| 40 câu held-out | 1 | 1.860 |
| Tổng embed | 14 | 123.119 |

20 lượt online không nằm trong 14 POST. Index local đang thiếu vector, nên retrieval là `lexical_degraded` và số embedding câu hỏi là 0.

Job embed production giữ trần 12 POST và 40.000 token. Batch 36.276 để lại 3.724 trong job; batch 37.558 để lại 2.442. Retry của đúng batch đó bị từ chối, unknown giữ đến hết run, index không promote. Đường 10 POST / 113.887 byte là đường thành công, không phải lời hứa retry và không hứa đúng 3 job sau lỗi.

Trần harness đề xuất cho bốn stage embed là 14 POST, 123.119 token account và 20 phút. Đây là số để review chọn hoặc từ chối. Chưa có quyền gọi API.

Lan Standard giữ 15 câu/ngày và 60.000 token/ngày. Không đổi Lan thành Admin hoặc Premium. 20 câu trong một ngày UTC vẫn vượt trần 15 câu.

Chi tiết: [dry-run-envelope.md](dry-run-envelope.md). Protocol actor, ngày, mẫu số và seed disposable: [evaluation-protocol.md](evaluation-protocol.md).

## Việc đã sửa

- `AiAcceptanceRunGuard` nhận từng send giả trước HTTP, giữ unknown, từ chối khi batch không còn chỗ trong job hoặc khi run vượt trần token/POST/thời lượng, và không promote khi còn unknown hoặc thiếu chunk.
- Replay 487 chunk khóa batch đầu 36.276 + 1.790, batch kế 37.558, tổng 10 POST và 113.887 byte. Promote chỉ đúng khi đủ 487 chunk và unknown bằng 0.
- `IAiTransportClock` để test điều khiển backoff, đọc body và deadline. Production vẫn dùng `Stopwatch`, `CancelAfter` và `Task.Delay`. Tham số thứ tư của `OpenAiProviderClient` có default, nên đăng ký DI hiện tại không đổi.
- Cửa sổ 0,8–2,5 giây và số lần gọi được giữ. Test mới: 503, backoff 250 ms, body thứ hai, một cửa sổ 1 giây ban đầu, `Starts == 1`.
- Fixture revision 2 không đổi. Test khóa canonical `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`, 120/80/40, answered 44/18, mọi ca `actor-lan-standard`, ngày `2026-11-06`.
- Index vector thiếu trả `lexical_degraded`, lý do `manifest-missing`, 0 lệnh embedding.

## Preflight không trả phí

Docker vẫn không nối được `npipe:////./pipe/dockerDesktopLinuxEngine`. Cổng `127.0.0.1:3307` đóng. Không khởi động Docker, không chạy `scripts/start-local.ps1`, không seed và không đụng DB owner.

Vì vậy chưa có `DryRunAsync` trên MariaDB, chưa có cold start, `/health/ready`, speech-status hay diễn tập trình duyệt. Checksum file snapshot `cdbfb0dd5f7d65f734536afaabee5241fdfa794b3595596b93769eb88e286d10` chỉ là đo file local, không phải bằng chứng DB khớp corpus.

Bằng chứng provider, hủy và index thiếu là fake HTTP / SQLite. Không có request ra mạng.

## Sổ kiểm tra

| Kiểm tra | Lệnh | Kết quả |
| --- | --- | --- |
| Snapshot vào vòng sửa | SHA-256 từng entry đã lưu, rồi fingerprint | 155/155, `4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817` |
| Focused sau sửa | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase7FixRound1 --filter FullyQualifiedName~OpenAiProviderClientTests\|FullyQualifiedName~AiRagPhase7FixRound1Tests\|FullyQualifiedName~MissingVectorIndex_ReturnsLexicalDegradedWithoutQueryEmbedding` | 46 passed, 0 failed, 0 skipped, 3 s |
| Full suite cùng binary | `dotnet test ... -c Phase7FixRound1 --no-build` | 1142 passed, 22 skipped, 0 failed, total 1164, 2 m 29 s. TRX `.tmp/hybrid-rag-review/phase-7/phase7-fix-round-1-app.trx` |
| Node chat | `node --test scripts/tests/test_ai_chat_phase6_sources.cjs scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs` | 14 passed, 0 failed |
| Build app | `dotnet build TCTEnglish/TCTEnglish.csproj -c Phase7FixRound1 --no-restore` | Succeeded, 0 warning, 0 error |
| Whitespace | `git diff --check` | sạch |
| MariaDB / trình duyệt | Docker pipe và cổng 3307 | không chạy được |

22 skip là opt-in MariaDB, live audio/text và local-scale. Chúng không được tính là pass. Suite đầy đủ có cả hai test deadline/backoff từng fail dưới tải và test mới một cửa sổ 503. Lần chạy riêng không được dùng để đóng F03.

Không có file entity, DbContext hay migration trong phần hash đổi của vòng này. Không tạo migration.

## Rủi ro cho reviewer

- 14 POST và 123.119 byte là trần đề xuất. Chưa được duyệt để chi.
- Hai batch 36.276 và 37.558 không retry trong cùng job 40.000. Run dừng partial và không promote.
- Query embedding chỉ phát sinh sau khi index đã promote. Index thiếu thì 0 embedding câu hỏi.
- 20 lượt online của Lan Standard không nằm trong một ngày UTC vì trần 15 câu.
- File snapshot 487 chunk chưa được đối chiếu với MariaDB.
- Tham số đồng hồ là optional. Suite đầy đủ đi qua host test và không cần sửa `Program.cs`.

## Trạng thái

Sửa local: AWAITING_REVIEW. Nghiệm thu live, embed-dry-run và diễn tập trình duyệt: BLOCKED. CI chưa chạy. Production chưa deploy. Không có Phase 8. Báo cáo này không phải APPROVED.

Đoạn dán cho Codex:

Phase 7 sửa vòng 1 đang AWAITING_REVIEW cho phần không trả phí. Scope 163 mục, fingerprint 41386db1939220cc3d9f7e3f31bf5e00ac48425a81a6896572a92101c7bd9a35. Predecessor 155/155, fingerprint 4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817, file scope-manifest-input.json. HEAD vẫn 6af768bd32d16cc6d012d484ee82df4cab805c69. F01/F02/F03 đã sửa local. Envelope cũ A/B, 169087 và 282974 đã rút. Envelope mới để review: 14 POST embed và 123119 byte đã biết; batch 36276 và 37558 không retry trong job 40000. Full suite 1142 passed, 22 skipped, 0 failed, total 1164. Node 14/14. Build app 0 warning. Docker pipe linux thiếu, cổng 3307 đóng, nên MariaDB và trình duyệt chưa chạy. Không gọi API trả phí, không commit, không CI, không deploy. Nhờ review; đây không phải tự duyệt. Nghiệm thu live vẫn BLOCKED.
