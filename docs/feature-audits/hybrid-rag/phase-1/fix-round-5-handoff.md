# Phase 1 — handoff sửa vòng 5

Trạng thái: **AWAITING_REVIEW**. Chưa được duyệt. Phase 0 vẫn APPROVED. Phase 2–7 vẫn LOCKED. F02, F03, F04, HR-P1-R3-F05 và identity `card:{id}` đã được review trước xác minh; vòng này giữ các hồi quy đó. Agent không tự đóng finding và không mở Phase 2.

## Phạm vi

- Vòng sửa HR-P1-R1-F01. Plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-5.md`. Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-5.md`. Review: [review-round-5.md](review-round-5.md).
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, 44 digest entry khớp `26a1d1a138ba648137ea3c85b9d1fdfe8e221efe65ccc615f9b04bcf41ebf73e`. Fixture Phase 0 vẫn `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Fingerprint tích lũy sau vòng này, chưa duyệt: `2f8ca1d0d125a456703985a5f8606242f53548de4704218f4a9557e378f2d16d` (46 entry).
- Không sửa `Program.cs`, `appsettings.json`, `.csproj`, model hay schema. `IAiProviderClient` không đổi. Không API trả phí, CI, push, PR hay deploy.
- Manifest tích lũy: [scope-manifest.json](scope-manifest.json).

## Quyết định loại câu trả lời

Câu full từ thẻ chỉ khi câu là tra nội dung đã lưu của đúng mục tiêu: từ hoặc cụm ngắn, hỏi nghĩa đã lưu, giải thích từ trong bộ, kiểm tra từ trong notes, hoặc so sánh các bộ. Hướng dẫn nền tảng và dữ liệu của chính người dùng vẫn đi theo retriever tương ứng.

Một hit lexical được phép xem chỉ là ứng viên. Câu fact hoặc câu chưa nhận dạng không được `Answered`/`full` từ definition của thẻ. Heuristic token chữ-số đã được gỡ. Không thêm blacklist Unit/Lesson, prefix lịch sự hay chuỗi phủ định.

## Từng finding

### HR-P1-R1-F01

Trước khi sửa, hai test trên `Phase1FixRound5` đỏ (2 fail, 0 pass, khoảng 9 s):

- `What is the capital of Peru?` với definition `capital: LIMA9` vẫn `Answered`.
- France definition đúng `a country; its capital is not given in Unit12`, câu `What is the capital of France?` vẫn `Answered`. Assertion dừng ở hai điểm này, nên các câu lịch sự và Lesson4 trong cùng test chưa chạy tới.

Sau khi sửa, cùng hai test xanh (2 pass, khoảng 3 s), gồm đủ các nhánh:

- `What is the capital of France?` và `Can you tell me the capital of France?` với definition Unit12: không `Answered`, scope không phải `full`, text không chứa `not given in Unit12`, evidence rỗng.
- Spain `a country in Europe; its population is not listed in Lesson4`: cả `What is the population of Spain?` và `Could you tell me the population of Spain?` đều không full, text không chứa `Lesson4`. Đổi mã tham chiếu và đổi cách hỏi không tạo thêm support.
- France chỉ có definition `a European country`: cả câu trực tiếp và `Can you tell me the capital of France?` đều không full và không đưa definition đó ra như câu trả lời fact.
- `what does France mean`, `what does Spain mean` và `what does Peru mean` vẫn `Answered`/`full` với đúng definition đã lưu. Bare `France` và `Please check zephyr-window against my notes from class` vẫn trả nội dung thẻ.
- France/Nhật/Kenya/tiếng Việt, thời tiết hôm nay và giá vàng vẫn không full.

Oracle `LIMA9` của vòng 4 là control tổng hợp, không phải yêu cầu nghiệp vụ. `What is the capital of Peru?` không còn full và không đưa `LIMA9` ra. `what does Peru mean` trả definition đã lưu, kể cả đoạn chữ `capital: LIMA9`, vì người dùng hỏi nghĩa đã lưu.

## Kiểm tra

| Việc | Kết quả |
| --- | --- |
| Hai hồi quy trước khi sửa, `Phase1FixRound5` | 2 fail, 0 pass, khoảng 9 s |
| Sau sửa: hai hồi quy fact | 2 pass, 0 fail, khoảng 3 s |
| Filter plan chat/history/quota/cancel/retriever/composer/fixture, TRX `.tmp/hybrid-rag-phase1-fix5/phase1-fix5.trx` | 203 pass, 0 fail, khoảng 23 s |
| `--no-build` OpenAiProviderClientTests, MlNet, ba probe review, TRX `phase1-fix5-extra.trx` | 65 pass, 0 fail, khoảng 19 s |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0 |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests, OK, skipped=1, khoảng 82 s |
| `git diff --check` | exit 0 |
| Pomelo `ServerVersion.Parse("10.5.23-mariadb")` trên `BoundedExact`/`BoundedPhrase`/`BoundedBroad` | Test translation cũ nằm trong filter xanh. Query builder không đổi: `LIMIT 25/20/100`, `LOWER`, `REPLACE`, cột set id. Không mở kết nối |
| MariaDB disposable, trình duyệt, API trả phí, CI, production | Không chạy |

Không cộng hai filter .NET thành một tổng. 10.5.23 là phiên bản server MariaDB, không phải phiên bản package Pomelo. 203 gồm 202 test của vòng 4 và 1 test mới.

F02 forecast exact, F03 `go`/`up`, F04 tên bộ đầy đủ/ngoặc/Unicode/prefix/so sánh/Echo, F05 bare weather, mật khẩu, từ chối credential, thu hồi lớp và `card:{id}` nằm trong filter xanh.

## Giới hạn

- Phase 1 không trả fact tự do từ prose của thẻ. Mã bài học, token chữ-số và câu lịch sự không cấp quyền `Answered`/`full`.
- Hỏi nghĩa đã lưu vẫn được trả đúng definition, kể cả khi definition chứa một ghi chú fact.
- Câu từ ngắn tối đa 3 token, không phải câu hỏi chức năng, vẫn là tra từ. Đó là hợp đồng bare term đã có.
- Trần exact/phrase/broad vẫn 25/20/100. ACL và identity thẻ không đổi.
- Grounding fact đầy đủ thuộc Phase 4, đang LOCKED.
- Bản dịch Pomelo không phải lần chạy trên MariaDB.

## Trạng thái cuối

Phase 1: **AWAITING_REVIEW**. Phase 2–7: **LOCKED**.

```text
Phase 1 fix round 5 đang AWAITING_REVIEW. Handoff: docs/feature-audits/hybrid-rag/phase-1/fix-round-5-handoff.md. Manifest: docs/feature-audits/hybrid-rag/phase-1/scope-manifest.json. Nhánh feature/hybrid-rag-chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69, chưa commit. Fingerprint 2f8ca1d0d125a456703985a5f8606242f53548de4704218f4a9557e378f2d16d, 46 entry. F01 đã đỏ rồi xanh trên SQLite: Unit12, Lesson4 và câu lịch sự không còn full; LIMA9 không còn là fact dương. F02/F03/F04/F05 và card:{id} được giữ. Phase 2–7 vẫn LOCKED. Nhờ Codex review vòng 6, chưa tự duyệt.
```
