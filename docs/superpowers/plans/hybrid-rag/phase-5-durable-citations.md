# Phase 5 — durable citations, history and revoked-source protection

Entry: Phase 4 APPROVED, matching scope, Phase 5 READY and owner launch.
Mode IMPLEMENT_LOCAL. Skills: tct-superpowers, tct-engineering, tct-ef-migration,
tct-api-endpoint and security. Tentative dates 18–19 Nov. Phase 4 quota migrations
must already be complete; this phase does not repair an unapproved lifecycle.

## Outcome and established source

Persist minimal versioned grounding metadata with accepted assistant messages and
return consistent authorized sources through HTTP, SignalR completion, server views
and paged history. Protect old answer bodies as well as citations after access loss.

Read `TCTEnglish/Models/AiMessage.cs`, `Models/DbflashcardContext.cs`,
`Services/AI/{AiChatService,AiConversationService,AiStreamingService}.cs`,
`Controllers/AiController.cs`, `ViewModels/AI/{ChatReplyDto,AiChatMessageViewModel}.cs`,
`Realtime/AiStreamMessages.cs`, relevant hub mappings and existing history tests.
Verify actual call paths before editing; names/fields below are design proposals.

## Ordered checklist

- [x] Confirm the approved evidence/answer contract and decide a minimal serialized
      grounding envelope with schema version, outcome, source IDs/kind/version/hash,
      capture time and necessary safe rendering identifiers. Do not copy full private
      source snapshots, model reasoning or an entire provider response into metadata.
- [x] Perform the required additive migration impact assessment. Add a nullable
      MariaDB-compatible text mapping to AiMessage only if it satisfies size/query
      needs; use a separate table only with a documented rationale. Preserve legacy
      rows with no envelope and stable message IDs/content semantics.
- [x] Generate migration, Designer and snapshot via repo-local EF. Review Up/Down,
      null/default behavior, type/length/collation, FK/index order and no destructive
      operations. Do not handwrite an applied migration or alter history records.
- [x] Persist accepted answer text and metadata consistently within the existing
      validated-success transaction/lifecycle. Corruption or metadata-write failure
      must not create a success message with mismatched sources or erase known usage.
- [x] Add optional compatible metadata/source fields to the reply/view/stream
      contracts. Keep existing field/event names and existing consumers; absent
      grounding metadata on old rows must not break deserialization/rendering.
- [x] Reauthorize sources before returning history/citations, including load-older
      endpoints and full/embed initial rendering. Rebuild links server-side from
      allowed route/entity mappings; never trust stored/model-provided href values.
- [x] Suppress a historical answer body when it contains content grounded in a
      now-revoked/private source, with a neutral unavailable-source notice. Hiding
      the title/link alone is insufficient. For mixed sources, use a verified safe
      segmentation policy or suppress the whole affected answer; do not guess.
- [x] Apply the same revocation policy when loading history into provider context,
      retrying or caching answers. Ensure direct service callers cannot bypass it.
      The old assistant sentence must not be replayed as trusted evidence.
- [x] Distinguish revoked/removed access from changed-but-still-authorized sources.
      Historical facts may be labeled with capture time; never present an old
      snapshot as current progress or use a changed source as fresh grounding.
- [x] Validate envelope version/length/IDs. Bad/unknown metadata is safe/unverified,
      not a bypass to render arbitrary HTML/routes. Keep legacy UI behavior sensible.
- [x] Add source consistency/revocation/legacy/corrupt metadata regressions across
      send, stream completion, initial history, reload and multiple older pages.
- [x] Append verified bug fixes, review diff and prepare provider-specific evidence.

## Required acceptance

| Trigger | Expected result |
| --- | --- |
| Send then reload or load older twice | Same authorized source IDs/outcome and chronological messages |
| User A opens B's conversation/source IDs | Existing NotFound/access contract; no source/body leak |
| Class removal, downgrade or private-source deletion after reply | Related historical body/source metadata unavailable and excluded from context |
| Old row with null envelope or future/invalid schema | Safe compatible rendering, no fabricated citation |
| Metadata save/cancel failure around accepted reply | Atomic accepted artifacts and correct known-usage settlement |
| Published source edited after capture | Freshness clearly handled; no stale fact treated as current |

Run focused new citation/history/stream/revocation tests and affected Phase 4 quota/
cancel regressions plus build. Verify migration upgrade from representative old
messages on disposable MariaDB 10.5.23; execute again without reapplying and verify
row/content/relationship preservation and no pending model changes. Record actual
connection identity without secrets. SQLite-only evidence cannot approve schema.

No live provider call is required for this phase; use recording fakes. No production
schema/DB change is authorized. New protected config/project files remain outside
scope unless an actual owner grant applies.

## Exit

Deliver code/migration artifacts, HTTP/stream/history contract examples with synthetic
data, provider evidence and handoff/scope. The source metadata is ready for Phase 6
UI; do not expand to visual redesign here. Set AWAITING_REVIEW and stop; Phase 6
remains LOCKED until Codex approval.
