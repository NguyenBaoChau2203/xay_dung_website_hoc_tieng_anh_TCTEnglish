# Writing generation backend contract — step 01

Date: 2026-11-15. Contract version: **1**. Local implementation only.
UI binding belongs to step 02. Existing Writing recent visits and drafts remain
separate from creation. No paid live provider acceptance was performed.

## Requests and routes

All routes require authentication. Identity comes from `GetCurrentUserId()`.
Creation also checks the current database account: Premium/Admin, not blocked or
anonymized. Admin keeps its existing unlimited creation allowance. Possessing an
old Premium cookie does not override current database entitlement.

| Method | Route | Purpose |
|---|---|---|
| POST | `/Home/Writing/Exercises/CreateFromAi` | Existing route; either input mode |
| GET | `/api/writing/generation/capabilities` | Versioned internal frames, all 21 profiles, limits, slot definitions and unfilled bilingual previews |
| GET | `/api/writing/generation/{operationId}` | Owned receipt/result recovery; expires stale leases/offers |
| GET | `/api/writing/generation/by-key/{key}` | Recover the owned receipt when the first response, including its operation ID, was lost |
| GET | `/api/writing/generation/quota` | Current account owner, shared successful/reserved/remaining allowance and UTC reset; Admin has null limit/remaining |
| POST | `/api/writing/generation/{operationId}/confirm-template` | Explicitly accept a limited frame and its exact slots |
| POST | `/api/writing/generation/{operationId}/cancel` | Fence a pending operation; a completed result remains completed |

Writes require the existing `RequestVerificationToken` antiforgery header/cookie
pair. Recovery and confirmation for an inaccessible operation return404. An
inactive/non-entitled account cannot recover private result data. Responses use
`Cache-Control: no-store`. Creation accepts at most64KiB HTTP body; confirmation
accepts8KiB. These transport limits do not replace field validation.

Legacy request (omitting `mode` means `from_text`):

```json
{
  "sourceText": "A complete source article in Vietnamese or English, supplied by the learner.",
  "idempotencyKey": "stable-client-key-001"
}
```

The source remains20–4000 characters after line-ending/trim/Unicode NFC
normalization. The existing faithful source alignment prompt and enum fallbacks
remain. Explicit source-mode requests must not include active brief controls.
An outage never replaces this source with an internal template.

Brief request:

```json
{
  "mode": "from_brief",
  "brief": "Write a polite leave request to visit my family.",
  "level": "beginner",
  "contentType": "emails",
  "length": "short",
  "tone": "formal",
  "idempotencyKey": "stable-client-key-002"
}
```

Briefs require20–1200 characters, at least10 letters/digits, valid Unicode and no
unsupported control characters. `sourceText` must be empty. Unknown explicit
modes, inactive fields and invalid controls return400 before provider dispatch.
The brief must carry a stable key. Level values are `beginner`, `intermediate`,
`advanced`; formats are `emails`, `diaries`, `essays`, `articles`, `stories`,
`reports`; tones are `neutral`, `friendly`, `formal`. Length bands are
`short`6–8, `medium`10–12, `long`14–16 aligned pairs. Recognizable explicit
structured/natural-language constraint conflicts are rejected. This detection
does not claim complete semantic understanding of arbitrary descriptions.

The brief prompt asks OpenAI to author complete original content and then align
it. User text is task data, with no authority to change system instructions,
reveal keys, invoke tools or retrieve URLs. It is not treated as an article to
translate. Output must preserve the selected controls exactly; there are no
legacy enum defaults for brief results. Validation checks required metadata,
pair completeness/length, unique normalized sentences, count bands, paragraph
flags and final paragraph boundary. Structural acceptance cannot certify the
meaning or quality of a live translation.

## Envelope and frontend branching

Existing success fields remain under `data`: `exerciseId`, `sentenceCount`,
`level`, `contentType`, `isReplay`. The additive response is:

```json
{
  "success": true,
  "error": "",
  "errorCode": "",
  "retryAfterSeconds": 0,
  "contractVersion": 1,
  "data": {
    "operationId": "00000000-0000-0000-0000-000000000001",
    "status": "succeeded",
    "exerciseId": 123,
    "sentenceCount": 7,
    "level": "beginner",
    "contentType": "emails",
    "isReplay": false,
    "inputMode": "from_brief",
    "generationMethod": "internal_template",
    "templateKey": "leave-request",
    "templateVersion": 1,
    "fallbackReason": "network",
    "coverage": "Supported",
    "knownProviderTokens": 10,
    "uncertainProviderTokenUpper": 1000,
    "providerOutcomeUncertain": true
  }
}
```

