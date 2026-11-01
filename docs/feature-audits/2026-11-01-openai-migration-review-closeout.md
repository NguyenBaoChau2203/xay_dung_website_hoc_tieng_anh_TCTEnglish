# Review Plan 2 và chốt launcher local — 01/11/2026

Một agent Codex review/sửa/xác minh theo quyền owner. Nhánh
`codex/whisper-local-lifecycle`, HEAD `03c9c5ce8d30e84a5320985b90255bbbccf922ec`.
Giữ diff Plan1/Plan2 chưa commit. Không Git publish, CI remote hoặc production.

## Kết quả review

Provider của ứng dụng là OpenAI Responses, **gpt-6-luna + reasoning.effort=low**;
audio dùng Whisper. Codex/IDE model độc lập. Review source và các regression
không phát hiện thêm lỗi chặn ở serializer/parser/DI/consumer/media đã kiểm.
Không gọi kết quả này là nghiệm thu đầy đủ mọi feature trên browser/production.

| Finding | Trigger trước sửa | Sửa và bằng chứng |
| --- | --- | --- |
| R01 P2 — key local không được nối | OPENAI_API_KEY có giá trị nhưng app AI:ApiKey trống; launcher cũ cố ý không map | Owner xác nhận dùng nguồn này. Initialize-LocalAiEnvironment ưu tiên app env → app User Secrets → OPENAI_API_KEY; nối chỉ vào child env. Tests precedence/missing/legacy/whitespace/nonpersist pass. |
| R02 P2 — cổng cũ bị coi là app mới | Launcher build EF trước khi kiểm port; app Debug cũ giữ executable hoặc listener bất kỳ được báo already-running dù chưa nhận code/key mới | Từ chối busy port trước native operations/build; không kill/reuse thiếu bằng chứng. Real-launcher fixture với TCP listener chứng minh fail trước build. Lần thử thật5100 cũng từ chối; chuyển Release5102. |
| R03 P2 — cleanup để lại env rỗng | PowerShell chuyển untyped null thành empty string khi gọi SetEnvironmentVariable; biến từng unset trở thành override rỗng | Dùng NullString.Value cho unset thật; restore cả AI/speech/environment trên success/failure. Hai real-launcher fixture regressions từng red, sau sửa green. |

`StartLocalContractTests` cũ match chữ “already listening” và tên hàm restore;
assertions đó lỗi sau thay đổi có chủ đích. Đã bỏ hai phép match implementation;
test giữ các guard SkipWhisper/DatabaseOnly, còn busy port/cleanup chạy qua script
thật trong `test_local_launcher.py`. Không bỏ coverage hành vi để đạt pass.

## Nội dung đã kiểm từ source

- Responses ordered roles, Bearer header, store:false, low, bỏ sampling params;
  parse typed message/output_text, bỏ reasoning; refusal/incomplete/malformed và
  output rỗng không success. Deadline chung send/body/parse/retry, body cap1MiB,
  Retry-After/cancel/error redaction và usage không cộng reasoning hai lần.
- DI thực cùng các consumer Chat/Writing/Vocabulary/Reading/Listening. Giữ Local
  Writing và optional cloud deadline3s, generation quota/replay, atomic failure
  của Listening batch. Review API chat giữ Authorize/antiforgery/identity/NotFound.
- Audio captions-first; learner Speaking vẫn captions/SRT/VTT-only. Shared audio
  fallback bounded source/decode/chunks, FFmpeg pin, Whisper timestamps thật,
  singleton limiter và temp cleanup. Không thay bằng timestamp ước lượng.
- Runtime scan chỉ còn Google endpoint trong guard từ chối legacy; không còn
  client Gemini/text/audio HTTP runtime. Google OAuth giữ đúng chức năng.
- Không sửa persisted model/mapping/migration; EF pending-model check không có
  thay đổi. Các protected runtime configs/DI thuộc diff Plan2 đã được owner cho
  phép từ trước; follow-up này sửa launcher/tests/shared guidance, không đổi model.

## Key và kiểm thử cloud thực tế

1. OPENAI_API_KEY ban đầu được map đúng nhưng OpenAI từ chối; kiểm read-only
   model access xác nhận **HTTP401 invalid_api_key**. Không coi đây là live pass.
2. Owner cung cấp key thay thế và cho dùng local. Nhập bằng helper hiện hành qua
   Read-Host -AsSecureString/PTY (masked), lưu **User Secrets ngoài repo**; không
   đưa vào argv/source/log/Git. Không sửa global OPENAI_API_KEY của máy.
