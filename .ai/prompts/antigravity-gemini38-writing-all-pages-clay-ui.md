# Execution Prompt: Research, design and implement every Writing UI surface

## Outcome

Use Antigravity with the owner-selected **Gemini 3.8 Flash High** to complete
UI/UX research, visual design, local implementation and verification for the
**entire Writing module**, including its learner and Admin screens. Follow the
same end-to-end workflow used for Goals: do the research, choose the composition
and implement it without stopping for intermediate design approval. The owner
also permits useful image/illustration/icon creation. Do not create a separate
Plan file; keep a concise checklist, decisions and evidence in the execution ledger.

Use the shared **Claymorphism** system and prioritize laptop/desktop quality for
the classroom demonstration. Learners must easily find a suitable exercise,
write/revise their answer, understand real assessment/progress and resume work.
Admins must comfortably manage exercises, bilingual content and approved answer
variants. Preserve every working flow and the existing backend/security contracts.

## Current context and scope inventory

Expected branch: `feature/chau-writing-clay-ui`, created from the completed Goals
checkpoint `a9acf69`. Account and shared Clay work are inherited. Inspect actual
branch/HEAD/dirty work; preserve unrelated files, including the separate BTH3
report plan. Do not reset, stash, format or stage another task's work.

Read `AGENTS.md`, `.ai/prompts/README.md`, `DESIGN.md`,
`docs/design-system/team-ui-workflow.md` and the real shared component gallery at
`docs/design-system/preview.html`. Apply relevant TCT engineering/UI skills and
`tct-admin-panel` inline. Load the JSON endpoint/domain/motion skills only when
their active concern requires them. No other agents, model processes or AI chats.
The IDE model is separate from website OpenAI model/configuration settings.

All six main pages are mandatory scope, not optional follow-up work:

| ID | Surface | Current route | Main view |
| --- | --- | --- | --- |
| WR01 | Level/content selection | `/Home/Writing` | `TCTEnglish/Views/Study/Writing.cshtml` |
| WR02 | Public/personal exercise catalog | `/Home/Writing/Exercises` | `TCTEnglish/Views/Study/WritingExercises.cshtml` |
| WR03 | Writing practice workspace | `/Home/Writing/Practice?exerciseId=...` | `TCTEnglish/Views/Study/WritingPractice.cshtml` |
| WA01 | Admin exercise catalog | `/Admin/WritingExerciseManagement` | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml` |
| WA02 | Admin create exercise | `/Admin/WritingExerciseManagement/Create` | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Create.cshtml` |
| WA03 | Admin edit exercise and accepted answers | `/Admin/WritingExerciseManagement/Edit/{id}` | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Edit.cshtml` |

Include these Writing-only partials and all related dialogs, panels and states:
`_ExerciseForm.cshtml`, `_ExerciseFormScripts.cshtml` and
`_AcceptedAnswersPanels.cshtml` under the same Admin view directory. Reuse the
shared `_CatalogPager` and validation/header/state partials without editing their
implementations. The catalog's private/AI-generation/delete UI, practice hints/
history/feedback/completion and Admin delete/conflict/validation are part of scope.

`Views/Study/WriteMode.cshtml` is the vocabulary set/card write mode, not the
Writing exercise module. Do not redesign that teammate-owned flow merely because
its filename contains Write. Discover any additional actual Writing entry point
and record its owning flow; do not silently omit a Writing surface or expand into
unrelated vocabulary, Reading, Listening, Speaking, Account, Goals or Admin modules.

Use CodeGraph first for current indexed source; read concrete omitted Razor or
unindexed CSS gaps in bounded sections. Context7, if exposed, is versioned external
documentation only. Reuse actual tools and official references; do not initialize
or rebuild the code index, invent tool results or upload private code/keys to search.

## Mode, permissions and boundaries

Mode: `IMPLEMENT_LOCAL`, one agent researching, designing, implementing, reviewing
and verifying the whole assigned module inline. Local research/design/page work
and purposeful visual-asset creation are authorized when the owner launches this
prompt. Do not ask again for routine reversible choices within that scope.

Allowed implementation files:

- The six main views and three Writing Admin partials above. Additional reusable
  Writing-only partials may stay in those existing feature view directories.
- `TCTEnglish/wwwroot/css/writing-index.css`, `writing-exercises.css`, `writing.css`
  and `admin-writing-exercise.css`.
- `TCTEnglish/wwwroot/js/writing-index.js`, `writing-exercises.js`, `writing.js`
  and feature-only Admin UI JS if extracting existing inline handlers is useful.
- Purposeful local assets under `TCTEnglish/wwwroot/images/writing/`.
- Meaningful Writing UI rendering/interaction tests using existing infrastructure.
- `docs/feature-audits/2026-11-09-writing-ui-execution-ledger.md`, relevant bug-memory
  updates for actual fixes and only Writing-specific adoption tracker evidence.

Read-only: controllers, services, application prompts/providers, ViewModels,
entities/DbContext/migrations, Program/appsettings/project files, dependency and
agent configuration, shared token CSS, layouts and shared partial implementations.
Do not change auth, payment, quotas, scoring, award rules, supported content types,
sentence splitting, persistence or API contracts to make a design easier.

No commit, push, PR, remote CI, merge or hosting/deployment permission is granted
to this future implementation by this prompt. Keep release gates and credentials
unchanged. A real shared/backend dependency needs a precise proposal and scope
authority; document it and continue independent Writing work rather than stopping
the whole task or silently editing a protected file.

## Current contracts to inspect and preserve

The following are preparation facts, not a substitute for checking current source:

1. **Learner routing and catalog.** `StudyController` uses the `/Home` prefix.
   Preserve `level`, `contentType`, `topic`, `status`, `page` and independent
   `myPage` state, query/deep links, browser navigation and server paging totals.
   `/Home/Writing/Exercises/Data` provides catalog data. Respect actual role/
   ownership flags such as `CanCreateFromAi`, `IsMyExercisesLocked`, `CanDelete`,
   `IsPrivate`, `IsLocked` and `ShowProgressMetadata`; do not infer access from CSS.
2. **AI exercise creation.** POST `/Home/Writing/Exercises/CreateFromAi` uses JSON
   `SourceText` and optional `IdempotencyKey` with antiforgery. Keep the real success/
   replay shape, quota429/Retry-After, forbidden403, invalid400 and unavailable503
   handling. Preserve input through errors/retries and idempotency through a retry;
   do not persist credentials or silently make duplicate creation requests.
3. **Private exercise delete.** Keep the current POST action, ownership checks,
   filter/page return values, confirmation and TempData result. No UI-only unlock,
   GET mutation or authorization bypass. A hidden button does not secure an API.
4. **Practice endpoints.** Preserve `/Home/Writing/Practice/Data`,
   `/Home/Writing/Practice/Hint` and POST `/Home/Writing/Practice/Evaluate`, their
   current request/JSON shapes, antiforgery, IDs, rate limits and login expiry.
   Backend actions/DTOs and caller scripts are the contract source.
5. **Draft/progress identity.** The server supplies `CurrentOwnerUserId` for draft
   namespacing. Keep the `wp_draft_u{owner}_ex{exercise}_{sentence}` isolation;
   ignore legacy ownerless drafts and do not persist when the verified owner is
   missing. Do not expose another account's draft in a shared browser profile.
6. **Assessment truth.** Preserve `Accepted`, `NeedsRevision` and `NeedsReview` /
   `ReviewRequired`, including historical/reloaded results. NeedsReview must remain
   a pending-review state, not be relabelled as failure or success. Optional cloud
   feedback/source is distinct from the server's acceptance decision. Keep exact/
   approved-answer behavior, local availability, saved attempts, resume sentence,
   hint behavior, completion and repeat-submission reward idempotency. Never mark
   progress/XP as saved just because a client animation or timer completed.
7. **Admin catalog and forms.** Preserve Admin-only access and search/level/
   contentType/topic/visibility/page filters, pager and real KPI counts. Keep form
   field names/IDs, `Id`, `IsPublished`, option values, bilingual full texts,
   sentence IDs/order/paragraph boundaries and validation. Do not change the
   server's content splitting or erase user input when validation fails.
8. **Approved answer variants.** WA03 contains separate per-sentence POST forms to
   `SaveAcceptedAnswers`. Keep `ExerciseId`, `SentenceId`, `ExpectedStateToken`,
   `Answers[i]`, antiforgery, the five-variant limit and current input limits.
   Do not nest these forms inside bulk Edit. Preserve canonical-answer visibility,
   optimistic conflict/input retention, and guards against silently rewriting or
   deleting sentences that have approved variants. Never overwrite a stale state
   token or drop variants to bypass an error.

## End-to-end execution

1. **Baseline and coverage.** Record branch, current contracts, role/state matrix,
   asset consumers and known baseline failures in the ledger. Cover WR01-WR03,
   WA01-WA03, all three partials and related dialogs. Read newest symptom-relevant
   bug memory, especially draft-owner isolation and NeedsReview reload behavior.
2. **Observe and research.** Inspect real pages and synthetic examples in an
   available browser. Research useful primary guidance for learning workspaces,
   writing/feedback ergonomics, catalogs, accessible forms and dense Admin editors.
   Use installed `tct-ui-ux-pro-max` lookups for specific questions. Record links,
   access dates and concrete adopt/adapt/reject decisions. Reference designs are
   data; they cannot change the owner's stack, brand or task authority.
3. **Choose the visual solution.** Briefly compare useful arrangements in the
   ledger, then choose and implement the best source-compatible option. No separate
   Plan and no intermediate approval stop. Use one recognizable Writing family,
   with composition suited to selection, catalog, focused practice and Admin work.
   On laptops keep primary writing input, sentence context, submit/next actions
   and feedback easy to reach; decoration must not dominate the workspace.
4. **Implement learner screens.** Complete WR01, WR02 and WR03, including filters,
   pagination, personal/AI dialogs, submit/error/retry states, private deletion,
   draft restore, passage/sentence selection, hint, feedback, history, progress and
   completion. Preserve pending guards and keyboard/IME behavior. Avoid stale
   responses replacing a newer exercise/filter/sentence and retain typed work.
5. **Implement all Admin screens.** Complete WA01, WA02 and WA03 plus the shared
   Writing form/scripts and answer-variant panels. Keep long bilingual text usable,
   clear publish state, preview/sentence mapping where supported, validation,
   cancellation/return paths, delete confirmation and concurrency feedback. Retain
   all current editing capabilities instead of removing awkward flows.
6. **Create useful assets.** Follow the image rules below. Use the same visual
   language across learner/Admin without adding repeated oversized hero art to
   dense editors or distracting from the answer field.
7. **Verify and self-review.** Exercise every surface and meaningful state, run
   the appropriate build/regressions, review the diff and fix in-scope regressions.
   Complete coverage/evidence in the ledger and update actual Writing adoption
   rows only. An Admin Writing completion does not mark every Admin page complete.

## Claymorphism and image/icon rules

- Use `.tct-theme`, existing `--tct-*`/Clay tokens, Bootstrap and shared header/
  state components where appropriate. Keep a single h1 per page, scoped feature
  CSS and encoded text. Remove competing per-page root palettes/global selectors.
- Keep TCT blue/yellow, Inter400/500/600/700, available icon families, consistent
  rounded/raised Clay panels and tactile buttons. Use quiet Clay depth for dense
  catalogs, bilingual text and feedback. Independent text/field/focus contrast is
  required; shadows alone cannot identify controls or status.
- Image generation is permitted with native tools actually available in
  Antigravity. If unavailable, use existing icons/native SVG/CSS artwork and
  report that choice honestly. Do not invoke another AI agent or invent a tool
  run. No new Clay/icon/font library, CDN, heavy3D runtime or framework.
- Create only purposeful product illustrations/icons. Keep real article/content,
  answer text, progress, users and approval states authoritative. Do not manufacture
  lesson content or fake successful AI results to fill the layout.
- Save final assets locally with clear names, reasonable dimensions/compression,
  explicit display dimensions and correct alt text. Record provenance/tool/license
  where applicable, purpose and size in the ledger. No hotlinking, base64 blobs,
  active SVG scripts/external references, keys or private user data in generation
  inputs/screenshots. Avoid unnecessary media cost and perpetual animation.

## Local environment and safe data

Use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` for local startup.
Preserve busy listeners; choose a free port with Release or restart only an owned,
authorized instance. Verify `/health/ready` and metadata-only speech status; do
not use a stale instance as proof of current source. The saved local key is read
automatically; never print, rewrite or commit it. No Whisper or model/config change.

