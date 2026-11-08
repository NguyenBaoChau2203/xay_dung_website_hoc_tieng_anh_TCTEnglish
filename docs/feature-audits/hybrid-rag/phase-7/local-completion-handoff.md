# Phase 7 — local completion round 2

Ngày bàn giao: 2026-11-08. **Local AWAITING_REVIEW; nghiệm thu live BLOCKED.**
Owner yêu cầu sửa trực tiếp phần chưa ổn, mở web và chuẩn bị câu mẫu. Lượt này
thực hiện bằng một agent, không commit/push/CI/deploy và không gọi API trả phí.

## Danh tính

- Nhánh: `feature/hybrid-rag-chatbot`.
- HEAD: `6af768bd32d16cc6d012d484ee82df4cab805c69`.
- Input 163/163 đã khớp trước khi sửa:
  `41386db1939220cc3d9f7e3f31bf5e00ac48425a81a6896572a92101c7bd9a35`.
- Input giữ nguyên byte tại `scope-manifest-fix-round-1.json`.
- Archive tiền nhiệm 155 mục `scope-manifest-input.json` được giữ nguyên.
- Kết quả **172 mục**:
  `76a51a99633d0ea1574de7d0e63b7f7564e3a6b479df6c36a61ff16478a36806`.
- Handoff/review-state/current manifest và archive 163 mục không nằm trong digest;
  archive 155 mục vẫn nằm trong digest tích lũy như bản nộp. Không có credential
  hoặc runtime data trong scope.

## Các lỗi đã sửa trực tiếp

| Vấn đề | Nguyên nhân và thay đổi | Bằng chứng |
| --- | --- | --- |
| Run guard không hết hạn mặc định, promote quá hạn, usage âm/0 giải phóng reserve | Stopwatch mặc định, deadline liên kết HTTP/body, kiểm trước promote, giữ receipt sai thành unknown | 3 hồi quy đỏ trước sửa; sau sửa xanh |
| Guard chỉ được replay bằng số, chưa nằm trên client transport | `AiAcceptanceHttpHandler` dùng chung guard cho OpenAiEmbeddingClient và OpenAiProviderClient trong harness; mỗi send/retry đều qua cổng, đọc body có trần | 5 test trên production clients và HTTP giả: aggregate, retry/unknown, post cap, body timeout, receipt lỗi |
| “Nghĩa của từ forecast là gì?” bỏ sót thẻ có thật | Prefix ngắn lấy `tu forecast la gi` làm target; thêm frame đầy đủ trước prefix ngắn | 7 ca đỏ trước sửa, đầy đủ target/source sau sửa; kiểm thêm 2 ca không được lấy một phần cụm |
| “Ví dụ của forecast” thành lời chào trên model đi kèm | ML được phép đè CardLookup rõ ràng; dùng chung predicate từ khóa khi phân xử, vẫn cho ML xử lý bare text mơ hồ | Model giả thiếu lookup tái hiện Greeting trước sửa; regression trên model đi kèm và browser thật đều xanh |
| Login không tải Bootstrap CSS | SRI không khớp asset 5.3.3; sửa đúng digest, giữ kiểm integrity | Browser báo blocked trước sửa; sau sửa `bootstrapLoaded=true` |

Không sửa Program.cs, appsettings.json, csproj, model entity, migration hoặc file
model ML/fixture đóng băng. Không có pending model changes. Không thêm wrapper
acceptance vào production DI. Trần production embed giữ 12 POST / 40.000 token.

## Kiểm tra cuối

| Kiểm tra | Kết quả |
| --- | --- |
| Full app — `full-classifier-final.trx`, Phase7LocalCompletion | **1161 passed / 22 skipped / 0 failed**, tổng 1183; 3 phút 55 giây |
| Focused classifier/boundary/provider/acceptance | **109 passed / 0 failed** |
| Shipped-classifier regression thêm sau lượt focused | Có trong full cuối và pass |
| Node chat | **14 passed / 0 failed** |
| Standard Release launcher build | Thành công |
| MariaDB 10.5.23 migration repeat qua launcher | Không áp migration mới, DB đã cập nhật |
| EF pending model changes | `false` |
| `git diff --check` | Sạch |
| `/health/ready` | HTTP200, `{"status":"ready"}` |
| Speech status, metadata only | configured, `gpt-transcribe`, `VerifiedLive=false` |

22 skip là các test opt-in MariaDB/live/scale; không tính là pass. Build ở cấu
hình test riêng có warning nullable/analyzer từ các phần khác của repo; không
dùng kết quả đó để tuyên bố toàn repo sạch warning. Không sửa các phần không liên
quan chỉ để xóa warning. Lần thử compile chồng lên testhost đang chạy bị khóa DLL
trên Windows; đã chạy lại tuần tự/cấu hình riêng, không coi lỗi hạ tầng này là
regression sản phẩm hoặc bằng chứng đỏ của classifier.

## Preflight MariaDB và trình duyệt thật

Sau khi owner báo Docker đã mở, engine phục vụ bình thường. Agent không đổi
ACL/EFS, xóa IPC hoặc reset Docker/DB. Runtime hiện hữu được giữ lại.

