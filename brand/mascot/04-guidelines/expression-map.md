# Tico expression map

Poses exist as assets under `02-raster/poses/` and `TCTEnglish/wwwroot/brand/mascot/`.

| Pose | Board label | Intended UX use |
| --- | --- | --- |
| `welcome` | Chào mừng / Đăng nhập | Welcome / empty encouragement |
| `success` | Thành công / Hoàn thành bài | Success feedback |
| `retry` | Sai đáp án / Thử lại | Error / try-again (supportive) |
| `thinking` | Suy nghĩ / Đang tải | Loading / content not ready yet |
| `hero` | Chân dung chính | General portrait (asset ready; not wired in v0.1 views) |
| stickers | Sticker / biểu cảm nhỏ | Chat-sized reactions (`sleep` / `peek` / `cool` / `wink` / `cheer` + sheet) |
| head crop | Head sticker | Chatbot FAB / AI chat avatar (**not** site favicon) |

## Site favicon

Always the **logo icon**, not Tico head.

## Wired in v0.1 (existing UX only — no new pages)

| File | Pose | State |
| --- | --- | --- |
| `TCTEnglish/Views/Shared/_StateMessage.cshtml` | welcome / thinking / retry / success by `StateMessageKind` | Shared empty/loading/error/success partial |
| `TCTEnglish/Views/Study/Reading.cshtml` | welcome | Empty published reading list |
| `TCTEnglish/Views/Study/Listening.cshtml` | welcome | Empty personal listening list |
| `TCTEnglish/Views/Speaking/Index.cshtml` | welcome | Empty “my videos” |
| `TCTEnglish/Views/Speaking/Index.cshtml` | thinking | Empty public library |
| `TCTEnglish/Views/Shared/_AiChatLauncher.cshtml` + `Views/Ai/_ChatShell.cshtml` | head sticker | Chatbot FAB / AI chat avatar |

## Not changed in v0.1

Auth brand panel, Admin layout, Settings sidebar, Goals empty CTA chrome, and any page that does not already show an empty/error illustration.
