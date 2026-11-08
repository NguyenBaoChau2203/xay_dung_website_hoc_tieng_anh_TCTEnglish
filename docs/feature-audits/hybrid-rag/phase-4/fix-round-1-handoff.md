# Phase 4 — handoff sửa vòng 1

Ngày 2026-11-07. Cursor thực hiện `.ai/prompts/hybrid-rag/phase-4-fix-round-1.md` và `docs/superpowers/plans/hybrid-rag/phase-4-fix-round-1.md`. Kết quả là **AWAITING_REVIEW**. Phase 5–7 giữ LOCKED. Agent không tự duyệt và không coi F01–F05 là đã được Codex đóng.

## Danh tính

- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối `6af768bd32d16cc6d012d484ee82df4cab805c69`. Chưa commit.
- Trước khi sửa, manifest trả về khớp 109/109, fingerprint `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`. Fingerprint Phase 3 đã duyệt vẫn là `66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`.
- Manifest tích lũy sau sửa: [scope-manifest.json](scope-manifest.json), **113 entry**, fingerprint `b3406e3068e06442a6319e88a3e0f6e606ce9c18a9e997c0c13f983a3330693f`. Đã đọc lại file và tính lại: 113 hash khớp, fingerprint khớp. Đây là danh tính bàn giao, chưa phải phê duyệt.
- Bốn path mới trong digest: prompt và plan của vòng sửa, `TCTEnglish.Tests/AiRagPhase4RepairRound1Tests.cs`, `TCTEnglish/Services/AI/AiProviderReply.cs`. Mười bốn path cũ đổi hash, gồm bug log, ledger/journal, provider, budget, pipeline, selector, hybrid retrieval, composer và test MariaDB.
- Handoff triển khai Phase 4, handoff vòng sửa này, review round 1 và `review-state.md` đứng ngoài fingerprint. Các handoff/review/manifest của phase trước vẫn ngoài digest và được kê trong `baselineDirtyNotInDigest`.
- Không sửa `appsettings.json`, `.csproj`, `Program.cs` trong vòng này, và không sửa migration đã áp `20261106181456_AddAiQuotaTurnStage`. Không có migration mới. `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không gọi API trả phí. Không commit, push, PR, CI hay deploy.

## Từng finding

Test mới nằm trong `AiRagPhase4RepairRound1Tests`. Chúng thất bại trên cây trước khi sửa production, rồi xanh trên cấu hình `Phase4RepairRound1`. HTTP trong các test là handler giả. Ledger SQLite của test không thay cho MariaDB.

| ID | Đỏ, theo review và regression | Xanh sau sửa |
| --- | --- | --- |
| F01 | Lần embed đầu mất response, lần sau có receipt: usage đã biết bị rơi. Vector không hợp lệ làm mất known 7. Generation incomplete biến known 105 thành unknown 64 | `LostEmbeddingResponse_RetainsUnknownBoundAndKnownSeven`: 2 POST, known 7, unknown còn. `InvalidQueryVector_RetainsKnownSeven`: 1 POST, known 7. `IncompleteGeneration_RetainsKnownOneHundredFive`: KnownGenerationTokens 105; cùng embedding known 17 thì ActualTokens 122 |
| F02 | Trần output không đại diện cả lượt generation. Fixture 3228 byte vẫn lọt planned 328 / request 700. Lịch sử CJK có thể thành body vượt phần đã giữ | Planned 328, output 64, budget 700 bị từ chối hoặc `GenerationInputUpper` nhỏ hơn 3228. Lượt được nhận chỉ gửi body nằm trong trần byte của đúng payload `CreatePayload`. Lịch sử CJK bị cắt hoặc từ chối trước HTTP |
| F03 | Chunk `The bell is red. The door is blue.` trả cả chunk cho follow-up. `{"sentenceIds":[]}` completed vẫn có thể thành câu trả lời catalog | Follow-up `is that true?` chỉ còn câu đỏ, giữ sentence id gốc, hash câu, hash tài liệu, route, visibility và version. Completed rỗng không dùng catalog |
| F04 | Gỡ publish, đổi hash, chuyển private hoặc đổi snapshot trong lúc selector chạy vẫn thành câu đã chọn | `UnpublishDuringSelection_DoesNotAcceptTheOldSentence` không trả câu cũ. Nguồn còn hiệu lực và không đổi vẫn trả lời được. Usage đã trả tiền được giữ. Từ chối cố định của server được lưu với HTTP 200 |
| F05 | Chủ lease hết hạn vẫn gửi generation và receipt muộn ghi KnownGenerationTokens. Mutation trên dòng thiếu, đã đóng hoặc hết hạn vẫn báo thành công | `ExpiredOwner_DoesNotSendGenerationAfterRecovery`: 0 lần gọi selector, không giữ câu cũ. Trên MariaDB, `ExpiredOwner_DoesNotAcceptALateGenerationReceipt` trả false, KnownGenerationTokens null, settle đúng unknown embedding 22. Lease còn sống của cuộc hội thoại khác vẫn qua được |

`LexicalAnswerAdmission` không được nới. Không thêm bảng citation. Lease 3 phút không bị kéo dài. Dòng terminal không bị mở lại bởi receipt muộn.

## Cách sửa

Usage từng lần HTTP được ghi trước khi kiểm hình dạng câu trả lời. Lần gửi đã đi mà không có receipt cộng vào unknown; receipt known của lần sau được giữ cùng unknown đó. Preflight `invalid_configuration`, `budget_exhausted` và `corpus_stale` vẫn là 0 lần gửi.

Trần generation đo đúng byte UTF-8 của payload selector, gồm system prompt, câu hỏi, evidence và schema. Slack evidence vẫn là `min(800, room)` để câu nguồn ngắn còn được nhận. Payload không vừa thì pipeline cắt lịch sử user cũ nhất rồi cắt câu evidence cuối, hoặc từ chối trước HTTP. Không thêm gói tokenizer.

Materialize phát một evidence cho mỗi câu snapshot có hash khớp, kèm provenance tạm. Selector completed với mảng id rỗng không rơi xuống catalog.

Trước khi gọi selector và sau khi provider trả về, pipeline đọc lại publication, hash và snapshot. Lệch thì bỏ câu và JSON model, trả `SourceRevoked`, giữ usage đã ghi.

`MutatePendingAsync` trả false và không sửa dòng khi ledger thiếu, không còn Pending, hoặc lease đã tới hạn. `OwnsTurnAsync` là đọc không tracking. Begin, ghi usage hoặc mất chủ thì không gửi generation và không tạo artifact thành công.

## Kiểm tra

| Kiểm tra | Lệnh hoặc thao tác | Kết quả |
| --- | --- | --- |
| RAG, provider, cancel, quota, consumer | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4RepairRound1 --filter FullyQualifiedName~AiRag\|FullyQualifiedName~OpenAiProviderClientTests\|FullyQualifiedName~AiCancelAndStop\|FullyQualifiedName~AiQuotaReservation\|FullyQualifiedName~AiCloseout\|FullyQualifiedName~OpenAiConsumerPipeline` | **178 pass**, 0 fail, 0 skip, Duration 27 s, exit 0. Một lần trước đó 176 pass và 2 fail vì slack evidence bị co dưới độ dài câu; đã trả slack `min(800, room)` rồi chạy lại |
| Bộ app | cùng project, `-c Phase4RepairRound1` | **1086 pass**, **21 skip**, **1 fail**, total 1108, Duration 5 phút 27 giây. Fail duy nhất: `AiRagPhase3FixRound1Tests.CancellationAfterPaidSuccess_KeepsKnownUsageAndThePreviousIndex`, mong `promoted`, nhận `failed`. 21 skip gồm fact MariaDB vì bộ này không có biến môi trường acceptance |
| Cùng test promote, chạy riêng | `--no-build --filter FullyQualifiedName~CancellationAfterPaidSuccess_KeepsKnownUsageAndThePreviousIndex` | **1 pass**, 0 fail, 0 skip, Duration 5 s, exit 0. Assertion không bị nới. Chưa chạy lại cả bộ sau lần riêng này |
| Pending model | `dotnet tool run dotnet-ef -- migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext --configuration Phase4RepairRound1 --no-build` trên DB disposable | `No changes have been made to the model since the last migration.` Exit 0. Đây là đối chiếu metadata, không phải bằng chứng nâng cấp |
| Whitespace | `git diff --check` | không báo lỗi |
| Schema | so diff migration và snapshot với HEAD | không sửa file migration đã áp. Không thêm migration. Snapshot và `Program.cs` bẩn từ Phase 4 trước vòng sửa, không phát sinh thêm trong vòng này |

