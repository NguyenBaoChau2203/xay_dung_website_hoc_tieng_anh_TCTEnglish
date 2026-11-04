# Đóng Plan 1 + Plan 2 ở local — 03/11/2026

**Kết luận:** đã hoàn tất triển khai và kiểm chứng local của hai Plan UI và
logic/hiệu năng trên nhánh feature/chau-ui-performance-audit. Các checkpoint
02/11 là lịch sử; báo cáo này cập nhật những mục từng ghi PARTIAL/PENDING.
Không còn lỗi chặn local đã tái hiện trong phạm vi đã kiểm tra. Các nghiệm thu
ngoài môi trường local ở cuối tài liệu vẫn chưa thực hiện.

Owner cho phép sửa trực tiếp để kết thúc hai Plan. Một agent điều tra, sửa
và kiểm tra; giữ dirty work hiện hữu, không sửa báo cáo BTH2. Chưa commit/push,
chưa tạo PR/chạy CI remote/merge/deploy. Không thay đổi Plesk hoặc deploy gate.
Website vẫn dùng gpt-6-luna / low; không gửi request OpenAI trả phí hay email thật.

## 1. Kết quả theo batch

| Batch | Kết quả local | Chứng cứ chính |
| --- | --- | --- |
| Plan 1 | Giữ UI Account, Profile/Settings, AI full/embed, Speaking/Writing và Admin đã sửa qua các vòng trước | Browser mobile/desktop; smoke theo ba vai trò; submit guard |
| Preflight F1/F2 | Giữ breakpoint Admin991.98 và test submit chạy production JS thay cho dò source | UI review trước + Node behavior tests |
| BE0 | Có baseline trước thay đổi, fixture MariaDB riêng và phép đo sau sửa | LocalCatalogScaleTests, 200/2.000 records; 5warmup +30samples/scenario |
| BE1 | Guard hash social/legacy, validation, reset consume/replay/race, AccessDenied403, text Admin | Account integration tests + Standard vào Admin403 |
| BE2 | Quota reserve/settle tách unit of work, không release usage đã biết; recovery có caller ở lượt gửi sau | MariaDB concurrency + AI lifecycle regressions |
| BE3 | History keyset/load older nhiều trang, context theo budget; không gửi HTTP lại sau unknown SignalR outcome | AI history + Node chronological order/unknown outcome |
| BE4 | Speaking SQL filters/paging; Writing SQL filters/status/count/page; owned Writing paging; Admin SQL page20 | Catalog tests, provider scale, EXPLAIN, browser riêng24→25→xóa→24 |
| BE5 | SMTP cancellation/deadline/result; worker claim trạng thái trước gửi, lỗi một recipient/broadcast không bỏ cả batch | SMTP fake + worker regressions; mặc định off |
| BE6 | Profile chỉ đọc badges đã trao theo user bằng1 query; launcher fetch usage khi mở | ProfileBadgeReadTests, launcher/API tests, browser |
| BE7 | Vendored Marked/SweetAlert2/DOMPurify có license/hash; bỏ CSS import trùng, version CSS | Provenance, Markdown/XSS probe, 3trace trước +3trace sau |
| BE8 | Full suite, migration/race, browser và ASR synthetic local | Bảng bên dưới; gate hosting/máy khác tách riêng |

## 2. Những phần sửa thêm khi chốt

- **AI:** bỏ HTTP retry tự động và fallback sau invocation SignalR đã dispatch
  nhưng chưa biết kết quả. Khi provider đã trả usage mà lưu reply thất bại:
  bỏ reply chưa committed, cleanup đúng prompt/draft, giữ pending usage để recover,
  trả lỗi yêu cầu đọc lại history trước gửi mới. Quota API tính cả pending; lượt
  gửi sau recover usage đã biết từ scope cũ.
- **Context:** bỏ giới hạn100 message tùy ý. Đọc keyset từng50 message đến
  token budget; tính cả envelope để tin nhắn rất ngắn không làm đọc vô hạn.
- **Speaking:** tạo card bằng DOM/textContent thay cho nội suy dữ liệu vào HTML;
  load-more có error/retry và luôn mở lại nút. Thẻ riêng tải thêm giữ nút học/xóa/
  nâng cấp và trạng thái xử lý. Đếm tổng server thay vì số card trang đầu.
  Nút xóa mới hoạt động ngay lần đầu: không replay nested click() khi activation
  cũ đang chạy. Xóa fixture thật đã giảm count25→24.
