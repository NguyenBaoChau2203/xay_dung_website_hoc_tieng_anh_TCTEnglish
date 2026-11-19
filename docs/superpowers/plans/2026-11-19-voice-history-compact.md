# Compact voice history

Owner asks for a cleaner voice-history list in the chatbot. Keep one agent and
local-only work on the current branch; preserve all existing work and authority.

- [x] Inspect the current renderer, shared workspace and feature-scoped CSS.
  Cause: combined title/date link wraps unpredictably; flex shrinks the delete
  text. The oversized wrapping header competes with pagination in the iframe.
- [x] Render separate title/time lines with safe DOM text, compact minute-level
  local date/time, and a fixed 44px trash control with its accessible name.
  Use a stable two-column row and compact header; preserve IDs/API handlers.
- [x] Browser-check full chat and floating history at desktop, tablet and mobile,
  long titles, pending/empty/error states, read/delete/cancel/paging and focus.
  Use intercepted fake history; do not read/delete actual private conversations
  or create any paid voice session. Run existing focused JS checks and syntax.
- [x] Record evidence and leave local web ready. No schema, protected config,
  dependencies, commit, push, PR or deployment. No new unit tests for this small,
  reversible presentation change; browser acceptance is the meaningful check.

## Evidence

- Changed only the history renderer in ai-voice-page.js and feature-scoped
  ai-voice.css. API/ownership/antiforgery, stored data and handlers are unchanged.
  CSS/JS assets are served directly; no .NET rebuild needed for this static change.
- Existing mode/audio/coordinator Node suite: 23 passed, 0 failed/skipped.
  JavaScript syntax and git diff --check pass. Local readiness HTTP 200.
- Chrome browser: six layouts (full chat 1440/768/390/375, floating desktop/mobile),
  aligned 44×44 delete controls, ellipsis for long names, readable separate date
  and minute time, pending label, zero horizontal overflow and page errors.
  Read by Enter, delete cancel/confirm, next/previous paging and empty/error
  feedback pass. Hostile title is literal text with no injected image element.
- All voice/history API calls intercepted: no actual private-history deletion,
  no voice session create or paid provider request. Screenshot preview uses
  synthetic titles. Evidence/snapshots are ignored under
  .appdata/local/ai-voice-tests/history-compact-*.json/png.
- Native 200% zoom and hosting were not observed for this small presentation
  change. The running preview remains on port 5107; no runtime/credential edits.
