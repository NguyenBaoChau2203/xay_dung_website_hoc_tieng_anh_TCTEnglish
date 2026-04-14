# Premium User Speaking — Phase 0 Handoff

## Phase Handoff
Phase completed: Phase 0 — Pivot Audit And Salvage Map
Model used: Claude Opus 4.6 (Thinking) in Antigravity
Ready for next phase: YES

Scope completed:
- Audited entire workspace for Listening scaffold remnants — found only a 9-line placeholder view (`Views/Study/Listening.cshtml`) and zero `ListeningExercise*` entities
- Classified every relevant file as keep / port / supersede (see Section 2 below)
- Designed the recommended Speaking schema shape: extend `SpeakingVideo` with `OwnerUserId`, `SourceUrl`, `SourceType`, `TranscriptSource`, `ImportStatus`, `CreatedAt`; make `PlaylistId` nullable
- Designed indexes: `IX_SpeakingVideos_OwnerUserId_CreatedAt`, unique filtered `(OwnerUserId, YoutubeId)`, `IX_SpeakingSentences_VideoId_StartTime`
- Identified the transcript acquisition redesign: keep caption-first, replace Whisper fallback with Gemini fallback
- Mapped the English-only validation path at both caption and Gemini layers
- Mapped the Standard-visible-but-gated import UX design
- Produced the exact file-to-layer assignment for Phases 1–4
- Identified all security gaps: `SaveSpeakingProgress` lacks parent video access check, `Practice` has no owner filter, Index loads all videos, account deletion missing owned speaking cleanup
- Mapped the `ISpeakingService` service boundary to reduce controller heaviness (addressing TD-002)
- Identified `YoutubeUrlHelper` extraction opportunity from admin controller

Files changed:
- No source code files changed (Phase 0 is audit-only)
- Created `docs/premium-user-speaking-phase0-handoff.md` (this file)

Key rules/policies implemented:
- Feature must live in `SpeakingController` + `Views/Speaking/` boundary
- Learner label is `Bài nói của tôi`
- Import panel visible to all roles; actual import gated to Premium/Admin
- English-only by policy (no user-selectable language/subtitle fields)
- Caption-first transcript acquisition; Gemini fallback only
- `VietnameseMeaning = ""` for private imports (no translation)
- `DeleteBehavior.NoAction` for `OwnerUserId` FK (manual cleanup in account deletion)
- Private videos must never leak into public catalog queries

Commands run:
- No commands executed (audit-only phase)

Verification results:
- Confirmed no `ListeningExercise*` entities exist in the workspace
- Confirmed `YoutubeTranscriptService` currently uses Whisper fallback (must be changed to Gemini)
- Confirmed `GeminiProviderClient` infrastructure already exists in `Services/AI/`
- Confirmed `AccountController.DeleteUserRelatedDataAsync` cleans `UserSpeakingProgresses` but not owned `SpeakingVideos` (gap to fix in Phase 4)
- Confirmed `SaveSpeakingProgress` only checks sentence existence, not parent video ownership (gap to fix in Phase 4)
- Confirmed `SpeakingVideo.PlaylistId` is currently required with cascade delete FK — must become nullable
- Confirmed admin `NormalizeYoutubeId()` handles `youtube.com`, `youtu.be`, `shorts`, `embed` URL formats

Open risks or blockers:
- `Program.cs` must be edited in Phase 2 to register `ISpeakingService` — requires explicit user approval
- Gemini transcript fallback quality is untested — Phase 2 should add minimum sentence count validation
- Making `PlaylistId` nullable and `Level`/`Topic` non-required must not break existing admin create flow or public catalog Index filters — Phase 1 must verify backward compatibility
- Current Whisper fallback code should remain until Gemini fallback is verified working — final removal can happen in Phase 6

If approval is needed before the next phase:
- No approval needed — Phase 1 can start immediately

Next phase should start from:
- Read `docs/premium-user-speaking-feature-plan.md` for the business requirements
- Read `docs/premium-user-speaking-multi-model-execution-plan.md` Section 7 for Phase 1 scope
- Read this handoff for the exact schema shape and constraints
- Start with `Models/SpeakingVideo.cs` to add the new fields
- Then `Models/User.cs` to add `OwnedSpeakingVideos` nav collection
- Then `Models/DbflashcardContext.cs` to configure FK, indexes, and nullable changes
- Then create the additive migration
- Verify backward compatibility: existing admin create flow and public catalog queries must not break

---

## Detailed Salvage Classification

### KEEP as-is

