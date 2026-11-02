# Plan 1 — Whisper chạy cùng web local

Ngày lập: 01/11/2026, UTC+7. Baseline khảo sát: master `03c9c5c`.
Trạng thái: **đã thực thi local (Plan 1)** — ledger
`docs/feature-audits/2026-11-01-whisper-local-lifecycle-ledger.md`. Chưa commit/push.
Dừng để Codex review; chưa sang Plan 2.
Prompt: [.ai/prompts/cursor-auto-whisper-local-lifecycle.md](../../../.ai/prompts/cursor-auto-whisper-local-lifecycle.md).

## 1. Mục tiêu và quyền thực thi

Khi Châu yêu cầu AI agent chạy web local để test, agent dùng một entrypoint chung:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Entrypoint bảo đảm MariaDB dev, Whisper và web sẵn sàng, rồi báo URL web và trạng
thái speech. Người dùng không phải mở Whisper bằng tay mỗi phiên. Chỉ khởi động
khi có yêu cầu chạy local; không chạy khi mở folder, dùng MCP, build hoặc test thường.

- Khi owner gửi prompt thực thi, được sửa script/config local/rule/docs/tests,
  cài artifact Whisper đã khóa phiên bản vào thư mục ignored và kiểm thử local.
- Một agent; làm trên nhánh feature phù hợp, gợi ý `codex/whisper-local-lifecycle`.
  Giữ nguyên mọi thay đổi có sẵn, gồm tài liệu BTH2 đang untracked lúc khảo sát.
- Không commit/push/PR/merge/remote CI/deploy nếu chưa được owner cấp bước đó.
  Không đổi Plesk, production gate, CodeGraph policy/index, native trust hoặc model IDE.
- Không reset/import đè DB dev, không dừng tiến trình/container của người khác.
- **Dừng sau ledger để Codex review. Không thực hiện Plan 2 trong cùng lượt.**

## 2. Hiện trạng đã xác minh

| Hạng mục | Bằng chứng hiện tại |
| --- | --- |
| Máy Châu | Intel i5-11400H, 6 core/12 logical, RAM15.7GiB, RTX3050Ti Laptop + Intel UHD |
| Tài nguyên lúc khảo sát | RAM rảnh khoảng4.9GiB; ổ D rảnh khoảng7.4GiB; đây là snapshot, không phải mức cố định |
| Launcher | scripts/start-local.ps1 dùng PS7, MariaDB loopback3307, User Secrets, EF local và web5100; chưa start Whisper |
| Dữ liệu | compose.local.yml giữ volume tctenglish-local-mariadb-data; chỉ restore backup khi DB trống |
| Speech adapter | Learning:Speech; WAV PCM16 mono16kHz, tối đa15s/2MiB; queue+HTTP chung deadline15s mặc định; một inference + bốn chờ |
| Artifact có sẵn | .tmp/asr-acceptance-2930 chứa server b5454 và ggml-tiny.en.bin; chỉ là cache test cũ |
| Lần test cũ | Model77,704,715 bytes; binary folder khoảng23MB; working set server khoảng164MiB và silence1s khoảng1.8s trong ledger30/10 |
| Giới hạn bằng chứng | Silence không chứng minh độ chính xác tiếng nói hoặc latency của clip15s; CPU/GPU idle không suy ra tải thật |

Kết luận thiết kế: CPU `tiny.en`, hai thread, một inference phù hợp để thử local
một người trên máy này. Phải benchmark trước khi kết luận chạy ổn. Không cần CUDA,
GPU image hoặc Docker Whisper trong đợt này; MariaDB vẫn dùng Docker. Tổng file đã
quan sát khoảng96MiB, không phải tổng RAM/đĩa cần cho mọi thao tác. Đo cả web+DB+IDE.

## 3. Phạm vi file và lựa chọn kỹ thuật

Tên file mới là đề xuất; đối chiếu repo và giữ một entrypoint rõ ràng.

