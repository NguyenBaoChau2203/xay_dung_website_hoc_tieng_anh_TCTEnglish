# Phase 1 — Codex review vòng 4

Ngày 2026-11-06. Kết luận **CHANGES_REQUESTED**. Phase 0 giữ APPROVED;
Phase 2–7 giữ LOCKED. Chỉ review và ghi tài liệu, không triển khai fix app.

## Snapshot và quyền

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`; chưa commit.
- Handoff `fix-round-3-handoff.md`, manifest `fix-3`: **42/42 entry khớp**.
  Fingerprint tính lại bằng ordinal path/status/SHA-256, TAB/LF thật, một LF cuối:
  `ba644991f0284ee7a0b43eac3e1a5670b492f2b5941da0506be9b1b024bb502d`.
- 12 entry predecessor Phase 0 còn hiệu lực khớp; bug log đã nằm trong phase mới.
  Không có app/test thay đổi nào nằm ngoài manifest hiện tại hoặc predecessor.
- Fixture canonical UTF-8/LF đã tính lại, vẫn là
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Giữ grant DI hẹp đã ghi trong state. Diff Program.cs chỉ có registration scoped
  chat boundary. Không có thay đổi appsettings/project/model/schema/shared content.
- Không paid API, secret write, Docker/dev DB reset, remote Git/CI/deploy. Probe
  dùng SQLite in-memory synthetic và output cấu hình riêng cho vòng 4.

## Finding cũ sau review lại

| ID | Kết quả | Bằng chứng |
| --- | --- | --- |
| HR-P1-R1-F01 | **Còn mở, P1** | Plain country definition bị từ chối đúng với câu English cũ, nhưng chỉ thêm từ capital vào câu nói thiếu dữ kiện là lại Answered/full; cách hỏi tiếng Việt cũng bypass kiểm tra |
| HR-P1-R1-F02 | **Giữ đóng** | Bare/contextual forecast có exact priority 100 trước hơn 100 broad hits; hồi quy boundary pass |
| HR-P1-R1-F03 | **Giữ đóng** | go/up/help wording/control và stopword pass |
| HR-P1-R1-F04 | **Còn mở, P1** | Daily Words/Unicode/duplicate/comparison đã đạt; tên có từ Study ở cuối lại bị cắt và đổi sang bộ Daily |
| HR-P1-R3-F05 | **Đã xác minh đóng** | Bare weather trước/sau thêm bộ Weather đều trả exact weather trong Nature/card:1; không còn hard filter theo token trùng tên bộ |

Identity `card:{CardId}` được giữ. Không thêm ID mới cho hai biểu hiện cùng root
cause/acceptance chưa khép của F01/F04. Các ca lỗi vòng 3 đã pass trên bản mới.

## HR-P1-R1-F01 — P1 — Có từ khóa thuộc tính chưa chứng minh có câu trả lời

Nguồn hiện tại:

- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs:44–59`: evidence được coi
  là hỗ trợ chỉ vì chứa attribute và entity; không có kiểm tra giá trị trả lời.
