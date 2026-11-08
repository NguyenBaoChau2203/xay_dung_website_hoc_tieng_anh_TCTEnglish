# Phase 2 — Codex review vòng 1

Ngày 2026-11-06. **CHANGES_REQUESTED**. Có bốn lỗi được tái hiện trên snapshot
hiện tại; Phase 3–7 tiếp tục **LOCKED**. Đây là review application code, chưa sửa
các lỗi. Không suy ra quyền sửa Phase 2 từ lần owner cho phép hoàn thiện Phase 1.

## Snapshot đã kiểm

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- [Manifest](scope-manifest.json): **60/60 entry khớp**, fingerprint tính lại
  `ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a`.
- Predecessor Phase 1:
  `764d370dc1b2482cd1b78954a8959dfd4706b16b5156ed00659a9301b729e7a9`.
  Không bỏ entry kế thừa. Hai hash thay đổi trong phần kế thừa là Program.cs và
  AiChatAnswerService.cs; 12 entry mới là corpus/tests và admin rebuild.
- Đã đọc source hiện tại qua CodeGraph và các khoảng bị lược bỏ bằng đọc có giới
  hạn. Handoff Cursor là dữ liệu để đối chiếu, không thay thế acceptance.
- Các tài liệu review/repair mới được tạo sau snapshot trên; manifest trả về
  được giữ nguyên để định danh chính xác source đã review. Repair phải bổ sung
  plan/prompt đang thực thi vào manifest tích lũy mới.

## Findings

### HR-P2-R1-F01 — P1 — Dry-run gửi thành rebuild thật

Source: `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml:174`,
`:176`, `:310`; `AiManagementController.RebuildRetrievalIndex`, dòng 141/145.

Form dùng hai submit button cùng tên `dryRun`, lần lượt `true` và `false`.
Trong sự kiện submit, handler vô hiệu hóa tất cả button trước khi browser tạo
dữ liệu form. Disabled submitter không còn được gửi. Action nhận `bool dryRun`
mặc định false khi thiếu trường và gọi `BuildAsync`, không gọi `DryRunAsync`.

Đã tái hiện bằng Chrome 156, native `button.click → submit → formdata`, với form
và handler hiện tại trong tab cách ly. Bấm Dry-run và Build đều chỉ gửi token
antiforgery giả của fixture, không có `dryRun`. Không gọi rebuild trên dữ liệu thật
để chứng minh lỗi này. Đây là browser reproduction của serialization, chưa phải
kiểm thử đăng nhập admin end-to-end.

Kỳ vọng: Dry-run chỉ đọc/đếm/hash, không tạo snapshot hoặc đổi manifest. Thực tế:
đường đi của nút Dry-run chọn nhánh ghi snapshot. Auth/antiforgery hiện có không
ngăn lỗi chọn operation này. Root cause đã xác nhận, không phải giả thuyết.

### HR-P2-R1-F02 — P1 — Bỏ câu khớp khi chuyển chunk thành evidence

Source: `RetrievalCorpusService.ToHit`,
`TCTEnglish/Services/AI/Rag/Corpus/RetrievalCorpusService.cs:312`, đặc biệt dòng 314.

Tạo Reading published, title `Harbor observations`, body làm dữ liệu thử:

> Welcome to the lesson. The tidal harbor marker is visible at dawn.

Với `zzzz tidal harbor marker qqqq`, BM25 tìm đúng `reading:1`, nhưng ToHit luôn
chọn câu hợp lệ đầu tiên trong chunk, không xét query/matched terms. Hit là
`reading:1#s1`, text `Welcome to the lesson.`. Câu chứa `tidal harbor marker` ở s2
bị mất. Boundary với toàn bộ 12 retriever production và lexical service trả
`Answered/full` cùng câu chào. Với `Where is the tidal harbor marker visible?`,
text còn ghép các câu về Add-card/Flashcard không trả lời vị trí được hỏi.

Kỳ vọng: hit mang câu/span thực sự liên quan trong chunk, với sentence ID/hash
đúng text; một câu mở đầu không thay thế phần body tạo ra match. Không cần một
model generation mới để chọn đúng evidence. Các test hiện tại chỉ dùng bài một
câu nên chưa bắt được lỗi này.

### HR-P2-R1-F03 — P1 — Có BM25 hit là được nâng thành Answered/full

Source: `AiChatAnswerService.TryLexicalAnswerAsync`,
`TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs:164`, đặc biệt dòng 173;
`Bm25Ranker.Rank:49` nhận mọi điểm lớn hơn 0 làm candidate.

Để tách khỏi F02, probe tiếp theo dùng bài chỉ có **một câu**:

> The tidal harbor marker is visible at dawn.

Hai câu hỏi `Why does the tidal harbor marker glow at midnight?` và
`How many people live near the tidal harbor marker?` không có câu trả lời trong
nguồn này. Tuy vậy boundary trả nguyên câu dawn và hai guide Add-card/Flashcard,
outcome `Answered`, scope `full`. Cùng service khi không inject lexical trả
`OutOfScope`; vì vậy đây là đường bypass được Phase 2 thêm vào, không chỉ là lỗi
classifier cũ. Không có dữ kiện về nguyên nhân phát sáng hoặc dân số trong corpus.

