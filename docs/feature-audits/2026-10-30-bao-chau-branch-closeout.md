# Tổng kết nhánh và phần việc của Nguyễn Bảo Châu

Ngày: 30/10/2026 (UTC+7). Nhánh: `feature/bao-chau-ui-audit`.
Phạm vi: kiểm kê thay đổi hiện có, review nguồn/contract và bằng chứng kiểm thử,
commit theo nhóm, merge các PR đang mở và đồng bộ master. Một agent thực hiện.

## 1. Kết quả Git

- PR đang mở lúc bắt đầu chỉ có #121; đã merge vào master tại `b1e56a9`.
- Đã pull master vào nhánh feature bằng merge commit `a5e6592`, giữ lịch sử
  và các commit tính năng. Local master cũng fast-forward tới origin/master.
- Conflict duy nhất `.ai/README.md`: giữ cả quy định database/PR/release mới
  và mục Context routing. Tree sau merge bằng trước merge; không đổi mã ứng dụng.
- Đã push nhánh feature và thiết lập upstream. Các tính năng mới vẫn ở nhánh
  feature để owner tiếp tục làm; chúng không được đưa vào master qua PR #121.
- Không còn PR đang mở tại lần kiểm tra sau merge. Báo cáo và plan này được
  lưu thêm trong commit bàn giao cuối cùng.

| Commit nhóm | Nội dung |
| --- | --- |
| `26bd8c9` | 29 file công cụ/rule AI, context routing, policy CodeGraph incremental |
| `46f8648` | 15 file model/schema, bốn migration tháng 10 và snapshot/metadata |
| `fb37425` | 61 file logic học tập, engine adapters, completion/outbox/worker và tests |
| `f9b19eb` | 29 file UI, vòng đời ghi âm, phản hồi, import/poll và Node tests |
| `d355368` | 25 file audit, ledger, prompt, runbook và kế hoạch các đợt sửa |
| Commit chứa báo cáo này | Tổng kết đóng góp, kiểm chứng và bàn giao nhánh |

Snapshot riêng trước thao tác nằm trong `.appdata/local/branch-closeout-20261030`
(ignored), gồm 160 file và Git bundle. Đối chiếu cuối: 156 file giữ nguyên bytes,
hai tài liệu chỉ bỏ whitespace cuối dòng, hai thay đổi tài liệu có chủ đích
(plan mới và làm rõ migration import đã được test trên MariaDB riêng).
Không có thay đổi nội dung ngoài dự kiến; không đưa SQL/credential/runtime data
vào commit. Scan theo mẫu credential không thay thế rotation các secret cũ.

## 2. Những phần Châu đã xây dựng trước đợt này

Đối chiếu phân công trong README và lịch sử commit trên master, không suy ra
quyền tác giả từng dòng chỉ từ tên nhánh hoặc danh tính Git của commit mới.

| Phần việc | Bằng chứng lịch sử tiêu biểu |
| --- | --- |
| Tài khoản, xác thực, bảo mật, profile, SMTP/reset password | `7d2eac5` (12/03), `fdfb663` (14/04) |
| Speaking, dictation, lưu tiến độ, import video riêng cho Premium | `8af1d81` (10/03), `d951339` (13/04), `699f9a4` (16/05) |
| Writing, lưu tiến độ bền vững và sinh bài Premium | `a4b9b26` (06/04), `5df6528` (07/04), `31216b5` (12/04) |
| Goals, streak, XP và badges | `963ee39` (29/03), `062ab17` (04/04) |
| AI Chatbot, ML.NET intent và trang quản trị AI | `ba4f95f` (14/04) |
| Admin/người dùng, trạng thái hoạt động realtime | `23d3630`, `bd9804c` (13/03) |
| Kiến trúc feature, UI Stitch và nền CI/IIS | `9a18838` (20/03), `00941c7` (30/04), `0a08b96` (10/05) |

README phân công Notifications gốc cho Phú Trọng. Những thay đổi notification
trong nhánh này là cải thiện tích hợp độ tin cậy, không đổi phân công gốc.

## 3. Những cải thiện trong đợt 29–30/10

