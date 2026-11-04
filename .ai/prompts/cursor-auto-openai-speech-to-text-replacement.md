# Cursor Auto — thực thi thay toàn bộ Whisper bằng OpenAI Speech-to-Text

Đây là prompt owner giao để IMPLEMENT, không chỉ phân tích hoặc viết lại Plan.
Một agent thực thi tuần tự; Auto chọn model IDE nào cũng phải giữ các yêu cầu.
Không spawn subagent, worker, reviewer AI, chat/CLI/model process khác.

## 1. Đọc và scope

Đọc AGENTS.md và Plan:

    docs/superpowers/plans/2026-11-03-openai-speech-to-text-replacement-plan.md

Áp dụng tct-superpowers, tct-engineering và tct-speaking-feature theo từng batch.
API/security/EF/tooling/UI skills chỉ đọc lúc batch cần. CodeGraph trước indexed
source, bounded reads cho gap/unindexed scripts; không init/rebuild CodeGraph.
Nguồn API: tài liệu OpenAI chính thức trong Plan, không source/blog tổng hợp.

Hai Plan UI/logic trước đã đóng local. Công việc mới này chỉ thay dependency
Whisper/audio vận hành, giữ kết quả trước. Workspace hiện có nhiều dirty và
untracked files (baseline lập Plan khoảng129), HEAD210bf2d, nhánh
feature/chau-ui-performance-audit. Snapshot hiện trạng trước sửa, không reset,
clean, stash hoặc checkout master làm mất work. Không sửa file BTH2/bài đồng đội
ngoài caller bắt buộc của scope. Không commit baseline để né dirty tree.

Tôi cho phép chỉnh DI Program.cs, appsettings không secret, scripts, active
agent instructions/docs cần cho Plan. Dự kiến không cần .csproj/dependency mới
và không đổi schema. Nếu persisted model thật sự phải đổi, theo EF procedure
và provider tests; không sửa migration đã áp dụng/Down về baseline.

Phạm vi là local. **Không commit/push/PR/CI remote/merge/deploy/Plesk**;
PLESK_DEPLOYMENT_ENABLED tiếp tục false. Không gọi API trả phí tự động.

## 2. Các quyết định đã chốt

1. Thay toàn bộ active Whisper bằng OpenAI Speech-to-Text, không giữ local/
   Colab/Whisper fallback. Model audio target gpt-transcribe theo docs03/11;
   trước khi khóa request mở lại đúng official docs. Không chọn model deprecated
   hoặc thay model theo IDE Auto. Text AI giữ gpt-6-luna / low.
2. Server gọi file transcription API bằng HttpClient, không đưa key/model URL
   vào JS/browser. Không gửi reasoning của text model vào Audio API.
3. Resolve audio key explicit env/User Secrets trước, rồi shared AI key theo
   precedence hiện có. Owner đã cho phép OPENAI_API_KEY fallback cho TCT; chỉ
   resolve memory/child process, restore env. Dùng lại key đang có, không yêu cầu
   owner paste key. Không log/argv/commit/persist một bản sao key khi fallback.
4. Key có mặt chỉ là configured; không suy account có model/quota. Missing key
   vẫn chạy web/DB cho developer; Speech fail rõ, không fake điểm.
5. Assessment dùng transcript + text-match hiện hữu. Không prompt reference text
   vào ASR, không post-process transcript theo đáp án. Không bịa fluency/phoneme.
6. Bảo toàn WAV15s/2MiB, deadline15s gồm queue+HTTP+parse, concurrency1/queue4,
   caller cancellation và release lease. Thêm guard assessment theo user10/phút
   sau access validation; không lấy quota15 câu chat cho audio.
7. No automatic retry sau POST hoặc fallback provider khi outcome chưa rõ.
   Giữ UI chủ động retry sau thông báo; không hứa provider không charge khi cancel.
8. Import legacy/admin cần timecodes. Dùng measured PCM speech-window planner
   thuần C# theo mục4.2 của Plan, tối đa10s/window. Mỗi transcript map một window
   thật; timestamps từ sample offsets, nguồn openai-audio-vad. Không chia đều
   thời lượng theo chữ/câu, không gắn0..duration giả, không tắt guard timestamp.
   Nêu rõ đây là segment-window timing, không word alignment.