Root cause: source access/hash hợp lệ chỉ chứng minh nguồn còn được xem, không
chứng minh nó hỗ trợ yêu cầu. TryLexicalAnswerAsync nối tất cả hit rồi đánh dấu
full vô điều kiện. Sửa F02 để chọn đúng câu vẫn không giải quyết được F03.
Kỳ vọng: không trả full hoặc text như đã giải đáp cho phần thiếu chứng cứ;
loại evidence không dùng, giữ hỏi rõ/thiếu chứng cứ/ngoài phạm vi an toàn.
Phase 2 có thể giữ extract/reference có giới hạn; không triển khai generation
Phase 4 hoặc vá bằng blacklist vài câu probe.

### HR-P2-R1-F04 — P2 — Câu dài vượt trần chunk và ra thẳng câu trả lời

Source: `CorpusChunker.Pack`,
`TCTEnglish/Services/AI/Rag/Corpus/CorpusChunker.cs:85` và `:97`;
trần tại `CorpusModels.cs:21`.

Body thử là `harborword` lặp 2700 lần rồi dấu chấm: **29.700 ký tự**, dưới
MaxBodyCharacters 200.000. Bộ đếm hiện tại cho **7.425 token xấp xỉ**. Prepare
tạo một chunk chứa nguyên sentence, vượt MaxChunkTokens **600**. Điều kiện flush
yêu cầu current có hơn một sentence nên không chặn được trường hợp này.

Đã xác nhận qua ingestion Reading published → BuildAsync `promoted` → chat với
`zzzz harborword qqqq marker`: evidence chứa **29.700 ký tự**, answer **29.750
ký tự**, completion **7.438 token xấp xỉ**. Đây là hành vi thực thi, không chỉ là
giá trị hằng số không dùng. Kỳ vọng: không promote/return một chunk/evidence vượt
giới hạn đã công bố; chia span ổn định hoặc loại rõ nguồn quá dài, có accounting.
Giữ nguyên text gốc và provenance, không cắt text rồi giả vờ là cả câu nguồn.

## Kiểm tra đã chạy

Configuration riêng `Phase2ReviewRound1`; không dùng binary Cursor làm kết luận.

| Kiểm tra | Kết quả |
| --- | --- |
| Build app/tests qua dotnet test, corpus/BM25 + affected chat/boundary/lifecycle/provider consumer/fixture/retriever suites | **222 pass**, 0 fail/skip; có đủ 7 test corpus mới |
| OpenAiProviderClient, MlNet và ba nhóm review-probe cũ | **65 pass**, 0 fail/skip |
| Probe SQLite qua corpus và 12 retriever production | F02/F03/F04 tái hiện; có control English password help giữ đúng live answer |
| Chrome isolated native submit | F01 tái hiện với cả hai submit button |
| Baseline development chạy lại | **15/17 top-1**, held-out scored **0**, 39 guide, 86.313 byte; build 64 ms, 17 query 647 ms tổng, RAM estimate 5.480.240 byte trong lần chạy này |
| Tooling validator | exit 0 |
| Python tooling suite | **135 tests**, 134 pass, 1 skip; skip non-Windows media contract trên Windows |

Không cộng các filter thành một số test duy nhất. File mang tên
`AiPhase2ServiceTests.cs` định nghĩa `AiChatServiceTests`, đã nằm trong nhóm 222.
Một lần lọc nhầm theo tên file không discover test; lần đó không được tính là pass.
Build có warning nullable/analyzer hiện hữu, không có build error.

Hai miss baseline giống handoff, không chỉnh trọng số/fixture/held-out:
HR-NAV-D04 → guide:study-flashcard; HR-NAV-D12 → guide:daily-challenge. Probe
boundary production cho English password help vẫn lấy guide đúng qua live path;
không biến một retrieval miss thành finding rằng UI chat trả sai trong ca này.

Evidence ignored: `.tmp/hybrid-rag-review/phase-2/round-1/` chứa TRX,
`Probe.ps1`, `behavior-probes.json`, `browser-submit.json`. Probe chỉ dùng SQLite
synthetic và snapshot trong subtree `corpus-phase2-tests`, không ghi index thật.
`lexical-baseline.txt` ở thư mục `.tmp/hybrid-rag-review/phase-2/` là output test.

## Phần được giữ và giới hạn

Adapters loại private/draft, recheck publication/hash/user, field BM25, stable
ties, foreign requested path refusal, checksum, atomic manifest và preserve index
khi failure/cancel đều có source/tests tương ứng. DI store singleton/corpus scoped
không giữ scoped DbContext trong singleton; IAiProviderClient vẫn OpenAiProviderClient.
Không phát hiện persisted-model/schema/shared-content change trong Phase 2 này.

Chưa kiểm admin đăng nhập, POST antiforgery thật, layout responsive/keyboard hay
rebuild thành công trên trang chạy local. Fixture browser ở trên chỉ chứng minh
F01. Chưa có provider execution MariaDB cho adapter mới; SQLite không phải bằng
chứng tương thích server MariaDB. Không có migration nên không yêu cầu một
migration rỗng. Không API trả phí, CI, staging/production, commit/push/PR.

Approval chưa được cấp. [Repair plan](../../../superpowers/plans/hybrid-rag/phase-2-fix-round-1.md)
và [Cursor Auto prompt](../../../../.ai/prompts/hybrid-rag/phase-2-fix-round-1.md)
chỉ hoạt động khi owner yêu cầu thực thi sửa. Phase 1 approval lịch sử giữ nguyên;
Phase 2 CHANGES_REQUESTED, Phase 3–7 LOCKED.
