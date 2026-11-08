# Phase 1 — Codex review vòng 2

Ngày 2026-11-06. Kết luận: **CHANGES_REQUESTED**. Phase 0 giữ APPROVED;
Phase 2–7 giữ LOCKED. Review này không sửa application code và không duyệt Phase 1.

## Snapshot và quyền

- Owner trả `fix-round-1-handoff.md` để review, không giao Codex triển khai sửa.
- Nhánh thực tế: `feature/hybrid-rag-chatbot`.
- HEAD đầu/cuối: `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- Manifest vòng sửa 1: **37/37 digest entry khớp**. Tính lại fingerprint bằng
  path/status/SHA-256, sắp ordinal, delimiter TAB và LF thật, một LF cuối:
  `606fc4edf058543087e93a0a689a3e08f1c0ad31d53f6a3311d886c8e16ee1b1`.
- Kiểm lại 12 entry Phase 0 không bị Phase 1 thay thế: đều khớp. Bug log đã được
  Phase 1 sửa và nằm trong manifest hiện tại; không đòi hash lịch sử của nó khớp.
- Fixture canonical UTF-8/LF vẫn là
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Giữ grant DI hẹp đã ghi trong review-state. `Program.cs` vẫn chỉ thêm một
  registration scoped cho boundary chat; global `IAiProviderClient` không đổi.
  Không có appsettings/project/model/schema/shared-content change trong vòng này.
- Không gọi API trả phí, đọc key, reset DB, chạy remote CI, commit/push/PR/deploy.
  Chỉ ghi report, repair plan/prompt, state và probe trong scratch ignored.

## Đối chiếu finding vòng trước

| ID | Kết quả review lại | Bằng chứng |
| --- | --- | --- |
| HR-P1-R1-F01 | **Còn mở, sửa một phần** | Câu giá vàng gốc đã từ chối đúng; câu thời tiết tiếng Anh vẫn thành Answered/full từ một thẻ trùng từ |
| HR-P1-R1-F02 | **Còn mở, sửa một phần** | Bare term đã tìm đúng trước hơn 100 broad hits; thêm ngữ cảnh tra thẻ thì exact lại bị bỏ trước cap |
| HR-P1-R1-F03 | **Đã xác minh đóng** | Test SQLite bao gồm `go`, câu help với `go`/`up`, control `forecast` và stopword `la` đều pass; token ngắn chỉ vào exact tier |
| HR-P1-R1-F04 | **Còn mở, sửa một phần** | Card identity đã dùng `card:{CardId}`, hai bộ có clarification và specified-set/comparison pass; bốn bộ thì bộ được chỉ định bị cắt trước selection |

Không tạo ID mới cho ba biểu hiện cùng root cause/acceptance đang sửa dở.
Không coi việc handoff đã liệt kê giới hạn là owner chấp thuận miễn acceptance.

## HR-P1-R1-F01 — P1 — Câu fact ngoài nền tảng vẫn được nâng thành full answer

Nguồn hiện tại:

- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs:15–25`:
  policy chỉ nhận một danh sách cụm tiếng Việt; từ weather/today không được nhận.
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs:44–54,128–133,175–190`:
  OutOfScope được thêm CardLookup; bất kỳ card hit có thể đổi intent và outcome.

Reproduction độc lập trên SQLite:

1. User Standard 1 có một thẻ `weather`, definition `STORED_WEATHER_MEANING`.
2. Gọi boundary thật với `What is the weather today?`.
3. Classifier trả `OutOfScope`, confidence `0.25`; boundary trả **Answered/full**,
   `card:2`, text là định nghĩa đã lưu của weather.

Expected: từ chối yêu cầu thời tiết hiện tại ngoài nền tảng; không gọi đó là trả lời
đầy đủ bằng định nghĩa. Observed không phải chatbot bịa dự báo thời tiết: lỗi đã
chứng minh là dùng evidence sai loại cho fact được hỏi và gắn nhãn full.

Control cùng probe: câu giá vàng cũ đã OutOfScope; `vang nghia la gi` và
`zephyr-window` vẫn trả nghĩa đúng. Không sửa bằng cách chặn toàn bộ unknown intent.
Root cause đã xác nhận: blacklist theo chủ đề/cụm chưa kiểm chứng rằng thẻ hỗ trợ
đúng loại yêu cầu. Cần giữ fallback tra học liệu, đồng thời kiểm tra lookup/fact
intent; thêm vài từ vào blacklist đơn thuần không khép nguyên nhân này.

## HR-P1-R1-F02 — P1 — Exact tier bỏ token đúng khi câu hỏi có ngữ cảnh

Nguồn: `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs:24–58`
và `BuildExactNeedles:128–153`, nhất là dòng 135–137. Exact needles chỉ gồm term
đầu, cụm có khoảng trắng và token đúng hai ký tự; token đơn `forecast` không được
đưa vào exact tier khi câu lọc ra nhiều token. Phrase tier chỉ thử cụm đầu.

Reproduction độc lập trên SQLite:

1. Seed 120 thẻ `aaa-001`…`aaa-120`, definition `forecast is merely mentioned here`.
2. Thêm thẻ đúng `forecast`, definition `EXACT_FORECAST_TARGET`, CardId 1001.
3. Query `forecast`: có thẻ đúng đầu tiên, priority 100.
4. Query **`Giải thích forecast trong bộ thẻ của tôi`**: chỉ trả aaa-001/002/003,
   priority 40; `forecast` mất khỏi kết quả.

Expected: cùng yêu cầu tra nghĩa phải tìm thẻ đúng trước broad definition hits.
Observed: thẻ đúng không vào exact/phrase tiers, broad tier cắt 100 trước scoring.
Đây là cùng lỗi relevant-candidate-before-cap của F02, không phải yêu cầu BM25.
Giữ truy vấn có trần và ACL; sửa parsing/exact-tier coverage phù hợp với các câu
tra cứu hợp lệ, không tải toàn bộ thẻ hoặc chỉ tăng trần cho fixture này.

## HR-P1-R1-F04 — P2 — Chọn bộ sau Take(3) làm mất bộ đã chỉ định

Nguồn:

- `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs:74–78`:
  sắp theo Term/SetName/CardId rồi `Take(3)`.
- `TCTEnglish/Services/AI/Internal/CardLookupSelection.cs:45–57`:
  chọn set name chỉ trong các snippet còn lại sau trần.

Reproduction độc lập trên SQLite:

1. User 1 có bốn bộ Alpha, Beta, Gamma, Sporting; mỗi bộ chứa một thẻ `match`.
2. Sporting có definition `SPORTING_TARGET_MEANING`.
3. Hỏi **`nghia cua match trong bo Sporting`**.
4. Boundary trả **NeedsClarification**, hỏi “bộ nào?” nhưng chỉ liệt kê
   Alpha/Beta/Gamma; không còn Sporting hoặc `card:4` trong evidence.

Expected: trả nghĩa trong Sporting vì user đã chỉ định một bộ được phép xem.
Root cause: qualifier chưa tham gia chọn candidate trước các cap; selection
downstream không thể khôi phục card bị loại. Cần áp dụng qualifier đã xác định vào
truy vấn/ranking trước trần, vẫn kiểm quyền. Không khép lỗi bằng tăng Take(3).

Phần identity đã đạt: snippet có EntityId/ContainerId do server lấy từ DB, key
`card:{id}` phân biệt hai card cùng term; test gọi lại giữ key. Phải giữ phần này.

## Kiểm tra độc lập và giới hạn

| Kiểm tra | Kết quả thực tế |
| --- | --- |
| `dotnet test ... -c Phase1ReviewRound2` với filter chat/context/conversation/quota/cancel/history/closeout/consumer/retriever/composer/fixture trong repair plan | **194 pass, 0 fail, 0 skip**, khoảng 26 s; build cả app/test thành công |
| `dotnet test ... -c Phase1ReviewRound2 --no-build` filter OpenAiProviderClientTests, MlNet, CursorReviewProbeTests, RepairReviewProbeTests, ThirdReviewProbeTests | **65 pass, 0 fail, 0 skip**, khoảng 27 s |
| Bốn regression mới của Cursor | Có trong TRX và pass; các assertion đúng cho fixture đang dùng, nhưng chưa bao phủ ba biến thể trên |
| Probe độc lập InMemory rồi SQLite với boundary/retrievers/composer production | Cùng 7 kết quả, gồm 3 lỗi và các control; không dùng lifecycle stub hoặc API provider |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0, Agent tooling verified |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests, 134 pass, 1 skip, khoảng 83 s |
| Manifest và predecessor | 37/37 hiện tại; 12/12 predecessor còn hiệu lực; fixture hash không đổi |

Không cộng hai filter .NET thành một số test duy nhất. Build có warning nullable
và analyzer đã hiển thị; không có build error. Tooling checks không chứng nhận
runtime chatbot hay giao diện.

Evidence local ignored tại `.tmp/hybrid-rag-review/phase-1/round-2/`:
`phase1-review2.trx`, `phase1-extra-review2.trx`, `Probe.ps1`, `Probe-Sqlite.ps1`,
`behavior-probes.json`, `behavior-probes-sqlite.json`. Runner SQLite dùng helper
test hiện có, database in-memory dùng một lần và toàn bộ 12 retriever production;
guide đọc từ đường dẫn source thật. Probe dừng nếu lỗi setup; exit 0, các control
và cùng kết quả trên hai provider xác nhận reproduction hợp lệ.

Không chạy browser, API trả phí, MariaDB disposable, CI/staging/production. Chưa
có bằng chứng Pomelo translation từ handoff; không suy từ SQLite ra hosting.
Không có persisted-model change nên không yêu cầu migration rỗng. Vòng sửa sau
cần kiểm tra translation Pomelo của query thay đổi hoặc disposable MariaDB theo
plan; đây là giới hạn bằng chứng, không phải một lỗi SQL đã được chứng minh.

## Handoff

Repair plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-2.md`.
Execution prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-2.md`.
Giữ F01/F02/F04 mở, F03 đóng; Phase 1 CHANGES_REQUESTED; Phase 2–7 LOCKED.
Cursor cần owner khởi chạy vòng sửa, trả AWAITING_REVIEW để Codex review vòng 3.
