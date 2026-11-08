# Phase 1 — Codex review vòng 3

Ngày 2026-11-06. Kết luận **CHANGES_REQUESTED**. Phase 0 giữ APPROVED;
Phase 2–7 giữ LOCKED. Codex chỉ review và tạo tài liệu điều phối, không sửa app.

## Snapshot được kiểm

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`; chưa commit.
- Handoff: `fix-round-2-handoff.md`; manifest round `fix-2` có **39/39 entry khớp**.
- Fingerprint tính lại bằng sorted path/status/SHA-256, delimiter TAB/LF thật và
  một LF cuối: `f513c62c6fdff3c12db7516cf8c5e1f0616212522b276caaa06b3d31e746435e`.
- 12 entry Phase 0 không bị phase sau thay thế vẫn khớp. Bug log đã nằm trong
  snapshot mới. Fixture canonical UTF-8/LF giữ
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Giữ grant Program.cs DI hẹp đã ghi trong state. Diff Program.cs vẫn chỉ thêm
  registration scoped cho chat boundary. Không có sửa project/appsettings,
  persisted model/schema hoặc shared content. Không cần migration rỗng.
- Không chạy paid API, ghi secret, reset DB, remote CI/Git hoặc deploy. Probe dùng
  SQLite in-memory dùng một lần, dữ liệu synthetic; không tác động Docker/dev DB.

## Đối chiếu các finding trước

| ID | Kết quả | Bằng chứng |
| --- | --- | --- |
| HR-P1-R1-F01 | **Còn mở** | Câu weather today và giá vàng đã từ chối đúng; câu fact không có mốc thời gian vẫn được nâng thành full answer từ thẻ không hỗ trợ fact đó |
| HR-P1-R1-F02 | **Đã xác minh đóng** | Bare forecast và câu có ngữ cảnh đều có exact priority 100 trước 120 broad definition hits; boundary test cũng trả đúng definition |
| HR-P1-R1-F03 | **Tiếp tục đóng** | Regression SQLite go/up, help wording, forecast và stopword pass |
| HR-P1-R1-F04 | **Còn mở** | Sporting qua bốn bộ/30 duplicate và comparison đã đạt; tên nhiều từ bị cắt thành prefix và có thể trả full từ bộ khác |

Phần identity của F04 vẫn đạt: `card:{CardId}`, EntityId/ContainerId từ DB và key
ổn định. Có thêm một regression mới **HR-P1-R3-F05** do lọc tên bộ trên mọi token.

## HR-P1-R1-F01 — P1 — Fact không có mốc hiện tại vẫn dùng definition làm full answer

Nguồn:

- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs:17–28,67–94`:
  ngoài danh sách chủ đề cố định, policy chỉ nhận fact có aboutNow.
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs:44–54,128–133,175–190`:
  unknown/OutOfScope tiếp tục được nâng thành CardLookup/Answered khi có card hit.

Reproduction SQLite với toàn bộ 12 retriever production:

1. User 1 sở hữu thẻ `France`, definition `STORED_FRANCE_COUNTRY_DEFINITION`.
   Definition không có dữ kiện trả lời câu hỏi thủ đô.
2. Hỏi **`What is the capital of France?`**.
3. Classifier: OutOfScope, confidence 0.25. Boundary: **Answered/full**, `card:1`,
   trả định nghĩa của France.
4. Control `what does France mean` cũng trả cùng definition; control này hợp lệ.

Expected: khi evidence không hỗ trợ fact được hỏi, từ chối hoặc hỏi rõ ý định tra
nghĩa, không coi token trùng là đã trả lời đủ. Observed không phải app bịa tên thủ
đô; lỗi là gắn full cho nội dung không trả lời yêu cầu. Không cần web/API để chứng
minh sự thiếu dữ kiện trong synthetic source.

Root cause vẫn là thiếu kiểm tra loại yêu cầu và phần câu hỏi được evidence hỗ
trợ. Sửa temporal frame đã khép reproduction thời tiết, nhưng không khép acceptance
F01. Không sửa bằng blacklist France/capital hoặc chặn mọi low-confidence lookup.

## HR-P1-R1-F04 — P1 — Tên bộ nhiều từ bị đổi sang bộ có tên là prefix

Nguồn: `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs:232–248`
lấy đúng một token sau marker; `MatchAccessibleSetNamesAsync:255–273` so sánh từng
token với toàn bộ tên bộ; `RetrieveAsync:77–88` dùng tên đó lọc trước mọi candidate.

Reproduction SQLite:

1. User 1 có bộ **Daily Words**, thẻ forecast/`TARGET_DAILY_WORDS_MEANING`;
   và bộ **Daily**, thẻ forecast/`WRONG_PREFIX_SET_MEANING`.
2. Hỏi **`nghia cua forecast trong bo Daily Words`**.
3. Boundary trả **Answered/full từ Daily**, chỉ `card:2`, không có meaning của
   Daily Words. Control chỉ định Daily trả card:2 đúng như yêu cầu.

Expected: chọn tên đầy đủ Daily Words; nếu không resolve được thì clarify/refuse,
không tự đổi nguồn sang prefix. Handoff đã ghi giới hạn multiword, nhưng owner
chưa miễn acceptance này. Đây là tên bộ thông thường trong nền tảng, cùng hợp đồng
specified-set của F04. Priority nâng từ P2 lên **P1** vì đã chứng minh full answer
nhầm nguồn, thay vì chỉ thiếu kết quả trong danh sách clarification.

## HR-P1-R3-F05 — P1 — Từ cần tra bị suy thành qualifier của bộ không được chỉ định

Nguồn: `CardLookupRetriever.cs:77–88,255–273`. `matchedSets` luôn được lấy từ mọi
token và áp vào query, kể cả khi `explicitSet` là null. Một từ trùng tên bộ tự trở
thành hard filter và loại thẻ exact trong bộ khác.

Reproduction SQLite, cùng user và cùng query **`weather`**:

1. Bộ Nature có thẻ weather/`CORRECT_EXACT_WEATHER_MEANING`.
   Boundary trả đúng weather, `card:1`, Answered/full.
2. Thêm bộ Weather chứa thẻ cloud, definition `weather patterns mentioned in
   another card`. Không sửa thẻ weather/Nature hoặc câu hỏi.
3. Boundary chuyển sang **cloud trong Weather**, `card:2`, Answered/full.
   Từ cần tra và exact card ban đầu vẫn tồn tại, được phép xem.

Expected: lookup không chỉ định bộ vẫn xét mọi nguồn được phép và giữ exact
priority; thêm metadata/tập nội dung khác không được ép một qualifier mới.
Impact: thêm/đổi tên một bộ có thể phá tra từ đang hoạt động và làm trả sai term.
Không có bằng chứng IDOR trong ca này: cả hai bộ đều của user; đây là lỗi relevance.

Đã chạy cùng probe trên binary snapshot review vòng 2 trước sửa: cả trước và sau
khi thêm bộ Weather đều trả đúng weather/card:1. Vì vậy đây là regression được
giới thiệu bởi vòng sửa 2, được theo dõi bằng ID mới, không mở lại F02 đã sửa đúng.

## Kiểm tra độc lập

| Việc | Kết quả |
| --- | --- |
| Build/test cấu hình `Phase1ReviewRound3`, filter chat/context/conversation/quota/cancel/history/closeout/consumer/retriever/composer/fixture | **198 pass, 0 fail, 0 skip**, khoảng 26 s; build app/tests thành công |
| `--no-build`, filter OpenAiProviderClientTests, MlNet và ba suite review probe | **65 pass, 0 fail, 0 skip**, khoảng 18 s |
| Probe SQLite trên binary hiện tại, boundary cùng 12 retriever production | 13 quan sát gồm ba lỗi trên và controls; ba reproduction vòng 2 đã đạt |
| Probe so sánh binary review vòng 2 | Xác minh regression F05; không tính kết quả binary cũ vào acceptance bản hiện tại |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0, Agent tooling verified |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests: 134 pass, 1 skip, khoảng 67 s |
| Manifest/predecessor/whitespace | 39/39 hiện tại, 12/12 predecessor còn hiệu lực; `git diff --check` pass |

Không cộng hai filter .NET thành số test duy nhất. Build vẫn có warning nullable
và analyzer; không có build error. Hook/tooling suite không chứng nhận app/browser.

Test `CardLookup_BoundedFilters_TranslateOnPomeloMariaDbProvider` có trong lượt 198
và pass. Nó dùng Pomelo hiện tại, `ServerVersion.Parse("10.5.23-mariadb")`, kiểm
translation LOWER/REPLACE/LIMIT của query exact/phrase mẫu, không mở kết nối.
**10.5.23 là phiên bản server MariaDB, không phải phiên bản package Pomelo.**
Không suy translation mẫu thành bằng chứng mọi production query đã chạy trên DB.
Không có lỗi SQL/provider thực thi được khẳng định trong review này.

Evidence ignored: `.tmp/hybrid-rag-review/phase-1/round-3/` gồm
`phase1-review3.trx`, `phase1-extra-review3.trx`, `Probe-Sqlite.ps1`,
`behavior-probes-sqlite.json`, `Probe-PriorSnapshot.ps1`,
`comparison-prior-review-snapshot.json`. Runner hiện tại load cấu hình Round3;
runner so sánh có tên riêng và load Round2. Cả hai exit 0, dùng source guide path
thật và test SQLite helper; không thay production code hoặc assertion của Cursor.

Không chạy browser, MariaDB disposable, paid API, CI/staging/production. Chưa có
BM25/vector/generation hoặc citation bền; các mục đó thuộc phase chưa mở.

## Handoff và gate

- Repair plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-3.md`.
- Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-3.md`.
- Mở: HR-P1-R1-F01, HR-P1-R1-F04, HR-P1-R3-F05.
- Đóng: HR-P1-R1-F02, HR-P1-R1-F03; giữ phần identity đã đạt của F04.
- Phase 1 CHANGES_REQUESTED; Phase 2–7 LOCKED. Cursor sửa vòng 3 rồi trả
  AWAITING_REVIEW cho Codex review vòng 4. Không có authority triển khai fix trong
  lượt review hiện tại, commit hoặc release.
