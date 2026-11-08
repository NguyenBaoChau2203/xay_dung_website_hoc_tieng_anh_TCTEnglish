# Phase 6 handoff — giao diện nguồn, cache và chịu lỗi

> Bản dưới đây ghi nhận lần nộp BLOCKED ban đầu. Lượt hoàn tất tiếp theo đã bổ sung
> browser và sửa lỗi; xem [handoff mới](local-completion-handoff.md) cùng
> [browser closeout](browser-closeout.md). Sau yêu cầu chốt mới nhất của owner,
> [review round1](review-round-1.md) duyệt snapshot153: Phase6 APPROVED, Phase7 READY.
> Các trạng thái BLOCKED dưới đây là lịch sử; Phase7 chưa được triển khai.

Phase 6 dừng ở **BLOCKED**. Bản triển khai cục bộ đã có, nhưng một hạng mục bắt buộc trên trình duyệt chưa được chạy. Phase 7 vẫn LOCKED. Báo cáo này không phải APPROVED, không phải CI và không phải bản phát hành.

## Phạm vi và danh tính

- Phase 6, vòng implementation. Prompt `.ai/prompts/hybrid-rag/phase-6-ui-resilience.md`. Plan `docs/superpowers/plans/hybrid-rag/phase-6-ui-resilience.md`. Một agent, IMPLEMENT_LOCAL.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối đều `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit, không push, không PR, không CI từ xa, không deploy.
- Trước khi sửa, manifest Phase 5 đã được kiểm lại: 143/143 file khớp, fingerprint `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
- Manifest hiện tại: [scope-manifest.json](scope-manifest.json). 151 mục. Fingerprint `0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`. Predecessor là fingerprint Phase 5 ở trên. Review tiền nhiệm: `docs/feature-audits/hybrid-rag/phase-5/review-round-3.md`.
- Báo cáo trình duyệt: [browser-report.md](browser-report.md).
- Cây làm việc bẩn từ các phase trước được giữ nguyên. File nằm ngoài digest vẫn là tập baseline đã ghi ở Phase 5, cộng thêm `docs/feature-audits/hybrid-rag/phase-5/review-round-3.md` (biên bản duyệt Phase 5, Phase 6 không sửa).

File mới trong digest:

- `TCTEnglish/ViewModels/AI/LearnerChatState.cs`
- `TCTEnglish/Services/AI/Rag/LearnerSourceLink.cs`
- `TCTEnglish/Services/AI/Rag/LearnerSourceDisplayCache.cs`
- `TCTEnglish/Services/AI/AiChatDiagnostics.cs`
- `TCTEnglish/Views/Ai/_ChatSources.cshtml`
- `TCTEnglish.Tests/AiRagPhase6UiResilienceTests.cs`
- `scripts/tests/test_ai_chat_phase6_sources.cjs`

File đã có trong digest Phase 5 và đổi hash ở Phase 6:

- `.ai/context/bug-fix-log.md`
- `TCTEnglish/Program.cs` — chỉ thêm singleton `ILearnerSourceDisplayCache`
- `TCTEnglish/Services/AI/AiChatService.cs`
- `TCTEnglish/Services/AI/Rag/AiChatAnswer.cs`
- `TCTEnglish/Services/AI/Rag/GroundedChatPipeline.cs`
- `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs`
- `TCTEnglish/ViewModels/AI/ChatGroundingDto.cs`
- `TCTEnglish/Views/Ai/_ChatShell.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`

File đưa vào digest lần đầu vì Phase 6 sửa nó, trạng thái `modified`:

- `TCTEnglish/wwwroot/css/ai-chat.css`

Quyền đã dùng: DI hẹp trong `TCTEnglish/Program.cs` cho cache hiển thị nguồn. Không sửa `appsettings.json`, `.csproj`, model AI, hay `IAiProviderClient`. Không migration mới. Không nút rebuild admin mới. Không gọi API trả phí. Khóa đã lưu không được in hay ghi đè. Loopback và khóa giả chỉ nằm trong tiến trình con.

## Kết quả quan sát được

