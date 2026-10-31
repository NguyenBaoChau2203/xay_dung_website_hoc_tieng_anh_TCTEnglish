# Review sau SA01–SA03 và xác minh release — 31/10/2026

Baseline `69c6815..df251e6`, branch `feature/bao-chau-ui-audit`.
HEAD `df251e642e43a30a8f784bc2cefaa2d7214a9449`.
Một agent review/verify trực tiếp. Chủ dự án yêu cầu ổn định local trước;
không có quyền push/dispatch CI/merge/deploy trong lượt này.

## Kết luận và trạng thái hosting

Ba finding SA01–SA03 đã được sửa và các probe cũ đều pass. Còn một finding P2
trong bootstrap local (LB01 bên dưới) và các gate nghiệm thu chưa khép. Chưa đề
nghị release production.

| Kiểm tra read-only | Kết quả đã xác minh |
| --- | --- |
| Latest GitHub Actions | Run41156950080, event workflow_dispatch, feature/bao-chau-ui-audit, SHA f03aba0 |
| Build/tests job124405732767 | Success: 728 pass, 1 MariaDB acceptance skip, total729 |
| Release/Plesk job124406653257 | Skipped, không thực thi webhook/deploy |
| GitHub variable | PLESK_DEPLOYMENT_ENABLED=false |
| Remote master | b1e56a998480af495f1cec27212d8be0dd7ea04a, chưa chứa feature mới |
| Release branch | codex/production-release tại9c48c426dcb94db4ebabecec2060b7fe6fea4383 |
| Public release manifest | release.json vẫn enabled=false |
| Public website | GET HTTPS tctenglish.site theo redirect: HTTP200, curl exit0 |

Run: [GitHub Actions41156950080](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/41156950080).
Runtime/tests/scripts/workflow diff giữa CI SHA `f03aba0` và HEAD `df251e6` bằng0;
HEAD chỉ đóng dấu ledger. Các kết quả này chứng minh **run đó không deploy hoặc
chạy migration hosting**. HTTP200 chỉ chứng minh trang có phản hồi khi kiểm tra,
không chứng minh toàn bộ chức năng hay schema production đã được nghiệm thu.
Không đăng nhập lại/đổi cấu hình Plesk hoặc query database hosting trong review.

Workflow `.github/workflows/deploy.yml` tách hai job. Build dùng MariaDB tạm trên
GitHub runner và connection loopback33307. `dotnet publish`, EF bundle/SQL và
upload artifact chỉ chuẩn bị package. Release cần build thành công, ref master
và gate true; production environment cũng được khai báo nhưng không giả định
đã cấu hình required reviewers. Feature push không được Plesk triển khai theo
nhánh release hiện dùng.

Lý do Cursor push/chạy CI: prompt copy trước có câu giao "commit theo nhóm,
push feature và chạy CI đúng SHA". Đó là quyền trong prompt đã soạn, không phải
release job tự vượt gate. Chỉ dẫn mới của owner thay quyền đó: local trước, remote
theo từng bước được yêu cầu rõ. AGENTS.md, Cursor always-applied rule và active
prompt đã được sửa local; giữ nguyên workflow/gate/Plesk.

## Các lỗi cũ đã sửa

| Finding | Source/test evidence | Giới hạn |
| --- | --- | --- |
| SA01 ambient transaction retry | CompletionCreditService bỏ retry khi caller sở hữu transaction; Goals không bắt1213/1205 để increment tiếp; test propagation/cancel/owned retry xanh | Chọn propagate/fail an toàn; chưa phải whole-unit HTTP retry đã nghiệm thu MariaDB |
| SA02 overwrite sentinel | Script kiểm tct.run/port, read identity DB cũ, bỏ DROP/UPSERT, kiểm native exit; 6 script regressions pass | Còn LB01 về encode SQL literal |
| SA03 blank audio | Marker-only thành NoSpeech, mixed giữ Hello; adapter/assessment regressions pass | Full real-audio app receipt/progress/credit vẫn chưa hoàn tất |

Probe cũ trong `.tmp/sentinel-rereview/AmbientRetryReviewProbe.cs` được inject
tạm qua ignored MSBuild targets: **3/3 pass** ở code mới. Sau đó build/test lại
tracked assembly không có import và list-tests xác minh không còn ReviewProbes.

## LB01 — P2: SQL literal làm đổi mật khẩu chứa backslash

File `scripts/bootstrap-mariadb-acceptance.ps1:135–137`:

