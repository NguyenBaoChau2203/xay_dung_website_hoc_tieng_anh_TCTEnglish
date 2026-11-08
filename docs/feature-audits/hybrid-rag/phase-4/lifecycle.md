# Phase 4 quota turn lifecycle

## Migration Impact: AddAiQuotaTurnStage

**Type**: Additive
**Tables affected**: AiQuotaReservationLedgers
**Backward compatible**: Yes
**Data loss risk**: None

Migration `20261106181456_AddAiQuotaTurnStage` adds nullable columns only:
ActiveStage, LeaseExpiresAtUtc, KnownEmbeddingTokens, KnownGenerationTokens,
UnknownEmbeddingTokens, UnknownGenerationTokens, TerminalIntent.
Existing rows stay valid. A null stage with a null lease keeps the previous
ActualTokens recovery rule. Down drops only those new columns. No table drop,
rename, or NOT NULL without a default.

## States

One conversation keeps the existing execution guard. One ledger row is the
question reservation for that logical turn. A new row starts at Admitted with
LeaseExpiresAtUtc = clock + 3 minutes and ActualTokens = 0. Stage writes refresh
the lease and do not set ActualTokens, so another conversation cannot treat a
live turn as already spent.

| State | Meaning | Another turn may close it |
| --- | --- | --- |
| Admitted | Reserved, no provider send yet | Only after the lease expires |
| Embedding | Query embedding started; unknown embedding bound may be stored | Only after the lease expires |
| EmbeddingKnown | Embedding usage is known; that unknown bound is cleared | Only after the lease expires |
| Generating | Sentence selection started; unknown generation bound may be stored | Only after the lease expires |
| Validating | Generation usage is known; that unknown bound is cleared | Only after the lease expires |
| AwaitingRecovery | Save of a known reply failed. ActualTokens and TerminalIntent=Settle are stored | Yes, including while the old lease is unexpired |
| Settled | Terminal. One Standard question is consumed | No. A second settle is a no-op |
| Released | Terminal. The question is returned | No. A second release is a no-op |

## Known, unknown, and no-spend

The bill is the sum of known embedding, known generation, unknown embedding, and
unknown generation, clamped at int.MaxValue. Token arithmetic is not a currency
invoice.

- No-spend: cancel or failure before any paid stage releases the row and returns
  the question. No assistant row is stored.
- Known spend: the recorded prompt/completion total is settled even when the
  server rejects the model text.
- Unknown spend: timeout or a provider failure after a send settles the
  conservative bound already stored. It is never rewritten to zero.
- If that conservative sum is greater than zero, it replaces the local
  answer-token estimate. If it is zero, settlement uses the requested amount so
  a deterministic reply and a legacy ActualTokens row still settle.
- A release requested after a conservative or ActualTokens charge is stored as
  Settled for that charge.

Expired lease with no spend releases the question. Expired lease with known or
unknown spend settles that charge once. Recovery does not start a new provider
call. Quota saves use an isolated scope or suspend non-quota entities, so a
pending assistant in the request DbContext is not flushed.

## Standard question count

The Standard limit remains 15 pending plus settled questions.

- A request that fails the budget check before reserve consumes no question and
  makes no retrieval or provider call.
- A completed deterministic reply consumes one question. That includes a
  greeting, a clarification, and a fixed server refusal.
- Invalid or unavailable model output is stored as the fixed server sentence and
  returned as HTTP 200. The stored text is that constant, not model prose. The
  client retry on 503/500 therefore does not send the turn again.
- Cancel during embedding stores no assistant row. A known or unknown bound
  still settles one question.
- A later settle of an already terminal row does not add a second question or
  replace the settled token total.

## What does not survive reload

Evidence, selected sentence ids, and citation routes live only in the request.
They are not written to the assistant row. Reloading the conversation does not
restore them as evidence. Assistant history and user-claimed facts are never
evidence. A follow-up resolves from the latest prior user question, and access
is checked again. Phase 5 still owns durable citations.

System policy, user messages, history, and evidence stay separate inputs.
Retrieved text cannot change policy or authorize tools, SQL, web, or actions.

## Verified closeout contract — 2026-11-07

Chat transports check durable ownership before every HTTP attempt, including a
retry. Generation also checks current source/hash/snapshot before each send.
Known receipts and uncertain-attempt bounds are published cumulatively using a
bounded independent cleanup token, before output validation or caller cancellation
can discard them. A preflight with zero actual sends clears its unknown hold;
HTTP 400 after a send retains its known/uncertain charge. A blocked retry does not
charge the allowance of an attempt that was never sent.

All sentence-bearing paths recheck ownership and current source/snapshot before
composition, including the single-sentence path. Transient answer evidence retains
sentence hash, document hash, snapshot ID, route, visibility, source/corpus/chunker
versions and entity identity where supplied. No citation persistence is added here.

See [review round 2](review-round-2.md) for the verified checks and final approval.