3. Khởi động lại chỉ review-owned Release process PID3244, sau khi xác minh OS
   path và start time; không dừng Debug2748/MariaDB/Whisper/workloads khác.
4. Smoke adapter qua DI thật với key mới: **1 test pass, 2 requests tổng hợp**;
   actual model cả hai **gpt-6-luna**, request effort=low. Text input11/output5/
   total16,2559ms; JSON input24/output15/total39,1522ms. Tổng55tokens, reasoning0.
   Retry tắt; không chạy paid API cho full suite/startup.

Git-visible text scan của cả hai key: **clean**. Completed live log scan: clean.
Không ghi key hoặc fingerprint trong tài liệu này. Key đã xuất hiện trong chat;
owner nên thay key sau đợt test. Khi thay, helper và launcher giữ cùng quy trình.

## Checks của lần review này

Evidence dưới `.tmp/plan2-review/` ignored.

| Check | Kết quả | Evidence |
| --- | --- | --- |
| Release build + provider/consumer/media focused | **74 passed, 0 failed** | focused.log/trx |
| Full application suite, ASR/media explicit opt-in, cloud flags off | **808 passed, 0 failed, 5 skipped /813** | application-full.log/trx |
| Live silence + speech-through-app credit/replay/reload + real decode/ASR | Cả3 test được thực thi/pass trong full suite | application-full.trx |
| Key source/config focused | **12 passed** | key-green.log |
| Real-launcher fixtures: success/failure/busy port | **3 passed** | launcher-green.log |
| Full scripts suite | **107 passed, 0 failed** (202.047s) sau khi cập nhật assertions copy cũ | scripts-final.log |
| Node setup | **9 passed** | node.log |
| Native host hook contracts | **PASS**; không chứng minh IDE tự invoke/trust | hooks.log |
| Agent tooling validator | **pass** sau final suite | tooling-final.log |
| EF pending-model | **No changes have been made to the model since the last migration.** | pending-model.log |
| OpenAI live với key mới | **1 test /2 requests passed** | openai-live-final.log/trx |
| Launcher thật, key source + Whisper + health | **pass** | launcher-final-5102.log |

Năm skip của full suite: hai OpenAI live (không bật cloud trong full suite), ba
MariaDB acceptance. Smoke hai requests ở trên chạy riêng. Bài live từng consumer
với key mới chưa chạy lại; ledger Plan2 lưu kết quả 11requests của phiên triển khai
trước, không dùng thay kết quả phiên này.

## Trạng thái và quy định chạy local

Web đang giữ chạy ở **http://localhost:5102**, Release PID**20392**, start19:57:03
UTC+7 ngày01/11/2026 lúc ghi nhận,
health HTTP200 `{"status":"ready"}`. Source key **User Secrets**, Luna low.
Whisper PID**13092**, tiny.en b5454, http://127.0.0.1:8092, inference Status ready.
MariaDB loopback3307 ready; startup không áp dụng migration mới (already up to
date). Debug PID2748 ở5100 là instance cũ, không dùng để kết luận Plan2 đã load.
PID chỉ là snapshot; xác minh ownership/status hiện tại trước thao tác process.

AGENTS.md và .ai/README.md là quy tắc chung cho Codex/Cursor/Claude Code/
Antigravity: khi owner yêu cầu chạy local, dùng:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Nếu cổng default đang có workload cần giữ, chọn free port/config phù hợp:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5102
```

Lệnh thứ hai chỉ dùng khi5102 trống; bản đang giữ chạy không cần mở thêm.
Agent xác nhận app health, speech Status và nguồn key (metadata), không in key,
không gọi paid API chỉ vì start web. Không start web/Whisper trên folderOpen,
ordinary build/test/MCP/hooks. Startup không hứa key có quota/access.

## Giới hạn còn lại

Authenticated browser journeys toàn bộ cloud features, mic/accent/noise thật,
YouTube network thực, accuracy/load dài, clean-machine full setup, MariaDB race,
staging/recovery/production: **Not run trong phiên review**. Không có schema
change cần migration mới. Không có blocker code còn mở trong phạm vi đã kiểm;
những acceptance này phải giữ đúng nhãn. Xem ledger và runbooks cho phạm vi đầy đủ.

- [Execution ledger](2026-11-01-openai-provider-migration-ledger.md)
- [Follow-up checklist](../superpowers/plans/2026-11-01-openai-local-review-followup.md)
- [Local OpenAI/key/launcher runbook](../operations/openai-local-setup.md)
