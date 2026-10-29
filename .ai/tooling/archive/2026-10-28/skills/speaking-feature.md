---
name: Speaking Feature
description: Build or extend speaking practice features (shadowing, dictation, listening) with HTML5 media APIs for TCT English
---

# Speaking Feature Builder

## When to Use
Use when building or extending speaking practice modes: Shadowing (A-B repeat), Dictation (type what you hear), or Listening Comprehension.

## Data Model
```
SpeakingPlaylist (Id, Title, Description, CreatedAt)
  └── SpeakingVideo (Id, PlaylistId, Title, YoutubeId, Level, Topic, Duration, CreatedAt)
        └── SpeakingSentence (Id, VideoId, Text, StartTime, EndTime)

UserSpeakingProgress (Id, UserId, VideoId, SentenceId, Mode, IsCompleted, UpdatedAt)
```

## Speaking Modes

### Shadowing
- A-B repeat using HTML5 `<audio>` with `currentTime` to loop segments
- Loop state: `{ startTime, endTime, isLooping }`
- Store loop points in JS variables, not DOM

### Dictation
- Grading: normalize → trim → remove punctuation → case-insensitive compare
- Partial match: count correct words / total words → percentage score
- Use `StringComparison.OrdinalIgnoreCase` in C#

### Listening Comprehension
- Multiple choice questions about audio content

## Progress Tracking Pattern
```csharp
[HttpPost("api/speaking/{sentenceId}/progress")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SaveSpeakingProgress(int sentenceId, [FromBody] SpeakingProgressDto dto)
{
    var userId = GetCurrentUserId();
    // Verify sentence exists, then upsert progress
}
```

## UI Conventions
- Progress bar: Bootstrap `.progress`
- Score colors: green ≥80%, yellow 50-79%, red <50%
- Level badges: A1=primary, A2=info, B1=success, B2=warning, C1=danger, C2=dark
- YouTube integration: `IYoutubeTranscriptService.ExtractTranscriptAsync(youtubeId)`
