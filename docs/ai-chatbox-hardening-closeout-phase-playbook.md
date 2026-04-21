# AI Chatbox Hardening Closeout Phase Playbook

> Follow-up execution playbook for closing the AI chatbox hardening work after
> `docs/ai-chatbox-hardening-phase-playbook.md`.
>
> Use this file only after the original Phase 0 through Phase 6 work has been
> implemented. The goal here is to remove the remaining gaps that prevent the AI
> chatbox package from being called 100% closed.

## How To Use This File

Tell the coding agent:

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase [N] only.
Do not start any later phase.
```

Each phase is intentionally small. If a phase uncovers a larger bug, stop after
documenting it and split the extra work into a new phase instead of expanding
the current one silently.

## Shared Agent Rules For Every Closeout Phase

Use this shared instruction block for every phase:

```text
Read `AGENTS.md` first and follow it strictly.
Then read, in order:
1. `docs/project-structure.md`
2. `docs/architecture-prioritized-backlog.md`
3. `.ai/context/known-issues.md`
4. `.ai/context/coding-conventions.md`
5. `docs/ai-chatbox-hardening-phase-playbook.md`
6. `docs/ai-chatbox-hardening-closeout-phase-playbook.md`

Treat this closeout file as the current AI chatbox closeout checklist.
Treat the older hardening playbook as historical implementation context.

Inspect existing local changes before editing. Do not overwrite unrelated work.
Do not modify `appsettings.json`, `.csproj`, or migrations unless a phase
explicitly permits it. Do not modify `Program.cs` unless the phase explicitly
requires DI or route verification changes and the final summary explains why.

This is bug-fix/hardening work. Before editing code, read:
- `.ai/context/bug-fix-log.md`
- `.ai/context/known-issues.md`

Keep work inside the correct feature boundary:
- AI chat code: `TCTEnglish/Services/AI`, `TCTEnglish/Controllers/AiController.cs`,
  `TCTEnglish/Views/Ai`, `TCTEnglish/wwwroot/js/ai-chat*.js`,
  `TCTEnglish/wwwroot/css/ai-chat*.css`
- Website guide data: `TCTEnglish/wwwroot/data/ai/website-guides.json`
- Dashboard launcher blockers: `HomeController` and dashboard/shared layout files
  only if a phase explicitly targets launcher smoke tests.

Preserve anti-IDOR, anti-CSRF, async EF, `AsNoTracking()` on display reads,
thin controller boundaries, and ViewModel-only views.

Preserve UTF-8. When reading Vietnamese files from Windows PowerShell, prefer
explicit UTF-8 reads such as:
`[System.IO.File]::ReadAllText((Resolve-Path 'path'), [System.Text.Encoding]::UTF8)`.
Run `python scripts/encoding_guard.py` if it exists. If it does not exist,
record that explicitly and run an equivalent UTF-8/mojibake check for touched
Vietnamese files.

Run targeted verification for the phase. After a real bug fix, append a new
entry to `.ai/context/bug-fix-log.md` using the required format. Update
`.ai/context/known-issues.md` only when the fixed issue was listed there or when
the phase confirms a new unresolved blocker that should be tracked.

Finish with a concise Vietnamese summary covering:
- result;
- files changed;
- checks run;
- remaining risks or blockers;
- what the next phase should verify first.
```

## Current Audit Snapshot

Snapshot date: 2026-04-21.

Additional verification reported: user confirmed full test run and browser
smoke coverage for `/AI/Chat` and the embedded launcher with no failures.

The original hardening phases appear to have covered the main defects:

- `InternalKnowledgeProvider` now aggregates snippets from all matching
  retrievers.
- `StudyRecommendationRetriever` exists and is reachable for owned sets with no
  learning progress.
- mastered status matching covers `Mastered`, `mastered`, and `Learned`
  case-insensitively.
- deterministic fallback covers Reading, Writing, Listening, support,
  notifications, overview/navigation, Goals, Daily Challenge, class chat, and
  folder organization prompts.
- quick-action click handling is scoped to the chat shell and guarded against
  duplicate clicks.
- focused AI service tests are passing.

Verification already observed during this audit:

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~MlNetIntentClassifierAssetResolverTests|FullyQualifiedName~MlNetAiQueryClassifierTests|FullyQualifiedName~MlNetIntentDatasetLoaderTests"
```

Result: passed 166/166 after setting `DOTNET_CLI_HOME` to a workspace-local
folder.

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests&FullyQualifiedName!~HomeIndex&FullyQualifiedName!~AiLauncher_"
```

Result: passed 30/30 when run separately.

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests&FullyQualifiedName~HomeIndex"
```

