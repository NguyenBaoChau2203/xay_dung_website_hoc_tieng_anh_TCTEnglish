# Phase 6 — review cuối và duyệt snapshot local

Kết quả ngày **2026-11-07: APPROVED** cho đúng snapshot153 entry,
fingerprint `68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
Phase7 chuyển **READY**, chưa IN_PROGRESS, chưa triển khai và chưa được duyệt.

## Phạm vi và ủy quyền hiện tại

Yêu cầu mới nhất của chủ repo: “hoàn thiện phase 6 luôn cho tôi để tôi qua phase 7”.
Lượt này hoàn tất review/gate Phase6 để chuyển bước. Yêu cầu này thay thế việc giữ
AWAITING_REVIEW/cấm APPROVED ở lần bàn giao trước cho mục đích chốt Phase6;
không cấp quyền chạy Phase7, API trả phí hay thao tác Git/release.
Các prompt/checklist/handoff trước giữ nguyên làm lịch sử của lượt triển khai.
Trạng thái hiện hành nằm trong review này và review-state.md.

Một agent thực hiện trực tiếp, không agent phụ. Nhánh
`feature/hybrid-rag-chatbot`; HEAD đầu/cuối
`6af768bd32d16cc6d012d484ee82df4cab805c69`. Không thay mã ứng dụng, test, config,
model, migration hoặc fixture đánh giá trong lượt chốt review này.

## Danh tính và kiểm tra source

- Tính lại SHA256 raw bytes của153/153 file: khớp manifest đã bàn giao; tính lại
  fingerprint ordinal cũng khớp chuỗi trên. Không có drift source/test/guidance.
- Predecessor Phase6 triển khai151 entry:
  `0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`.
  Bản manifest input giữ nguyên byte trong `scope-manifest-implementation.json`.
- Phase5 được duyệt143 entry:
  `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
- Đọc source hiện hành qua CodeGraph cho invocation/event ownership,
  cache/authorization callers, source-link validation và diagnostics; đối chiếu
  contract cùng regression. Source cache chỉ lưu title/route/type, tối đa128entry,
  TTL2phút; reauthorize nguồn trước reuse, không chia sẻ personal answer body.
  Scope gồm user, role/quyền media, membership/ownership, snapshot và source version.
  Thu hồi nguồn xóa cache của user/source; không lấy TTL làm quyền truy cập.

## Finding và blocker đã đóng

| Mục | Kết luận review |
| --- | --- |
| Browser A→B với cache ấm | Đã có đăng xuất/đăng nhập thật. Native page và Messages của hội thoại A trả404 cho B; B hỏi cùng câu chỉ nhận card của B. Đối chiếu actual cache hit/timestamps, không dùng SQLite thay browser |
| Browser Premium→Standard | Cookie A còn cũ nhưng DB role đã Standard; reload full/embed và JSON đều bỏ body/title/link media riêng, status unavailable/sources rỗng, trong TTL cache |
| Browser gỡ membership | Có hit trước revoke, sau revoke không còn body/title/link lớp trong DOM/JSON; owner và lớp vẫn tồn tại trên fixture |
| HR-P6-C-F01 — late Stop | Attempt cancelled được giữ; Started đến trễ nhận StopAiStream. Placeholder/text/source chưa accept bị bỏ, draft giữ nguyên |
| HR-P6-C-F02 — Stop/retry ownership | Không transport mới khi invocation cũ chưa settle; không fallback HTTP; catch gắn attempt/generation; id đã kết thúc không chiếm lượt mới. Ordered/deduplicated chunks chỉ hiện sau accepted complete |
| HR-P6-C-F03 — source whitespace | Bubble/section bỏ whitespace template, markdown giữ định dạng; ảnh/DOM xác nhận vùng source gọn và focus nhìn thấy được |
| Ma trận UI còn lại | Đã kiểm real paging117id/115legacy đúng thứ tự, xóa nguồn, HTTP/SignalR/full/embed, hostile title,390px và text200%, missing/corrupt index; các fault UI có ghi rõ cách tiêm |

Không còn finding actionable chưa xử lý trong phạm vi Phase6 được review.
Không tạo repair plan rỗng. Không chỉnh assertion hoặc dữ liệu để miễn gate.

## Verification và loại bằng chứng