Người học thấy nhãn trạng thái dùng chung giữa Razor và JSON: đã trả lời, cần làm rõ, chưa đủ căn cứ, ngoài phạm vi, trợ lý tạm thời không trả lời, nguồn đã thay đổi, số liệu tại thời điểm trả lời, tạm thời không xem được nguồn, chưa xác minh được nguồn. Tiêu đề nguồn được encode. Liên kết chỉ còn khi server chấp nhận đường dẫn cục bộ. `data-source-key` vẫn còn trên thẻ, chữ hiển thị không phải khóa nguồn.

Cache hiển thị tối đa 128 mục, TTL 2 phút, khóa theo người dùng, phạm vi truy cập, khóa nguồn và phiên bản nội dung. Cache không giữ thân câu trả lời. Lần chiếu lại vẫn kiểm tra quyền hiện tại. Từ chối truy cập thì quên bản hiển thị của người đó với nguồn đó.

Log hoàn tất giữ mã hội thoại, chế độ truy xuất, khóa nguồn, phiên bản và thời gian. Log bỏ nội dung prompt, thân nguồn, URL và khóa. Chi tiết vận hành không thêm bề mặt admin mới.

Trên trình duyệt cục bộ, hội thoại đầy đủ và embed đã gửi, tải lại và giữ cùng nhãn cùng liên kết Reading. Enter kép không tạo hai bong bóng. Stop trước khi có câu trả lời đã khôi phục bản nháp và không để lại chữ dang dở. 429 giả lập và lỗi mạng giả lập giữ bản nháp. Sau khi bỏ qua hàng `#typing`, thử lại không nhân đôi bong bóng người dùng. Tiêu đề dài và chuỗi giống HTML hiển thị thành chữ. Ảnh chụp nằm trong `.appdata/local/phase6-browser/`, không đưa vào Git.

Việc chưa chạy trên trình duyệt, và là lý do BLOCKED:

- Đổi tài khoản A sang B, hạ quyền, và gỡ thành viên lớp khi cache còn ấm. Máy cục bộ chỉ có một tài khoản trong file truy cập. Hạ admin duy nhất hoặc gỡ membership sẽ sửa MariaDB của chủ repo. Test SQLite `WarmCache_DoesNotLeakAcrossAccountDowngradeOrRevoke` đã qua.
- Thu hồi nguồn và dòng legacy NULL trên trang thật. Hai việc này có trong test C#, chưa có thao tác trình duyệt tương ứng.
- Trang lịch sử cũ nhiều trang bằng nút tải tin thật. JS `loadOlderMessages` đã chạy trong trình duyệt với fetch giả, và Node C4/D3 đã qua.
- 429, timeout và mất mạng của provider. Câu trả lời guide/thẻ không gọi provider. Loopback không nhận request nào. 429 và mất mạng đã được giả lập ở `window.fetch`.
- Vòng focus nhìn thấy được. Tab đưa focus tới `a.ai-chat-source-link`. Trong công cụ trình duyệt, phần tử không khớp `:focus` và outline tính được vẫn là `none`.

Những mục trên không được tính là đã qua trên trình duyệt.

## Sổ kiểm tra

| Kiểm tra | Lệnh hoặc thao tác | Môi trường | Kết quả | Bằng chứng |
| --- | --- | --- | --- | --- |
| Khớp Phase 5 trước khi sửa | Hash lại 143 mục | Local, không API | Khớp | Fingerprint `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4` |
| Test C# Phase 6 | `dotnet test` filter lớp `AiRagPhase6UiResilienceTests` | Debug, SQLite test host | 8/8 qua | Encode tiêu đề, nhãn, cache, hạ quyền, log |
| Bộ test tập trung | Filter AiRag, WebsiteGuide, InternalKnowledge, OpenAiConsumer, Cancel/Stop, History paging, Quota | Release, trước sửa JS/CSS cuối | 219 qua, 0 thất bại | JS/CSS sau đó không đổi assembly C# |
| Node chat | `node --test` submit guard, load older, phase 6 | Node, sau sửa JS cuối | 13 qua, 0 thất bại | Chạy lại lúc bàn giao |
| Model EF | `dotnet ef migrations has-pending-model-changes` với MariaDB local | Docker MariaDB 10.5.23 | Không có thay đổi model | Không migration mới |
| Whitespace | `git diff --check` | Working tree | Exit 0 | Không cảnh báo |
| Khởi động | `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` cổng 5186 | Release, BaseUrl con trỏ loopback | `/health/ready` 200 `{"status":"ready"}` | Kiểm lại lúc bàn giao |
| Speech | `scripts/speech-status.ps1` | Metadata kho khóa đã lưu | configured, VerifiedLive false | Không request giọng nói |
| Loopback | `loopback.cjs` trên `127.0.0.1:18086` | Trả 429 nếu có request, không ghi body hay khóa | Không có `requests.jsonl` | Lúc bàn giao cổng 18086 không còn listen |
| Trình duyệt UI | Xem browser-report.md | `http://localhost:5186`, admin cục bộ sẵn phiên | Một phần | Ảnh trong `.appdata/local/phase6-browser/` |
| A sang B, hạ quyền, gỡ lớp | Bắt buộc trên trình duyệt | Không chạy | BLOCKED | Chỉ có bằng chứng SQLite |