Result: passed (HomeIndex launcher tests no longer return 500 in SQLite).

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "TypeName~TCTEnglish.Tests.AiProductionClassifierIntegrationTests"
```

Result: passed (production classifier guardrails and model integrity checks).

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~MlNetRuntimeIntegrationTests|FullyQualifiedName~MlNetTrainerServiceTests"
```

Result: passed 4/4, but these tests use synthetic model artifacts and do not
prove the shipped `intent-classifier-model.zip` recognizes the current quick
actions.

`git diff --check` passed with only line-ending warnings. `scripts/encoding_guard.py`
is present and used for UTF-8 verification.

## Remaining Findings To Close

All closeout findings have been addressed. The AI chatbox hardening package is
ready to be closed.

## Global Closeout Done Criteria

The AI chatbox hardening package is 100% closeable only when:

- all original hardening done criteria still pass;
- HomeIndex/launcher AI tests pass without excluding `HomeIndex` or
  `AiLauncher_` cases;
- website guide answers never present placeholder route templates as direct
  user-facing URLs;
- all concrete routes in `website-guides.json` resolve against the current app;
- production DI/classifier behavior recognizes the current quick actions and
  still refuses unrelated prompts;
- study recommendation remaining counts include unstarted cards in owned sets;
- goal remaining count uses the same daily activity signal across retrievers;
- quick-action behavior is smoke-tested in a real browser for full and embedded
  chat;
- UTF-8/encoding checks are repeatable;
- focused AI tests, launcher tests, and route tests pass;
- full `dotnet test` is either passing or remaining failures are explicitly
  listed as unrelated blockers in `known-issues.md` or the final summary;
- `.ai/context/bug-fix-log.md` contains closeout entries for actual fixes.

## Phase 0 - Re-Audit And Lock The Closeout Baseline

### Goal

Confirm the exact current state before changing code. Do not edit production
code in this phase.

### Area Hint

AI chat / dashboard launcher / website guide / tests

### Task

```text
Task: Re-audit the AI chatbox hardening work after the original six phases and
lock the remaining closeout checklist.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific scope:
- Inspect `git status --short` and preserve unrelated local changes.
- Inspect the current diff for AI service, guide, UI, and tests.
- Inspect `HomeController.GetRandomChallengeAsync()`,
  `HomeController.GetSystemWrongAnswersAsync()`, and AI launcher tests.
- Inspect all routes in `website-guides.json`, especially routes containing
  `{id}`, `{setId}`, or `{videoId}`.
- Inspect `LearningProgressRetriever`, `StudyRecommendationRetriever`,
  `InternalKnowledgeProvider`, and `TemplateAnswerComposer`.
- Inspect `MlNetAiQueryClassifier`, ML.NET seed/model files, and current tests
  that force deterministic classification.
- Do not edit production code in this phase.

Deliverables:
- Confirm which findings in this closeout file still reproduce.
- Record the exact failing test names and commands.
- Record which phases are still needed and whether any phase should be split.
```

### Suggested Verification

Use a workspace-local .NET home to avoid sandbox first-run writes:

```powershell
$env:DOTNET_CLI_HOME='D:\repo\.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_NOLOGO='1'
```

Then run:

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~MlNetIntentClassifierAssetResolverTests|FullyQualifiedName~MlNetAiQueryClassifierTests|FullyQualifiedName~MlNetIntentDatasetLoaderTests"
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests&FullyQualifiedName~HomeIndex"
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~MlNetRuntimeIntegrationTests|FullyQualifiedName~MlNetTrainerServiceTests"
```

Do not run `dotnet test` commands for the same project in parallel because the
MVC test manifest can be locked by another MSBuild process.

### Phase 0 Done Criteria

- No production code changed.
- Remaining blockers are confirmed or explicitly dismissed with evidence.
- The next implementation phase is clear.

## Phase 1 - Unblock HomeIndex And Launcher Tests

### Goal

Fix the `/Home/Index` 500 so AI launcher tests can run as part of the closeout
suite.

### Area Hint

Dashboard / HomeController / AI launcher integration tests

### Task

```text
Task: Fix the dashboard/HomeIndex blocker that prevents AI launcher closeout
tests from passing in the SQLite test host.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Reproduce the failing HomeIndex AI launcher tests first.
- Inspect `HomeController.GetRandomChallengeAsync()` and
  `HomeController.GetSystemWrongAnswersAsync()`.