| File | Thay đổi dự kiến |
| --- | --- |
| scripts/start-local.ps1 | Tích hợp start/status Whisper mặc định cho phiên chạy web; thêm -SkipWhisper; giữ -DatabaseOnly/-BackupZip/-NoBrowser |
| scripts/local-speech.ps1 (mới) | Các action Setup/Start/Status/Stop/Benchmark; dùng lại helper để tránh hai implementation |
| tooling/whisper/artifacts.lock.json (mới) | URL, phiên bản, SHA256, license, tên executable/model; chỉ metadata được commit |
| scripts/tests/test_local_speech.py (mới) | Test hành vi lifecycle bằng fake process/HTTP và fixture-owned resources |
| .vscode/tasks.json | Task chạy local gọi cùng launcher, không đặt speech thành folderOpen |
| AGENTS.md | Một chỉ dẫn ngắn: yêu cầu chạy web local phải dùng launcher và kiểm speech; không nhân bản rule ở cả bốn client |
| docs/operations/local-development-quickstart.md, local-speech.md | Một lệnh chạy hằng ngày, opt-out, stop/status, cổng, lỗi và rollback |
| docs/feature-audits/2026-11-01-whisper-local-lifecycle-ledger.md (mới khi thực thi) | Kết quả thật, benchmark, diff, blocker và trạng thái từng gate |

Không cần sửa Program.cs, appsettings hoặc csproj để chạy executable bên ngoài.
Không đổi schema/model. Nếu phát hiện cần sửa app vì lỗi tích hợp thực tế, chẩn đoán
và ghi bằng chứng trước; không tận dụng plan này để thay thuật toán chấm Speaking.

## 4. Các bước thực thi có phụ thuộc

### W0 — Chốt baseline và thiết kế lifecycle (P0)

- [ ] Đọc AGENTS hiện tại, tct-superpowers/tct-engineering/tct-tooling-review và
  phần local setup cần dùng. CodeGraph trước khi đọc indexed source. Không preload lịch sử.
- [ ] Ghi branch/SHA/dirty files, CPU/RAM/đĩa và listeners5100/3307/8092. Không in
  User Secrets/database.env. Cổng8092 là đề xuất: nếu bị chiếm không rõ chủ, báo
  xung đột hoặc chọn cổng explicit; không chiếm lại hay kill theo tên process.
- [ ] Đọc toàn bộ launcher trước khi sửa: giữ logic backup DB trống, credential
  loopback và worker billing disabled. Rà env kế thừa để không trỏ hosting.
- [ ] Chốt state machine absent→starting→ready→stopped/failed; file state nằm ở
  `.appdata/local/speech/`, gắn checkout, run ID, PID, start time, đường dẫn thật,
  executable hash, model hash và cổng. State không chứng minh process còn thuộc ta.

### W1 — Artifact setup tái lập và gọn đĩa (P0, sau W0)

- [ ] Lock server Windows x64 `b5454/whisper-bin-x64.zip`, SHA256
  `6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb`.
  Nguồn: release ggml-org/whisper.cpp b5454; không tự thay latest.
- [ ] Lock ggml-tiny.en.bin SHA256
  `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f`.
  Ghi nguồn HF ggerganov/whisper.cpp và revision nếu xác minh được; URL cập nhật
  được chỉ chấp nhận khi bytes vẫn đúng hash. Lưu license/provenance.
- [ ] Executable đã quan sát có SHA256
  `a97456e747f28e7bb09c1dac35f40d94404c5ecffa74aaaaa7a3753ea6543121`.
  Kiểm cả DLL cần dùng, không chỉ file exe. Reuse cache cũ chỉ sau xác minh bộ
  artifact với lock; thiếu bằng chứng DLL thì lấy ZIP đúng hash để dựng lại.
- [ ] Cache bền vững dưới `.appdata/local/speech/`; không giữ runtime dưới .tmp
  lâu dài, không đưa binary/model/audio vào Git. Setup idempotent, hash fail thì
  không execute; download tạm rồi atomic rename; chống archive path traversal.
- [ ] Kiểm free disk trước download/extract; không tự xóa cache/project khác để
  lấy chỗ. Có lỗi offline thì thông báo lệnh setup/retry, không tải lại mỗi lần run.
