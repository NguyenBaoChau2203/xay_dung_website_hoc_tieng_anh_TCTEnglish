# Phase 6 — hoàn tất ma trận trình duyệt local

Ngày 2026-11-07. Đây là bằng chứng bổ sung cho
[báo cáo triển khai ban đầu](browser-report.md). Giữ nguyên báo cáo cũ như lịch sử;
các hàng thiếu được chạy và kiểm lại ở lượt này. Trạng thái tại bàn giao là Phase6
**AWAITING_REVIEW**, Phase7 **LOCKED** theo yêu cầu giữ gate lúc đó. Sau yêu cầu
chốt mới nhất, [review round1](review-round-1.md) duyệt đúng snapshot153:
Phase6 **APPROVED**, Phase7 **READY**, chưa triển khai. Bằng chứng browser giữ nguyên.

## Môi trường và giới hạn bằng chứng

- Khởi động theo quy định: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
  -Configuration Release -WebPort 5196`. MariaDB dev hiện hữu được giữ nguyên.
  `/health/ready` của 5196 trả 200. `speech-status.ps1` báo configured,
  `gpt-transcribe`, VerifiedLive false; chỉ đọc metadata, không gửi audio.
- Fixture riêng: Docker `tct-migration-check-phase6browser`, MariaDB **10.5.23**,
  port **33326**, DB `tct_migration_check`, 15 migration. Không dùng dữ liệu hoặc
  tài khoản thật để hạ quyền. Ba user tổng hợp 700001/700002/700003.
- Web **5197** chạy DLL Release của ứng dụng, Razor/JS/CSS thật, DB fixture trên;
  web **5198** chạy DLL Phase6Closeout, cùng DB fixture và content root riêng
  `.tmp/hybrid-rag-review/phase-6/closeout/index-content` để phá index an toàn.
  `/health/ready` cả hai trả 200. Port là tài nguyên tạm của lượt này.
- Chrome DevTools, context riêng `phase6-closeout`. Đăng nhập cookie thật qua
  form có antiforgery; đăng xuất qua chức năng có sẵn. Không thêm route đăng nhập
  tắt, header giả danh, hay thay service phân quyền.
- Fake key và AI BaseUrl `http://127.0.0.1:18096` chỉ trong process con. Saved User
  Secrets không đổi. Loopback chỉ trả lỗi, không forward. Chốt `/status` là
  `requests: 0, paidCalls: 0`; không xuất hiện `provider-requests.jsonl`.
