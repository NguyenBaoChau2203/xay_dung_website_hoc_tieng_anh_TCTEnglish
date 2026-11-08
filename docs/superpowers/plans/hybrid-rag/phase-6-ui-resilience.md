# Phase 6 — source UI, cache, diagnostics and browser acceptance

Entry: Phase 5 APPROVED, matching scope, Phase 6 READY and owner launch.
Mode IMPLEMENT_LOCAL. Skills: tct-superpowers, tct-engineering, tct-ui-component,
tct-ui-review and security; API/admin skills for affected endpoints. Read DESIGN.md.
Tentative dates 20–21 Nov. Preserve the existing Razor/Bootstrap interface identity.

## Outcome and boundaries

Make evidence and refusal states clear in full/embed chat, keep the interface usable
through streaming/history/errors and make cache/diagnostics preserve current access.
Browser checks with controlled providers are required; live API quality is Phase 7.

Established UI: `TCTEnglish/Views/Ai/{Chat,ChatEmbed,_ChatShell}.cshtml`,
`wwwroot/js/ai-chat.js`, `wwwroot/css/ai-chat.css`, launcher integration and existing
SignalR/HTTP contracts. Inspect current DOM and rendering callers before editing.
Do not introduce a frontend framework, new CDN, broad redesign or model changes.

## Ordered checklist

- [ ] Render a compact source area using structured server metadata, encoded titles
      and server-generated allowlisted local links. Show capture/freshness only when
      useful; do not show internal scores, private IDs, raw errors or implementation
      terminology to learners. Do not render arbitrary model HTML/URLs.
- [ ] Cover answered, insufficient evidence, clarification, outside scope and
      temporary unavailability with clear Vietnamese text and accessible controls.
      Preserve the draft on errors and allow a deliberate retry without duplication.
- [ ] Show identical authorized source/outcome data after HTTP send, SignalR complete,
      full/embed reload and multiple load-older pages. Never display partial unvalidated
      model factual text while waiting for the server's accepted answer.
- [ ] Preserve Enter submit guard, quick actions, Stop, focus behavior, scroll and
      chronological paging. Handle late/duplicate/out-of-order stream events by the
      existing conversation/stream identity; stop/retry cannot replay stale sources.
- [ ] Implement bounded caches only for a documented useful layer. Personalized
      results are keyed by user/access scope, source version and relevant context;
      do not share personal fact answers. Reauthorize cached sources on return.
- [ ] Invalidate on content/version/publication, membership/entitlement and account
      changes. Expiry is a resource bound, not a substitute for current authorization.
      Test A→logout→B and same-user downgrade/revoke with warm cache/history.
- [ ] Log structured request/source keys, version, retrieval mode/outcome, stage
      latency, token usage and safe error codes. No full prompts/private source text,
      secrets, model reasoning or entire provider payload. Verify error sanitization.
- [ ] Expose operator diagnostics only through existing authorized admin boundaries
      if needed; no new learner-facing internals. If a rebuild control is added to
      existing Admin AI maintenance, preserve CSRF/access/budget/dry-run restrictions.
- [ ] Add meaningful JS/browser tests for sources/state/stream/retry/history and
      server cache/access/log tests. Keep existing Node chat guards passing.
- [ ] Run the local launcher with MariaDB, preserve unknown listeners and use a
      free port/isolated configuration when required. Confirm `/health/ready` and
      `scripts/speech-status.ps1` metadata; no startup speech/embedding/text request.
- [ ] Use a recording loopback provider/test-host setup for nonpaid browser flows;
      verify zero unintended external requests. Keep real saved keys unchanged,
      scope any fake credentials/endpoints to child/test processes and restore env.
- [ ] Verify full/embed at desktop and narrow viewport, keyboard-only navigation,
      200% text zoom, long Vietnamese/English content, source-link focus and errors.
      Preserve recording/screenshots in ignored task-local storage with no credentials.
- [ ] Review UI/service diff and write a reproducible browser acceptance report,
      exact startup/port/provider modes and handoff/scope.

## Required browser and resilience matrix

| Journey | Required result |
| --- | --- |
| Full and embedded chat: send, complete, reload, older pages | Same answer/sources, readable order and no duplicate message |
| Double Enter/quick action while sending; Stop then retry | Draft/guard preserved; no second unintended request or stale stream |
| Malicious title/HTML/link, revoked source, legacy row | Encoded safe content, access-safe body, sensible missing sources |
| API timeout/429/network loss, missing/corrupt index | Clear typed state; no invented fallback or stuck loading |
| A→B, role downgrade, membership removal with warm cache | No cross-account or revoked source/title/body/fact leak |
| Keyboard, narrow viewport, zoom and long source titles | Visible focus, operable controls, readable content without trapped UI |

Run new focused C#/JS/browser regressions, affected earlier-phase access/history/
cancel tests, build and the established Node submit/load-older tests. Record actual
browser viewport/actions/assertions and fake-versus-live mode; static DOM grep is
not browser proof. If browser execution is unavailable, this required phase gate
is BLOCKED. No paid-provider call or hosting change is implied by local startup.

## Exit

Deliver accessible source UI, bounded access-safe cache behavior, sanitized
diagnostics and a real local browser report. Keep final quality/latency/live account
claims pending Phase 7. Set Phase 6 AWAITING_REVIEW and stop; Phase 7 stays LOCKED.
