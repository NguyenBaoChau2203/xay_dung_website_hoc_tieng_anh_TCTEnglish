# Đồng bộ toolkit4 client — thực thi local khi owner giao

Clients: Codex, Cursor, Claude Code, Antigravity. Một agent đang làm task; không
delegate hoặc mở thêm AI chat/CLI/model để làm worker/reviewer/smoke task.
Reuse AGENTS.md/.ai/README.md và tct-tooling-review + tct-engineering; CodeGraph
first cho source indexed. Không nạp lại toàn bộ lịch sử plan/vendor/role.

Đọc scope/baseline và đúng bước active trong
`docs/superpowers/plans/2026-10-31-four-client-tooling-alignment-plan.md`.
Lượt tạo file chỉ lập plan; quyền thực thi lấy từ chat owner. HEAD baseline
df251e6 có nhiều diff application acceptance của owner/agent khác; giữ nguyên.

Ưu tiên A0 inventory + invariant checklist → A1 MCP actual routing/pin → A2 core
instructions gọn → A3 skills/hooks → A4 protocol/native evidence → A5 measurement/
handoff. Không coi có config là client đã nạp; không coi hook scripts pass là
native invocation đã xảy ra. Có thể ghi native unverified nếu thiếu UI/log/trust.

Các điểm cần xử lý:

- Global Antigravity có `codegraph serve --mcp` từ PATH1.6.0, project dùng1.6.2.
  Xác minh server thực trước, không mặc định global thắng hoặc tự xóa cấu hình đó.
- AGENTS snapshot22289 bytes, sát limit24KB của Antigravity. Giữ core security/
  owner/local-first/DB/release/verification rules; chuyển chi tiết architecture/
  tooling sang docs đã có. Giữ một nguồn, không viết4 bản đầy đủ.
- CLAUDE.md import cả AGENTS và README; giảm eager imports hợp lý. Cursor có
  thể discover `.claude/skills` và canonical skills: kiểm unique catalog trước
  khi bỏ adapter tương thích. Không thay model allowlist hay plugin cá nhân.
- Giữ local policy auto-sync theo checkout/version, explore-only, Git7 read tools,
  no auto full-index/setup indexing và script privacy/no-loop.

Phạm vi khi được giao: project tooling/rules/docs/tests local. Nếu native user
config cần sửa, tạo backup/patch/diff cụ thể để owner review; chỉ apply khi chat
đã cấp quyền cụ thể cho file/scope đó. Không đọc/in secrets hoặc đè các MCP khác.
Không sửa trust/auto-approve/bypass, không cài plugin/Context7/nâng CLI toàn máy.

Không application source/DB/Program/appsettings/csproj/packages/target/migrations,
không dừng workload/app owner, không tự commit/push/PR/remote CI/merge/Plesk/gate/
deploy. Lượt này chỉ local. Không dùng task tooling để hoàn tất app acceptance
đang làm song song. Chạy tooling checks có liên quan, không full app suite ở mỗi
hook. Giữ trạng thái configured/script-verified/native-verified tách riêng.

## Chat copy

```text
Thực hiện đồng bộ toolkit của TCT English cho Codex, Cursor, Claude Code và Antigravity bằng một agent duy nhất, chỉ local.
Đọc .ai/prompts/four-client-tooling-local-execution.md và thực thi A0–A5 trong docs/superpowers/plans/2026-10-31-four-client-tooling-alignment-plan.md. Tôi giao sửa phần rules/tooling/docs/tests trong repo theo plan; giữ mọi diff ứng dụng/acceptance hiện có.
Ưu tiên xác minh MCP thực sự được host nạp, thống nhất launcher/pin, rút gọn instructions luôn nạp và kiểm skills/hooks theo từng client. Giữ single-agent, local-first, data/security/release rules và CodeGraph policy hiện có.
Config cá nhân/global chỉ chuẩn bị patch/diff để tôi review, chưa tự apply. Không sửa trust, cài/nâng công cụ toàn máy, mở thêm AI task, commit/push/PR/CI từ xa/merge hoặc deploy. Báo actual evidence và blockers; không gọi cả4 client đã hoạt động chỉ vì scripts pass.
```
