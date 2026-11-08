# Phase 7 — review envelope và kiểm tra trước API

Ngày review: **2026-11-08**, Asia/Bangkok. Kết quả: **CHANGES_REQUESTED** cho các
việc local bên dưới; Phase7 vẫn **BLOCKED** trước API trả phí. Không APPROVED.
Không chọn/ủy quyền A hoặc B thay owner. Không có Phase8.

Nhánh `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`. Đã tính lại Phase6 **153/153** và
Phase7 **155/155** raw hashes, không drift. Fingerprint input Phase7:
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`.
Fingerprint Phase6 đã duyệt giữ nguyên:
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.

Lượt này chỉ review và tạo tài liệu sửa. Không sửa product/test/config/model/
migration; không start Docker/web, không reset DB, không gọi text/embedding/speech,
không commit/push/CI/deploy. Bản manifest nộp giữ nguyên; review/plan/prompt là
artifact mới ngoài input155, cần đưa guidance mới vào scope của lượt sửa sau.

## HR-P7-R1-F01 — P1: dự phòng retry B chưa có token admission khả thi

**Vị trí**: `dry-run-envelope.md:45–57,108–112`;
`VectorIndexService.cs:107–124,360–385`;
`EmbeddingOperationBudget.cs:41–64,87–96`.

Phép cộng169087 =113887+55200 đúng cho byte đầu vào duy nhất của index cộng
reserve12chat. Nó chưa bao gồm lượt embed đã gửi nhưng không biết usage rồi gửi
lại. Request budget và token budget phải cùng chấp nhận một retry;20POST không
phải dự phòng đầy đủ nếu token admission chặn trước.

Đọc snapshot thật và tính lại cùng thứ tự batching cho kết quả nominal đúng như
bàn giao: job1=66chunk/2POST/38066byte; job2=85/2/39951; job3=336/6/35870.
Nhưng batch đầu là **36276byte**. Khi response mất sau send, ledger giữ36276;
job40000 chỉ còn **3724**, không thể reserve36276 cho retry. Job có thể dừng failed
trước khi batch đó thành cache; không được hứa hoàn thành đúng3job/20POST trong
trường hợp này. Code chặn budget đúng; đây là lỗi mô tả/phương án chạy.

Nếu minh họa mỗi input được gửi hai lần và lần đầu chưa biết usage, phần embed là
**227774**, cộng55200 thành **282974**. Đây chỉ là phép tính counterexample,
**không phải ngân sách đã xin/được duyệt, cũng không bảo đảm số job/batch hiện tại**.
Ngay cả tổng ba trần job40000 cộng12chat cũng là175200, khác169087; số reserve,
known usage và unknown hold cần tách rõ.

**Sửa cần thiết**: chọn policy retry/unknown cụ thể; tính lại batch/job theo ledger
thật, giới hạn duration và tổng run; có guard trước từng HTTP attempt xuyên các
job/turn/retry. Hoặc ghi rõ trần chỉ là hard stop cho một phần công việc, có thể
dừng partial, không bảo đảm build xong. Không nâng cap40000/12 âm thầm hoặc coi
usage mất là0. Chỉ xin duyệt envelope mới sau nonpaid simulation.

## HR-P7-R1-F02 — P1: chưa có protocol thực thi nối ca synthetic với máy/corpus

**Vị trí**: `demo-script.md:3–8,18–23`; `dry-run-envelope.md:15–16,82–104`;
fixture `hybrid-rag-evaluation.json` actor/context/source IDs;
`HybridRetrievalService.cs:141–175`; Phase7 plan mụcA/B/C.

Fixture dùng `actor-lan-standard`, businessDate **2026-11-06**, corpus
`synthetic-phase0`,51nguồn có id `src-*`. Envelope chạy12câu với Premium/Admin
trên corpus local487chunk nhưng chưa chỉ ra seed/adapter, actor/quyền, đồng hồ
nghiệp vụ, mapping source/chunk và expected facts trên môi trường đó. Không thể
coi một câu hỏi fixture đã được chạy đúng chỉ vì gửi nguyên câu lên tài khoản
khác. Không đưa fixture vào DB owner để làm khớp số liệu.

Với vector index thiếu, code trả `LexicalDegraded` trước lời gọi query embedding;
các ca đã có câu trả lời deterministic còn kết thúc sớm hơn. Vì vậy A có thể là
smoke/degraded rehearsal, có thể không tạo embedding call nào; không phải bằng
chứng live dense/hybrid. Không ép gọi model để đủ số lượng.

Envelope đã thành thật ghi A/B chưa đủ40held-out/62answered: giữ thông tin đó.
Tuy nhiên,12ca là mẫu số của **đợt thử giới hạn**, không thay mẫu số nghiệm thu
Phase7 hoặc yêu cầu ít nhất20online turn của plan khi budget cho phép. B cũng
chưa chứa phần query evaluation có provenance để so lexical/dense/hybrid.

**Sửa cần thiết**: tách frozen synthetic evaluation và actual-corpus rehearsal;
khóa actor/role/date/state/source mapping trong môi trường disposable, và nêu
các phép đo/denominator theo từng suite. Dùng20online turn hoặc ghi rõ partial và
tiếp tục BLOCKED cho final acceptance. Giữ quota ngày60000 và Standard15câu;
không đổi role/bypass quota để làm đúng kỳ vọng fixture. Không tune held-out.

## HR-P7-R1-F03 — P2: hai regression còn phụ thuộc scheduler/wall clock

**Vị trí**: `OpenAiProviderClientTests.cs:138–154`;
`OpenAiProviderClient.cs:40–43,149–166`.

TRX gốc xác nhận focused382pass/3skip/**1fail**: backoff expected1call, actual2.
Test dùng `CancellationTokenSource(60)` còn backoff default250ms, không có barrier
xác nhận đã vào backoff. Full suite1135pass/22skip/**1fail**: error timeout đúng,
Stopwatch2.8705035s ngoài0.8–2.5s. Chạy riêng xanh không xóa failure dưới tải.

Source có token checks trước send và linked deadline cho cả request. Chưa có
bằng chứng từ hai failure này rằng provider vượt quyền sau khi token đã thật sự
cancel. Đây là test timing nhạy tải, cần tái hiện bằng coordination/clock kiểm soát
được trước khi kết luận product bug. Test deadline hiện chỉ dùng SlowContent ở
attempt đầu, còn chưa thực thi retry để chứng minh phần “DoesNotResetForRetry”.

**Sửa cần thiết**: điều khiển đúng giai đoạn send/body/backoff/deadline, chứng minh
caller cancellation chặn request tiếp và deadline không reset qua retry. Có thể
dùng test synchronization/clock seam hẹp nếu cần, giữ default production behavior.
Không nới2.5s, bỏ assert số call, skip, quarantine hoặc tắt parallel toàn suite.
Sau sửa chạy nhóm và full suite; không thay bằng nhiều lần chạy riêng.

## B01 — preflight không trả phí còn thiếu và có blocker môi trường

Chưa có embed-dry-run với MariaDB đang phục vụ, nên checksum file không chứng minh
DB vẫn khớp snapshot. Chưa có cold-start/browser/network rehearsal Phase7.
Reviewer kiểm tra Docker API và nhận lỗi pipe `dockerDesktopLinuxEngine` không
tồn tại. Không có dry-run DB thành công trong lượt review; không start daemon hoặc
tuyên bố `corpus-drift`/`ready` khi chưa đọc DB. Cần khôi phục môi trường local và
chạy các bước này bằng launcher chuẩn, fake loopback settings ở process con.
Các bước này không cần quyền trả phí và nên hoàn tất trước envelope mới.

## Kiểm tra reviewer và giới hạn

- Rehash Phase6/Phase7:153/153 và155/155 khớp; predecessor đúng.
- Đọc source hiện hành qua CodeGraph cho provider deadline/retry, job resume,
  budget ledger và missing-index retrieval. Xác nhận code **có** partial-job/cache
  resume; không kết luận sai rằng corpus>40000 phải bị từ chối toàn bộ.
- Đọc snapshot/checksum:471documents,487chunks,113887byte; cùng checksum nộp.
  Đây là file-only check, không thay DB dry-run.
- Đọc TRX gốc để xác nhận cả2failure và tổng bộ test đã nộp.
- Chạy `OpenAiProviderClientTests|AiRagPhase3CloseoutTests` trên binary
  Phase7Acceptance: **49passed,0skipped,0failed**. Có unknown-usage retry-admission
  và partial-build/cache-resume regression. Kết quả hẹp này không biến full suite
  đỏ thành xanh. Không chạy lặp full suite khi chưa sửa nguyên nhân.
- Evidence mới ở ignored `.tmp/hybrid-rag-review/phase-7/review-round-1/`:
  manifest input, `budget-recalculation.json`, `retry-counterexample.json`,
  `phase7-review-provider.trx`. Không có credential/provider payload.

## Quyết định chuyển tiếp

Về mục tiêu dense/hybrid, hướng B phù hợp hơn A, nhưng chưa nên duyệt con số
80POST/169087 như kế hoạch có retry và hoàn thành Phase7. Trước tiên sửa local
F01–F03, hoàn tất preflight khả dụng và trả envelope mới có phạm vi rõ ràng.
Đây không phải đề nghị tự nâng ngân sách lên282974 hoặc cho phép chi tiền.

Giữ Phase7 BLOCKED, latest review CHANGES_REQUESTED. Thực thi sửa cần owner launch
riêng; review này không tự sửa product/test. Tài liệu cho lượt sửa:
- [Repair plan](../../../superpowers/plans/hybrid-rag/phase-7-fix-round-1.md)
- [Execution prompt](../../../../.ai/prompts/hybrid-rag/phase-7-fix-round-1.md)
