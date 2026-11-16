# Execution prompt 01: Writing from a description and internal fallback

Selected model: GPT 6.1 Sol High. One agent investigates, implements, reviews and
verifies inline. Do not delegate or invoke another AI process/model.

## Outcome and inputs

Add real composition from a learner's brief while preserving the existing pasted
source conversion. OpenAI stays primary. The selected fallback is an internal,
bounded bilingual-template generator, not another API or Ollama/local LLM.

Read AGENTS.md, `.ai/prompts/README.md`,
`docs/superpowers/plans/2026-11-15-writing-ai-brief-hybrid/README.md` and
`docs/superpowers/plans/2026-11-15-writing-ai-brief-hybrid/01-backend.md`.
Use CodeGraph first for indexed source, then bounded reads for missing sections.
Read only relevant bug memory and current generation/private/quota contracts.

Inspect the actual branch/worktree. Expected: `fix/writing-goals-profile-polish`.
Current Writing recent-history implementation may still be active: complete that
work before editing overlapping Writing files. Preserve all uncommitted Writing,
Goals, badge migrations, 108 content packages, tests and recent-history changes.
Do not pull/reset/stash, replace the branch or recreate earlier work as setup.

## Mode and authority

IMPLEMENT_LOCAL when invoked: implement step 01 backend/domain/API/migration and
meaningful tests, plus the precise contract needed by the UI. No commit/push/PR/
remote CI/merge/deploy, hosting changes, live paid API tests, model downloads or
second AI provider. Protected Program.cs/appsettings/project files retain their
guards. Extend existing registered Writing service/pure helpers first; if a
protected edit is essential, prepare the exact minimal proposal and identify the
real authority gap rather than silently changing global configuration.

## Execute

1. Follow tct-superpowers/tct-engineering/tct-api-endpoint and inspect the actual
   source/route/status/role/quota baseline. Reproduce meaningful defects/new
   behavior with failing regressions. Preserve old sourceText-only requests.
2. Add explicit from_text/from_brief request semantics. Brief mode has bounded
   description and the three current levels, six formats, supported length/tone
   controls. Its composition prompt authors complete original bilingual text;
   it does not translate the description as if it were the article.
3. Keep current OpenAI client/model settings, outage guard and transport deadline/
   retry/body limits. Avoid another nested retry/circuit stack. Validate resulting
   sentence pairs, constraints, metadata, counts/paragraphs and output size. New
   brief constraints cannot be silently replaced by legacy enum defaults.
4. Build the pure typed internal template catalog/generator from the plan's seven
   initial intents and three reviewed levels. Advertise actual supported slots,
   tones and lengths. Use original natural bilingual phrase blocks; no network
   calls, arbitrary text as fake translation or random generic filler.
5. Separate Supported/NeedsTemplateConfirmation/Unsupported coverage. A keyword
   match only suggests a frame. Default to informed confirmation if the original
   free description contains unsupported details. Bind selection/version/slots
   to the owner and current operation. Do not pretend offline source translation
   or existing-catalog selection is newly composed AI text.
6. Implement the failure matrix: supported local offers for true provider failure;
   no bypass for invalid input, entitlement, app quota, cancellation or refusal.
   Handle unknown-after-dispatch and uncertain DB/response outcomes through
   durable operation recovery, not blind resubmission or duplicate publication.
7. Implement durable user/key/payload identity and shared reservation/settlement
   for both modes/engines. Keep the current five-successful-creations Premium cap
   and documented UTC boundary; preserve Admin policy. One logical operation,
   including fallback confirmation/retry, creates and consumes at most once.
   Verify independent process/restart/concurrent behavior; memory locks are not
   the correctness boundary. Preserve known/uncertain provider usage separately.
8. Persist private exercise, ordered bilingual sentences, successful result,
   provenance and quota outcome atomically. Expose minimal typed origin/status/
   capabilities/recovery contracts and preserve legacy success fields. Creating
   content grants no study progress/XP/streak/badge or recent visit by itself.
9. Follow tct-ef-migration for all persisted changes. Ship additive migration,
   Designer/snapshot and real MariaDB 10.5.23 upgrade/repeat/FK/index/concurrency/
   old-row preservation evidence, respecting existing uncommitted migrations.
   Extend actual account removal/anonymization for new request data.
10. Run the plan's domain/API/template/quota/recovery/access regressions, build and
    appropriate existing Writing generation/recent/Goals/private tests. Use fake
    OpenAI for all ordinary tests; preserve unrelated failures honestly.

## Deliverables and completion

Complete working backend, not scaffolding/NotImplementedException. Write the real
routes/request/result/capability/provenance/recovery/quota contract in
`docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/backend-contract.md` and
the stage 01 ledger there. Record language review for the 21 core template profiles.
Raw private prompts/answers/keys never go into logs, exports or Git. No changes to
the 108 public lesson packages or production.json; private outputs are not shared.

Report in Vietnamese: changes, exact evidence, migration impact, fallback coverage,
unsupported cases, remaining limits and local versus CI/staging/production.
Do not claim a fake-provider run verifies live AI quality or that every topic works
offline. Leave UI binding to step 02 after this contract is actually available.