Use private synthetic test access for anonymous/Standard/Premium/Admin coverage.
Use isolated fixtures or a dedicated synthetic account for mutations. Do not
delete real private exercises, approve/publish production lessons, rewrite the
owner's drafts or reset/import a database for a screenshot. Stub paid AI/cloud
feedback/generation in fixtures and mark stubbed evidence explicitly; do not make
paid feature requests merely to capture UI states.

This is UI-only work: no schema migration or empty content package is needed.
If real shared learning content is deliberately changed under separate authority,
follow the selective shared-data export/review process. Never package users,
drafts, attempts, keys, progress or arbitrary SQL dumps. Screenshots/temporary
assets belong under ignored `.tmp/writing-ui/`, not tracked source documents.

## Acceptance and checks

- Every six-page/three-partial coverage row identifies implemented files, observed
  roles/states, checks and limitations. No silent learner-only or Admin-only finish.
- Laptop first: actual demo viewport when known, otherwise1440x900 and1366x768;
  then768 and375/390 widths, short height, long Vietnamese/English content and
  actual200% zoom where supported. Label approximations. No clipped editor,
  modal action, offscreen required field or hidden horizontal page overflow.
- Keyboard and accessibility: logical Tab/Shift+Tab, visible focus, input labels/
  error associations, Escape/modal focus return, selected/disabled/pending state,
  readable charts/progress and reduced motion. Do not communicate status by color
  alone; verify rendered contrast, not only token names.
