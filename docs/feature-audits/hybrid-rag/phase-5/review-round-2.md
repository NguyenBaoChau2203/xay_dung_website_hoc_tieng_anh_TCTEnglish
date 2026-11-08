# Phase 5 — Codex review round 2

Ngày review: 07/11/2026. Kết luận: **CHANGES_REQUESTED**.
Phase 6–7 giữ **LOCKED**. Đây là review sửa vòng 1, không phải quyền triển khai
vòng sửa tiếp hoặc phase sau.

## Danh tính bàn giao và phạm vi

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`.
- [Manifest sửa vòng 1](scope-manifest.json): **140/140 SHA-256 khớp**. Tính lại
  fingerprint ordinal từ byte file, gồm LF cuối:
  `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`.
- `predecessorFingerprint` khớp snapshot Phase 5 đã review 137 entry:
  `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
  Tham chiếu đường dẫn predecessor còn sai, ghi riêng tại F06 bên dưới.
- Dùng một agent, CodeGraph trước cho source; đọc ngắn các đoạn bị cắt để kiểm
  parser, history, commit/projection và regression mới. Tái kiểm F01–F05 theo
  source/caller/test, không coi các checkbox tự đánh dấu là phê duyệt.
- Mode REVIEW_ONLY: giữ nguyên source/test/schema/config và manifest thuộc 140
  entry. Probe tạm được compile rồi gỡ; bản source/log/TRX ở ignored
  `.tmp/hybrid-rag-review/phase-5/round-2/`. Chỉ ghi artifact review/plan/prompt/state.

## Trạng thái từng finding

| Finding | Kết luận round 2 | Bằng chứng |
| --- | --- | --- |
| HR-P5-R1-F01 / P1 | **CLOSED** cho lỗi đã nêu | Từng batch history được project trước khi tính token và trước mọi return; regression giữ turn gần, chặn body đã thu hồi, kiểm cửa sổ/batch và ngân sách 0 |
| HR-P5-R1-F02 / P1 | **PARTIALLY FIXED — OPEN** | JSON hỏng/future schema đã thay body context; envelope thiếu `sources` vẫn bị chấp nhận như danh sách rỗng |
| HR-P5-R1-F03 / P2 | **CLOSED** cho lỗi đã nêu | Raw shape và null entry được kiểm, typed loop có null guard; HTTP history/send regressions xanh, chỉ catch JsonException trong parser |
| HR-P5-R1-F04 / P1 | **CLOSED** cho lỗi đã nêu | Capture giữ sourceKey+sentenceId; kiểm theo nhóm, hash document không khớp không fallback câu đầu; v1 thiếu provenance được xử lý bảo thủ; display dedup tách khỏi validation |
| HR-P5-R1-F05 / P2 | **PARTIALLY FIXED — OPEN** | Exception/cancel được ném từ projection đã trả fallback và giữ settlement; chưa có deadline cho projection bị treo sau commit |
| HR-P5-R2-F06 / P3 | **NEW — OPEN** | Đường dẫn predecessorManifest trỏ Phase 4 trong khi predecessorFingerprint là Phase 5 137 entry |

## F02 còn mở — thiếu trường sources làm mất ranh giới chưa xác minh

**Nguồn**: `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs:584–611`,
đặc biệt `var list = parsed.Sources ?? []`.

`Capture` luôn ghi trường `sources`, kể cả danh sách rỗng cho outcome không cần
nguồn. Parser chỉ kiểm shape khi tìm thấy trường đó. Nếu field bị thiếu nhưng
schemaVersion/outcome còn hợp lệ, `Sources == null` được chuyển thành `[]` và
`TryRead` trả true. `ProjectAsync` không còn nguồn nào để kiểm, đi nhánh current
và giữ nguyên body trong ProviderContext. Đây không phải dòng legacy NULL, cũng
không phải một envelope đã xác minh với danh sách rỗng rõ ràng.

**Đã tái hiện bằng hai probe**:

```json
{"schemaVersion":1,"outcome":"answered"}
```

```json
{"schemaVersion":1,"outcome":"answered","scope":"full","capturedAtUtc":"2026-11-07T00:00:00Z"}
```

Sau projection ProviderContext, cả hai vẫn có
`MISSING_SOURCES_PRIVATE_BODY`; hai test `MissingSourcesField_MustNotReplayAnUnverifiableBody`
đỏ. Không có HTTP provider thật; đây là bằng chứng tại contract projection nội bộ.

**Sửa tối thiểu**: yêu cầu cấu trúc envelope hiện hành đủ field bắt buộc; `sources`
phải có mặt và là array theo cùng chính sách tên field của serializer. Thiếu field
phải đi ApplyUnverified, loại body cũ khỏi ProviderContext. Giữ riêng legacy NULL
và envelope hợp lệ với `sources: []`; không cần migration hoặc rewrite dữ liệu cũ.

## F05 còn mở — catch exception chưa đặt hạn chờ sau commit

**Nguồn**: `TCTEnglish/Services/AI/AiChatService.cs:400–410`.

Catch hiện xử lý tốt lỗi được trả hoặc ném ra. Tuy nhiên lời gọi projection vẫn
nhận `CancellationToken.None`, không có CTS/deadline hay cơ chế hạn thời gian
bao quanh await. Nếu truy vấn/adapter chưa hoàn tất, fallback không chạy được và
send/stream cùng conversation lease tiếp tục chờ dù assistant và quota đã commit.
Timeout riêng của một DB command không phải deadline cho toàn projection.

Plan sửa vòng 1 đã yêu cầu một “bounded fail-closed post-commit result”. Test
`ProjectionCancelAfterCommit` hiện mô phỏng ném OperationCanceledException; nó chưa
chứng minh thời hạn dừng của một thao tác đang chờ cancellation token.

