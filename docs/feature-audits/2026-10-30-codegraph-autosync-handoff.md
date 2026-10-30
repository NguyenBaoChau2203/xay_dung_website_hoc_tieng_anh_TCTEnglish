# CodeGraph 1.6.2 — backup, rebuild và auto-sync riêng checkout

Ngày 2026-10-30 (UTC+7). Owner đã duyệt A5: backup/rebuild + incremental auto-sync,
sửa rule/config liên quan, giữ explore-only/single-agent và default của đồng đội;
không sửa app/apply DB thật/commit/push. Một agent thực hiện toàn bộ.

## Kết quả

- Backup index cũ bằng SQLite online backup API: 497 file, 8.083 node, 17.689 edge.
- Rebuild CLI pinned 1.6.2 thành công: **577 file, 9.657 node, 21.146 edge** lúc kết thúc.
- Metadata `indexed_with_version=1.6.2`, `index_state=complete`; status không còn pending.
- Auto-sync local đã bật, debounce 2.000ms, MCP project-mode initialize/list/explore
  truy vấn được `SpeakingImportJobService`. CodeGraph tool trong phiên cũng trả
  source của `readLocalPolicy`, symbol mới trước đó chưa được index.
- Status quan sát cuối: **577 file, 9.662 node, 21.159 edge**, up to date.
  Có sửa đổi source/test song song trong working tree; không chạy CLI sync bổ sung
  để có kết quả này. Số node/edge tiếp tục thay đổi theo source đang làm.

## Cách triển khai

`scripts/agent-tools.cjs mcp codegraph` gọi `launch.cjs --mcp-project`.
`project-policy.cjs` đọc `.ai/codegraph.local.json` được Git bỏ qua, kiểm schema,
root checkout, phiên bản đã duyệt, index sẵn và debounce. Không có policy thì
dùng adapter read-only hiện tại; policy sai bị từ chối. Bản package tiếp tục ghim
1.6.2 với transitive lock, không cài/nâng dependency.

Chỉ checkout hiện tại có approval: mode auto-sync, real root hiện tại,
reviewedVersion 1.6.2, debounce 2000. Không sửa MCP definitions của bốn client
hoặc user-global config. Checkout khác không có file ignored nên không bật writer.
Không thay semantics của cờ `--mcp-readonly`.

`mcp-autosync.cjs` là gate stdio tới server/daemon native của upstream:

- Tool list chỉ `codegraph_explore`, kể cả daemon được client khác khởi động với
  tool list rộng hơn; gate cũng từ chối call tool khác trực tiếp.
- ProjectPath phải ở trong checkout duyệt, sau đó normalize về root. Chặn batch
  RPC để không bypass gate. Không mở writer cho project khác từ một query.
- Upstream quản lý catch-up, watcher, debounce, writer lock và chia sẻ daemon.
  Hai MCP client fixture cùng một PID daemon, không có vòng CLI sync do hook.
- Giữ telemetry/update/download checks tắt. Watcher/daemon là tiến trình công cụ
  cục bộ, không chạy model AI khác hoặc thay chính sách single-agent.
- Không tự init/full rebuild, setup/folder-open/post-merge vẫn không index.

AGENTS, README, engineering skill, Claude instructions, SessionStart pointer và
docs cập nhật ngoại lệ opt-in cục bộ. Approval không phải quyền bật auto-sync
ở máy khác hoặc cho agent tự tạo một approval mới.

## Backup và sự cố đã giải quyết

Backup: `.tmp/codegraph-auto-sync/backups/20261030T044450Z/codegraph.db`.
Manifest cùng thư mục lưu snapshot metadata/counts/method/SHA-256.

SHA-256: `d3feedc860e413c6c2003867dc5f83b93caf91c18405911dd18a1de1f73dab94`.

Đã kiểm quick_check=ok và đối chiếu lại hash sau rebuild. Backup API bao gồm các
transaction committed còn ở WAL; không chỉ copy main DB đang mở.

Lần rebuild đầu bị Windows EPERM vì adapter read-only giữ handle DB. Đã xác minh
đúng executable/command line của duy nhất tiến trình adapter thuộc checkout,
đóng tiến trình công cụ đó rồi retry CLI thành công. Không dừng tiến trình AI/IDE,
không xóa `.codegraph` bằng recursive delete, không can thiệp ứng dụng.

## Kiểm thử

| Kiểm tra | Kết quả |
| --- | --- |
| Policy/scope tests, Node | 10/10 pass; có red trước implementation |
| Bootstrap tests, Node | 9/9 pass; tổng Node 19/19 |
| Python tooling unittest | 41/41 pass |
| Tooling validator | Pass |
| Four-host lifecycle command/contracts | Pass; không thay native definitions/trust |
| Read-only MCP smoke | Pass, main DB/WAL fingerprint giữ nguyên |
| Auto-sync fixture MCP/SQLite | Pass; chạy trước khi bật checkout thật và chạy lại sau cùng |
| Project-mode live MCP | Pass; version 1.6.2, explore-only, C# source mới |
| Solution build | Pass, 0 errors ở lần chạy ghi log; không phải review chức năng Learning |

Fixture xác nhận default read-only; add/edit/delete nodes trong SQLite thật;
debounce burst 10 saves; offline catch-up; hai client được bảo vệ dùng chung
daemon với một client backend cấu hình rộng hơn (`explore,node`); deny tool
khác/outside path; xóa opt-in rồi dừng/reopen phục hồi read-only, DB/WAL không đổi.
Không tạo/sửa file ứng dụng để thử watcher. Lần setup fixture đầu dùng `index`
khi chưa init bị từ chối đúng; đã sửa checker dùng `init` cho fixture mới,
production vẫn dùng `index` trên index đã tồn tại.

Trong fixture với debounce 1.000ms, một lượt edit được index sau khoảng 1,3 giây;
working set daemon lấy mẫu khoảng 205–219 MiB qua các lượt, CPU là thời gian
tích lũy của test. Đây không phải benchmark tải hoặc cam kết production latency.

Artifacts ignored: `.tmp/codegraph-auto-sync/`: baseline, preservation, backup,
rebuild logs, fixture logs/results, project-mcp.log, build.log, status-final.log.
Không sửa source/tests ứng dụng trong task này. Hash baseline phát hiện bốn file
app/test thay đổi song song; đã giữ nguyên và ghi trong preservation.json, không
restore hoặc nhận đó là thay đổi của task tooling.

## Vận hành và rollback

Phiên IDE đã nạp launcher cũ cần reconnect/restart MCP CodeGraph để dùng routing
mới. Đã kiểm server qua entrypoint thực trên checkout; chưa quan sát native UI
của mọi phiên Cursor/Antigravity/Claude. Smoke scripts không thay trust của host.

Native daemon mặc định idle-exit khoảng 5 phút sau client cuối. Khi MCP chạy lại
sẽ catch-up; không phải dịch vụ chạy vĩnh viễn khi không còn client. Trong khoảng
debounce hoặc khi source chưa index được, agent vẫn dùng stale warning/bounded reads.

Tắt: local mode read-only hoặc bỏ approval, restart các MCP của checkout; dừng
đúng daemon project bằng manager nếu cần dừng ngay. Không đổi mode ngầm dưới
cờ read-only, không dừng mọi Node/AI process. Restore backup cần yêu cầu riêng
và đóng các client đang dùng index; không chạy restore tự động.

Context7, app DB/migrations, code Learning và Git publishing không thuộc task này.
Không có finding tooling còn mở sau checks trên; giới hạn host reload/quan sát
và hiệu năng thực tế được nêu rõ, không cam kết mức giảm token khi chưa đo usage.