- Remove or guard any SQLite-incompatible EF query expression such as
  `OrderBy(c => Guid.NewGuid())`.
- Preserve the existing SQL Server behavior where possible.
- For non-SQL Server providers, use the same provider-safe random id/offset
  pattern already used by `GetTodayFoldersAsync()`.
- Do not change unrelated dashboard behavior.
- Keep daily challenge answer options randomized.
- Keep anti-forgery and signed challenge-token behavior intact.
```

### Required Tests

- `AiPhase4HardeningIntegrationTests.HomeIndex_RendersAiLauncherWithDialogAccessibilitySemantics`
- `AiPhase4HardeningIntegrationTests.HomeIndex_StandardUser_RendersLauncherPlanSummary`
- `AiPhase4HardeningIntegrationTests.HomeIndex_PremiumUser_RendersLauncherUnlimitedPlanSummary`
- Existing daily challenge smoke/integration tests, if present.

### Suggested Verification

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests&FullyQualifiedName~HomeIndex"
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~Sprint1SmokeTests|FullyQualifiedName~CriticalFlowSqliteIntegrationTests"
```

### Phase 1 Done Criteria

- `/Home/Index` returns 200 in the relevant AI launcher tests.
- HomeIndex/launcher tests pass without exclusions.
- No unrelated `HomeController` refactor was introduced.
- A bug-fix log entry is appended if code changed.

## Phase 2 - Close Website Guide Route Contracts

### Goal

Make every website guide route either a real concrete route or clearly not a
direct link.

### Area Hint

Website guide JSON / WebsiteGuideRetriever / TemplateAnswerComposer / route tests

### Task

