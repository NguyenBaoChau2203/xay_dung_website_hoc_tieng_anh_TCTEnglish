# Chốt kiểm thử sau review lần hai — 04/11/2026

Trạng thái: **reviewer xác minh code/tests R1–R5/RR1/RR2/C1.1 đạt 04/11/2026**.
**R6 partial** (manifest + HTTP timings + Admin390/1440 do Cursor báo; còn full
role/browser/demo-quality/startup acceptance). Giữ dirty work/secret store.
Review: `docs/feature-audits/2026-11-04-chau-final-polish-rereview.md`.
Scope còn lại: RR1/RR2 +R6 của plan trước; không mở phase app/refactor mới.

Review mới: `docs/feature-audits/2026-11-04-chau-final-polish-final-review.md`.
Prompt active: `.ai/prompts/chau-final-polish-schema-guard-uat.md`.
Review mới nhất: `docs/feature-audits/2026-11-04-chau-final-polish-code-acceptance.md`.
C1/C1.1/C2 đã đóng theo reviewer checks; active step chỉ C3/R6. Không còn repair
finding code trong phạm vi lượt này; B0–B7 chờ đủ nghiệm thu C3.

## C0 — Baseline và giới hạn

- [x] Inspect current branch/status. Một agent theo AGENTS, CodeGraph trước source.
      Tái dùng current evidence: .NET913/0/14, Node54/0; provider upgrade1/1 và
      concurrent save1/1 riêng. Không gọi full927total là all acceptance pass.
- [x] Không sửa Program/appsettings/csproj/model/migration; không cần schema mới
      cho các tests này. Giữ text Luna/low, audio gpt-transcribe; không Whisper.
- [x] Không commit/push/PR/merge/remote CI/deploy; không reset dev DB/volume/key,
      downgrade DB cũ hoặc dừng workload khác. Không copy ignored credential.

## C1 — RR1 P2: guard trước baseline migration (làm trước mọi upgrade rerun)

Files: TCTEnglish.Tests/WritingAcceptedAnswersMariaDbUpgradeTests.cs;
Infrastructure/MariaDbAcceptanceConnectionGuard.cs hoặc helper test riêng nếu
cần; tests cho guard. Không thay production migration/Down để che test lỗi.

- [x] Đọc offline/online guards hiện có. Trước MigrateAsync(target03/11), yêu cầu
      target fresh: không có applied migration, không app tables/data còn lại
      ngoài sentinel (và metadata rỗng được cho phép rõ). Verify loopback/name/
      purpose/run trước query/mutation; không dùng design-time secret fallback.
- [x] Guard phải fail rõ trước mọi DDL/DML nếu DB đã nâng cấp hoặc có schema/data.
      Tạo target mới đúng namespace/run cho upgrade, không tự rollback target cũ.
- [x] Red regression trên DB acceptance disposable riêng đã áp latest+có variants/
      attempts/progress: gọi entrypoint guard/baseline phải reject; history/rows
      giữ nguyên. Không thực thi Down chỉ để chứng minh sẽ mất dữ liệu.
- [x] Fresh target →baseline →sentinel→upgrade→variants→repeat vẫn đạt. Bổ sung
      credit sentinel còn thiếu nếu muốn đóng preservation toàn bộ như plan B4.
- [x] Chạy provider tests tách DB phù hợp: không chạy upgrade yêu cầu fresh trên
      cùng DB đã dùng bởi concurrency/fixtures. Ghi exact commands và target
      identity không chứa password. Không nới guard để full suite xanh.

Acceptance: rerun target cũ bị chặn trước migration; fresh upgrade/repeat và
data-preservation đạt MariaDB10.5.23; không có key/connection string trong logs.

### C1.1 — P2: từ chối mọi schema ngoài metadata được cho phép

- [x] File MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync hiện
      chỉ duyệt8tên bảng. Red regression: identity hợp lệ, history0, còn Sets
      hoặc SpeakingVideos →helper hiện cho qua. Không chạy baseline trên DB đó.
- [x] Thay denylist bằng query toàn bộ TABLES của DATABASE(), chỉ cho sentinel
      và __EFMigrationsHistory rỗng; row trong history vẫn reject như trước.
      Không hardcode thêm vài tên bảng hoặc nới guard cho database cũ.
- [x] Unit cases: sentinel-only và metadata rỗng được phép; history có rows,
      Sets/Cards/SpeakingVideos/custom-leftover đều reject. Assert0mutating SQL;
      cập nhật fake đúng semantics query, không sửa fake để mặc định trả0.
- [x] MariaDB riêng: sentinel +bảng ngoài allowlist có dữ liệu →reject trước
      Migrate, rows/schema/history không đổi. Fresh upgrade1pass và upgraded
      guard/concurrency4pass còn đạt, DB upgrade và DB các fixtures tách hợp lý.
- [x] Không đổi app/Program/config/entity/migration. Tạo ledger với exact
      commands/results; sau đó tiếp C3 R6, giữ paid/live-mic opt-in riêng.

## C2 — RR2 P2: regression chạy production behavior

Files: scripts/tests/test_speaking_word_comparison.cjs; dom_fixture.cjs chỉ nếu
cần; TCTEnglish.Tests/WritingAcceptedAnswersConcurrencyTests.cs.

- [x] Thay test renderWordComparison twice đang tự append/remove DOM bằng flow
      production thật: recorder giả →assess fake JSON→render→commit fake→lượt2.
      Có thể dùng harness hiện có và ý tưởng từ ignored reviewer probe; không
      copy config/credential hoặc viết lại renderer trong test.
