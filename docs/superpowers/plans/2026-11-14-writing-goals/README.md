# Writing, Goals and Profile work pack

Prepared on 2026-11-14 from master `c6367a00269bc23749a9a02a7d26bcc89001fac8`.
Working branch: `fix/writing-goals-profile-polish`.

## Owner decisions and execution order

The latest request replaces eight lessons with **six additional lessons in each
level/content-type cell: 3 levels × 6 formats × 6 = 108 new public Writing lessons**.
Existing lessons remain. Small topic tags do not multiply this quota.
Gemini 3.8 Flash High handles interface work first. GPT 6.1 Sol High then handles
Writing logic, Goals integration, badge rules and content in separate chats.
Return to Gemini for final binding, then GPT for integration acceptance.
Use the exact model selected by the owner; these labels do not certify availability.

| Order | Plan | Execution prompt | Client/model | Result |
| --- | --- | --- | --- | --- |
| 01 | [UI first](01-ui-first.md) | [01 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/01-gemini-ui-first.md) | Gemini 3.8 Flash High | Polish existing real UI; record missing data contracts |
| 02 | [Writing behavior](02-writing-backend.md) | [02 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/02-gpt-writing-backend.md) | GPT 6.1 Sol High | Audit/fix access, assessment, drafts, progress, completion |
| 03 | [Goals and streak](03-goals-streak-profile.md) | [03 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/03-gpt-goals-streak-profile.md) | GPT 6.1 Sol High | Confirmed study days, XP, daily targets and Profile consistency |
| 04 | [Badge system](04-badges.md) | [04 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/04-gpt-badges.md) | GPT 6.1 Sol High | Ten new real awards; tiers, mystery rules and safe reconciliation |
| 05 | [108 Writing lessons](05-writing-content.md) | [05 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/05-gpt-writing-content.md) | GPT 6.1 Sol High, three chats |36 beginner, 36 intermediate, 36 advanced; portable packages |
| 06 | [UI integration](06-ui-integration.md) | [06 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/06-gemini-ui-integration.md) | Gemini 3.8 Flash High | Bind actual new backend contracts; finish responsive states |
| 07 | [Acceptance](07-integrated-acceptance.md) | [07 prompt](../../../../.ai/prompts/writing-goals-2026-11-14/07-gpt-integrated-acceptance.md) | GPT 6.1 Sol High | Review/fix remaining integration defects and hand off manual tests |

Run these sequentially in the same checkout/branch. One agent per chat; no agents
running concurrently. Preserve previous-stage changes even when uncommitted.
Each chat reads this README, its own plan/prompt, current AGENTS.md and only the
relevant source/skill. It need not preload the other plans or old conversation.

## Authority

This preparation turn authorizes pull/sync, branch creation and planning documents.
It does not implement application features. Sending a stage's launch message
requests its scoped local implementation and verification. Future stages have no
new authority to commit/push, create PRs, run remote CI, merge or deploy; those
require the owner's instruction for this work. The previous Speaking release
authorization is not a standing release grant for this new feature.

Preserve Docker/user data and secrets. Follow AGENTS.md for protected configuration,
additive EF migrations and shared-content packages. Do not change Program.cs,
appsettings or project files without actual task authority. Prefer extending
existing registered services. No paid AI calls are authorized by this pack.
Writing browser submissions must use verified fee-free configuration or a test
host with fake optional providers. An existing saved key does not grant spending.

## Source evidence and risks

These are source observations, **not newly reproduced runtime defects**. Later
stages must reproduce disputed behavior before changing it.
Unprefixed application paths in these source maps are relative to `TCTEnglish/`;
`docs/`, `.ai/`, `scripts/` and `TCTEnglish.Tests/` paths are repository-relative.

| Observation | Source anchor | Stage |
| --- | --- | --- |
| Goals already has real persisted rewards: 12 seed badges, seven metric types, UserBadges and award logic | Models/Badge.cs; GoalsService.RefreshUserBadgesAsync |03/04 |
| Profile earned list joins UserBadges; its empty-state template additionally hardcodes three different proposed badge names/zero counters | GoalsService.GetEarnedBadgesForProfileAsync; Views/Account/Profile.cshtml, earned-badges section |01/04/06 |
| Goals expires a broken current streak when reading; Profile uses raw user.Streak | GoalsService.ComputeCurrentStreak; Accounts/AccountProfileService.BuildAsync |03 |
| WeeklyActivity is projected from CardsReviewed. Mini streak/daily dots reuse it as general study/completion state | GoalsService.GetGoalsAsync; Views/Goals/Index.cshtml |01/03 |
| Profile recently-unlocked uses raw UTC date and seven-day range; Goals uses business date/today | GoalsService.GetEarnedBadgesForProfileAsync and BuildBadgeViewModel |03/04 |
| Goals business date uses static BusinessDateHelper.Today; StreakService uses injectable LearningBusinessClock | GoalsService.ResolveBusinessDate; StreakService |03 |
| Login establishes the cookie; it does not itself award a learning day | AccountController.SignInUserAsync |03 |
| Writing evaluation persists attempts/progress transactionally; completion credit is currently requested only on first full exercise completion | WritingService.EvaluateWritingSentenceAsync; Learning/CompletionCreditService.cs |02/03 |
| Optional cloud feedback does not decide local pass/XP; NeedsReview must remain distinct from accepted/revision | Services/Writing/WritingAssessmentPolicy.cs; WritingService |02 |
| Writing levels are beginner/intermediate/advanced; content types are emails/diaries/essays/articles/stories/reports | WritingService.GetWritingLevels/GetWritingContentTypes |05 |
| Badge reference data currently uses HasData; WritingExercise sharing is supported by the typed shared-data pipeline | Models/DbflashcardContext.cs; docs/operations/shared-data-workflow.md |04/05 |

