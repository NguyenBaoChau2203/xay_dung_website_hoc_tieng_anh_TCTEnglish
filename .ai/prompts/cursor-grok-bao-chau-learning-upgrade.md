# Cursor / Grok 4.7 High — Nâng cấp chức năng học của Nguyễn Bảo Châu

Bạn làm việc trong repository TCT English. Chủ nhóm muốn dùng **Grok 4.7 High** trong Cursor; tên này là lựa chọn của chủ nhóm, không phải yêu cầu gọi API/Grok từ bên trong ứng dụng. Không tự đổi model hoặc mở agent/chat/CLI AI khác. **Một agent tự điều tra, thực hiện, review và test.**

## Đọc đầy đủ trước khi sửa

1. `AGENTS.md`, `.ai/README.md`.
2. `docs/feature-audits/2026-10-29-bao-chau-learning-reliability-audit.md`.
3. `docs/superpowers/plans/2026-10-29-bao-chau-learning-reliability-plan.md`.
4. `docs/project-structure.md`, `.ai/context/coding-conventions.md`, `.ai/context/known-issues.md`, newest entries của `.ai/context/bug-fix-log.md`.
5. Chỉ skill liên quan trong `.agents/skills/`: `tct-superpowers`, `tct-engineering`; `tct-speaking-feature` cho Speaking, `tct-api-endpoint` khi đổi API, `tct-ef-migration` khi schema. Đọc DESIGN.md nếu cần sửa trạng thái trong view; không mở đợt redesign.

Plan có quyết định kỹ thuật và test case cụ thể. Báo cáo là bằng chứng khảo sát, không thay cho source hiện tại. Nếu source thay đổi, cập nhật ledger và điều chỉnh bước thực thi có bằng chứng trong cùng phạm vi; không tự mở rộng task.

## Chọn chế độ từ yêu cầu chủ nhóm

- **Chưa chỉ định lô triển khai:** chỉ làm **L0 preflight**, tạo/cập nhật `docs/feature-audits/bao-chau-learning-execution-ledger.md`, báo lô nên làm, rồi dừng. Đọc prompt không tự cho phép sửa app.
- **Chủ nhóm chỉ định “thực hiện L0 và L1”:** làm L0, sau đó đúng L1. Không làm L2–L7 hoặc sửa Program/config/schema.
- **Chủ nhóm duyệt lô khác:** đọc ma trận quyền của plan. Làm đúng lô, phụ thuộc và quyền được cấp; kiểm kết quả lô trước còn đúng. Không tự tick những phần chưa chạy.
- Đối với Program.cs/appsettings/.csproj, thay đổi hợp đồng API, migration/apply, cài engine/model và hạ tầng: quyền là theo phần cụ thể trong ma trận. Nếu còn thiếu quyền, hoàn thành phần độc lập có thể review trước, nêu chính xác thay đổi cần duyệt và dừng phần phụ thuộc. Không xin xác nhận lặp lại những gì đã được chủ nhóm cho phép rõ.

| Lô | Kết quả cần đạt |
| --- | --- |
| L0 | Preflight, baseline và ledger, source chưa đổi |
| L1 | Bỏ demo/điểm bịa, browser Speaking preview-only, phản hồi đọc thông báo đúng |
| L2 | Writing core nội bộ và needs-review, không chờ cloud |
| L3 | Notification ngày Việt Nam/dedup/paging/worker, giữ năm GoalArea |
| L4 | Thu thật + ASR nội bộ + assessment phía server, khóa score-only |
| L5 | Speaking import job có timecode thật, không đổi importer dùng chung |
| L6 | Atomic completion/credit, chịu crash/replay/concurrency |
| L7 | Grammar/local LLM Writing tùy chọn, mặc định off đến khi đủ bằng chứng |

## Phạm vi chức năng

Được xử lý: Speaking video/shadowing/dictation, Speaking theo bộ thẻ (chỉ chức năng nói, không đổi bộ thẻ), Writing người học, Goals, Notifications. Chức năng này được nâng cấp cả nghiệp vụ/dịch vụ/API/JS/tests theo lô, **khác** task UI-only của Antigravity.

Không sửa Auth/Profile/Settings, Contact, AI Chat/embed, Admin, Billing, Reading, Listening, Vocabulary/Set/Folder, Class/Chat. Không đổi AI Chat khi sửa thành phần AI nằm trong Writing. Chỉ đọc caller ngoài phạm vi để tránh regression.

Baseline lúc khảo sát ở `feature/bao-chau-ui-audit`, HEAD `3d27a68c8468207eca5a11653c72e735117fc637`, có **19 file UI/CSS sửa chưa commit**. Không checkout/reset/stash/clean hay format đè chúng. Chụp baseline diff mới trước làm. Nếu Antigravity đang sửa cùng view, không ghi đè; làm phần độc lập và báo đúng file/hunk cần phối hợp.

## Những điều không được hiểu sai

