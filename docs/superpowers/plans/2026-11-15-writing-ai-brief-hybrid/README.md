# Writing from a brief, with an internal bilingual fallback

Prepared: 2026-11-15. Checkout branch observed: `fix/writing-goals-profile-polish`.
Base HEAD: `c6367a00269bc23749a9a02a7d26bcc89001fac8`; substantial newer local work
is uncommitted. Source files are changing while Writing recent-history work runs.
Re-read the actual implementation before executing these plans.

## Confirmed owner decisions

- Keep the current pasted-text-to-exercise flow and add a real create-from-brief
  flow: users describe the article they want to practise, not supply its full text.
- OpenAI remains primary. Fallback uses an **internal bilingual template engine**,
  with supported topics/intents and explicit limitations.
- No second AI provider/API, no Ollama installation/model download or local LLM
  service is part of this selected implementation.
- A catalog recommendation is not a newly generated article. Template output
  is not labeled as free-form AI generation. Unsupported details are not silently
  discarded to claim success.
- This turn prepares plans/prompts only. Execute locally when the owner invokes
  the relevant launch message. No remote release stage or paid live test is granted.

## Work sequence and model handoff

| Step | Plan | Prompt | Purpose |
| --- | --- | --- | --- |
| 01 |[Backend](01-backend.md)|[GPT backend prompt](../../../../.ai/prompts/writing-ai-brief-hybrid/01-gpt-backend.md)|GPT 6.1 Sol High: two input modes, real OpenAI generation, offline frames, durable quota/replay, contracts/tests |
| 02 |[Interface and acceptance](02-interface.md)|[UI prompt](../../../../.ai/prompts/writing-ai-brief-hybrid/02-gemini-interface.md)|Gemini 3.8 Flash High: implement the panel with actual backend data and honest fallback behavior |

Work sequentially with one agent at a time. Let the current Writing recent-history
implementation finish before changing shared files. Do not delegate to other AI
agents or launch an external model process. Preserve all existing uncommitted
Writing/Goals/badges/content/recent-history changes; no pull/reset/stash/cleanup or
branch replacement as setup. This plan does not ask to redesign those features.

Use existing registered Writing services and pure typed helpers. Program.cs,
appsettings and project files remain protected; do not invent permission to edit
them. Prepare a specific minimal proposal only if truly necessary. Domain logic,
template definitions and validators can extend current Writing partials without
changing the global IAiProviderClient registration.

## Current source evidence

Application paths below are relative to `TCTEnglish/`. Findings here are source
observations, not newly reproduced runtime defects.

| Evidence | Design consequence |
| --- | --- |
| WritingGenerationViewModels currently accepts SourceText and IdempotencyKey | Add an explicit mode/brief contract; an omitted mode preserves the old request |
| WritingService.Generation prompts translate/align an existing 20–4000-character source | Brief mode needs its own composition prompt and hard user-selected constraints |
| Current successful result supplies exerciseId/level/contentType for practice redirect | Preserve existing envelope/fields and add optional origin/status data |
| Generation currently allows Premium/Admin, with five successful creations/day for Premium | Preserve entitlement/cap; both modes/engines share the same application allowance |
| Existing generation cap counts RequestedAtUtc from UTC midnight; replay/user locks are in-memory with a five-minute window | Document this boundary, and make cross-process/restart replay/quota safe without silently redefining the day |
| The browser creates a fresh idempotency key for each submit attempt | Retry after an unknown response needs a stable logical request and recovery path |
| A transaction currently saves private exercise, sentence pairs and success log | Extend that atomic unit for generation receipt, provenance and quota settlement |
| OpenAiProviderClient already has time/attempt/body limits and an outage circuit | Reuse existing transport; do not add another hidden retry/circuit stack |
| OllamaWritingModel is optional feedback-only and not a generator | It is neither a fallback for this feature nor a reason to start/install Ollama |
| The catalog is 118 public lessons after the 108-lesson work according to its ledger | Query current public data for optional suggestions; do not hardcode that count |

## Product sketch

Two top-level tabs:

1. **"Mô tả bài muốn luyện"**: brief, level, format, length and tone. Example:
   "Viết email xin nghỉ phép để thăm gia đình, trình độ cơ bản, giọng lịch sự."
2. **"Dán bài có sẵn"**: current full Vietnamese/English source conversion,
   retaining its existing limits and meaning-preservation contract.

AI success creates one private bilingual exercise ready for the existing practice
flow. Its source label is **"Tạo bằng OpenAI"**.
If AI is unavailable, present eligible internal frames and explain the supported
details. After informed acceptance, template success creates one private exercise
and says **"Tạo theo khung song ngữ nội bộ"**.

A free-form brief can contain details the offline engine cannot understand. A
keyword/topic match is enough to suggest a frame, not enough to assert the frame
satisfies every requirement. Explicit confirmation is the default. Automatic
fallback is allowed only for an already accepted fully structured supported frame
and slots; no unmodeled free-text constraints may be silently ignored.

The internal engine requires no network for composition. It is useful offline
from OpenAI but still needs the web application and its database to be available.
It does not translate an arbitrary pasted article while OpenAI is unavailable.

## References and evidence discipline

Read current AGENTS.md, prompt-authoring rules and only relevant skills/source.
For error handling, inspect the actual .NET transport first and consult
[OpenAI error codes](https://developers.openai.com/api/docs/guides/error-codes)
and [request-ID guidance](https://developers.openai.com/api/reference/overview#debugging-requests)
(opened 2026-11-15). Backoff respects Retry-After and existing transport attempts;
IDs aid diagnosis, not provider billing exactly-once guarantees.
Unknown-after-dispatch handling is a project design choice requiring durable
application receipts and conservative usage evidence, not a promised provider
idempotency property. Fake-provider tests do not certify live model quality.

## Deliverables and launch messages

Backend writes an actual contract/ledger under
`docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/` before UI implementation.
UI uses that real contract and writes its own browser/test ledger there.
New template wording/rules must be original and reviewed; personal generated
exercises/briefs/answers stay out of shared content packages and Git.

Select GPT 6.1 Sol High for step 01, then paste:

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/writing-ai-brief-hybrid/01-gpt-backend.md and implement step 01 locally after the current Writing recent-history work finishes. Preserve all existing changes. Add true Writing generation from a description using OpenAI and an honest limited internal bilingual-template fallback, with shared quota, durable idempotency, private persistence and regression/provider evidence. No second AI API or Ollama. Work as one agent and report in Vietnamese. No paid live tests, commit, push or deployment.
```

After backend completion, select Gemini 3.8 Flash High for step 02 and paste:

```text
Read D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.ai/prompts/writing-ai-brief-hybrid/02-gemini-interface.md and implement step 02 locally using the actual backend contract. Preserve Writing recent history and existing forms. Build the two creation modes, clear controls/examples, honest template confirmation and recoverable states, then verify the integrated private practice flow. Work as one agent and report in Vietnamese. No paid API calls, commit, push or deployment.
```
