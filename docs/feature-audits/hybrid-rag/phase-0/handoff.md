# Bàn giao Phase 0 — discovery, hợp đồng và baseline

Sửa provenance sau review round 1: câu trong ma trận cũ nói checkout không có `.codegraph/` là sai. Thư mục đã có. Phiên discovery không gọi CodeGraph; catalog MCP của Cursor trong phiên đó không có tool này, và các kết luận discovery là đọc source trực tiếp. Phiên sửa round 1 mới gọi CLI ghim, chỉ đọc, và không init/rebuild/sync. Bản ghi đó nằm ở [coverage-matrix.md](coverage-matrix.md) và [fix-round-1-handoff.md](fix-round-1-handoff.md). Đoạn này không bịa một lần gọi CodeGraph cho phiên discovery.

## Định danh và phạm vi

- Phase 0, chưa có vòng sửa. Plan: `docs/superpowers/plans/hybrid-rag/phase-0-discovery.md`. Prompt: `.ai/prompts/hybrid-rag/phase-0-discovery.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối cùng là `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Working tree lúc vào đã untracked: `.ai/prompts/hybrid-rag-chatbot-execution.md`, `.ai/prompts/hybrid-rag/`, `docs/feature-audits/2026-11-05-hybrid-rag-research.md`, `docs/superpowers/plans/2026-11-05-hybrid-rag-chatbot.md`, `docs/superpowers/plans/hybrid-rag/`. Những file đó được giữ nguyên, trừ `review-state.md` được cập nhật trạng thái phase.
- File phase này tạo: ma trận, hợp đồng, baseline, fixture 120 ca, test harness, manifest và bàn giao này.
- Không có quyền sửa `Program.cs`, `appsettings.json`, `.csproj`, gọi API trả phí, Git remote, CI hay deploy.
- Manifest: [scope-manifest.json](scope-manifest.json). Chưa có approval tiền nhiệm vì đây là phase đầu.

## Kết quả quan sát được

Đường chat production đi `POST /AI/Chat/Send` hoặc `ClassChatHub.StartAiStream` trên `/classChatHub`, rồi `AiChatService` gọi `OpenAiProviderClient`. Context chỉ có system prompt, lịch sử và câu hỏi. Mười hai retriever và `InternalKnowledgeProvider` có đăng ký nhưng không phải bind production của `IAiProviderClient`. Test host thì có gắn provider nội bộ; đó không phải hành vi production.

41 guide đều được đối chiếu route với source. Phần lớn route `/Home/...` vẫn là attribute route hiện hành, không phải route gãy. Guide 9 nói chế độ Read trong khi `Study.cshtml` chỉ thấy Flashcard, Write, Quiz, Matching. Guide 21 mô tả trợ lý đã trả lời từ dữ liệu người dùng; đường OpenAI hiện không đưa snippet. Guide 40 cố ý không có route admin.

Hợp đồng đề xuất nằm trong [contracts.md](contracts.md): evidence có khóa/hash/câu/route/quyền/thời điểm; outcome có kiểu; chỉ mục public tách khỏi tra cứu private; rebuild dự kiến là POST admin trên `AiManagementController`; quota nhiều giai đoạn không được dùng `RecoverKnownPendingAsync` khi stage trước vẫn đang mở.

Fixture synthetic 120 ca, 80/40, đã đóng băng. Fact yêu cầu nằm trong text nguồn giả. Không có user thật. Bộ test không chấm điểm RAG.

Checklist plan 0: các mục discovery, ma trận, hợp đồng, fixture, validator và baseline đã có bằng chứng local. Không sửa runtime. Không có mục nào được owner hoãn.

## Sổ kiểm tra

| Kiểm tra | Lệnh hoặc bước | Môi trường | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| Fixture và test retriever/classifier trực tiếp | `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj -c Phase0Eval --filter "FullyQualifiedName~HybridRagEvaluationBaselineTests\|FullyQualifiedName~WebsiteGuideRetrieverTests\|FullyQualifiedName~DeterministicIntentClassifierTests\|FullyQualifiedName~StudyRecommendationRetrieverTests\|FullyQualifiedName~UserVocabularyRetrieverTests\|FullyQualifiedName~LearningProgressRetrieverTests\|FullyQualifiedName~InternalKnowledgeProviderTests"` | Local, không OpenAI | Passed 93, failed 0, skipped 0 | [baseline-results.md](baseline-results.md) |
| Composer và baseline regression nội bộ | Cùng project, `-c Phase0Eval --no-build`, filter `TemplateAnswerComposerTests\|AiBaselineRegressionTests` | Local, không OpenAI | Passed 38, failed 0, skipped 0 | Cùng baseline |
| Quan sát 120 câu | In từ `HybridRagEvaluationBaselineTests` | Classifier từ khóa và `WebsiteGuideRetriever` | `guideHits=58`; intent `CardLookup:10,ClassInfo:2,MyProgress:6,OutOfScope:78,WebsiteGuide:24` | Không so với outcome mong đợi |
| Tooling validator | `uv run --no-project python scripts/agent_tooling.py` | Local | Exit 0, `Agent tooling verified (working tree).` | stdout |
| Python suite | `uv run --no-project python -m unittest discover -s scripts/tests -v` | Local Windows | 135 test, 134 pass, 1 skip, 72.379s | Skip: nhánh host không phải Windows |
| Whitespace file phase | Quét UTF-8, không BOM, không trailing space/tab trên file trong digest | Local | Sạch | Script kiểm tra trước khi hash |
| `git diff --check` | `git diff --check` | HEAD không có diff tracked | Exit 0, không có output | File mới là untracked nên được quét riêng |
| Browser | Không mở |  | not-run | Không ghi là pass |
| Provider trả phí | Không gọi |  | not-run | Không ghi là pass |
| CI / production | Không chạy |  | not-run | Local không phải release |

Không đổi entity nên không có migration, Designer, snapshot hay export shared data.

## Rủi ro và chỗ reviewer nên xem

- Race quota: `ReserveQuestionAsync` settle mọi ledger Pending có `ActualTokens > 0`. Phase 4 phải tách stage. Chưa sửa code.
- `CardLookupRetriever.Take(500)` không sắp xếp. `SpeakingRetriever` không lọc public/owner. `LearningAreaProgressRetriever` đếm mọi speaking video. Retriever tiến độ dùng ngày UTC, Goals dùng ngày Việt Nam.
- `FolderDetail` trả folder theo id cho user đã đăng nhập dù không phải owner. Chưa sửa.
- `AiChatService` chặn chuỗi `password`, nên câu tiếng Anh hỏi cách đổi mật khẩu không tới guide 19.
- Tên kiểu trong hợp đồng còn là đề xuất.
- Browser và API live chưa chạy. Đừng đọc `guideHits=58` hay `OutOfScope:78` thành chất lượng RAG.
- Phase này không đụng hành vi đã duyệt trước đó vì chưa có phase nào APPROVED và không sửa code app.

## Trạng thái cuối

Phase 0: `AWAITING_REVIEW`. Phase 1–7: `LOCKED`. Chưa ghi `APPROVED`.

Tin nhắn gửi Codex:

> Cursor Auto đã xong Phase 0. Hãy review theo `.ai/prompts/hybrid-rag/codex-phase-review.md`, kiểm tra code và bằng chứng trong `docs/feature-audits/hybrid-rag/phase-0/handoff.md`. Nếu chưa đạt, tạo plan fix và prompt fix cho các lỗi thật rồi giữ khóa phase kế tiếp. Nếu đạt, cập nhật review-state và cho phép tôi bắt đầu phase kế tiếp.
