# Phần việc Châu: tiến độ và gói cải tiến cuối — 04/11/2026

Một agent rà soát theo README phân công, Git/source hiện tại và các ledger có
chứng cứ. Base master8203e30 (PR124 đã MERGED); workspace ở
fix/local-launcher-preserve-user-secrets, còn dirty local fix/startup/uninstall.
Đây là review/kế hoạch, chưa triển khai hai finding Writing mới, không commit,
push, migration, paid API, dừng workload hoặc bật deployment.

Cập nhật phạm vi 04/11: owner đã chọn cả hai tính năng mới ở mục6 để Cursor
Auto thực thi cùng các cải tiến được đề xuất. Plan/prompt đã mở rộng B0–B7;
đây là cập nhật tài liệu, không phải bằng chứng hai tính năng đã được lập trình.

## 1. Phạm vi Châu ban đầu

README56–86 giao Account/auth/authorization, SMTP, Speaking, Writing,
Goals/Badges, AI Chatbot, Admin Dashboard/users/content/AI và kiến trúc/hạ tầng.
Đó là phân công module, không phải chứng cứ Châu tự viết mọi dòng trong module.
Notifications, shared layout và các integration Reading/Listening có phần của
đồng đội; ghi nhận sửa tích hợp, không đổi quyền sở hữu công việc.

| Phần ban đầu | Đã có trước đợt cải tiến | Kết quả gần đây | Trạng thái |
| --- | --- | --- | --- |
| Account/phân quyền | Cookie/OAuth, profile/settings, đổi/reset password, Admin user management | Guard social/malformed hash, validate, reset consume/race,403 page, fixes accessibility | Core/UI đã master; SMTP/OAuth thật cần nghiệm thu môi trường riêng |
| SMTP/lock | Mail reset/block/unlock, worker | Deadline/cancel, encode reason, commit state trước SMTP, conditional unlock và batch isolation | Code/test đã master; auto-unlock default-off có chủ ý |
| Speaking | YouTube/captions, shadowing/dictation, Web Speech, progress | Bỏ random/demo scores; WAV→server assessment/receipt; giữ access/dedup; cloud OpenAI thay Whisper; bounded windows và paging | Đã master; mic/accent/noise thật/quality chưa đóng |
| Writing | Workspace/gợi ý/chấm/lưu bài, từng phụ thuộc Gemini | Local exact/NeedsReview, feedback AI tùy chọn có budget, save/progress/replay an toàn, SQL filters/paging/owned content | Đã master;2 finding UX/privacy mới bên dưới chưa sửa |
| Goals/Badges | Goal/streak/XP/badges | Atomic completion credit +outbox/dedup, race/lease/batch; Profile chỉ đọc badge đã trao | Core đã master; source bật đủ5 learning areas |
| AI Chat | Tư vấn tiếng Anh, context/history | OpenAI Luna/low, durable quota ledger/recovery, context budget, paging, cancel, tránh retry sau unknown outcome | Đã master; happy-path cloud tiny request từng đạt, chưa full live journeys |
| Admin/UI | Dashboard, user/content/AI management | Labels/focus/mobile/reduced-motion, real filters/paging, safer dynamic cards, pinned/sanitized browser assets | Đã master; cần full interaction/role/browser matrix cuối |
| Local/CI/CD/team | Hosting/DB và thao tác local/deploy cũ | Docker MariaDB10.5.23, team sample bootstrap,4IDE rules/hooks/CodeGraph, CI +gated release | CI master success; production release skipped/gatefalse; enabled deploy/recovery drill chưa chứng minh |

## 2. Tiến trình 29/10–04/11

-29–31/10: learner reliability, chấm/lưu Speaking/Writing thật, credit/transaction/
  outbox/import-job/lease/cancel; MariaDB acceptance và chuẩn hóa4AI clients.
-01/11: Docker/team local +OpenAI text migration; Whisper local là giải pháp
  trung gian đã bị thay thế, không phải việc còn phải khôi phục.
-02–03/11: Plan1 UI và Plan2 logic/performance, nhiều vòng review/repair;
  Account/AI history/quota, SQL catalogs, SMTP, assets/profile/lazy launcher.