9. Giữ captions/SRT/VTT trước và learner captions-only hiện có; không bật cloud
   fallback mới cho learner. Cap180 request/import, total300s, all-or-nothing,
   temp-owned cleanup, yield shared admission giữa windows.
10. Docker MariaDB giữ như cũ; FFmpeg pin vẫn cho media import. Ordinary web/
    mic PCM không download/probe Whisper hoặc cần chạy model/FFmpeg.

## 3. Thực thi STT0–STT8, không bỏ batch

Tạo ledger:

    docs/feature-audits/2026-11-03-openai-speech-replacement-ledger.md

Mỗi checkpoint ghi files/contracts/checks/pass-fail/remaining/latency/cost limits.
Đọc đúng batch và source liên quan, không load mọi audit cũ.

### STT0 — xác minh baseline và inventory

- Ghi branch/HEAD/dirty snapshot và bảo vệ source cũ; đọc bug memory phù hợp.
- Inventory Whisper runtime/options/DI/scripts/native tasks/active docs/tests/
  fixtures/config keys; distinguish historical records không cần sửa.
- Xác minh app port/process ownership trước thao tác. Không kill theo tên hay
  restart workload cũ để lấy benchmark. Dùng free port nếu chưa có quyền restart.
- Dựng baseline offline/fake; số862pass/13skip là evidence cũ, không pass tự động
  cho source mới. Startup phase measurement theo Plan, không giả số trước–sau.

### STT1–3 — config, adapter và Speaking

- Viết red behavior tests fake HTTP/config, rồi implement minimal options/
  resolver/OpenAiSpeechTranscriber và typed client DI.
- JSON strict, error/status/response-size/cancel/deadline/queue/lease đúng Plan.
  AllowAutoRedirect=false hoặc policy tương đương ngăn credential rời host.
- Test config precedence, explicit invalid key không silent-switch, no network
  khi missing key, key chỉ ở header, text model/key behavior không regress.
- TestWebApplicationFactory mặc định dùng fake speech hoặc blocked outbound:
  ordinary tests không được đi Internet chỉ vì máy có OPENAI_API_KEY.
- Update SpeakingAssessmentService metadata/status; access/validation trước paid
  admission. Test per-user guard với fake clock, receipt/replay/XP unchanged.

### STT4 — media import với timestamp thật của cửa sổ audio

- Viết unit tests planner/RIFF/frame/sample offsets trước implement.
- Tạo PCM windows bằng năng lượng/khoảng nghỉ, trim/pad/merge có cap; không thêm
  model ML/python/CUDA để thay một dependency nặng bằng dependency nặng khác.
- Importer cắt đúng bytes, gửi từng WAV, gắn transcript vào window được gửi.
  Assert request audio bytes khớp timebase và end<=actual source duration.
- Kiểm continuous speech/hard bound, quiet voice/noise, partial failure, total
  deadline, request cap, lease fairness, cleanup, >=3 usable cues khi có thật.
- Không nhân bản/tách cue rồi chia đều thời gian. Không biến test
  PlainTextWithoutSegments_IsRejected thành “text nào cũng publish”; chuyển
  coverage để chỉ importer có measured windows mới được dựng timed cues.
- YoutubeTranscriptService source mới cho writes mới; dữ liệu/history source
  whisper-audio cũ đọc nguyên. Learner captions/subtitle routes gọi0cloud audio.
- Không sửa worker retry không liên quan: hiện worker learner không gọi audio.
  Nếu phát hiện caller audio bị retry ngoài adapter, xử lý đúng phạm vi/bằng test.

### STT5–6 — launcher và cả bốn AI clients

- Bỏ start/setup/inference-probe/model download từ start-local. Giữ busy-port,
  DB ownership, User Secrets, env restoration, bootstrap không thay DB có users.
- Tạo speech-status.ps1 chỉ đọc metadata; không health-check cloud bằng request
  tính phí. Startup/default setup/doctor/build/hooks/CI không Speech API calls.