| Kiểm tra | Lượt chốt review này |
| --- | --- |
| Scope raw hashes/fingerprint |153/153 khớp |
| Node source/Stop/submit/paging |14passed,0failed |
| Focused RAG/access/quota/cancel/provider |224passed,0skipped,0failed;30s; TRX `phase6-approval-focused.trx` |
| Build Phase6Closeout `--no-restore` |Succeeded,0warning,0error |
| Browser artifacts |Đọc/kiểm assertion trên JSON thật; đối chiếu actual cache probes và bằng chứng trình duyệt đã chạy ở lượt hoàn tất trong cùng chat |
| Full app trên đúng source snapshot |Tái kiểm TRX của lượt hoàn tất:1136passed,22opt-in skipped,0failed,total1158; không chạy lại toàn bộ vì source không đổi và focused/build đã xanh |
| EF/MariaDB |Dùng bằng chứng pending-model không đổi và update lặp không áp dụng migration của cùng source; không khởi động/reset DB thêm trong lượt review |
| Whitespace/Git |`git diff --check` sạch; branch/HEAD giữ nguyên |

Đã kiểm dữ liệu trong `account-b-isolation.json`, `account-b-own-reply.json`,
`class-citation-before.json`, `class-citation-after.json`,
`media-downgrade-verified.json`, `real-paging.json`, `source-deleted.json`,
`browser-stream-green.json`, `focus-final.json`, `narrow-final.json`,
`zoom-final.json` và `cache-probes.json`. JSON assertions pass. Các mốc actual hit:
A10:44:51Z hit1; class10:48:47Z hit2 rồi revoke10:48:53Z entries3→2;
media10:53:53Z hit5 rồi downgrade10:53:57Z entries3→2. Đều trước TTL expiry.

Xem [browser-closeout.md](browser-closeout.md) cho môi trường, DOM/HTTP/screenshot,
fake-versus-real transport và procedure tái hiện. Không chạy lại browser trong lượt
chốt; bằng chứng browser do cùng agent trực tiếp chạy ở lượt trước của chat này,
source153 vẫn khớp. Helpers/ảnh/log/TRX là ignored local evidence trong
`.tmp/hybrid-rag-review/phase-6/closeout/`, không phải fixture portable đã commit.

Các lỗi UI429/timeout/network và event ordering được tiêm có kiểm soát trên trang
thật. Provider fake-HTTP regression là bằng chứng tầng riêng. Browser không có
provider request; không gọi chúng là provider E2E hoặc live acceptance.22skip là
opt-in hiện hữu, không được tính pass. Cảnh báo SRI ở form Account đã có sẵn,
ngoài thay đổi Phase6 và không chặn native login/chat acceptance; không mở rộng
sửa Account. Không dùng cảnh báo đó để bỏ yêu cầu browser bắt buộc nào.

## Gate và bàn giao Phase7

**APPROVED Phase6** chỉ cho fingerprint153 entry nêu trên. **READY Phase7**
cho phép chủ repo khởi chạy bước tiếp theo theo plan/prompt đã chuẩn bị; chưa
khởi chạy trong lượt này. Scope manifest giữ153file/hash cũ, chỉ bổ sung metadata
review/state/exclusion cho báo cáo này. Không đổi source fingerprint khi đổi gate.

Phase7 cần đo thật chất lượng lexical/dense/hybrid, factual support, latency và
live account trên corpus đã freeze. Những bằng chứng đó chưa có và không được
suy ra từ Phase6. Prompt Phase7 hiện hành yêu cầu lập dry-run envelope trước;
ngân sách60POST/100000token trong plan chỉ là đề xuất, chưa phải quyền chi tiền.
Không gọi text/embedding/speech trả phí trước ngân sách cụ thể được owner duyệt.

Không commit/push/PR/CI/merge/deploy hoặc đổi production/staging gate. Không đổi
secrets, DB dev, corpus owner hay workloads đang có. Các process/container fixture
của lượt hoàn tất đã được dừng và giữ evidence/storage.

Tài liệu chuyển bước:
- [Phase7 plan](../../../superpowers/plans/hybrid-rag/phase-7-demo-acceptance.md)
- [Phase7 execution prompt](../../../../.ai/prompts/hybrid-rag/phase-7-demo-acceptance.md)
- [Review state](../../../superpowers/plans/hybrid-rag/review-state.md)
