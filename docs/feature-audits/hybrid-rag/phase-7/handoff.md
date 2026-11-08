# Phase 7 — bị chặn trước lệnh trả phí

> Review 2026-11-08: [round1 — CHANGES_REQUESTED](review-round-1.md).
> Cần sửa envelope retry/aggregate, protocol dữ liệu/actor và hai test timing;
> hoàn tất preflight không trả phí khi Docker khả dụng. Bàn giao dưới đây giữ làm
> input lịch sử; A/B chưa phải kế hoạch đã duyệt. Phase7 vẫn BLOCKED, chưa gọi API.

Phase 7: **BLOCKED**. Không APPROVED. Không có Phase 8. Chưa gọi text,
embedding hoặc speech. Chưa commit, push, PR, CI hoặc deploy.

Owner khởi chạy bằng fingerprint Phase 6
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
Trước khi sửa, 153/153 hash khớp. Nhánh `feature/hybrid-rag-chatbot`, HEAD đầu
và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`.

## Việc đã xong và việc đang chặn

Fixture revision 2 còn nguyên, canonical
`1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`: 120 ca,
80/40, đúng đếm bảy hạng mục. Không ca nào bị loại khỏi mẫu số.

Snapshot lexical local `s6466491683224781ace3ddbe570ff492` có 471 tài liệu và
487 chunk, checksum `cdbfb0dd5f7d65f734536afaabee5241fdfa794b3595596b93769eb88e286d10`.
Chưa có embedding cache hay vector index. Trần giữ chỗ UTF-8 của cả index là
113.887 byte, cao hơn trần đề xuất 100.000, nên chưa xây index.

Envelope và 16 câu diễn tập:

- [Dry-run envelope](dry-run-envelope.md) — xin owner chọn phương án A hoặc B.
- [Demo script](demo-script.md) — chưa chạy trên trình duyệt.

Chưa chạy `embed-dry-run` trên MariaDB, nên chưa chứng minh DB còn khớp snapshot.
Chưa diễn tập cold start, `/health/ready`, speech-status hay mất mạng. Những
bước đó không thay cho phép đo live.

## Kiểm tra

| Kiểm tra | Kết quả |
| --- | --- |
| Node submit/paging/source | 14 passed / 0 failed. `TCT_OPENAI_LIVE`, `TCT_OPENAI_FEATURE_LIVE`, `TCT_OPENAI_SPEECH_LIVE` đều trống |
| Focused RAG/access/quota/provider, configuration Phase7Acceptance | 382 passed / 3 skipped / 1 failed, tổng 386, 1 phút 3 giây. Fail: `CallerCancellation_DuringBackoff_Propagates`, expected 1 call, actual 2. TRX `.tmp/hybrid-rag-review/phase-7/phase7-focused.trx` |
| Cùng test backoff, chạy riêng | 12/12 passed |
| App suite `--no-build` cùng binary | 1135 passed / 22 skipped / 1 failed, tổng 1158, 1 phút 55 giây. Fail: `Deadline_CoversBodyRead_AndDoesNotResetForRetry`, `ErrorCodeTimeout` đã đúng, elapsed 2.8705 giây ngoài khoảng 0.8–2.5. TRX `phase7-app.trx` |
| Cùng test deadline, chạy riêng | 8/8 passed |
| Build app `--no-restore` | Succeeded, 0 warning, 0 error |
| `git diff --check` | sạch |

22 skip là opt-in MariaDB, live audio/text và local-scale, cùng loại với lần
Phase 6 (1136/22/0 trên tổng 1158). Lần này không phải 0 failed. Hai fail là
cửa sổ đồng hồ dưới tải song song; mã provider không đổi trong lượt này và cả
hai test xanh khi chạy một mình. Không nới assertion.

Ba skip của lượt focused là ba test `MariaDbAiTurnLifecycleTests`.

## Phạm vi sửa

Không sửa product, test, config, model hay migration. Tài liệu mới nằm trong
`docs/feature-audits/hybrid-rag/phase-7/`. Fingerprint Phase 6 vẫn khớp 153/153
sau khi `review-state.md` chuyển Phase 7 sang BLOCKED, vì file trạng thái nằm
ngoài digest đó. Scope Phase 7 có 155 entry, fingerprint
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`. Manifest:
[scope-manifest.json](scope-manifest.json).

## Chưa được coi là nghiệm thu

Chưa có Recall@5, câu trả lời live, latency p95, usage thật hay diễn tập trình
duyệt. Index dense local vẫn trống. Phương án A, nếu được duyệt, chỉ đo 12 ca
đã liệt kê và vẫn để index trống. Phương án B cần trần mới ít nhất 80 POST và
169.087 token giữ chỗ. Chưa duyệt phương án nào.
