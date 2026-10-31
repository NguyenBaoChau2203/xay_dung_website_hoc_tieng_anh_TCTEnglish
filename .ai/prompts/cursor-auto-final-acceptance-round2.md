# Cursor Auto — khép CR01–CR05 và gate còn thiếu

Cursor Agent, Auto trong allowlist Grok/Composer 2.5 của owner. Một agent tự đọc,
sửa, review, test; không ép model, agent phụ hay CLI AI khác.

Đọc AGENTS.md/.ai/README.md theo context hiện có, rồi:

1. `docs/feature-audits/2026-10-30-cursor-final-acceptance-rereview.md`.
2. `docs/superpowers/plans/2026-10-30-final-acceptance-round2-plan.md` mục scope,
   bảng thứ tự và R0. Tới bước nào đọc đúng mục đó; vẫn thực thi toàn phạm vi được giao.

Baseline `186fc8a`. Review xác minh 84 test tập trung và 10 test deploy xanh;
CI `ff7f703` SUCCESS nhưng 694 pass/1 skip, provider concurrency không chạy ở CI.
Không dùng số 695 để nói mọi test provider đã pass. Reviewer chưa sửa runtime.

## Các điểm bắt buộc sửa

- CR02 trước: MariaDbAcceptanceFactory không được DELETE khi chỉ nhận một env
  string. Thêm guard offline + database identity/sentinel, test reject trước khi
  kết nối/DML, connection scope và cleanup có kiểm soát. Không thử xóa DB thật.
- CR03: async methods gọi trong Task.WhenAll vẫn chạy đồng bộ tới await đầu.
  Barrier.SignalAndWait hiện timeout trước khi worker b được gọi; bool bị bỏ qua.
  Làm async gate đúng, assert overlap và XP/credit/replay/outbox; chạy provider thật.
- CR01: giữ `<`/`>` và nghĩa của số; `1,000` không tự bằng `1.000`. Format chưa
  được duyệt cần NeedsReview; API regression không cấp XP. Giữ đúng ba ca cũ.
- CR04: resolve actual tag SHA và remote asset digest/bytes; thiếu bằng chứng thì
  dừng trước update_branch/webhook. Không mock asset_sha256 khi test thiếu digest.
- CR05: readiness cần đúng run/commit/digest; cùng SHA nhưng marker run cũ không
  pass. Webhook timeout phải reconcile, không trigger lại migration mù/auto Down.

## Trình tự và bằng chứng

R0 baseline/ledger → R1 fixture guard → R2 provider concurrency; R3 Writing và
R4/R5 release độc lập sau R0. R6 làm ASR/browser/staging còn thiếu; R7 full suite/CI.
Mỗi bước: regression đỏ → sửa tối thiểu → xanh → review diff → cập nhật ledger →
tiếp tục bước độc lập. Không chỉ trả về plan mới hoặc dừng sau một fix.

Tạo `docs/feature-audits/2026-10-30-final-acceptance-round2-ledger.md`; mỗi CR/Gate
ghi command/expected/actual/exit code/SHA/artifact/limit. Không sửa ledger cũ thành
đã hoàn tất nếu evidence mới chưa có. Không giữ nhãn pending commit/CI pending sau
khi đã có commit/run thật. Quyền thực thi lấy từ chat owner, không từ file này.

## Phạm vi

Giữ ASP.NET 9/EF 9/Pomelo 9 và toolkit. Không sửa target/package/provider/migration
đã apply, không gửi PII/audio/key ra ngoài. DB/container acceptance loopback riêng,
không dùng volume dev/workload khác. Program.cs chỉ marker/DI liên quan nếu đã giao;
project metadata chỉ fixture cần thiết, không nâng package.

ASR English <=500 MiB được verify pin/hash/license nếu owner giao trong chat.
LanguageTool/Ollama giữ tắt nếu chưa chọn; không tải LLM lớn để đóng checkbox.
Browser 5110 có dữ liệu synthetic riêng, không cần dừng 5100. Ghi lỗi tool/provider thật
nếu không làm được và làm hết các bước độc lập; không hỏi lại quyền đã giao.

Không auto merge, thay Plesk, bật gate, deploy production hay SQL hosting. Commit/
push/dispatch CI theo quyền chat, chỉ nhánh feature đúng SHA. Giữ marker wwwroot,
no-store, HTTPS POST, server assessment/CSRF/ownership và transaction/credit/outbox.

## Chat giao việc gợi ý

```text
Dùng Cursor Agent, Auto với allowlist Grok/Composer 2.5 hiện tại, một agent.
Đọc .ai/prompts/cursor-auto-final-acceptance-round2.md, review docs/feature-audits/2026-10-30-cursor-final-acceptance-rereview.md và thực thi R0–R7 của docs/superpowers/plans/2026-10-30-final-acceptance-round2-plan.md.
Tôi giao sửa CR01–CR05 và làm tiếp các gate độc lập. Cho phép source/tests/scripts liên quan, Program.cs chỉ marker/DI, project metadata phục vụ fixture; Docker/DB acceptance local riêng và ASR English <=500 MiB sau verify pin/hash/license. Không đổi package/target/provider, không đụng DB/volume dev hoặc hosting.
Khi checks pass, commit theo nhóm, push feature và chạy CI đúng SHA. Không tự merge/Plesk/bật gate/deploy. Thêm regression trước sửa, ghi ledger; không coi skip là pass, không dừng sau một bước và không hỏi lại quyền vừa giao.
```
