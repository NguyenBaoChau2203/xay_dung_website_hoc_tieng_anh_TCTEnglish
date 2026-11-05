# Cursor Auto — gói chốt trước UAT và hai tính năng mới của Châu

Khi owner gửi prompt này để thực thi, triển khai local toàn bộ B0–B7 trong plan:
`docs/superpowers/plans/2026-11-04-chau-final-polish-before-uat.md`.
Audit nguồn: `docs/feature-audits/2026-11-04-chau-progress-and-final-polish.md`.

Owner đã chọn cả hai tính năng Speaking đối chiếu từng từ và Writing nhiều đáp
án do Admin duyệt vào phạm vi bắt buộc ngày 04/11/2026. Triển khai và kiểm chứng,
không chỉ lập lại plan hoặc dừng sau hai finding Writing. Một agent duy nhất
theo AGENTS.md; không subagent, AI reviewer riêng hoặc gọi model/CLI khác để giao việc.

## Phạm vi và thứ tự bắt buộc

1. **B0 — Bảo toàn change set có sẵn.** Inspect branch/status/diff thực tế;
   workspace đang có dirty local startup/key fixes và tài liệu uninstall/audit.
   Không reset/stash/clean hoặc ghi đè để làm sạch. Startup chỉ đọc User Secrets;
   DB/worker flags, TEMP/TMP và fallback key chỉ vào child environment, có restore
   khi thành công/lỗi. Explicit key update giữ backup/recovery khi replace lỗi.
   Không thử ghi lại key thật, chuyển secret store hoặc hạ EFS/ACL để chạy test.
2. **B1 — Nháp Writing theo tài khoản.** CodeGraph trace practice controller →
   service →ViewModel →Razor →production JS. Red regression cho A→B cùng storage
   đọc nhầm nháp. Đưa server-verified current-user ID qua ViewModel; namespace
   mới owner/exercise/sentence. Ignore legacy unowned drafts, không nhận nháp cũ
   về user đang login/không xóa draft thật. Storage unavailable và thiếu owner
   phải graceful. Client namespace không thay cho server access control.
3. **B2 — Ba trạng thái Writing.** Trace AssessmentStatus/AssessmentSource/
   ReviewRequired qua response, saved snapshot, Razor, JS normalize và UI.
   NeedsReview hiển thị “Cần xem lại”, không gán tất cả passed=false là “Làm lại”.
   Accepted/NeedsRevision/NeedsReview phải đúng cả submit/reload; NeedsReview
   không auto-next/XP. Giữ accepted history, dedup và optional-feedback contract.
4. **B3 — Speaking đối chiếu từng từ.** Pure deterministic alignment dùng
   authorized reference và full transcript đã có, trước khi cắt preview; không
   gọi ASR lần hai. Trả matched/missing/extra/substituted và availability bằng
   DTO/JSON bổ sung tương thích. Bound256tokens mỗi phía; quá giới hạn báo chưa
   có comparison, không giả vờ đã so toàn câu. UI chú giải rõ, textContent/encode,
   không chỉ dùng màu, mobile/keyboard. Gọi là đối chiếu nội dung nhận diện,
   không tuyên bố chấm phoneme/fluency. Giữ điểm text-match/ngưỡng/receipt/credit.
   Clear stale results khi đổi câu/hủy/lượt mới; NoSpeech/error không có diff giả;
   reload không tự gọi API. Không thêm bảng lưu alignment ở v1.
5. **B4 — Writing nhiều đáp án Admin duyệt.** Thực hiện đủ B4.1–B4.4 của plan:
   - Bảng con cho từng sentence, max5 variants/câu, max1000ký tự/đáp án, normalized
     SHA-256 hash/index tương thích MariaDB, approver ID/time do server ghi.
     Canonical EnglishMeaning vẫn giữ nguyên và được chấp nhận; zero variants
     tương thích dữ liệu cũ. Không overwrite/backfill users/attempts/progress.
   - V1 thêm partial/form riêng trên Admin Edit, POST riêng Admin-only +CSRF,
     exercise/sentence/variant IDs được query ràng buộc; sai quan hệ trả404.
     Không nested forms. Không nhét list vào bulk FullText parsing rồi để mất.
   - Guard bulk passage Edit theo B4.2: không gắn variants sang câu mới khi câu
     bị đổi/tách/ghép/xóa; giữ sentence IDs và attempts. Transaction/stale checks
     bảo vệ Admin concurrent save và cap5, lỗi phải rollback toàn list.
   - Server grading canonical local-exact hoặc local-approved-variant →Accepted;
     chưa khớp →NeedsReview, hard-invalid →NeedsRevision. Trace out-of-scope guard
     trước Assess để không từ chối approved-current hoặc nhận variant câu khác.
     Không leak đáp án tham chiếu/list ra learner initial JSON để chấm client.
     AI không tự duyệt; thêm/xóa variant không chấm lại lịch sử/nhân XP.
   - tct-ef-migration bắt buộc: migration +Designer +snapshot, review Up/Down,
     pending-model check và disposable MariaDB10.5.23 upgrade/repeat/preservation.
     Provider-only concurrency/unique/FK/index phải có evidence; SQLite-only
     hoặc skip MariaDB không đủ để gọi B4 hoàn thành.