CodeGraph supplies partial Razor source in this checkout; bounded direct reads of
the missing view sections were needed. Use the same fallback for omitted source.

## Proposed learning contract

Treat the following as the requested implementation direction, to be reconciled
with existing contracts and documented in stage 03 before changes:

- Signing in, loading a page, refreshing or saving a target alone earns no study
  streak, XP or badge. Clearly explain this in the UI.
- A meaningful accepted Writing sentence persisted for the current user is a
  confirmed practice event, even before the whole exercise is finished. Repeating
  a valid exercise on a later business date can keep the practice streak.
- Keep **practice-day activity**, **first exercise completion credit**, and
  **daily-goal completion** separate. A repeat must not mint another lifetime
  exercise credit, completion count or20 XP reward. Existing once-per-day streak
  bonus remains once per business date, not per sentence/request.
- The Writing goal counter continues to represent first completed exercises
  unless the owner explicitly changes that product rule. State its meaning.
- One UTC+7 learning business date, injectable clock, maximum one streak step/day,
  missing-day reset and preserved longest streak across Goals/Profile/dashboard.
- Unknown historical activity is unknown. Never manufacture study dates, hours,
  streaks or awards from login timestamps or merely-created empty rows.
- Goals reads and Profile reads do not become reward-minting GET requests.
  Any historic award reconciliation is explicit, bounded, idempotent and evidenced.

## Badge tier/copy proposal

These literal Vietnamese strings are UI data, shared by both model stages.
Persist stable keys; keep presentation labels separate from award thresholds.

| Stable display key | UI label | Meaning |
| --- | --- | --- |
| chill | Nhập môn chill | First small milestones |
| cuon | Bắt đầu cuốn | Repeated practice |
| chien-than | Hệ chiến thần | Sustained challenge |
| trum-cuoi | Trùm cuối server | Long-term milestones |
| bi-an | Ủa, bí mật nè! | Hidden discovery group |

Mystery is a visibility attribute, not necessarily harder than every normal
badge. Model a normal difficulty plus secret visibility, and expose the mystery
display group when appropriate. Locked secrets reveal only a generic hint;
their rule/name/threshold/progress must not leak through HTML/JSON/data attributes.
Earned secrets reveal their details. No click-to-unlock or browser award logic.

Preserve the existing 12 codes, IDs and earned history. Add these ten proposed
definitions after stage 03 verifies the metrics. Thresholds describe real server
metrics; they do not imply an independently certified language proficiency.

| New stable code | Name | Group | Exact proposed rule |
| --- | --- | --- | --- |
| writing-mood | Bút mới lên mood | chill |3 distinct first-completed Writing exercises |
| study-checkin | Check-in bằng bài học | chill |3 confirmed active business dates |
| writing-deadline | Deadline cũng phải nể | cuon |10 distinct first-completed Writing exercises |
| xp-mood | XP lên mood | cuon |500 legitimately accumulated XP |
| keyboard-form | Bàn phím vào form | chien-than |25 distinct first-completed Writing exercises |
| steady-flame | Giữ lửa không seen | chien-than |Longest confirmed streak 14 days |
| keyboard-boss | Trùm cuối bàn phím | trum-cuoi |50 distinct first-completed Writing exercises |
| monthly-flame | Giữ lửa xuyên tháng | trum-cuoi |Longest confirmed streak 30 days |
| secret-plot-twist | Plot twist thứ 13 | bi-an |13 distinct first-completed Writing exercises |
| secret-quiet-effort | Hệ chăm ngầm | bi-an |21 confirmed active business dates |

The last two descriptions are development reference data, not locked-user copy.
Reconcile existing tiers coherently; do not rename legacy awards silently.
No sleep-deprivation, spending, password/account-change or random-chance awards.

## Content quota and reference source

| Level | emails | diaries | essays | articles | stories | reports | New total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| beginner | 6 | 6 | 6 | 6 | 6 | 6 | 36 |
| intermediate | 6 | 6 | 6 | 6 | 6 | 6 | 36 |
| advanced | 6 | 6 | 6 | 6 | 6 | 6 | 36 |
| Total | 18 | 18 | 18 | 18 | 18 | 18 | 108 |

