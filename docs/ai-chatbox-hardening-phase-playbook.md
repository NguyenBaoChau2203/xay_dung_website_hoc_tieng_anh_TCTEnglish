# AI Chatbox Hardening Phase Playbook

> Supporting execution playbook.
> Use this file when assigning an agent to finish and harden the current TCT AI
> chatbox work one phase at a time. This is not the source of architecture
> truth; `docs/project-structure.md` and
> `docs/architecture-prioritized-backlog.md` remain authoritative.

## How To Use This File

Tell the coding agent:

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase [N] only.
Do not start any later phase.
```

For Visual Studio / Copilot-style agents:

```text
@workspace #solution Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase [N] only.
Do not start any later phase.
```

For Antigravity / Codex-style agents:

```text
@workspace Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase [N] only.
Do not start any later phase.
```

Each phase below is self-contained. The agent must still follow the shared
rules in this file before doing any phase work.

## Shared Agent Rules For Every Phase

Use this shared instruction block for every phase:

```text
Read `AGENTS.md` first and follow it strictly.
Then read, in order:
1. `docs/project-structure.md`
2. `docs/architecture-prioritized-backlog.md`
3. `.ai/context/known-issues.md`
4. `.ai/context/coding-conventions.md`

Treat `docs/implementation_plan.md` as supporting context only.
Treat `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.

Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless the phase explicitly permits it.

Follow core repo rules:
- use ViewModels for views;
- keep controllers thin;
- use async I/O;
- enforce anti-IDOR and anti-CSRF;
- use `GetCurrentUserId()` from `BaseController`;
- return `NotFound()` for ownership violations.

For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

This is bug-fix/hardening work. Before editing code, read:
- `.ai/context/bug-fix-log.md`
- `.ai/context/known-issues.md`

Reuse an old fix only if the root cause matches.
After verification, append a bug-fix entry to `.ai/context/bug-fix-log.md` in the required format.
Update `.ai/context/known-issues.md` only if a fixed issue is listed there.

If the platform supports subagents and the task is non-trivial, spawn one explorer subagent for the relevant area first; otherwise continue locally.

Complete only the requested phase end-to-end, run relevant verification, and finish with a concise Vietnamese summary covering:
- result;
- files changed;
- checks run;
- skills/workflows used;
- remaining risks or blockers;
- what the next phase should verify first.
```

## Current Known Findings

These findings are the minimum defects that must be resolved before the work is
considered complete.

1. `StudyRecommendationRetriever` is not selected in real DI.
   - `InternalKnowledgeProvider` currently chooses the first retriever whose
     `CanHandle` returns true.
   - `LearningProgressRetriever` is registered before
     `StudyRecommendationRetriever` and already handles
     `UserIntent.StudyRecommendation`.
   - Result: users with sets but no learning progress can still get the old
     empty-state response.

2. Mastered-card counting is case-sensitive in
   `StudyRecommendationRetriever`.
   - Existing code treats `Mastered` and `Learned` as mastered,
     case-insensitively.
   - The new retriever only counts exactly `mastered`.
   - Result: remaining card counts can be inflated.

3. Quick-action intent fallback is incomplete.
   - New ML.NET seed rows do not help unless the model has been retrained and
     loaded.
   - Deterministic fallback does not reliably recognize Reading, Writing,
     Listening, support/contact, notifications, overview/about/navigation guide
     questions.
   - Result: quick-action prompts can classify as `OutOfScope`.

4. Website guide route correctness must be verified.
   - Current guide entries added for Reading/Writing/Listening used
     `/Study/ReadingList`, `/Study/WritingList`, `/Study/ListeningList`.
   - Verify actual routes against controller attributes and route mapping before
     finalizing.

## Important Code Map

Primary AI chat files:

