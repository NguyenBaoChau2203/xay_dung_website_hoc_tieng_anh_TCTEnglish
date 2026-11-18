# Kiểm tra và sửa logic Account — 2026-11-08

Thực hiện theo yêu cầu mới nhất của Châu: kiểm tra bản sửa của Gemini và trực tiếp sửa backend hoặc logic; UI/UX do Châu và Gemini duyệt. Một agent làm việc trên `feature/chau-ui-design-system`, HEAD `eed4e2c95c79ef708dc9af77c8b9326186835122`.

## Kết quả sửa

| Lỗi đã tái hiện | Trước sửa | Sau sửa |
| --- | --- | --- |
| Modal xóa tài khoản bị mất khóa cuộn khi resize | Mở Settings ở 390px, mở modal, chuyển 1440px: modal vẫn `show` nhưng `body` mất `overflow: hidden` và `modal-open`. | Account chỉ dọn trạng thái do drawer sở hữu; modal giữ khóa cuộn. |
| Drawer mất Bootstrap instance khi đổi breakpoint liên tiếp | Mobile → desktop → mobile trong lúc đóng: lần mở sau không có instance, `aria-modal` hoặc khóa cuộn. | Đợi transition hoàn tất và xét breakpoint hiện tại; drawer mở lại có instance, dialog và khóa cuộn. |
| Form bị khóa sau khi submit bị hủy | Listener hủy submit nhưng guard vẫn đánh dấu đang gửi và vô hiệu hóa nút. | Tôn trọng sự kiện đã bị hủy; nếu bị hủy ở listener sau thì khôi phục pending qua microtask. Vẫn chặn gửi trùng và giữ gate `CONFIRM`. |

Phần sửa chạy trong `account-ui.js` và `account-auth-ui.js`. Đây là xử lý sự kiện và trạng thái chức năng. Không sửa CSS, bố cục, màu sắc, nội dung Razor hoặc quyết định thiết kế của Gemini.

Đã đọc các hợp đồng Login, ForgotPassword, ResetPassword, ChangePassword và DeleteAccount hiện tại, đồng thời kiểm chứng bằng nhóm integration test Account. Không có phát hiện trong phạm vi này buộc phải sửa controller C# hoặc schema. Mô tả field trong execution ledger đã được sửa theo hợp đồng thực tế: Login gửi `email`, `password`; Register gửi `email`, `fullName`, `password`; cả hai có antiforgery. Checkbox `terms` không có `name`; không có binding `RememberMe`, `ConfirmPassword` hoặc `AcceptTerms` cho hai form này.

## File thay đổi trong lượt này

- `TCTEnglish/wwwroot/js/account-ui.js`: vòng đời drawer, quyền sở hữu trạng thái, khôi phục submit bị hủy.
- `TCTEnglish/wwwroot/js/account-auth-ui.js`: tôn trọng submit bị hủy, khôi phục native pending, giữ AJAX login.
- `scripts/tests/test_account_ui.cjs`: kiểm thử transition có hai giai đoạn, giữ trạng thái modal khác, breakpoint liên tiếp, gửi lần đầu/gửi trùng và hủy submit.
- `scripts/tests/test_account_auth_ui.cjs`: hồi quy native submit bị hủy trước/sau handler và AJAX login đã bị hủy.
- `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md`: sửa tên field theo source.
- `.ai/context/bug-fix-log.md`: ghi nguyên nhân và bằng chứng.
- `docs/superpowers/plans/2026-11-08-account-logic-followup.md`: checklist thực thi.
- Báo cáo này.