- Runtime hiện **net9.0 / EF9.0.4 / Pomelo MySQL**, không phải .NET10/SQLServer như một số tài liệu cũ.
- Goals source hiện có cả năm lĩnh vực; không tắt Reading/Listening theo backlog đã cũ.
- Source Speaking có demo/random score lưu thật và API nhận điểm client. L1 loại demo/điểm bịa và để browser score **preview-only**, tạm không ghi tiến độ/XP từ nhánh đó tới khi L4 có assessment server; phải nói rõ đánh đổi. L1 không tự khóa endpoint legacy, rủi ro giả điểm S02 vẫn mở cho tới L4.
- Web Speech không cần key nhưng có thể dùng nhận dạng trên mạng. Transcript edit distance chỉ là mức khớp từ, không đo fluency/prosody/phoneme chuẩn. Không thay điểm random bằng số cố định.
- Writing đã có fallback rule-based, nhưng thường chờ cloud trước và kiểm từ khóa còn yếu. L2 chọn core nội bộ: exact/đáp án đã duyệt mới accepted; câu có thể đúng khác cách diễn đạt thành needs-review. Không tự gọi là sai nghĩa, không auto-pass bằng overlap hoặc “không có lỗi grammar”.
- `IYoutubeTranscriptService` dùng chung với Listening/Admin; `IAiProviderClient` dùng chung với nhiều feature. Tạo adapter/options giới hạn theo Speaking/Writing, không đổi global để tiết kiệm vài dòng.
- Khảo sát chưa có log chứng minh key bị khóa vì nguyên nhân nào; không tự kết luận, đổi key hoặc né quota. Phân loại lỗi và đo latency bằng dữ liệu ẩn secrets.
- Tái sử dụng repo là lấy engine/thành phần và luồng đã kiểm chứng, vẫn cần adapter và test của TCT. Không thay hệ thống bằng full FastAPI/React/Electron app, không copy mọi content/model chỉ vì repo public.

## Chọn nguồn và cài đặt có kiểm soát

- ASR thử nghiệm chính: whisper.cpp **v1.9.5**, SHA `d1be6fde11ac6e0407606b4e42fe72d34add8037`, HTTP nội bộ; WAV mono PCM16 16kHz, `/inference`, `verbose_json`. Không public server ví dụ hoặc `/load`.
- Grammar tùy chọn: LanguageTool **v6.8**, SHA `e807fcde6a6506191e1470744d2345da28c26be6`, Java17, `/v2/check`; không thay bộ chấm nghĩa, không dùng public free endpoint.
- Tham khảo luồng: Ear2Finger SHA `675750c02c645e27a4fd00fd0dae4d2dfe4471ba`. Chỉ xem phần dictation/caption/history, không nhập AI coach/Qdrant/frontend/backend riêng.
- Các repo còn lại trong báo cáo là phương án thay thế/hoãn/không được copy; không cài hết. Tôn trọng license/NOTICE của code, model và artifact riêng.
- Ollama/local LLM là L7 tùy chọn, adapter Writing riêng, model/digest và RAM/CPU/GPU phải được duyệt/benchmark. Không đổi Gemini BaseUrl thành localhost rồi coi protocol tương thích.
- Chưa được tải model lớn/bật container/service/mở cổng khi chỉ đang preflight. Không auto-download model trong request học.

## Trình tự thực hiện mỗi lô

1. Ghi mục tiêu, phạm vi file và quyền được cấp vào ledger. Dùng CodeGraph trước tìm/đọc source; không tự index/sync. Bounded read cho file chưa được index/trả đầy đủ.
2. Kiểm hợp đồng thật, điểm gọi, source chưa commit, tests liên quan. Nêu lỗi cũ và failure mode của lô trước chỉnh.
3. Viết regression có ý nghĩa cho lỗi, chạy đỏ, triển khai tối thiểu, chạy xanh. Không xóa/skip test hoặc sửa assertion chỉ để khớp code mới. Những hành vi thay có chủ đích phải trỏ về quyết định trong plan.
4. Bảo toàn CSRF, currentUserId server-side, ownership/NotFound, Premium scope, JSON/routes/selectors. Snapshot câu đang làm khi async; không lưu nhầm câu từ callback cũ.
5. Test dịch vụ chết/không key/timeout/429/hủy request, concurrency/replay/crash theo lô. Không biến lỗi vận hành thành kết quả học sai, điểm fake hoặc success giả.
6. Review toàn bộ diff của mình inline, không đụng hunk UI sẵn có. Chạy build/relevant tests và kiểm browser/audio/DB/engine khi có môi trường hợp lệ; ghi phần chưa có bằng chứng.
7. Cập nhật ledger và bug memory cho bug thực sự đã sửa. Báo tiếng Việt rồi **dừng sau lô đã được yêu cầu**. Không commit/push/merge/deploy hoặc apply migration DB thật.

## Kiểm chứng và bàn giao

Lệnh gốc (tên solution phải đúng):

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity minimal
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~Speaking|FullyQualifiedName~Writing|FullyQualifiedName~Goals|FullyQualifiedName~Notification" --blame-hang-timeout 90s --logger "trx;LogFileName=learning-upgrade.trx" --results-directory .tmp/learning-reliability-upgrade/test-results --verbosity minimal
git diff --check
```

Chạy Node regressions được tạo ở lô tương ứng và full .NET suite trước bàn giao thay đổi dịch vụ dùng chung theo plan. `--no-build` chỉ sau build mới. Test nền khi khảo sát là 111/111; phải báo số thực tế của lần chạy mới.

Không dùng `dotnet run` trên cấu hình DB thật vì startup seeding. Không gửi audio/bài thật ra cloud để test, không in secrets. Không tuyên bố test MySQL/engine/mic pass nếu chỉ chạy SQLite/stub hoặc screenshot. Không tuyên bố app đã ít phụ thuộc bên ngoài chỉ vì thêm file config.

Kết thúc mỗi lô phải có: hành vi trước/sau, file đã thay, test và số pass/fail, thay đổi hợp đồng/quyền, phần đã kiểm trên browser/engine/DB, residual risk, điều kiện lô tiếp. Nếu chỉ L0, kết luận rõ **“Đã hoàn tất preflight; chưa sửa ứng dụng. Chờ chủ nhóm chọn lô triển khai.”**
