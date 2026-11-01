# Speaking learner import (L5)

- Learner `CreateOwnedVideo` uses **English captions with valid timestamps only**.
- Shared `GetTranscriptForSpeakingImportAsync` (Listening/Admin) tries captions first, then bounded local Whisper audio import with real ASR timestamps.
- Premium Speaking learner import stays captions/SRT/VTT-only; it does not download media.
- Optional async jobs: `ISpeakingImportJobService` + `SpeakingImportWorker` (queued/running/succeeded/failed).
- Subtitle helper: `SpeakingSubtitleParser` for SRT/WebVTT (1 MiB / 2000 cues / 30 min). Rejects invalid/unordered timestamps; never fabricates timecodes.
- If a retry finds the owner video already created for that YouTube id and the transcript hash matches, the job is marked succeeded. A different subtitle hash does not overwrite the existing video.
- The learner form posts `/api/speaking/imports` (or `/api/speaking/imports/subtitles` when an SRT/VTT file is chosen), receives 202, and polls the status URL. `/Speaking/My/Create` remains for older callers.
- Failed or cancelled jobs are requeued on the same row. The unique `(OwnerUserId, RequestFingerprint)` index stays in place.
- The worker publishes the video, sentences and job result in one transaction. Lease token fencing stops a second worker from publishing the same job.
- `SourceContentHash` is additive and nullable. Migration `20261030113000_LearningImportSourceHash` was verified on isolated MariaDB 10.5.23 copies of the hosting backups; it has not been applied to the live hosting database.
- Shared audio import uses pinned FFmpeg8.1.1 (repo-local bootstrap, checksum verified), PCM16 mono16k on owned temporary disk, 10s ASR chunks and the same inference limiter as learner assessment. Limits: source30min/100MiB, decoded PCM57,600,000bytes, total300s including queueing. No partial publish or invented timestamps. Missing engine/decoder, invalid timestamps, codec/download failure and timeout return unavailable with an internal error code.
- [Local provider/key setup and acceptance](openai-local-setup.md). YouTube network/access failures remain separate from engine health or OpenAI key status.
