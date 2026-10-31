# ASR engine acceptance notes (G01) — sentinel pass

Date: 2026-10-30 UTC+7. Isolated from production hosting.

## Verified pin (English ≤500 MiB)

| Item | Value |
| --- | --- |
| Model | `ggml-tiny.en.bin` |
| Source | `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin` |
| Size | 77,704,715 bytes (~74 MiB) |
| SHA256 | `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f` |
| License | MIT (HF model card `ggerganov/whisper.cpp`) |
| Local path | `.tmp/asr-acceptance-2930/ggml-tiny.en.bin` (ignored) |
| Verify | `Get-FileHash … -Algorithm SHA256` → HASH_OK |

## Server binary (loopback)

| Item | Value |
| --- | --- |
| Binary release | whisper.cpp `b5454` `whisper-bin-x64.zip` |
| ZIP SHA256 | `6BA69E3482D7826214F90A6A9C84CA07782AEC1E1D0C6A7C30C994FD5D816CCB` |
| Executable | `.tmp/asr-acceptance-2930/bin-b5454/Release/whisper-server.exe` |
| Listen | `127.0.0.1:8090` (`--host 127.0.0.1 --port 8090 -m ggml-tiny.en.bin -l en`) |
| Docker image also pulled | `ghcr.io/ggerganov/whisper.cpp:main` digest `sha256:13d0e7c873c59a262dca621b57ad28de40e9927ac883d936dfd8459142c90db4` (not required once native binary ran) |
| Docs pin v1.9.5 | Tag has no Windows server asset; used verified `b5454` Windows x64 prebuild instead |

## E2E probe (synthetic silence WAV, PCM16 mono 16 kHz, 1 s)

| Probe | Result |
| --- | --- |
| Cold `/inference` | HTTP 200, `time_total≈1.78s`, text `[BLANK_AUDIO]`, `no_speech_prob≈0.905` |
| Warm `/inference` | HTTP 200, `time_total≈1.85s` |
| Working set | ≈164 MiB while serving |
| Unknown path | HTTP 404 |
| Hosting Nhân Hòa ASR | Still owner/provider decision; not used |

Adapter contract regressions remain covered by unit tests. App acceptance host used
`Learning:Speech:WhisperHttpBaseUrl=http://127.0.0.1:8090` on `:5110` only.