- `TCTEnglish/Controllers/AiController.cs`
- `TCTEnglish/Services/AI/AiChatService.cs`
- `TCTEnglish/Services/AI/AiConversationService.cs`
- `TCTEnglish/Services/AI/Internal/InternalKnowledgeProvider.cs`
- `TCTEnglish/Services/AI/Internal/DeterministicIntentClassifier.cs`
- `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs`
- `TCTEnglish/Services/AI/Internal/Retrievers/*.cs`
- `TCTEnglish/Services/AI/Internal/Data/intent-samples.seed.csv`
- `TCTEnglish/wwwroot/data/ai/website-guides.json`
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/Views/Ai/ChatEmbed.cshtml`
- `TCTEnglish/Views/Ai/_ChatShell.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/wwwroot/css/ai-chat.css`

Route verification files:

- `TCTEnglish/Program.cs`
- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Controllers/HomeController.cs`
- `TCTEnglish/Controllers/NotificationController.cs`
- `TCTEnglish/Views/Shared/_Layout.cshtml`

Test files to inspect/reuse:

- `TCTEnglish.Tests/AiBaselineRegressionTests.cs`
- `TCTEnglish.Tests/TemplateAnswerComposerTests.cs`
- `TCTEnglish.Tests/WebsiteGuideRetrieverTests.cs`
- `TCTEnglish.Tests/DeterministicIntentClassifierTests.cs`
- `TCTEnglish.Tests/InternalKnowledgeProviderTests.cs`
- `TCTEnglish.Tests/MlNetRuntimeIntegrationTests.cs`
- `TCTEnglish.Tests/MlNetAiQueryClassifierTests.cs`

## Global Done Criteria

The overall hardening effort is complete only when:

- Study recommendation works for users with existing sets and no progress.
- Study recommendation still works for users with progress.
- No other user's sets, cards, progress, classes, or private data leak.
- Mastered-card counts match existing status semantics.
- Reading/Writing/Listening/support/notification quick-action prompts classify
  as `WebsiteGuide` under deterministic fallback.
- Out-of-scope prompts are still refused.
- Website guide routes returned by AI are real routes.
- Quick action buttons submit one message, not duplicates.
- Full AI chat page and embedded launcher chat both smoke-test successfully.
- Relevant AI tests pass.
- UTF-8/Vietnamese text remains clean.
- `.ai/context/bug-fix-log.md` is updated after verification.

## Review Gate For Every Phase

Before closing any phase, explicitly check:

- Did this phase touch only intended files?
- Did it preserve unrelated local changes?
- Did it preserve UTF-8 Vietnamese text?
- Did it follow post-refactor boundaries?
- Did it avoid adding domain logic to `HomeController`?
- Did it avoid new feature screens under `Views/Home/`?
- Did it add/update tests proportional to risk?
- Did it run relevant checks?
- What should the next phase verify first?

## Phase 0 - Audit And Lock Scope

### Goal

Audit the current implementation and produce a concrete checklist for the
implementation phases. Do not edit production code in this phase.

### Area Hint

AI chat / internal knowledge provider / retrievers / classifier / chat UI

### Task

