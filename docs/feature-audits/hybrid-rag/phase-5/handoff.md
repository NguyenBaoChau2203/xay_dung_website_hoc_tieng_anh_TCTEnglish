# Phase 5 handoff — citation bền và ẩn câu trả lời khi mất quyền nguồn

## Phạm vi

- Phase 5, lần triển khai local. Prompt `.ai/prompts/hybrid-rag/phase-5-durable-citations.md`. Plan `docs/superpowers/plans/hybrid-rag/phase-5-durable-citations.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối đều `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, manifest Phase 4 khớp 117/117, fingerprint `1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`.
- Cây dirty của Phase 0–4 được giữ. Phase 5 thêm cột nullable `AiMessages.GroundingMetadata`, phong bì citation phiên bản 1, và kiểm tra quyền lại lúc đọc.
- Grant đã có: chỉ đăng ký DI chat trong `Program.cs` cho `IGroundingCitationService`. Không đổi `appsettings.json`, `.csproj`, `IAiProviderClient`, model hay credential.
- Không gọi API trả phí, không push, không PR, không CI, không deploy. Provider trong test là fake.
- Manifest: [scope-manifest.json](scope-manifest.json), 137 mục, fingerprint `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
- Phase 6 và Phase 7 vẫn LOCKED.

## Migration impact: AddAiMessageGroundingMetadata

**Type**: Additive

**Tables affected**: `AiMessages`

**Backward compatible**: Yes

**Data loss risk**: None trên `Up`. `Down` chỉ xóa cột `GroundingMetadata` mà migration này thêm.

Cột `GroundingMetadata` là `longtext` NULL, charset `utf8mb4`, không index, không khóa ngoại mới. Dòng cũ giữ `NULL`. Id, nội dung và quan hệ hội thoại không đổi. Migration `20261107043157_AddAiMessageGroundingMetadata`, Designer và snapshot đi cùng code. EF repo-local 9.0.4. Không sửa migration đã áp dụng.

## Hành vi đã kiểm

Câu trả lời được chấp nhận lưu nguyên văn và một phong bì JSON phiên bản 1 trong cùng lần `SaveChanges`. Phong bì có `schemaVersion`, `outcome`, `scope`, `capturedAtUtc` và tối đa 12 nguồn. Mỗi nguồn có khóa, loại, id câu, hash và visibility. Phong bì không lưu route, tiêu đề, đoạn nguồn, reasoning hay payload provider. Route được dựng lại từ thực thể còn quyền xem.

Gửi “what does forecast mean” với thẻ 401 có định nghĩa `predict-citation-token` trả `outcome` `answered`, nguồn `card:401`, route `/Home/Study/301`. Metadata đã lưu không chứa token, `/Home/` hay `http`. Tải lại trang và JSON lịch sử trả cùng khóa nguồn. Xóa thẻ 401 thì nội dung assistant thành `Nguồn dùng cho câu trả lời này không còn xem được.`, `status` `unavailable`, `sources` rỗng. User khác mở hội thoại nhận 404, body không có token.

Stream completion trả cùng nguồn `card:401`. Trang cũ hơn giữ thứ tự thời gian: dòng legacy không có phong bì vẫn hiện nguyên văn; dòng nguồn đã mất quyền bị thay cả câu, không chỉ ẩn link. Lần tải cũ thứ hai trùng id và nội dung.

Phong bì JSON hỏng hoặc `schemaVersion` 9 giữ nguyên câu đã lưu, `status` `unverified`, không có nguồn và không dựng route từ blob. Nguồn published bị sửa sau lúc ghi thì màn hình giữ câu cũ kèm mốc UTC; ngữ cảnh gửi provider đổi thành câu nguồn đã cũ và không còn câu cũ. Gỡ publish thì cả câu biến thành thông báo không còn xem được.

Rời lớp hoặc hạ vai trò khỏi private video làm câu lịch sử và danh sách nguồn biến mất, kể cả khi nạp lịch sử cho provider. Snapshot của chính user được gắn `freshness` `snapshot`, không bị gọi là tiến độ hiện tại. Lỗi ghi metadata sau khi provider đã trả lời không tạo dòng assistant thành công; ledger còn `Pending` và `ActualTokens` lớn hơn 0.

## Sổ kiểm tra

| Kiểm tra | Lệnh | Môi trường | Kết quả |
| --- | --- | --- | --- |
| Manifest Phase 4 trước khi sửa | SHA-256 từng file rồi fingerprint thứ tự path | Local, không API | 117/117, `1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61` |
| Citation Phase 5 | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug --filter FullyQualifiedName~AiRagPhase5CitationTests` | SQLite `TestWebApplicationFactory`, fake provider | Lần đầu 7 passed, 1 failed vì JSON escape tiếng Việt. Sau khi đọc field `content`, test đó passed. Cả 8 test nằm trong full suite bên dưới, 0 failed |
| Phase 4 grounded/quota | `dotnet test ... -c Debug --filter FullyQualifiedName~AiRagPhase5CitationTests\|FullyQualifiedName~AiRagPhase4` | SQLite, fake provider | 39 passed, 1 failed: reply thành thông báo mất nguồn vì `reading:1` chưa có passage. Sau khi seed passage published id 1, test đó passed |
| Node chat | `node --test scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs` | Node, không API | 7 passed, 0 failed |
| Pending model | `dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext` | EF 9.0.4, cổng đóng 127.0.0.1:9, không đụng DB dev | No changes, exit 0. Lặp lại `--configuration Debug --no-build` sau nâng disposable, cùng kết quả |
| Nâng schema disposable | Các bước của `scripts/verify-mariadb.ps1` trên assembly Debug: DB rỗng, `lower_case_table_names=1`, nâng tới `20260510020134_AddReadingUserTranslation`, chèn user 900001, checksum, nâng tới `20261106181456_AddAiQuotaTurnStage`, chèn hội thoại và câu legacy, nâng hết, lặp lại | MariaDB `10.5.23-MariaDB-1:10.5.23+maria~ubu2004`, image `mariadb:10.5.23`, digest `sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730`, container `tct-migration-check-phase5`, DB `tct_migration_check`, client `127.0.0.1:33320`, cổng trong container 3306 | 15/15 migration. User checksum `2695971206` trước và sau. `AiMessages` `645607072`. `AiConversations` `3761657848`. Câu `22222222-2222-2222-2222-222222222222`, role 2, content `legacy-answer-body`, `GroundingMetadata` NULL. Cột `longtext`, nullable, không index. FK `FK_AiMessages_AiConversations` còn. Lần update thứ hai không thêm history |
| Full suite | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Debug` | Local Debug, không biến `TCT_ACCEPTANCE_MARIADB`, không API | 1108 passed, 22 skipped, 0 failed, 4 m 29 s, total 1130 |

Script `scripts/verify-mariadb.ps1` gốc gắn `--configuration Release --no-build`. Lần này chạy cùng các mốc trên assembly Debug vừa build. DB dev `tctenglish-mariadb-local` cổng 3307 không bị dùng. Container disposable đã xóa sau khi ghi số liệu. 22 skip của full suite là fact MariaDB/live opt-in. Chúng không được tính là pass. Bằng chứng schema là lần MariaDB 10.5.23 ở trên, không phải SQLite.

Chưa mở trình duyệt. Test host đã kiểm HTML `data-source-key`, thông báo và JSON lịch sử. Phase 6 mới là cổng kiểm tra giao diện trên trình duyệt.

## Rủi ro cho reviewer

- Một nguồn trong phong bì mất quyền thì cả câu bị thay bằng thông báo, vì câu trả lời không được cắt theo từng nguồn.
- Nguồn đổi nhưng vẫn xem được thì màn hình giữ câu cũ và ghi mốc UTC. Ngữ cảnh provider không nhận câu cũ làm dữ liệu hiện tại.
- Snapshot tiến độ của chính user không được trình bày như số hiện tại.
- Test grounded dùng `reading:1` phải có passage published. Bỏ projection sẽ làm câu của nguồn không tồn tại hiện lại.
- Thông báo tiếng Việt trong JSON HTTP có thể bị escape. Hãy đọc field đã parse.
- Chưa có cuộc gọi provider sống. Phase 7 giữ phần chấp nhận đó.
- Phase 6 vẫn khóa: cache, chẩn đoán và giao diện nguồn đầy đủ chưa bắt đầu. Shell chỉ thêm danh sách nguồn do server dựng.

## Trạng thái

Phase 5 là AWAITING_REVIEW. Phase 6 và Phase 7 vẫn LOCKED. Báo cáo này không phải APPROVED, không phải CI và không phải production.

Đoạn dán cho Codex:

Phase 5 local đang AWAITING_REVIEW. Scope 137 mục, fingerprint 01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165. Phase 4 trước khi sửa khớp 117/117, fingerprint 1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61. HEAD vẫn 6af768bd32d16cc6d012d484ee82df4cab805c69. MariaDB 10.5.23 disposable đã nâng, lặp lại và giữ dòng cũ; pending model không đổi. Full suite 1108 passed, 22 skipped, 0 failed. Nhờ review; đây không phải tự duyệt. Phase 6–7 vẫn LOCKED.
