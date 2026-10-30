# Plan tối ưu nạp ngữ cảnh của agent

Ngày 2026-10-30. Đọc báo cáo `docs/feature-audits/2026-10-30-agent-context-efficiency-review.md` theo mục cần biết; không nạp lịch sử Learning để làm task tooling này. Một agent, giữ mọi diff của tác vụ ứng dụng đang chạy.

## A0 — Chẩn đoán, đã thực hiện

- [x] Kiểm tra MCP config/tool exposure theo host, chỉ xuất metadata không chứa secrets.
- [x] Truy CodeGraph trước, phân biệt source graph còn mới và file tooling ngoài index.
- [x] Pinned CLI status: ghi số pending và engine index; không sync/index.
- [x] Kiểm prompt, canonical skill/adapters, ba role, SessionStart/Pre/Post/Stop.
- [x] Phân biệt Context7 trong snapshot hướng dẫn với server/config/tool thực sự.

## A1 — Quy tắc nạp theo nhu cầu, đã thực hiện

**File:** AGENTS.md, `.ai/README.md`, `.agents/skills/tct-engineering/SKILL.md`, `tct-superpowers/SKILL.md`, `.ai/roles/implementer.md`.

- [x] Reuse instructions/source còn mới trong chat, không đọc lại qua mỗi adapter/skill.
- [x] Bắt đầu CodeGraph bằng exact symbols/maxFiles 1–3; expand theo gap được nêu rõ, không query toàn hệ thống.
- [x] Chỉ bounded reads khi stale/unindexed/edit/gap; không hard cap số file làm bỏ regression hoặc kiểm quyền.
- [x] Context7 có điều kiện, đúng version/API và reuse ID; không private source/log, không giả MCP call khi tool không có.
- [x] Phân cấp docs thành lookup hierarchy; bug memory lấy newest/matching sections; role/vendor on-demand.

**Đạt:** các entrypoint không yêu cầu đọc cả catalog/history và không cản fallback chính đáng. Giữ single-agent, access control và verification.

## A2 — Prompt đang dùng, đã thực hiện

**File:** `.ai/prompts/cursor-grok-learning-remediation-round2.md`, `docs/superpowers/plans/2026-10-30-learning-remediation-round2-plan.md`.

- [x] Startup chỉ lấy scope/quyền/thứ tự; đọc section C đang làm và finding RR tương ứng.
- [x] Plan cũ/ledger lịch sử chỉ tra khi thiếu contract; không giảm số bước C0–C9 phải hoàn thành.
- [x] Thêm routing CodeGraph/Context7 và reuse trong chính prompt, để phiên Cursor hiện tại có thể được nhắc áp dụng mà không tải lại cả lịch sử.

**Đạt:** agent nói được bước hiện tại và câu hỏi còn thiếu, thay vì mở thêm tài liệu theo danh sách máy móc.

## A3 — Hook, đã thực hiện

**File:** `scripts/agent_lifecycle.py`, `docs/agent-hooks.md`.

- [x] SessionStart pointer nhắc active step/reuse/MCP purpose, giữ ngắn.
- [x] Giữ nguyên native definitions/translation/guard/cache; không tự trust/reload host.
- [x] Không chặn mọi read và không gọi Context7 hoặc CodeGraph tự động mỗi tool call.

**Đạt:** bốn host output contracts vẫn qua smoke checks, read-only operations không bị ép nạp thêm context. Không claim hook đã tự chạy ở mọi IDE từ test script.

## A4 — Kiểm chứng và bảo toàn

- [x] Tooling validator, 41 Python tests, 9 Node setup tests, four-host smoke và CodeGraph read-only smoke đã qua.
- [ ] Agent tiếp theo quan sát một tác vụ thực tế: reuse source đã nhận; chỉ load active step; ghi ngắn ví dụ fallback stale/missing. Không mở thêm source chỉ để chứng minh ít đọc.
- [ ] Chỉ đo token khi có usage log/cùng model/cùng scope; không suy mức tiết kiệm từ số tool calls hoặc hứa phần trăm.
- [x] Build snapshot trong lượt A5 đã qua, 0 errors; lỗi compiler ở audit trước là lịch sử. Không sửa đè ứng dụng để làm tooling xanh và không coi build này là nghiệm thu toàn bộ Learning suite.

## A5 — Index CodeGraph, đã duyệt và triển khai ngày 2026-10-30

Owner đã giao rõ backup, rebuild 1.6.2 và incremental auto-sync cho riêng checkout
hiện tại, cho phép sửa rule/config liên quan. Đã triển khai launcher/policy/gate,
tests và docs; không sửa app/package, không apply DB thật hoặc commit/push.

- [x] Owner duyệt đầy đủ phạm vi A5; cài package hay sửa skill không tự cấp quyền cho checkout khác.
- [x] SQLite backup API, quick_check và SHA-256 xác minh; chứa committed WAL, lưu ignored.
- [x] Rebuild bằng pinned CLI 1.6.2; 577 file ban đầu, metadata indexed_with_version=1.6.2.
- [x] Status up to date và query symbol mới qua MCP; project-mode live smoke chỉ expose explore.
- [x] Fixture trước activation: default read-only, create/edit/delete, debounce, catch-up, hai client một daemon, chặn ngoài checkout/tool khác và rollback read-only.
- [x] Local opt-in `.ai/codegraph.local.json` bound root/version, được Git ignore; mặc định các checkout khác không đổi.

