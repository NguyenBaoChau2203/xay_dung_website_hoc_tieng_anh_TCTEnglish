# Phase 6 — hoàn tất local, chờ review

> Cập nhật sau yêu cầu mới nhất của owner: [review round1](review-round-1.md) đã
> APPROVED đúng snapshot153 entry bên dưới; Phase7 READY, chưa triển khai.
> Phần bàn giao AWAITING_REVIEW/cấm duyệt dưới đây là lịch sử của lượt trước,
> được thay thế về gate bởi review mới; không cấp quyền API trả phí hoặc release.

Phase 6: **AWAITING_REVIEW**. Phase 7: **LOCKED**. Không APPROVED.
Đã hoàn tất sửa và bằng chứng local theo yêu cầu mới nhất; chưa cấp phép chạy
Phase 7. Không commit, push, PR, CI hoặc deploy; không gọi API trả phí.

Nhánh `feature/hybrid-rag-chatbot`, HEAD đầu/cuối
`6af768bd32d16cc6d012d484ee82df4cab805c69`.
Input151/151 đã khớp trước sửa:
`0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`.
Giữ manifest input nguyên byte trong `scope-manifest-implementation.json`.
Phase5 tiền nhiệm đã duyệt143:
`33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
Manifest trả về hiện tại: [scope-manifest.json](scope-manifest.json).
Scope cuối **153 entry**, fingerprint
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
Đã đọc lại manifest từ file và đối chiếu153/153 hash; bản input archive nguyên byte.

## Những điểm đã hoàn thiện

| Finding/hạng mục | Thay đổi và bằng chứng |
| --- | --- |
| Blocker browser A→B/hạ quyền/gỡ lớp với cache ấm | Cookie login và UI/endpoint thật trên MariaDB10.5.23 disposable. Có actual singleton hit trước đổi quyền trong TTL2phút. B không đọc được hội thoại A; hạ role/gỡ lớp trả notice, không body/title/link cũ. Không thay cache code để vượt test |
| HR-P6-C-F01, P1 — Stop trước stream id | Giữ cancelled attempt; khi Started đến trễ gửi Stop lên server, bỏ chunk/source chưa accept |
| HR-P6-C-F02, P1 — Stop/gửi lại và sự kiện cũ | Chặn transport mới khi invocation cũ chưa settle; bind catch với attempt/generation; mỗi attempt nhận một id, bỏ id đã kết thúc. Không tự fallback HTTP; giữ draft và retry chủ động |
| HR-P6-C-F03, P2 — khoảng trắng source reload | CSS tách whitespace bubble/source khỏi markdown, bỏ khoảng trống do indentation Razor; giữ body/code whitespace. Xác nhận bằng ảnh thật |
| Phần matrix còn lại | Load older thật117tin/115legacy; xóa nguồn; HTTP/SignalR và full/embed cùng body/source; hostile title, focus visible,390px,text200%; missing/corrupt index. Fault UI429/timeout/network và stream trễ được tiêm có ghi rõ tầng |

Mã sản phẩm sửa trong lượt này chỉ ở `wwwroot/js/ai-chat.js` và
`wwwroot/css/ai-chat.css`; regression ở `scripts/tests/test_ai_chat_phase6_sources.cjs`.
Bug log, checklist và prompt review được cập nhật. Giữ thay đổi Phase0–6 có sẵn;
không sửa Program.cs/appsettings/csproj/model/migration trong lượt hoàn tất này.

## Kiểm tra đã chạy

| Kiểm tra | Kết quả |
| --- | --- |
| Regression JS trước sửa | **2 failed / 5 passed** |
| Regression JS sau sửa | **7/7** |
| Node chat submit/paging/source | **14 passed / 0 failed** |
| App đầy đủ, configuration Phase6Closeout | **1136 passed / 22 skipped / 0 failed**, tổng1158,4m23s |
| Focused RAG/access/quota/cancel/provider | **224 passed / 0 skipped / 0 failed** |
| Final app build `--no-restore` | Succeeded,0 warning/0 error trên lần build cuối |
| EF pending model | Không có thay đổi |
| Disposable MariaDB10.5.23 | Đã áp dụng15migration; update lặp không áp dụng thêm, giữ dữ liệu fixture |
| Launcher5196, fixture5197/5198 | `/health/ready` đều200 trước cleanup |
| Speech metadata | configured,`gpt-transcribe`,VerifiedLive false; không request audio |
| Loopback18096 | requests0,paidCalls0; không file request log |
| Diff whitespace | `git diff --check` sạch |

22 skip là opt-in hiện hữu cho MariaDB/live audio/text/local-scale, không tính là
acceptance pass. TRX/log nằm dưới ignored `.tmp/hybrid-rag-review/phase-6/closeout/`.

## Tài liệu và giới hạn

- [Browser hoàn tất](browser-closeout.md): môi trường, hành động, actual cache hit,
  timestamps, before/after, screenshot và phân biệt fixture với transport thật.
- [Checklist và finding](../../../superpowers/plans/hybrid-rag/phase-6-local-completion.md).
- [Prompt review tiếp](../../../../.ai/prompts/hybrid-rag/phase-6-closeout-review.md).
- Báo cáo/handoff BLOCKED ban đầu giữ làm lịch sử, có link tới lần này.

Đây là kiểm tra local với dữ liệu tổng hợp. Chưa đánh giá semantic/latency hay
quota/account provider thực. Browser fault injection không phải provider E2E;
fake-HTTP .NET regression kiểm tầng provider riêng. Không dùng SQLite thay cho
ba hàng browser quyền truy cập. Auth form vẫn có cảnh báo SRI Bootstrap có sẵn;
không phát sinh do Phase6, không ảnh hưởng đăng nhập native hoặc chat CSS được
nghiệm thu; không mở rộng sửa Account trong lượt này.

Web5196/5197/5198 và provider18096 do lượt này tạo được dừng sau bằng chứng;
container fixture được stop và giữ storage để review. MariaDB dev owner, khóa
đã lưu và corpus owner giữ nguyên. Không cần đụng admin duy nhất của owner để
tái hiện. Fingerprint là danh tính bàn giao, **không phải phê duyệt**. Cần lượt
review/ủy quyền tiếp theo trước khi thay đổi gate Phase7.