```powershell
function Escape-SqlLiteral([string]$value) {
    return ($value -replace "'", "''")
}
```

Hàm chỉ escape dấu nháy. Giá trị được ghép vào `IDENTIFIED BY '$passwordLiteral'`
tại dòng153 và khi grant lại. Trong sql_mode MariaDB mặc định không có
NO_BACKSLASH_ESCAPES, `\n`, `\t`, `\b`, `\'` có nghĩa escape thay vì ký tự gốc.

Tái hiện an toàn:

1. Chạy script thật trên PowerShell5.1 với fake docker trong test harness hiện có,
   mật khẩu synthetic gồm6 ký tự `Aa\nBb`. Script exit0 và báo Bootstrapped; SQL
   sinh ra vẫn chỉ có một backslash trong literal.
2. SELECT read-only trên MariaDB10.5.23 acceptance kiểm cùng literal:
   expected UTF8 hex `41615C6E4262`, actual `41610A4262` (newline thay `\n`).
   Không tạo user/đổi password thật để thử lỗi, không chạm DB hosting/dev.
3. Kết quả: tài khoản được bootstrap có thể mang mật khẩu khác với connection
   string người gọi cung cấp; bootstrap báo thành công nhưng bước kết nối sau
   thất bại. Không kết luận production bị ảnh hưởng: đây là script acceptance local.

Artifacts ignored: `.tmp/sa-rereview/bootstrap-password-probe.py`,
`bootstrap-password-probe.log`, `bootstrap-password-generated.sql`,
`password-literal-mariadb.log`.
Contract tham chiếu: [MariaDB string literals](https://mariadb.com/docs/server/reference/sql-structure/sql-language-structure/string-literals).
Sửa bằng encoding/literal có contract rõ với session sql_mode, hoặc driver hỗ
trợ parameter thích hợp; kiểm actual login bằng đúng password trên DB disposable.
Không vá bằng cách đổi mật khẩu owner hoặc thay sql_mode global của server.

## Verification lần này

| Check | Actual |
| --- | --- |
| App Debug build, artifacts riêng | 0 errors, 35 warnings |
| Focused Release transaction/Goals/Whisper/assessment | 34 pass, 0 skip |
| Ba probe review cũ | 3 pass, 0 skip |
| Full tracked Release suite, provider local được bật | 729 pass, 0 skip |
| Windows Python suite gồm6 bootstrap tests | 61/61 pass |
| Tooling validator | Pass; không thay app tests/UI |
| Diff whitespace | Pass |

Provider run dùng container mới do review sở hữu, DB
`tct_acceptance_review_5391b7048c` tại127.0.0.1:3312, image MariaDB10.5.23 digest
đã pin trong repo. Bootstrap/migrations local rồi full suite, bao gồm dual-credit
5 vòng. Cleanup xác minh exact container id và run label trước khi xóa container/
anonymous volume của riêng lượt review. Giữ acceptance3310, dev3307 và app5110.
Logs/TRX: `.tmp/sa-rereview/`. Không đọc/in credential thật vào báo cáo.

Attempt test Debug đầu bị app5110 PID1456 khóa apphost.exe; chuyển sang Release,
không dừng app. Cursor ledger có incident từng dừng nhầm app5100; khi kiểm tra
hiện chỉ5110 đang listen, 5100 chưa có listener. Đây là trạng thái local riêng với
production; không tự restart hoặc đổi cấu hình dev trong review.

## Phần chưa nghiệm thu xong

- G03: notification450 recipients/lease/checkpoint/import/outbox provider matrix
  vẫn chủ yếu SQLite. Full729/729 có một provider test được bật, không biến toàn
  suite thành MariaDB tests.
- G01: có raw silence response và adapter tests; chưa full WAV→assessment→receipt
  →progress→credit/reload bằng engine thật qua ứng dụng.
- G04: có synthetic bcrypt login và HTTP authenticated200; chưa đủ25 view gốc,
  desktop/mobile/keyboard/states/races. Student bị redirect Admin là đúng quyền.
- G06: đã lấy client/dump local; enabled recovery drill còn vướng path Plesk
  hardcoded. Client nằm workspace có thể dùng cho harness đúng scope; không cần
  quyền ghi Program Files để chứng minh backup/failure/restore ở staging local.

Plan local: [LB01 và acceptance](../superpowers/plans/2026-10-31-local-bootstrap-and-acceptance-plan.md).
Prompt local: [Cursor Auto](../../.ai/prompts/cursor-auto-local-bootstrap-and-acceptance.md).
