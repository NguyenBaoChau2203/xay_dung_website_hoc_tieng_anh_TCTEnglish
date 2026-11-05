# Cursor Auto — tiếp R6/UAT, code findings đã đóng

> Superseded05/11/2026 cho phần đã hoàn tất: đọc
> `docs/feature-audits/2026-11-05-chau-final-polish-owner-closeout.md` trước.
> Reviewer đã sửa Speaking init/NoSpeech/Goals summary và kiểm browser main
> journeys, đầy đủ run929/0/18skip +Node58. Không chạy lại các bước tạo account/
> fixture hoặc sửa code đã đạt chỉ vì dùng prompt lịch sử này. Chỉ tiếp các case
> owner UAT chưa có evidence hoặc lỗi mới owner giao. Paid/mic/staging/deploy
> vẫn cần scope tương ứng; đây không phải lệnh tự chạy API hay deploy.

Khi owner giao thực thi, đọc review mới:
`docs/feature-audits/2026-11-04-chau-final-polish-code-acceptance.md`.
Chỉ làm C3/R6 tại
`docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md`.

R1–R5/RR1/RR2/C1.1 đã đạt reviewer checks. Guard kiểm toàn schema, unit8cases,
fresh upgrade1 +leftover1 +regressions4 trên MariaDB đã đạt. Full .NET reviewer
921/0/18. Không triển khai lại features/guard hoặc mở repair phase khi chưa có
bug mới; tiếp nghiệm thu R6, phân biệt implemented và accepted trên browser.

Chuẩn bị synthetic accounts riêng cho Admin/Premium/Standard theo quyền test
owner đã cấp; không đổi role người dùng thật hoặc hỏi gửi mật khẩu/key vào chat.
Credentials để ignored; không seed/reset DB. Đọc DESIGN/UI skills trước UI review.

R6: browser3roles390/1440 theo B6 gốc, same-profile A→B drafts, local/optional-AI
status reload, Admin variants/conflict, Speaking lifecycle và core Account/
Goals/Badges/AI/notifications/role/error/dedup. Dùng fake provider cho automation.
Tạo demo synthetic có Admin approve→learner variant Accepted→reload và unapproved
NeedsReview; manifest hiện variants0. Kiểm video53 captions overlap/đoạn dài,
chọn2video/2Writing có IDs/routes và nội dung thật sự dùng được.

Đo startup launcher→ready riêng với first/warm HTTP request latency; số HTTP
331/891ms đã ghi chưa phải full startup duration. Ghi điều kiện/memory, không
kill workloads. Chưa kiểm được thì pending, không đánh dấu B0–B7 hoàn thành.

Một agent theo AGENTS, CodeGraph trước source; giữ dirty work/dữ liệu/key/native
trust. Scope là UAT/demo/measurement; không sửa Program/appsettings/csproj,
entities/migrations hoặc app protocol đã đúng. Không reset dev DB/volume, không
dừng workloads. Local launch/key theo AGENTS; không ghi lại User Secrets.
Giữ Luna/low +gpt-transcribe, không Whisper; fake provider trong tests,
paid API/mic thật vẫn cần owner opt-in.

Nếu UAT phát hiện bug, ghi repro/expected/actual/files và regression; sửa theo
scope owner giao, không refactor lan rộng hoặc sửa applied migrations. Chạy
checks phù hợp thay đổi; không lặp full suite chỉ vì vừa mở trang. Review diff,
secret/artifact check, update ledger/known issues và bug-log sau fix. Chưa
commit/push/PR/merge/remote CI/deploy.
Báo tiếng Việt phần đã đạt và phần UAT/demo/measurement/live còn pending.
