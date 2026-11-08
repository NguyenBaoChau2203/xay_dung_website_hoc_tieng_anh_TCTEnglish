# Phase 5 sửa vòng 2 — envelope thiếu sources và hạn chiếu sau commit

## Phạm vi

- Phase 5, sửa vòng 2. Prompt `.ai/prompts/hybrid-rag/phase-5-fix-round-2.md`. Plan `docs/superpowers/plans/hybrid-rag/phase-5-fix-round-2.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối đều `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, manifest 140 mục khớp 140/140, fingerprint `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`. `predecessorFingerprint` của bản đó là snapshot Phase 5 đã review `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`. Trường `predecessorManifest` khi đó vẫn trỏ `phase-4/scope-manifest.json`.
- F01, F03 và F04 đã được review đóng được giữ. Vòng này xử lý phần còn của F02, F05 và F06.
- Không thêm cột, không thêm migration, không sửa `20261107043157_AddAiMessageGroundingMetadata`. Không đổi `Program.cs`, `appsettings.json`, `.csproj`, provider hay credential.
- Không gọi API trả phí, không push, không PR, không CI, không deploy. Provider trong test là fake.
- Manifest mới: [scope-manifest.json](scope-manifest.json), 143 mục, fingerprint `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`. `predecessorFingerprint` là fingerprint 140 mục ở trên. `predecessorReview` là [review vòng 2](review-round-2.md), artifact đã xác nhận snapshot đó. Không còn đường dẫn manifest Phase 4.
- Phase 6 và Phase 7 vẫn LOCKED. Báo cáo này không tự duyệt.

## Đánh giá schema

Ba việc sửa không đổi entity, DbContext hay mapping. `dotnet ef migrations has-pending-model-changes` trên binary Debug cuối báo không có thay đổi model, exit 0.

## Hành vi đã sửa

Envelope phiên bản 1 phải có đúng một mảng `sources`. Tên field theo chính sách không phân biệt hoa thường của serializer. Thiếu field, `sources` null, hoặc không phải mảng thì metadata không xác minh được: màn hình giữ câu đã lưu với `status` `unverified` và không có nguồn; ngữ cảnh provider nhận `Không xác minh được nguồn của câu trả lời này. Không dùng nội dung cũ làm dữ liệu hiện tại.` Parser vẫn chỉ nuốt `JsonException`.

`sources: []` viết rõ, kể cả tên `Sources`, với outcome không cần nguồn vẫn giữ câu và `status` `current`. Metadata `NULL` vẫn là dòng cũ: giữ câu, `Grounding` null.

Sau khi assistant và quota đã commit, chiếu màn hình dùng token riêng 5 giây. Đây là cùng ngân sách sau chấp nhận đã dùng cho lưu dòng và chốt quota, vì chiếu chỉ kiểm lại tối đa 12 nguồn. Hết hạn, hủy hoặc lỗi trả thông báo chưa xác minh, giữ một assistant, một envelope và một lần settle. Lệnh `await` kết thúc trước khi scope và lease hội thoại được thả, nên không để tác vụ tiếp tục trên DbContext đã dispose. Stream gọi `SendAsync`, nên completion dùng cùng hạn. Tải lại sau đó kiểm tra quyền trên câu đã lưu.

## Sổ kiểm tra

