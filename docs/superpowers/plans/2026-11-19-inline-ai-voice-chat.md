# Inline voice mode in the existing chatbot

Owner request: switch between text and voice inside the current floating/full
chatbot, without navigation or a new tab, and add expressive TCT fox artwork.
Work stays on feature/ai-voice-conversation, one agent, local only. Existing
Program.cs approval is limited to the earlier DI line. No protected-file, paid
test, Git or release authority is expanded by this plan.

## Scope and reuse

- Reuse the authenticated voice APIs, WebRTC helper, coordinator, quota, private
  topics/history and effective configuration validation. No schema/API changes.
- Extract the existing Voice view into one typed workspace partial. A small MVC
  view component supplies provider availability for full/embedded chat, avoiding
  feature logic in the existing text controller or duplicate voice workspaces.
- Keep chat DOM, IDs, draft, transcript, stream/history and text quota contracts.
  Mode buttons replace the navigation link in _ChatShell (full and embedded).
- Scoped CSS provides a compact, scrollable voice workspace and state mascot;
  no shared token/layout/root palette change. Existing standalone route remains
  available for existing bookmarks; chatbot entry stays inside the shell.
- Closing the floating launcher notifies its same-origin child to stop media.
  Switching to text awaits cleanup confirmation; failed cleanup retains voice
  controls for retry. Entering voice never starts a provider call or microphone.
- Generated asset: six equal-cell fox expressions in one transparent PNG atlas,
  based on the owner's reference, built-in ImageGen. Preserve existing brand
  assets. Record the prompt and provenance next to the new asset.

## Steps and acceptance

- [x] Implement and test mode transitions, cleanup concurrency/failure behavior.
- [x] Extract typed workspace; wire full/embedded chat and load existing voice
  scripts/Bootstrap support. Preserve availability and ownership boundaries.
- [x] Add scoped responsive presentation, state sprites and clear turn guidance.
- [x] Integrate launcher-close lifecycle with origin/source guards and microphone
  permission. Exercise custom-topic modal/focus inside the iframe.
- [x] Build and run relevant .NET/Node regressions. Browser: no navigation/new
  window, unchanged draft/history, no paid calls on mode switch, ready/listening/
  thinking/speaking/muted/error/pending cleanup, retry, launcher close, 1440
  expanded/collapsed, 768 and 375–390, keyboard/reduced motion/zoom where available.
- [x] Review changed consumers and evidence, restart authorized local preview,
  hand off in Vietnamese with skipped checks and remaining limits. No commit,
  push, PR, CI or deployment; no DB reset, credential write or quota refund.

Evidence: docs/feature-audits/2026-11-19-inline-ai-voice-chat.md. Actual native
200% zoom was attempted but not observed in headless Chrome; no physical-mic/live
provider acceptance rerun for this UI change. Do not count these as passed checks.