- Cùng file `31–41,62–76,128–151`: chỉ nhận khung English cụ thể; `la gi` được
  coi là tra nghĩa bất kể cả câu đang hỏi thuộc tính nào.
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs:67–101,145–150,192–207`:
  check chỉ chạy cho khung nhận diện; card còn lại được nâng thành full answer.

Reproduction độc lập trên SQLite với toàn bộ 12 retriever production:

1. User 1 có thẻ France, definition đúng nguyên văn:
   `a country; its capital is not provided in this card`.
2. Hỏi `What is the capital of France?`.
3. Boundary trả **Answered/full**, evidence `card:1`, text là definition trên.
4. Hỏi `Thủ đô của France là gì?`: cũng **Answered/full**, cùng evidence/text.
5. Control `what does France mean` trả definition như yêu cầu tra nghĩa, hợp lệ.

Expected: thiếu giá trị thủ đô thì refuse/clarify/insufficient theo contract,
không gắn full cho đoạn nguồn xác nhận đang thiếu dữ kiện. Đây không phải yêu cầu
LLM phải suy ra tên thủ đô; cũng không có tên thủ đô bịa trong output. Lỗi là quyết
định evidence support/outcome và đổi câu hỏi fact thành câu trả định nghĩa.

Test mới đang có một oracle sai cần sửa trong cùng finding:
`TCTEnglish.Tests/AiRagBoundaryAccessTests.cs:177–205`, method
`UnsupportedAttributeFact_RefusesUnrelatedDefinitionWhileSupportedAndLookupStillAnswer`.
Definition Kenya là `STORED_KENYA_NOTE the capital city is recorded here`, không
nêu giá trị thủ đô, nhưng dòng 203–205 yêu cầu Answered/full. Test này xác nhận
word overlap, không chứng minh trả lời được câu hỏi. Không giữ oracle đó làm
tiêu chí bắt buộc cho bản sửa tiếp theo.

Root cause đã xác minh: keyword presence và một số frame được dùng thay cho quyết
định một loại câu trả lời có được hỗ trợ. Phase 1 có thể giữ baseline thận trọng
với refuse/clarify khi chưa chứng minh được support; không cần thêm paid/NLI path.
Không khép lỗi bằng regex/chủ đề mới hoặc chỉ lọc chuỗi `not provided` cụ thể.

## HR-P1-R1-F04 — P1 — Stopword của câu tra cứu làm mất một phần tên bộ

Nguồn: `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs:308–349`.
`ReadExplicitLabel` loại label bắt đầu bằng LookupFraming; `TrimLabel` còn xóa
LookupFraming ở cả đầu/cuối tên. Danh sách đó có Study, Class, My, Notes, Reading.
Các từ hợp lệ trong SetName bị xử lý như từ khung của câu hỏi.

Reproduction SQLite:

1. User 1 có Daily Study/forecast/`TARGET_DAILY_STUDY_MEANING` và
   Daily/forecast/`WRONG_DAILY_PREFIX_MEANING`.
2. Hỏi `nghia cua forecast trong bo Daily Study`.
3. Boundary trả **Answered/full từ Daily**, `card:2`, không có target meaning.
4. Hỏi `nghia cua forecast trong bo "Daily Study"`: vẫn trả nhầm Daily/card:2.
5. Control chỉ định Daily trả card:2 đúng như yêu cầu.

Expected: giữ toàn bộ tên Daily Study, resolve đúng SetId trước cap; không được
đổi nguồn sang Daily. Nguồn có thể là tên chứa từ thường, không chỉ Daily Words.
Root cause chính là không tách việc bỏ từ khung của target khỏi bảo toàn label.
LookupFraming hữu ích khi phân tích từ cần tra nhưng không được cắt tên nguồn.
Nhánh generic “trong bộ thẻ của tôi” cần được nhận riêng, không bằng cách xóa
bất kỳ từ nào xuất hiện trong tên bộ. Quote không hiện bảo vệ literal label.

## Kiểm tra độc lập

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet test ... -c Phase1ReviewRound4` filter chat/context/conversation/quota/cancel/history/closeout/consumer/retriever/composer/fixture | **201 pass, 0 fail, 0 skip**, khoảng 27 s; app/test build thành công |
| `--no-build`, filter OpenAiProviderClientTests, MlNet và ba suite review probe | **65 pass, 0 fail, 0 skip**, khoảng 19 s |
| SQLite production boundary/retrievers probe trên binary Round4 | **19 quan sát**: các ca vòng trước đã đạt; keyword-missing-value, câu fact tiếng Việt và hai cách viết Daily Study vẫn lỗi |
| `uv run --no-project python scripts/agent_tooling.py` | exit 0, Agent tooling verified |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 tests: 134 pass, 1 skip, khoảng 67 s |
| Manifest/predecessor/fixture/whitespace | 42/42, 12/12 còn hiệu lực, canonical hash giữ nguyên, `git diff --check` pass |

Skip là `test_local_media.LocalMediaTests.test_unsupported_platform_is_rejected_before_creating_artifacts`,
reason `Non-Windows rejection contract`; checkout chạy Windows. Không tính skip là
pass và không có app test nào bị skip trong hai filter .NET.

Test Pomelo hiện gọi query builder production BoundedExact/BoundedPhrase/BoundedBroad
và ToQueryString với server MariaDB 10.5.23; kiểm trần/source filter/LOWER/REPLACE
pass. Không mở kết nối, không thực thi MariaDB. Không có persisted mapping change,
không cần migration rỗng. Không có SQL/provider runtime bug được khẳng định.

Evidence ignored tại `.tmp/hybrid-rag-review/phase-1/round-4/`:
`phase1-review4.trx`, `phase1-extra-review4.trx`, `Probe-Sqlite.ps1`,
`behavior-probes-sqlite.json`. Runner load đúng output Round4, SQLite helper dùng
database synthetic riêng và source guide path thật. Exit 0; không sửa app/tests.

Không cộng hai filter .NET thành tổng test duy nhất. Build có warning nullable/
analyzer, không có build error. Không chạy browser, paid API, MariaDB disposable,
CI/staging/production; tooling/script suite không thay thế nghiệm thu ứng dụng.

## Handoff và gate

Repair plan: `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-4.md`.
Prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-4.md`.
Giữ F01/F04 mở; F02/F03/F05 đã đóng, identity được giữ. Phase 1 CHANGES_REQUESTED;
Phase 2–7 LOCKED. Cursor sửa vòng 4, trả AWAITING_REVIEW để Codex review vòng 5.
