# Phase 1 — Codex review vòng 5

Ngày 2026-11-06. Kết luận **CHANGES_REQUESTED**. Chỉ còn
**HR-P1-R1-F01 (P1)** chưa khép. F04 được xác minh đóng; F02/F03/F05 và identity
được giữ. Phase 0 APPROVED, Phase 2–7 LOCKED. Không sửa application code.

## Snapshot và phạm vi

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`; chưa commit.
- Handoff `fix-round-4-handoff.md`, manifest `fix-4`: **44/44 entry khớp**.
- Fingerprint tính lại bằng ordinal path/status/SHA-256, delimiter TAB/LF thật và
  một LF cuối: `26a1d1a138ba648137ea3c85b9d1fdfe8e221efe65ccc615f9b04bcf41ebf73e`.
- 12 entry Phase 0 còn hiệu lực khớp. Bug log được phase sau thay thế có digest
  trong snapshot mới. Không có app/test thay đổi ngoài hai manifest có hiệu lực.
- Canonical fixture hash đã tính lại và giữ
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Grant DI hẹp được giữ; Program.cs vẫn chỉ thêm scoped chat boundary. Không sửa
  appsettings/project/model/schema/shared content. Không cần migration rỗng.
- Không paid API, secret write, dev DB reset, remote Git/CI/deploy. Probe dùng
  SQLite in-memory synthetic, output riêng Round5; chỉ ghi report/plan/prompt/state.

## Đối chiếu finding

| ID | Kết quả | Bằng chứng |
| --- | --- | --- |
| HR-P1-R1-F01 | **Còn mở, P1** | Các reproduction vòng 4 đã pass, nhưng mã bài có chữ+số bị coi là giá trị thuộc tính; câu hỏi lịch sự đi qua nhánh không nhận diện rồi thành full answer |
| HR-P1-R1-F02 | Giữ đóng | Exact/contextual forecast trước hơn 100 broad hits; regression boundary pass |
| HR-P1-R1-F03 | Giữ đóng | go/up/help/control/stopword pass |
| HR-P1-R1-F04 | **Đã xác minh đóng** | Probe quoted/unquoted Daily Study chọn card:1; Daily chọn card:2; SQLite test Study/Reading Lab, full-name/Unicode/prefix/ambiguity/comparison pass |
| HR-P1-R3-F05 | Giữ đóng | Bare weather trước/sau thêm bộ Weather vẫn lấy Nature/weather/card:1 |

Oracle Kenya đã được sửa đúng: câu chỉ nói capital city được ghi ở đâu đó không
còn được coi là full answer. Source identity vẫn lấy card ID từ server.
Không tạo finding mới cho các đường đi chưa khép của F01.

## HR-P1-R1-F01 — P1 — Heuristic chữ+số và nhánh không nhận diện vẫn cho phép full answer thiếu evidence

Nguồn hiện tại:

- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs:183–201`:
  HasExplicitAttributeValue chỉ tìm một token dài ít nhất 4, có chữ và số, trong
  sáu token sau attribute. Không chứng minh token đó là giá trị của thuộc tính.