Configuration test là `Phase4RepairRound1`. `scripts/verify-mariadb.ps1` cố định `--configuration Release --no-build`. Release đang bị `TCTEnglish.exe` pid 19632 khóa, nên không chạy script đó và không dừng tiến trình của owner. Các bước tương đương chạy trên assembly `Phase4RepairRound1`.

## MariaDB 10.5.23

Image local `mariadb:10.5.23`, digest `sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730`. Engine Docker 29.9.2. DB dev `tctenglish-mariadb-local` trên `127.0.0.1:3307` không bị dùng.

Nâng cấp disposable, container `tct-migration-check-phase4r1`, `127.0.0.1:33318`, database `tct_migration_check`, bắt đầu rỗng, `lower_case_table_names=1`:

- Update tới `20260510020134_AddReadingUserTranslation`, chèn user 900001, `CHECKSUM TABLE users` trước và sau nâng cấp đầy đủ giống nhau.
- Update đầy đủ áp 14 migration, gồm `20261106181456_AddAiQuotaTurnStage`.
- Update lặp: `No migrations were applied. The database is already up to date.`
- History 14, đúng số migration nguồn. Bảy cột stage đều `IS_NULLABLE=YES`: ActiveStage, KnownEmbeddingTokens, KnownGenerationTokens, LeaseExpiresAtUtc, TerminalIntent, UnknownEmbeddingTokens, UnknownGenerationTokens.