- Giữ local-media pin; Setup/Verify là bước import riêng, không kéo dependency
  này chỉ để mở web/ghi âm PCM. Missing decoder báo rõ ở import.
- Chuyển consumers/helpers/test fixtures trước retire local-speech.ps1 và
  tooling/whisper/artifacts.lock.json. Không xóa cache/trust/volume ngoài scope.
  Stop old process chỉ khi ownership được chứng minh; không dựa process name.
- Sửa scripts/agent_clients.py rồi generated IDE tasks, current AGENTS/.ai guide/
  native adapters và README/onboarding/local-speech/openai setup/import docs.
  Cả Codex/Cursor/Claude Code/Antigravity dùng cùng canonical command mới.
- Không blanket sửa tất cả chữ Whisper trong historical audits/migration/data.
  Ghi allowlist hợp lý cho history/read-compat; active runtime không còn dùng nó.
- Tách _find_pwsh/helper imports khỏi tests lifecycle cũ trước xóa, tránh làm
  toàn bộ script unittest fail vì thiếu module test_local_speech.

### STT7–8 — xác minh và chốt

Chạy commands phù hợp ở mục7 Plan: focused .NET, JS recorder/Speaking, full
script unittest, agent_tooling.py, bootstrap checks nếu sửa generator/setup,
full solution Release, EF pending-model, git diff --check và secret scan.
Database acceptance chỉ dùng connection explicit đã kiểm loopback/tên DB/
sentinel/runId trước DML; không dùng design-time CLI fallback User Secrets để
dựng baseline, không chạy Down hoặc reset DB local của owner.
Tooling pass không thay app/browser acceptance. Nếu native hooks đổi thì chạy
check_team_hooks.py; không chỉnh trust flag hoặc index CodeGraph.

Browser local fake provider: Standard/Premium/Admin/outsider, mic flow/cancel/
change sentence/no-key/network error, receipt/reload/replay, Admin audio import,
learner subtitles, mobile390px và desktop. Giữ iframe YouTube và UI hiện hữu.
Đừng ghi “đã test mic thật” nếu chỉ dùng WAV synthetic.

So startup3cold/3warm cùng điều kiện khi có baseline hợp lệ. Chứng minh không
spawn Whisper, không download model,0paid requests lúc launch/health. Không dùng
app binary cũ làm chứng cứ cho source/key mới, không bỏ migration guard cho nhanh.

## 4. Live acceptance có tính phí — mặc định tắt

Implement test/script opt-in TCT_OPENAI_SPEECH_LIVE=1, budget đề xuất tối đa8POST
và120s audio tổng cho một đợt, fail-fast, no retry. Counter tại HTTP boundary
tính cả failed calls; không reset budget theo test method, không chạy cost loop.
Flags loopback Whisper cũ không được bật cloud. Tất cả test khác fake/offline.

**Không tự bật live flag, không gọi OpenAI chỉ để kiểm key.** Sau offline xong,
báo owner script/command và budget cụ thể để owner bật/cho phép riêng. Tiếp tục
hoàn tất implementation/docs trong lúc live chưa được phép. Ghi live unverified,
không gọi nó pass hoặc blocker giả cho phần code đã có thể hoàn tất.

## 5. Handoff bắt buộc

Self-review toàn diff trong scope bằng một agent. Sửa các lỗi còn lại có bằng
chứng; không kết thúc bằng TODO “chưa nối import/launcher” hoặc “đợi model khác”.

Trả tiếng Việt:

- Đã thay những runtime/caller/dependency nào, file chính và config/key source.
- Tests thực chạy: count/pass/fail/skip, fake vs live, browser và startup evidence.
- Timestamp window timing/accuracy limits; account/quota chưa verified khi live off.
- Model text vẫn Luna low; không secret/DB reset/remote CI/production change.
- Ledger/quickstart cho teammates và gates cần owner nghiệm thu trước25/11.

Không tự commit/push/PR/merge/deploy. Nếu context compact, tiếp tục checkpoint
đang dở từ ledger, không chạy lại toàn bộ hoặc quay sang một Plan cũ.
