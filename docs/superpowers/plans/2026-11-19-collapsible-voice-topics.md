# Collapsible voice topic card

Owner requests expand/collapse for the whole topic selection card to save space.
Use native details/summary, initially open. A compact closed summary keeps the
current topic title/level visible. Keep IDs, selected values, quotas, custom-topic
tools, API handlers and voice lifecycle unchanged; no persistence/config changes.

- [x] Wrap existing controls in a native disclosure and style the focus/chevron
  with scoped tokens. Update summary using safe DOM text on selection/reload.
- [x] Build, run existing voice/mode checks and browser-check full/embedded chat,
  desktop/mobile, Enter/Space toggling, preserved selection/tools, and no voice
  create/microphone side effects. Use fake endpoints, not paid calls.
- [x] Record evidence/limits and leave local ready. No new unit test for this
  small native disclosure; no protected files, data, dependencies, Git or deploy.

## Evidence

- Release build and existing render/API-security tests: 8 passed, 0 failed/skipped.
  Existing mode/audio/coordinator Node tests: 23 passed. Syntax/whitespace pass.
- Chrome seven cases: full chat at 1440/768/390/375, standalone workspace, floating
  desktop/mobile. Native Enter/Space and click toggle, selected private topic and
  inner tools state survive collapse/reopen/reload. Collapse also survives mode
  switching within the page; it is not written to storage across reload/logout.
- In inline cases the closed card is 74–78px high; expanded private-tools fixture
  is 319–352px. These are observed fixture heights, not fixed savings for every
  account/content combination. Long closed titles truncate with a full tooltip;
  no horizontal overflow or page errors. Screenshot inspected.
- Fake read-only topic/history endpoints: zero microphone calls, voice creates,
  provider calls or actual data mutations. Evidence in ignored
  .appdata/local/ai-voice-tests/topics-collapse-evidence.json and topics-collapsed-*.png.
- Local preview restarted through start-local.ps1 on 5107 with the already
  authorized child-only voice configuration. Native 200% zoom and hosting not
  observed for this native disclosure change; no claim they passed.