Probe mở transaction **read-only** trên `tctenglish_dev` và gọi đúng các service
dry-run: 471 tài liệu, 487 chunk, 30 exclusion, snapshot
`s6466491683224781ace3ddbe570ff492`, trần UTF-8 113.887 byte, cache miss487,
ước lượng 27.610 token / 8 request. ActualPromptTokens0 / ApiCalls0; embedding
client chặn mọi lời gọi và đếm được 0. DB thật khớp snapshot; pending model false.
Dense vẫn `missing / manifest-missing`. Không bấm Build/Tạo vector. Ước lượng8
không thay replay admission10 POST nominal theo trần từng job.

Hai tài khoản Standard local `smoke` và `manual` được tạo bằng helper idempotent
có kiểm tra danh tính trước khi tái sử dụng. Mỗi tài khoản có bộ riêng hai thẻ
forecast/lighthouse, không publish. Không đổi admin/quyền, không sửa bộ owner,
không export user/private content vào shared package. Credential chỉ nằm ở file
ignored `.appdata/local/phase7-manual-test.json`.

Browser dùng tài khoản smoke, form/Enter thực, MariaDB thật, full chat và iframe.
Provider tạm là loopback không forward để loại mọi chi phí; counter cuối **0 POST,
0 paid calls**. Những ca quan sát:

- Chính câu tiếng Việt từng lỗi trả nghĩa dự báo, nguồn forecast.
- Phiên âm forecast và ví dụ đã lưu đúng; lighthouse trả ngọn hải đăng.
- Thiếu phiên âm lighthouse và cụm forecast accuracy báo thiếu căn cứ, không
  nhận một phần target hoặc tạo dữ kiện mới.
- Reading/Writing/Contact dẫn đúng route; danh sách bộ của smoke chỉ hiện một
  bộ riêng hai thẻ và nhãn số liệu tại thời điểm trả lời.
- Câu bóng đá không bịa tỷ số/người thắng. Yêu cầu xuất mật khẩu bị chặn trước
  generation bằng lỗi đầu vào; không tính lỗi đầu vào này là generated refusal.
- Reload hội thoại cuối giữ năm câu hỏi/đáp và nguồn forecast, không còn lời
  chào sai. Mở nguồn `/Home/Study/67` thấy bộ riêng và hai từ đúng.
- Iframe gửi câu tra nghĩa thật, có nguồn đúng; iframe rộng390px nằm trong
  viewport thực500px của browser, input/nút Gửi không chồng, không overflow ngang.
  Escape đóng modal và trả focus về nút mở. Console study/embed không có error.

Không lấy các ca này thay benchmark synthetic120 hoặc mẫu số answered62/held-out18.
Các ca an toàn đã kiểm ở Phase6 được giữ; lượt này không nhận rằng đã chạy lại
toàn bộ ma trận đổi quyền/cache của Phase6.

## Web bàn giao đang chạy

Lệnh chuẩn: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5199`.
Địa chỉ: http://localhost:5199/AI/Chat. Process app23344, MariaDB local được giữ
chạy. Chỉ process test web do lượt này sở hữu được restart; loopback fixture đã
được dừng sau khi ghi counter. App bàn giao dùng **User Secrets có sẵn**,
`gpt-6-luna` reasoning `low`; speech `gpt-transcribe`. Không in/copy/ghi lại key.

Browser bàn giao đăng nhập vào **Phase 7 local manual**, chat rỗng, trạng thái
idle, không có lịch sử smoke. Không gửi câu AI tự động sau khi khôi phục provider
bình thường. Tài khoản manual còn nguyên quota chat để owner thử. Có thể đăng
nhập bằng tài khoản riêng; dữ liệu thẻ sẽ theo đúng quyền tài khoản đó.

Câu mẫu và kết quả mong đợi: [local test questions](../../../operations/hybrid-rag-local-test-questions.md).
Standard vẫn 15 câu / 60.000 token/ngày UTC. Key configured chỉ chứng minh cấu
hình local, chưa chứng minh quyền model, hạn mức hoặc chất lượng câu trả lời live.

## Phần còn BLOCKED

Chưa có quyền chi cụ thể; chưa xây dense index bằng API thật, chưa seed/chạy
disposable synthetic benchmark đầy đủ, chưa đo Recall@5 / chất lượng 62 ca hay
20 lượt online. Các phần này vẫn **BLOCKED**, không APPROVED Phase7. Preflight
local MariaDB/browser B01 đã có bằng chứng; không còn bị chặn bởi Docker ở thời
điểm bàn giao. Không có Phase8. Local green không xác nhận CI hoặc production.

Evidence máy này nằm dưới ignored
`.tmp/hybrid-rag-review/phase-7/local-completion-round-2/`: red/green TRX,
`full-classifier-final.trx`, `node-final.txt`, `browser-final.json`,
`mariadb-dry-run-with-model-final.json`, `speech-status-final.txt`,
`runtime-final.json`, `launcher-owner-final.txt`. Không đưa log runtime hoặc
credential vào Git. Không có commit/push/remote CI/deploy trong lượt này.
