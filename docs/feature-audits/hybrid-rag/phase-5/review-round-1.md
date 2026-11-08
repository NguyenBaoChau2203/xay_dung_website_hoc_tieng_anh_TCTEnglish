# Phase 5 — Codex review round 1

Ngày review: 07/11/2026. Kết luận: **CHANGES_REQUESTED**.
Phase 6–7 tiếp tục **LOCKED**. Review này không cấp quyền sửa hoặc mở phase sau.

## Snapshot và phạm vi kiểm tra

- Nhánh thực tế `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`.
- Manifest bàn giao: [scope-manifest.json](scope-manifest.json). Tính lại SHA-256
  từ byte file: **137/137 khớp**, fingerprint ordinal có LF cuối:
  `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
- Tiền nhiệm được ghi trong manifest là Phase 4 đã APPROVED, 117 entry,
  `1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`.
  Các file chung được Phase 5 sửa hợp lệ không cần tiếp tục giống byte Phase 4.
- Rà soát envelope/capture/projection, quyền card/class/private media và corpus,
  các nhánh nạp history, send/commit/stream, mapping HTTP/ViewModel/DOM,
  migration/Designer/snapshot, DI và test bàn giao. CodeGraph dùng trước cho source.
- Chỉ thêm probe tạm để review; bản compile đã được gỡ khỏi `TCTEnglish.Tests`.
  Source probe và kết quả nằm trong ignored
  `.tmp/hybrid-rag-review/phase-5/round-1/`. Không sửa source/test/migration/config
  thuộc 137 entry, không sửa manifest bàn giao. Review/state/plan/prompt là artifact
  review mới. Không gọi API trả phí, commit, push, CI hoặc deploy.

## Findings

### HR-P5-R1-F01 — P1: lịch sử hết ngân sách trả về trước kiểm quyền nguồn

**Nguồn**: `TCTEnglish/Services/AI/AiChatService.cs:595–608`, đặc biệt dòng 599.

`LoadContextHistoryAsync` gọi `ProjectAsync(... ProviderContext ...)` ở cuối hàm,
nhưng khi message tiếp theo vượt `remaining`, hàm reverse rồi return ngay. Các
assistant đã chọn trước đó được trả nguyên văn, dù metadata trỏ tới nguồn đã xóa
hoặc không còn quyền xem. Nhánh này xuất hiện tự nhiên với hội thoại dài.

**Đã tái hiện**: seed 120 user message cũ, mỗi message 6.000 ký tự (trong trần
persisted Content 8.000), và một assistant mới hơn chứa `REVOKED_HISTORY_SECRET`
với nguồn `card:99999` không tồn tại. Gửi `hello` qua `IAiChatService` thật, dùng
pipeline ghi nhận thay cho provider. Cả `GroundedTurnInput.History` và `Context`
vẫn chứa marker. Probe `HistoryBudgetEarlyReturn_MustStillRemoveRevokedBody` đỏ.
Không có HTTP provider thật; đây là bằng chứng context nội bộ chưa được lọc.

**Sửa cần thiết**: mọi đường thoát phải dùng cùng bước projection trước khi tạo
context/history turn; đo token sau projection hoặc bảo đảm trần trên phần text
thực sự được dùng. Giữ phân trang, thứ tự và giới hạn tải; không nạp toàn bộ chat.

### HR-P5-R1-F02 — P1: envelope không xác minh được vẫn giữ câu cũ trong ProviderContext

**Nguồn**: `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs:212–215`.

Khi `TryRead` trả false, code chỉ đặt `Grounding = Unverified()` rồi tiếp tục.
`Content` giữ nguyên ở cả Display và ProviderContext. Nguồn trong envelope hỏng
hoặc schema tương lai không thể được reauthorize, nhưng câu cũ vẫn được đưa vào
đường context như assistant thông thường. Điều này khác policy đã áp dụng cho
nguồn changed/snapshot và không đáp ứng checklist ngăn replay câu cũ chưa xác minh.

**Đã tái hiện**: hai probe `UnverifiableMetadata_MustNotReplayPrivateBodyToProvider`
với `{broken` và schemaVersion 9 chứa `card:99999`. Sau projection ở
ProviderContext, cả hai vẫn trả `UNVERIFIABLE_PRIVATE_SECRET`, probe đều đỏ.
Đây là quan sát tại API projection, không phải bằng chứng đã gửi bí mật tới API
thật. Selector hiện chủ yếu dùng user history; không lấy điều đó làm bảo đảm cho
mọi caller của context contract.

**Sửa cần thiết**: phân biệt legacy/null thật sự với metadata có nhưng không
xác minh được. Với ProviderContext, bỏ hoặc thay body chưa xác minh bằng notice
an toàn. Có thể giữ policy Display unverified hiện tại theo contract; không cần
đổi tất cả legacy rows hay suy đoán quyền từ blob không hợp lệ.

### HR-P5-R1-F03 — P2: phần tử nguồn null làm hỏng đọc lịch sử

**Nguồn**: `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs:503–511`.

JSON hợp lệ `{"schemaVersion":1,"outcome":"answered","sources":[null]}` deserialize
thành list có phần tử null. Vòng lặp truy cập `source.SourceKey` gây
`NullReferenceException`; catch chỉ xử lý JsonException. Một row như vậy có thể
làm lỗi initial history, load-older hoặc lượt chat cần đọc row đó.

**Đã tái hiện**: `NullSourceEntry_MustNotCrashHistory` đỏ ở cả Display và
ProviderContext, stack trỏ đúng dòng 511. Tất cả probe dùng SQLite/fake services.

**Sửa cần thiết**: kiểm cấu trúc/null entry và các identity/field cần dùng trước
khi dereference; metadata không hợp lệ đi theo policy an toàn của F02. Không dùng
catch-all để nuốt lỗi DB, hủy request hoặc lỗi lập trình ngoài parser. Thêm coverage
qua HTTP history và send, không chỉ unit gọi parser.

### HR-P5-R1-F04 — P1: gộp theo document làm mất citation câu thứ hai và bỏ lọt stale body

**Nguồn**: `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs:139–142`,
`432–454`; đối chiếu `GroundedChatPipeline.cs:295–315`.

Pipeline trả evidence theo sentence; `Capture` lại dedup chỉ theo SourceKey.
Hai câu thuộc cùng tài liệu chỉ lưu SentenceId/hash đầu tiên. Khi document thay
đổi, `CorpusFreshnessAsync` vẫn kết luận current nếu câu đầu còn cùng hash. Câu
thứ hai đã sửa không còn identity để kiểm, nên toàn câu trả lời cũ được giữ lại.

**Đã tái hiện**: passage published có hai câu `The harbor lamp is red.` và
`The harbor bell is blue.`. Capture hai evidence thật từ `CorpusChunker` chỉ lưu
`reading:1#s1`. Sửa riêng câu thứ hai thành `The harbor bell is green.` rồi project
ProviderContext: body vẫn gồm `The harbor bell is blue.`. Probe
`TwoCitedSentencesInOneDocument_EditSecondMustInvalidateWholeHistoricalAnswer` đỏ.
Code hiện sẽ đi nhánh current khi Display; phép thử trực tiếp xác nhận stale body
còn ở ProviderContext, envelope chỉ có câu đầu.

**Sửa cần thiết**: giữ mọi sentence identity ảnh hưởng body, hoặc lưu một fingerprint
document đủ bảo thủ và không fallback bỏ qua câu đã dùng. Dedupe citation hiển thị
có thể tách khỏi provenance dùng kiểm freshness. Đồng bộ cache key, giới hạn
envelope/source và cách đọc envelope v1 đã lưu; không suy đoán các câu bị mất.
Đổi một câu đã trích phải làm toàn answer changed và loại body cũ khỏi context.

### HR-P5-R1-F05 — P2: projection lỗi sau commit biến lượt đã lưu thành lỗi gửi

**Nguồn**: `TCTEnglish/Services/AI/AiChatService.cs:398–411`.

Sau khi assistant và metadata được commit, quota đã finalize, send còn await
projection có truy vấn DB với `CancellationToken.None`. Exception tại đây thoát
ra caller vì catch persistence chỉ áp dụng khi `!replyCommitted`. Người dùng
thấy gửi lỗi trong khi lịch sử đã có câu trả lời; gửi lại có thể tạo lượt mới và
tính quota thêm. JS hiện không tự retry HTTP (maxAttempts=1), nên không khẳng định
có retry tự động.

**Đã tái hiện**: fault injection chỉ vào Display projection sau Capture bình thường.
`ProjectionFaultAfterCommit_MustNotReturnFailureForCommittedTurn` quan sát
`InvalidOperationException` và **1 assistant đã commit**. Probe đỏ, không gọi API.

**Sửa cần thiết**: sắp thứ tự các bước fallible trước commit hoặc thiết kế kết quả
sau commit có giới hạn và fail closed, vẫn giữ identity/settlement của lượt đã
chấp nhận. Nếu kiểm quyền thất bại, không fallback hiển thị nguyên văn chưa được
authorize. Không release known usage, rollback hoặc tạo câu assistant thứ hai.

## Những phần đã có bằng chứng tốt

- 8 test citation bàn giao pass: send/reload, stream service, older pages,
  chống mở chat user khác, xóa card, rời class, downgrade private media,
  changed published source, legacy và lỗi Capture giữ usage.
- HTTP/hub giữ field/event hiện có và thêm grounding tùy chọn. Controller giữ
  authorize/antiforgery/NotFound; route nguồn dựng lại từ query còn quyền xem.
  Razor encode text; JS tạo node/textContent cho title và notice, không dùng href
  trong metadata cũ. Chưa làm browser acceptance mới.
- Migration `20261107043157_AddAiMessageGroundingMetadata` thêm duy nhất nullable
  `longtext` utf8mb4; Designer và snapshot có đủ. Up không làm mất dữ liệu. Down
  chỉ bỏ cột mới; không chạy Down. Các migration cũ thuộc scope được giữ hash.

## Kiểm chứng độc lập

| Kiểm tra | Kết quả / giới hạn |
| --- | --- |
| Hash và fingerprint | 137/137 khớp, đúng fingerprint bàn giao |
| Build cấu hình riêng | App và tests `Phase5Review` build thành công, có warning sẵn có |
| Test citation bàn giao | 8 passed, 0 failed, 0 skipped |
| Probe đã chỉnh fixture đúng schema/giới hạn Content | 7 failed / 0 passed / 0 skipped, xác nhận 5 finding trên; không dùng lỗi compile/fixture ban đầu làm finding |
| Node history/submit | 7 passed / 0 failed |
| Bộ app bàn giao, loại đúng lớp probe reviewer | **1108 passed / 22 skipped / 0 failed**, total 1130, 3 phút 26 giây; cả 8 test citation bàn giao pass. 22 skip gồm MariaDB/live/scale opt-in, không tính là pass |
| MariaDB 10.5.23 độc lập | Disposable `tct-migration-check-phase5review`, `127.0.0.1:33321`, DB `tct_migration_check`, lower_case_table_names=1; nâng baseline qua quota stage tới đủ 15 migration, chạy lại không đổi history |
| Bảo toàn dữ liệu | User 900001 checksum `2307622351` giữ nguyên; hash các field legacy message/conversation trước/sau `1926429b6114730cece67fd38fa934eb79883b28ee8e6026936d7558f00ebf08`; body `legacy-review-body`, quan hệ còn, metadata NULL |
| Schema thực tế / EF | GroundingMetadata longtext/nullable/utf8mb4, FK_AiMessages_AiConversations còn; repo-local EF 9.0.4: no pending model changes |
| Diff whitespace | `git diff --check` sạch lúc kiểm tra; sẽ kiểm lại sau khi ghi artifact |

Lệnh bộ app: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase5Review --no-build --no-restore --filter FullyQualifiedName!~AiRagPhase5ReviewProbeTests`.
TRX/log và script MariaDB trong ignored thư mục review. Script đọc credential
container disposable trong bộ nhớ/env con, không ghi giá trị vào source/argv/log.
DB dev cổng 3307 và các workload sẵn có không bị dùng hoặc reset.

## Bàn giao sửa

- [Repair plan](../../../superpowers/plans/hybrid-rag/phase-5-fix-round-1.md)
- [Execution prompt](../../../../.ai/prompts/hybrid-rag/phase-5-fix-round-1.md)
- [Review state](../../../superpowers/plans/hybrid-rag/review-state.md)

Các đường dẫn trên được kiểm lại trước bàn giao. Owner cần launch repair scope;
thực thi xong phải trả AWAITING_REVIEW, không tự đóng finding, không mở Phase 6.

Closeout review: tính lại sau khi ghi artifact vẫn **137/137 khớp**, fingerprint
bàn giao không đổi; branch/HEAD giữ nguyên. Các link Markdown mới hợp lệ,
`git diff --check` sạch. Container disposable do review tạo đã được dừng sau khi
kiểm tra đúng ID/name; dữ liệu synthetic còn để kiểm lại, không xóa container/DB.
Không có sửa lỗi implementation trong lượt REVIEW_ONLY này. Không có browser,
live provider, CI hoặc production acceptance mới.
