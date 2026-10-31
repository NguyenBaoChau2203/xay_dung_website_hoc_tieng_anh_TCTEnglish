# Plan đồng bộ hiệu quả Codex, Cursor, Claude Code và Antigravity

Ngày31/10/2026 UTC+7; HEAD quan sát `df251e6`, feature/bao-chau-ui-audit.
Mục tiêu: cùng quy định và công cụ đã pin, ít context trùng, quyền rõ, có bằng
chứng host thực tế. Đây là plan, không phải ghi nhận đã triển khai các bước dưới.
[Prompt thực thi](../../../.ai/prompts/four-client-tooling-local-execution.md).

Execution update31/10: owner subsequently authorized implementation, grouped
commits, PR/merge to master and workspace synchronization, with production still
excluded. Results: [execution ledger](../../feature-audits/2026-10-31-four-client-tooling-ledger.md).
That ledger distinguishes implemented/protocol-verified work from native UI
evidence not observable here. The original planning checklist below is retained
as scope/reference; its initial local-only handoff does not revoke newer authority.

## Bằng chứng hiện có

- Các client đã có native MCP/hooks và chung `scripts/agent-tools.cjs`.
  `scripts/agent_clients.py` giữ định nghĩa managed; validator so sánh cấu hình.
- Canonical16 skills trong `.agents/skills/tct-*`; Antigravity legacy16 adapters
  trong `.agent/skills`, Claude16 adapters trong `.claude/skills`.
- Project CodeGraph1.6.2, explore-only, default read-only, auto-sync cần local
  opt-in đúng checkout/version. Checkout hiện tại đã có opt-in; không phát tán nó.
- Antigravity global `~/.gemini/config/mcp_config.json` còn codegraph từ PATH:
  `codegraph serve --mcp`; PATH trả1.6.0. Project config dùng launcher1.6.2.
  Chưa xác minh live host chọn cấu hình nào; không kết luận global đang override.
- `AGENTS.md`22289 bytes/344 dòng; `.ai/README.md`5675 bytes/99 dòng;
  `CLAUDE.md`1099 bytes/19 dòng, import cả hai file trên. Số liệu là snapshot,
  không phải số token billing hay benchmark.
- Cursor còn có cơ chế discovery compatibility cho `.claude/skills`; cần kiểm
  actual unique catalog, chưa xác nhận bị nạp trùng mọi skill.
- Script/contract hooks bốn host và10 tests policy CodeGraph đã pass. Điều này
  chưa chứng minh UI/trust/native invocation ở từng client.
- Working tree đang có công việc application/DB acceptance khác; giữ nguyên,
  không chạy formatter/build/refactor lên các diff đó để làm task tooling.

## Quyền và ranh giới

Một agent thực hiện inline; không delegate sang AI/chat/CLI/model khác. Lượt lập
plan không cấp quyền sửa native/global config. Khi owner giao thực thi, làm phần
repo/local đã giao; config cá nhân cần scope rõ trong chat. Chuẩn bị patch backup/
diff reviewable trước khi xin quyền sửa global nếu chưa có. Không thay trust,
không auto-approve, không bypass policy hoặc khởi chạy model khác để smoke test.

Chỉ local: không commit/push/PR/dispatch CI/merge/Plesk/gate/deploy. Không app
source/DB/packages/target/migration/Program/appsettings/csproj. Không cài Context7
hay plugin mới, không nâng CodeGraph toàn máy, không tự init/full-rebuild index.
Các quyền local auto-sync đã duyệt vẫn theo đúng checkout policy hiện có.

## A0 — Baseline và acceptance matrix (ưu tiên cao)

- [ ] Tạo ledger `docs/feature-audits/2026-10-31-four-client-tooling-ledger.md`:
  client/version/surface, project config, relevant user config metadata, nguồn
  instructions/skills, MCP actual launch command/version/tools, hook trust/native
  evidence, trạng thái configured/protocol-tested/native-verified/blocked.
- [ ] Chỉ đọc metadata cần thiết; không dump toàn bộ global env/config có secrets.
  Giữ diff có sẵn, ghi hash/baseline cho chính files tooling sẽ sửa.
- [ ] Dùng official docs đúng version/surface khi xác nhận native schema hoặc
  precedence. File tồn tại, plugin enabled flag và script pass không bằng active.
