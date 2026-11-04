# Cursor Auto — sửa review OpenAI Speech R1–R7

Owner giao prompt này để IMPLEMENT sửa review, không viết lại Plan.
Đọc AGENTS.md, rồi:

    docs/feature-audits/2026-11-03-openai-speech-review.md
    docs/superpowers/plans/2026-11-03-openai-speech-review-repair-plan.md

Giữ original STT Plan làm contract. Một agent, không spawn workers/AI reviewer/
chat/CLI khác. Giữ dirty work trên feature/chau-ui-performance-audit và web cũ
port5112. Không reset/stash/commit/push/PR/remote CI/merge/deploy/Plesk; gatefalse.
Không paid API tự động. Text Luna low, audio gpt-transcribe giữ riêng.

Thực thi tuần tự:

1. R1(P1): regression audio21s có pause100ms ở10s. Normalize padding/overlap
   trước split10s để windows sorted/disjoint và giữ speech samples. Importer
   validate tất cả windows trước provider đầu tiên. Không drop phần tiếng hoặc
   bỏ guard; importer real-planner+fake-provider phải success, invalid planner
   phải fail với0HTTP thay vì đã gọi4lần mới invalid_timestamps.
2. R2(P2): regression synthetic varying-amplitude ở review, sửa threshold không
   vượt mọi frame trong dải đã tái hiện. Giữ silence/noise/quiet/continuous tests;
   không coi tone fixture là nghiệm thu mic thực.
3. R3(P1): shared budget8POST/120s ở HTTP boundary cho mọi live audio path,
   gồm AudioImportLiveAcceptanceTests. Counter private của live-silence không
   bảo vệ importer. Test fake cross-client/import/concurrent/cancel/cap trước
   bất kỳ paid run nào. No retries, không reset budget theo method.
4. R4(P2): explicit-invalid/empty ở shared AI__ApiKey hoặc AI:ApiKey phải
   fail-closed, chỉ missing mới fallback. Giữ defaults không chứa empty ApiKey
   vô ý. Redact credential diagnostics; không sửa/persist key của owner.
5. R5(P2): response stream IOException sau200headers phải map unavailable503,
   không throw500. Giữ caller cancellation/deadline/lease/1HTTP attempt.
6. R6(P2): per-user singleton admission phải evict expired users và bound buckets;
   fake clock10.000users+2h không được giữ10.001entries. Không để eviction active
   bucket bypass10/min; không thêm schema/distributed quota.
7. R7(P2): speech-status phản ánh Enabled/Model/BaseUrl/key precedence env/
   secrets/defaults. Tách key-present nếu cần; metadata-only0network. Sửa active
   UI Admin Create còn chữ Whisper, không rewrite historical audits/DB metadata.
8. Bổ sung positive fake-HTTP app assessment→receipt→commit/replay coverage;
   browser source mới ở free port Debug, fake endpoint/key process-local.
   Không gọi web Release cũ bằng source mới, không bật paid flag để lấy proof.

Probe evidence ở ignored .tmp/stt-review/probes.log và source-linked harness;
chuyển ý nghĩa thành tests chính thức, không commit bin/obj/probe dependencies.
CodeGraph trước indexed source; diagnose/red regression→fix→focused green.
Chạy full .NET/JS/scripts/tooling theo repair Plan sau các fixes, ghi counts đúng.
Reviewer fresh baseline:870pass/0fail/12skip; JS12/12; Python Ran92, skip1.

Update ledger hiện có, bug log/known issues đúng phạm vi. R1–R7 phải có trạng
thái/evidence; live chưa owner opt-in vẫn unverified. Không kết thúc ở TODO
“còn import/budget/status chưa nối”. Không sửa code đồng đội ngoài caller cần thiết.
Handoff bằng tiếng Việt kèm files/tests/risks. Không tự commit/push/merge/deploy.
