# Phase 4 — Codex review vòng 2 và local closeout

Ngày 07/11/2026. Owner yêu cầu review bản repair và **sửa trực tiếp phần còn lỗi**
để chuyển Phase 5. Một agent thực hiện review, regression và sửa local.
**Phase 4 APPROVED, Phase 5 READY; Phase 6–7 LOCKED.** Chưa triển khai Phase 5.

## Danh tính và quyền

- Branch `feature/hybrid-rag-chatbot`, HEAD đầu/cuối
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, chưa commit.
- Bản repair trả về khớp **113/113 hash**, fingerprint
  `b3406e3068e06442a6319e88a3e0f6e606ce9c18a9e997c0c13f983a3330693f`.
- Tiền nhiệm review vòng 1: 109 entry,
  `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`.
- [Plan closeout đã thực hiện](../../../superpowers/plans/hybrid-rag/phase-4-closeout.md).
- Không sửa Program.cs, appsettings/.csproj, persisted model/DbContext, migration/
  Designer/snapshot hoặc shared content trong closeout. Không thêm migration.
  Các thuộc tính provenance mới thuộc record transient, không thuộc EF mapping.
- Quyền sửa local đến từ yêu cầu owner mới, thay giới hạn review-only trước đó.
  Không thêm quyền API trả phí, Git remote/CI hay release.

## Kết quả từng finding

| Finding | Bản trả về và phần closeout |
| --- | --- |
| HR-P4-R1-F01 | Cursor đã giữ known + unknown qua các ca chính. Còn mất receipt khi caller hủy sau body, chưa persist receipt trước retry embedding, nhầm HTTP 400 đã gửi với preflight, và có thể giữ cả allowance của retry chưa gửi. Các nhánh này đã có regression đỏ rồi được sửa |
| HR-P4-R1-F02 | Envelope/payload byte cap, history/evidence shrink và attempt allowance đúng trên bản trả về; tiếp tục đạt các regression CJK và cap. Closeout giữ hợp đồng đó |
| HR-P4-R1-F03 | Sentence materialization/empty selection đã sửa đúng. Còn document/snapshot identity bị mất khi chuyển sang AiChatAnswer.Evidence; đã bổ sung vào record transient và giữ source version/entity identity |
| HR-P4-R1-F04 | Ca unpublish trong selector đã sửa. Còn nhánh một sentence không recheck sau journal await và không phát hiện thay snapshot với cùng document hash; đã sửa |
| HR-P4-R1-F05 | Begin/receipt trên ledger hết hạn hoặc terminal đã bị từ chối đúng. Còn thiếu guard trên từng HTTP retry; đã nối guard ownership/current-source vào từng send, và kiểm cuối trước compose |
| HR-P4-R2-F06, P2 | Promote lexical/vector dễ fail khi Windows reader giữ file hoặc file con. Đã tái hiện native error 32 khi replace manifest và error 5 khi rename directory; thêm bounded retry cho đúng thao tác atomic, giữ index cũ |

### Receipt, cancellation và số lần gửi

Hai test hủy ở EOF của body đã nhận đủ JSON usage, trước checkpoint cancellation:

- Generation receipt 105 phải được ghi bằng cleanup token độc lập; cùng embedding
  known 17 thì settle **122**, không tạo assistant. MariaDB có thêm test xác nhận
  receipt 105 bền, unknown 0, settle lặp vẫn **1 question / 105 token**.
- Embedding receipt 7 phải tới journal dù retrieval bị cancel trước materialization.
- HTTP 500 embedding có receipt 7, rồi retry: receipt đầu phải có trong journal
  trước POST sau; tổng known **14**, unknown **0** sau hai receipt.
- HTTP 400 đã gửi vẫn giữ unknown của attempt. Chỉ ledger attempt **0** mới là
  không gửi; không suy diễn send từ mã lỗi `invalid_configuration`.
- Khi nguồn bị thu hồi sau POST đầu và retry bị guard chặn, unknown giữ bound của
  **một attempt đã gửi**, không thay bằng allowance của cả hai attempt.

Hai transport có callback cumulative known/unknown, với token cleanup độc lập có
thời hạn. Callback là chat opt-in; non-chat payload/options mặc định giữ hợp đồng.
Receipt được ghi độc lập với shape/selection và trước kiểm cancel sau body.

### Ownership và current source

Guard được kiểm trước từng send/retry. Nguồn hoặc ownership mất sau POST đầu thì
không gửi POST tiếp. Mọi nhánh có sentence đều được recheck trước compose, kể cả
nhánh không cần selector. Evidence giữ snapshot ID; snapshot thay dù document hash
không đổi vẫn bị từ chối. Document hash, snapshot, source version và entity identity
được giữ trong AiChatAnswer.Evidence, cùng sentence hash/route/visibility vốn có.
Tất cả vẫn transient; không triển khai lưu citation hoặc UI Phase 5.