| Kiểm tra | Lệnh | Môi trường | Kết quả |
| --- | --- | --- | --- |
| Manifest vào vòng sửa | SHA-256 từng file rồi fingerprint thứ tự path | Local, không API | 140/140, `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683` |
| Regression đỏ | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug --filter FullyQualifiedName~AiRagPhase5FixRound2Tests` | SQLite, fake provider | Failed 5, Passed 5, Skipped 0, Total 10. Đỏ đúng: hai envelope thiếu `sources`, token chiếu không hủy được ở send và stream. `sources` null/object, mảng rỗng và legacy NULL đã xanh |
| Trang cũ sau khi sửa cursor test | Cùng filter, một test HTTP | SQLite | Lần đầu trang cũ rỗng vì test đổi mốc thời gian sang giờ máy. Sau khi gửi nguyên chuỗi UTC của API, assertion sản phẩm đỏ: `status` `current` thay vì `unverified` |
| Regression xanh | Cùng filter sau khi sửa | SQLite, fake provider | 10 passed, 0 failed, 29 s |
| Focused | `dotnet test ... -c Debug --filter FullyQualifiedName~AiRagPhase5\|FullyQualifiedName~AiRagPhase4\|FullyQualifiedName~AiCancelAndStop\|FullyQualifiedName~AiQuota\|FullyQualifiedName~OpenAiConsumerPipeline` | SQLite, fake provider | Passed 80, Skipped 2, Failed 0, Total 82, 39 s |
| Node chat | `node --test scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs` | Node, không API | 7 passed, 0 failed. JS không đổi; chạy lại để xác nhận |
| Pending model | `dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext --configuration Debug --no-build` | EF 9.0.4, disposable `127.0.0.1:33324` | `No changes have been made to the model since the last migration.` exit 0 |
| Nâng schema disposable | DB rỗng, `lower_case_table_names=1`, nâng tới `20260510020134_AddReadingUserTranslation`, chèn user 900001, nâng tới `20261106181456_AddAiQuotaTurnStage`, chèn hội thoại và câu legacy, nâng hết, lặp lại | MariaDB `10.5.23-MariaDB-1:10.5.23+maria~ubu2004`, image `mariadb:10.5.23`, digest `sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730`, container `tct-migration-check-phase5r2` đã xóa, DB `tct_migration_check`, client `127.0.0.1:33324` | 15/15 migration. Checksum `users` `2336846340` trước, giữa và sau. Câu `22222222-2222-2222-2222-222222222222`, hội thoại `11111111-1111-1111-1111-111111111111`, user 900001, role 2, content `legacy-answer-body`, `GroundingMetadata` NULL. Cột `longtext`, nullable, không index. FK `FK_AiMessages_AiConversations` còn, `DELETE CASCADE`. Lần update thứ hai không đổi history |
| Full suite | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug` | Local Debug, không biến `TCT_ACCEPTANCE_MARIADB`, không API | 1128 passed, 22 skipped, 0 failed, total 1150, 3 m 38 s |

Hai skip của bộ focused và 22 skip của full suite là fact MariaDB hoặc live opt-in. Chúng không được tính là pass. DB dev cổng 3307 không bị dùng. Container disposable và file mật khẩu tạm đã xóa.

Chưa mở trình duyệt. Phase 6 vẫn là cổng kiểm tra giao diện.

## Rủi ro cho reviewer

- Envelope v1 thiếu `sources` giờ là chưa xác minh. Dòng đã lưu không bị viết lại. Màn hình vẫn hiện câu cũ; chỉ ngữ cảnh provider bị thay.
- `sources: []` hợp lệ vẫn là câu không nguồn. Đừng gộp trường hợp này với field bị thiếu.
- Hạn 5 giây có thể trả thông báo chưa xác minh khi kiểm quyền còn chậm. Câu đã lưu vẫn còn để tải lại. Hạn này không phải SLA lấy từ cửa sổ quan sát 7 giây của probe review.
- Send và stream dùng cùng hạn vì stream gọi `SendAsync`. Không có retry HTTP mới.
- Không có manifest 137 hay 140 mục bất biến trên đĩa. Tiền nhiệm được ghi bằng fingerprint 140 mục và đường dẫn review vòng 2, không trỏ file manifest đang bị ghi đè và không trỏ manifest Phase 4.

## Trạng thái

Phase 5 là AWAITING_REVIEW. Phase 6 và Phase 7 vẫn LOCKED. Kiểm tra ở trên là local. CI chưa chạy. Production chưa deploy. Báo cáo này không phải APPROVED.

Đoạn dán cho Codex:

Phase 5 sửa vòng 2 đang AWAITING_REVIEW. Scope 143 mục, fingerprint 33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4. Predecessor là snapshot đã review 140/140, fingerprint 6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683, tham chiếu review-round-2.md. HEAD vẫn 6af768bd32d16cc6d012d484ee82df4cab805c69. F02 còn lại, F05 hạn 5 giây và F06 đường dẫn tiền nhiệm đã sửa local. Regression đỏ 5 failed / 5 passed, sau đó 10/10. Focused 80 passed, 2 skipped, 0 failed. Full suite 1128 passed, 22 skipped, 0 failed. Không migration mới. MariaDB 10.5.23 disposable đã nâng, lặp lại và giữ user 900001 cùng câu legacy NULL. Pending model không đổi. Nhờ review; đây không phải tự duyệt. Phase 6–7 vẫn LOCKED.