-04/11: OpenAI Speech hoàn thiện offline/fake pipeline, bỏ engine Whisper,
  vendor sanitizer security update,5 commits +PR124 merge; sau đó sửa lỗi
  startup ghi User Secrets/EFS ở local và khôi phục cấu hình key thành công.
-Whisper native43files/104.7MiB đã owner xóa, unused image~1.55GiB đã gỡ.
  Recording/logs và2metadata nhỏ còn được ghi rõ trong uninstall audit.

## 3. Chứng cứ và ranh giới

GitHub đọc mới: PR124 MERGED, merge8203e30; master run41795999777 success,
release jobskipped, PLESK_DEPLOYMENT_ENABLED=false. Không chạy CI mới trong review.

| Bằng chứng | Kết quả / độ mới |
| --- | --- |
| Full .NET ở merge review04/11 |890pass/0fail/12skip; không chạy lại trong lượt planning này |
| MariaDB upgrade/repeat |1pass, và6 concurrency/worker pass trong merge review |
| JavaScript toàn bộ |44pass trong merge review |
| Python sau local startup fix |Ran105,104pass/1skip;7launcher +14key/config focused pass |
| User Secrets sau owner nhập lại |Cấu hình text/speech nhận từ store; length/mtime/protection không đổi qua startup |
| Cloud key check trước incident/re-entry |GET hai model200, tiny text200/completed/OK; không coi đó là live ASR mới sau restart |
| Current local probes |/health/ready200; speech configured User Secrets AI:ApiKey; VerifiedLive=false |
| Current resource snapshot |RAM15.69GiB, available1.05GiB;43running Docker containers; TCT web working set~190MiB, MariaDB~108.7MiB |

Resource snapshot chỉ một thời điểm, không phải CPU/RAM peak hay quan hệ nhân
quả. Chưa cold/warm benchmark kiểm soát workload; không tự dừng các stack khác.
Một browser/layout pass trước đó và tests xanh không phải nghiệm thu mọi thao tác,
mọi accent/mic, provider quota, clean teammate PC hay hosting FFmpeg/rollback.

## 4. Findings mới cần sửa trước đợt test tổng

### F1 — P1: draft Writing chưa có owner namespace

Source: writing.js82–109, WritingPractice.cshtml payload; AccountController187–204.
Key hiện tại wp_draft_ex{exerciseId}_{sentenceId}; save/load không dùng user ID,
logout chỉ sign-out cookie. A lưu nháp chưa submit →logout →B mở cùng bài/câu
trên cùng origin/profile có thể nhận draft A. Không cần DB/AI để xảy ra.

Probe chạy chính helper của production script trong2VM contexts dùng cùng storage,
exercise42/sentence7 nhưng userA/B: crossAccountDraftVisible=true. Đây là bằng
chứng helper/source; chưa chạy browser đăng nhập A→B hoặc dùng draft của owner.
Evidence ignored: .tmp/progress-review/source-probes.json.

