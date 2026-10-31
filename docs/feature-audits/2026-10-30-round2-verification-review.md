# Review kết quả sửa vòng 2 của Cursor

Ngày: 30/10/2026 (UTC+7). HEAD `12ccec5`, source CI `559ae9a` (runtime tree
giống nhau). Nhánh `feature/bao-chau-ui-audit`. Một agent review inline, không sửa
app/tests/workflow, không chạy DELETE hoặc bootstrap trên DB thật.

## Kết luận

Các ca CR01/CR04/CR05 được nêu ở review trước đã được xử lý đúng trong code và
qua probe độc lập. Blocking barrier của CR03 đã được thay bằng async rendezvous;
test có thêm assert replay/credit/outbox. Tuy nhiên **CR02 chưa đóng hoàn toàn**:
guard offline đã tốt hơn, nhưng sentinel được fixture tạo/ghi đè trước khi verify.
Đợt code tiến bộ rõ; chưa đủ điều kiện nghiệm thu toàn bộ engine/browser/staging.

## S01 — P2: Sentinel bị tự tạo/ghi đè trước khi kiểm tra danh tính target

Vị trí chính: `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceFactory.cs:50`–51.
Implementation: `MariaDbAcceptanceConnectionGuard.cs:137`–169; dòng 163 có
`ON DUPLICATE KEY UPDATE purpose = VALUES(purpose)`.

Factory đã reject remote/dev/hosting database, port3307 và tên sai trước host
build. Phần đó sửa đúng. Nhưng InitializeAsync mở connection rồi gọi:

1. EnsureSentinelAsync: CREATE TABLE IF NOT EXISTS và INSERT/UPSERT purpose chuẩn.
2. VerifyOnlineIdentityAsync: đọc lại đúng purpose vừa ghi.
3. Tiến hành DELETE các bảng trong database acceptance.

Do vậy sentinel không còn là bằng chứng target được bootstrap/ủy quyền trước
fixture. Một DB có tên acceptance đúng contract nhưng thiếu sentinel hoặc có
sentinel của suite khác vẫn bị chuyển thành target hợp lệ rồi reset dữ liệu.

Probe gọi source guard thật bằng DbConnection giả, không DB/network, cho kết quả:

| Sentinel lúc đầu | Verify trực tiếp | Sau Ensure → Verify như factory |
| --- | --- | --- |
| Không có | Từ chối | **Chấp nhận**, đã CREATE/INSERT sentinel |
| purpose=`unrelated-suite-must-not-reset` | Từ chối | **Chấp nhận**, purpose bị UPSERT thành final-acceptance-g03 |

Đây là lỗ hổng guard còn lại trong phạm vi database acceptance, mức rủi ro thấp
hơn vòng trước: các database dev/hosting thông thường đã bị offline guard chặn.
Review không suy diễn fixture hiện vẫn DELETE được mọi host/DB như phiên cũ.

Sửa: tách bootstrap có chủ đích ra khỏi InitializeAsync. Fixture chỉ verify
database + sentinel trước mọi write; thiếu/mismatch phải dừng. Bootstrap không
được upsert mục đích khác thành đúng; run-specific identity và lifecycle DB riêng
phải được kiểm chứng. Thêm tests orchestration cho missing/wrong sentinel, assert
không CREATE/INSERT/DELETE/seed; giữ các offline guard tests đã pass.

## Đối chiếu năm phát hiện cũ

| ID | Kết quả kiểm tra lại |
| --- | --- |
| CR01 Writing | `<5`/`>5`, `1,000`/`1.000`, currency/percent khác trả NeedsReview. Formatting `i like tea!` vẫn Accepted. Corpus đã đổi numeric comma/dot chưa được duyệt thành NeedsReview |
| CR02 fixture | Offline guard và same-connection cleanup/finally đã thêm; S01 khiến online identity chưa đạt |
| CR03 concurrency | Không còn sync Barrier. Hai worker có await gate + timeout, loser phải Replay; correctness trên provider thật vẫn cần chạy lại sau S01, matrix lease/import chưa thực hiện |
| CR04 publisher | Resolve actual tag SHA, cả annotated tag; thiếu digest thì tải/hash bytes. Probe sai SHA/missing URL bị reject; matching digest/bytes được reuse |
| CR05 readiness | Marker có run+commit+digest, cache check; sameSHA/run cũ bị reject; webhook ambiguous chuyển reconcile. Tests có regression tương ứng |

Không coi các kết quả này là một audit bảo mật toàn sản phẩm hoặc mọi biến thể
ngôn ngữ. Phạm vi là source diff vòng 2 và các trigger cụ thể trong review trước.

## Gate nghiệm thu còn mở

- G01: tiny.en (~74MiB) đã tải và ghi SHA256/license; chưa dựng whisper-server
  và chưa E2E WAV → assessment → progress/credit, latency/memory/mic thật.
  “Chưa có binary/image” là bước setup còn thiếu, không tự chứng minh đã hết
  khả năng làm trong phạm vi local engine được owner giao trước đó.
- G03: mới dual-completion theo ledger; notification/import/lease expiry/outbox
  matrix rộng hơn vẫn Not run. Không dùng migration smoke thay concurrency.
- G04: browser matrix vẫn các nhóm AU/SP/... chứ chưa có evidence cho 25 view;
  acceptance host 5110 chưa được dựng, desktop/mobile/keyboard chưa chạy.
- G06: enabled Windows staging và failure/restore drill chưa chạy. Rotation/
  hosting ASR/provider là phần cần phối hợp; gate vẫn false, không deploy thật.

## Kiểm chứng độc lập trong lượt này

- App Debug build mới: **0 lỗi, 35 warning sẵn có**, 18,02 giây.
- Focused Writing/corpus/persistence, guard và marker: **83 pass / 0 fail / 0 skip**.
- Deploy guard/publisher tests trên Windows: **14/14 pass**.
- Tooling validator và toàn bộ Python/script checks: **55/55 pass**. Ba tài liệu
  bàn giao kiểm tra link, code fences và whitespace đều đạt.
- Probe source thật qua connection giả tái hiện S01; không gọi DB/network.
- Probe publisher offline không mock chính resolve_tag_commit_sha hoặc hash
  fallback: tag SHA sai/missing proof reject, annotated tag/matching bytes reuse,
  oldrun reject/currentrun accept. Probe Writing source mới trả kết quả như bảng.
- GitHub [run 41092509174](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/41092509174)
  đúng SHA 559ae9a, SUCCESS: **710 pass / 1 skipped / total 711**. Skip là test
  MariaDB concurrency. Script suite Linux 14 tests có 2 Windows-only skip. Release skipped.
- Không rerun destructive MariaDB fixture trước khi guard được sửa. Không chạy
  lại fullsuite khi runtime chưa thay đổi và CI cùng tree đã xác minh.
- Artifacts ignored: `.tmp/round2-rereview/`. Không sửa source production/tests,
  không commit/push trong review. PLESK_DEPLOYMENT_ENABLED kiểm tra vẫn false.

Plan: [sentinel và nghiệm thu còn lại](../superpowers/plans/2026-10-30-sentinel-and-acceptance-plan.md).
Prompt: [Cursor Auto](../../.ai/prompts/cursor-auto-sentinel-and-acceptance.md).