- [ ] Có lock lúc setup/start để hai terminal không extract/start cùng lúc. Không
  cài service Windows, scheduled task, firewall rule hoặc thay PATH toàn máy.

### W2 — Start/Status/Stop an toàn (P0, sau W1)

- [ ] Start hidden, bind127.0.0.1, English, tiny.en, CPU threads2; capture log riêng.
  Đảm bảo process tồn tại sau khi CLI Start đã return; không dựa vào shell còn mở.
- [ ] Poll readiness có deadline; kiểm model load và một probe `/inference` hợp lệ
  sau cold start. HTTP200 /health đơn lẻ chưa chứng minh model/inference ready.
- [ ] Run lại: reuse process nếu identity/current flags/model/cổng đúng và healthy;
  không tạo thêm process. Có lock concurrent start. State hỏng/stale/PID reused:
  báo chẩn đoán hoặc loại bỏ state đã xác minh thuộc workspace, không kill nhầm.
- [ ] Stop chỉ process có marker+start time+canonical path/hash/command đúng. Nếu
  identity không đủ, từ chối stop. Không `taskkill /IM`, Stop-Process -Name hay kill
  tất cả dotnet/node/whisper. State và logs không chứa audio người học mặc định.
- [ ] Không tự restart liên tục khi model load fail. Có bounded retry; timeout
  không để process do chính lần start lỗi tạo ra chạy mồ côi.

### W3 — Gắn vào một lệnh chạy web cho bốn agent (P0, sau W2)

- [ ] start-local bình thường: đảm bảo DB→Whisper→cấu hình process web→web. In trạng
  thái từng dịch vụ và URL sau readiness. `/health/ready` chỉ chứng minh DB/app;
  ghi trạng thái ASR riêng, không đổi public readiness production vì yêu cầu local.
- [ ] `-DatabaseOnly`: giữ ý nghĩa DB-only, không download/start/probe Whisper.
  `-NoBrowser`: vẫn bật speech. `-SkipWhisper`: web-only có thông báo ASR chưa được
  launcher bảo đảm; không chuyển sang điểm giả/Gemini để giả vờ thành công.
- [ ] Cấp Learning__Speech__WhisperHttpBaseUrl/ModelIdentity cho child web process
  và cấu hình queue/timeout cần thiết bằng env local. Giữ URL khớp cổng/helper.
  Khôi phục env khi launcher kết thúc; không ghi đè key/config người dùng khác.
- [ ] Nếu web5100 đã chạy, không dừng/restart instance không thuộc launcher. Báo
  rõ cần kiểm config instance đó; web healthy chưa chứng minh đang gọi Whisper mới.
- [ ] Whisper có thể giữ chạy giữa các lần restart web; stop helper là hành động
  explicit. Ctrl+C web không được dừng DB hoặc workload ngoài phiên. Ghi hành vi này.
- [ ] Thêm VSCode/Cursor task gọi đúng script và một pointer trong AGENTS. Codex,
  Claude, Antigravity đọc cùng rule. Không sửa native hook để start web mỗi tool call.

### W4 — Hồi quy lifecycle, tích hợp và tải (P0/P1, sau W3)

Viết test đỏ trước cho hành vi mới có rủi ro process/state, rồi sửa đến xanh.

| Ca | Tiêu chí nghiệm thu |
| --- | --- |
| Setup mới/reuse/offline/hash sai | Tái lập đúng pin; reuse không download; hash sai không chạy exe |
| Hai start đồng thời | Một process; không giẫm state/log hoặc bỏ process mồ côi |
| Cổng bị process khác chiếm | Không kill/reuse nhầm; lỗi rõ và không giả ready |
| Stale state/PID reused/stop | Không dừng process ngoài quyền sở hữu |
| -DatabaseOnly/-SkipWhisper | Không phát sinh speech download/process ngoài contract |
| Launcher→web→Whisper | Cấu hình child đúng; request qua adapter thật, không bypass website để kết luận E2E |
| Silence/no engine/timeout | NoSpeech hoặc lỗi đúng; không lưu completion/XP giả |
| Lời nói thật | Có transcript; câu phù hợp→assessment→progress→credit một lần; reload/replay không cộng lặp |
| Đổi câu/hủy/clip quá dài | Không gắn kết quả câu cũ vào câu mới; input bị chặn đúng |