### Lỗi promote không ổn định

Bản bàn giao báo fail promote trong full suite, pass khi chạy riêng. Review cũng
gặp fail promote ở lexical/vector khi chạy filter rộng. Không nới assertion.
Đã bổ sung message diagnostic cho các assertion promote liên quan.

Reproduction độc lập trên Windows giữ reader không cho delete sharing:

- `File.Replace` manifest trả sharing violation **32**.
- `Directory.Move` khi file con đang mở trả **IOException / native 5**.

Giới hạn retry: tối đa **8 lần thử**, tổng backoff tối đa **700 ms**, có cancellation.
Chỉ retry thao tác rename/replace atomic; native 5 chỉ áp dụng Directory.Move.
Không đổi ACL, không retry request embedding và không thay manifest cũ trước khi
atomic promotion thành công. Lỗi I/O khác vẫn được trả về. Test có last-valid index.

Các fail cũ chỉ báo status nên không đủ để quy nguyên nhân chính xác từng lần fail
về native code nào. Những reproduction trên chứng minh cơ chế và bản sửa; kết quả
full suite cuối bên dưới xác nhận cả các test gốc, không dùng isolated pass thay thế.

## Verification

- Bản Cursor đầu vào: focused **178/178 pass** trên output `Phase4Closeout` mới.
- Có **13 regression bổ sung**, được quan sát đỏ trước các sửa tương ứng. Nhánh
  ownership retry được chạy lại đỏ với allowance thật sự đủ cho hai attempt.
- Focused cuối: **191/191 pass**, gồm RAG/provider/cancel/quota/consumer.
- Full suite cuối: **1.100 pass / 22 skip / 0 fail**, 1.122 tổng, 3 phút 15 giây.
  Các skip gồm live API/speech và MariaDB/upgrade/benchmark opt-in; không tính skip
  là pass. Các test promote gốc đều đạt trong lần chạy toàn bộ này.
- MariaDB lifecycle cuối: **3 pass / 0 skip / 0 fail** trên binary cuối, 16 giây.
- Tooling validator exit 0, `git diff --check` sạch; không đổi tooling configuration.
- Không gọi paid API. HTTP handlers, vector và usage là synthetic; không dùng chúng
  làm bằng chứng chất lượng semantic. Live provider/semantic vẫn thuộc Phase 7.

Đã tự kiểm upgrade trên MariaDB **10.5.23**, container disposable riêng
`tct-migration-check-phase4closeout`, loopback **33319**, DB `tct_migration_check`:

1. DB mới rỗng, `lower_case_table_names=1`; nâng tới
   `20260510020134_AddReadingUserTranslation`.
2. Thêm user synthetic 900001; checksum trước/sau upgrade đều **2660695814**.
3. Nâng đầy đủ **14 migration**; update lặp không đổi history hoặc dữ liệu.
4. Repo-local EF pending-model: **No changes**, exit 0. Không generation migration.

Test lifecycle dùng acceptance target được sentinel/run-id guard xác nhận:
`tctenglish-mariadb-acceptance-phase4r1`, port **3338**, DB/run `tct_acceptance_phase4r1`
/ `phase4r1`. Không dùng DB dev port 3307. Credential chỉ lấy trong process memory,
không in, không ghi vào tài liệu/Git hoặc User Secrets.

Lệnh chính:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Closeout --no-restore --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~AiCancelAndStop|FullyQualifiedName~AiQuotaReservation|FullyQualifiedName~AiCloseout|FullyQualifiedName~OpenAiConsumerPipeline'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Closeout --no-build --no-restore
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Closeout --no-build --no-restore --filter FullyQualifiedName~MariaDbAiTurnLifecycleTests
```

Lệnh MariaDB chạy với biến acceptance trong child process và được khôi phục sau đó.
TRX/console/SQL upgrade script và manifest đầu vào nằm tại ignored
`.tmp/hybrid-rag-review/phase-4/round-2/`. Không đổi fixture evaluation/held-out,
không tune ngưỡng để vượt nghiệm thu. Không chạy browser mới vì closeout chỉ đổi
backend; không tuyên bố browser/UI hoặc live model đã được nghiệm thu ở đây.

## Gate

Không còn finding chưa xử lý trong phạm vi Phase 4 đã kiểm. Codex ghi **APPROVED**
cho snapshot cuối, chỉ mở **Phase 5 READY**. Phase 6–7 giữ LOCKED. Phase 5 cần owner
khởi chạy riêng. Không commit/push/PR/CI/deploy hoặc thay môi trường production.

## Snapshot được duyệt

[Manifest cuối](scope-manifest.json) có **117/117 hash khớp**, fingerprint
`1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`. Approval áp dụng các byte sau closeout; manifest, review/handoff
và coordination state được loại khỏi digest để tránh vòng hash. HEAD không đổi.
