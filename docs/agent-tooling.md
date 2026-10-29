# Kiểm kê và nâng cấp bộ công cụ AI của TCT English

> Cập nhật 29/10/2026: đã cài thêm CodeGraph 1.6.2 riêng cho dự án và MCP đọc
> index hiện có. Xem [codegraph.md](codegraph.md). Source registry hiện có 12 repo,
> 84 file nguồn giữ lại; các thống kê 11 repo/83 file bên dưới là đợt kiểm kê đầu.

> Đính chính sau phản hồi của chủ dự án: lượt đầu chỉ cài Git pre-commit và bỏ
> sót lifecycle hooks cho AI. Đã bổ sung bốn hook trong .codex/hooks.json, kiểm
> thử script/launcher, và ghi rõ bước host review/trust còn cần thực hiện trong
> [agent-hooks.md](agent-hooks.md). Không coi test script là bằng chứng hook đã
> được Codex tự động gọi.

Ngày kiểm tra: **28/10/2026**. Phạm vi: cấu hình, skill, role, hook và công cụ
phát triển của repo. Không đổi giao diện đang chạy, ứng dụng, database hoặc các
file Program.cs, appsettings và csproj. Toàn bộ công việc do một agent thực hiện.

## Trước khi nâng cấp

| Hạng mục | Kết quả kiểm kê |
| --- | --- |
| Skill dự án | 10 skill trong .agent/skills; chưa có .agents/skills để Codex tự phát hiện |
| Metadata | name dùng tên hiển thị có khoảng trắng, chưa khớp tên thư mục chuẩn |
| Workflow | 5 workflow Antigravity; một số ví dụ gắn với kiến trúc/quy tắc cũ |
| Role | Chưa có role dự án; không có bằng chứng về role cũ đang được bật |
| Hook | core.hooksPath chưa cấu hình; .git/hooks chỉ có file sample |
| MCP | Chưa có cấu hình MCP ở phạm vi repo |
| CodeGraph | CLI 1.6.0; index có 497 file, 8.083 node, 17.689 edge, do engine 1.5.0 tạo |
| Nền tảng | App và test thực tế target net9.0, EF Core 9.0.4; SDK máy là 10.0.300 |
| Tài liệu | Một số tài liệu đang hoạt động ghi nhầm .NET/EF 10 |
| Template | Razor dùng comment Handlebars; ServiceResult khác API OperationResult; icon Bootstrap Icons không được layout bảo đảm |
| VS Code | settings.json có cú pháp JSON không hợp lệ |

CodeGraph đã được dùng trước khi tìm mã. Không tự re-index hoặc thay bản cài toàn
máy: chủ dự án kiểm soát việc indexing. Không đánh đồng một skill vẫn hữu ích với
skill lỗi thời chỉ vì ngày tạo; giữ các skill nghiệp vụ và sửa điểm sai cụ thể.

## Các nguồn đã đối chiếu và quyết định

Hai đường dẫn Addy Osmani trong yêu cầu trùng nhau; đã gộp thành một nguồn.
Tổng cộng **11 repo riêng biệt** đã được đọc tại nguồn và kiểm tra checkout.
Commit đầy đủ và SHA-256 của 83 file nguồn được giữ lại nằm trong
[sources.lock.json](../.ai/tooling/sources.lock.json).

