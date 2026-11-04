# Local speech (OpenAI Speech-to-Text)

Replaces the retired local Whisper lifecycle. Assessment and Admin audio-import
fallback call OpenAI file transcription (`gpt-transcribe`) from the server.
Browser/JS never receives the API key, model URL, or raw provider errors.

## Configuration

| Key | Default | Meaning |
| --- | --- | --- |
| `Learning:Speech:Enabled` | `true` | When false, speech stays NotReady without network |
| `Learning:Speech:OpenAI:Model` | `gpt-transcribe` | Official file transcription model |
| `Learning:Speech:OpenAI:BaseUrl` | `https://api.openai.com/v1/` | Audio API root (not `AI:BaseUrl`) |
| `Learning:Speech:OpenAI:ApiKey` | omitted | Optional speech-only secret; explicit empty/invalid does not fall through |
| `Learning:Speech:OpenAI:MaxResponseBytes` | `262144` | Bounded response read |
| `Learning:Speech:InferenceTimeoutSeconds` | `15` | Queue + HTTP + parse deadline |
| `Learning:Speech:MaxAudioSeconds` / `MaxPayloadBytes` | `15` / `2MiB` | Learner clip caps |
| `Learning:Speech:MaxConcurrentInferences` / `MaxQueuedInferences` | `1` / `4` | Process-local admission |
| `Learning:Speech:MaxAssessmentsPerUserPerMinute` | `10` | Per-user guard after access validation |
| `Learning:AudioImport:MaxTranscriptionRequests` | `180` | Windowed import POST cap |

Key resolve order: explicit speech env → explicit speech User Secrets →
`AI__ApiKey` → User Secrets `AI:ApiKey` → `OPENAI_API_KEY`. Text AI stays
`gpt-6-luna` / `low` and does not send `reasoning.effort` into Audio API.

At every key level, explicit empty/invalid stops fallback. Missing and empty are
different: fresh startup leaves AI:ApiKey absent; an existing empty User Secret
stays unchanged. To configure that machine, use `scripts/set-local-ai-key.ps1`
to replace its local override. Keys are never persisted from OPENAI_API_KEY.

## Launcher and status

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
pwsh -NoProfile -File scripts/speech-status.ps1
```

Ordinary startup: MariaDB + web. No local ASR download/start, no paid speech
probe. `speech-status.ps1` is metadata-only (`configured` ≠ live-verified).
It reads local Development appsettings, User Secrets and environment in app
order. Invalid Enabled configuration reports ConfigurationValid=false;
invalid endpoint values are redacted rather than printing URL credentials.

Missing key still opens web/DB; Speaking returns NotReady clearly (no fake scores).

## Media import (FFmpeg pin)

Browser mic PCM does not need FFmpeg. Admin/legacy audio fallback does:

```powershell
pwsh -NoProfile -File scripts/local-media.ps1 -Action Setup
pwsh -NoProfile -File scripts/local-media.ps1 -Action Verify
```

Import plans measured PCM speech windows (segment-window timing, not word
alignment), sends each window WAV to OpenAI, and maps transcript → sample
offsets. New transcript source label: `openai-audio-vad`. Historical
`whisper-audio` rows remain readable.

## Live opt-in (paid)

Default tests/scripts are offline/fake. Owner-only:

```powershell
$env:TCT_OPENAI_SPEECH_LIVE = '1'
# Shared test budget: ≤8 POSTs and ≤120s verified WAV audio per test process.
# Each new test process starts a new budget; repeated runs can incur more cost.
```

Loopback Whisper flags (`TCT_ASR_LIVE_BASEURL`, etc.) must not enable cloud.

## Retired

`scripts/local-speech.ps1`, `tooling/whisper/artifacts.lock.json`, and fake
Whisper fixtures are removed from active setup. Existing ignored
`.appdata/local/speech` caches are never read or started by the launcher. Old
Whisper prompts are historical and explicitly superseded; do not restore their
engine download/start instructions. Preserve recordings/logs when an owner asks
to retire a remaining cache; cleanup must not touch DB data, uploads or API keys.
