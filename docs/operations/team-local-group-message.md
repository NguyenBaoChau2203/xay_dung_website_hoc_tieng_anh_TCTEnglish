# Đoạn chat Châu gửi nhóm sau merge

Mọi người pull `master` mới để dùng bộ local Docker + Whisper + OpenAI nhé.

Máy Windows x64 cần có: Git, Node.js22+ kèm npm, PowerShell7, .NET SDK9+/
ASP.NET runtime phù hợp, Docker Desktop chạy Linux containers/WSL2, Visual C++
Redistributable x64. **Không cần SSMS, SQL Server hay cài MariaDB riêng.**

Trong thư mục gốc dự án:

1. Chạy `node scripts/check-local.cjs` để biết thiếu mục nào và link cài.
2. Chạy `SETUP-AGENTS.cmd` một lần, mở lại IDE/chat và xác nhận trust/MCP khi hỏi.
3. Key test Châu gửi riêng: chạy `pwsh -NoProfile -File scripts/set-local-ai-key.ps1`
   để nhập key có che ký tự. Nhờ AI Agent mở helper này cũng được. Không đưa key
   vào source/GitHub hoặc nhờ agent in file secrets.
4. Chạy `SETUP-LOCAL.cmd`, hoặc nhờ AI Agent: “Đọc AGENTS.md và chạy web local bằng
   scripts/start-local.ps1 -NoBrowser, xác nhận MariaDB, Whisper, web và nguồn key;
   giữ dữ liệu hiện có, chỉ làm local.”

Lần đầu script tự tải dependency đã pin, tạo DB Docker, chạy migration và dữ liệu
mẫu. Tài khoản admin/student/premium có mật khẩu ngẫu nhiên trong file riêng
`.appdata/local/access.json` trên máy. Lần sau giữ dữ liệu/key, không nhập lại.
Website dùng GPT-6 Luna low cho văn bản, Whisper cho audio; model trong Cursor/
Codex/Claude Code/Antigravity của mỗi bạn là lựa chọn độc lập.

Mỗi máy có dữ liệu riêng. Nếu cần đúng dữ liệu Châu đang dùng, nhận backup riêng
và import lúc DB còn trống; không push SQL/backup/volume/key/uploads lên Git.
Chi tiết và link cài nằm ở `docs/operations/team-local-onboarding.md`.

Làm feature trên nhánh riêng, tạo PR vào master. CI build/test vẫn chạy khi có
PR/push, **deploy production đang OFF**; chỉ Châu bật sau khi test master ổn định.
