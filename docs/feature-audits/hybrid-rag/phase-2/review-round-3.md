# Phase 2 — Codex review và hoàn thiện trực tiếp

Ngày 2026-11-06. **APPROVED** cho Phase 2 trên snapshot local cuối đã kiểm chứng.
Owner yêu cầu kiểm tra phase hiện tại và hoàn thiện luôn phần còn dở;
quyền sửa local thay thế chế độ review-only của lượt trước. Một agent thực hiện
điều tra, sửa, tự review và kiểm chứng. Không triển khai Phase 3.

## Snapshot và phạm vi

- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- Manifest đầu vào khớp **67/67 entry**, fingerprint
  `ab0b31c22cad9b6bebddf802433684663e995f7de236c6caea56d6bad2c97b51`.
- Manifest cuối **70 entry**, fingerprint
  `62a864787561df4164da82030b24b590564fdce804d097a49b66c4ddc5a4dbdf`.
- Plan thực thi: [Phase 2 closeout](../../../superpowers/plans/hybrid-rag/phase-2-closeout.md).
- Thay đổi code trong lượt này nằm ở `LexicalAnswerAdmission`,
  `AiChatAnswerService`, `ChatExternalFactPolicy` và test `AiRagPhase2FixRound2Tests`.
  Giữ nguyên toàn bộ công việc đã có trong checkout.
- Không sửa thêm Program.cs; đăng ký OpenAiProviderClient và DI đã bàn giao giữ
  nguyên hash. Không sửa appsettings/.csproj, entity/DbContext, migration hay
  nội dung nguồn/shared-data. Snapshot build chỉ là dữ liệu dẫn xuất local đã ignore.

## Kết quả kiểm tra và sửa

| Finding | Kết quả kiểm chứng |
| --- | --- |
| HR-P2-R1-F01 | Giữ đóng: operation rõ ràng, auth/antiforgery, Dry-run không ghi, chống gửi lặp; đã kiểm form admin đăng nhập thật và bàn phím |
| HR-P2-R1-F02 | Đã kiểm lại: hai query framing trả đúng câu `#s2`, exact text/hash; biến thể chủ đề/vị trí câu và body-only/later chunk vẫn đạt |
| HR-P2-R1-F03 | Sửa phần còn thiếu: full phải khớp toàn bộ chủ thể, quan hệ và điều kiện trong cùng câu nguồn; không lấy thuộc tính của đối tượng khác; hướng dẫn trùng keyword không thay câu hỏi học liệu |
| HR-P2-R1-F04 | Giữ đóng: span/chunk v2 giới hạn 600 token xấp xỉ, nối lại đúng text/hash, snapshot v1 bị từ chối |

Trước sửa, regression mở rộng có **17 fail/6 pass**. Các negative bao gồm màu
của đèn cạnh marker, số người ở gần vật khác, nguyên nhân rung chuông thay cho
phát sáng, năm của đèn thay cho marker, người vẽ tranh thay cho người xây, vị trí
đối tượng khác, thiếu từ trong chủ thể, thiếu điều kiện midnight, và câu nhiều ý.
Hai positive `copper bell` và yêu cầu passage bị hướng sang guide không liên quan.

Đã thay heuristic any-number/any-color/any-because bằng một grammar extraction
hẹp, toàn câu. Câu có nguồn nhưng ngoài grammar trả thiếu bằng chứng; grammar
không phải công cụ hiểu mọi cách diễn đạt. Truy vấn từ khóa và yêu cầu xem đoạn
nguồn trả `answerScope=excerpt`, với nhãn trích nguyên văn, không báo full.
Reference ID/hash/text vẫn lấy từ nguồn đã recheck quyền/publication/hash.

Sửa oracle của positive nguyên nhân: nguồn chỉ nói marker glows because lamp is
lit không chứng minh điều kiện midnight. Positive hỏi glow không kèm điều kiện;
negative giữ query midnight, và positive khác có midnight thật trong nguồn.
Không sửa fixture evaluation đóng băng để đạt test.

Một biến thể polite có `about` còn rơi về Dashboard; regression mới thất bại
**1/14** trước sửa. Routing hiện xét chủ đề được nhắc nguyên văn trong học liệu
trước khi dùng guide trùng keyword. Đây chỉ chọn đường kiểm tra, không chứng minh
đủ dữ kiện. Unknown fact request vẫn phải qua admission và bị từ chối nếu thiếu.

Lượt toàn bộ đầu tiên tìm thêm **7 ca refusal** không đạt: homework, giải thích
không gắn nguồn, random fact và yêu cầu làm hộ Reading. Bốn deterministic case
cũng fail trên binary `Phase2FixRound2` trước lượt closeout. Đã giữ refusal cho
task không gắn nguồn; câu hỏi rõ về passage đã lưu vẫn được truy xuất. Không đổi
assertion refusal cũ để che lỗi, không gọi provider tự do.

## Bằng chứng local

- Build app và test bằng configuration riêng `Phase2Closeout` qua `dotnet test`:
  thành công; giữ các compiler warning có sẵn.