Use original authored bilingual sentence-by-sentence exercises or reusable
licensed/public-domain material with precise provenance. The owner permits
reference to multiple sources, including [EnglishDatpmt Writing](https://english.datpmt.com/writing).
Its public page was inspected on 2026-11-14: three levels and six formats; the
public page alone does not establish rights/access to its full lesson texts.
Reference its interaction/topic ideas and link sources; do not bypass login or
bulk-copy unlicensed lesson banks. Additional public references inspected during
planning are [British Council writing](https://learnenglish.britishcouncil.org/free-resources/writing)
and [Cambridge writing activities](https://www.cambridgeenglish.org/learning-english/activities-for-learners/?skill=writing).
Use their format/skill guidance as references; access to a page does not grant
bulk republication rights. Verify the exact page/license for any text actually
reused. Retain this project's three level keys; these references do not certify
the authored lessons as independently CEFR-assessed. No paid generation calls.

## Shared handoff files

Each stage creates/updates its own ledger under
`docs/feature-audits/2026-11-14-writing-goals/` using the stage number/name.
Stage 01 writes `01-ui-data-contract.md`; stage 03 writes
`03-learning-contract.md`; stage 04 writes `04-badges-contract.md`; stage 05 writes
`05-content-matrix.md` with batch receipts. Stage 06 consumes the actual contracts,
not guessed property names. Save repeatable manual steps in stage 07's ledger.
Every handoff states actual checks, blocked checks and files changed in Vietnamese.

## Copy-paste launch messages

Select the named model manually, then paste only the relevant block into a new chat.
Paths below are repository-relative; the branch must be inspected before editing.

### 01 — Gemini UI first

```text
Read .ai/prompts/writing-goals-2026-11-14/01-gemini-ui-first.md and execute stage 01 locally on fix/writing-goals-profile-polish. Preserve the current attractive Goals design, polish Writing/Goals/Profile using real existing data, and record missing backend contracts. Keep new unsupported badge states out of the live UI. Work as one agent, finish scoped checks and report in Vietnamese. No commit, push or deployment.
```

### 02 — GPT Writing logic

```text
Read .ai/prompts/writing-goals-2026-11-14/02-gpt-writing-backend.md and execute stage 02 locally. Audit and fix Writing access, assessment, saved progress, drafts and completion integrity. Preserve Gemini's UI changes and record the accepted-sentence event boundary for stage 03. One agent; no paid API calls or remote release actions. Report evidence in Vietnamese.
```

### 03 — GPT Goals/streak/Profile

```text
Read .ai/prompts/writing-goals-2026-11-14/03-gpt-goals-streak-profile.md and execute stage 03 locally after stage 02. Verify real study-day streaks, XP, goal counters and consistent Profile/dashboard displays; fix reproduced defects and implement the agreed practice-day contract without duplicate completion rewards. One agent; preserve data/UI; no remote release. Report in Vietnamese.
```

### 04 — GPT badge rules

```text
Read .ai/prompts/writing-goals-2026-11-14/04-gpt-badges.md and execute stage 04 locally after stage 03. Add the ten defined awards, GenZ tiers and safe mystery visibility using real server rules; preserve existing awards and ship any required migration/provider proof. Record actual frontend contracts. One agent; no remote release. Report in Vietnamese.
```

### 05a/05b/05c — GPT content, one level per chat

```text
Read .ai/prompts/writing-goals-2026-11-14/05-gpt-writing-content.md. Execute only the beginner batch: 6 new lessons for each of 6 formats, 36 new lessons. This is part of the 108-lesson request, not144. Preserve existing lessons/progress; validate all bilingual content, import idempotently and export reviewed shared-data packages. Save the matrix/receipt and report in Vietnamese. No paid API calls or remote release.
```

```text
Read .ai/prompts/writing-goals-2026-11-14/05-gpt-writing-content.md. Continue only the intermediate batch: 6 new lessons for each of 6 formats, 36 new lessons. Reuse the existing manifest/importer, preserve the beginner batch and old data, validate/export packages and update the receipt. One agent; no paid API calls or remote release. Report in Vietnamese.
```

```text
Read .ai/prompts/writing-goals-2026-11-14/05-gpt-writing-content.md. Continue only the advanced batch: 6 new lessons for each of 6 formats, 36 new lessons. Preserve earlier batches and old data; validate/export packages, rerun safely and prove the complete 18-cell matrix has exactly108 net new lessons. One agent; no paid API calls or remote release. Report in Vietnamese.
```

### 06 — Gemini final integration

```text
Read .ai/prompts/writing-goals-2026-11-14/06-gemini-ui-integration.md and execute stage 06 locally. Bind the completed real backend contracts and 108-lesson catalog to the approved UI, including tier filters, mystery/earned states and Profile consistency. Test through available Chrome DevTools MCP or a real-browser fallback; never fabricate unlocks. One agent; no remote release. Report in Vietnamese.
```

### 07 — GPT integration acceptance

```text
Read .ai/prompts/writing-goals-2026-11-14/07-gpt-integrated-acceptance.md. Review the combined local work, fix remaining in-scope defects, verify migrations/packages and Writing-to-Goals-to-Profile behavior, then give me the manual test order. Preserve all existing work/data. One agent; no paid API calls, commit, push or deployment. Report actual evidence and remaining limits in Vietnamese.
```