- [ ] Giữ danh sách invariants để dùng kiểm A2: single-agent, ownership/CSRF/
  identity/ViewModel/async, protected config authority, DB migration/data/release
  rules, local-first/no remote action, CodeGraph source freshness/scope và
  verification/report/handoff. Không rút ngắn bằng cách bỏ quy tắc này.

## A1 — Một đường MCP đã pin, kiểm riêng từng client (ưu tiên cao)

Files: `.codex/config.toml`, `.mcp.json`, `.cursor/mcp.json`,
`.agents/mcp_config.json`, `scripts/agent_clients.py`, `scripts/agent-tools.cjs`,
CodeGraph launcher/policy/tests chỉ khi cần sửa lỗi đã xác minh.

- [ ] Kiểm project launchers cùng chọn1.6.2; Git chỉ7 read tools, CodeGraph chỉ
  explore. Không dùng npx latest hoặc native global command thay project launcher.
- [ ] Xác minh Antigravity actual source của server cùng tên trong UI/log. Nếu
  workspace đã thắng thì không cần sửa global. Nếu global cũ được dùng, chuẩn bị
  project-scoped resolution/diff trước; không gỡ MCP của project khác hoặc đổi
  global CLI1.6.0 chỉ để giải quyết workspace này. Nếu host bắt buộc global edit,
  backup/patch giữ các server khác và quyền owner cần cụ thể cho file đó.
- [ ] Inventory shadow/duplicate config tương tự cho Codex/Cursor/Claude; không
  giả định chỉ Antigravity có vấn đề. Giữ custom settings và server khác.
- [ ] Giữ default read-only, local opt-in theo root/version, explore-only/path
  scope, telemetry off, shared writer và setup không init/index. Không chạy full
  rebuild để xử lý reconnect hoặc stale source khi chưa được giao.
- [ ] Chỉ sửa native definitions cần thay và đồng bộ validator/generator tương
  ứng; không chạy `agent_clients.py --write` đè tất cả customizations một lượt.

## A2 — Giảm instructions luôn nạp, giữ một nguồn quy định (ưu tiên cao)

Files: `AGENTS.md`, `.ai/README.md`, `CLAUDE.md`,
`.cursor/rules/database-pr-release.mdc`, shared skills/docs đúng scope.

- [ ] Rút `AGENTS.md` về core invariants và routing; chuyển bảng architecture,
  entity/file map, lịch sử tooling/chi tiết vận hành sang đúng docs hiện có.
  Không tạo bốn bản rules đầy đủ hoặc duplicate các docs đã authoritative.
- [ ] Mục tiêu ban đầu khoảng8–12KB nếu giữ đủ invariants; đây là target để review,
  không phải quota bắt buộc. Đo before/after bytes/lines/import footprint, không
  suy ra phần trăm tiết kiệm token. Giữ dưới24,000 bytes cho Antigravity.
- [ ] Claude giữ import core policy chắc chắn. Chuyển routing dài của README sang
  pointer đọc theo nhu cầu khi phù hợp; chia thành nhiều @imports vẫn nạp toàn bộ,
  không phải progressive disclosure. Verify instruction loading ở version đã có.
- [ ] Cursor rule nhỏ chỉ nhắc entrypoint/invariants cần alwaysApply; không chép
  lại toàn AGENTS. Antigravity dùng AGENTS được native hỗ trợ, chỉ thêm bridge
  nếu client cũ thực sự cần; không tạo GEMINI.md lặp cùng toàn bộ nội dung.
- [ ] Shared engineering/superpowers giữ active-step loading, exact CodeGraph
  symbols, fresh source counts as read, bounded fallback. Context7 conditional,
  chỉ external docs/version, không tự thêm server hoặc gửi private source/log.
- [ ] Review invariant checklist trước/sau và semantic diff, update validators
  có meaning nếu chúng phụ thuộc heading/path; không sửa expected chỉ để xanh.

## A3 — Skills/plugins/hooks gọn và đúng contract (ưu tiên vừa)

- [ ] Giữ16 canonical skill; adapters chỉ entrypoint. Kiểm UI/catalog unique
  skill name và nguồn nạp trên mỗi host trước khi đổi compatibility path. Không
  xóa `.agent/skills` hoặc `.claude/skills` khi chưa chứng minh clients không cần.
