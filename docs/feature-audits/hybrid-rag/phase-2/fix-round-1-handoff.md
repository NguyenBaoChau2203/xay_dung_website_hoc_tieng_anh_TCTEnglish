# Phase 2 — handoff sửa vòng 1

Ngày 2026-11-06. Cursor Auto thực hiện `.ai/prompts/hybrid-rag/phase-2-fix-round-1.md`. Kết quả là **AWAITING_REVIEW**. Phase 3–7 giữ LOCKED. Agent không tự duyệt.

## Danh tính

- Nhánh `feature/hybrid-rag-chatbot`. HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`. Chưa commit.
- Trước khi sửa, fingerprint 60 entry `ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a` khớp. Phase 2 đang CHANGES_REQUESTED, rồi được owner mở repair.
- Đây vẫn là retrieval từ vựng. Chưa có embedding hay câu trả lời model tự do.
- `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không sửa appsettings, `.csproj`, model, migration, shared data. Không gọi API trả phí, không commit/push/PR/CI/deploy.
- Chunker mới là `paragraph-sentence-v2`. Snapshot `paragraph-sentence-v1` bị từ chối. Admin cần bấm xây chỉ mục lại trước khi index cũ được dùng.

## Finding

| ID | Trạng thái | Bằng chứng |
| --- | --- | --- |
| HR-P2-R1-F01 | Đã sửa, chờ Codex kiểm | Form không còn `dryRun`. Submit ghi `operation` rồi mới disable nút. Thiếu hoặc sai operation thì redirect, không tạo manifest. Test host: Dry-run khi chưa có index và khi đã có index không đổi byte manifest; Build tạo snapshot; anonymous không ghi; Standard bị chặn; thiếu antiforgery trả 400. |
| HR-P2-R1-F04 | Đã sửa, chờ Codex kiểm | Câu `harborword` 29.700 ký tự, 7.425 token xấp xỉ, được chia span liền kề. Mỗi span và mỗi chunk sau khi nạp lại đều không quá 600 token xấp xỉ. Hash là hash của span, không phải của cả câu. Nối span trả lại đúng câu gốc. |
| HR-P2-R1-F02 | Đã sửa, chờ Codex kiểm | `Welcome to the lesson. The tidal harbor marker is visible at dawn.` với `zzzz tidal harbor marker qqqq` trả câu chứa marker, không trả câu chào. Đã kiểm đầu, giữa, cuối, heading, chunk sau và hai câu trùng. Gỡ publish vẫn recheck trước khi trả evidence. |
| HR-P2-R1-F03 | Đã sửa, chờ Codex kiểm | Với 12 retriever production và lexical service, câu why/how many tiếng Anh và tiếng Việt, cùng câu why về red lantern, không còn Answered/full, evidence rỗng, không gắn Flashcard/Add card. `Where is the tidal harbor marker visible?` và `zzzz tidal harbor marker qqqq` vẫn trả đúng câu. Đổi mật khẩu vẫn ra `/Account/Settings`. Giá vàng vẫn ngoài phạm vi. `vang nghia la gi` vẫn trả definition đã lưu. |

## Kiểm tra local

| Kiểm tra | Kết quả |
| --- | --- |
| Đỏ trước sửa, `Phase2FixRound1`, bốn test form/chunk/span/admission | 4 fail, 0 pass |
| `AiRagPhase2FixRound1Tests` sau sửa | 5 pass |
| Filter plan, gồm corpus, boundary, chat, quota, cancel, history, provider consumer, retriever, baseline | **227 pass**, 0 fail/skip, khoảng 32 s |
| OpenAiProviderClient, MlNet, ba nhóm review-probe, `--no-build` | **65 pass** |
| `scripts/agent_tooling.py` | exit 0 |
| `python -m unittest discover -s scripts/tests -v` | **135** test, 134 pass, 1 skip |
| `git diff --check` | không có lỗi whitespace |
| Fixture `hybrid-rag-evaluation.json` | `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f` không đổi |
| Baseline development | 17 câu guide, top-1 **15/17**, held-out scored **0**. Corpus `lexical-v1`, chunker `paragraph-sentence-v2`, normalizer `vi-en-fold-v1`. 39 document, 86.313 byte, build khoảng 198 ms, query khoảng 735 ms, RAM ước lượng khoảng 7.4 MB. Miss: HR-NAV-D04 → `guide:study-flashcard`; HR-NAV-D12 → `guide:daily-challenge` |
| Schema | không đổi, không có migration |
| MariaDB | không chạy. SQLite không chứng minh provider hosting |
| CI / production | không chạy |

Browser: handler submit thật đã được bấm. Dry-run gửi `operation=dry-run` cùng token và disable cả hai nút. Nút lần hai không bấm được vì đã disable. Build gửi `operation=build`. Đây là serialization, chưa phải trang admin đăng nhập trên launcher. POST admin, antiforgery, quyền và byte snapshot được kiểm trên test host, không phải trên site local của owner. Launcher canonical không được mở, nên không có ảnh trang admin đang chạy.

## Giới hạn

Hai miss baseline giữ nguyên, không chỉnh BM25. Token vẫn là độ dài/4. Snapshot cũ không tự xóa. Index v1 phải rebuild. Chưa có dense retrieval.

## Việc Codex cần xem

Đối chiếu bốn finding trên snapshot mới. Xác nhận Dry-run không promote, span quá dài không vượt trần, câu evidence là câu khớp, và câu why/how many không còn full. Phase 2 đang **AWAITING_REVIEW**. Không mở Phase 3.
