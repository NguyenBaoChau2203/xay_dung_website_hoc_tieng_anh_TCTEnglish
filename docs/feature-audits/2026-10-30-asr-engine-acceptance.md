# ASR engine acceptance notes (G01) — round 2

Date: 2026-10-30. Isolated from production hosting.

## Verified pin (English ≤500 MiB)

| Item | Value |
| --- | --- |
| Model | `ggml-tiny.en.bin` |
| Source | `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin` |
| Size | 77,704,715 bytes (~74 MiB) |
| SHA256 | `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f` |
| License | MIT (HF model card `ggerganov/whisper.cpp`) |
| Local path | `.tmp/asr-acceptance-2930/ggml-tiny.en.bin` (ignored) |
| Verify command | `Get-FileHash … -Algorithm SHA256` → HASH_OK |

## Status

- Adapter contract regressions: **pass**
- Model pin/hash/license: **verified and downloaded**
- Real engine E2E (WAV → assessment → receipt → progress → credit): **Blocked**
  - Exact gap: no `whisper-server` / whisper.cpp HTTP binary on PATH or in checkout after search
  - `.tmp/learning-reliability-audit` only has README/source notes, not a runnable server
  - Docker images on machine: no whisper/asr image present (`docker images` filter empty)
  - Hosting Nhân Hòa ASR endpoint still a separate owner/provider decision

## Next owner/provider step

Build or install whisper.cpp server for the pinned model on loopback, set
`Learning:Speech:WhisperHttpBaseUrl`, then run synthetic PCM16 mono 16 kHz WAV
through assess/progress and record cold/warm latency.
