# Phase 1 — handoff hoàn thiện trực tiếp trong review vòng 6

Ngày 2026-11-06. Owner đã yêu cầu Codex hoàn thiện phần còn lại để chuyển Phase 2.
Cùng một agent triển khai, review và xác minh. Kết quả nghiệm thu ở
[review-round-6.md](review-round-6.md): **Phase 1 APPROVED, Phase 2 READY**.
Phase 3–7 giữ LOCKED. Chưa thực thi Phase 2.

## Snapshot

- Nhánh `feature/hybrid-rag-chatbot`, HEAD đầu/cuối
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- Snapshot Cursor trả về: 46 entry, fingerprint
  `2f8ca1d0d125a456703985a5f8606242f53548de4704218f4a9557e378f2d16d`, đã kiểm
  trước khi sửa; giữ ở `scope-manifest-fix-round-5.json`.
- Snapshot sau Codex hoàn thiện: **48 entry**, fingerprint
  `764d370dc1b2482cd1b78954a8959dfd4706b16b5156ed00659a9301b729e7a9`.
  42 entry cũ giữ nguyên byte; bốn entry được sửa và hai entry mới có trong manifest.
- Fixture Phase 0 canonical UTF-8/LF giữ
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Không sửa Program.cs trong closeout, appsettings/.csproj/model/schema/content;
  không paid API, ghi secret, reset DB, commit/push/PR/CI/deploy.

## HR-P1-R1-F01 được hoàn thiện

Reproduction còn lại: Explain why France has seasons/câu tiếng Việt và Explain
why forecast accuracy changes vẫn full từ định nghĩa của France/forecast.
Boolean nhận từ explain/meaning không mang mục tiêu đầy đủ nên không chứng minh
candidate trả lời được yêu cầu. Probe đã tái hiện trước khi triển khai.

Thay đổi:

- `StoredCardLookupRequest` giữ kind và toàn bộ target của frame tra cứu; không
  dùng một keyword trong câu để cấp full answer. Clause tên bộ vẫn do retriever
  resolve theo quyền và SetId trước cap.
- Boundary chỉ giữ card có title chuẩn hóa khớp toàn bộ target, đồng thời có field
  definition/phonetic/example tương ứng. Không dùng water để trả đầy đủ cho water
  cycle, hoặc forecast để trả đầy đủ cho forecast accuracy.
- Thiếu source/field trả InsufficientEvidence, scope không full, evidence rỗng.
  Câu không nhận diện có card lexical hit được từ chối. Evidence của lượt tra thẻ
  chỉ chứa các card thực sự được chọn, không thêm guide trùng từ nhưng không dùng.
- Giữ bare term, hỏi nghĩa, explain term, quoted phrase, notes lookup, so sánh bộ,
  typed platform/current-user flows và mọi lifecycle/access rule.
- Sửa control Weather/cloud: hỏi weather trong bộ chỉ có cloud không thể là full
  definition answer; tra cloud trong đúng bộ vẫn trả đúng. Control này vẫn phát
  hiện việc bỏ source filter vì bỏ filter sẽ trả weather từ Nature.

Các tệp app/test sửa trong closeout:
`Rag/StoredCardLookupRequest.cs`, `Rag/AiChatAnswerService.cs`,
`Rag/ChatExternalFactPolicy.cs` dưới Services/AI và
`TCTEnglish.Tests/AiRagBoundaryAccessTests.cs`; thêm bug memory và executed plan
`docs/superpowers/plans/hybrid-rag/phase-1-fix-round-6.md`.

## Kiểm tra thực tế

| Kiểm tra | Kết quả |
| --- | --- |
| Hồi quy trước sửa, Phase1FixRound6 | 13 ca: **11 fail, 2 pass**, TRX phase1-fix6-red |
| Toàn bộ AiRagBoundaryAccessTests sau sửa | **36 pass**, 0 fail/skip; build thành công |
| Filter Phase 1 main theo plan, sau sửa | **215 pass**, 0 fail/skip, khoảng 17 s |
| Provider/ML.NET/ba suite review probe, sau sửa | **65 pass**, 0 fail/skip, khoảng 10 s |
| Probe độc lập SQLite với 12 retriever production | **30 quan sát**; 10 scenario unsupported đều non-full/evidence rỗng; control nguồn và tra nghĩa đúng |
| Tooling validator | exit 0 |
| Python scripts suite cuối | 135 tests, 134 pass/1 skip, khoảng 71 s |
| Whitespace/fixture/predecessor/source scope | Pass; manifest 48 entry và fixture không đổi |

Không cộng hai filter .NET thành tổng test duy nhất. Skip là local-media contract
từ chối nền tảng ngoài Windows; máy hiện tại chạy Windows. Bản dịch Pomelo từ
production query builders vẫn pass trong main filter; không thực thi MariaDB.
Không có persisted model change nên không phát sinh migration/data package.

Evidence ignored ở `.tmp/hybrid-rag-review/phase-1/round-6/` gồm
`phase1-fix6-red.trx`, `phase1-fix6-boundary.trx`, `phase1-fix6-main.trx`,
`phase1-fix6-extra.trx`, `Probe-Fixed.ps1`, `behavior-probes-fixed.json`.
Probe trước sửa giữ riêng ở Probe-Sqlite/behavior-probes-sqlite; không dùng binary
cũ để chứng minh acceptance sau sửa.

## Phạm vi nghiệm thu và bước tiếp theo

Đây là baseline deterministic an toàn của Phase 1, chưa phải Hybrid RAG hoàn chỉnh.
Các cách diễn đạt chưa nhận diện hoặc mục tiêu không có học liệu phù hợp có thể
được hỏi rõ/từ chối. Corpus/BM25, semantic retrieval, generation có grounding và
citation bền vẫn cần các phase riêng. Chưa chạy browser/live API/CI/hosting.

F01/F02/F03/F04/HR-P1-R3-F05 đều đã xác minh đóng trong phạm vi Phase 1.
Owner có thể khởi chạy `.ai/prompts/hybrid-rag/phase-2-corpus-bm25.md` trên Cursor
Auto; chỉ Phase 2 được mở và phải trả AWAITING_REVIEW khi hoàn thành.