Sửa: owner ID từ server/helper đã xác thực, namespace mới theo owner/bài/câu,
không tự nhập draft legacy không chứng minh owner, xử lý storage unavailable.
User namespace chỉ ngăn nhầm lẫn UI; không phải security boundary của browser
storage hoặc biện pháp chống XSS. Chưa thêm server draft schema trong gói tối thiểu.
[MDN localStorage](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage),
[OWASP local storage](https://cheatsheetseries.owasp.org/cheatsheets/HTML5_Security_Cheat_Sheet.html#local-storage).

### F2 — P2: NeedsReview bị hiển thị như NeedsRevision

Backend WritingAssessmentPolicy trả NeedsReview/ReviewRequired=true khi đáp án
khác tham chiếu; không khẳng định sai. Response ViewModel có fields riêng, nhưng
writing.js161–180 normalize bỏ fields này và219–226 dựa passed=false →“Làm lại”.
Razor initial lastEvaluation payload cũng không chuyển trạng thái review riêng.
Probe current normalize/label: reviewRequired không còn, assessmentStatusnull,
label“Làm lại”. Chưa thực hiện browser submit/reload trong review này.

Sửa mapping response→client→saved snapshot→Razor cho đủ Accepted/NeedsRevision/
NeedsReview; giữ passed=false/CanAutoAdvance=false/không XP cho NeedsReview.
Không biến AI feedback thành quyết định pass hoặc thêm DB column vì tiện.

### F3 — P2 tài liệu và nghiệm thu

README còn mô tả Admin/Teacher/Student trong khi Roles ở Models/User.cs là
Admin/Premium/Standard; Student chỉ legacy→Standard. Một số backlog tháng3
còn nói SQL Server, secrets trong config, full startup seeding, Goals2areas
deferred và chưa có AI/paging — không dùng làm TODO mới khi source đã thay đổi.
Đối với lịch sử credential, source defaults đã sạch không chứng minh lịch sử Git
hoặc toàn bộ credential cũ đã được rotate; giữ việc xác minh đó trước release.

## 5. Gói chốt được đề xuất

1. Đóng local startup/key/uninstall change set đã làm; giữ production off.
2. Sửa F1/F2, đồng bộ F3; regression/role-isolation trước browser tổng.
3. Thêm Speaking đối chiếu từng từ và Writing đáp án Admin duyệt theo mục6;
   migration bổ sung của Writing phải đi cùng code và kiểm chứng MariaDB.
4. Chọn bộ học liệu demo hiện có và kiểm captions/timecode/đáp án, không reset/
   seed đè dữ liệu. Fixture bootstrap phục vụ dev không mặc nhiên là bài demo chuẩn.
5. Đo startup cold/warm khi kiểm soát workload; ghi số thật, không hứa tốc độ từ
   việc bỏ Whisper. Chuẩn bị checklist3roles/desktop/mobile/loading/error/reload.
6. Một đợt UAT tổng thể, ghi P0/P1/P2/P3 và sửa từng lô; chạy smoke sau mỗi sửa.
   Giới hạn trả phí/mic chỉ mở khi owner cho phép riêng.

Điều kiện đóng: không còn P0/P1 trong phạm vi; F1/F2 regression +browser đạt,
hai tính năng mới và migration Writing đã kiểm chứng,3roles đã thử luồng chính/
negative cases, lưu/reload/dedup đúng, bộ demo hợp lệ,
key không đổi và không leaked; CI/release chỉ thao tác khi owner cấp giai đoạn.
Không gán phần trăm hoàn thành tùy ý hoặc gọi local closeout là production-ready.

## 6. Hai tính năng mới đã được owner chọn, chờ triển khai

- Speaking đối chiếu transcript với câu mẫu theo từ khớp/thiếu/thừa/khác để
  feedback cụ thể hơn. B3 dùng full transcript của lần đánh giá hiện có, không
  gọi thêm provider, không chấm phoneme/fluency hoặc đổi điểm/receipt/credit.
  Không lưu alignment schema mới hoặc thêm model/provider/Whisper.
- Writing nhiều đáp án biến thể do Admin duyệt: policy hiện chỉ có một reference
  normalized-exact. B4 bổ sung bảng con/migration và Admin authoring theo ID câu,
  tích hợp server grading, giữ NeedsReview cho câu chưa khớp đáp án được duyệt.
  Tối đa5 variants/câu, không sửa canonical/attempts/progress cũ hoặc nhân XP;
  phải kiểm disposable MariaDB upgrade/repeat/data preservation.

Hai mục trên đã được thêm vào execution prompt và điều kiện đóng, không còn là
phần tùy chọn. Source pitfall của B4 đã được plan xử lý: Admin bulk validation
tái tạo Sentences từ FullText và sync theo vị trí; variant authoring dùng form
riêng trên câu đã lưu, có guard tránh gắn sang câu khác khi sửa passage.

Sau UAT mới cân nhắc account-service extraction, broader index/load testing và
enabled staging backup/restore drill. Không gộp rewrite controller/framework,
AI provider hoặc tính năng ngoài phân công vào vòng hoàn thiện demo.

Plan: docs/superpowers/plans/2026-11-04-chau-final-polish-before-uat.md.
Prompt: .ai/prompts/chau-final-polish-before-uat.md.