- **Writing:** list không tải toàn bộ attempts/feedback. SQL tính attempt/pass/
  thời gian cuối và chỉ materialize trang6. Pass lịch sử vẫn giữ dù lần mới fail.
  MariaDB phát hiện correlated DISTINCT derived table không truy cập được alias
  ngoài; thay bằng đếm sentence có EXISTS attempt pass. Không tăng timeout hoặc
  bỏ test provider để che lỗi.
- **Admin catalogs:** lọc trước Skip/Take20; topic facets lấy toàn catalog; stats
  dùng aggregate riêng; paging giữ query string. Giữ modal sửa/xóa, role và CSRF.
- **Email/unlock:** commit state trước SMTP. Chỉ caller thắng conditional update
  gửi thông báo. Phân biệt deadline/caller-cancel; lỗi broadcast không chặn mail,
  lỗi một mail không bỏ recipient tiếp theo. Không mở DB transaction qua SMTP.
- **Profile/assets:** bỏ full Goals query khi chỉ cần badges, không award trên
  GET Profile; không query quota ở mọi layout; pin thư viện, bỏ import CSS trùng.

## 3. Kiểm chứng

Artifacts ở .tmp/plan-closeout/, được ignore khỏi Git. Các lượt chạy độc lập
không được cộng thành một tổng pass gây nhầm lẫn.

| Kiểm tra | Kết quả | Artifact |
| --- | --- | --- |
| Full .NET cấu hình tối ưu CloseoutFinal | 862 pass /0 fail /13 skip, tổng875, 3m28s | full-final.trx |
| Full solution Release chuẩn, sau các sửa cuối | 862 pass /0 fail /13 skip, tổng875, 3m31s | release-final.trx |
| Full solution Release sau migration bỏ index trùng | 862 pass /0 fail /13 skip, tổng875, 3m27s | release-schema-final.trx |
| JS AI + Speaking chạy production scripts trong harness | 17 pass /0 fail | js-all-final.log |
| MariaDB quota/completion/notification concurrency | 7 pass /0 fail | mariadb.trx |
| MariaDB concurrency sau bỏ index trùng | 7 pass /0 fail, 28s | mariadb-final.trx |
| Upgrade October →2 migration quota, chạy lại, giữ user/history (trước tối ưu index) | 1 pass /0 fail | upgrade.trx |
| Upgrade đủ3 migration, giữ user/quota/PK, bỏ đúng index trùng và chạy lặp | 1 pass /0 fail, 17s | upgrade-final.trx |
| Scale trước/sau MariaDB10.5.23 | Mỗi lượt2 case pass; Writing/Speaking/history50/500/5000 | baseline.trx, after.trx, JSON theo size |
| SMTP/worker/AI lifecycle focused | 28 pass /0 fail | workflow-final.trx |
| Admin paging + Writing focused | 11 pass /0 fail; trước sửa2/2 red | admin-green.trx, admin-red.trx |
| AI reply-save failure + profile badge isolation | 4 pass /0 fail | closeout-extra.trx |
| Whisper thật: silence + app assessment/commit/replay + decode/import | 3 pass /0 fail, 31s | asr-app-retry.trx |
| EF pending-model, local tool9.0.4 | Không có model drift | model-check.log |
| EF pending-model sau migration index | Không có model drift | model-final.log |

Full suite tắt opt-in. Trong13 skip có nhóm MariaDB, migration, scale, ASR đã
chạy riêng ở trên; hai test OpenAI live trả phí vẫn không chạy. Không gọi full
suite có skip là đã chạy tất cả live acceptance.

ASR mở rộng lần đầu fail engine_unavailable vì engine đã dừng. Sau khi chạy
local-speech.ps1 -Action Start và Status inference probe ready, ba test chạy lại
đều pass. Không đổi lỗi thành skip hoặc tăng deadline ứng dụng. WAV/media dùng
fixture synthetic local đã có; không mic cá nhân/download YouTube thật.

### Browser và dữ liệu

Tạo duy nhất một account synthetic, lần lượt cấp Standard/Premium/Admin trên
chính fixture rồi login lại để refresh role. Không dùng/đổi role account owner.
Cả ba vai trò GET Profile, Settings, Goals, Speaking, Writing list
(/Home/Writing/Exercises), AI full/embed đều200. Standard vào Admin403; Admin mở
Dashboard/UserManagement/Speaking/Writing management200.

- Màn390px/reduced motion: Profile/Speaking/AI/Admin không tràn ngang document;
  table có vùng cuộn riêng. Desktop1440px cũng được kiểm tra.
- Admin page20, visibility filter thật giữ selected, form GET; page2 và modal
  xóa đúng ID/title đã mở, không xóa dữ liệu cũ.