```text
Task: Audit current AI chatbox implementation and create a concrete fix checklist for the known review findings without editing production code.

Area hint: AI chat / internal knowledge provider / retrievers / classifier / chat UI

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

Specific scope:
- Inspect `TCTEnglish/Services/AI/Internal/InternalKnowledgeProvider.cs`.
- Inspect all `TCTEnglish/Services/AI/Internal/Retrievers/*.cs`.
- Inspect `TCTEnglish/Services/AI/Internal/DeterministicIntentClassifier.cs`.
- Inspect `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs`.
- Inspect `TCTEnglish/wwwroot/data/ai/website-guides.json`.
- Inspect `TCTEnglish/Views/Ai/_ChatShell.cshtml`.
- Inspect `TCTEnglish/wwwroot/js/ai-chat.js`.
- Inspect existing AI tests under `TCTEnglish.Tests`.
- Inspect actual routes for Reading, Writing, Listening, Contact, and Notifications.
- Do not edit production code in this phase.

Deliverables:
- A short map of current AI chat behavior.
- A list of exact files likely needed for Phases 1-5.
- A checklist of failing or missing behavior to cover with tests.
- Recommended implementation path for Phase 1:
  - aggregate all matching retrievers, or
  - merge recommendation fallback into `LearningProgressRetriever`, or
  - another codebase-consistent approach.
- Targeted test commands to run in later phases.
```

### Phase 0 Done Criteria

- No production code changed.
- Current behavior and gaps are documented in the final response.
- Next phase has a clear implementation recommendation.

## Phase 1 - Fix StudyRecommendation Coverage

### Goal

Fix findings 1 and 2. Make study recommendation actually work for users with
sets but no progress, and fix mastered-card status semantics.

### Area Hint

AI retrievers / internal knowledge provider / StudyRecommendation

### Task

```text
Task: Fix StudyRecommendation intent coverage so AI can recommend a study set even when the user has vocabulary sets but no learning progress, and fix mastered-card status matching.

Area hint: AI retrievers / internal knowledge provider / StudyRecommendation

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

This phase explicitly permits modifying `Program.cs` only if needed for AI retriever DI registration or ordering. Do not modify `appsettings.json` or any `.csproj`.

Specific requirements:
- Fix the issue where `StudyRecommendationRetriever` is unreachable because `InternalKnowledgeProvider` selects only the first retriever that can handle an intent.
- Choose the least risky architecture after inspecting existing tests:
  - update `InternalKnowledgeProvider` to aggregate snippets from all matching retrievers, or
  - merge the set-fallback behavior into the existing `LearningProgressRetriever`, or
  - use another clean codebase-consistent approach.
- Do not solve this by blindly swapping DI order unless you prove it preserves existing progress-based recommendations.
- Fix mastered-card counting to match existing semantics:
  - `Mastered` counts as mastered;
  - `mastered` counts as mastered;
  - `Learned` counts as mastered;
  - matching must be case-insensitive.
- Preserve ownership checks:
  - set reads must be scoped to `OwnerId == userId`;
  - progress reads must be scoped to `UserId == userId`;
  - card reads must not leak cards through another user's sets.
- Keep async EF usage and `AsNoTracking()` for display-only reads.
- Prefer behavior-level tests over implementation-detail tests.

Required tests:
- user has learning progress -> gets existing progress-based study recommendation;
- user has owned sets but no learning progress -> gets concrete study recommendation, not empty state;
- user has no sets -> gets empty state;
- another user's sets/cards/progress do not leak;
- mastered status variants `Mastered`, `mastered`, and `Learned` are counted correctly.

Suggested verification:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests"`

Before closing:
- Review the diff for IDOR, async EF usage, DI side effects, and unintended behavior changes.
- Record what Phase 2 should verify first.
```

### Phase 1 Done Criteria

- StudyRecommendation behavior works with progress and without progress.
- Mastered status counts are correct.
- Relevant tests pass.
- If `Program.cs` was touched, the final summary says exactly why.

## Phase 2 - Fix Quick-Action Intent Fallback And Guide Routes

### Goal

Fix finding 3 and verify route correctness for guide answers.

### Area Hint

AI deterministic classifier / website guide knowledge base

### Task

```text
Task: Fix AI quick-action intent classification fallback and correct website guide routes for Reading, Writing, Listening, support, notifications, and overview/navigation guide prompts.

Area hint: AI deterministic classifier / website guide knowledge base

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

Specific requirements:
- Update `DeterministicIntentClassifier` so fallback classification recognizes guide questions about:
  - Reading;
  - Writing;
  - Listening;
  - contact/support;
  - notifications;
  - about/overview/navigation if represented in `website-guides.json`.
- Keep OutOfScope protection intact for:
  - general English homework;
  - translation requests unrelated to platform usage;
  - grammar explanations;
  - random facts;
  - unrelated questions.
- Correct `website-guides.json` routes to real routes in the current app.
- Verify routes against controller attributes and route mapping before changing:
  - Reading entry route;
  - Writing entry route;
  - Listening entry route;
  - Contact/support route;
  - Notification route or bell dropdown wording.
- If the route is intentionally a legacy `/Home/...` route because of controller attributes, keep it and document why in the final response.
- Do not add feature screens or controller actions just to satisfy a guide route.
- Keep JSON valid and UTF-8.

Required tests:
- deterministic classifier maps these prompts to `WebsiteGuide`:
  - "Làm bài Reading như thế nào?"
  - "Tính năng Writing hoạt động ra sao?"
  - "Cách liên hệ hỗ trợ?"
  - "Tôi có thể xem thông báo ở đâu?"
  - "Tính năng Listening gồm những phần nào?"
- deterministic classifier still maps unrelated prompts to `OutOfScope`.
- `WebsiteGuideRetriever` returns expected guide and route for Reading/Writing/Listening/support/notification prompts.

Suggested verification:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~AiBaselineRegressionTests"`
- Run `python scripts/encoding_guard.py` if it exists.

Before closing:
- Manually review touched Vietnamese/JSON text for mojibake.
- Record what Phase 3 should verify first.
```

### Phase 2 Done Criteria

- Quick-action guide prompts do not classify as `OutOfScope` under fallback.
- Guide routes are real current routes.
- JSON remains valid UTF-8.
- Relevant tests pass.

## Phase 3 - Harden Quick Actions UI

### Goal

Make quick-action buttons reliable and polished in both full chat and embedded
launcher chat.

### Area Hint

AI chat Razor partial / wwwroot JavaScript / chat shell UI

### Task

```text
Task: Harden the AI chat quick actions UI so the empty-state buttons submit safely, work in embedded and full chat, and remain accessible/responsive.

Area hint: AI chat Razor partial / wwwroot JavaScript / chat shell UI

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

Specific requirements:
- Inspect:
  - `TCTEnglish/Views/Ai/_ChatShell.cshtml`
  - `TCTEnglish/Views/Ai/Chat.cshtml`
  - `TCTEnglish/Views/Ai/ChatEmbed.cshtml`
  - `TCTEnglish/wwwroot/js/ai-chat.js`
  - `TCTEnglish/wwwroot/css/ai-chat.css`
- Ensure quick action click handling is scoped to the initialized chat shell/form where practical.
- Prevent double submission while a quick action is in progress.
- Keep anti-forgery behavior intact for `AiController.Send`.
- Quick actions should hide or disable only after the client has accepted the submit.
- Full chat page must work.
- Embedded launcher chat must work.
- Button text must not overflow on small widths.
- Do not introduce large visual redesigns unless needed for correctness.
- Avoid unrelated CSS churn.

Manual smoke test if browser tooling is available:
- open `/AI/Chat` with empty chat state;
- click Reading quick action;
- start a new conversation and click Writing quick action;
- start a new conversation and click support quick action;
- start a new conversation and click study recommendation quick action;
- open embedded launcher chat and repeat one quick action;
- verify exactly one user message is sent per click;
- verify an answer appears;
- verify no duplicate message appears;
- verify composer returns to usable state.

Suggested verification:
- Run targeted AI tests from Phases 1-2.
- Run build or test command that compiles Razor views if available.
- Run `python scripts/encoding_guard.py` if it exists and Vietnamese UI text was touched.

Before closing:
- Review accessibility labels, button behavior, encoded Vietnamese text, and mobile wrapping.
- Record what Phase 4 should verify first.
```

### Phase 3 Done Criteria

- Quick actions send one message per click.
- Full page and embedded chat both work.
- No CSRF regression.
- Responsive layout remains usable.

## Phase 4 - Add Focused Regression Test Pack

### Goal

Make the fixed AI chat behavior hard to regress.

### Area Hint

`TCTEnglish.Tests` / AI regression tests

### Task

```text
Task: Add a focused AI chat regression test suite covering retriever orchestration, study recommendations, website guide quick actions, deterministic fallback, and route correctness.

Area hint: TCTEnglish.Tests / AI regression tests

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

Specific requirements:
- Reuse existing test patterns in `TCTEnglish.Tests`.
- Add tests that exercise the real provider/retriever composition as much as practical.
- Avoid only testing manually curated old retriever lists if that misses real DI/provider behavior.
- Cover:
  - StudyRecommendation with progress;
  - StudyRecommendation with owned sets but no progress;
  - StudyRecommendation with no sets;
  - no data leakage from other users;
  - mastered status variants: `Mastered`, `mastered`, `Learned`;
  - deterministic fallback classifies Reading/Writing/Listening/support/notification quick prompts as `WebsiteGuide`;
  - WebsiteGuideRetriever returns real current routes for Reading/Writing/Listening/support/notification;
  - OutOfScope still refuses unrelated grammar/homework/random fact prompts.
- Do not over-test private implementation details where behavior-level tests are enough.
- Keep fixtures maintainable and small.

Suggested verification:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~Ai"`
- If reasonable, run full `dotnet test`.

Before closing:
- Review test names and fixture setup for maintainability.
- Record what Phase 5 should verify first.
```

### Phase 4 Done Criteria

- Regression tests cover the known findings.
- AI-related tests pass.
- Tests are readable and not excessively brittle.

## Phase 5 - Final Verification, Encoding, And Bug Log

### Goal

Close the hardening package cleanly.

### Area Hint

AI chat final verification / repo context docs

### Task

```text
Task: Finalize AI chatbox hardening by running encoding checks, updating bug-fix context, and performing final verification.

Area hint: AI chat final verification / repo context docs

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

Specific requirements:
- Inspect final diff for unrelated changes.
- Run `python scripts/encoding_guard.py` if it exists.
- Run targeted AI tests.
- Run full `dotnet test` if reasonable.
- If UI was changed and browser tooling is available, smoke test:
  - full `/AI/Chat`;
  - embedded launcher chat;
  - quick actions;
  - normal typed message;
  - conversation history remains usable.
- Append a new entry to `.ai/context/bug-fix-log.md` using its required format.
- Update `.ai/context/known-issues.md` only if one of the fixed AI chat issues was listed there.
- Do not update architecture docs unless the implementation changed an architectural contract.
- Do not start optional quality improvements in this phase.

Suggested verification:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~Ai|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests"`
- `dotnet test` if reasonable.

Before closing:
- Confirm all Global Done Criteria are met or explicitly list remaining blockers.
```

### Phase 5 Done Criteria

- Encoding check is run or explicitly unavailable.
- Relevant tests pass.
- Bug-fix log is updated.
- Remaining risks are documented.

## Phase 6 - Optional Answer Quality Improvements

Only run this phase after Phases 1-5 are complete and verified. Split it into
smaller phases if it grows.

### Goal

Improve AI answer quality without changing provider architecture more than
necessary.

### Area Hint

AI answer quality / website guide retriever / answer composer

### Task

```text
Task: Improve AI chat answer quality after the bug fixes by expanding website guide coverage, improving retrieval ranking, and preserving existing safety boundaries.

Area hint: AI answer quality / website guide retriever / answer composer

Follow the Shared Agent Rules in `docs/ai-chatbox-hardening-phase-playbook.md`.

Specific requirements:
- Do this only after Phases 1-5 are complete and verified.
- Do not change auth, billing, appsettings, `.csproj`, migrations, or unrelated controllers.
- Review current website guide coverage and identify missing high-value TCT English topics.
- Improve retrieval only within the current internal-knowledge architecture.
- Keep answers concise and Vietnamese.
- Do not reveal internal architecture, database details, admin routes, or backend logic.
- Add behavior tests for any new answer/retrieval behavior.
- If this grows beyond guide coverage and ranking, stop and split the next work into another phase file or another phase entry.
```

### Phase 6 Done Criteria

- Existing fixed behavior remains green.
- New answer-quality behavior is covered by tests.
- No safety boundary regression.

## Recommended Phase Order

Run phases in this order:

1. Phase 0 - Audit And Lock Scope
2. Phase 1 - Fix StudyRecommendation Coverage
3. Phase 2 - Fix Quick-Action Intent Fallback And Guide Routes
4. Phase 3 - Harden Quick Actions UI
5. Phase 4 - Add Focused Regression Test Pack
6. Phase 5 - Final Verification, Encoding, And Bug Log
7. Phase 6 - Optional Answer Quality Improvements

Do not combine Phases 1, 2, and 3 unless the user explicitly asks. They touch
different risk areas and are easier to review independently.

## Minimal Command Templates

Use these as user-facing commands in future sessions:

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 0 only.
```

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 1 only.
```

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 2 only.
```

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 3 only.
```

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 4 only.
```

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 5 only.
```

```text
Read `docs/ai-chatbox-hardening-phase-playbook.md` and execute Phase 6 only.
```
