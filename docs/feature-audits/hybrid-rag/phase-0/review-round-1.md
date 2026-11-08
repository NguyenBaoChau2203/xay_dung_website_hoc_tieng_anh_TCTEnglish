# Review Phase 0 — vòng 1, 06/11/2026

**Kết quả: CHANGES_REQUESTED. Phase 1 tiếp tục LOCKED.**

Một agent review trực tiếp source, fixture và artifact; không sửa application,
test hoặc fixture của Cursor. Báo cáo local, không phải CI hay release.

## Snapshot và kiểm tra độc lập

- Nhánh `feature/hybrid-rag-chatbot`; HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`.
- Manifest được trả về có fingerprint
  `f671a9f1a49282d5cab76474efe132e557dcaa07d482ba35c8decc6bb81b6cdd`.
  Cả 5 digest entry khớp bytes hiện tại; tính lại bằng TAB/LF thật khớp fingerprint.
- Không có diff application tracked với HEAD. File mới của Phase 0 là untracked
  và đã được review trực tiếp, không bỏ qua vì `git diff HEAD` rỗng.
- CodeGraph trả source hiện tại của `HybridRagEvaluationBaselineTests` và các
  service cần đối chiếu; đọc trực tiếp các tài liệu/JSON chưa được graph bao phủ.
- Chạy lại cấu hình riêng `Phase0Review`, không ghi đè Debug/Release:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0Review --filter 'FullyQualifiedName~HybridRagEvaluationBaselineTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests' --logger 'console;verbosity=normal' --results-directory .tmp/hybrid-rag-review/phase-0 --logger 'trx;LogFileName=phase0-review.trx'
```

Kết quả **131 passed, 0 failed**, test time 5,2987 giây sau build; TRX ở
`.tmp/hybrid-rag-review/phase-0/phase0-review.trx`. Đây là test offline. Test validator
đang kiểm cấu trúc/hash và containment của fact; không chứng minh nhãn kỳ vọng đúng.

`agent_tooling.py` đạt. Python suite **135 test: 134 pass, 1 skip**, 54,404 giây;
skip là nhánh non-Windows. Test staging trong suite dùng fixture, không deploy.
Probe review được lưu ignored tại `.tmp/hybrid-rag-review/phase-0/review-probes.json`.

## HR-P0-R1-F01 — P1: ground truth chấp nhận thông tin không trả lời câu hỏi

**Source:** `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json:475` và `:509`;
validator `HybridRagEvaluationBaselineTests.cs:109–114`.

- HR-NAV-D14 hỏi “Quiz mode mở từ đâu?”, outcome `answered/full` nhưng fact bắt
  buộc chỉ là “A set is placed in a folder from that folder screen.”
- HR-NAV-D15 hỏi Matching dùng làm gì, cũng `answered/full` với đúng fact đặt set
  vào folder. Nguồn được trỏ tới không mô tả chức năng Matching.
- Validator chỉ kiểm fact nằm trong canonicalText. Test 131/131 vẫn pass khi hai
  case không có fact trả lời đúng câu hỏi.

**Ảnh hưởng:** một đoạn trích có nguồn nhưng lạc đề có thể được xem là đủ đáp án;
đánh giá hỗ trợ câu hỏi ở các phase sau sẽ sai ngay từ oracle. Đây là lỗi fixture
mới trong Phase 0, không phải bằng chứng chatbot runtime đang trả các câu này.

**Cần sửa:** rà soát toàn bộ 120 nhãn, thêm nguồn/fact synthetic đúng chức năng
cho Quiz/Matching hoặc đổi outcome nếu thật sự thiếu nguồn. Xác định rõ kết quả
số cần trả lời cho câu hỏi “còn thiếu bao nhiêu” thay vì chỉ ràng buộc các số đầu
vào; fact phải bao phủ ý hỏi. Giữ số case/split, ghi revision và lý do sửa.

## HR-P0-R1-F02 — P1: ca sự cố chỉ khai báo bằng lời hỏi, thiếu trạng thái mô phỏng

**Source:** fixture `:3602` (HR-OPS-D04), `:3631` (D05), `:3658` (D06), `:3773`
(H03); validator `HybridRagEvaluationBaselineTests.cs:90–120`.

Tất cả 120 context chỉ có businessDate, corpus, priorMessages và timezone. Không có
typed operation, quota usage, fault injection, tombstone hay index/source version
transition cho các ca cần điều kiện đó:

- D04 yêu cầu từ chối thẻ “đã xóa”, nhưng source zephyr-window vẫn hiện diện như
  case trả lời được; không có trạng thái xóa hoặc sự kiện làm thay đổi world state.
- D05 dựa vào câu người dùng nói đã hết 15 lượt, không có số lượt đã dùng trong
  setup. Không thể dùng lời tự khai làm dữ liệu quota có thẩm quyền.
- D06 không có expected/current source hash hay bản index cũ để tái hiện stale hash.
- H03 hỏi về HTTP 503, nhưng không thiết lập provider trả 503. Các ca cancel cũng
  chỉ là message text, không phải thao tác Stop/cancel được mô tả trong setup.