- Focused RAG + deterministic/production-classifier integration: **115/115**,
  0 fail/skip, khoảng 71 giây. Gồm positive nguồn thật, access/revoke, v2 snapshot,
  operation auth/antiforgery, F02 và mọi negative vừa sửa.
- Toàn bộ suite trên snapshot cuối: **1.044 pass, 0 fail, 19 skip / 1.063 tổng**,
  khoảng 3 phút 40 giây, TRX `phase2-approved.trx`. Các skip dành cho API trả phí,
  MariaDB nghiệm thu/disposable, migration-upgrade và benchmark opt-in; không phải
  tiêu chí Phase 2 bị bỏ qua. Không tuyên bố đã nghiệm thu các luồng opt-in này.
- Node submit guard/history order: **7/7**.
- Agent tooling validator: exit **0**; Python suite **135 tổng, 134 pass/1 skip**.
  Skip là contract dành cho non-Windows trên Windows. Tooling không thay app test.
- Development baseline: **15/17 top-1**, held-out scored **0**, 39 document,
  86.313 byte, build 141 ms, 17 query tổng 737 ms, RAM estimate 6.141.440 byte.
  `lexical-v1 / paragraph-sentence-v2 / vi-en-fold-v1`. Không tune BM25.
  Hai miss cũ giữ nguyên: `HR-NAV-D04->guide:study-flashcard` và
  `HR-NAV-D12->guide:daily-challenge`. Đây là baseline retrieval, không phải
  tỷ lệ trả lời đúng hoặc ngưỡng nghiệm thu cuối Phase 7.
- Fixture canonical hash giữ nguyên
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.

Evidence bỏ qua Git ở `.tmp/hybrid-rag-review/phase-2/closeout/`: TRX đỏ/xanh,
console, JSON browser và hai ảnh form desktop/narrow. Một lần build test trùng
thư mục với testhost đang chạy bị khóa DLL; giữ nguyên process và chạy lại sau
khi suite hoàn tất. Không dùng lượt build lỗi đó làm bằng chứng thành công.

## Browser admin thật

Khởi động bằng `scripts/start-local.ps1 -NoBrowser -Configuration Release
-WebPort 5100`. MariaDB ready, committed migrations đã current, bootstrap bỏ qua
DB có user. Dùng thông tin đăng nhập đã lưu local, không hiển thị credential.

- Route thật `/Admin/AiManagement`, content root `TCTEnglish`.
- Dry-run bằng **Enter** trên nút đang focus: redirect đúng trang, thông báo
  `dry-run: dry-run documents=471 chunks=487 exclusions=30`. Manifest bytes và
  danh sách thư mục snapshot giữ nguyên.
- Build được chọn rõ: `promoted`, **471 document / 487 chunk / 30 exclusions**,
  snapshot mới `s6466491683224781ace3ddbe570ff492`.
- Sau Build, Tab đi Dry-run → Build; Shift+Tab quay lại Dry-run, Enter chạy
  Dry-run. Byte manifest vẫn nguyên.
- Bấm đúp Dry-run khi POST được làm chậm 600 ms: **một POST** `dry-run`, cả hai
  nút disabled, hidden operation đúng và `dataset.submitted=true`; manifest không đổi.
- Desktop 1440 px: hai nút cùng hàng. Narrow 390 px: hai nút xếp dọc, rộng
  310 px, không tràn ngang. Đo sau khi responsive transition ổn định và kiểm ảnh.
- `/health/ready` **200** với `{"status":"ready"}`. Speech status `configured`,
  `VerifiedLive=false`; không có speech/embedding/generation trả phí.

Case chưa có index giữ bằng chứng browser của handoff sửa vòng 2 và HTTP
regression vòng 1; lượt này giữ snapshot hợp lệ sẵn có, không xóa để diễn lại.
Auth/antiforgery tiếp tục được kiểm qua test host. UI/admin/store không bị sửa
trong lượt closeout, vì vậy bằng chứng trước vẫn áp dụng cho phần đó.

## Giới hạn và gate

Phase 2 cung cấp lexical BM25 và extraction có giới hạn. Embedding, vector và
RRF thuộc Phase 3; grounding rộng, metadata bền và nghiệm thu live thuộc phase
sau. Không có thay đổi schema/content nên không cần migration/shared package.
Không chạy paid API, CI, commit/push/PR, staging hay production.

Không còn finding trong phạm vi Phase 2. Phase 2 **APPROVED**; Phase 3 **READY**,
chưa được thực thi. Phase 4–7 giữ LOCKED. Fingerprint cuối được ghi trong
[manifest](scope-manifest.json) và [review-state](../../../superpowers/plans/hybrid-rag/review-state.md).

Lệnh kiểm chứng chính:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2Closeout --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiProductionClassifierIntegrationTests'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2Closeout --no-build --no-restore --logger 'trx;LogFileName=phase2-approved.trx' --results-directory .tmp/hybrid-rag-review/phase-2/closeout
node --test scripts/tests/test_ai_chat_submit_guard.cjs scripts/tests/test_ai_chat_load_older_order.cjs
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```
