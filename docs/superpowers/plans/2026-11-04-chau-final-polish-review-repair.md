# Repair plan sau review Cursor final polish — 04/11/2026

Trạng thái: **R1–R5 đã sửa và verify local 04/11/2026**; **R6 partial**
(fixture/upgrade/concurrency đạt; browser UAT/demo/measurement còn pending).
Không commit/push/PR/merge/remote CI/deploy.

Reviewer lần hai04/11 đã xác minh R1–R5 bằng run mới, MariaDB và production-JS
probes. Còn RR1/RR2 ở kiểm thử +R6 nghiệm thu. Scope còn lại:
`docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`;
evidence: `docs/feature-audits/2026-11-04-chau-final-polish-rereview.md`.

Review nguồn: `docs/feature-audits/2026-11-04-chau-final-polish-review.md`.
Plan gốc B0–B7: `docs/superpowers/plans/2026-11-04-chau-final-polish-before-uat.md`.

## Scope và thứ tự

1. P0 bảo toàn dữ liệu/key/checkout; ghi baseline full suite đang1fail.
2. R1 P1 atomic replacement/concurrency + R2 P1 stale content; thiết kế và sửa
   chung protocol transaction/state, test từng lỗi riêng.
3. R3 P2 Writing cloud-feedback status qua reload.
4. R4 P2 full-suite regression do structure test cũ.
5. R5 P2 Speaking comparison lifecycle + meaningful DOM tests.
6. R6 P2 migration preservation/fixture cleanup, demo/measurement/UAT và report.

Giữ net9.0/EF9.0.4/Pomelo9.0.0/MariaDB10.5.23; text gpt-6-luna/low,
audio gpt-transcribe. Không phục hồi Whisper. Không sửa Program/appsettings/
csproj, applied migration/Designer/snapshot đã tồn tại chỉ để tiện fix. Các sửa
R1–R5 dự kiến không cần schema mới. Nếu thực sự chọn persisted version column,
đánh giá impact và tạo follow-up additive migration +Designer/snapshot/provider
proof trong cùng change set; không sửa migration AddWritingAcceptedAnswers đã
áp dụng ở local. Không xóa/reset dev volume, seed đè hoặc đụng hosting.

## P0 — Chuẩn bị

- [ ] Inspect branch/status và current source bằng CodeGraph. Không reset/stash/
      clean; phân biệt các startup/key changes đã có từ trước.
- [ ] Đọc newest relevant bug log/known issues và R1–R6. Không coi log “fixed”
      là bằng chứng các edge cases này đã xong.
- [ ] Lưu baseline/commands trong execution ledger riêng. Probe ignored là
      evidence, không copy database.local.json hoặc key vào Git.

## R1/R2 — Protocol atomic và state token đúng nội dung

Files chính: Admin WritingExerciseManagementController.cs, management ViewModels,
_AcceptedAnswersPanels.cshtml; helper/service có sẵn phù hợp; new/focused
WritingAcceptedAnswers integration +MariaDB concurrency tests.

- [ ] Viết red tests tái hiện hai request/context, mỗi người submit5đáp án khác
      với cùng state rỗng; hiện result10. Barrier phải bảo đảm hai request cùng
      tới điểm cạnh tranh, không dựa sleep may rủi. Có timeout và cleanup riêng.
- [ ] Red test: GET form Hello/no variants →bulk Edit Goodbye →save stale Hi there.
      Hiện accepted. Test tiếp khi list đã có variants và canonical thay đổi.
- [ ] Thiết kế protocol dùng chung cho save variants và passage Edit: lock parent
      exercise/sentence theo thứ tự nhất quán trước khi đọc state cần ghi, rồi
      load fresh, compare token, validate và replace trong transaction. Có thể
      dùng MySQL row lock tham số hóa hoặc CAS/version tương đương thật sự atomic.
      Không dùng transaction mặc định hoặc Include lần hai như một lock.