Lần bootstrap acceptance đầu thất bại vì container đã tạo sẵn database `tct_acceptance_phase4r1` mà không có sentinel, và script từ chối ghi đè. Đã xóa chỉ container acceptance của vòng này và tạo lại không precreate database. Container migration-check giữ nguyên kết quả ở trên.

Acceptance, container `tctenglish-mariadb-acceptance-phase4r1`, `127.0.0.1:3338`, database `tct_acceptance_phase4r1`, run `phase4r1`:

- Bootstrap tạo sentinel. `dotnet ef database update` áp đủ 14 migration trên DB này.
- `dotnet test ... -c Phase4RepairRound1 --no-build --filter FullyQualifiedName~MariaDbAiTurnLifecycleTests` với `TCT_ACCEPTANCE_MARIADB` và `TCT_ACCEPTANCE_RUN_ID=phase4r1` chỉ trên tiến trình test: **2 pass**, 0 fail, 0 skip, Duration 12 s. Gồm lease còn sống rồi hết hạn, và receipt generation muộn bị từ chối.
- Biến connection của tiến trình script được xóa sau test. Mật khẩu không được in.

## Giới hạn

Handler giả và SQLite không phải lượt gọi OpenAI. Không có request speech và không có embedding live. Key local nếu có thì không bị đọc để gọi API. Bộ app còn một fail promote của Phase 3 khi chạy cả suite; lần chạy riêng thì xanh. Không có CI và không có production deploy.

## Việc Codex cần xem

Known và unknown cùng sống qua retry. Body selector đúng byte đã admit. Câu gốc và provenance đi theo evidence tạm. Nguồn đổi trong lúc chọn thì không thành câu assistant. Chủ hết hạn không gửi generation và không nhận receipt muộn. Manifest 113 entry ở trên là snapshot cần đối chiếu. Fingerprint tiền nhiệm là `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`. Fingerprint bàn giao là `b3406e3068e06442a6319e88a3e0f6e606ce9c18a9e997c0c13f983a3330693f`.

Phase 4 đang **AWAITING_REVIEW**. Không mở Phase 5.

## Tin nhắn dán cho Codex

Phase 4 repair round 1 đã xong local và đang AWAITING_REVIEW. Fingerprint 113 entry `b3406e3068e06442a6319e88a3e0f6e606ce9c18a9e997c0c13f983a3330693f`, tiền nhiệm đã review `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`. F01–F05 có regression xanh trong filter 178 pass. Bộ app 1086 pass, 21 skip, 1 fail promote Phase 3; chạy riêng test đó thì 1 pass. MariaDB 10.5.23: nâng cấp 14 migration, checksum user 900001 giữ nguyên, update lặp ổn định; acceptance lifecycle 2 pass 0 skip. Không có migration mới, không gọi API trả phí, chưa commit. Phase 5–7 vẫn LOCKED. Nhờ review, không triển khai phase sau.