| Phần | Cải thiện đã có | Mức hoàn thiện được chứng minh |
| --- | --- | --- |
| Speaking | Bỏ điểm random/fallback lưu thật; Web Speech là preview; receipt phía server mới được lưu progress; kiểm tra WAV, ownership, timeout/queue/cancel; recorder gắn đúng câu | Contract/fixture và Node pass. Chưa xác minh engine ASR + mic/browser thật; chưa có chấm fluency/prosody hoàn chỉnh |
| Import Speaking | Caption-only cho learner, SRT/VTT upload, enqueue/poll, retry job cũ, fingerprint/hash, publish trong transaction và fencing | Tests pass; chưa nghiệm thu YouTube/network thật. ASR để tạo bài vẫn Deferred |
| Writing | Local policy mặc định, không chờ cloud; exact answer được Accepted, khác đáp án chuyển NeedsReview; giữ draft/attempt, deadline chung cho feedback tùy chọn | Core/contract tests pass. Không phải bộ chấm ngữ nghĩa đầy đủ; LanguageTool/Ollama và corpus chất lượng chưa nghiệm thu |
| Goals/XP | Completion và reward cùng transaction, credit key chống thưởng lặp; rollback khi lưu credit lỗi; nhận biết đúng chuyển tiếp streak/badge | Tests pass; chưa chứng minh race nhiều instance trên MariaDB |
| Notifications | EventKey chống trùng, paging tại DB, clock UTC+7, outbox, batch/checkpoint/lease/heartbeat; UI chỉ xác nhận đã đọc khi server thành công | Tests pass; migration thật pass trên bản sao, khác với kiểm thử concurrency/cluster |
| Account/AI/Admin UI | Điều chỉnh form quên/đặt lại mật khẩu, labels/focus/accessibility và trạng thái phản hồi ở các view liên quan | Build/render/DOM checks hỗ trợ; chưa nghiệm thu visual/responsive toàn bộ 25 view. Backend OAuth/SMTP/AI không được viết lại trong đợt này |
| Local/CI/CD | Docker MariaDB, User Secrets loopback, startup migration/seeding có gate, design-time factory, Linux test host ổn định, publish/bundle và Plesk HTTPS POST | Local dùng được; pipeline setup/master xanh. Production release vẫn tắt |
| Agent tooling | Rule tự đánh giá schema khi prompt thiếu; migration cùng PR; Cursor alwaysApply, Claude import; context gọn và CodeGraph opt-in theo checkout | Tooling checks pass; native trust/plugin/engine trên máy đồng đội vẫn cần xác nhận riêng |

## 4. Kiểm chứng của lượt chốt nhánh

- App và test project build thành công vào artifacts tạm; không có lỗi biên dịch.
- Full application suite theo layout chuẩn: **620 passed / 0 failed / 0 skipped**,
  2 phút 23 giây. TRX ignored: `.tmp/branch-closeout-tests/branch-closeout-standard.trx`.
- Lượt dùng `--artifacts-path` ban đầu build được nhưng test lookup source sai
  thành `.tmp/TCTEnglish`; đã chạy lại layout chuẩn, không sửa app để né lỗi lệnh.
  Binaries chuẩn được kiểm tra không cũ hơn bất kỳ file C# đang thay đổi nào.
- Python tooling/script tests **49/49**; Node behavior/setup/policy **36/36**.
- `agent_tooling.py`, staged validation hooks và `git diff --check`: pass.
- EF 9.0.4 `has-pending-model-changes`: không có thay đổi so với migration cuối.
- Bằng chứng MariaDB của phiên setup trước vẫn áp dụng cho các file migration
  không đổi: restore backup 26/09 và dump mới 30/10, upgrade bốn migration,
  giữ checksum sáu bảng, chạy lại không đổi history. Không apply hosting thật.
- CI master tại `b1e56a9`: run **41064290016** thành công, tooling **41064289973**
  thành công. Đây là master/setup, không phải Linux CI của toàn bộ feature mới.
- Local `/health/ready` trả ready. `PLESK_DEPLOYMENT_ENABLED=false`.
- Sau giải quyết merge, tree bằng `d355368`; chỉ thêm tài liệu bàn giao sau đó,
  nên không chạy lại 620 tests chỉ vì thay đổi tài liệu/ancestry.

## 5. Đã hoàn thiện chưa?

Đã có nền tảng chức năng và bằng chứng kiểm thử tốt hơn, đủ tiếp tục phát triển
local. Chưa đủ bằng chứng để kết luận toàn bộ sản phẩm/production hoàn thiện.

1. Speaking xác minh tiến độ cần kết nối và thử ASR thật; Web Speech preview
   không tự thay vai trò server. Cần microphone, đổi câu/hủy/timeout trên browser.
2. Kiểm tra chất lượng Writing/corpus, LanguageTool/Ollama nếu chọn bật; luồng
   sinh bài song ngữ và iframe/captions YouTube vẫn có phụ thuộc mạng/provider.
3. Kiểm tra race, lease recovery và nhiều process trên MariaDB; migration pass
   không chứng minh cluster behavior. Mở feature PR khi sẵn sàng để chạy Linux CI
   trên đủ 620 tests của nhánh thay vì dựa vào 563 tests của master hiện tại.
4. Nghiệm thu responsive/accessibility thực tế cho 25 view trong UI audit.
5. Rotate credentials từng có trong Git public, xác minh config mới và diễn tập
   release/failure recovery trên staging trước khi bật production gate. Lên kế
   hoạch nâng phiên bản .NET/MariaDB riêng theo lịch hỗ trợ.

Các review/ledger cũ giữ kết quả tại thời điểm viết. Dùng báo cáo này và runbook
hiện hành để đọc trạng thái mới; không coi mọi dòng Not run cũ là tình trạng hiện
tại, cũng không coi test xanh là đã nghiệm thu những môi trường còn thiếu.
