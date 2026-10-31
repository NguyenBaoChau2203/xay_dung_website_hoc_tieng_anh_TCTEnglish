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

Pinned engine candidate (ops spike, not auto-installed by the app): whisper.cpp **v1.9.5**, commit `d1be6fde11ac6e0407606b4e42fe72d34add8037`.

Learner audio: WAV mono PCM16 16 kHz only. Client scores are rejected; `POST /api/speaking/{id}/progress` requires `assessmentId` from `POST /api/speaking/{id}/assess`.

When BaseUrl is empty, assess returns **503** `engine_not_ready` — practice/listen still works; no XP from client forgery.

Admission (`SpeechInferenceLimiter`) is **per web process**: `MaxConcurrentInferences` running plus `MaxQueuedInferences` waiting. Overflow returns **429** + Retry-After. Multiple app instances need a limiter at the ASR service; this process cap is not a cluster cap. Rollback: leave `WhisperHttpBaseUrl` empty. Do not fall back to client scores.
