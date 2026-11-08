# Phase 1 — handoff sửa vòng 2

Trạng thái: **AWAITING_REVIEW**. Chưa được duyệt. Phase 0 vẫn APPROVED. Phase 2–7 vẫn LOCKED. F03 và phần identity `card:{id}` đã được review vòng 2 xác minh; vòng này giữ các hồi quy đó. Agent không tự đóng finding.

## Phạm vi

- Vòng sửa phần còn lại của HR-P1-R1-F01, F02 và F04. Plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-2.md`. Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-2.md`. Review: [review-round-2.md](review-round-2.md).
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, 37 digest entry khớp `606fc4edf058543087e93a0a689a3e08f1c0ad31d53f6a3311d886c8e16ee1b1`.
- Fixture Phase 0 vẫn `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Không sửa `Program.cs`, `appsettings.json`, `.csproj`, model hay schema. `IAiProviderClient` không đổi. Không API trả phí, CI, push, PR hay deploy.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json).

## Từng finding

### HR-P1-R1-F01

Trước: user có thẻ `weather` / `STORED_WEATHER_MEANING`; “What is the weather today?” ra `Answered`. Sau: `OutOfScope`, scope không phải `full`, text không chứa definition đó. “Thời tiết hôm nay thế nào?” cũng `OutOfScope`. “what does weather mean” và bare `weather` vẫn `Answered` với definition. Câu dài không có fact-frame “Please check zephyr-window against my notes from class” vẫn trả “a high window”. Câu giá vàng cũ vẫn từ chối.

Quy tắc mới không chỉ thêm chữ weather: câu có mốc hiện tại (today/hôm nay/right now) và hình câu hỏi (what is/bao nhiêu/thế nào/ra sao) bị coi là fact ngoài học liệu, trừ khi đang hỏi nghĩa đã lưu hoặc dữ liệu học của chính user. Các cụm giá/thị trường cũ vẫn là tín hiệu bổ sung.

### HR-P1-R1-F02

Trước: bare `forecast` đúng, nhưng “Giải thích forecast trong bộ thẻ của tôi” chỉ còn `aaa-*` priority 40. Sau, trên SQLite, cả hai câu đều xếp `forecast` / `EXACT_FORECAST_TARGET` đầu tiên và boundary trả `Answered`/`full` với definition đó. Thẻ `forecast` của user khác không xuất hiện. Test `go`/`up`, cửa sổ 500 và 120 broad `zephyr-window` vẫn xanh.

Token nội dung được tách khỏi từ khung như trong/bộ/thẻ/explain rồi đưa vào exact tier. Trần vẫn 25 exact, 20 phrase, 100 broad.

### HR-P1-R1-F04

Trước: bốn bộ Alpha/Beta/Gamma/Sporting cộng 30 bản `match` khác làm “nghia cua match trong bo Sporting” thành NeedsClarification không còn Sporting. Sau: `Answered`/`full`, chỉ definition `SPORTING_TARGET_MEANING`, evidence card đúng một key `card:{id}` của thẻ Sporting. “trong bo Secret” của user khác không thành `Answered` và không mượn definition bộ khác. “so sanh match giua bo Sporting va Pairs” vẫn trả cả hai nghĩa, không hỏi “bộ nào”. Test hai bộ cũ và key ổn định vẫn xanh.

Tên bộ một từ được phép xem được lọc bằng SQL trước mọi `Take`. Bộ ghi rõ sau “trong bo/trong set” nhưng không truy cập được thì dừng, không rơi sang bộ khác.

## Kiểm tra

| Việc | Kết quả |
| --- | --- |
| Ba hồi quy mới trước khi sửa, `Phase1FixRound2` | 3 fail, 0 pass |
| Sau sửa: ba hồi quy, Pomelo translation, F03, giá vàng, hai bộ, cửa sổ 500, lớp, mật khẩu | 11 pass, 0 fail, khoảng 6 s |
| Filter plan chat/history/quota/cancel/retriever/composer/fixture, logger minimal, TRX `.tmp/hybrid-rag-phase1-fix2/phase1-fix2.trx` | 198 pass, 0 fail, khoảng 26 s |
| `--no-build` OpenAiProviderClientTests, MlNet, ba probe review | 65 pass, 0 fail, khoảng 18 s |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0 |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests, OK, skipped=1, khoảng 78 s |
| `git diff --check` | exit 0 |
| Pomelo 10.5.23 `ToQueryString` | Có `LIMIT`, `LOWER`, `REPLACE`. Không mở kết nối, không thực thi SQL |
| MariaDB disposable, trình duyệt, API trả phí, CI, production | Không chạy |

Không cộng 198 và 65 thành một tổng.

## Giới hạn

- Tên bộ nhiều từ chưa được nhận từ câu hỏi.
- Bộ không tồn tại trả về ngoài phạm vi/không phải full answer, chưa có câu “không thấy bộ” riêng.
- Exact tier vẫn trần 25 sau khi đã lọc bộ. Không tải toàn bộ thẻ.
- Bản dịch Pomelo không phải chứng nhận MariaDB đã chạy query.
- Chưa có BM25, citation bền hay UI phase sau.

## Trạng thái cuối

Phase 1: **AWAITING_REVIEW**. Phase 2–7: **LOCKED**.

```text
Phase 1 fix round 2 đang AWAITING_REVIEW. Handoff: docs/feature-audits/hybrid-rag/phase-1/fix-round-2-handoff.md. Manifest: docs/feature-audits/hybrid-rag/phase-1/scope-manifest.json. Nhánh feature/hybrid-rag-chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69, chưa commit. F01/F02/F04 còn lại đã đỏ rồi xanh trên SQLite và boundary local. F03 và card:{id} được giữ. Phase 2–7 vẫn LOCKED. Nhờ Codex review vòng 3, chưa tự duyệt.
```
