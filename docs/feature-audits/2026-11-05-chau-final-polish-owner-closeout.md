# Châu final polish — review và hoàn tất local 04–05/11/2026

Owner cấp quyền review +fix phần còn lại; một agent thực hiện inline. Nhánh
`fix/local-launcher-preserve-user-secrets`; giữ toàn bộ worktree hiện có. Không
commit/push/PR/merge/remote CI/deploy hoặc thay đổi Plesk/gate trong lượt này.

## Kết luận

Fix lịch sử Writing của Cursor đúng: `ResolveHistoricalStatusLabel` dùng nguồn
đánh giá để giữ “Cần xem lại” cho NeedsReview thay vì nhãn nhị phân “Cần sửa”.
Các finding R1–R5/RR1/RR2/C1.1 đã được đóng ở các review trước, không làm lại.

Reviewer tìm và sửa thêm các lỗi dưới đây. Không còn P0/P1 được tái hiện trong
phạm vi kiểm tra. Implementation/regression local sẵn sàng cho owner test giao
diện/chức năng tiếp. Không gọi mọi case trong B6 là đã thao tác thủ công; mic/API
thật và broad acceptance ngoài các dòng evidence bên dưới vẫn là bước riêng.

| Lỗi | Nguyên nhân và sửa | Bằng chứng |
| --- | --- | --- |
| Speaking chưa chọn câu khi YouTube tải chậm; callback muộn reset lựa chọn |Khởi tạo câu ngay trong init; onReady chỉ chọn nếu chưa có câu |2test production JS red→green; browser chặn iframe_api vẫn chọn/đổi câu được |
| Toast Speaking tràn mobile |Giới hạn chiều rộng theo viewport, cho xuống dòng |Browser390px khi toast hiện, rect nằm trong viewport sau layout; ảnh đã xem |
| NoSpeech mất thông báo sau render điểm |updateScoreBoard ghi đè status; đặt thông báo sau render |Regression thật fail trước sửa; browser hiển thị “Chưa nhận được lời nói.”, không POST progress |
| Goals Writing1/2 nhưng tổng0% |Summary chỉ lấy Vocabulary; lấy trung bình phần trăm các target>0, cap từng mục tiêu theo logic cũ |3case red→green: Writing50%, vượt target100%, đa kỹ năng25%; không cộng các đơn vị khác nhau |
| Form sửa goal lấy nhầm target |Area và target không cùng goal; lấy cả hai từ mục tiêu dương đầu tiên |Integration +browser Writing target2; target0 không làm loãng summary |
| Biểu đồ tuần nói chưa có hoạt động dù vừa Writing |Dữ liệu biểu đồ chỉ CardsReviewed |Ghi rõ “Ôn từ vựng hàng tuần”; không thay schema hoặc hệ thống điểm |

Goals thay đổi logic đọc/ViewModel; không thay entity/mapping hoặc schema.
Không cần migration mới cho các sửa ở lượt này. Migration accepted-answers đã
có trong worktree cùng Designer/snapshot; provider evidence tham chiếu báo cáo
[code acceptance trước](2026-11-04-chau-final-polish-code-acceptance.md).

## Browser và dữ liệu synthetic

Browser Chrome thật qua Playwright runtime đã có sẵn; không cài framework mới.
Tài khoản Admin/Standard/Premium riêng trong ignored `.appdata/local/uat-access.json`.
Không in password/key/cookie/CSRF token trong evidence. Dùng DB Docker local,
không reset volume hoặc sửa nội dung của owner.

| Kiểm tra | Kết quả và giới hạn |
| --- | --- |
|60tổ hợp:3roles ×390/1440 ×10routes |Rendered200/403 đúng. Home,Profile,Settings,Goals,Writing23,Speaking55,Notifications,AI full/embed,Admin Edit23. Đây là route/layout smoke |
| Admin2tab synthetic23 |A thêm Hello there;B form cũ bị conflict, giữ Greetings đang nhập. ReloadA và gỡ variant tạm, giữ Hi! ban đầu |
| Writing approved/canonical/unapproved |Submit UI thật: Hi!→Accepted/local-approved-variant; I like tea→Accepted/local-exact; paraphrase chưa duyệt→NeedsReview, reload giữ “Cần xem lại” |
| Draft cùng browser A→B |Standard lưu wp_draft_u66;logout→Premium: không khôi phục draft Standard, draft gốc vẫn còn. Không chỉ dùng context tách biệt |
| Goal modal |Lưu Writing2; Escape đóng và trả focus về nút mở |
| Completion→Goals/Badges |Writing23 hoàn tất04/11; Writing24 synthetic hoàn tất05/11:1/2,50%,streak2,3/12badges. Qua nửa đêm0/2 là reset ngày đúng, không phải mất progress |
| Replay |Gửi lại Writing24 vẫn1/2,50%,3/12badges; dedup/reward/receipt có thêm integration coverage |
| Notifications |Trang đúng /Notification/Index có thông báo badge/streak; missingCSRF400, Premium sửa notification Standard404; mark-all từ5→0 |
| AI full/embed |/AI/Chat và embed là trang thật, không N/A. Fake503 +chặn hub negotiate: thông báo lỗi và cho gửi tiếp; embed390px không overflow |
| Speaking |Browser recorder/ASR/progress **giả lập**, không mic/cloud thật: chips4loại, literal<b> không tạo HTML; NoSpeech không commit; mic denied không assess thêm. Nghe/video media bị chặn trong probe tải chậm |
| Speaking concurrency/cancel/late/limits |Production JS harness +.NET integration; không tuyên bố mọi nhánh này đã tự thao tác bằng mic |

