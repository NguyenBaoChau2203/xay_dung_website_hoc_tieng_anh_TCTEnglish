# Review nghiệm thu phần việc 29–30/10

Ngày: 30/10/2026, UTC+7. Nhánh `feature/bao-chau-ui-audit`, HEAD `54f0c14`.
Một agent review. Không sửa mã ứng dụng, schema, config hoặc workflow; không
commit/push/deploy trong lượt review này. Chỉ tạo review, plan, prompt và probe
ignored. Các file marker thử local đã được dọn sau khi kiểm tra.

## Kết luận

**Chưa hoàn tất nghiệm thu.** Có nhiều phần đã triển khai và kiểm thử tốt, nhưng
review này xác nhận bốn vấn đề cụ thể và các cổng môi trường chưa đóng. Không
thể dùng kết quả 620 tests xanh để kết luận đã hoàn thiện Writing, ASR và CD.

Đây là review độc lập sau báo cáo closeout, không phải yêu cầu làm lại L0–L7 hay
quay lại các lỗi RR01–RR07 đã được sửa. Các test mới dưới đây là đề xuất regression,
chưa có trong test project ở HEAD này.

## REV01 — P1: Writing auto-pass khi dấu làm thay đổi giá trị/nghĩa bị xóa

Vị trí: `TCTEnglish/Services/Writing/WritingAssessmentPolicy.cs:127`, đặc biệt
dòng 134; nhánh Accepted tại dòng 65. Caller:
`TCTEnglish/Services/WritingService.cs:197`, persistence/reward từ dòng 231.

`NormalizeForComparison` đổi mọi ký tự không phải chữ/số/whitespace thành dấu
cách. Các dấu âm, tiền tệ và phần trăm biến mất; hai câu khác nghĩa trở thành
khớp chính xác. Probe gọi trực tiếp assembly hiện tại cho kết quả:

| Tham chiếu | Câu gửi | Kết quả hiện tại |
| --- | --- | --- |
| `The temperature is -5 degrees.` | `The temperature is 5 degrees.` | Accepted, passed=true |
| `I paid $50.` | `I paid €50.` | Accepted, passed=true |
| `The discount is 5%.` | `The discount is 5.` | Accepted, passed=true |

Đây là tái hiện policy thật, không phải mô phỏng thuật toán. Đã đối chiếu call
chain: service dùng quyết định này để lưu attempt/progress và cấp completion
credit khi đủ điều kiện. Lượt review không gửi các câu này vào endpoint/DB thật;
regression ở endpoint và XP cần bổ sung trong bước sửa.

Sửa: chuẩn hóa bảo toàn ý nghĩa số/dấu/đơn vị. Trường hợp không chứng minh được
tương đương phải NeedsReview/NeedsRevision, không auto-pass. Giữ formatting và
biến thể đã được duyệt; không thay bằng keyword coverage hoặc LLM quyết định XP.

## REV02 — P1: Public release marker được ghi ở vị trí ứng dụng không phục vụ

Vị trí: `scripts/deploy-plesk.ps1:160`; consumer:
`scripts/publish-plesk-release.py:68`. Middleware:
`TCTEnglish/Program.cs:235`; IIS wildcard: `TCTEnglish/web.config:6`.

Script ghi `<SiteRoot>/tct-release.json`, trong khi `UseStaticFiles()` dùng
`wwwroot`. Không có route riêng phục vụ marker tại content root. Probe trên web
local đang chạy tạo marker đúng vị trí script và một file đối chứng trong wwwroot:

- `/tct-release.json`: **404** dù file ở content root tồn tại.
- File JSON đối chứng trong wwwroot: **200**, body đúng.
- web.config đã publish dùng handler `path="*"`/AspNetCoreModuleV2.

Hai file probe đã xóa; không chạm hosting. Kết quả chứng minh mismatch local và
cấu hình publish; chưa phải một lần deploy IIS production để đo lỗi end-to-end.
Hệ quả dự kiến: Plesk có thể cập nhật app và readiness thành công, nhưng publisher
không đọc được commit marker rồi báo release thất bại.

Sửa: thống nhất đường ghi/đường HTTP, metadata công khai tối thiểu, cache policy,
thời điểm ghi thành công và cách giữ/khôi phục marker khi release thất bại.
Không mở truy cập toàn content root để chữa một file marker.

## REV03 — P2: ASR parser không phân biệt schema lỗi và không có lời nói

Vị trí: `TCTEnglish/Services/Speaking/WhisperHttpTranscriber.cs:144`;
GetString/TryGetDouble dòng 146–157; catch dòng 98 chỉ bắt JsonException.

Probe dùng adapter thật + HttpMessageHandler fixture, không gọi engine/mạng:

| HTTP 200 body | Kết quả hiện tại |
| --- | --- |
| `{"text":123}` | Ném InvalidOperationException |
| `{"text":"Good morning","segments":[{"start":"bad","end":1,"text":"Good morning"}]}` | Ném InvalidOperationException |
| `{"error":"engine failure"}` | NoSpeech / no_speech |
| `{}` | NoSpeech / no_speech |

Sai kiểu JSON có thể lọt ra endpoint thành lỗi 500; schema lỗi lại có thể bị
hiển thị như người học không nói. Đây không phải bằng chứng engine whisper.cpp
thật luôn trả các body này; đây là lỗi xử lý response sai/misconfigured proxy.