- Để chứng minh cache thực sự ấm, startup hook **chỉ đọc** ở process 5197 đăng ký
  DiagnosticListener và đọc singleton `ILearnerSourceDisplayCache` tại
  `Response.OnStarting`. Không thay service, response hay auth. Log chỉ chứa UTC,
  path/status/authenticated/hits/entries; không chứa cookie/khóa/body. Đây là
  instrumentation fixture dùng cơ chế
  [.NET runtime startup hook](https://github.com/dotnet/runtime/blob/main/docs/design/features/host-startup-hook.md).
  `cache-probes.json` có 52 hàng, không có observer failure.

Card A/B được hỏi qua UI thật, tạo câu trả lời thật của pipeline local. Hai lịch sử
lớp/media được seed bằng `GroundingCitationService.Capture` và hash nguồn hợp lệ
để kiểm tra đọc lại/thu hồi, không giả DOM hoặc vượt quyền đọc. Các lỗi transport
và sự kiện trễ được tiêm riêng vào browser để kiểm UI; chúng được ghi rõ dưới đây.
Không coi kết quả đó là provider round trip hay chất lượng API live.

## Cache ấm và thay đổi quyền — chạy thật trên MariaDB/browser

TTL cache là hai phút. Các mốc dưới đây lấy từ actual singleton của cùng process,
không dùng số đếm dựng trong test hoặc suy ra từ tải trang.

| Hàng | Trước thay đổi | Thao tác và kết quả sau thay đổi | Bằng chứng local |
| --- | --- | --- | --- |
| A → logout → B | A hỏi `what does forecast mean`, nhận `PHASE6_A_PRIVATE_DEFINITION`, nguồn card930001 `/Home/Study/920001`. 10:44:22.684Z tạo entry; reload 10:44:51.464Z tăng hits 0→1 | Logout A, đăng nhập B. Mở hội thoại A trả 404; endpoint đúng `/AI/Chat/Messages?conversationId=...` cũng 404 lúc 10:45:50.195Z. DOM/response không có body A, không link nguồn A. B hỏi cùng câu, nhận riêng `PHASE6_B_PRIVATE_DEFINITION`, nguồn card930002 lúc 10:46:02Z, trước expiry 10:46:22Z của entry A | `account-b-isolation.json`, `account-b-own-reply.json`, `cache-probes.json` |
| Premium → Standard, cùng cookie A | Media880001 có body/title `PHASE6_PRIVATE_CLIP_A`, route `/Speaking/Practice/880001`. 10:53:52.816Z tạo/ređọc; 10:53:53.115Z hits 4→5, entries3 | Chỉ hạ role DB của user tổng hợp A, chờ lệnh exit0. Không đăng nhập lại để làm mới claim. Reload 10:53:57.351Z: hits5, entries2; DOM không media/title/link. Messages HTTP200, body thay bằng notice, status `unavailable`, sources `[]`. Embed 10:53:59.975Z cũng không có nguồn riêng | `media-downgrade-verified.json`, `cache-probes.json`; không dùng `media-after.json` thăm dò lỗi route làm acceptance |
| Gỡ thành viên lớp A | Class810001 hiện `PHASE6_CLASS_MEMBER_ONLY`, route `/Home/ClassDetail/810001`. Reload 10:48:47.481Z tăng hit lên2, entries3 | Xóa membership A trên fixture, giữ owner và lớp. Reload 10:48:53.075Z: hit2, entries2. DOM không body/title/link lớp. Messages HTTP200, body notice, status `unavailable`, sources `[]` | `class-citation-before.json`, `class-citation-after.json`, `cache-probes.json` |

Không thay cache partition hoặc bỏ kiểm tra quyền để làm xanh ma trận. Test SQLite
`WarmCache_DoesNotLeakAcrossAccountDowngradeOrRevoke` vẫn là regression bổ sung;
các hàng browser ở trên mới là bằng chứng đóng blocker đã nộp.

## Lịch sử, source và luồng hoàn tất

- Hội thoại A `03b1aa32-3ee1-441a-bdc4-54cbd69e64a5`: seed 115 dòng legacy metadata
  NULL cùng hai tin thực. Browser hiển thị 50 tin ban đầu; bấm **Load older** hai
  lần qua network thật thành 100 rồi **117**. 117 id duy nhất, 115 marker
  `PHASE6_LEGACY_000`…`114` đúng thứ tự; legacy không bị gắn nguồn; nút older hết.
  Link card của tin có metadata vẫn đúng. `real-paging.json`.
- Đổi title card thành chuỗi `<img src=x onerror=alert(1)> Tiêu đề nguồn dài `
  cùng 140 chữ W. Full/embed reload hiện chữ literal, không phần tử `img` trong
  link; nguồn chuyển `changed`. DOM và screenshot xác nhận xuống dòng.
  Bộ Node thực thi JS production kiểm thêm `javascript:`, external URL, `/Admin`,
  route không hợp lệ và title rỗng. Không gọi route nguy hiểm để thử.
- Xóa card A trên DB fixture rồi reload embed: body cũ bị thay notice; không còn
  `PHASE6_A_PRIVATE_DEFINITION`, title hostile hoặc link; Messages HTTP200, nguồn
  `unavailable`, sources `[]`. `source-deleted.json`. Sau bằng chứng, khôi phục
  đúng card tổng hợp ban đầu để thử tiếp, không phục hồi dữ liệu owner.
- Gửi lại `what does forecast mean` trên hội thoại A qua **SignalR thật**:
  11:09:39Z một accepted stream, body A và nguồn `forecast` `/Home/Study/920001`.
  Embed reload + Messages rồi full reload cho cùng hai body/nguồn đã lưu, cùng
  nhãn `Đã trả lời từ nguồn được phép xem`. `real-stream-answered.json`,
  `real-embed-reload.json`, `real-full-reload.json`. HTTP send ban đầu cũng dùng
  pipeline thật. Không dùng lời đáp model giả để chứng minh hàng này.

## Stop, submit, retry và lỗi — ghi đúng tầng được kiểm

Trang Razor/JS thật, transport SignalR fixture có promise Start chưa settle:

1. Enter hai lần trong lúc pending: chỉ một Start, không factual text trước accept.
2. Stop trước Started khôi phục đúng draft. Enter lại khi Start cũ còn pending:
   không có Start thứ hai và không HTTP fallback; có notice chờ kết thúc.
3. Started cũ đến trễ: gửi đúng `StopAiStream('browser-old')`; chunk/completion và
   source cũ không xuất hiện. Settle invocation cũ rồi chủ động Enter lại mới gửi.
4. Lượt mới nhận chunk1 trước chunk0, chunk0 lặp; replay Started/completion của
   lượt cũ. UI chỉ nhận `ACCEPTED SOURCE`, route `/Home/Reading`; không `STALE FACT`,
   `STALE SOURCE`, `DUPLICATE`, không partial trước complete.

`browser-stream-green.json`, `browser-stream-init.js` và Node đỏ/xanh lưu ở thư mục
evidence. Đây là controlled event ordering, không phải cố tình làm mạng thật đảo
thứ tự. Production SignalR giữ cùng contract, không thêm request-id vào wire.

Ở trang embed mới, chỉ thay fetch cho `/AI/Chat/Send`, các fetch khác vẫn thật:

| Fault UI được tiêm | Kết quả thực tế |
| --- | --- |
| HTTP429 từ quick action | 1 send, 1 bubble user, draft nguyên câu Reading, notice tiếng Việt và nút Thử lại. Chủ động retry: sends2, vẫn 1 bubble user, draft rỗng, idle |
| Response504 có error timeout, trả sau 200ms | Tổng sends3, không auto retry, draft `phase6 timeout draft`, error có notice timeout; không kẹt sending |
| `TypeError('Failed to fetch')` | Tổng sends4, draft `phase6 network draft`, notice mạng gián đoạn và Thử lại; không tự gửi lần hai |
| JSON outcome provider_unavailable | Nhãn `Trợ lý tạm thời không trả lời được` |

`browser-429.json`, `browser-timeout.json`, `browser-network.json`. Lỗi provider
HTTP/usage/retry/timeout được kiểm ở .NET bằng fake HTTP trong focused224; **browser
không phát sinh provider request**, không gán các fault UI này thành provider E2E.

## Thiếu/hỏng index và accessibility

- Process5198 có content root riêng, manifest chưa tồn tại. Quick action Reading
  11:07:10Z trả notice chỉ mục chưa hợp lệ, nhãn **Chưa đủ căn cứ**, không link,
  idle. Gửi tiếp qua SignalR thật cũng cùng body/nguồn rỗng.
  `real-http-reading.json`, `real-stream-reading.json`.
- Tạo manifest JSON hỏng có marker fixture trong đúng content root riêng, không
  đụng corpus owner. Quick action Reading 11:07:59Z cho cùng notice và nhãn,
  không nguồn, không loading kẹt. `index-corrupt-reading.json`.
  Các probe câu hỏi khác ra out_of_scope không được dùng thay bằng chứng index này.
- Native keyboard **Shift+Tab rồi Tab** trên trang full: document có focus,
  activeElement đúng link, `:focus-visible` true, outline xanh solid2px được nhìn
  thấy bằng ảnh. `source-focus-final.png`, `focus-final.json`.
- Viewport mobile thật **390×844** (emulate, không dùng resize bị giới hạn500):
  innerWidth và document.scrollWidth đều390, nút Gửi trong khung, title hostile dài
  xuống dòng, không có ảnh được thực thi. `narrow-390-final.png`, `narrow-final.json`.
- **200% text size**, root font32px trên viewport1440×1000; đây là text zoom bằng
  root font-size, không tuyên bố browser UI zoom/device scale. scrollWidth1440,
  title/notice đọc được, input và Gửi dùng được. `text-200-final.png`,
  `zoom-final.json`. Không commit style inline thử nghiệm; reload xóa nó.
- Trang full cũng được kiểm tại390px: scrollWidth390, history đóng mặc định,
  input và Gửi nằm trong khung. Text200% ở full: scrollWidth1440, composer còn
  dùng được, lịch sử và chat có vùng cuộn. `full-narrow-390.png`, `full-text-200.png`.
- CSS repair: section không còn kế thừa pre-wrap; bubble bỏ whitespace template,
  markdown giữ pre-wrap. Cùng card/viewport: body markdown327.53px không đổi,
  section95.06px; bubble636.91→466.72px sau sửa khoảng trắng ngoài partial.

## Bằng chứng và tái hiện

Tất cả helper/log/JSON/ảnh/TRX nằm ở ignored
`.tmp/hybrid-rag-review/phase-6/closeout/` trong checkout này. Các file được nêu
đích danh mới là acceptance; probe thăm dò chưa chờ command/response hoàn tất hoặc
sai route không được tính. Không đưa key/cookie vào báo cáo. Fixture script chỉ
nhận DB/port/container ID allowlist; mật khẩu DB ngẫu nhiên được đọc vào memory.

Để tái hiện: dùng launcher free port và fake child AI settings; tạo MariaDB10.5.23
dùng một lần, migrate/seed các thực thể tổng hợp trên; login thật; hỏi card rồi
reload để đọc actual cache hit. Trong TTL, logout/login B hoặc cập nhật role/
membership fixture, chờ mutation exit0, reload full/embed và gọi endpoint Messages
đúng query. Kiểm absence cả body/title/link; không dựa riêng vào status hay số hit.
Lịch sử lớp/media cần envelope do Capture tạo, không dùng raw title làm quyền.

Sau nghiệm thu, dừng các web/provider/DB fixture do lượt này tạo, giữ storage và
evidence để reviewer xem. Giữ MariaDB dev owner, saved secrets, corpus owner,
applied migration history và branch/HEAD. Không commit/push/CI/deploy.

Chất lượng semantic, latency API thực và tài khoản/quota provider live vẫn chưa
được đo. Đây là giới hạn của lượt local, không phải kết quả Phase 7.
