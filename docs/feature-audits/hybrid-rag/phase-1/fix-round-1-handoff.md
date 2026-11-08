# Phase 1 — handoff sửa vòng 1

Trạng thái: **AWAITING_REVIEW**. Chưa được duyệt. Phase 0 vẫn APPROVED. Phase 2–7 vẫn LOCKED. Agent không tự đánh dấu finding đã đóng.

## Phạm vi

- Vòng sửa HR-P1-R1-F01 đến F04. Plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-1.md`. Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-1.md`. Review gốc: [review-round-1.md](review-round-1.md).
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, 32 digest entry khớp fingerprint đã trả về `781f92c523d57554a0b96acfd9c704986e8c7ac4e74cf47c9fcd39a2d9ef483f`.
- Grant DI hẹp đã ghi ở Phase 1 được giữ. `Program.cs` không đổi trong vòng này. `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không sửa `appsettings.json`, `.csproj`, model, schema, dữ liệu shared. Không API trả phí, CI, push, PR hay deploy.
- Hash fixture Phase 0 `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json` vẫn `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json).

## Từng finding

### HR-P1-R1-F01

Trước: “Giá vàng hôm nay là bao nhiêu?” với thẻ `vang` / `SCOPED_CARD_DEFINITION` ra `Answered`, scope `full`. Sau: `OutOfScope`, scope không phải `full`, text không chứa definition đó. Control “vang nghia la gi” vẫn `Answered`/`full` và có definition. Control “zephyr-window” vẫn trả “a high window”.

### HR-P1-R1-F02

Trước: 120 thẻ `aaa-001`… có definition nhắc `zephyr-window` làm kết quả đầu không còn là term chính xác. Sau, trên SQLite: snippet đầu là `zephyr-window` / `exact-authorized-definition`. Thẻ cùng term của user khác không xuất hiện. Test cửa sổ 500 thẻ không liên quan và test gỡ thành viên lớp vẫn xanh.

### HR-P1-R1-F03

Trước: `go` và `up` không có snippet; `forecast` vẫn có. Sau: “go”, “tu go la gi” và “up nghia la gi” trả đúng definition. `forecast` vẫn là control. Câu chỉ có stopword “la” trả rỗng. Token 2 ký tự đi tầng exact, không đưa vào `Contains` broad.

### HR-P1-R1-F04

Trước: bare term `match` ở hai bộ ra `Answered`/`full`, không hỏi chọn nguồn, hai evidence cùng key theo title. Sau: outcome `NeedsClarification`, text có “bộ nào”, không dùng “Mình tìm thấy từ”. Source key là `card:{CardId}` của từng thẻ, khác nhau và giống nhau ở lần gọi thứ hai. “nghia cua match trong bo Sporting” chỉ còn “a sporting contest”. “so sanh match giua bo Sporting va Pairs” là `Answered` và có cả hai nghĩa, không hỏi “bộ nào”.

## Trần truy vấn

Các tầng đều có predicate quyền cũ và `AsNoTracking`:

| Tầng | Cách chọn | Trần |
| --- | --- | --- |
| Exact | `Term` viết thường nằm trong needle của cụm đầy đủ hoặc token đúng 2 ký tự; cụm có dấu cách còn dạng gạch nối và gạch dưới | 25, `OrderBy CardId` |
| Phrase | `lower(replace(Term,'-',' '))` chứa cụm dài ít nhất 3 ký tự | 20, `OrderBy CardId` |
| Broad | token đơn dài ít nhất 3 ký tự, `Contains` trên term hoặc definition | 100, `OrderBy Term, CardId` |

Sau khi ghép và loại trùng `CardId`, chấm điểm trong bộ nhớ rồi lấy 3. Không tải cả bảng.

Giới hạn provider: test query mới chạy SQLite. Test thẻ cũ vẫn chạy EF InMemory và xanh. Chưa chạy MariaDB disposable trong vòng này, nên chưa có chứng nhận bản dịch Pomelo trên hosting.

## Kiểm tra

| Việc | Lệnh | Kết quả |
| --- | --- | --- |
| Bốn hồi quy mới, trước khi sửa | `dotnet test ... -c Phase1FixReview` filter bốn tên test mới | 4 fail, 0 pass. Đúng F01–F04 |
| Bốn hồi quy mới cộng hai test thẻ cũ, sau khi sửa | Cùng cấu hình, thêm hai test cửa sổ 500 và thu hồi lớp | 6 pass, 0 fail, khoảng 10 s |
| Filter plan: chat, history, quota, cancel, retriever, composer, baseline, fixture | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixReview` với filter trong plan. Logger console để `minimal`, thêm TRX `.tmp/hybrid-rag-phase1-fix1/phase1-fix1.trx` | 194 pass, 0 fail, khoảng 26 s |
| Provider và ML.NET | `dotnet test ... -c Phase1FixReview --no-build` filter `OpenAiProviderClientTests\|MlNet` | 61 pass, 0 fail, khoảng 14 s |
| Tooling | `uv run --no-project python scripts/agent_tooling.py` | exit 0, “Agent tooling verified (working tree).” |
| Script tests | `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests, OK, skipped=1, 88,3 s |
| Whitespace | `git diff --check` | exit 0 |
| Trình duyệt, API trả phí, MariaDB, CI, production | Không chạy | Không chạy |

Không cộng 194 và 61 thành một tổng duy nhất vì hai filter khác nhau.

## Giới hạn còn lại

- Danh sách cụm fact ngoài học liệu không phải bộ phân loại tri thức thế giới đầy đủ.
- Tầng exact chỉ chuẩn hóa hoa/thường và `-`/`_`, chưa phải toàn bộ `AiTextNormalizer`.
- Hơn 3 thẻ cùng term vẫn bị `Take(3)` sau khi chấm điểm.
- Tên bộ ngắn hơn 3 ký tự không được coi là nguồn đã chỉ định.
- Evidence vẫn chỉ tồn tại trong lượt trả lời. Chưa có citation bền, BM25 hay UI phase sau.
- Không có kiểm tra trình duyệt.

## Trạng thái cuối

Phase 1: **AWAITING_REVIEW**. Phase 2–7: **LOCKED**.

```text
Phase 1 fix round 1 đang AWAITING_REVIEW. Handoff: docs/feature-audits/hybrid-rag/phase-1/fix-round-1-handoff.md. Manifest: docs/feature-audits/hybrid-rag/phase-1/scope-manifest.json. Nhánh feature/hybrid-rag-chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69, chưa commit. F01–F04 đã có hồi quy đỏ rồi xanh trên boundary local. Phase 2–7 vẫn LOCKED. Nhờ Codex review lại, chưa tự duyệt.
```