## Bằng chứng kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet build TCTEnglish/TCTEnglish.csproj -c Release --no-restore` | Qua; 0 warning, 0 error. |
| `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs` | 23/23 qua. Trước sửa production, bộ hồi quy mới có 7 lỗi; log red/green được giữ. |
| Nhóm .NET AccountUiRenderTests, AccountCorrectnessIntegrationTests, ProfileBadgeReadTests, SharedUiPartialsRenderTests | 27/27 qua. Bao gồm reset token dùng một lần/cạnh tranh, bcrypt bất thường, HTTP 403 và IDOR 404. |
| Chrome 156.0.8078.80 với Razor + Bootstrap thật | 13 ca logic qua; chi tiết trong `.tmp/account-logic-followup/browser-results.json`. |
| Toàn bộ suite .NET, lượt 1 | 1175 qua, 22 skip, 1 lỗi trong 1198 test: `AiRagPhase5FixRound2Tests.StreamCompletion_UsesTheSameProjectionDeadline`, assertion deadline tại dòng 216. |
| Chạy riêng test RAG vừa lỗi | 1/1 qua. Chưa chứng minh được nguyên nhân của lỗi ở lượt toàn bộ; không thay source RAG hoặc nới assertion. |
| Toàn bộ suite .NET, lượt đối chiếu sau khi dừng Chrome/fixture host | 1176 qua, 22 skip, 0 lỗi; tổng 1198 test, thời gian 3 phút 5 giây. Giữ nguyên kết quả lượt 1 để không che lỗi deadline chưa xác định nguyên nhân. |
| Kiểm tra cú pháp JS và `git diff --check` | Qua. |
| Đối chiếu SHA256 với baseline đầu lượt | 31 tệp trong baseline; 24 giữ nguyên, 7 thay đổi đúng danh sách được phép (có cả checklist mới tạo trước baseline), 0 tệp mất hoặc thay đổi ngoài dự kiến. Razor, CSS và `DESIGN.md` giữ nguyên; branch và HEAD không đổi. |

Các ca Chrome gồm giữ khóa cuộn modal khi resize; đóng drawer khi chuyển desktop; đổi mobile → desktop → mobile rồi mở lại; resize lúc drawer đang mở; Escape; gửi Register native; Login AJAX lỗi rồi thử lại; Forgot/Reset gửi lần đầu; submit bị hủy; gate `CONFIRM`; Hủy sửa hồ sơ giữ ảnh đã lưu; link lấy lại mật khẩu khi token hết hạn.

Tệp bằng chứng tạm nằm trong thư mục ignored `.tmp/account-logic-followup/`: `baseline.json`, `preservation.json`, `node-red.txt`, `node-green.txt`, `browser-results.json`, các JSON tái hiện trước sửa và `tests/*.trx`. TRX chính: `account-followup-focused.trx`, `account-followup-full.trx`, `rag-deadline-isolated.trx`, `account-followup-full-retry.trx`. Các JSON trước sửa và sau sửa ghi trạng thái DOM; không lưu mật khẩu, antiforgery token hoặc thông tin người dùng thật. Host kiểm thử thuộc lượt này đã dừng; cổng 5184 không còn listener của fixture.

## Giới hạn và phần Châu/Gemini xem tiếp

- Browser dùng GET-only bridge của `TestWebApplicationFactory`, SQLite in-memory và dữ liệu giả. Mọi POST của browser được intercept; không gửi email, đặt lại mật khẩu hoặc xóa tài khoản thật. Các test backend chạy riêng trong fixture tự động.
- Không xác nhận app local cùng MariaDB đã ready trong lượt này; đây không phải bằng chứng provider MariaDB hoặc production.
- Hủy submit và `pageshow(persisted=true)` dùng sự kiện giả lập. Chưa xác nhận một vòng BFCache thật, zoom trình duyệt 200% hoặc các yêu cầu UI/UX.
- Shell hiện có lỗi console `jQuery is not defined` đã được ghi nhận ở baseline trước bản Account. Không sửa shared layout trong lượt này; kiểm tra hủy submit không được trình bày thành bằng chứng jQuery Unobtrusive hoạt động.
- Châu và Gemini tiếp tục xem bố cục, màu sắc, độ tương phản, focus/touch target và trải nghiệm ở các kích thước màn hình. Không đánh dấu UI/UX đã được duyệt từ kết quả logic này.
- Không commit, push, PR, remote CI, merge hoặc deploy. Không thay đổi Program.cs, appsettings.json, project file, model hoặc DB.

Để xem bản thiết kế tĩnh đang có, mở `docs/design-system/account-ui-preview.html`. Để xem app thật, chạy từ thư mục repo: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`; chỉ sử dụng URL local do launcher in ra sau khi `/health/ready` thành công. Nếu cổng đang được dùng, giữ tiến trình hiện có và chọn cổng trống với `-Configuration Release -WebPort <port>`. Preview tĩnh không thay cho kiểm chứng backend.