- [ ] Không query tracking trước transaction rồi tái dùng entity cache để xác minh
      state. Nếu cần read-only prevalidation, không dùng nó làm state có thẩm quyền.
      Xử lý retry/execution strategy bằng context/state sạch; không retry mù lỗi
      conflict thành ghi đè. Không đổi isolation global hoặc lock mọi bài học.
- [ ] Expected state bind đúng exercise/sentence, canonical/Vietnamese/mapping
      liên quan và variants. UI token và server compare cùng format canonical,
      thứ tự cố định, timestamp precision đúng provider. Token không thay access.
- [ ] Sau lock, mọi validation/stale/SQL lỗi rollback cả list. Contender phải báo
      cần tải lại; không union list/âm thầm overwrite. Giữ tối đa5 và unique hash.
- [ ] Preserve input khi validation/save failure thay vì redirect làm mất các
      câu Admin vừa gõ; lỗi field/form hữu ích. Không đưa raw SQL/secret vào UI.
- [ ] Giữ Admin role/CSRF, exercise+sentence binding và404. Giữ sentence IDs,
      attempts/progress; metadata Edit không bị block vô lý. Bulk text guard
      phải còn đúng khi save variants đang chạy cạnh tranh.
- [ ] Regression: empty/existing list concurrent saves, stale list, stale text,
      save-vs-bulk-edit hai thứ tự, canonical duplicate, duplicate variants,
      max5/length1000, rollback, cross-ID/CSRF/role; no learner answer-list leak.
      Exercise delete/parent removal không để orphan, deadlock hoặc partial save.

Acceptance: MariaDB thực không bao giờ>5; ít nhất một stale request bị từ chối
đúng state, không silently merge/overwrite. Hi there không được auto-approved
cho Goodbye từ form cũ; accepted history và XP không thay đổi.

## R3 — Giữ NeedsReview khi có AI feedback bổ sung

Files: Services/Writing/WritingAssessmentPolicy.cs,
Services/WritingService.cs snapshot/optional-feedback, tests WritingAvailability/
WritingAiEvaluationIntegrationTests/LocalAssessment/status UI.

- [ ] Fake provider trả feedback hợp lệ nhanh trong Local+CloudEnhancement;
      submit câu không khớp đáp án đã duyệt → response NeedsReview; reload Data/
      Razor vẫn NeedsReview. Test hiện tại rơi về NeedsRevision do source
      local+ai-feedback không được ResolveHistoricalStatus nhận diện.
- [ ] Map semantics của nguồn này theo contract hiện tại, hoặc bảo toàn decision
      source và tách optional provenance theo cách tương thích. Không suy luận
      verdict từ SummaryTitle hoặc map mọi ai/legacy false thành NeedsReview.
- [ ] Accepted canonical/variant vẫn accepted; hard-fail vẫn NeedsRevision;
      AI mode hiện có giữ contract riêng. Không paid API, không thay model/mode
      mặc định để test pass. Cả submit/reload không auto-credit NeedsReview.

Acceptance: server response, persisted snapshot, Razor và JS nhất quán ba trạng
thái có/không cloud; retry/AI failure không mất lịch sử hoặc nhân credit.

## R4 — Sửa test gate thất bại

File: TCTEnglish.Tests/WritingAdminControllerStructureTests.cs33–37.

- [ ] Reproduce targeted test; thay cách slice end marker phụ thuộc tên method cũ
      bằng kiểm tra ổn định hơn. Vẫn giữ invariant manual transaction chạy bên
      trong execution strategy; không skip/xóa assertion hoặc đổi app tên method
      về cũ chỉ để chiều string test.
- [ ] Targeted green rồi chạy full .NET sau toàn bộ code changes cuối cùng.
      Baseline review908pass/1fail/13skip, không lấy focused green thay full suite.

## R5 — Comparison thuộc đúng lượt ghi âm

Files: wwwroot/js/speaking.js, scripts/tests/test_speaking_word_comparison.cjs,
test_speaking_behavior.cjs/dom_fixture.cjs nếu cần thêm DOM capability có kiểm thử.