- [ ] Rà plugin Superpowers/global skills có kích hoạt nội dung trùng hoặc flow
  subagents/model routing trái owner. Repo skill inline là nguồn thủ tục; không
  tự đổi model allowlist, cài plugin hàng loạt hoặc gỡ plugin cá nhân.
- [ ] Giữ native hook event/output theo host: SessionStart ngắn, direct delegation
  denial, content-fingerprint tooling validation, Stop không loop. Không hook
  full build/tests cho mọi edit hoặc đọc source; không thêm chặn grep/read mù.
- [ ] Local-first/no remote scope phải rõ trong core rule và execution prompt.
  Native permission UI/trust vẫn độc lập; không tuyên bố regex/hook chặn được mọi
  git/gh/HTTP write hoặc thay thế kiểm soát production hiện có.
- [ ] Nếu tìm mismatch thực thì regression đỏ theo actual payload trước fix,
  giữ privacy/malformed-input/no-loop checks. Không sửa hooks chỉ để cả4 file
  nhìn giống nhau.

## A4 — Chẩn đoán read-only và chứng minh native (ưu tiên cao trước handoff)

- [ ] Reuse/mở rộng diagnostic hiện có thành một báo cáo đọc-only cho4 client:
  version/config source, canonical/adapters, expected/actual executable, pins,
  enabled tools, index policy/freshness, hook state có thể quan sát và blockers.
  Nếu thêm doctor mode, không auto-install/rewrite/index/trust trong doctor.
- [ ] Script checks: tooling validator; Python suite; Node setup+policy tests;
  four-host hooks; Git MCP read-only and CodeGraph read-only/project-mode smoke
  theo phạm vi policy hiện có. Fixture mới phải ở ignored disposable output.
- [ ] Native checks dùng UI/config/log hiện có, không spawn thêm AI reviewer hoặc
  gửi task cho4 model. Kiểm loaded rules, skills, MCP initialize/tools/connection
  và automatic hook evidence khi có. Thiếu native invocation thì ghi unverified,
  không biến protocol smoke thành4 host đều verified.
- [ ] Khi host đòi owner trust/reload, chuẩn bị config diff cụ thể và bước cần
  owner thao tác; hoàn thành các check độc lập trước. Không chỉnh trust store hay
  dùng bypass flags. Không nhận live reload đã thành công khi chỉ file đã đổi.

## A5 — Đo hiệu quả và handoff local (ưu tiên sau correctness)

- [ ] Đo metadata/context tĩnh trước/sau: bytes luôn nạp, imports, unique skills,
  số tool MCP được expose, duplicate server sources; hook/launcher startup timings
  có command/sample count. Usage/billing chỉ ghi nếu client thực sự cung cấp.
- [ ] Không benchmark bằng cách mở4 tác vụ AI song song. Theo dõi cùng loại việc
  trong các phiên owner đang dùng bình thường; không cam kết phần trăm khi thiếu
  dữ liệu trước/sau có thể so sánh.
- [ ] Handoff ngắn: start/continue/review/fix prompts chung, notes riêng cho mỗi
  client, team setup và rollback. Không tạo thêm layer rules cho mỗi model.
- [ ] Ledger rõ hạng mục fixed/configured/script-pass/native-unverified, files,
  actual tests và blockers. Giữ diff local chưa commit/push; owner review trước
  mọi bước từ xa. Nếu app khác đổi song song, ghi ngoài scope, không build app
  đang sửa dở để kết luận tooling làm hỏng nó.

## Official references cho các lựa chọn trên

- [Codex instructions](https://learn.chatgpt.com/docs/agent-configuration/agents-md):
  discovery/cumulative limit theo cấu hình; không giả định unlimited instructions.
- [Claude memory/imports](https://code.claude.com/docs/en/memory): @imports nạp
  cùng context; pointers/scoped rules có mục đích khác.
- [Cursor skills](https://cursor.com/docs/skills): nhiều discovery directories;
  cần kiểm actual catalog trước khi dọn adapters.
- [Antigravity rules](https://antigravity.google/docs/rules),
  [MCP](https://antigravity.google/docs/mcp/),
  [hooks](https://antigravity.google/docs/hooks/): scope/native format và giới hạn
  rule24,000 bytes. Không suy ra version client đã cài từ docs website.
