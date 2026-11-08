# Phase 5 — Codex review round 3

Ngày review: 07/11/2026. Kết luận: **APPROVED** cho snapshot 143 entry nêu dưới.
**Phase 5 APPROVED; Phase 6 READY; Phase 7 LOCKED.**

## Snapshot và phạm vi

- Nhánh `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`.
- [Manifest sửa vòng 2](scope-manifest.json): tính lại từ raw bytes,
  **143/143 hash khớp**, fingerprint ordinal có LF cuối:
  `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
- Tiền nhiệm đúng là snapshot 140 entry đã review:
  `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`,
  tham chiếu [review round 2](review-round-2.md). Có bản manifest 140 đầu vào ở
  ignored evidence của round 2 để đối chiếu hash, không suy đoán lịch sử từ file mới.
- So với snapshot 140: code ứng dụng chỉ đổi `AiChatService.cs` và
  `GroundingCitationService.cs`; thêm test sửa vòng 2, plan/prompt và cập nhật
  bug log/README. Không xóa entry. **10 entry persisted model/migration/DI trong
  scope giữ nguyên SHA-256**, gồm entity, DbContext, migration, Designer, snapshot
  và Program.cs liên quan. Không thêm migration hoặc sửa cấu hình/project.
- Review cùng một agent, CodeGraph trước cho source; đối chiếu acceptance của
  Phase 5 và sửa vòng 2. Không triển khai Phase 6. Source/test/manifest bàn giao
  được giữ; chỉ ghi review/state. Probe compile copy dùng xong được gỡ.

## Kết quả từng finding

| Finding | Kết quả review round 3 | Cơ sở |
| --- | --- | --- |
| HR-P5-R1-F01 / P1 | CLOSED — giữ kết quả round 2 | History project từng batch trước chi phí token và mọi return; regression budget/batch/zero giữ xanh, không thay implementation này trong sửa vòng 2 |
| HR-P5-R1-F02 / P1 | CLOSED | Parser bắt buộc đúng một sources array, tên field không phân biệt hoa thường; thiếu/null/object/deep/future/corrupt không replay body trong ProviderContext; explicit [] và legacy NULL giữ riêng |
| HR-P5-R1-F03 / P2 | CLOSED — giữ kết quả round 2 | Kiểm shape và null source trước dereference; lỗi parser đi policy unverified; HTTP history/send xanh |
| HR-P5-R1-F04 / P1 | CLOSED — giữ kết quả round 2 | Provenance từng sourceKey+sentenceId, kiểm freshness theo nhóm; đổi câu thứ hai/thiếu provenance cũ không còn replay stale body; display gom link riêng |
| HR-P5-R1-F05 / P2 | CLOSED | Token độc lập AcceptedTurnProjectionBudget=5 giây; deadline thực kết thúc projection đang chờ trên send và stream, trả notice an toàn, giữ assistant/envelope và settle một lần |
| HR-P5-R2-F06 / P3 | CLOSED | Bỏ predecessorManifest sai; predecessorReview trỏ đúng report đã xác nhận fingerprint 140 entry |

Các kết quả trên đối chiếu source và regression đã chạy, không lấy checkbox tự
đánh dấu làm bằng chứng. Full suite và kiểm chứng MariaDB cuối đã hoàn tất.
Không còn finding actionable trong phạm vi acceptance Phase 5 đã review.

## Xác minh các sửa còn lại

**F02** — `GroundingCitationService.TryRead` duyệt property và yêu cầu đúng một
field sources, thống nhất `OrdinalIgnoreCase` với serializer. Thiếu array hoặc
duplicate field được từ chối trước typed materialization; typed Sources null vẫn
bị chặn. Chỉ catch JsonException trong parser, không nuốt lỗi DB/cancellation.
Policy Display unverified vẫn giữ body đã lưu theo contract; ProviderContext thay
bằng notice. Đây khác với legacy NULL và explicit empty array hợp lệ.

**F05** — `AiChatService.SendAsync` dùng CTS riêng 5 giây sau durable acceptance,
await projection trực tiếp rồi mới rời scope/lease. Không race bỏ lại background
task trên DbContext. Token đi qua query card/class/private media, corpus/live
document, guide deserialization và lesson EF queries. Khi deadline hủy thao tác
đang chờ, catch trả unverified notice, không hiển thị nguyên văn chưa kiểm được
quyền và không thêm assistant/resettle/release known usage.

Đây là deadline theo cancellation hợp tác, phù hợp interface và các I/O hiện có;
không phải SLA cứng cho thread bị treo hoặc phụ thuộc không tôn trọng token.

**Ba probe độc lập của reviewer từ round 2 được chạy lại nguyên source**, dùng
SQLite/fake services, không sửa assertion để xanh:

- Hai envelope thiếu sources trước đây giữ private marker; nay ProviderContext
  chỉ còn `Không xác minh được nguồn của câu trả lời này. Không dùng nội dung cũ
  làm dữ liệu hiện tại.`
- Waiting projection nay nhận token `CanBeCanceled=True`, send hoàn tất trước
  khi reviewer mở barrier; đúng 1 assistant, ledger Settled và 1 settled question.
  Probe cũ quan sát trong 7 giây, còn deadline implementation là 5 giây.

Maintained regression sửa vòng 2 kiểm thêm timeout trên streaming service, HTTP
context/paged history, empty array với Sources hoa, legacy NULL và authorized
reload sau fallback. Các lỗi F01/F03/F04 và đường metadata Capture failure tiếp
tục được kiểm qua bộ focused/full.

## Sổ kiểm chứng độc lập

| Kiểm tra | Kết quả |
| --- | --- |
| Input identity | 143/143 hash và fingerprint đúng; branch/HEAD như bàn giao |
| Build | App/test cấu hình riêng Phase5Review build thành công; có warning sẵn có |
| Focused bàn giao | **80 passed / 2 skipped / 0 failed**, gồm các test citation và sửa vòng 1/2 |
| Reviewer probes cũ | **3 passed / 0 failed / 0 skipped**; trước sửa vòng 2 cả 3 đều đỏ |
| Tổng focused có probe | **83 passed / 2 skipped / 0 failed**, total 85 |
| Node history/submit | **7 passed / 0 failed** |
| Full suite bàn giao, loại đúng lớp reviewer probe | **1128 passed / 22 skipped / 0 failed**, total 1150, 3 phút 33 giây; skip gồm MariaDB/live/scale opt-in, không tính là pass |
| MariaDB final binary / pending model | MariaDB **10.5.23**, update lặp không áp dụng thêm migration, 15 history entry giữ nguyên; repo-local EF 9.0.4 **no pending model changes** |
| Bảo toàn dữ liệu/provider | User 900001 checksum **3269365640** giữ nguyên; legacy message/conversation fingerprint **76164d37220f9b7dcf0040d1199e383681a80ada0187e47abb90e667bd201be9** khớp round 2; body legacy-review-body, metadata NULL, FK còn |
| Cột MariaDB thực tế | GroundingMetadata longtext / nullable / utf8mb4 |

Lệnh focused dùng filter Phase5/Phase4/AiCancelAndStop/AiQuota/OpenAiConsumerPipeline.
Lệnh full: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase5Review --no-build --no-restore --filter FullyQualifiedName!~AiRagPhase5ReviewRound2ProbeTests`.
Source probe/TRX/log/script nằm trong ignored
`.tmp/hybrid-rag-review/phase-5/round-3/` và source probe gốc ở round-2.