Ba lỗi JS đã sửa và ghi trong `.ai/context/bug-fix-log.md`: `adjustTextareaHeight` nằm sai scope, Stop bỏ qua khi chưa có stream id, và `#typing` làm thử lại nhân đôi bong bóng. Trước khi sửa, test F2 SignalR và thử lại trên trình duyệt thất bại theo đúng các triệu chứng đó. Sau khi sửa, Node 13/13 và lần thử lại trên embed giữ một bong bóng.

## Rủi ro cần reviewer nhìn

- Hàng ma trận “A→B, hạ quyền, gỡ thành viên, cache ấm” chưa có bằng chứng trình duyệt. Đây là hạng mục bắt buộc còn thiếu, không phải ghi chú phụ.
- Outline focus của liên kết nguồn chưa được xác nhận bằng pixel. Rule `a.ai-chat-source-link:focus` và `:focus-visible` đã có trong `ai-chat.css`.
- Hội thoại thử `1020019f-5da2-40d3-a0bb-efcbfee85a29` còn trên MariaDB local, gồm câu Reading, “phase6 double enter”, và lượt “phase6 stop browser”. Một lần thử lại trước khi sửa có thể đã lưu hai bong bóng “phase6 retry same”. Chưa xóa. Không reset database.
- `speech-status.ps1` đọc kho User Secrets đã lưu (BaseUrl OpenAI, model `gpt-transcribe`). Tiến trình web của lượt này dùng BaseUrl loopback và khóa giả trong biến môi trường con. Hai nguồn này khác nhau. Không có request trả phí.
- Phase 5 đã duyệt hành vi citation, lịch sử và hủy. Bộ test tập trung 219 ca vẫn qua trước sửa JS/CSS. Reviewer nên đọc `ai-chat.js`, `_ChatSources.cshtml`, `LearnerSourceDisplayCache.cs`, `GroundingCitationService.cs` và `AiChatDiagnostics.cs`.

Tái hiện tối thiểu trên máy này: mở `http://localhost:5186/AI/Chat` bằng phiên admin cục bộ, gửi một quick action, tải lại, rồi mở cùng `conversationId` với `embed=true`. Đổi tài khoản cần một tài khoản thứ hai do chủ repo cấp, hoặc một database dùng một lần. Không hạ admin duy nhất trên Docker `tctenglish_dev`.

## Trạng thái cuối

Phase 6: **BLOCKED**. Phase 7: **LOCKED**. Không tự duyệt.

Để gỡ BLOCKED cần bằng chứng trình duyệt cho đổi tài khoản, hạ quyền và gỡ thành viên khi cache còn ấm, trên database không phải bản admin duy nhất của chủ repo. Sau đó mới đặt lại AWAITING_REVIEW.

Đoạn dán cho Codex:

```
Phase 6 đang BLOCKED, chưa được duyệt. Đừng APPROVED và đừng mở Phase 7.
Nhánh feature/hybrid-rag-chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69.
Fingerprint nộp: 0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae (151 mục).
Predecessor Phase 5: 33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4.
Handoff: docs/feature-audits/hybrid-rag/phase-6/handoff.md
Thiếu bằng chứng trình duyệt: đổi tài khoản A→B, hạ quyền, gỡ thành viên lớp với cache ấm.
Test SQLite WarmCache_DoesNotLeakAcrossAccountDowngradeOrRevoke đã qua. Việc đó không thay cho hàng ma trận trình duyệt.
Không commit, không push, không deploy, không gọi API trả phí.
```