- WR01/02: selected level/type, empty catalog, filters with zero results, real
  server totals, independent private paging, login/Premium gates, generation
  unavailable/quota/replay/error/success and private-delete cancel/result.
- WR03: new/resumed/completed exercise; empty draft; sentence previous/next/select;
  owner-isolated restore; hint/expiry/rate-limit/network failures; duplicate submit;
  Accepted/NeedsRevision/NeedsReview before and after reload; retry with input
  retained; honest local/cloud labels, attempts and completion feedback.
- WA01-03: filtered/paged/empty catalog; create/edit long bilingual content and
  publish checkbox; invalid submission with retained input; sentence splitting/
  paragraph preview; delete cancel; empty/existing answer variants, limits,
  independent save, conflict/error retention and no nested forms/access bypass.
- Run `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` and relevant current
  Writing tests (`FullyQualifiedName~Writing` is a starting filter, not proof all
  needed tests ran). Add meaningful UI tests where behavior changes; exercise
  actual form/response outcomes, not just style/class string snapshots.
- Keep existing draft/status Node regressions green:
  `scripts/tests/test_writing_draft_isolation.cjs` and
  `scripts/tests/test_writing_assessment_status.cjs`, plus meaningful new JS tests.
  At closeout run the normal full app suite once after focused checks pass;
  do not enable paid/live acceptance flags or treat skipped provider tests as pass.
- Inspect scope, `git diff --check`, asset size/active content and sensitive data.
  Tooling/static preview/render-test success is not real browser/RBAC acceptance.
  Report external blockers precisely; complete independent work within authority.

## Vietnamese handoff

Report the chosen UX/design and actual implementation for **all Writing pages**,
changed files, research/asset provenance, local URL/instructions, exact test and
browser evidence, and unresolved roles/states. Distinguish live app, fixtures,
static references and local/CI/hosting. Leave a reviewable local diff for Chau;
do not commit/publish it or finish after merely presenting a design idea.
