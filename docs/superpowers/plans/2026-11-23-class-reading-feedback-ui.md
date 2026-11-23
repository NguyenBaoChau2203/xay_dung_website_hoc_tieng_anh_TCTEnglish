# Class + Reading feedback UI — ledger

## Scope

- Shared `TctFeedback.toast` / `TctFeedback.confirm` on learner `_Layout`
- Migrate Class + Reading off native `alert` / `confirm`
- Out of scope: Sets.toast visual parity, Speaking, Admin, inbox bell

## Files

- `TCTEnglish/wwwroot/js/tct-feedback.js`
- `TCTEnglish/wwwroot/css/tct-feedback.css`
- `TCTEnglish/Views/Shared/_TctFeedback.cshtml`
- `TCTEnglish/Views/Shared/_Layout.cshtml`
- `TCTEnglish/wwwroot/js/Class-Detail.js`
- `TCTEnglish/Views/Class/Class.cshtml`
- `TCTEnglish/Views/Class/BrowseClasses.cshtml`
- `TCTEnglish/wwwroot/js/reading-study.js`
- `TCTEnglish/wwwroot/js/reading-translation.js`

## Acceptance

- No native alert for Class copy-code / join pending / Reading timer & submit error
- Destructive Class/Reading actions use Clay Bootstrap confirm modal
- RETEST R1–R10 recorded Pass / Fail / Blocked with evidence

## RETEST log (2026-11-23)

Environment: `localhost:5100` (`scripts/start-Local.ps1`), Chrome DevTools MCP.
Auth: không có session đăng nhập trong MCP browser → Class Detail / Reading / Sets thao tác thật **Blocked**.

| ID | Result | Evidence |
|---|---|---|
| R1 (toast copy UX) | Pass (API/layout) | `/Home/Privacy` + smoke: toast title/message đúng, viền success `rgb(16,185,129)`, z-index 1090, không `alert` native |
| R2 clipboard deny | Blocked | Cần Class Detail + clipboard deny trên session owner |
| R3 regen confirm | Pass (API) | Confirm modal mở, Hủy→`false`, OK→`true`, nút `btn-danger`; Class Detail regen chưa chạy được (auth) |
| R4 leave/delete/kick | Blocked | Cần role owner/member trên Class Detail |
| R5 approve/decline | Blocked | Cần pending join request |
| R6 join pending toast | Blocked | Cần user ngoài lớp |
| R7 Reading timer | Blocked | Cần bài Timed + session |
| R8 submit error | Blocked | Cần Reading attempt + lỗi mạng |
| R9 delete translation | Blocked | Cần bản dịch + session |
| R10 Sets toast regress | Blocked | Cần đăng nhập AddCards |

Source gate: không còn `alert(`/`confirm(` production trong Class-Detail (chỉ fallback), Class/Browse pending dùng `TctFeedback`, reading-study không còn `alert`, reading-translation confirm qua `TctFeedback`.