| File | Role in Speaking pivot |
|------|----------------------|
| `Models/SpeakingVideo.cs` | Core entity — extend with new fields |
| `Models/SpeakingSentence.cs` | Core entity — no structural change |
| `Models/SpeakingPlaylist.cs` | Public catalog grouping — unchanged |
| `Models/UserSpeakingProgress.cs` | Progress tracking — reused for private imports |
| `Controllers/SpeakingController.cs` | Extended with import/delete endpoints |
| `Views/Speaking/Index.cshtml` | Extended with import panel + private section |
| `Views/Speaking/Practice.cshtml` | Adjusted for no-translation items |
| `ViewModels/SpeakingIndexViewModel.cs` | Extended with private video list |
| `ViewModels/SpeakingPracticeViewModel.cs` | Adjusted for optional translation |
| `ViewModels/SpeakingVideoViewModel.cs` | Extended with owner/status fields |
| `Areas/Admin/Controllers/SpeakingVideoManagementController.cs` | Shares URL normalization plumbing |
| `Areas/Admin/ViewModels/SpeakingVideoManagementViewModel.cs` | Unrelated to learner flow |
| `Services/YoutubeTranscriptService.cs` | Caption-first logic reused; fallback changed |
| `Services/AI/GeminiProviderClient.cs` | Gemini infra for transcript fallback |
| `Models/DbflashcardContext.cs` | Extended with new entity config |

### PORT into Speaking

| Concept | Source | Target |
|---------|--------|--------|
| Entitlement check | Writing `SourceType` + role checks | `SpeakingService.ImportAsync` |
| Duplicate-per-owner guard | Writing unique check pattern | `(OwnerUserId, YoutubeId)` index |
| Import transaction pattern | Admin `SpeakingVideoManagementController.Create` | `SpeakingService.ImportAsync` |
| YouTube URL normalization | Admin controller `NormalizeYoutubeId()` L332-394 | New `Services/YoutubeUrlHelper.cs` |
| Locked-shell view-model | Writing downgrade pattern | Speaking `MyVideoCardViewModel` |
| Account deletion cleanup | `DeleteOwnedPrivateWritingContentAsync` L707-748 | New `DeleteOwnedPrivateSpeakingContentAsync` |

### SUPERSEDE

| File | Status |
|------|--------|
| `Views/Study/Listening.cshtml` | 9-line placeholder — leave alone, not product path |
| Whisper fallback in `YoutubeTranscriptService` | Replace call path with Gemini in Phase 2 |
| Any future `ListeningExercise*` entities | Never create for this feature |

---

## Schema Shape For Phase 1

### `SpeakingVideo` — new/changed fields

```
OwnerUserId     int?            FK -> Users.UserId, DeleteBehavior.NoAction
SourceUrl       nvarchar(500)?  Full YouTube URL as submitted
SourceType      nvarchar(50)    "admin" (default) | "premium-user-youtube"
TranscriptSource nvarchar(50)?  "youtube-captions" | "gemini-fallback"
ImportStatus    nvarchar(50)    "ready" (default) | "processing" | "failed"
CreatedAt       datetime2       Default SYSUTCDATETIME()
PlaylistId      int? (was int)  Make nullable — private imports have no playlist
```

### `SpeakingVideo` — existing fields nullability adjustment

```
Level     -> remove IsRequired() from Fluent config (allow null/empty for private)
Topic     -> remove IsRequired() from Fluent config (allow null/empty for private)
```

### Indexes

```
IX_SpeakingVideos_OwnerUserId_CreatedAt
IX_SpeakingVideos_OwnerUserId_YoutubeId (unique filtered WHERE OwnerUserId IS NOT NULL)
```

### User entity addition

```csharp
public virtual ICollection<SpeakingVideo> OwnedSpeakingVideos { get; set; } = new List<SpeakingVideo>();
```

---

## Security Gaps For Phase 4

1. **SaveSpeakingProgress** — must verify sentence belongs to accessible video (public OR owned private)
2. **Practice** — must filter by `OwnerUserId == null || OwnerUserId == currentUserId`
3. **Index** — public catalog must filter `WHERE OwnerUserId IS NULL`
4. **Account deletion** — must add `DeleteOwnedPrivateSpeakingContentAsync`
5. **Downgrade** — entitlement check uses current role claim, existing items become locked shells

---

## Transcript Acquisition For Phase 2

Current: `YouTube captions (English) → Whisper fallback`
Target: `YouTube captions (English) → Gemini fallback`

- Keep `ParseClosedCaptions` and auto-generated caption grouping logic
- Replace `ProcessAudioWithWhisperAsync` with Gemini-based transcript generation
- Store which path was used in `TranscriptSource` field
- Add English-only post-validation
- Reject import if no usable English transcript can be obtained
