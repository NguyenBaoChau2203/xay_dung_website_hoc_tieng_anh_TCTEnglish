# Bàn giao sửa Phase 0 — round 1

## Định danh

- Phase 0, repair round 1. Plan: `docs/superpowers/plans/hybrid-rag/phase-0-fix-round-1.md`. Prompt: `.ai/prompts/hybrid-rag/phase-0-fix-round-1.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Fingerprint scope đã review trước khi sửa: `f671a9f1a49282d5cab76474efe132e557dcaa07d482ba35c8decc6bb81b6cdd`. Năm file digest lúc đó vẫn khớp hash đó trước khi sửa nội dung.
- Fingerprint scope sau sửa: `b4205acda7cc68a5e0ec94a1c68410b6d7ee3181381bdebfec09f69d8ea65ce2`. Cách tính nằm trong [scope-manifest.json](scope-manifest.json).
- Hash canonical fixture revision 2: `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`. Hash raw CRLF của revision 1, chỉ để truy vết: `0abf1e31fc781c0ba7b87d5916cda23e69564033b19efdebfdfd9e7ef2bb4116`.
- Local. Không CI, không production, không deploy.

## Từng finding

Chưa đánh dấu finding là resolved. Codex cần kiểm tra lại.

| ID | Việc đã làm | Bằng chứng | Còn chờ |
| --- | --- | --- | --- |
| HR-P0-R1-F01 | Sửa Quiz/Matching, Grammar, Premium, harbor và phép trừ 6. Partial ghi facet chưa trả lời. 120 ID và tỷ lệ 80/40 giữ nguyên | [oracle-validation.md](oracle-validation.md), fixture revision 2 | Đối chiếu ngữ nghĩa của reviewer. Test không thay việc đó |
| HR-P0-R1-F02 | Thêm `scenario` cho quota, timeout, network, HTTP 503, stop-stream, xóa, gỡ xuất bản, hash cũ và chuyển quyền. Clone hỏng bị validator từ chối | `HybridRagFixtureValidator`, 8 test clone | Reviewer đọc world state, không suy từ câu hỏi |
| HR-P0-R1-F03 | Hash canonical UTF-8/LF. LF và CRLF cùng hash. Đổi fact, ID, outcome hoặc split thì hash đổi. Pin `.gitattributes` chỉ cho fixture | Test `CanonicalHash_TreatsLfAndCrlfAsTheSameFreeze` và theory 4 ca | Không có |
| HR-P0-R1-F04 | Pending sang Settled giữ tổng câu hỏi. Pending sang Released trả suất. Race thiếu token generation còn nguyên | [contracts.md](contracts.md) mục 7, đối chiếu `ApplyTerminalStateAsync` | Không sửa code quota |
| HR-P0-R1-F05 | Ma trận nói đúng: `.codegraph/` có từ 2026-09-09. Phiên discovery đọc source vì không dùng CodeGraph. Phiên này gọi CLI ghim explore, chỉ đọc | [coverage-matrix.md](coverage-matrix.md), ghi chú trong [handoff.md](handoff.md) | Không init, rebuild hay sync |

## File

- `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json`
- `TCTEnglish.Tests/HybridRagEvaluationBaselineTests.cs`
- `TCTEnglish.Tests/HybridRagFixtureValidator.cs`
- `docs/feature-audits/hybrid-rag/phase-0/oracle-validation.md`
- `docs/feature-audits/hybrid-rag/phase-0/contracts.md`
- `docs/feature-audits/hybrid-rag/phase-0/coverage-matrix.md`
- `docs/feature-audits/hybrid-rag/phase-0/baseline-results.md`
- `.gitattributes`
- `.ai/context/bug-fix-log.md`
- Manifest và file này

Review round 1, plan sửa và prompt sửa được giữ. Không sửa `Program.cs`, appsettings, csproj, schema hay nội dung bài học thật.

## Kiểm tra

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet test ... -c Phase0FixReview --filter FullyQualifiedName~HybridRagEvaluationBaselineTests` | 15 passed, 0 failed. Có khóa fixture, LF/CRLF, 4 đột biến và 8 clone |
| Lệnh `--no-build` retriever, classifier, provider, composer, regression | 129 passed, 0 failed |
| Quan sát revision 2 | `guideHits=58`; intent `CardLookup:10,ClassInfo:2,MyProgress:6,OutOfScope:78,WebsiteGuide:24`. Câu hỏi không đổi nên trùng số lịch sử. Không phải điểm RAG |
| `uv run --no-project python scripts/agent_tooling.py` | Exit 0. `Agent tooling verified (working tree).` |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 test, 134 pass, 1 skip, 70.214s. Skip là nhánh host không phải Windows |
| `git diff --check` | Exit 0, không output. File untracked được quét UTF-8 và trailing space riêng |
| Browser, API trả phí, migration | not-run |

## Rủi ro

- Oracle đúng hay sai vẫn cần người review đọc [oracle-validation.md](oracle-validation.md). Pass 15 test không chứng minh điều đó.
- Race token generation, cửa sổ 500 thẻ, ngày UTC của retriever và Guide 9 vẫn là gap nguồn. Chưa sửa runtime.
- Hash raw của file LF revision 2 tình cờ trùng hash canonical vì file không có CR. Test CRLF chứng minh hash raw của bản CRLF thì khác. Scope manifest vẫn hash byte thô.

## Trạng thái

Phase 0: `AWAITING_REVIEW`. Phase 1–7: `LOCKED`. Không ghi `APPROVED`.

Tin nhắn gửi Codex:

> Cursor Auto đã nộp sửa Phase 0 round 1 cho HR-P0-R1-F01 đến F05. Hãy review lại theo `.ai/prompts/hybrid-rag/codex-phase-review.md` và `docs/feature-audits/hybrid-rag/phase-0/fix-round-1-handoff.md`. Chưa đánh dấu finding là resolved. Nếu chưa đạt, giữ Phase 1 khóa. Nếu đạt, cập nhật review-state rồi mới cho phép phase kế tiếp.
