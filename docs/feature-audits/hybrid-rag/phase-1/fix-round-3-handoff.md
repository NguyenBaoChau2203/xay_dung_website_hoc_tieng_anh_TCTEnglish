# Phase 1 — handoff sửa vòng 3

Trạng thái: **AWAITING_REVIEW**. Chưa được duyệt. Phase 0 vẫn APPROVED. Phase 2–7 vẫn LOCKED. F02, F03 và identity `card:{id}` đã được review trước xác minh; vòng này giữ các hồi quy đó. Agent không tự đóng finding.

## Phạm vi

- Vòng sửa HR-P1-R1-F01, HR-P1-R1-F04 và HR-P1-R3-F05. Plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-3.md`. Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-3.md`. Review: [review-round-3.md](review-round-3.md).
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, 39 digest entry khớp `f513c62c6fdff3c12db7516cf8c5e1f0616212522b276caaa06b3d31e746435e`.
- Fingerprint tích lũy sau vòng này, chưa duyệt: `ba644991f0284ee7a0b43eac3e1a5670b492f2b5941da0506be9b1b024bb502d`.
- Fixture Phase 0 vẫn `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Không sửa `Program.cs` trong vòng này, không sửa `appsettings.json`, `.csproj`, model hay schema. `IAiProviderClient` không đổi. Không API trả phí, CI, push, PR hay deploy.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json).

## Từng finding

### HR-P1-R1-F01

Trước: thẻ `France` chỉ có `STORED_FRANCE_COUNTRY_DEFINITION`; “What is the capital of France?” vẫn `Answered`/`full`. Sau: không còn `Answered`, scope không phải `full`, text không chứa definition đó. Câu cùng cấu trúc “What is the currency of Japan?” cũng từ chối `STORED_JAPAN_COUNTRY_DEFINITION`. “What is the capital of Kenya?” vẫn `Answered`/`full` vì definition có chữ capital. “what does France mean”, bare `France` và “Please check zephyr-window against my notes from class” vẫn trả học liệu. “What is the weather today?” và “Giá vàng hôm nay là bao nhiêu?” vẫn `OutOfScope`.

Quy tắc không chặn chữ France hay capital. Câu mở đầu bằng what/who is/are the X of Y chỉ được dùng thẻ khi definition hoặc example chứa thuộc tính X và thẻ đúng thực thể Y. Thẻ khác chỉ nhắc thuộc tính thì không được trả lời thay.

### HR-P1-R1-F04

Trước: “nghia cua forecast trong bo Daily Words” trả nghĩa của bộ Daily. Sau: `Answered`/`full`, chỉ `TARGET_DAILY_WORDS_MEANING`, một key `card:1`. “trong bo Daily” vẫn chỉ `DAILY_EXACT_MEANING` và `card:2`. Tên trong ngoặc `"Daily Words"`, bộ chỉ có tên nhiều từ Blue Ocean, và bộ lưu “Thời Tiết” đều ra đúng thẻ và giữ tên Unicode trong câu trả lời. “trong bo Daily Extra” không thành `Answered` và không mượn Daily hay Daily Words. Hai bộ cùng tên Echo được hỏi “bộ nào”, evidence `card:5` và `card:6`, không full một nghĩa. “so sanh forecast giua bo Daily Words va Daily” trả cả hai nghĩa.

Nhãn sau “trong bo/trong set”, hoặc từng vế sau “giua bo … va …”, là cả cụm tên. Khớp bằng tên đã chuẩn hóa với set được phép xem, rồi lọc `SetId` trước trần 25/20/100. Prefix không thay tên đầy đủ. Tên không resolve được thì không trả thẻ bộ khác.

### HR-P1-R3-F05

Trước khi thêm bộ Weather, bare `weather` trả `CORRECT_EXACT_WEATHER_MEANING` và `card:1`. Sau khi thêm bộ Weather với thẻ cloud có definition nhắc weather, cùng câu bare vẫn trả thẻ Nature/`card:1`, không đổi sang cloud. “nghia cua weather trong bo Weather” trả definition của cloud. “nghia cua weather trong bo Nature” vẫn trả nghĩa exact. Bỏ qualifier sẽ làm control bộ Weather thất bại vì exact Nature thắng.

Từ cần tra không còn bị đem đi lọc tên bộ. Chỉ marker nguồn hoặc câu so sánh mới tạo ràng buộc set.

## Kiểm tra

| Việc | Kết quả |
| --- | --- |
| Ba hồi quy mới trước khi sửa, `Phase1FixRound3` | 3 fail, 0 pass |
| Sau sửa: ba hồi quy, Pomelo, F02 forecast, F03 go/up, bốn bộ, hai bộ, thời tiết, giá vàng | 10 pass, 0 fail, khoảng 13 s |
| Filter plan chat/history/quota/cancel/retriever/composer/fixture, TRX `.tmp/hybrid-rag-phase1-fix3/phase1-fix3.trx` | 201 pass, 0 fail, khoảng 39 s |
| `--no-build` OpenAiProviderClientTests, MlNet, ba probe review, TRX `phase1-fix3-extra.trx` | 65 pass, 0 fail, khoảng 24 s |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0 |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests, OK, skipped=1, khoảng 100 s |
| `git diff --check` | exit 0 |
| Pomelo `ServerVersion.Parse("10.5.23-mariadb")`, query builder `BoundedExact`/`BoundedPhrase`/`BoundedBroad`, `ToQueryString` | Có `LIMIT 25`, `LIMIT 20`, `LIMIT 100`, `LOWER`, `REPLACE`, cột set id. Không mở kết nối |
| MariaDB disposable, trình duyệt, API trả phí, CI, production | Không chạy |

Không cộng 201 và 65 thành một tổng. 10.5.23 là phiên bản server MariaDB, không phải phiên bản package Pomelo.

## Giới hạn

- Dạng fact ngoài khung “what/who is/are the X of Y”, ngoài mốc hiện tại và danh sách chủ đề cũ, chưa được xét hỗ trợ theo thuộc tính.
- Bộ không thấy hoặc tên không khớp vẫn không `Answered`, chưa có câu báo “không thấy bộ” riêng.
- Hai bộ trùng tên thì hỏi lại; câu so sánh có một vế không resolve được thì không trả vế còn lại.
- Trần exact/phrase/broad vẫn 25/20/100. Không tải toàn bộ thẻ.
- Bản dịch Pomelo không phải chứng nhận query đã chạy trên MariaDB.
- Chưa có BM25, citation bền hay UI phase sau.

## Trạng thái cuối

Phase 1: **AWAITING_REVIEW**. Phase 2–7: **LOCKED**.

```text
Phase 1 fix round 3 đang AWAITING_REVIEW. Handoff: docs/feature-audits/hybrid-rag/phase-1/fix-round-3-handoff.md. Manifest: docs/feature-audits/hybrid-rag/phase-1/scope-manifest.json. Nhánh feature/hybrid-rag-chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69, chưa commit. F01/F04/F05 đã đỏ rồi xanh trên SQLite và boundary local. F02/F03 và card:{id} được giữ. Phase 2–7 vẫn LOCKED. Nhờ Codex review vòng 4, chưa tự duyệt.
```
