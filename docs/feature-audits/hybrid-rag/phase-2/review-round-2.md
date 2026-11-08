# Phase 2 — Codex review vòng 2

Ngày 2026-11-06. **CHANGES_REQUESTED**. F01 và F04 đã sửa đúng lỗi code được
báo; **F02 và F03 vẫn mở** qua reproduction mới cùng root cause. Không có finding
mới được gán thêm ID. Phase 3–7 tiếp tục **LOCKED**.

## Snapshot và thẩm quyền

- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- [Manifest tích lũy](scope-manifest.json): **64/64 entry khớp**, fingerprint tính lại
  `67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a`.
- Predecessor fingerprint của review vòng 1 là
  `ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a`.
  Bốn entry mới là test repair, LexicalAnswerAdmission và plan/prompt fix vòng 1.
- Dùng source hiện tại qua CodeGraph; đọc giới hạn các phần bị lược bỏ, nhất là
  Razor handler, SplitSpans và test helper. Kiểm source, caller, test assertion
  và chạy probe riêng; không lấy nhãn tự báo trong handoff làm approval.
- Lượt này REVIEW_ONLY cho application code. Chỉ tạo report, plan/prompt repair
  và cập nhật state; không sửa code/tests ứng dụng hoặc manifest trả về.

## Trạng thái finding cũ

| Finding | Kết luận | Bằng chứng |
| --- | --- | --- |
| HR-P2-R1-F01 | **CLOSED về lỗi chọn operation** | Handler ghi hidden operation trước khi disable; Chrome gửi đúng dry-run/build. POST test host kiểm missing/invalid/token, dry-run không tạo manifest và không đổi byte manifest đã có. Acceptance browser admin đăng nhập vẫn chưa hoàn tất |
| HR-P2-R1-F02 | **OPEN, P1** | Ca gốc nhiều câu đã đúng; câu dẫn nhập có nhiều từ chung vẫn thắng câu chứa nội dung cần tìm |
| HR-P2-R1-F03 | **OPEN, P1** | Ca gốc why/count được chặn, nhưng năm không phải dân số, because không đúng sự kiện và at dawn không phải vị trí vẫn được full |
| HR-P2-R1-F04 | **CLOSED trên phạm vi lỗi đã báo** | Span/chunk đã reload không vượt 600; long sentence nối lại đúng gốc, hash theo span; snapshot v1 bị từ chối |

## HR-P2-R1-F02 — từ dẫn nhập thay thế nội dung cần tìm

Source: `RetrievalCorpusService.ToHit`,
`TCTEnglish/Services/AI/Rag/Corpus/RetrievalCorpusService.cs:314`, cùng
`QueryOverlap:346`. Root cause đã xác nhận: đếm giao token gồm cả từ dẫn nhập,
không phân biệt từ xác định chủ đề với các từ chung của câu hỏi.

Probe dùng Reading published, title `Daily notes`, keywords `A1`/`Sea`; title và
keywords không chứa marker hoặc từ dẫn nhập. Body là dữ liệu synthetic:

> Here you can find examples in this lesson. The tidal harbor marker is visible beside the gate.

Hai query:

- `Where can I find the tidal harbor marker in this lesson?`
- `Can I find the tidal harbor marker in this lesson?`

RetrieveAsync tìm đúng `reading:1`, nhưng trả `reading:1#s1`, text `Here you can
find examples in this lesson.` Câu s2 về marker bị bỏ. Qua boundary với đủ 12
retriever production, cả hai query trả chính câu dẫn nhập này, `Answered/full`,
evidence s1. Vì vậy đây là lỗi retrieval/evidence độc lập trước cả admission.

Ca gốc `Welcome to the lesson. The tidal harbor marker is visible at dawn.` với
query không có framing đã đúng; không dùng việc ca đó pass để đóng toàn bộ F02.
Repair cần chọn được phần body mang nội dung thực sự được hỏi, giữ exact
text/hash/ID và recheck nguồn, không chỉ đổi nhãn câu trả lời sau khi mất s2.

## HR-P2-R1-F03 — kiểm từ chỉ loại yêu cầu chưa kiểm dữ kiện được hỏi

Source: `Corpus/LexicalAnswerAdmission.cs:14`, `:54`, `:84`;
`AiChatAnswerService.TryLexicalAnswerAsync:178` vẫn trả full nếu TryAdmit thành công.

Classify/Covers hiện chỉ xét một loại yêu cầu và một số dấu hiệu từ trong span.
Covers không gắn dấu hiệu đó với đúng sự kiện/đối tượng/thuộc tính của query;
các frame chưa nhận dạng trở thành Reference với Covers mặc định true.

Probe qua production boundary, mỗi ca chỉ có một bài Reading published liên quan:

| Câu hỏi | Toàn bộ nội dung bài liên quan | Kết quả hiện tại |
| --- | --- | --- |
| How many people live near the tidal harbor marker? | The tidal harbor marker was installed in 2008. | Answered/full, dùng năm 2008 làm evidence cho câu hỏi số người |
| Why does the tidal harbor marker glow at midnight? | The tidal harbor marker was repainted because the old paint faded. | Answered/full từ nguyên nhân sơn lại; không có nguyên nhân phát sáng |
| Where is the tidal harbor marker visible? | The tidal harbor marker is visible at dawn. | Answered/full, nhưng dawn là thời gian, không có vị trí |
| When was the tidal harbor marker constructed? | The tidal harbor marker is visible at dawn. | Answered/full từ thời điểm nhìn thấy, không có thời điểm xây |
| Who built the tidal harbor marker? | The tidal harbor marker is visible at dawn. | Answered/full qua Reference mặc định; không có người xây |
| Which color does the tidal harbor marker have? | The tidal harbor marker is visible at dawn. | Answered/full qua Reference mặc định; không có màu sắc |

Đây không phải yêu cầu chatbot biết dữ kiện bên ngoài. Kỳ vọng là từ chối/hỏi rõ
khi học liệu không đủ, thay vì khẳng định đã trả lời đầy đủ. Hai ca gốc why/count
với body chỉ có dawn đã non-full/evidence rỗng, nhưng chỉ cần nguồn có chữ số hay
because không liên quan thì lỗi quay lại. Không thể đóng F03 bằng thêm blacklist
wording hoặc thêm vài từ vào Covers.

**Oracle test cũng cần sửa:**
`AiRagPhase2FixRound1Tests.UnsupportedLexicalQuestion_DoesNotBecomeFullAnswer:264`
đang đòi `Where ... visible?` phải full từ `at dawn`. Positive này không kiểm
đúng yêu cầu vị trí. Giữ nó thành negative; positive thời gian dùng `When ...
visible?`, hoặc positive vị trí có body `... stands beside the eastern gate.`.
Probe hai positive đúng này đều Answered/full. Không sửa fixture evaluation
đã freeze để biến một câu không được hỗ trợ thành đáp án hợp lệ.

## Verification

Binary riêng `Phase2ReviewRound2`, build app/tests thành công. Các nhóm đã chạy:

- Corpus/boundary/chat/lifecycle/consumer/retriever/fixture: **227 pass**,
  0 fail/skip; gồm 5 test fix vòng 1. Khoảng 32 giây.
- OpenAiProviderClient/ML và ba nhóm probe cũ: **65 pass**, 0 fail/skip,
  khoảng 14 giây. Không gọi provider thật.
- Probe độc lập: **13 scenario** qua corpus/12 retriever/boundary. Hai case
  F02 framing và sáu case F03 thiếu support vẫn full; các control gốc và positive
  đúng đã được đối chiếu. Các scenario synthetic không chấm held-out.
- Long source probe: promoted, 14 span gồm câu chào, max span **600 token xấp xỉ**,
  nối các span còn lại bằng đúng câu dài 29.700 ký tự. Regression còn kiểm chunk
  reload/hash/version; `paragraph-sentence-v2` được giữ, v1 phải rebuild.
- Chrome native submit trên tab cách ly: đúng một operation dry-run/build và cả
  hai nút disabled. Test host chạy POST/auth/antiforgery/manifest qua test mới.
- Baseline chạy lại: **15/17**, held-out scored **0**; 39 document, 86.313 byte;
  build 99 ms, 17 query tổng 758 ms, RAM estimate 4.056.216 byte. Phiên bản:
  lexical-v1 / paragraph-sentence-v2 / vi-en-fold-v1. Hai miss cũ giữ nguyên.
- Tooling validator exit 0; Python suite **135 tests, 134 pass/1 skip**, khoảng
  52 giây. Skip là non-Windows media contract khi chạy trên Windows.
  Diff/manifest/link checks đạt; tooling không thay app acceptance.

Evidence ignored ở `.tmp/hybrid-rag-review/phase-2/round-2/`: TRX,
`Probe-Round2.ps1`, `behavior-probes.json`, `browser-submit.json`. Probe chỉ dùng
SQLite synthetic/snapshot trong `corpus-phase2-tests`; không rebuild index thật.
Probe khởi tạo AiChatAnswerService với DeterministicIntentClassifier và 12
concrete retriever; đây là service-boundary probe, không phải phiên chat browser.

## Acceptance còn thiếu và gate

Chưa có phiên browser đăng nhập trên trang admin chạy bằng launcher local để kiểm
Dry-run/build và giao diện/thao tác sau redirect. Handler cách ly cộng HTTP test
host là bằng chứng hữu ích cho F01, chưa hoàn tất tiêu chí browser đã ghi trong
plan fix vòng 1. Đây là thiếu evidence, không gán thành lỗi code mới. Repair kế
tiếp phải thực hiện hoặc báo BLOCKED cụ thể; không tự bỏ tiêu chí để mở Phase 3.

Không có schema/content/config/DI/provider change mới do reviewer; không API trả
phí, MariaDB execution, CI, commit/push/PR hay staging/production. Approval lịch
sử Phase 0/1 giữ nguyên. Phase 2 **CHANGES_REQUESTED**, Phase 3–7 **LOCKED**.

[Plan fix vòng 2](../../../superpowers/plans/hybrid-rag/phase-2-fix-round-2.md)
và [prompt Cursor Auto](../../../../.ai/prompts/hybrid-rag/phase-2-fix-round-2.md)
chỉ kích hoạt khi owner yêu cầu chạy vòng sửa.