Synthetic exercise24 tạo qua Admin UI, title `UAT Synthetic Daily Goal 2026-11-05`,
hai câu “Xin chào. Tôi thích trà.”/“Hello. I like tea.”. Exercise23 giữ demo variant.
Các mutation chỉ tài khoản/fixture UAT; không đưa DB rows, backup hoặc account file
vào Git. Demo Speaking55+44 giữ theo manifest;53 không demo-approved.

## Checks chạy mới

| Check | Kết quả |
| --- | --- |
| Full .NET trước các lỗi mới |926pass/0fail/18skip/944total |
| Goals focused sau sửa |51pass/0fail/0skip |
| Full .NET Release sau sửa cuối |**929pass/0fail/18skip/947total**,3m09s |
| Node toàn bộ test_*.cjs sau sửa cuối |**58pass/0fail/0skip** |
| Writing focused |46pass/0fail |
| Launcher/key focused Python (discover đúng test root) |7+14=**21pass/0fail** |
| Secret pattern scan cuối trên73file changed/untracked được Git xét |Không phát hiện OpenAI key/private-key header theo pattern; không thay thế mọi loại security audit |
| git diff --check |Đạt |

Full test command: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
--no-build --no-restore --logger "trx;LogFileName=owner-final-after-fixes.trx"
--results-directory .tmp/final-uat-owner/testresults`. Release test assembly được
build trong focused run ngay trước đó. Node chạy tất cả `scripts/tests/test_*.cjs`.
18skip là các test opt-in có điều kiện môi trường/provider/live; không tính thành
pass. MariaDB upgrade/repeat/concurrency/guard đã có1+1+4pass ở review trước,
không chạy lại baseline trên target đã nâng cấp và không gọi đó là run mới.
Python chạy `uv run --no-project python -m unittest discover -s scripts/tests
-p test_local_launcher.py -v` và pattern `test_local_ai_config.py`. Attempt đầu
dùng dotted module từ repo root thiếu `pwsh_util` import path; chưa chạy tests,
đã sửa invocation sang discover và chạy đủ21pass, không sửa code để né lỗi.

Evidence ignored: `.tmp/final-uat-owner/{routes-matrix.json,testresults,
goals-red.log,goals-green-release.log,nospeech-red.log,node-final-tests.log,
full-final-tests.log}`; ảnh Admin stale, Speaking mobile/word comparison và
Goals mobile; `owner-journeys.json` lưu kết quả đã kiểm, không có credential.
Các screenshot chính đã được xem; không chỉ lưu file rồi gọi QA.

## Runtime và phần còn cần owner nghiệm thu

Web mới tại http://localhost:5100 qua launcher chuẩn, MariaDB healthy, readiness200.
Key lấy tự động từ User Secrets, speech-status configured/ConfigurationValid,
VerifiedLive=false. Writing dùng Local/cloud=false theo defaults đã kiểm: User
Secrets không có override Learning:Writing và config Development không đổi các
option đó. Invocation thử UAT dùng prefix WritingEvaluation không bind vào
Learning:Writing; không dùng nó làm mẫu cấu hình. Prefix đúng cho override là
Learning__Writing__*. Không sửa kho key hoặc config Git. Lần startup thường tiếp
theo vẫn theo cấu hình key/options của owner. Không có local Whisper.

Một số attempts build đầu bị apphost/dll đang dùng; không tính là app test fail.
Release dùng cho kiểm thử để giữ app đang mở; sau đó chỉ dừng đúng process Debug
do task tạo, kiểm Path/PID, và khởi động lại để nhận code Goals mới. Sau interruption
Docker từng kẹt starting/timeout60s, đã tự lên và retry launcher thành công.
Không reset Docker/WSL/volume hoặc dừng stack khác để xử lý.

Cold launcher→ready ở measurement trước khoảng105s trên máy thiếu RAM, warm
ready113–163ms; không dùng warm HTTP để gọi startup nhanh hoặc hứa mức tăng tốc.

Còn cần owner nghiệm thu: mic thật, chất lượng ASR/accent, paid OpenAI text/audio
và account/quota; các luồng OAuth/SMTP/payment thật; broad toàn bộ B6 theo thực tế
demo nếu muốn ký nghiệm thu sản phẩm. Không phát hiện các lỗi code trên để chuyển
lại Cursor sửa. Không đánh dấu live/production acceptance hoàn tất.

## Database dùng chung

Owner chọn dev/staging chung, chưa có máy chủ và cần đề xuất. Đã thêm
[database-collaboration](../operations/database-collaboration.md) +link onboarding:
Git chuyển code/migration/seed đã review, không tự sync rows Docker. Mục tiêu,
XP và huy hiệu đã nhận thuộc user, cả trên DB chung. Chưa cấp phát endpoint,
DNS hoặc workflow staging. Đề xuất kiểm hạn mức Plesk N518 trước, nếu không đủ
thì VPS4GB cho web+DB staging riêng; giữ cổng DB trong mạng riêng và production
gate như cũ. Giá/nguồn chính thức và checklist đã ghi trong runbook.