- Speaking seed25 video synthetic riêng: load24+1, thẻ cuối có học/xóa, mở modal
  ngay lần đầu, xóa fixture qua endpoint, count25→24 không reload.
  Native DOM probes kiểm thumbnail injection và503/error/re-enable.
- Marked render bold; DOMPurify loại script/onerror. Script ở /lib/pinned;
  SweetAlert2 global sẵn sàng trong Admin. Request ảnh /AI/x404 trong probe
  XSS là ảnh synthetic của chính test, không asset sản phẩm bị thiếu.
- Launcher ban đầu ghi đang kiểm tra; mở panel mới fetch, đúng0/15 cho account
  mới và lazy-load iframe. Không gửi chat tới cloud thật.

Xóa account bằng own DeleteAccount sau identity guard FullName/email/CSRF;
cleanup toàn bộ fixture còn lại. Users checksum trước/sau đều **4192988673**,
account synthetic còn0, video synthetic còn0. Không reset dev DB/import backup
hosting. Launcher áp dụng migration local; trạng thái cuối được kiểm lại.

### Benchmark và SQL

i5-11400H (6cores/12threads), RAM15,7GiB, Docker Linux engine, MariaDB10.5.23
port3328/DB disposable có sentinel/runId. net9/config tối ưu, một request lab
mỗi lần, 5warmup+30samples, không paid AI. Cùng harness/size; desktop/web/Whisper/
browser tạo background load. Đây là lab, không SLA hoặc hosting load test.

| Writing list | Trước200 | Sau200 | Trước2.000 | Sau2.000 |
| --- | ---: | ---: | ---: | ---: |
| p50 ms | 36,67 | 25,94 | 336,86 | 38,58 |
| p95 ms | 45,45 | 39,10 | 427,05 | 92,79 |
| Rows thực đọc | 801 | 10 | 8.001 | 10 |
| Allocated bytes trung bình/process | 2.345.398 | 322.708 | 21.235.083 | 315.304 |
| Reader commands | 4 | 6 | 4 | 6 |
| Serialized VM bytes | 3.030 | 3.097 | 3.042 | 3.100 |

Size2.000: rows giảm99,88%, allocations98,52%, p50 giảm88,55%. Hai query thêm
phục vụ count/owned paging có giới hạn, không N+1. VM không giảm vì UI trước đây
cũng chỉ trả trang6 sau khi tải hết data; tăng nhẹ do pagination metadata mới.
Đây không phải HTML/network bytes.

Speaking2.000 giữ30rows/7commands; p50 sau58,98ms so trước45,42ms trong lab
background load: **không tuyên bố Speaking nhanh hơn**. Lợi ích là continuation/
filter/function đúng. History50/500/5000 đọc tối đa52rows,2commands, trả50/page.
Profile regression dùng1reader command, ít hơn full Goals, đúng user, không sinh
badge. Không đo30sample Profile và không tuyên bố giảm latency Profile.

explain.txt là EXPLAIN SQL tương đương projection mới trên MariaDB, không log
params/data thật. LIMIT6; attempt count dùng UserId/Exercise/CreatedAt index;
sentence dùng Exercise/SortOrder. Không thêm index trùng hoặc migration từ
recommendation SQL Server cũ.

### Assets

