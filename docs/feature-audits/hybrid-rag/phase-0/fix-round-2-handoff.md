# Bàn giao sửa Phase 0 — round 2

## Định danh

- Phase 0, repair round 2. Plan: `docs/superpowers/plans/hybrid-rag/phase-0-fix-round-2.md`. Prompt: `.ai/prompts/hybrid-rag/phase-0-fix-round-2.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Fingerprint đã review ở round 2, trước khi sửa: `b4205acda7cc68a5e0ec94a1c68410b6d7ee3181381bdebfec09f69d8ea65ce2`. Mười một digest entry vẫn khớp trước khi đổi validator và test.
- Fingerprint scope sau sửa: `1c33f6700cde76fc80558f823e2fb5699cbeae8c6fba89aeaab29bd25a2268cb`. Cách tính nằm trong [scope-manifest.json](scope-manifest.json).
- Fixture revision 2 không đổi. Hash canonical vẫn `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- Local. Không CI, không production, không deploy.

## HR-P0-R2-F01

Finding này chưa được đánh dấu resolved. Codex cần kiểm tra lại.

Trước khi sửa, bốn clone trong bộ nhớ gọi `HybridRagFixtureValidator.Validate` và nhận `errors=[]`. Bốn test tương ứng fail với `Assert.Contains`, chuỗi lỗi rỗng:

- HR-OPS-D05 đặt 0/15 câu và 0/60000 token, outcome vẫn `quota_exceeded`.
- HR-OPS-H03 đổi scenario thành `{type:none}`, outcome vẫn `provider_unavailable`.
- HR-OPS-D01 đổi scenario thành `{type:none}`, outcome vẫn `cancelled`.
- HR-OPS-D04 trỏ tombstone sang `src-card-invoice` với hash đúng, còn `sourceIds` của case vẫn là `src-card-zephyr-window`.

Sau khi sửa, cùng bốn clone báo:

- `HR-OPS-D05 quota setup does not reach a limit`
- `HR-OPS-H03 missing provider fault`
- `HR-OPS-D01 missing cancel action`
- `HR-OPS-D04 scenario source is outside the declared scope`

Validator chỉ đọc scenario đã có rồi so với outcome và `sourceIds`. Nó không tạo fault, quota, lệnh hủy hay nguồn từ outcome, câu hỏi hoặc ID. Ca `provider-fault` không bị ép thành lỗi nếu outcome không yêu cầu lỗi đó. Fixture 15/15 câu, token 1200/60000 vẫn hợp lệ vì hạn câu đã chạm. Các kiểm tra schema, hash, quyền và stale cũ vẫn giữ.

## File

- `TCTEnglish.Tests/HybridRagFixtureValidator.cs`
- `TCTEnglish.Tests/HybridRagEvaluationBaselineTests.cs`
- `docs/feature-audits/hybrid-rag/phase-0/baseline-results.md`
- `.ai/context/bug-fix-log.md`
- Manifest và file này

Không sửa fixture, `Program.cs`, appsettings, csproj, schema hay bài học thật. Review round 1, review round 2 và bàn giao round 1 được giữ.

## Kiểm tra

| Kiểm tra | Kết quả |
| --- | --- |
| Bốn clone trước khi sửa, `Phase0FixRound2`, filter `Validator_RejectsOutcomeAndSourceScopeMismatches` | 4 failed. Mỗi lỗi là `String: ""` |
| `dotnet test ... -c Phase0FixRound2 --filter FullyQualifiedName~HybridRagEvaluationBaselineTests` | 19 passed, 0 failed. Có fixture đóng băng, LF/CRLF, 4 đột biến, 8 clone cũ và 4 clone mới |
| Lệnh `--no-build` retriever, classifier, provider, composer, regression | 129 passed, 0 failed |
| `uv run --no-project python scripts/agent_tooling.py` | Exit 0. `Agent tooling verified (working tree).` |
| `uv run --no-project python -m unittest discover -s scripts/tests -v` | 135 test, 134 pass, 1 skip, 101.941s |
| `git diff --check` | Exit 0, không output |
| Browser, API trả phí, migration, CI, production | not-run |

CodeGraph CLI đọc `Validate` và `ValidateScenario` ở chế độ đọc. Không init, rebuild hay sync.

## Giới hạn

Pass 19 test không chứng minh chất lượng RAG. Đối chiếu oracle của round 1 vẫn nằm ở [oracle-validation.md](oracle-validation.md). Race token, cửa sổ 500 thẻ và Guide 9 vẫn là gap nguồn, chưa sửa runtime.

## Trạng thái

Phase 0: `AWAITING_REVIEW`. Phase 1–7: `LOCKED`. Không ghi `APPROVED`.

Tin nhắn gửi Codex:

> Cursor Auto đã nộp sửa Phase 0 round 2 cho HR-P0-R2-F01. Hãy review lại theo `.ai/prompts/hybrid-rag/codex-phase-review.md` và `docs/feature-audits/hybrid-rag/phase-0/fix-round-2-handoff.md`. Fixture revision 2 và hash canonical không đổi. Chưa đánh dấu finding là resolved. Nếu chưa đạt, giữ Phase 1 khóa. Nếu đạt, cập nhật review-state rồi mới cho phép phase kế tiếp.