- [ ] Red DOM behavior tests chạy production speaking.js: có comparison cũ →
      nhấn Record lần mới →panel cũ phải ẩn/xóa trước khi xin mic; mic denied,
      cancel/error/new sentence/late response đều không khôi phục kết quả cũ.
- [ ] Clear khi khởi tạo epoch mới, không clear sai vào nút Stop của lượt đang
      ghi hoặc bỏ epoch/sentence binding. Reset error paths an toàn.
- [ ] Test thực sự inspect DOM/panel/request count; regex presence không đủ.
      Fixture hiện thiếu firstChild/removeChild browser semantics: bổ sung tối
      thiểu đúng hoặc dùng browser harness sẵn có; test clear và render2lần không
      cộng dồn. Không sửa fixture để mock tự clear thay production.
- [ ] Assert DOM encoding/markup transcript, token-limit, noSpeech/error và
      không có extra ASR request; score/receipt/replay/credit giữ nguyên.

## R6 — Acceptance và báo cáo đúng bằng chứng

- [ ] MariaDbAcceptanceFactory.AcceptanceTables thêm WritingAcceptedAnswers trước
      sentence/users parents khi reset fixture; test rerun hai fixtures không
      orphan hoặc answer contamination. Không tắt FK global để né lỗi.
- [ ] Upgrade test phải dùng DB mới, loopback/name/sentinel/run verified và guard
      lịch sử rỗng trước baseline migration; không downgrade DB acceptance cũ
      có data của run khác/previous probe. Không dùng dev connection fallback.
- [ ] Seed sentinel attempts/sentence progress/exercise progress/credits theo
      model thật dưới schema cũ, rồi upgrade giữ IDs/values/relations; thêm variants
      rồi migrate repeat và assert giữ tất cả. Kiểm FK/index/approver deletion,
      unique/hash/rollback/concurrency đúng MariaDB10.5.23. SQLite không thay thế.
- [ ] Chọn đủ2video/2bài Writing thật sự mở được, có IDs/route/caption timecodes/
      canonical+approved+NeedsReview cases trong ignored manifest không chứa key.
      Không tự seed đè dev data; dùng namespace synthetic riêng khi cần.
- [ ] Ghi cold/warm startup/memory/timings với điều kiện đo rõ, không tự dừng
      workload khác. Không suy diễn snapshot RAM thành benchmark/root cause.
- [ ] Browser three-role UAT390/1440: same-profile A→B draft, local/optional-AI
      status reload, Speaking diff lifecycle, Admin CRUD/variants/conflict,
      Account/Goals/Badges/AI/notifications negative/replay theo B6 gốc.
      Dùng fake provider cho failure/noSpeech; không dùng mic thật/paid calls
      nếu chưa owner opt-in riêng. Chưa kiểm được phải pending, không tick done.
- [ ] Cập nhật plan gốc/audit/ledger/known issues theo implemented vs automated vs
      browser/live pending. Bug-fix-log chỉ ghi fix khi đã chứng minh green.

## Checklist đóng repair

- [x] R1/R2 red→green trên MariaDB và tests chống regression đã được track.
- [x] R3 HTTP/snapshot/reload và R5 production-DOM tests đạt.
- [x] R4 full .NET 0 fail (922 pass / 6 skip); Node speaking/writing 16/16.
- [x] Schema upgrade/repeat/preservation trên MariaDB acceptance `chau1104r:3331`;
      không sửa applied migration/history.
- [ ] R6 browser 3-role UAT còn pending (Premium/Standard/live); demo manifest +
      cold-warm + Admin 390/1440 Writing partial đạt 04/11;
      fixture + upgrade + concurrency đã có evidence.
- [x] Diff/secret/artifact: không commit key/backups; không đụng production.
- [x] Báo cáo tiếng Việt cho owner; chưa commit/push/PR/merge/remote CI/deploy.