## Schema và provider evidence

Không có thay đổi persisted model hoặc migration so với snapshot 140. Vì hash
schema/DI trong scope không đổi, review tái sử dụng bằng chứng nâng baseline
MariaDB 10.5.23 độc lập của round 2, rồi khởi động đúng disposable đã được giữ lại
để kiểm history/data và chạy update lặp/pending model trên binary cuối.

Disposable: `tct-migration-check-phase5review2`, container ID
`ef95132fae825bd6fbe04092c935091c9041c8b7ff4ef72d8ee26425e2ddad1b`,
DB `tct_migration_check`, cổng `127.0.0.1:33323`, lower_case_table_names=1.
Không reset hoặc downgrade DB này, không dùng DB dev 3307. Lần thử đầu sau start
gặp DB chưa sẵn sàng; đã thêm bounded readiness wait cho script kiểm tra ignored,
không coi lỗi khởi động đó là lỗi ứng dụng hoặc một lượt kiểm chứng đạt.

## Giới hạn và bàn giao

Không có paid provider call, browser acceptance mới, CI, commit, push, PR hoặc
deployment. Các skip không được tính là pass. Browser/UX/cache/resilience và live
semantic evaluation thuộc các gate sau; review này chỉ chốt acceptance Phase 5.

Trạng thái gate chính thức được ghi ở
[review-state.md](../../../superpowers/plans/hybrid-rag/review-state.md).
README trong digest 143 entry giữ mốc bàn giao của implementer; report/state mới
ghi kết quả review và không thay đổi danh tính snapshot đã nhận.

Phê duyệt chỉ có hiệu lực cho snapshot 143 entry/fingerprint trên và acceptance
local của Phase 5. Phase 6 READY vẫn cần owner launch prompt hiện có
`.ai/prompts/hybrid-rag/phase-6-ui-resilience.md`; review không tự chạy UI/cache,
không mở Phase 7 hoặc cấp quyền API trả phí/Git/CI/release.

Closeout: đọc lại cuối cùng vẫn **143/143 khớp**, fingerprint và byte manifest
bàn giao giữ nguyên; branch/HEAD không đổi. Các link mới hợp lệ,
`git diff --check` sạch. Probe compile copy đã gỡ. Disposable được dừng sau khi
kiểm tra đúng ID/name, giữ dữ liệu synthetic cho đối chiếu; DB dev/workload owner
không bị thay đổi. Không cần thêm repair plan vì không còn finding mở.
