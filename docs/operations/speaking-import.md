# Speaking learner import (L5)

- Learner `CreateOwnedVideo` uses **English captions with valid timestamps only**.
- Shared `GetTranscriptForSpeakingImportAsync` (Listening/Admin) still may use Gemini fallback — unchanged.
- No Gemini hang in the Premium Speaking import path.
- Optional async jobs: `ISpeakingImportJobService` + `SpeakingImportWorker` (queued/running/succeeded/failed).
- Subtitle helper: `SpeakingSubtitleParser` for SRT/WebVTT (1 MiB / 2000 cues / 30 min). Rejects invalid/unordered timestamps; never fabricates timecodes.
- If a retry finds the owner video already created for that YouTube id and the transcript hash matches, the job is marked succeeded. A different subtitle hash does not overwrite the existing video.
- The learner form posts `/api/speaking/imports` (or `/api/speaking/imports/subtitles` when an SRT/VTT file is chosen), receives 202, and polls the status URL. `/Speaking/My/Create` remains for older callers.
- Failed or cancelled jobs are requeued on the same row. The unique `(OwnerUserId, RequestFingerprint)` index stays in place.
- The worker publishes the video, sentences and job result in one transaction. Lease token fencing stops a second worker from publishing the same job.
- `SourceContentHash` is additive and nullable. Migration `20261030113000_LearningImportSourceHash` was verified on isolated MariaDB 10.5.23 copies of the hosting backups; it has not been applied to the live hosting database.
- ASR-for-import is **Deferred** (no engine, no media download in this remediation).