Sửa: validate ValueKind/schema và segment trước khi truy cập; malformed response
thành Unavailable/invalid_schema. Chỉ NoSpeech với response đúng contract nhưng
transcript rỗng. Giữ cancellation, deadline tổng và nhả slot cả khi parse lỗi.

## REV04 — P2: Publisher chưa hỗ trợ rerun sau khi release đã được tạo

Vị trí: `scripts/publish-plesk-release.py:57`–64. Tag lấy từ RUN_ID + SHA và
luôn gọi `gh release create`; không có nhánh kiểm tra/reuse release/asset cũ.

Theo [GitHub variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables),
RUN_ID và RUN_NUMBER không đổi khi rerun; RUN_ATTEMPT tăng. Fixture offline mô
phỏng lần 1 tạo release rồi webhook lỗi, lần 2 cùng run có tag đã tồn tại:
lần 2 dừng ở create và chưa tới webhook. Không tạo release thật để thử.

Sửa: định nghĩa rerun idempotent và đối chiếu đúng commit/digest/asset. Không chỉ
gắn thêm RUN_ATTEMPT hay xóa tag để vượt qua vì Plesk còn guard run/attempt riêng.
Trường hợp đã migration một phần phải báo operator recovery, không blind retry.
`gh release create` gồm nhiều API request, không phải giao dịch nguyên tử theo
[GitHub CLI](https://cli.github.com/manual/gh_release_create).

## Những phần đã có bằng chứng và cần giữ

- Speaking receipt/ownership/CSRF, Web Speech preview-only, WAV validation,
  timeout/limiter và recorder lifecycle đã có implementation/tests.
- Writing Local, attempt/draft, NeedsReview và default-off optional feedback đã
  có; REV01 là lỗ hổng chuẩn hóa cụ thể, không phải lý do bỏ thiết kế Local.
- Completion transaction + credit/outbox; notification semantic keys, paging,
  clock UTC+7, conditional claim/fencing/checkpoint/renewal đã có source/tests.
- Import có retry/poll/subtitle/source hash và fenced publish transaction.
- Docker dev/User Secrets, startup gates, EF factory, migration upgrade/checksum
  trên bản sao và CI/Plesk bootstrap đã kiểm chứng trong phiên trước.
- Rule Cursor/Claude/AGENTS đã ghi. Không chỉnh toolkit/MCP/model routing để sửa
  lỗi nghiệp vụ. Giới hạn model Cursor lần tới do owner đặt: Auto, Grok/Composer 2.5.

## Các cổng nghiệm thu chưa đóng (không gán thành lỗi đã tái hiện)

| Cổng | Còn thiếu | Điều kiện khép |
| --- | --- | --- |
| G01 ASR thực tế | Engine/model identity, WAV thật, cold/warm latency, mic/browser | E2E WAV → assessment → progress/credit, silence/unavailable/cancel đúng |
| G02 Writing quality | Corpus có số/dấu/nghĩa khác và optional engines thật | Corpus ít nhất 40 ca, nhãn kỳ vọng, không false auto-pass; optional-off vẫn chạy |
| G03 MariaDB concurrency | Nhiều connection/worker, lease expiry/crash, outbox race | Fixture MariaDB riêng, barrier/fault injection; đủ row, không thưởng/trùng/mất |
| G04 UI 25 view | Visual/responsive/keyboard/browser acceptance | Matrix 25 view có evidence desktop/mobile, không đánh dấu screenshot giả |
| G05 CI feature | Linux CI ở SHA chứa đủ feature + các bản sửa | Full suite mới trên đúng SHA; không dùng 563 tests master thay cho feature |
| G06 Production | Credential rotation, staging release/failure/restore drill | Owner/provider xác nhận; gate vẫn false cho tới khi có bằng chứng và chỉ định |

Docker ASR trên máy dev không tự trở thành endpoint mà Windows hosting Nhân Hòa
truy cập được. Sự tồn tại của adapter không chứng minh gói hosting chạy được engine.
Grammar/LLM tùy chọn không phải điều kiện để core Local ghi nhận câu khớp chuẩn;
ASR dùng để tạo bài và fluency/prosody không được tự thêm thành scope bắt buộc mới.

## Kiểm chứng và giới hạn

- Release app build mới: **0 lỗi, 35 cảnh báo sẵn có**, 30,76 giây.
- Focused WritingLocalAssessment/SpeakingAssessment/SpeakingInferenceLimiter:
  **20 pass, 0 fail, 0 skip**. Deploy guards: **8/8 pass**.
- Counterexample probes ở REV01–03 tái hiện các lỗi dù các test cũ vẫn xanh.
- Full 620/620 của lượt closeout trước là bằng chứng lịch sử trên cùng tree mã
  ứng dụng; lượt này không chạy lại toàn bộ khi chưa sửa code.
- Probe/outputs ignored: `.tmp/review-2930/`; không phải regression tests đã commit.
- Không thử mic/YouTube/engine thật, cluster MariaDB hay production release trong
  lượt review. Không đổi PLESK_DEPLOYMENT_ENABLED (đã kiểm tra false).

Plan: [thực thi nghiệm thu](../superpowers/plans/2026-10-30-final-acceptance-remediation-plan.md).
Prompt: [Cursor Auto](../../.ai/prompts/cursor-auto-final-acceptance-remediation.md).