**Đã tái hiện**: fake projection có barrier và tôn trọng cancellation token,
Capture dùng service thật. Khi Display projection bắt đầu, quan sát:

- token `CanBeCanceled = false`;
- sau 7 giây send vẫn chưa hoàn tất;
- chỉ hoàn tất sau khi reviewer mở barrier;
- sau khi mở barrier: đúng 1 assistant đã lưu, ledger Settled, đúng 1 câu hỏi.

Probe `ProjectionAfterCommit_WhenDependencyWaits_MustHaveAnIndependentDeadline`
đỏ. **7 giây là cửa sổ quan sát của probe, không phải SLA mới**. Source xác nhận
không có deadline ở boundary này. Probe luôn mở barrier trong finally để không
để lại task hay workload bị treo.

**Sửa tối thiểu**: chọn và ghi rõ deadline hữu hạn phù hợp cho projection của lượt
đã chấp nhận; truyền token độc lập có hạn qua các I/O liên quan. Deadline/cancel/lỗi
phải trả fallback an toàn, giữ identity/assistant/envelope và settlement một lần.
Không fallback raw text chưa được authorize; không bỏ lại task dùng DbContext đã
dispose. Thêm regression chờ cancellation thật ở HTTP/service và stream boundary.

## F06 mới — P3: tham chiếu tiền nhiệm sai snapshot

**Nguồn**: `docs/feature-audits/hybrid-rag/phase-5/scope-manifest.json:818`.

`predecessorManifest` trỏ `phase-4/scope-manifest.json`, fingerprint của file đó là
`1073d507...`. Trong cùng manifest, `predecessorFingerprint` đúng là snapshot
Phase 5 đã review `01b58fe2...`. Đường dẫn và fingerprint không chỉ cùng một tiền
nhiệm. Điều này làm người chạy vòng sau dễ kiểm nhầm prerequisite.

**Sửa tối thiểu trong manifest vòng tiếp**: dùng tham chiếu lịch sử chính xác nếu
có artifact thật, hoặc bỏ/đổi nhãn trường đường dẫn gây hiểu nhầm và trỏ rõ report
review kèm fingerprint. Không tạo một manifest 137 entry bằng hash hiện tại để
giả lập lịch sử. Đây là lỗi truy vết, không phủ nhận việc 140 hash bàn giao khớp.

## Kiểm chứng độc lập và giới hạn

| Kiểm tra | Kết quả |
| --- | --- |
| Manifest/branch/HEAD | 140/140 khớp và fingerprint đúng; giữ nhánh/HEAD bàn giao |
| Build app/test | Cấu hình riêng `Phase5Review` build thành công, có warning sẵn có |
| Focused | **70 passed / 2 skipped / 0 failed**, total 72, gồm 10 regression sửa vòng 1 |
| Reviewer probes mới | **3 failed / 0 passed / 0 skipped**, xác nhận phần còn mở của F02 và F05 |
| Node history/submit | **7 passed / 0 failed** |
| Full suite bàn giao, loại đúng lớp probe reviewer | **1118 passed / 22 skipped / 0 failed**, total 1140, 3 phút 52 giây; skip gồm MariaDB/live/scale opt-in, không tính là pass |
| MariaDB 10.5.23 / pending model | Fresh disposable `tct-migration-check-phase5review2`, DB `tct_migration_check`, `127.0.0.1:33323`, lower_case_table_names=1; đủ **15 migration**, chạy lặp không đổi history; EF 9.0.4 no pending model changes |
| Bảo toàn dữ liệu trên MariaDB | User 900001 checksum **3269365640** không đổi; fingerprint legacy message/conversation **76164d37220f9b7dcf0040d1199e383681a80ada0187e47abb90e667bd201be9** trước/sau khớp; body `legacy-review-body`, GroundingMetadata NULL, FK còn |
| Cột thực tế | GroundingMetadata longtext / nullable / utf8mb4; không sinh migration mới |

Lệnh focused dùng filter Phase5/Phase4/AiCancelAndStop/AiQuota/OpenAiConsumerPipeline.
Lệnh full: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase5Review --no-build --no-restore --filter FullyQualifiedName!~AiRagPhase5ReviewRound2ProbeTests`.
Các skip vẫn là skip. Không dùng SQLite để thay bằng chứng schema MariaDB.

Không thêm migration. Không sửa Program.cs, cấu hình, project files, dữ liệu học
liệu hay source thuộc snapshot bàn giao. Không gọi API trả phí hoặc làm Git/CI/
deploy từ xa. Không có browser hoặc live semantic acceptance mới. DB dev cổng
3307 và workload của owner được giữ nguyên.

## Bàn giao

- [Plan sửa vòng 2](../../../superpowers/plans/hybrid-rag/phase-5-fix-round-2.md)
- [Prompt sửa vòng 2](../../../../.ai/prompts/hybrid-rag/phase-5-fix-round-2.md)
- [Review state](../../../superpowers/plans/hybrid-rag/review-state.md)

Vòng sửa tiếp chỉ xử lý F02/F05 còn mở và F06 truy vết; giữ regression F01/F03/F04
đã xanh. Sau owner launch, hoàn tất phải trả AWAITING_REVIEW; không tự duyệt,
không triển khai Phase 6.

Closeout: sau khi ghi artifact, **140/140 vẫn khớp**, fingerprint và byte manifest
bàn giao giữ nguyên; nhánh/HEAD không đổi. Link Markdown mới hợp lệ,
`git diff --check` sạch. Probe compile copy đã gỡ, evidence còn trong ignored
round-2. Container disposable của review được dừng sau kiểm tra đúng ID/name;
không xóa DB/container, không đụng cổng 3307. Review không sửa implementation.
