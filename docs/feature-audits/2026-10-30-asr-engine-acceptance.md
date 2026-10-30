# ASR engine acceptance notes (G01)

Date: 2026-10-30. Isolated from production hosting.

## Candidate pin (not auto-installed)

| Item | Value |
| --- | --- |
| Engine | whisper.cpp |
| Tag/commit candidate | v1.8.x / see `docs/operations/local-speech.md` (verify before download) |
| Model budget | English model ≤ 500 MiB |
| Required before download | official tag/commit, image digest or model SHA256, license text |
| Audio | synthetic or licensed fixture, PCM16 mono 16 kHz |
| Endpoint | loopback only; no client-supplied URL/model |

## Status this run

- Adapter contract regressions: **pass** (`WhisperHttpTranscriberTests`).
- Real engine E2E (WAV → assessment → receipt → progress → credit): **Not run**.
  Reason: no pre-verified pinned model artifact with hash/license present in this
  checkout; free RAM ~2.7 GiB but download/verify was deferred to keep the run
  inside approved scope without guessing digest/license. Hosting Nhân Hòa ASR
  endpoint remains a separate owner/provider decision.

## How to close later

1. Verify pin + SHA256 + license for a ≤500 MiB English model.
2. Start loopback whisper.cpp with that model; set `Learning:Speech:WhisperHttpBaseUrl`.
3. Run synthetic WAV through assess/progress and record cold/warm latency + memory.
4. Keep Web Speech preview-only; no fake XP on unavailable/cancel/silence.
