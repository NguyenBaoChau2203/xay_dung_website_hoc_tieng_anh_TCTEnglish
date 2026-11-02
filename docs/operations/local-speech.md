# Local speech / ASR (L4)

Configuration (`Learning:Speech` — do not put secrets in appsettings committed to git):

| Key | Default | Meaning |
| --- | --- | --- |
| `Learning:Speech:WhisperHttpBaseUrl` | empty | Internal whisper.cpp HTTP base. Empty = engine not ready |
| `Learning:Speech:WhisperInferencePath` | `/inference` | Only `/inference` is used (not `/load`) |
| `Learning:Speech:ModelIdentity` | `whisper.cpp-unconfigured` | Recorded on assessments |
| `Learning:Speech:InferenceTimeoutSeconds` | `15` | One budget for queue wait and HTTP/parse. It is not reset after a slot is granted |
| `Learning:Speech:MaxAudioSeconds` | `15` | Mic clip limit |
| `Learning:Speech:MaxPayloadBytes` | `2097152` | 2 MiB |
| `Learning:Speech:AssessmentTtlMinutes` | `10` | Progress receipt lifetime |

## Daily local launcher (preferred)

When an agent or developer needs the web app for speech testing:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Order: MariaDB → pinned Whisper (`127.0.0.1:8092`, `tiny.en`, 2 threads) → web
`http://localhost:5100`. The launcher sets process env
`Learning__Speech__WhisperHttpBaseUrl` / `ModelIdentity` for the child web process
and restores those keys when it exits. Whisper keeps running across web restarts.
The same launcher selects `AI__ApiKey`, app User Secrets, then the owner-approved
`OPENAI_API_KEY` fallback for cloud text. It reports only the key source and
configured status; see [OpenAI setup](openai-local-setup.md). A busy web port is
refused before build; an older process is not proof of current speech/key config.

| Flag | Behavior |
| --- | --- |
| `-NoBrowser` | Still starts Whisper |
| `-SkipWhisper` | Web/DB only; prints that ASR was not ensured (no fake scores / cloud audio fallback) |
| `-DatabaseOnly` | MariaDB only; no Whisper download/start and no web |

Helper commands:

```powershell
pwsh -NoProfile -File scripts/local-speech.ps1 -Action Status
pwsh -NoProfile -File scripts/local-speech.ps1 -Action Stop
pwsh -NoProfile -File scripts/local-speech.ps1 -Action Setup
pwsh -NoProfile -File scripts/local-speech.ps1 -Action Benchmark -BenchmarkSeconds 5
```

Pinned metadata (commit): `tooling/whisper/artifacts.lock.json` (whisper.cpp **b5454**
Windows x64 + `ggml-tiny.en.bin`). Binaries/models live under ignored
`.appdata/local/speech/`. Setup reuses a hash-matching legacy cache when present;
hash mismatch refuses execute. Do not start Whisper on folderOpen, MCP, ordinary
build/test, or lifecycle hooks.

Stop only kills a process whose PID + start time + OS executable path + hash +
port match this checkout (state.commandLine alone cannot authorize Stop).
Ctrl+C on the web process does not stop MariaDB or Whisper.

Launcher logs under `.appdata/local/speech/logs/` record runId/pid/cmdline
markers from the helper. WMI process create does not capture whisper-server
stderr; treat those files as launcher diagnostics, not full engine console
output.

Readiness uses one total deadline, including each HTTP probe and retry delay.
A valid inference response arriving after the deadline does not mark the engine
ready. The probe timeout is capped by the remaining startup budget.

## Explicit live app acceptance

The speech-through-app test is opt-in and uses an isolated test database. Supply
a permitted WAV (PCM16 mono16kHz, at most15s) saying **Good morning everyone**:

```powershell
$env:TCT_ASR_LIVE_BASEURL = 'http://127.0.0.1:8092'
$env:TCT_ASR_LIVE_SPEECH_WAV = 'C:/path/to/permitted-speech.wav'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter FullyQualifiedName~WhisperLiveAppAcceptanceTests
```

Discovery never calls the engine. Missing opt-in skips the test; an explicitly
configured missing file, unavailable engine or failed recognition fails it. The
test goes through app authentication/antiforgery, actual Whisper HTTP, assessment,
progress, one completion credit, replay and reload. Its SQLite fixture does not
modify the development or hosting database and does not prove MariaDB races or
browser microphone permissions. Synthetic offline speech is useful integration
evidence; it does not measure human pronunciation quality.

## App contract

Learner audio: WAV mono PCM16 16 kHz only. Client scores are rejected;
`POST /api/speaking/{id}/progress` requires `assessmentId` from
`POST /api/speaking/{id}/assess`.

When BaseUrl is empty, assess returns **503** `engine_not_ready` — practice/listen
still works; no XP from client forgery.

Admission (`SpeechInferenceLimiter`) is **per web process**:
`MaxConcurrentInferences` running plus `MaxQueuedInferences` waiting. Overflow
returns **429** + Retry-After. Multiple app instances need a limiter at the ASR
service; this process cap is not a cluster cap. Rollback: leave
`WhisperHttpBaseUrl` empty or launch with `-SkipWhisper`. Do not fall back to
client scores.

`/health/ready` proves DB/app readiness only. Speech status is separate
(local-speech Status / launcher summary).

## Plan 2: shared audio import

Listening/Admin dùng Whisper khi thiếu captions. Learner Speaking vẫn nhận
captions/SRT/VTT. Launcher verify FFmpeg8.1.1 bằng `scripts/local-media.ps1`;
build/test/hook thường không start Whisper hay tải artifacts. Giới hạn media và
live opt-in: [OpenAI local setup](openai-local-setup.md). Không gửi audio tới Luna.