**Đạt:** index hiện tại cập nhật, incremental updater đã được gọi trên checkout
thật. Phiên IDE đang dùng launcher cũ cần reconnect MCP để nhận routing mới;
không claim đã reload mọi host. Chi tiết/rollback: `docs/codegraph.md` và
`docs/feature-audits/2026-10-30-codegraph-autosync-handoff.md`.

### Phương án khuyến nghị cho index đang ở engine 1.5.0

Phương án dưới đây đã được owner chọn và thực thi; giữ lại chi tiết thiết kế:

1. Khi tác vụ sửa source hiện tại đã ổn định, backup index SQLite nhất quán rồi **rebuild một lần bằng pinned 1.6.2** để tạo graph đầy đủ theo engine đang cài. `sync` chỉ cập nhật tăng dần; nếu chỉ muốn xử lý phần pending hiện tại, owner có thể chọn sync thay vì rebuild.
2. Để giữ mới về sau, cho phép **catch-up lúc MCP kết nối + watcher sync tăng dần** của CodeGraph. Native watcher có debounce để gộp nhiều lần save; bắt đầu theo mặc định khoảng 2 giây, điều chỉnh bằng đo thực tế. Không full rebuild mỗi lần sửa file.
3. Đổi chính sách rõ ràng: tự sync tăng dần trong checkout đã opt-in; full rebuild/cài package/thay trust vẫn cần yêu cầu riêng. Giữ expose MCP chỉ `explore`, package pin, telemetry tắt và single-agent. Watcher là tiến trình công cụ cục bộ, không gọi thêm model AI.
4. Dùng cơ chế phối hợp/writer-lock của upstream cho các client cùng checkout; kiểm tra thực tế Codex/Cursor/Antigravity mở đồng thời. Không tự dựng nhiều timer/job cùng gọi CLI sync. Nếu owner chỉ chọn máy hiện tại, giữ opt-in cục bộ/ignored và mặc định read-only của các checkout khác; không push database/cache index lên Git.
5. Trước bàn giao thay đổi auto-sync phải bổ sung test riêng trên fixture/index cô lập: create/edit/delete và catch-up sau khi watcher ngừng; tool list vẫn chỉ explore; query mới trả đúng symbol; debounce gộp burst save; tắt opt-in quay lại read-only. Test không sửa source app để thử watcher. MCP smoke read-only hiện tại vẫn phải qua ở chế độ mặc định.
6. Chỉ báo đã bật thành công sau khi host nạp cấu hình và watcher thực sự cập nhật fixture. Ghi CPU/RAM/độ trễ quan sát được; không hứa zero stale hoặc mức tiết kiệm token. Trong lúc sync chưa xong, giữ stale warning và bounded source fallback.

Files triển khai nếu owner chọn auto-sync: launcher/adapter CodeGraph, routing MCP của host được chọn, local opt-in, rule/docs liên quan và tests tooling. Không thay `readOnly` thành writable một cách âm thầm dưới cờ `--mcp-readonly`; giữ đường default read-only rõ ràng.

Đã xác minh CLI help của bản cài 1.6.2 cho `index`, `sync`, `serve --mcp` và `--no-watch`; chưa chạy các action ghi index. Tham khảo cơ chế chính thức: [CodeGraph 1.6.2 auto-sync](https://github.com/colbymchenry/codegraph/blob/v1.6.2/README.md#key-features).

## A6 — Context7, chưa xác nhận cài đặt chạy được

- [ ] Xác định server/plugin ở đúng project/host/profile người dùng đang nhắc tới, không lấy dòng vendor README làm bằng chứng cài.
- [ ] Nếu host có tool, probe bằng một câu hỏi tài liệu công khai đúng version: resolve library rồi query-docs; giữ ID cho các câu hỏi tiếp, không gửi source/private logs.
- [ ] Nếu thật sự thiếu config, nêu đúng chỗ thiếu và đề xuất cấu hình theo tài liệu chính thức. Chỉ cài/thêm server khi owner giao việc đó; không sửa mọi client hoặc thêm key vào Git. Không dùng npx @latest/global installer như tác dụng phụ của prompt.
- [ ] Kiểm tool exposure sau reload/trust do host yêu cầu, rồi mới báo Context7 sử dụng được. Không sửa trust database hay thay account của owner.

**Đạt:** phân biệt Installed/configured/available/call verified theo host; còn thiếu thông tin thì ghi rõ, không gọi việc cài thêm lần nữa là tối ưu token.

## Giới hạn và bàn giao

Audit ban đầu không ghi index; lượt A5 sau đó đã được owner cấp quyền riêng và
hoàn thành backup/rebuild/checkout-local auto-sync. A6 Context7 vẫn ngoài phạm vi
lượt A5, chưa cài thêm. Không triển khai lại Learning hoặc apply migration. Dùng
`.ai/prompts/cursor-context-efficient-workflow.md` để nhắc agent đang làm việc;
prompt đó không tự cấp quyền sửa thêm domain, init hoặc full rebuild index.
