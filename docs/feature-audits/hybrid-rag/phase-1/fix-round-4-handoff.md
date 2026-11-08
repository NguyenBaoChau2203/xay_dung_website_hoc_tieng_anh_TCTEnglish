# Phase 1 — handoff sửa vòng 4

Trạng thái: **AWAITING_REVIEW**. Chưa được duyệt. Phase 0 vẫn APPROVED. Phase 2–7 vẫn LOCKED. F02, F03, HR-P1-R3-F05 và identity `card:{id}` đã được review trước xác minh; vòng này giữ các hồi quy đó. Agent không tự đóng finding.

## Phạm vi

- Vòng sửa HR-P1-R1-F01 và HR-P1-R1-F04. Plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-4.md`. Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-4.md`. Review: [review-round-4.md](review-round-4.md).
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, 42 digest entry khớp `ba644991f0284ee7a0b43eac3e1a5670b492f2b5941da0506be9b1b024bb502d`.
- Fingerprint tích lũy sau vòng này, chưa duyệt: `26a1d1a138ba648137ea3c85b9d1fdfe8e221efe65ccc615f9b04bcf41ebf73e`.
- Fixture Phase 0 vẫn `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Không sửa `Program.cs` trong vòng này, không sửa `appsettings.json`, `.csproj`, model hay schema. `IAiProviderClient` không đổi. Không API trả phí, CI, push, PR hay deploy.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json).

## Từng finding

### HR-P1-R1-F01

Trước: thẻ France `a country; its capital is not provided in this card` vẫn `Answered`/`full` cho “What is the capital of France?”. “Thủ đô của France là gì?” cũng trả definition đó vì “là gì” bị coi là tra nghĩa. Oracle Kenya cũ bắt Answered chỉ vì definition có chữ capital.

Sau: cả câu tiếng Anh và tiếng Việt đều không `Answered`, scope không phải `full`, text không chứa `FRANCE_COUNTRY_NOTE`. Japan chỉ nói “the currency is discussed in class” cũng không thành full. Kenya “the capital city is recorded here” không còn là oracle dương. “What is the capital of Peru?” với definition `capital: LIMA9` vẫn `Answered`/`full` và text có `LIMA9`. “what does France mean”, bare `France`, câu tra `zephyr-window`, thời tiết hôm nay và giá vàng giữ hành vi cũ.

Quy tắc không lọc chuỗi “not provided”. Quan hệ thuộc tính “X of Y” hoặc “X của Y” chỉ được trả lời khi evidence có token giá trị gắn với thuộc tính đó, token phải có cả chữ và số. Câu văn chỉ nhắc thuộc tính không đủ. “là gì” không biến cả câu thành tra nghĩa.

### HR-P1-R1-F04

Trước: “nghia cua forecast trong bo Daily Study” và dạng có ngoặc trả nghĩa của bộ Daily. Sau: cả hai cách đều `Answered`/`full`, chỉ `TARGET_DAILY_STUDY_MEANING`, một key `card:1`. “trong bo Daily” vẫn là `card:2`. “trong bo Study” ra `STUDY_ONLY_MEANING`/`card:3`, không lấy Daily Study. “trong bo Reading Lab” ra `TARGET_READING_LAB_MEANING`/`card:4`, không lấy bộ Lab.

Tên sau marker hoặc trong ngoặc được giữ nguyên, kể cả Study/Reading, rồi khớp set được phép xem bằng `SetId` trước trần. Cụm “trong bộ thẻ của tôi” vẫn không phải tên bộ, nên F02 tiếp tục xếp exact forecast. Daily Words, Unicode, prefix không tồn tại, Echo và comparison vẫn xanh.

## Kiểm tra

| Việc | Kết quả |
| --- | --- |
| Hai hồi quy mới/sửa oracle trước khi sửa, `Phase1FixRound4` | 2 fail, 0 pass |
| Sau sửa: hai hồi quy, tên bộ cũ, bare weather, F02, F03, bốn bộ, hai bộ, thời tiết, giá vàng, Pomelo | 11 pass, 0 fail, khoảng 5 s |
| Filter plan chat/history/quota/cancel/retriever/composer/fixture, TRX `.tmp/hybrid-rag-phase1-fix4/phase1-fix4.trx` | 202 pass, 0 fail, khoảng 37 s |
| `--no-build` OpenAiProviderClientTests, MlNet, ba probe review, TRX `phase1-fix4-extra.trx` | 65 pass, 0 fail, khoảng 19 s |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0 |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests, OK, skipped=1, khoảng 95 s |
| `git diff --check` | exit 0 |
| Pomelo `ServerVersion.Parse("10.5.23-mariadb")` trên `BoundedExact`/`BoundedPhrase`/`BoundedBroad` | Vẫn pass. Hình query không đổi trong vòng này: `LIMIT 25/20/100`, `LOWER`, `REPLACE`, cột set id. Không mở kết nối |
| MariaDB disposable, trình duyệt, API trả phí, CI, production | Không chạy |

Không cộng hai filter .NET thành một tổng. 10.5.23 là phiên bản server MariaDB, không phải phiên bản package Pomelo.

## Giới hạn

- Fact chỉ được trả lời khi evidence có token giá trị gồm chữ và số. Tên thủ đô viết thường trong câu văn vẫn bị từ chối.
- Thuộc tính dài hơn ba token chưa được nhận là fact.
- Bộ không thấy vẫn không `Answered`, chưa có câu báo riêng.
- Trần exact/phrase/broad vẫn 25/20/100.
- Bản dịch Pomelo không phải chứng nhận query đã chạy trên MariaDB.
- Chưa có BM25, citation bền hay UI phase sau.

## Trạng thái cuối

Phase 1: **AWAITING_REVIEW**. Phase 2–7: **LOCKED**.

```text
Phase 1 fix round 4 đang AWAITING_REVIEW. Handoff: docs/feature-audits/hybrid-rag/phase-1/fix-round-4-handoff.md. Manifest: docs/feature-audits/hybrid-rag/phase-1/scope-manifest.json. Nhánh feature/hybrid-rag-chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69, chưa commit. F01/F04 đã đỏ rồi xanh trên SQLite và boundary local. Oracle Kenya chỉ nhắc capital đã được sửa. F02/F03/F05 và card:{id} được giữ. Phase 2–7 vẫn LOCKED. Nhờ Codex review vòng 5, chưa tự duyệt.
```
