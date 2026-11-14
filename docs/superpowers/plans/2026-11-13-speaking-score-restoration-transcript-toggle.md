# Speaking result restoration and transcript visibility

Mode: IMPLEMENT_LOCAL. Owner authorized both fixes. One agent works inline;
preserve the current dirty checkout, existing import fixes and CTA removal.
No paid ASR, protected configuration, schema or release authority is needed.

## Evidence and intended behavior

`selectSentence` restores numeric scores but unconditionally clears comparison.
Commit never keeps the comparison/availability on the sentence. Practice GET
only projects numeric progress. Unmeasured fluency/completeness become zero.
The old text toggle is wired twice and only hides the duplicate center text.

Keep a complete committed result per sentence. Restore word comparison after
navigation and reload using the existing consumed assessment that corresponds
to the saved progress (same user/sentence/score/commit timestamp). Never use
another user's receipt, an uncommitted take or a worse later attempt. Display
unmeasured metrics as unavailable; preserve zero scored results. No new AI call.

Remove the duplicate English/Vietnamese reference from the scoring card. Move
the existing hide control to the transcript header, available at every width.
Hide/collapse the entire transcript list including translations; keep the header
and Show button reachable. Preserve state across sentence/tab changes. Word
comparison remains part of assessment feedback, not a second reference panel.

## Ordered checklist and acceptance

- [x] Add meaningful failing Node regressions for committed-result navigation,
  zero scores and transcript visibility; add .NET projection/access regressions.
- [x] Restore committed comparison/availability in JS and the practice ViewModel
  through a batched owned receipt query; preserve HTTP/antiforgery contracts.
- [x] Move the toggle and remove duplicate text/handlers; scope CSS to Speaking.
- [x] Run relevant Node/.NET checks and build; review the incremental diff inline.
- [x] Verify local MariaDB-backed practice, sentence1→2→1, reload and responsive
  hide/show/keyboard behavior. Use saved results or intercepted fixtures, no paid
  recording request. Record actual provider/browser limits.
- [x] Update bug memory and report outcome/files/checks/limits in Vietnamese.

## Evidence ledger

- Reproduced3 Node and2 .NET failures before implementing. Expanded coverage now
  passes38 .NET cases (SpeakingTranscriptDisplay/Assessment/TextAlignment and
  PremiumSpeakingLifecycleIntegration) and36 Node cases (Speaking behavior,
  word comparison, transcript selection, dictation and Clay interactions).
- Tests exercise public/private ownership,0 and90 saved results, several videos
  and sentences, foreign/unconsumed/later receipts and arbitrary1→3→2→3→1
  navigation. Submission TTL is separate from restoring consumed history.
- No application condition contains the owner's video/user identity. The code
  path is shared across all practice pages;82/173/172 are evidence identities,
  not configuration or production exceptions.
- Actual MariaDB-backed Sports172 contains43 cues. The saved90 result restores
  21 word operations,90 accuracy and unavailable fluency/completeness (—).
  Score/chip data is identical before/after1→2→1 and full reload. No assessment
  or progress POST occurs. Personal173 starts without stale feedback.
- Public82 (230 cues) also has no duplicate center text; hide/show and retaining
  hidden state on sentence navigation work. Native public query/JSON render
  succeeds without schema changes.
-1440/768/390: no horizontal overflow, list completely hidden, header/toggle
  reachable; collapsed heights are about101/101/89px. Enter/Space, tab changes
  and sentence changes pass. CSS-simulated200% zoom does not overflow; native
  browser zoom remains unverified.
- Launcher build succeeds on5100, schema current, shared sync preserved,
  readiness HTTP200, speech configured without a paid call. Diff check passes.
  Existing concurrent owner visual/test-fixture edits were preserved; only an
  extra trailing blank line was removed from the already-touched import test.
- Screenshots (ignored):output/playwright/speaking-score-restored-1440.png and
  output/playwright/speaking-transcript-hidden-1440.png.
- No persisted model/mapping changed: ViewModels, runtime read projection and UI
  only. No migration or shared export is required. Old numeric-only history
  without its source receipt retains its stored numbers; comparison is not
  fabricated. No commit/push/CI/staging/production or paid speech operation.