The numbers/IDs above are synthetic contract examples. The incidental numeric
`outcome` property is a service enum; the UI must branch on the documented
`status` and `errorCode` strings. Do not equate HTTP200 with exercise creation:
an offer uses200 with `success:false`, `data.status:"awaiting_template"` and
`coverage:"NeedsTemplateConfirmation"`. It includes `templates`,
`coveredConstraints`, `missingConstraints` and `offerExpiresAtUtc`.
There is no practice redirect or quota success until `status` is `succeeded`.

| HTTP | State/code | Frontend handling |
|---|---|---|
| 200 | `succeeded` | Use the existing private practice redirect fields; show truthful origin |
| 200 | `awaiting_template` | Show the offered frame, unfilled preview, supported slots and omissions; request explicit acceptance |
| 202 | `running` | Poll the owned operation using bounded intervals; preserve the logical key |
| 202 | `recovery_required` | A commit/response may be uncertain; recover this receipt before another creation |
| 400 | Invalid input/constraints/confirmation | Correct input; no provider/fallback bypass |
| 403 | `premium_required` | Current account entitlement guard |
| 404 | Foreign/missing operation or `result_deleted` | Do not create a replacement automatically |
| 409 | `idempotency_conflict` | Same key with different original or confirmed payload |
| 429 | `daily_quota_exceeded` | Shared application allowance; `Retry-After` when supplied |
| 503 | `failed` / `cancelled` | Preserve the code and operation; no blind resend |

## Internal coverage and informed confirmation

The pure internal engine has zero HTTP calls. It still requires the running
application and database. Version1 supports **short only**, exactly7 bilingual
lines per profile. All21 profiles are source-managed original language blocks.

| Key | Topic | Format | Tone | Levels | Slots |
|---|---|---|---|---|---|
| `leave-request` | work | emails | formal | all3 | recipient, startDate, endDate, reason |
| `meeting-follow-up` | work | emails | formal | all3 | none; fixed fictional meeting/trial/Friday follow-up |
| `study-reflection` | study | diaries | friendly | all3 | none; fixed reading-session reflection |
| `transport-opinion` | travel | essays | neutral | all3 | none; fixed public-transport argument |
| `study-habit` | study | articles | neutral | all3 | none; fixed ten-minute reading habit |
| `weekend-story` | daily-life | stories | friendly | all3 | none; fictional Linh/community-garden story |
| `community-cleanup` | environment | reports | formal | all3 | none; fictional ten-volunteer cleanup report |

The capabilities response advertises only these combinations. `UnfilledPreview`
is an unfilled source-managed frame, not a generated result. Leave slots require
a1–60-character proper name (letters, spaces, apostrophe/hyphen), ISO
`yyyy-MM-dd` dates spanning0–31 days inclusive of equal dates, and reason choice
`family`, `health` or `personal`. English dates are formatted with invariant
English month names; Vietnamese dates use `dd/MM/yyyy`. Arbitrary reasons are
never inserted into an English sentence as fake translation. Each advertised
slot is required; extra slots/unknown versions are rejected.

Keyword matching only suggests compatible frames. It never claims to understand
all the learner's free description. Every free-form match requires confirmation
and declares that details outside the frame/slots are omitted. Unsupported
topic/register/length yields `Unsupported`, no local creation and no generic
replacement. A confirmed valid frame has `Supported` coverage for that accepted
scope, not for every condition in the original brief.

```json
{
  "templateKey": "leave-request",
  "templateVersion": 1,
  "acceptLimitedCoverage": true,
  "slots": {
    "recipient": "An",
    "startDate": "2026-12-01",
    "endDate": "2026-12-03",
    "reason": "family"
  }
}
```

Send this only to the same owned operation's confirmation route. The original
request hash stays immutable. A separate canonical confirmation hash binds the
frame/version, sorted slot payload and original hash. Replay returns the same
result; a changed confirmed selection conflicts. Confirmation does not call
OpenAI. Existing-catalog selection/cloning is not part of these endpoints.

## Durable state, quota and usage

`WritingGenerationOperations` has a unique `(UserId, IdempotencyKey)` index.
Keys are trimmed, lowercased ASCII letters/digits/`-`/`_`, maximum100 characters;
case does not distinguish operations. The SHA256 identity covers normalized
mode/source/brief and every structured control. Legacy no-key requests use a
deterministic user-scoped payload identity; an intentional second creation must
supply a new key. Receipts survive process restart without a five-minute cache.

