---
name: Vocabulary Feature
description: Build or extend vocabulary/flashcard features for TCT English (sets, cards, study modes, folders)
---

# Vocabulary Feature Builder

## When to Use
Use when building or extending vocabulary features: flashcard sets, cards, study modes (quiz, write, match), folders, and learning progress.

## Data Model
```
User → Folder (nested, ParentFolderId) → Set (OwnerId) → Card (Term, Definition, Phonetic, Example, Topic)
                                                            └── LearningProgress (UserId, CardId, Status: new|learning|mastered)

User → Class → ClassMember (Owner|Member) → SavedFolder
```

## Study Modes
| Mode | View | Key Logic |
|------|------|-----------|
| Flashcard | Study.cshtml | Card flip, mark known/unknown |
| Quiz | QuizMode.cshtml | 4 choices, distractors from same set |
| Write | WriteMode.cshtml | Type term, normalized grading |
| Matching | MatchingMode.cshtml | Drag pairs, max 8 per round |
| Read | Reading.cshtml | Passive review |

## Card CRUD Pattern
```csharp
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> AddCard(int setId, CardViewModel model)
{
    var userId = GetCurrentUserId();
    var set = await _context.Sets.FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == userId);
    if (set == null) return NotFound();
    // Validate, create card, SaveChangesAsync
}
```

## Progress Tracking
```csharp
// Status progression: new → learning → mastered
// Track per-card, per-user, per-mode
// Save asynchronously via API (don't block study UX)
```

## UI Conventions
- Card flip: CSS `transform: rotateY(180deg)` with `perspective`
- Progress bar: `known/total` ratio updated in real-time
- Streak: fire emoji 🔥 + count in Bootstrap `.badge`
- Empty state: "No cards yet. Add your first card!" CTA
- Topic tags: Bootstrap `.badge .bg-secondary`

## Grading Rules
- Write mode: trim + lowercase + remove extra spaces before comparison
- Quiz mode: distractors from SAME set (not random)
- Match mode: max 8 pairs per round