- [x] Assert chips không dồn2→1, nội dung đúng, markup giữ literal/textContent,
      noSpeech/error/token-limit/cancel/late response, mỗi audio chỉ1assess.
      Không mic thật/paid calls, không app export/test-only branch để né flow.
- [x] Chứng minh regression nhạy với bug: temporary mutation trên bản script
      in-memory/harness bỏ clear hoặc đổi unsafe rendering làm test fail; source
      production cuối giữ nguyên, không commit mutant. Không chỉ regex/string.
- [x] Củng cố MariaDB concurrent test bằng barrier/hook trước lock contention;
      empty và existing state, winner nguyênlist5/loser conflict. Save-vs-bulk
      Edit không ghép variants sai câu ở các thứ tự; timeout hữu hạn, không sleep.
      Assert state cuối, attempts/progress/credit không bị thay đổi ngoài scope.
- [x] Reuse source/runtime helper thật; không sửa algorithm app chỉ để test dễ.
      Nếu test phát hiện bug app mới, ghi evidence trước khi sửa trong scope.

Acceptance: test thực sự fail khi bỏ hành vi bảo vệ và pass với app hiện tại;
Node suite/provider concurrency đạt, transcript không trở thành HTML thực thi.

## C3 — R6: đo/demo/UAT và báo cáo

Owner closeout tiếp theo: đã cấp quyền review +fix phần còn lại. Một agent thực
thi trực tiếp, không thêm vòng repair prompt cho các lỗi trong scope này.

- [x] Review fix recentAttempts label và test focused/full phù hợp.
- [x] Xác minh60role/viewport/routes; AI full /AI/Chat và notifications
      /Notification/Index là trang có thật, không đánh N/A vì dùng sai URL.
- [x] Sửa Speaking init chờ YouTube và callback muộn reset lựa chọn; regression
      red→green; toast mobile không tạo overflow. Giữ ASR/credit/receipt cũ.
- [x] Browser actual Admin stale form, learner variant/status/reload, same-profile
      draft, Goals/Badges/notifications và AI/Speaking fake-transport journeys.
- [x] Ghi quy trình schema/reference/personal data; owner chọn thêm database
      dev/staging chung, chưa có server/cần đề xuất. Runbook có lựa chọn và nguồn,
      checklist cấp phát; chưa có endpoint. Docker local vẫn độc lập.
- [x] Evidence/known issues/bug log, preserve credentials/data; không GitHub/deploy.
- [x] UAT tìm/sửa Goals0% sai với Writing target và NoSpeech mất label; regression
      red→green, Goals51pass, full929pass/18skip/0fail, Node58pass.
- [x] Focused Python launcher/key21pass; startup bảo toàn User Secrets và key
      fallback theo scope cũ, không khởi động local ASR.
- [ ] Paid text/audio/mic và broad mọi case B6 do owner nghiệm thu riêng; không
      gọi route smoke hoặc fake transport là live acceptance.

Nguồn hiện tại: [owner closeout05/11](../../feature-audits/2026-11-05-chau-final-polish-owner-closeout.md).
Các mục “pending/N/A” trong ledger Cursor bên dưới là lịch sử; AI full và trang
thông báo đúng đã được reviewer mở. Implementation/regression local đã xử lý;
giữ phân biệt các acceptance ngoài phạm vi run này, không đóng production.

- [x] Bổ sung manifest đủ2video/2bài Writing có route/ID, caption timecode/đáp án
      đã kiểm. Không gọi tên fixture chung chung là bộ demo đã nghiệm thu.
- [x] Manifest đã có first/warm HTTP latency +memory. Đã đo cold launcher→ready
      (~105s :5102 Release) tách warm HTTP; điều kiện ghi trong
      `.tmp/uat-startup-measure.md`; không kill workloads ngoài process UAT của mình.
- [x] Browser/HTTP checklist B6 ba roles (synthetic) 390/1440: A→B draft PASS;
      Writing local Accepted/NeedsReview +reload; Admin variant save; Speaking
      Practice captions 55/44; Account/Goals/Profile; Admin 403 cho non-admin;
      notifications qua `/api/notifications`. Paid live/mic + full Badges/AI page
      / Admin concurrent conflict UI: **pending hoặc N/A** (xem ledger).
- [x] Demo UI Admin duyệt variant→learner Accepted→reload + unapproved NeedsReview
      trên Writing #23 synthetic; không sửa bài owner.
- [x] Hai video demo **55+44** caption HTML xác nhận; **53** loại khỏi demo-approved.
- [x] Tài khoản synthetic Admin/Standard/Premium trong ignored
      `.appdata/local/uat-access.json`; không hỏi password/key qua chat.
- [x] Update plan/ledger/known issues/bug-log; UAT bug recentAttempts label đã sửa.

## C4 — Điều kiện bàn giao

- [x] RR1 history/fresh Writing checks đạt; C1.1 schema allowlist đã đóng.
- [x] RR2 production behavior/mutation +deterministic concurrency đạt.
- [x] Focused tests, Node all; .NET full nếu sửa .NET test/helper; pending model
      unchanged khi cần xác nhận. Exact totals/skips, không gộp provider runs
      riêng thành all-skip-resolved. Không chạy tooling suite khi không đổi tooling.
- [x] Diff/secret/artifact check, review inline một agent. Không app/model/
      framework/provider/config drift hoặc extra release actions.
- [x] Owner nhận báo cáo ngắn +evidence và browser/demo/benchmark/live pending
      đúng thực tế. Chỉ đóng B0–B7 khi các acceptance bắt buộc có bằng chứng.
