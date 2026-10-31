# Cursor Auto — sentinel và nghiệm thu còn lại

Một agent, Cursor Agent/Auto; giữ allowlist Grok/Composer 2.5. Không dùng AI agent/
CLI khác hoặc đổi model configuration. Reuse AGENTS.md/.ai/README.md, dùng
tct-engineering + tct-superpowers, CodeGraph exact symbols cho indexed source.

Đọc:

- `docs/feature-audits/2026-10-30-round2-verification-review.md`.
- `docs/superpowers/plans/2026-10-30-sentinel-and-acceptance-plan.md` scope/S0,
  sau đó đúng bước đang làm; vẫn làm hết các bước được owner giao.

Baseline `12ccec5`. Writing các ca đã báo, publisher actual tag/digest/run đã qua
review; async barrier đã cải thiện. Không mở lại các việc đó nếu không có lỗi mới.
S01 còn lại: factory gọi EnsureSentinelAsync rồi mới VerifyOnlineIdentityAsync;
missing/wrong sentinel bị CREATE/UPSERT thành đúng trước DELETE. Các offline guard
đã có không chữa được thứ tự này. Không tự stamp để vượt kiểm tra.

S0 negative orchestration tests trước sửa → S1 tách bootstrap có ownership khỏi
fixture verify/reset → S2 provider tests thật → S3 engine/browser/staging còn thiếu
→ S4 full suite/CI/ledger. Viết regression đỏ, sửa tối thiểu, green và review diff
inline rồi tiếp tục bước độc lập, không chỉ trả plan mới hoặc dừng sau một fix.

Connection giả để test missing/wrong purpose/run, assert không write trước reject.
Chỉ sau guard xanh mới tạo DB acceptance ephemeral do harness sở hữu, có identity
riêng. Không dùng DB dev/hosting, không xóa volume/workload khác. Giữ net9/EF9/
Pomelo9, không thêm package hay sửa migrations đã apply để phục vụ fixture.

Ghi ledger `docs/feature-audits/2026-10-30-sentinel-acceptance-ledger.md`:
ID/file/command/expected/actual/SHA/artifact/limit. Không giữ pending push sau có
commit. CI lịch sử 710 pass/1 skip không chứng minh provider đã chạy ở run đó.

ASR tiny.en đã có hash nhưng thiếu server; UI host 5110 chưa dựng. Đây là các bước
setup cần thực hiện trong phạm vi đã giao, không mặc định gán cho owner. Nếu thiếu
tool/quyền/tài nguyên thực tế, ghi command/lỗi/đầu vào và làm tiếp bước độc lập.
LanguageTool/Ollama giữ tắt nếu chưa được chọn. Không tự bật production gate/Plesk/
deploy/merge hoặc gửi secrets/audio/PII ra ngoài.

Quyền sửa/commit/push/CI lấy từ chat owner. Review này chỉ tạo tài liệu, không tự
triển khai. Nếu owner giao, giữ quyền đó giữa các bước, không xin lại chung chung.

## Chat copy

```text
Dùng Cursor Agent, Auto trong allowlist Grok/Composer 2.5, một agent duy nhất.
Đọc .ai/prompts/cursor-auto-sentinel-and-acceptance.md, review docs/feature-audits/2026-10-30-round2-verification-review.md và làm S0–S4 trong docs/superpowers/plans/2026-10-30-sentinel-and-acceptance-plan.md.
Tôi giao sửa S01, thêm regression orchestration trước sửa và làm tiếp provider/ASR/browser/staging local trong phạm vi plan; cho phép Docker/DB synthetic riêng, ASR English <=500 MiB có pin/hash/license. Không đụng DB/volume dev, hosting, package/target hoặc migration đã apply.
Khi checks pass, commit theo nhóm, push feature và chạy CI đúng SHA. Không tự merge, đổi Plesk, bật gate hay deploy production. Ghi ledger/evidence và đầu vào thật sự thiếu; không dừng sau sửa guard hoặc hỏi lại quyền vừa giao.
```