- [ ] Chạy schema/credit integration tests hiện có bằng fixture, không ghi dữ
  liệu test vào tài khoản người thật. Browser acceptance dùng tài khoản local test.
- [ ] Chạy real live ASR test bằng TCT_ASR_LIVE_BASEURL đã kiểm loopback; discovery
  không HTTP. Opt-in lỗi phải fail. Không set biến acceptance MariaDB sang DB dev.
- [ ] Benchmark cold start, warm latency clip5/10/15s, silence, một request đang
  chạy+bốn chờ/overflow; ghi số mẫu, CPU, peak working set, tổng RAM rảnh và disk.
  Dùng fixture tiếng Anh có provenance/license hoặc bản ghi được owner cho phép;
  silence-only không thay speech benchmark. Không gọi cloud TTS để tạo mẫu ngầm.
- [ ] Mục tiêu thử nghiệm: interactive clip hoàn tất trong timeout15s hiện tại
  trên máy Châu khi web+DB+IDE đang chạy, không tăng RAM liên tục qua10 lần. Nếu
  không đạt, ghi p50/p95 thực tế, thử threads2/4 có đo; không tự nâng model/GPU hoặc
  timeout vô hạn. Working set cũ164MiB chỉ làm mốc tham khảo, không SLA.
- [ ] Mic/permission thực không tự động hóa được thì giữ gate đó Not run và ghi
  bước owner thao tác; không đánh dấu toàn bộ browser flow pass từ WAV fixture.

### W5 — Kiểm tra cuối và handoff (sau W4)

```powershell
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --no-project python scripts/agent_tooling.py
node --test scripts/tests/test_agent_setup.cjs
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --no-build
git diff --check
```

Thực hiện script tests mới trên Windows thật, không coi skip Linux là pass.
Chọn focused/live checks ở W4; full app suite một lần sau thay đổi cuối, không
rebuild sau mỗi dòng docs. Không đổi CI workflow chỉ để có test xanh. Không có
persisted-model change nên không tạo migration; ghi đánh giá schema trong ledger.

- [ ] Ledger ghi command/result/skips, process ownership và phiên còn chạy phục
  vụ owner (PID/port, cách Stop), artifacts/pin và benchmark limitations.
- [ ] Review diff inline; bảo đảm không commit binary/secrets, không đổi DB dev
  data/reset volume, production gate untouched. Nêu native UI chưa kiểm nếu có.
- [ ] Bàn giao: URL local, command daily/status/stop/opt-out, file đổi, lỗi còn
  lại. Dừng cho Codex review; chưa tự commit/push/merge và chưa sang Plan 2.

## 5. Rollback và điều kiện chuyển Plan 2

Rollback local bằng stop đúng owned process và -SkipWhisper, khôi phục riêng env
do launcher đặt; giữ DB/volume/User Secrets khác. Không xóa cache hàng loạt.
Plan 2 chỉ được bắt đầu khi owner đã nhận review Plan 1 và yêu cầu tiếp tục.
Whisper binary/model/hash và long-lived lifecycle phải rõ trước khi tái sử dụng
cho audio fallback toàn hệ thống; clip acceptance chưa chứng minh long audio.

## 6. Nguồn

- [local quickstart](../../operations/local-development-quickstart.md),
  [speech contract](../../operations/local-speech.md),
  [engine ledger30/10](../../feature-audits/2026-10-30-asr-engine-acceptance.md).
- [whisper.cpp server](https://github.com/ggml-org/whisper.cpp/tree/master/examples/server),
  [release b5454](https://github.com/ggml-org/whisper.cpp/releases/tag/b5454).
  URL docs không thay pin artifact trong lock.