6. **B5 — Docs, demo và số đo.** Đồng bộ current roles Admin/Premium/Standard,
   MariaDB/Pomelo, runtime OpenAI; giữ lịch sử và phân công. Ít nhất2Speaking
   videos/2Writing exercises chất lượng có case hai tính năng mới. Không seed
   đè DB hoặc tự dừng app/container dự án khác. Ghi cold/warm/memory/timings thật,
   điều kiện đo và limitations, không hứa phần trăm tăng tốc chưa đo.
7. **B6 — UAT tổng thể.** Smoke/regression ngay từng lô trước UAT cuối. Ba roles,
   390px/1440px, Account/Profile/Settings, Speaking/Writing, Goals/Badges/
   notifications, AI full/embed, Admin; luồng chính/error/reload/cancel/access/
   dedup và cả hai tính năng mới. F1 phải A→B cùng browser storage context,
   không dùng hai profile tách rồi gọi isolation pass. Synthetic accounts riêng.
8. **B7 — Review và kết thúc.** Review diff, meaningful production-code regressions,
   build/relevant .NET/JS/Python checks, migration/provider evidence, browser
   evidence, secret/artifact scan. Append bug-fix log/known issues đúng finding.
   Báo cáo tiếng Việt: trước/sau, files, exact commands/results, remaining skips/
   blockers; đánh dấu hai tính năng mới riêng. Không gọi skipped/live-unrun là pass.

## Ràng buộc thực thi

- Load từng active step/skill: tct-superpowers +tct-engineering, domain/security,
  tct-api-endpoint khi sửa JSON, tct-ef-migration cho B4. CodeGraph trước source
  indexed, không auto init/rebuild/sync. Đọc DESIGN.md và UI skills trước UI edits.
- B4 đã cho phép entity/DbContext/additive migration đúng tính năng. Không xin
  lại quyền cho bước local trong scope; không tự mở schema/refactor khác.
  Không sửa Program.cs/appsettings/.csproj, applied migrations/history hoặc
  nâng framework/EF/Pomelo. New helper có thể pure/đi qua service hiện có.
- Web text `gpt-6-luna` / reasoning `low`, audio `gpt-transcribe`; model Cursor
  Auto thực thi độc lập cấu hình web. Không restore Whisper/local ASR/Colab.
- Local startup: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
  Nếu port bận, giữ process hiện có và dùng port trống theo AGENTS.md, không
  kill app chỉ vì đang chiếm port. Check /health/ready và scripts/speech-status.ps1;
  configured/key-source metadata không chứng minh account/quota hoặc live ASR.
- Không in key, đưa key vào argv/log/Git hoặc copy secret/DB backup/runtime upload.
  Không reset dev DB/volume, chạy destructive Down trên DB thật hoặc đụng hosting.
- Fake provider cho401/429/timeout/outage, count requests để chứng minh B3 không
  gọi thêm. Không tự gọi paid API/audio hoặc mic owner; cần owner opt-in riêng.
  Mọi hook/startup vẫn không gọi paid API.
- Không tự commit/push/PR/merge, chạy remote CI hoặc đổi Plesk/deployment settings.
  Giữ production deploy off; chủ dự án review local và cấp quyền giai đoạn sau.
- Ngoài scope: Teacher role/review queue, account/controller rewrite, đổi model/
  provider/framework, broad load testing và features mới của đồng đội. B3/B4
  không thuộc danh sách ngoài scope: phải triển khai để đóng gói công việc này.
