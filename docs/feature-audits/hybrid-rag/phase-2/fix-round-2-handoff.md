# Phase 2 — handoff sửa vòng 2

Ngày 2026-11-06. Cursor Auto thực hiện `.ai/prompts/hybrid-rag/phase-2-fix-round-2.md`. Kết quả là **AWAITING_REVIEW**. Phase 3–7 giữ LOCKED. Agent không tự duyệt và không mở Phase 3.

## Danh tính

- Nhánh `feature/hybrid-rag-chatbot`. HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`. Chưa commit.
- Trước khi sửa, fingerprint 64 entry `67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a` khớp, không lệch. Phase 2 được chuyển IN_PROGRESS rồi trả AWAITING_REVIEW.
- Fingerprint tích lũy sau vòng 2: 67 entry `ab0b31c22cad9b6bebddf802433684663e995f7de236c6caea56d6bad2c97b51`. Predecessor là fingerprint đã review `67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a`, chưa được duyệt.
- Đây vẫn là retrieval từ vựng. Không có embedding, dense retrieval hay câu trả lời model tự do.
- `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không sửa `Program.cs`, appsettings, `.csproj`, model, migration hay shared data. Không gọi API trả phí, không commit/push/PR/CI/deploy.
- Chunker giữ `paragraph-sentence-v2`. Không cần đổi chunker cho F02/F03. Snapshot `paragraph-sentence-v1` vẫn bị từ chối cho đến khi admin build lại.

## Finding

| ID | Trạng thái | Bằng chứng |
| --- | --- | --- |
| HR-P2-R1-F01 | Giữ đóng | Không viết lại luồng operation. Test form/HTTP vòng 1 vẫn xanh. Trang admin đã đăng nhập xác nhận Dry-run và Build. |
| HR-P2-R1-F04 | Giữ đóng | Không đổi chunker. Test span 600 token và từ chối v1 vẫn xanh. |
| HR-P2-R1-F02 | Đã sửa, chờ Codex kiểm | Hai câu review, title `Daily notes`, keyword A1/Sea, trả đúng câu marker với text, hash và id `#s2`. Biến thể copper bell, đổi thứ tự từ khung, intro cạnh tranh, và câu đầu thật sự đúng chủ đề cũng giữ đúng span. Boundary trả câu marker, không trả câu mở đầu. |
| HR-P2-R1-F03 | Đã sửa, chờ Codex kiểm | Năm người / năm 2008, glow / because sơn lại, where visible / at dawn, when constructed / at dawn, who và màu / at dawn, cùng câu when+who, đều InsufficientEvidence, scope không full, evidence rỗng, đúng câu từ chối. Oracle vòng 1 của where/visible đã đảo thành negative. |

## Cách sửa

`QueryOverlap` tách token khung khỏi token nội dung. Một token nội dung được nhân 100, nên câu chứa chủ đề thắng câu chỉ trùng find/in/this/lesson. Không gắn cứng từ marker hay vị trí câu.

`LexicalAnswerAdmission` chỉ nhận span nêu đúng facet được hỏi. Năm lịch không phải số người. Mệnh đề because của việc khác không phải nguyên nhân được hỏi. `at dawn` không phải địa điểm và không phải thời điểm xây dựng. Who, màu, và frame không nhận ra không rơi về Reference. Câu nhiều facet chỉ full khi cùng một span nêu đủ các facet. Câu không có frame sự kiện vẫn được trích dẫn khi span chứa chủ đề.

## Kiểm tra local

| Kiểm tra | Kết quả |
| --- | --- |
| Đỏ trước sửa, filter `AiRagPhase2FixRound` | 5 fail, 4 pass |
| Xanh sau sửa, cùng filter | 9/9 |
| Filter plan Phase2FixRound2 | **231 pass**, 0 fail/skip, khoảng 35 s |
| Provider, ML.NET, probe, `--no-build` | **65 pass**, 0 fail/skip, khoảng 14 s |
| `scripts/agent_tooling.py` | exit 0 |
| `unittest discover -s scripts/tests` | 135 pass, 1 skip |
| `git diff --check` | sạch |
| Baseline development | 17 câu, top-1 **15/17**, held-out scored **0**. Corpus `lexical-v1`, chunker `paragraph-sentence-v2`, normalizer `vi-en-fold-v1`. 39 document, 86313 byte, build 230 ms, query 785 ms, RAM ước lượng 7471288. Miss giữ nguyên: `HR-NAV-D04->guide:study-flashcard`, `HR-NAV-D12->guide:daily-challenge` |
| Fixture canonical | Test đóng băng trong filter vẫn xanh. Hash `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f` không đổi |

SQLite chỉ chứng minh test host. Không có đổi schema nên không có kiểm MariaDB migration. Không chỉnh trọng số BM25.

## Admin trên trình duyệt

Launcher `scripts/start-local.ps1 -NoBrowser`. `/health/ready` trả ready. Cổng 5100. Content root `TCTEnglish`. Đăng nhập bằng tài khoản local đã lưu, không ghi mật khẩu vào báo cáo.

Trước thao tác, `TCTEnglish/.appdata/local/ai-rag` không tồn tại. Trang hiện `missing`, snapshot “Chưa có bản hợp lệ”, 0/0.

- Dry-run khi chưa có index: redirect về `/Admin/AiManagement`, thông báo `dry-run: dry-run documents=471 chunks=487 exclusions=30`, thư mục index vẫn không có.
- Build: redirect, trạng thái `ready`, `promoted: promoted documents=471 chunks=487 exclusions=30`, snapshot `s9fe2b15587a448339c97b55446bf7e17`, 471/487, lúc build 06/11/2026 16:34. File local: `manifest.json` 413 byte, `corpus.json` 887256 byte, `corpus.sha256` 65 byte.
- Dry-run khi đã có index: cùng snapshot và cùng hash ba file. Không thêm snapshot.
- Lúc bấm, cả hai nút bị disable. Thử bấm Dry-run lần hai trên form đang gửi thì không có submit thứ hai; field ẩn là `dry-run`, `dataset.submitted` là true, name của nút đã được gỡ.
- Desktop: hai nút nằm cùng hàng. Viewport 390 px: hai nút xếp dọc, rộng khoảng 310 px.
- Nút Dry-run nhận focus. Phím Tab/Enter do công cụ trình duyệt gửi không kích hoạt hành vi mặc định của trình duyệt. `requestSubmit` của đúng nút Dry-run đang focus đã gửi dry-run và không đổi byte snapshot. Auth và antiforgery vẫn nằm ở regression HTTP vòng 1.

Index vừa build chỉ nằm trong `.appdata` local đã ignore. Không phải CI và không phải hosting.

## Phạm vi giữ nguyên

F01, F04, tra cứu thẻ, hướng dẫn đổi mật khẩu, giá vàng ngoài phạm vi, và fact của user hiện tại vẫn đi đúng luồng cũ. Không có Phase 3–7. Không tự duyệt.