- Cùng file `125–150`: left side dài hơn ba token không được nhận là fact;
  `44–50` còn trả true cho support khi không nhận diện.
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs:47–54,67–101,145–150,192–207`:
  source support check chỉ áp dụng với fact được nhận diện. Nhánh còn lại vẫn có
  thể nâng một card hit thành CardLookup/Answered/full.

### Reproduction A — Mã bài không phải câu trả lời

1. User 1 có thẻ France, definition
   `a country; its capital is not given in Unit12`.
2. Hỏi `What is the capital of France?`.
3. Boundary trả **Answered/full**, `card:1`, text là definition trên.
4. Trong cùng runner, definition thiếu dữ kiện không có Unit12 đã bị từ chối đúng.
   Control `what does France mean` vẫn trả definition như yêu cầu tra nghĩa.

Unit12 là tham chiếu bài học; thêm nó không bổ sung thủ đô. Chữ+số không phải bằng
chứng về một giá trị hay quan hệ. Handoff có khai báo heuristic này, nhưng không
có owner waiver cho unsupported full answer.

### Reproduction B — Thêm câu dẫn làm bypass support gate

1. User 1 có thẻ France, definition chỉ `a European country`.
2. `What is the capital of France?` trả OutOfScope đúng.
3. **`Can you tell me the capital of France?`** lại trả **Answered/full**, `card:1`,
   text là `a European country`.
4. Control `what does France mean` trả definition đúng loại yêu cầu.

Evidence/user/quyền không đổi. Chỉ cách diễn đạt thay đổi làm bộ nhận diện trả
false và đi qua fallback. Đây là câu hỏi thông thường, cùng acceptance F01 đã có.

Expected cho cả hai: refuse/clarify/insufficient phù hợp contract, scope không full
và text không trình bày định nghĩa như câu trả lời cho fact. Không yêu cầu bot
tìm tên thủ đô từ kiến thức ngoài, cũng không có tên thủ đô bịa trong output.
Lỗi đã chứng minh là dùng source không trả lời câu hỏi và gắn nhãn full.

Root cause: full answer vẫn là kết quả mặc định của card hit khi một heuristic
không chặn, thay vì chỉ được phép sau quyết định loại câu hỏi/evidence tích cực.
Round 4 đã cho phép baseline thận trọng; không cần tiếp tục xây parser fact tự do
cho Phase 1. Sửa bằng danh sách Unit/Lesson hoặc thêm một polite prefix không khép
nguyên nhân. Plan vòng 5 ưu tiên bỏ nhánh suy fact từ free text, chỉ trả full cho
các loại trả lời nội bộ baseline xác định được; unknown cần clarify/refuse.

## Kiểm tra độc lập

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet test ... -c Phase1ReviewRound5`, filter chat/context/conversation/quota/cancel/history/closeout/consumer/retriever/composer/fixture | **202 pass, 0 fail, 0 skip**, khoảng 29 s; app/test build thành công |
| `--no-build`, filter OpenAiProviderClientTests, MlNet và ba suite review probe | **65 pass, 0 fail, 0 skip**, khoảng 17 s |
| Probe SQLite trên production boundary và 12 retriever | **24 quan sát**, gồm các ca cũ đã đạt, hai lỗi A/B và các control |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0, Agent tooling verified |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests: 134 pass, 1 skip, khoảng 69 s |
| Manifest/predecessor/fixture/whitespace | 44/44; 12/12 còn hiệu lực; hash fixture giữ nguyên; git diff --check pass |

Skip vẫn là local-media non-Windows rejection contract trên host Windows; không
tính là pass. Không cộng hai filter .NET thành số test duy nhất. Build có warning
nullable/analyzer, không có build error. Static/tooling không chứng nhận app/UI.

Test translation Pomelo trên production BoundedExact/BoundedPhrase/BoundedBroad
vẫn pass trong filter chính, target server MariaDB 10.5.23; không mở kết nối và
không thực thi MariaDB. Query shapes không đổi trong vòng sửa này.

Evidence ignored: `.tmp/hybrid-rag-review/phase-1/round-5/phase1-review5.trx`,
`phase1-extra-review5.trx`, `Probe-Sqlite.ps1`, `behavior-probes-sqlite.json`.
Runner load đúng binary Round5, source guide path thật và SQLite helper synthetic;
exit 0. Không sửa source/assertion để lấy kết quả review.

Không chạy browser, MariaDB disposable, paid API, CI/staging/production. Chưa có
BM25/vector/generation/citation bền; không tự mở phase để xử lý finding này.

## Handoff và gate

Plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-5.md`.
Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-5.md`.
Chỉ F01 còn mở; F02/F03/F04/F05 đã đóng và cần giữ hồi quy. Phase 1
CHANGES_REQUESTED, Phase 2–7 LOCKED. Cursor sửa vòng 5, trả AWAITING_REVIEW cho
Codex review vòng 6. Không có authority sửa app trong lượt review hiện tại.