**Ảnh hưởng:** harness sau này phải đoán môi trường từ câu hỏi, case ID hoặc nhãn
kỳ vọng, khiến lỗi/quota/revocation không được tái hiện độc lập và dễ bị chấm sai.
Hiện chưa có fault harness thực thi; không gán đây là lỗi provider runtime.

**Cần sửa:** thêm setup/action có kiểu và kiểm tra nhất quán cho từng nhóm sự cố;
phân biệt câu hỏi bình thường với thao tác hủy. Dữ liệu setup độc lập với expected
outcome; validator phải phát hiện thiếu setup hoặc tham chiếu sai. Chỉ xây fixture/
validator ở Phase 0, không triển khai state machine application của Phase 4.

## HR-P0-R1-F03 — P2: raw hash khóa vào CRLF, sẽ thất bại trên checkout LF

**Source:** `HybridRagEvaluationBaselineTests.cs:17,64–66`; `.gitattributes:4` và
quy tắc hiện tại cho fixture: `text=auto`, `eol=unspecified`; local autocrlf=true.

Fixture hiện có 4.187 CRLF. Hash raw khớp expected
`0abf1e31fc781c0ba7b87d5916cda23e69564033b19efdebfdfd9e7ef2bb4116`.
Chỉ đổi CRLF sang LF, JSON giữ nguyên hoàn toàn, hash thành
`4e20524b57219146a96b7d4db548e509942e92a7ee49fc4ba8295844455b8949`.
Vì test hash raw bytes nên Assert.Equal sẽ thất bại trên LF sau Git normalization.
Đây là probe cục bộ trên bytes, không phải báo cáo đã chạy Linux CI.

Bug memory 01/05 đã ghi cùng lỗi raw-hash/line-ending với seed ML.NET.

**Cần sửa:** định nghĩa hash fixture theo UTF-8 với newline chuẩn LF, kiểm chứng
LF/CRLF cho cùng fingerprint và thay đổi nội dung thật vẫn bị phát hiện. Có thể
pin LF cho đúng fixture nếu cần, nhưng không chỉ cập nhật constant để khớp máy hiện
tại. Manifest scope vẫn được phép hash raw bytes để định danh snapshot local;
phân biệt rõ hai loại fingerprint và cập nhật tài liệu theo sửa đổi thực tế.

## HR-P0-R1-F04 — P2: hợp đồng mô tả sai việc trả lại lượt Standard khi settle

**Source:** `docs/feature-audits/hybrid-rag/phase-0/contracts.md:120`;
`AiDailyQuotaReservationService.cs:276–283` và phép tính usedQuestions khi reserve.

Tài liệu nói suất Standard có thể được nhả khi A vẫn chạy. Source khi settle giảm
PendingQuestions 1 và tăng SettledQuestions 1; tổng dùng để xét giới hạn không giảm.
Race **bỏ sót token generation** được nêu vẫn có cơ sở, nhưng kết luận trả lại lượt
Standard không được source chứng minh. Trả reservation sang Released là trường
hợp khác, không được đánh đồng với Settled.

**Cần sửa:** giữ phân tích token race đúng, sửa bảng/chuyển trạng thái theo công thức
counters thật, tách token budget với question quota. Không sửa quota application
trong Phase 0 và không tạo regression cho hành vi source không có.

## HR-P0-R1-F05 — P2: ghi nhận discovery sai về sự tồn tại của CodeGraph

**Source:** `docs/feature-audits/hybrid-rag/phase-0/coverage-matrix.md:4`.

Ma trận khẳng định checkout không có `.codegraph/`. Kiểm tra read-only thấy thư mục
có tại đúng root, creation time 09/09/2026; phiên review gọi được MCP và lấy source.
Không suy diễn rằng Cursor chắc chắn có MCP cùng phiên: khả năng tool trong phiên
Cursor và sự tồn tại directory là hai dữ kiện riêng. Ghi “không có directory” là sai
với checkout đang bàn giao và không đủ căn cứ để bỏ quy trình source discovery.

**Cần sửa:** kiểm tra và ghi đúng directory/tool capability, dùng graph/pinned CLI
khi khả dụng hoặc ghi chính xác lý do fallback. Không init/rebuild/sync index,
không giả vờ đã gọi tool ở phiên trước; bổ sung bằng chứng discovery hiện tại.

## Phạm vi và quyết định gate

Các lỗi trên thuộc fixture/validator và tài liệu Phase 0. Không yêu cầu sửa chatbot,
provider, Program, schema, key, học liệu thật hay chạy API/browser trả phí ở vòng này.
Browser chưa chạy được ghi đúng là not-run; không tự coi đây là lỗi khi chỉ khảo sát
source và đã nêu rõ khoảng trống rendered verification.

Tạo [plan fix vòng 1](../../../superpowers/plans/hybrid-rag/phase-0-fix-round-1.md)
và [prompt Cursor fix](../../../../.ai/prompts/hybrid-rag/phase-0-fix-round-1.md).
Cursor phải bàn giao lại để review từng ID. Không có findings nào được đánh dấu đã
sửa chỉ vì có plan. Phase 0 CHANGES_REQUESTED; Phase 1–7 LOCKED.
