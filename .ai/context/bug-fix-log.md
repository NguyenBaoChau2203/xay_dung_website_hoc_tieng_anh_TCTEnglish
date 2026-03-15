# TCT English — Bug Fix Log

After every bug fix, the agent **must append one entry** to this file.
This is a historical record of actual fixes — not a list of pending issues (see `known-issues.md`).

---

## How Agents Should Use This File

1. Read this file before fixing any bug.
2. Start from the newest entries and search for matching symptoms, stack traces, entities, controllers, or regression patterns.
3. Reuse a previous fix pattern only after confirming the same root cause exists in the current code.
4. If a previous entry influenced the solution, mention that relationship in `Notes`.
5. After the fix is verified, append one new entry below the marker in the "Fix History" section.

---

## Entry Format

```
### [BUG-ID or short description] — [fix date]

**Symptom**: What the user observed (HTTP code, broken UI, wrong data...)

**Root Cause**: Why it happened — which layer, file, or logic was wrong

**Solution**: What was changed and where — be specific

**Files Changed**:
- `path/to/file.cs` — line X: brief explanation

**Verification**: How the fix was confirmed (manual steps, automated test, regression check)

**Commit**: `fix(scope): message` — hash if available

**Notes**: Regression warnings, edge cases to watch, or related previous fixes (if any)
```

---

## Fix History

<!-- Agent: append new entries BELOW this line, newest first -->

---

### Speaking Video 500 Error — 2026-03-15 (sample entry)

**Symptom**: `/Speaking/Video/{id}` throws `NullReferenceException`, HTTP 500

**Root Cause**: `SpeakingSentence` was loaded without `.Include(v => v.SpeakingVideo)` — EF did not load the navigation property, resulting in a null reference

**Solution**: Added `.Include(s => s.SpeakingVideo)` to the query in `SpeakingController`

**Files Changed**:
- `TCTEnglish/Controllers/SpeakingController.cs` — line ~45: added `.Include(s => s.SpeakingVideo)`

**Verification**: Opened `/Speaking/Video/{id}` again, confirmed HTTP 200, and checked other speaking video queries for the same missing navigation include pattern

**Commit**: `fix(speaking): add Include for SpeakingVideo navigation property`

**Notes**: Check other queries in the same controller for the same missing Include pattern

---