Short serializable transactions use the existing `UserMutationScope` database
user-row lock. No DB transaction or user lock spans OpenAI HTTP. A100-second
lease plus fence identifies the owning attempt. The logical request budget is
90seconds including normal finalization, with separately bounded5-second failure
cleanup. Recovery fences expired attempts, releases reservations and records
`outcome_unknown`; it never resends OpenAI. A suitable brief can then offer a
template. Explicit cancellation is terminal and never offers a fallback.
Late responses cannot publish over cancellation, expiration or a settled result.

Offers expire after24hours and hold **no reservation**. Confirming an expired
offer cannot revive it. Recovery is demand-driven through stable-key replay or
the status route. Even without a recovery caller, expired leases no longer
count as active reservations, so a crashed process cannot block quota forever.

Premium allows five successful private creations per UTC calendar day across
both modes/engines. UTC00:00 is07:00 in Asia/Bangkok/Vietnam. Count existing
successful `create-from-ai` logs exactly once; each new settlement writes one
such log. Do not add receipt successes to the same count. Unexpired running
reservations protect concurrent allowance, including while crossing midnight.
Finalization rechecks current role/status and the current UTC day; a cross-day
success is assigned to its settlement day. Template confirmation checks the
then-current day's allowance. Admin retains no five-success cap.

The existing OpenAI transport retains model/settings, bounded body reads,
deadline, retry policy and outage guard. Writing adds usage callbacks and stops
retrying an uncertain dispatched outcome. Definitive429 rejections can use the
existing bounded retry policy; there is no outer retry/circuit or second provider.
Circuit/config/authentication/provider billing/rate/outage/malformed-output
failures may offer compatible templates. Invalid input, application entitlement,
application quota, cancellation, refusal and guard failures never do.

Known cumulative provider tokens and a conservative uncertain-token upper bound
remain on the receipt even if the eventual engine is internal. A crash before a
usage callback leaves an uncertainty flag; zero recorded tokens is not proof of
zero billing. The internal composition itself uses zero external calls, but its
earlier OpenAI attempt may have cost money. Exactly-once provider billing is not
claimed. Failure before publication or a lost commit acknowledgement recovers
the application receipt instead of re-dispatching.

## Private persistence, origin and retention

Finalization atomically saves the owned/unpublished exercise, ordered bilingual
sentence pairs, origin, successful log and receipt/quota settlement. Creating an
exercise writes no attempt/progress/XP/streak/badge/credit/outbox/recent visit.
Practice and assessment continue through current access-controlled services.

`WritingExercises` receives nullable `InputMode`, `GenerationMethod`,
`TemplateKey`, `TemplateVersion`, `FallbackReason`. Old rows keep null values:
legacy/unknown origin. New engines use `openai` or `internal_template`; both keep
the established private `SourceType=premium-user-ai` access discriminator.
Paged cards and practice ViewModels expose a typed `origin` object. The success
response also exposes origin directly. Step02 must label these “Tạo bằng OpenAI”
and “Tạo theo khung song ngữ nội bộ” respectively.

Receipts retain hashes, constraints, state, usage and result references, not raw
briefs, source text or slot values. Raw generated sentences remain private in the
existing exercise tables. Operational feature logs contain IDs/error codes,
not prompts, answers or keys. Minimal receipt tombstones remain for the account
lifetime to preserve durable replay; offer usability ends after24hours. Actual
account removal/anonymization deletes receipts, logs and owned private exercises
in the existing locked cleanup transaction. Deleting an unused exercise sets its
receipt FK to null, retaining `result_deleted` replay protection.

Migration: `20261115010152_AddWritingGenerationOperations` with Designer and
snapshot. `Up` is additive; `Down` removes only this feature's new schema and was
reviewed but not executed. Existing badge/recent migrations remain intact.
No public content packages, production manifest, global provider configuration,
Program.cs, appsettings or project files are changed by this step.

Exact local evidence and language review are in [01-backend.md](01-backend.md).

Step02 integration review adds the two read endpoints above without schema changes.
Quota returns `{success:true,data:{ownerUserId,dailyLimit,succeeded,reserved,
remaining,isUnlimited,resetAtUtc}}`. Both endpoints require current authenticated
identity, enforce current entitlement/ownership and return no-store; missing or
foreign receipts return404. These reads never dispatch OpenAI. The browser stores
only the owner-scoped key, SHA256 fingerprint and operation ID in sessionStorage,
and recovers by key before another creation when the initial response was lost.
Reload/activation performs reads only. Unknown confirmation/cancel outcomes keep
the receipt. A conflict never silently rotates into a new key. Terminal failures
permit a new logical request only on an explicit subsequent submit.

The current UI and final review evidence/limits are in
[03-review-and-local-handoff.md](03-review-and-local-handoff.md), including the
accidental live-call incident; the review as a whole must not be described as
having made zero paid calls.
