# Phase 5 sửa vòng 1 — HR-P5-R1-F01 đến F05

## Phạm vi

- Phase 5, sửa vòng 1. Prompt `.ai/prompts/hybrid-rag/phase-5-fix-round-1.md`. Plan `docs/superpowers/plans/hybrid-rag/phase-5-fix-round-1.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối đều `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, manifest Phase 5 đã review khớp 137/137, fingerprint `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`. Fingerprint Phase 4 đã duyệt vẫn là `1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`.
- Cây dirty Phase 0–5 được giữ. Vòng này không thêm cột, không thêm migration, không sửa `20261107043157_AddAiMessageGroundingMetadata`, Designer hay snapshot.
- Không đổi `Program.cs`, `appsettings.json`, `.csproj`, `IAiProviderClient`, model hay credential. Không gọi API trả phí, không push, không PR, không CI, không deploy. Provider trong test là fake.
- Manifest mới: [scope-manifest.json](scope-manifest.json), 140 mục, fingerprint `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`. `predecessorFingerprint` là fingerprint 137 mục ở trên.
- Phase 6 và Phase 7 vẫn LOCKED. Báo cáo này không tự duyệt các finding.

## Đánh giá schema

Năm finding không đổi entity, DbContext hay mapping. Cột `GroundingMetadata` `longtext` NULL đã có từ Phase 5 vẫn là hợp đồng lưu. `dotnet ef migrations has-pending-model-changes` báo không có thay đổi model. Không sinh migration mới và không sửa history đã có trong source.

## Hành vi đã sửa

Metadata `NULL` hoặc chỉ khoảng trắng vẫn là dòng cũ: giữ nguyên câu, `Grounding` null. Metadata có mặt nhưng không đọc được (JSON hỏng, schema tương lai, phần tử nguồn null hoặc sai kiểu, quá sâu, quá dài) thì màn hình giữ câu đã lưu với `status` `unverified` và không có nguồn. Ngữ cảnh provider đổi câu đó thành `Không xác minh được nguồn của câu trả lời này. Không dùng nội dung cũ làm dữ liệu hiện tại.` Parser chỉ nuốt `JsonException`. Lỗi database và hủy thao tác vẫn thoát ra ngoài. Route lưu trong blob không được tin.

Mỗi câu được trích lưu một dòng, khóa trùng là `sourceKey` cộng `sentenceId`. Màn hình gom một link theo `sourceKey` nhưng hash của từng câu vẫn nằm trong metadata. Hash tài liệu đã lưu mà không khớp `ContentHash` hoặc hash tiêu đề-thân của bản đang sống thì cả câu là `changed`; không lấy câu đầu còn khớp để gọi là hiện tại. Phong bì v1 không có hash tài liệu mà thiếu câu đang sống cũng là `changed`. Dòng cũ không bị viết lại và không bị bịa hash. Nếu một nguồn trong phong bì mất quyền, cả câu và mọi nguồn bị thay bằng thông báo không còn xem được.

Lịch sử nạp từng batch tối đa 50 dòng, chiếu ở chế độ provider trước khi tính token và trước mọi lần return, kể cả khi dòng kế tiếp vượt ngân sách. Ngân sách tính trên chữ đã chiếu. Ngân sách bằng 0 không nạp lịch sử. Thứ tự thời gian và kiểm tra hội thoại của người dùng được giữ.

Sau khi assistant và ledger đã commit, lỗi hoặc hủy lúc chiếu màn hình không ném ra ngoài, không thêm assistant thứ hai, không settle lần nữa và không Release usage đã biết. Reply HTTP và stream, vì stream gọi `SendAsync`, trả thông báo chưa xác minh được nguồn. Câu đã lưu vẫn là câu được chấp nhận để lần tải sau kiểm tra quyền lại. Lỗi `Capture` trước commit vẫn đi đường `reply_persistence_failed` như cũ.

## Sổ kiểm tra