Vendored Marked18.1.3, SweetAlert2 11.26.25, DOMPurify3.2.6 (giữ bản DOMPurify
cũ); license/SHA256/source ở wwwroot/lib/pinned/provenance.json.
Release đã kiểm: [Marked](https://github.com/markedjs/marked/releases),
[SweetAlert2](https://github.com/sweetalert2/sweetalert2/releases).

3trace trước/3trace sau trên Auth desktop, cold cache/CPU1x/no throttle.
Khôi phục tạm đúng duplicate import CSS do phiên này sửa; sau đó restore
byte-for-byte, hash35F623359999F1E9A876BA153CF6927E34C2424189692B7FEBFA86083FE15AB0.

- Trước9resources, hai learning CSS URL có/không version, mỗi response
  transfer2.767bytes; FCP876/484/504ms.
- Sau8resources, một learning CSS response2.767bytes; FCP440/344/324ms.
- Ba mẫu ít/fonts/CDN biến động: chỉ khẳng định bỏ1 request trùng/2.767bytes
  trong điều kiện đo, không cam kết LCP/SLA từ estimated savings.
- Trace filePath Windows bị MCP từ chối: đã loại lượt đó, chạy lại6 trace hợp lệ
  không filePath; summary/timing ở assets-before.json và assets-after.json.

## 4. Gate ngoài local, chưa thực hiện

Các mục này là nghiệm thu phát hành/vận hành riêng, **không được coi là pass**
khi đóng phần triển khai local:

1. Mic/accent/noise người thật, quality pronunciation/semantic Writing, nhiều
   người đồng thời. Synthetic ASR chỉ chứng minh pipeline/receipt/credit/replay.
2. Clean teammate setup trên máy Windows khác; doctor/bootstrap trên máy này
   không chứng minh clean-PC acceptance.
3. N518/Plesk: xác nhận quyền FFmpeg/ASR, CPU/RAM/service manager và kết nối
   nội bộ/TLS/auth. Local là ASP.NET web + Docker MariaDB + whisper.cpp loopback.
   Hosting ASP.NET/IIS + MariaDB đã biết; ASR/FFmpeg cần service được hosting cho
   phép hoặc máy chủ khác được kiểm soát. Chưa chứng minh shared hosting chạy
   Docker/Whisper; không trỏ production vào localhost máy cá nhân/mở inference
   server ra Internet để vượt giới hạn gói.
4. Staging deploy/backup failure/restore drill/readiness/public commit marker.
   Production gate vẫn off; CI xanh không cấp quyền bật gate.
5. Cloud key/account/quota: chỉ xác nhận key source configured, không live quota.

SMTP commit state trước mail/conditional claim ngăn hai worker gửi trùng cùng
trạng thái; không hứa exactly-once qua crash/network, chưa có durable email
outbox. Worker default off là rollout có chủ ý. Unknown provider billing không
tự suy ra; usage đã biết recover, request không rõ kết quả phải đọc history.

Hai migration quota additive có Designer/snapshot. Migration thứ3
20261103141227_RemoveRedundantAiQuotaUniqueIndex bỏ index phụ trùng khóa chính
(UserID,DayUtc), giữ PK/foreign key và mọi hàng; Down tạo lại index phụ. Không
sửa migration đã áp dụng. Test guarded tự dựng baseline October bằng EF thật
trên DB mới, thêm user/quota rồi upgrade và chạy lặp; không invent history.

### Sự cố local khi chuẩn bị fixture — đã báo owner

Một lệnh EF chuẩn bị baseline đặt nhầm ConnectionStrings__Dbflashcard thay vì
ConnectionStrings__DefaultConnection. Design-time factory dùng lại User Secrets
dev, nên database local bị rollback hai bảng quota mới của phiên này. Đã dừng
lệnh, áp dụng lại migration bằng launcher chuẩn và kiểm tra trạng thái cuối.
Production không được truy cập. Không khẳng định chưa hề chạy Down trên dev.

Trạng thái cuối: ba migration đều có trong history; hai bảng quota tồn tại và
hiện có0 row; PK có đủ2 cột, index phụ còn0; EF không có drift. Web tại
http://localhost:5112/health/ready trả200/ready; Whisper Status inference probe
ready. GitHub variable PLESK_DEPLOYMENT_ENABLED đọc lại là false.

Users checksum trước/sau vẫn4192988673 và các fixture riêng đã dọn sạch.
Phiên này không gửi cloud request hoặc tạo quota cho account owner; tuy vậy
không có snapshot hai bảng quota ngay trước thao tác nhầm, nên không dùng
checksum Users làm chứng cứ rằng mọi quota row trước sự cố đã được giữ.
Lịch sử chat/request logs nằm ở các bảng khác; không bị hai Down này xóa.
Đã bỏ cách chuẩn bị baseline bằng design-time CLI khỏi test: test dùng connection
acceptance explicit, kiểm loopback/tên DB/sentinel/runId và history trống trước
migrate. Không reset DB dev hoặc giả migration history để khắc phục.

## 5. Điểm tiếp tục

Owner có thể đóng hai Plan **trong phạm vi local** để chuyển việc tiếp theo.
Thay đổi vẫn ở working tree trên nhánh đã nêu, chờ lệnh review/commit theo nhóm.
Các gate mục4 là checklist trước release, không phải implementation còn dở bị ẩn.
Lần chạy local sau dùng launcher chuẩn:

    pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser

Key riêng/User Secrets/fallback OPENAI_API_KEY theo rule hiện hành; không paste
key vào prompt/repo, không đổi model website theo model IDE.

Final hygiene: git diff --check sạch; validator7 tài liệu/14 relative links,
6 hash/license assets đều hợp lệ; scan key pattern trên changed/untracked files
không thấy key. Các kiểm tooling tự động không thay thế kết quả app/browser ở trên.