| Nguồn | Tích hợp | Lý do/phần được chọn |
| --- | --- | --- |
| [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) | Cài script Python, dữ liệu và tài liệu; entrypoint tct-ui-ux-pro-max | Tra cứu UX, màu, type và cấu trúc; adapter giữ Razor/Bootstrap vì dataset không có profile riêng cho stack này |
| [Awesome DESIGN.md](https://github.com/VoltAgent/awesome-design-md) | Ghi nhận tham khảo; không kích hoạt hay chép template thương hiệu | Có ích để tham khảo; không thay nhận diện TCT bằng thương hiệu khác |
| [Taste Skill](https://github.com/Leonxlnx/taste-skill) | Đã đánh giá; không kích hoạt | Chồng lấn quy trình chọn hướng UI và thêm điều phối ảnh/browser chưa cần |
| [Impeccable](https://github.com/pbakaus/impeccable) | Chọn tám tài liệu review/craft; entrypoint tct-ui-review | Dùng audit, polish, responsive, edge states, typography; không cài binary bootstrap, subagent hoặc hook của upstream |
| [Emil Kowalski skills](https://github.com/emilkowalski/skills) | Chọn ba tài liệu; entrypoint tct-ui-motion | Dùng nguyên tắc motion và performance trên browser; không đưa React/native hay thư viện animation vào ứng dụng |
| [Hallmark](https://github.com/nutlope/hallmark) | Đã đánh giá; không kích hoạt | Chọn theme trùng nhiệm vụ với bộ thiết kế chính và dễ làm lệch nhận diện hiện có |
| [Google DESIGN.md](https://github.com/google-labs-code/design.md) | Giữ spec/license và tạo DESIGN.md của TCT | Ghi nhận màu, font, component từ CSS/layout thật; không thêm CLI hoặc đổi CSS của ứng dụng |
| [Superpowers](https://github.com/obra/superpowers) | Bảy tài liệu quy trình; entrypoint tct-superpowers | Lập kế hoạch, debug, TDD, review và verification chạy inline; không cài bộ điều phối nhiều agent |
| [Addy Osmani agent-skills](https://github.com/addyosmani/agent-skills) | Ba tài liệu kỹ thuật và hai tài liệu role; entrypoint tct-engineering | Chọn context engineering, source-driven development và code review; chuyển ví dụ sang ranh giới MVC của dự án |
| [MCP reference servers](https://github.com/modelcontextprotocol/servers) | Cài mcp-server-git==2026.10.19 và khóa dependency | Git server có adapter giới hạn bảy tool đọc cho mọi client; Codex giữ thêm allowlist. Không cài server archive hoặc filesystem trùng chức năng sẵn có |
| [ECC](https://github.com/affaan-m/ecc) | Chọn dependency review, verification và code review; entrypoint tct-tooling-review | Không nhập các hook shell, learned memory, telemetry hay worker/model mặc định của framework |

Các bộ trên có vai trò khác nhau. Không cài mọi bộ làm plugin hoạt động đồng
thời. Reference snapshot giữ nội dung nguồn và giấy phép để đối chiếu; entrypoint
của TCT quyết định phần nào áp dụng. Không chạy câu lệnh trong reference chỉ vì
tài liệu upstream yêu cầu. Root AGENTS.md và yêu cầu của chủ dự án luôn ưu tiên.

## Cấu trúc sau nâng cấp

- **16 skill canonical** ở .agents/skills, gồm 10 skill nghiệp vụ đã sửa và sáu
  skill tích hợp mới. Tất cả dùng tên tct-* để phân biệt với skill toàn máy.
- **16 adapter Antigravity** ở .agent/skills trỏ về cùng bản canonical.
- **Ba role inline** ở .ai/roles: implementer, ui-reviewer, security-reviewer.
  Cùng một agent áp dụng lần lượt; không phải cấu hình spawn agent.
- **Năm workflow** được chỉnh lại; **năm template** đã chuẩn hóa. Các phiên bản
  cũ được giữ ở .ai/tooling/archive/2026-10-28 để so sánh/phục hồi.
- **DESIGN.md** ghi nhận baseline hiện tại, bao gồm cả chênh lệch font mobile;
  không khẳng định CSS đã có token chung hay giao diện đã được kiểm tra browser.
- **Git hook pre-commit** kiểm tra nội dung trong Git index, không format/stage,
  commit/push hoặc thay file. core.hooksPath được bật riêng cho checkout hiện tại.
- **Bốn lifecycle hook cho AI:** SessionStart, PreToolUse, PostToolUse và Stop.
  Định nghĩa/script đã có và qua kiểm thử; tự động thực thi cần host review/trust.
  Hook kiểm tra tooling và quy định một agent; build/test ứng dụng vẫn là bước riêng.
- **CI tooling** ở .github/workflows/agent-tooling.yml kiểm tra metadata, nguồn,
  cấu hình và unit test trên PR. Checkout action được ghim commit của v7.0.1 đã
  kiểm tra tại repo chính thức; không thay pipeline deploy hiện tại.

## Cài lại trên một checkout khác

Cập nhật cho nhóm ngày 29/10/2026: dùng
[hướng dẫn thiết lập bằng nút/file](team-agent-setup.md). Đồng đội có thể cho phép
folder-open task, bật MCP hoặc bấm đúp SETUP-AGENTS.cmd; không cần gõ các lệnh
PowerShell bên dưới. Native hooks/MCP đã có cho Codex, Claude Code, Cursor và
Antigravity. Git server hiện tự giới hạn bảy tool đọc cho mọi client, thay cho
việc chỉ dựa vào allowlist Codex như báo cáo ban đầu ở dưới. Các lệnh sau là
lựa chọn bảo trì thủ công.

Yêu cầu: Git, uv, PowerShell 7 cho MCP trên Windows. Script kiểm tra tĩnh chỉ dùng
Python standard library (3.11+); môi trường MCP được khóa riêng ở Python 3.13.

```powershell
uv sync --project tooling/mcp --locked
pwsh -NoProfile -File scripts/install-agent-hooks.ps1
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --project tooling/mcp --locked python scripts/check_git_mcp.py
```

Hook installer sẽ dừng nếu có hooksPath khác hoặc các hook Git mặc định đang hoạt
động để tránh ghi đè hệ thống đang dùng. Không thay cấu hình home/toàn máy. python trên máy kiểm tra
là Windows Store alias; dùng Python do uv quản lý để chạy các script.

Codex nhận skill từ .agents/skills; xem [tài liệu skill chính thức](https://developers.openai.com/codex/skills/).
MCP cấu hình trong .codex/config.toml chỉ được host nạp cho project đã tin cậy;
xem [tài liệu MCP](https://developers.openai.com/codex/mcp/). Skill sẽ sẵn sàng ở
lượt kế tiếp; nếu chưa hiện, tải lại client. MCP mới cần phiên/client nạp lại
cấu hình. Không ghi thêm quyền trust hoặc tắt sandbox để ép host nhận cấu hình.

MCP smoke check kiểm tra launcher và server thực tế. Trong lượt triển khai này,
host cũng đã nạp namespace mcp__tct_git với đúng bảy tool và gọi git_status thành
công qua tool của chat. Server upstream vẫn có tool ghi; **allowlist của Codex** chỉ
cho phép git_status, git_diff_unstaged, git_diff_staged, git_diff, git_log, git_show
và git_branch. Đây không phải thay đổi biến server upstream thành read-only cho
mọi client. Đây là ghi nhận của lần triển khai ban đầu. Từ bản tích hợp nhóm
29/10, tooling/mcp/git_readonly.py gỡ handler ghi trước khi phục vụ và mọi client
dùng cùng launcher với bảy tool đọc.

## Kiểm chứng và giới hạn

- Build baseline: thành công, 0 warning và 0 error.
- Build cuối sau các thay đổi tooling: thành công, 0 warning và 0 error.
- 16/16 skill qua validator của skill-creator (Python UTF-8 trên Windows).
- 21/21 unit test tooling đạt sau bổ sung lifecycle hooks. Đã kiểm tra Git hook bằng index tạm:
  snapshot hợp lệ được chấp nhận; source đã stage bị sửa sai hash bị từ chối dù
  file trong working tree vẫn đúng. Git index của người dùng không bị thay đổi.
- Kiểm tra whitespace của toàn bộ diff, kể cả file mới, và encoding đều đạt.
- Launcher Windows của bốn lifecycle hook chạy trực tiếp từ thư mục con đều đạt;
  không ghi trạng thái trust và không dùng cờ bypass. Đây là kiểm tra script,
  chưa xác nhận host đã tự động gọi các hook mới.
- .NET test baseline: **555/557 đạt**, hai lỗi:
  Sprint1SmokeTests.CoreDetailRoutes_ReturnOkForAuthenticatedUser với route
  /Home/FolderDetail/201; GoalsPhase2IntegrationTests.RecordLearningActivityAsync_Works_WhenUserDailyActivitiesSchemaMissingPhase2AreaCounters.
- Hai lỗi trên xuất hiện khi chưa có thay đổi mã ứng dụng; không mở rộng task
  nâng cấp tooling thành sửa runtime. Kết quả TRX/log nằm trong thư mục .tmp được
  ignore. Không khẳng định toàn bộ regression suite xanh.
- Cả hai lỗi baseline có cùng exception ObjectDisposedException trên
  SQLitePCL.sqlite3_stmt. Đây là bằng chứng lỗi test SQLite hiện có, chưa chứng
  minh route hoặc logic mục tiêu tương ứng bị lỗi khi sử dụng ứng dụng thật.
- Chạy lại hai nhóm test tương ứng được **5/5 đạt**. Kết quả cho thấy lỗi baseline
  không tái hiện ổn định; chưa đủ bằng chứng để khẳng định toàn bộ suite hết lỗi.
- UI search chạy thật với domain UX và design-system; MCP initialize/list_tools/
  git_status chạy thật từ thư mục con. Có kiểm tra lại trước bàn giao.
- Unit test tooling kiểm tra metadata, version pin, hash, path traversal và sự
  khác biệt giữa staged content và sửa đổi chưa stage, với đường dẫn có dấu cách.
- Không chạy một nhóm AI reviewer hoặc tuyên bố đã thực hiện pressure test bằng
  agent độc lập. Review skill và cấu hình được thực hiện inline theo chính sách.
- Không thay đổi CodeGraph toàn máy hoặc tự re-index. Chưa đo được chất lượng UI
  của một tính năng tương lai chỉ bằng việc cài thêm skill.

Kết quả kiểm tra cuối và các tình huống được xác nhận được bổ sung vào plan
docs/superpowers/plans/2026-10-28-agent-tooling.md.

## Cập nhật và rollback

Muốn cập nhật: kiểm tra diff của một nguồn tại một thời điểm, đọc license, chọn
commit đầy đủ và chỉ nhập những file đang cần. Đối chiếu lại adapter trước khi
đổi source hash. Với MCP sửa version đã review, chạy uv lock/sync và smoke check.
Không biến quá trình khởi động chat hoặc pre-commit thành tự động kéo bản mới.
Hash phát hiện drift; nó không thay thế việc review upstream.
Các test fixture của UI/UX Pro Max đã được chuyển ra .tmp thay vì đưa vào skill
hoạt động. Hai file nhập có whitespace thừa được chuẩn hóa, ghi rõ trong lock;
không che lỗi bằng cách tắt kiểm tra whitespace.

Muốn tắt hook checkout này, chạy git config --local --unset core.hooksPath nếu
giá trị hiện tại vẫn là .githooks. Muốn tắt Git MCP, đặt enabled=false cho tct_git
trong .codex/config.toml rồi tải lại phiên. Có thể phục hồi các tài liệu cũ từ
archive sau khi đối chiếu; không khôi phục hàng loạt để đè công việc mới.
Việc gỡ toolkit không cần thay database hoặc dependency của ứng dụng.
