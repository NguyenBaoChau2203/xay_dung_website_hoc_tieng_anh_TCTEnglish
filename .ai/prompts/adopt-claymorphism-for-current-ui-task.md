# Execution Prompt: Align the current UI task with TCT Claymorphism

## Outcome and context

Continue the owner's current page assignment using the updated, owner-selected
Claymorphism direction for every redesigned TCT English screen. Prioritize the
laptop/desktop classroom demonstration, while retaining responsive/accessibility
requirements. This is a visual update to the existing task, not a new assignment
to redesign other teammates' screens or replace current functional repair work.

Re-read current `AGENTS.md`, `DESIGN.md` (Claymorphism and demo priority),
`docs/design-system/team-ui-workflow.md` and the live
`docs/design-system/preview.html`. Follow `.ai/prompts/README.md`. Use the existing
approved design and exact form/JS/security contracts for the assigned screens;
current owner visual decisions supersede older competing visual specifications.

## Mode and authority

Retain the current task's authorized mode. A design-only task produces updated
design/preview/checklist only; a local implementation task may align its owned
views/CSS with the current shared foundation and verify them. This prompt does
not grant broader shared-CSS, layout, backend, data, Git, CI or deployment rights.
One agent performs its work inline; never delegate to another agent/model/chat.

Inspect actual branch/dirty work. Preserve other ongoing changes. Use CodeGraph
first for indexed source and fill concrete CSS/Razor gaps with bounded reads.
Do not restart an existing app unless its ownership and restart authority are
established. Never treat a stale running instance as evidence for current source.

## Execute

1. Record the current screen scope, active step, outstanding functional findings
   and design revision. Keep required repairs and previously approved flows.
2. Map the current view's surfaces/controls to the shared Clay foundation. Reuse
   `.tct-theme`, `.tct-card`, Bootstrap components or `.tct-clay-surface` with its
   optional `tct-clay-surface--quiet` modifier. Use scoped `--tct-*`/Clay tokens;
   remove local flat depth/radius overrides that defeat the current shared style.
3. Use raised rounded panels and button depth. Keep form boundaries/focus explicit
   and prose, tables and transcripts readable within quiet Clay containers. Keep
   TCT colors, Inter, current icons, layout geometry and real content/state.
4. Update the assigned design/preview or implementation as permitted by its mode.
   No per-feature Clay palette/library/CDN, global reset or arbitrary shadows.
   If a shared component is actually missing, prepare the documented consumer/
   contrast proposal and continue independent work inside the existing scope.
5. Review laptop/desktop first, including limited height, then tablet/mobile,
   keyboard, zoom, meaningful states, contrast and reduced motion. For an
   implementation, run relevant build/behavior tests and verify actual pages.
   A static gallery or a passing form-rendering test is not full UI acceptance.
6. Update the task's evidence/ledger and actual adoption rows without rewriting
   unrelated reports. Leave unobserved states and other owners' pages pending.

## Acceptance and handoff

Every assigned redesigned screen visibly uses the shared Clay language and
preserves its existing behavior/security. No outstanding functional repair is
silently replaced by visual polish. Evidence identifies app/source revision,
viewports/roles/states, actual checks, remaining limits and owner decisions.

Report in Vietnamese. Distinguish updated design from implemented/verified pages
and local work from CI/staging/production. Do not commit or publish without the
owner's explicit authorization for that stage.