| Kiểm tra | Lệnh | Môi trường | Kết quả |
| --- | --- | --- | --- |
| Manifest vào vòng sửa | SHA-256 từng file rồi fingerprint thứ tự path | Local, không API | 137/137, `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165` |
| Regression đỏ | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug --filter FullyQualifiedName~AiRagPhase5FixRound1Tests` | SQLite, fake provider | Failed 7, Passed 2, Skipped 0, Total 9, 15 s. Hai test đã xanh là ngân sách lịch sử bằng 0 và thu hồi nguồn lẫn |
| Regression xanh | Cùng filter sau khi sửa, rồi thêm test cache/câu lặp | SQLite, fake provider | 9/9, sau đó 10/10, 0 failed |
| Focused | `dotnet test ... -c Debug --filter FullyQualifiedName~AiRagPhase5\|FullyQualifiedName~AiRagPhase4\|FullyQualifiedName~AiCancelAndStop\|FullyQualifiedName~AiQuota\|FullyQualifiedName~OpenAiConsumerPipeline` | SQLite, fake provider | Lần đầu 1 failed: `PublishedEdit_IsLabeled_AndLeavesProviderContext` thành `changed` vì hash tài liệu nằm ở field câu. Sau khi nhận hash tài liệu/thân là provenance cấp tài liệu: Passed 70, Skipped 2, Failed 0, Total 72, 37 s |
| Node chat | `node --test scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs` | Node, không API | 7 passed, 0 failed. JS và adapter không đổi; chạy lại để xác nhận |
| Pending model | `dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext --configuration Debug --no-build` | EF 9.0.4, disposable `127.0.0.1:33322`, không đụng cổng 3307 | `No changes have been made to the model since the last migration.` exit 0 |
| Nâng schema disposable | DB rỗng, `lower_case_table_names=1`, nâng tới `20260510020134_AddReadingUserTranslation`, chèn user 900001, nâng tới `20261106181456_AddAiQuotaTurnStage`, chèn hội thoại và câu legacy, nâng hết, lặp lại | MariaDB `10.5.23-MariaDB-1:10.5.23+maria~ubu2004`, image `mariadb:10.5.23`, digest `sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730`, container `tct-migration-check-phase5r1` đã xóa, DB `tct_migration_check`, client `127.0.0.1:33322` | 15/15 migration. Checksum `users` `67983375` trước, giữa và sau. Câu `22222222-2222-2222-2222-222222222222`, hội thoại `11111111-1111-1111-1111-111111111111`, user 900001, role 2, content `legacy-answer-body`, `GroundingMetadata` NULL. Cột `longtext`, nullable, không index. FK `FK_AiMessages_AiConversations` còn, `DELETE CASCADE`. Lần update thứ hai không đổi history |
| Full suite | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug` | Local Debug, không biến `TCT_ACCEPTANCE_MARIADB`, không API | 1118 passed, 22 skipped, 0 failed, total 1140, 3 m 27 s |

Hai skip của bộ focused và 22 skip của full suite là fact MariaDB hoặc live opt-in khi không có `TCT_ACCEPTANCE_MARIADB`. Chúng không được tính là pass. Bằng chứng schema là MariaDB 10.5.23 ở trên, không phải SQLite. DB dev `tctenglish-mariadb-local` cổng 3307 không bị dùng. Container disposable và file mật khẩu tạm đã xóa. Biến `ConnectionStrings__DefaultConnection` của shell đã được gỡ.

Chưa mở trình duyệt. Phase 6 vẫn là cổng kiểm tra giao diện.

## Rủi ro cho reviewer

- Phong bì v1 chỉ có câu đầu và không có hash tài liệu sẽ được gắn `changed` khi tài liệu sống còn câu khác, dù câu đầu vẫn khớp. Đây là chủ đích. Dòng đã lưu không bị sửa.
- Hash tài liệu không khớp bản sống thì cả câu là `changed`, kể cả khi một câu vẫn khớp.
- Hash câu bằng hash tài liệu, hash tiêu đề-thân hoặc hash cả thân được coi là provenance cấp tài liệu. Envelope một câu của Phase 5 đang lưu hash tài liệu ở field câu vẫn `current` cho đến khi tài liệu đổi.
- Lỗi chiếu sau commit trả thông báo chưa xác minh trên reply, trong khi dòng assistant đã lưu vẫn là câu được chấp nhận. Tải lại sẽ kiểm tra quyền lại. Người dùng không thấy send thất bại để retry tự động.
- Màn hình của metadata không đọc được vẫn hiện câu đã lưu. Chỉ ngữ cảnh provider bị thay.
- Chưa có cuộc gọi provider sống và chưa có kiểm tra trình duyệt. Phase 7 giữ phần chấp nhận provider. Phase 6 giữ giao diện nguồn đầy đủ.

## Trạng thái

Phase 5 là AWAITING_REVIEW. Phase 6 và Phase 7 vẫn LOCKED. Kiểm tra ở trên là local. CI chưa chạy. Production chưa deploy. Báo cáo này không phải APPROVED.

Đoạn dán cho Codex:

Phase 5 sửa vòng 1 đang AWAITING_REVIEW. Scope 140 mục, fingerprint 6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683. Predecessor là manifest đã review 137/137, fingerprint 01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165. HEAD vẫn 6af768bd32d16cc6d012d484ee82df4cab805c69. HR-P5-R1-F01–F05 đã sửa local: regression đỏ 7 failed / 2 passed, sau đó 10/10. Focused 70 passed, 2 skipped, 0 failed. Full suite 1118 passed, 22 skipped, 0 failed. Không migration mới. MariaDB 10.5.23 disposable đã nâng, lặp lại và giữ user 900001 cùng câu legacy NULL. Pending model không đổi. Nhờ review; đây không phải tự duyệt. Phase 6–7 vẫn LOCKED.
