# Plan1 repair — review lại và bàn giao Plan2 — 02/11/2026

Một agent review inline, nhánh feature/chau-ui-performance-audit, HEAD210bf2d
+dirty UI. Không sửa runtime của Gemini, không commit/push/CI/merge/deploy.

## Quyết định

**Có thể chuyển sang Plan2 local.** Năm findingR1–R5 của review trước đã được
reviewer kiểm lại bằng browser và source, không còn tái hiện trong các case dưới.
Chưa phải nghiệm thu toàn sản phẩm/production. Hai follow-up nhỏ ở cuối được
giao Cursor làm preflight của Plan2; không mở lại toàn bộ Plan1 cho Gemini.

## Bằng chứng kiểm lại

| Finding trước | Actual trong lượt này |
| --- | --- |
| R1 double submit |Fake fetch pending; native Enter tin1→1call, tin2 Enter+direct requestSubmit→vẫn1call/1bubble, draft tin2 giữ nguyên; error kết thúc mở khóa; tin2 gửi tiếp→call2/bubble2, draft clear đúng lúc. Request bị intercept toàn bộ, không OpenAI.|
| R2 avatar preview |DataTransfer PNG1px: image inline/fallback none; invalid PNG bytes→image none/fallback flex; valid lần nữa→inline/naturalWidth1. Không upload/save.|
| R3 clipping |390×500 log207px/wrapper208.67, last bottom449.08<=456.61;844×390 log44/wrapper46.42, last bottom358.55<=368.41;390×844 log469/wrapper471.25;1440×900 log466/wrapper468.45.20rows DOM, scrollTop=max, last line visible tất cả.|
| R3 composer/full+embed |390×500 textarea6lines height120: full log142/wrapper143.67 và embed log347/wrapper348.81, last line visible ở max scroll.|
| R4 Main |390 closed inert=true, open inert=false/expanded=true/focus inside; Escape inert=true/expanded=false/focus trigger; hidden link không focus được. Resize1440 inert=false/link focus được.|
| R4 Account |Profile/Settings390 closed/open/Escape: inert+expanded đồng bộ, focus về trigger.|
| R4 Admin |390 closed/open/Escape: inert+expanded đồng bộ, focus về trigger.|
| R5 Admin skip |390 focused skip hit-test đúng anchor, không bị navbar che.|

Điều kiện: web Development Release5112 hiện có (Razor runtime compilation), static
JS/CSS mới; fixture account local mới, Standard rồi promote chỉ fixture bằng admin
endpoint để kiểm Admin. Không mail/YouTube/paid cloud/training. Test viewport là
Chrome emulation, không keyboard/microphone vật lý hoặc kiểm accent người thật.

## Build/tests

- Fresh app+test project bằng `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj
  -c Plan1Review -p:Optimize=true --logger "trx;LogFileName=plan1-ui-rereview.trx"
  --results-directory .tmp/plan1-ui-rereview --verbosity quiet`.
- Cloud live flags0. **811passed,0failed,8skipped,total819**,2m10s. TRX ignored.
  Skips vẫn2OpenAI live,3ASR/media live,3MariaDB acceptance; không phải819pass.
- `node --test scripts/tests/test_ai_chat_submit_guard.cjs`:4/4pass. Đây là4
  source-pattern assertions; không gọi chúng là pending-promise behavior tests.
- git diff --check pass. Core config/backend/model/migrations không đổi.
- Ledger đã sửa bảng status/mapping finding, ghi compiled-binary101 cũ là lịch
  sử và tách verified source/browser. Câu 'toàn bộ suite100%' trong repair ledger
  vẫn nên đổi thành811executed pass/8not run khi Cursor cập nhật handoff.
- Repo variable PLESK_DEPLOYMENT_ENABLED=false lúc review; không remote stages.

## Follow-up không chặn bắt đầu Plan2

### F1 — P2: Admin mất menu tại đúng992px, đã có từ baseline

_AdminLayout CSS max-width992 ẩn sidebar; toggle d-lg-none bị Bootstrap ẩn từ992.
Runtime991: sidebarx=-260,inert=true,togglewidth42;992: sidebarx=-260,inert=true,
toggle display:none/width0;993/1440: desktop inert=false. Transition993 đang animate
nên không dùng x trung gian làm lỗi; breakpoint/inert/button kết quả rõ tại992.

Đã đối chiếu git show HEAD: CSS max-width992 và d-lg-none tồn tại trước repair.
Không quy lỗi mới cho Gemini; cần sửa trước nghiệm thu responsive cuối. Đồng bộ
CSS và matchMedia Admin theo Bootstrap lg (mobile max991.98), verify991/992/993.

### F2 — P2: test mới chỉ dò source, chưa bảo vệ hành vi R1

scripts/tests/test_ai_chat_submit_guard.cjs dùng assert.match regex cho4case,
không execute init/submit và không kiểm request count/draft/retry. Có thể pass
khi guard nằm sai thứ tự hoặc không bảo vệ đúng hành vi. Reviewer browser đã
chứng minh fix hiện tại hoạt động; cần behavior regression để bảo vệ khi sửa BE2.

Cursor preflight đổi sang chạy productionJS trong harness fake DOM/transport,
pending promise và events; assert1request, draft/bubble giữ, unlock/retry/IME.
Không tạo package/browser dependency mới chỉ để chạy test này.

## Handoff

- [Preflight plan](../superpowers/plans/2026-11-02-plan2-handoff-preflight.md).
- [Cursor prompt bắt đầu Plan2](../../.ai/prompts/cursor-plan2-handoff-preflight.md).
- Giữ toàn bộ UI diff; preflightF1/F2 rồi BE0–BE8 của Plan2 hiện có. Không tự
  xây phoneme engine/Writing semantic hoặc mở production gate.
- Account fixture id63 chỉ dùng riêng cho review đã xóa bằng own DeleteAccount
  sau identity guard, redirect Login200; credential sessionStorage đã xóa.
  Giữ dữ liệu/tài khoản có trước. Validator5tài liệu/handoff và5relative links
  hợp lệ; git diff --check sạch. Không sửa source runtime trong lượt review này.