```text
Task: Close the website-guide route contract so AI answers never show route
templates as direct navigable URLs.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Parse `TCTEnglish/wwwroot/data/ai/website-guides.json`.
- List all entries whose `route` contains `{`.
- Decide and implement one consistent contract:
  - either `route` must always be a concrete route, and template routes are
    replaced with safe parent/list routes;
  - or template routes are represented separately and
    `TemplateAnswerComposer` does not print them as direct URLs.
- Do not add controller actions only to satisfy a guide route.
- Verify all concrete routes against the current MVC endpoints.
- Keep legacy `/Home/*` routes when they are the real current routes because
  the feature controller uses `[Route("Home")]`.
- Keep JSON valid UTF-8.
```

### Required Tests

- A test that fails if any user-facing `route` in `website-guides.json`
  contains `{` or `}` unless the chosen contract explicitly supports
  non-clickable templates.
- A route-resolution test for every concrete guide route.
- Existing quick-action route tests for Reading, Writing, Listening, Contact,
  Notifications, Goals, Daily Challenge, class chat, and folder organization.
- A composer test proving placeholder/template routes are not rendered as
  `Bạn có thể truy cập ngay tại: /.../{id}`.

### Suggested Verification

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests"
```

Also parse JSON directly:

```powershell
[System.IO.File]::ReadAllText((Resolve-Path 'TCTEnglish\wwwroot\data\ai\website-guides.json'), [System.Text.Encoding]::UTF8) | ConvertFrom-Json | Out-Null
```

### Phase 2 Done Criteria

- No guide answer presents a placeholder route as a direct URL.
- All concrete guide routes resolve.
- Existing quick-action route behavior remains green.
- JSON remains valid UTF-8.

## Phase 3 - Fix Study Recommendation Accuracy And Arbitration

### Goal

Make study recommendation counts accurate for users with partial progress and
remove accidental reliance on retriever order.

### Area Hint

AI retrievers / InternalKnowledgeProvider / TemplateAnswerComposer

### Task

```text
Task: Fix study recommendation remaining-count accuracy for partially studied
sets and define how multiple recommendation snippets are chosen.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Add a failing test where an owned set has more cards than progress rows.
  Example: 10 cards, 1 `Learning` progress row, 1 `Mastered` progress row, and
  8 cards with no `LearningProgress` row. Remaining count should include the
  unstarted cards.
- Add a failing test where daily `UserDailyActivities.CardsReviewed` reduces
  goal remaining for recommendation answers.
- Decide the lowest-risk implementation:
  - make `LearningProgressRetriever` compute recommendation candidates from
    owned set/card totals, not only progress rows;
  - or move all StudyRecommendation candidate building into one retriever;
  - or make `TemplateAnswerComposer` choose the highest-priority complete
    recommendation snippet instead of the first one.
- Preserve progress-summary behavior for `MyProgress`.
- Preserve ownership checks:
  - owned sets must use `OwnerId == userId`;
  - progress must use `UserId == userId`;
  - joined card/set data must not leak another user's sets.
- Keep async EF and `AsNoTracking()` on read-only queries.
```

### Required Tests

- progress-based recommendation with all cards represented by progress rows;
- partial-progress recommendation where unstarted cards are counted;
- owned set with no progress still gets a recommendation;
- no owned sets still returns empty state;
- another user's sets/cards/progress do not leak;
- mastered variants `Mastered`, `mastered`, `Learned` remain mastered;
- daily goal remaining uses `UserDailyActivities.CardsReviewed`.

### Suggested Verification

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~TemplateAnswerComposerTests"
```

### Phase 3 Done Criteria

- Study recommendation remaining counts match owned card totals minus mastered
  cards.
- Recommendation choice is intentional and covered by tests, not an incidental
  side effect of DI order.
- No IDOR regression.

## Phase 4 - Lock Production Classifier Behavior

### Goal

Prove the actual production classifier path handles the current quick actions
and safety boundaries.

### Area Hint

ML.NET classifier / deterministic fallback / production DI send path

### Task

```text
Task: Add production-classifier coverage so the shipped ML.NET model, fallback
classifier, and InternalKnowledgeProvider agree on current quick-action behavior.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Inspect whether the app loads `intent-classifier-model.zip` in the current
  test host.
- Add tests that use the real DI classifier path, not a forced deterministic
  classifier, for:
  - Reading quick action;
  - Writing quick action;
  - Listening quick action;
  - Contact/support;
  - Notifications;
  - Goals/Daily Challenge/class chat/folder guide prompts;
  - StudyRecommendation quick action;
  - unrelated grammar/homework/random-fact prompts.
- If the current model predicts incorrectly, choose one of these fixes:
  - retrain and commit the model artifact with the seed data;
  - or change `MlNetAiQueryClassifier` to let high-confidence deterministic
    safety/guide rules override weak or unsafe ML predictions;
  - or configure tests/app behavior so deterministic fallback is intentionally
    the primary classifier for these hard safety/guide cases.
- Do not rely only on seed CSV changes. Seed rows do not affect production until
  the model artifact is retrained or bypassed.
```

### Required Tests

- A default-DI send-path test for every current quick-action prompt.
- A default-DI send-path test for at least three unrelated prompts that must be
  refused.
- A test or documented assertion that the model artifact and seed data are not
  drifting silently.

### Suggested Verification

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~MlNetRuntimeIntegrationTests|FullyQualifiedName~MlNetAiQueryClassifierTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~DeterministicIntentClassifierTests"
```

Add the new production-DI tests to the command once they exist.

### Phase 4 Done Criteria

- Production classifier behavior matches deterministic hardening expectations.
- Current quick actions work even when the ML.NET model artifact is present.
- Out-of-scope safety remains intact.

## Phase 5 - Browser Smoke Test Full And Embedded Chat

### Goal

Prove the chat works in a real browser, not only through server and structural
tests.

### Area Hint

AI chat Razor / JavaScript / launcher iframe / Playwright

### Task

```text
Task: Run and, if needed, fix real-browser smoke tests for full AI chat and the
embedded launcher chat.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Start the app locally using the repo's normal dev command.
- Use browser tooling, preferably Playwright, to test:
  - `/AI/Chat` empty state;
  - Reading quick action;
  - Writing quick action;
  - support quick action;
  - study recommendation quick action;
  - normal typed message;
  - embedded launcher opened from a non-AI page;
  - one quick action inside the embedded iframe.
- Verify exactly one user message appears per click.
- Verify exactly one assistant/system answer appears per accepted request.
- Verify the composer returns to usable state after success and after a forced
  error/rate-limit response.
- Verify mobile width does not overflow quick-action text.
- Prefer small fixes only:
  - scope anti-forgery token lookup to the active form;
  - restore or re-enable quick actions after failed send if the conversation was
    not created;
  - remove inline style from `_ChatShell.cshtml` if touched;
  - polish visible quick-action labels such as `ntn` if text still fits.
```

### Suggested Verification

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests"
```

Manual/browser checklist:

```text
/AI/Chat desktop
/AI/Chat mobile width
Home/dashboard launcher desktop
Home/dashboard launcher mobile width
Embedded iframe quick action
Typed message after quick action
Rate-limit or provider-error state
```

### Phase 5 Done Criteria

- Full chat and embedded launcher chat both work in a real browser.
- Quick actions do not double-submit.
- UI remains responsive on narrow widths.
- Any browser-only issue is fixed or explicitly tracked.

## Phase 6 - Add Repeatable Encoding Guard

### Goal

Make UTF-8/Vietnamese validation repeatable for future AI guide and UI edits.

### Area Hint

scripts / Vietnamese text files / docs / tests

### Task

```text
Task: Add or document a repeatable UTF-8 and mojibake guard for Vietnamese AI
chat files.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Check whether `scripts/encoding_guard.py` exists.
- If it does not exist, add a small repo-local guard script or add an equivalent
  documented command in this closeout phase's final summary.
- The guard should detect:
  - invalid UTF-8 bytes;
  - common mojibake markers such as `Ã`, `Ä`, `Â`, `â€™`, `â€œ`, `â€`, when they
    appear in files expected to contain Vietnamese prose;
  - accidental replacement characters.
- Run the guard against:
  - `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs`;
  - `TCTEnglish/Views/Ai/_ChatShell.cshtml`;
  - `TCTEnglish/wwwroot/data/ai/website-guides.json`;
  - `TCTEnglish/wwwroot/js/ai-chat.js`;
  - `.ai/context/bug-fix-log.md`;
  - this closeout playbook if touched.
- Account for legitimate Unicode punctuation if the repo already uses it.
```

### Suggested Verification

```text
python scripts/encoding_guard.py
```

If Python is unavailable or the script is intentionally not added, use a
PowerShell/.NET UTF-8 read and explicit codepoint/mojibake scan and document the
exact command.

### Phase 6 Done Criteria

- UTF-8 checking is repeatable.
- Touched Vietnamese AI files pass the guard.
- Future agents no longer have to infer encoding health from terminal display.

## Phase 7 - Final Closeout Verification And Documentation

### Goal

Close the hardening package cleanly, or explicitly document why it cannot be
closed yet.

### Area Hint

AI final verification / docs / bug-fix log / known issues

### Task

```text
Task: Perform final AI chatbox hardening closeout verification and update
documentation.

Follow the Shared Agent Rules in
`docs/ai-chatbox-hardening-closeout-phase-playbook.md`.

Specific requirements:
- Inspect final diff and ensure no unrelated changes were introduced.
- Run JSON parsing for `website-guides.json`.
- Run the encoding guard.
- Run focused AI tests.
- Run HomeIndex/launcher AI tests without exclusions.
- Run production-classifier tests added in Phase 4.
- Run full `dotnet test` if reasonable.
- If full tests still fail, classify each failure as:
  - AI closeout blocker;
  - non-AI blocker that must be fixed now;
  - unrelated existing failure to track outside this package.
- Append a final bug-fix log entry for actual fixes completed during closeout.
- Update `.ai/context/known-issues.md` only for unresolved confirmed blockers.
- Do not keep expanding answer-quality work in this phase.
```

### Suggested Verification

```text
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~MlNetRuntimeIntegrationTests|FullyQualifiedName~MlNetAiQueryClassifierTests|FullyQualifiedName~MlNetIntentDatasetLoaderTests|FullyQualifiedName~MlNetTrainerServiceTests"
dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore
git diff --check
```

### Phase 7 Done Criteria

- All Global Closeout Done Criteria are met, or remaining blockers are listed
  with owner area and next command to reproduce.
- Old hardening playbook findings are either fixed or superseded by documented
  closeout notes.
- The final response says whether the AI chatbox hardening package can be
  considered closed.

## Recommended Phase Order

Run phases in this order:

1. Phase 0 - Re-Audit And Lock The Closeout Baseline
2. Phase 1 - Unblock HomeIndex And Launcher Tests
3. Phase 2 - Close Website Guide Route Contracts
4. Phase 3 - Fix Study Recommendation Accuracy And Arbitration
5. Phase 4 - Lock Production Classifier Behavior
6. Phase 5 - Browser Smoke Test Full And Embedded Chat
7. Phase 6 - Add Repeatable Encoding Guard
8. Phase 7 - Final Closeout Verification And Documentation

Do not combine Phases 1, 2, 3, and 4 unless explicitly requested. They touch
different risk areas and should be easy to review independently.

## Minimal Command Templates

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 0 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 1 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 2 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 3 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 4 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 5 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 6 only.
```

```text
Read `docs/ai-chatbox-hardening-closeout-phase-playbook.md` and execute Phase 7 only.
```
