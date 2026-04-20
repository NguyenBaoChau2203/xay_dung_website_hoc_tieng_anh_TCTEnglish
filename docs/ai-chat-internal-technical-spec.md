# AI Chat Internal Technical Spec — Architecture Lock

> **Phase:** P02

> **Author:** Claude Opus (Antigravity)
> **Date:** 2026-04-13
> **Status:** FROZEN — coding phases must conform to this spec

---

## 1. Implementation Strategy Decision

### Decision: Strategy Pattern via `IAiProviderClient`

The internal pipeline will implement the existing `IAiProviderClient` interface.
This means:

- `AiController` → **no changes**
- `AiChatService` → **minimal changes** (pass `userId` to provider)
- `AiStreamingService` → **no changes**
- `AiObservabilityService` → **no changes**
- `AiConversationService` → **no changes**
- `AiConversationExecutionGuard` → **no changes**
- `AiContextBuilder` → **kept but role reduced** (internal provider may ignore its output); no code removal needed since the builder output is consumed but the internal provider only reads the user message from the messages list

### Interface modification (single breaking change)

```csharp
// IAiProviderClient.cs — FROZEN SIGNATURE
public interface IAiProviderClient
{
    Task<AiProviderReply> GenerateReplyAsync(
        int userId,                                     // ADDED
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct);
}
```

**Rationale:** The internal pipeline requires `userId` to query per-user data (sets, progress, classes). Adding it to the interface is the smallest possible change. `GeminiProviderClient` will ignore the parameter.

### `AiChatService` call-site change

Line 183 changes from:
```csharp
aiReply = await _providerClient.GenerateReplyAsync(contextResult.Messages, ct);
```
to:
```csharp
aiReply = await _providerClient.GenerateReplyAsync(userId, contextResult.Messages, ct);
```

This is the **only production code change** needed in existing files (besides `GeminiProviderClient` accepting the new param).

---

## 2. Frozen Interfaces

All new types live under `TCTEnglish.Services.AI.Internal` unless noted.

### 2.1 UserIntent Enum

```csharp
// TCTEnglish/Services/AI/Internal/UserIntent.cs
namespace TCTEnglish.Services.AI.Internal;

public enum UserIntent
{
    Greeting,
    MyVocabulary,
    MyProgress,
    CardLookup,
    SpeakingSuggestion,
    ClassInfo,
    WebsiteGuide,
    StudyRecommendation,
    OutOfScope
}
```

### 2.2 IntentClassification Record

```csharp
// TCTEnglish/Services/AI/Internal/IntentClassification.cs
namespace TCTEnglish.Services.AI.Internal;

public sealed record IntentClassification(
    UserIntent Intent,
    float Confidence,           // 0.0 → 1.0
    string ClassifierName);     // "keyword" or "mlnet"
```

### 2.3 IAiQueryClassifier

```csharp
// TCTEnglish/Services/AI/Internal/IAiQueryClassifier.cs
namespace TCTEnglish.Services.AI.Internal;

public interface IAiQueryClassifier
{
    IntentClassification Classify(string userMessage);
}
```

- Synchronous by design — keyword matching and ML.NET prediction are CPU-bound.
- P03/P04A will ship `DeterministicIntentClassifier` (keyword + regex).
- P05B will ship `MlNetIntentClassifier` as an alternative registration.

### 2.4 KnowledgeSnippet Record

```csharp
// TCTEnglish/Services/AI/Internal/KnowledgeSnippet.cs
namespace TCTEnglish.Services.AI.Internal;

public sealed record KnowledgeSnippet(
    string Title,
    string Body,
    string Source,
    string? Route = null,
    int Priority = 0);
```

### 2.5 IKnowledgeRetriever

```csharp
// TCTEnglish/Services/AI/Internal/IKnowledgeRetriever.cs
namespace TCTEnglish.Services.AI.Internal;

public interface IKnowledgeRetriever
{
    bool CanHandle(UserIntent intent);

    Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(
        int userId,
        string userMessage,
        CancellationToken ct);
}
```

- Each retriever handles one or more intents.
- `userId` is mandatory for anti-IDOR queries.
- All database queries must use `.AsNoTracking()` and `.Take(N)`.

### 2.6 IAnswerComposer

```csharp
// TCTEnglish/Services/AI/Internal/IAnswerComposer.cs
namespace TCTEnglish.Services.AI.Internal;

public interface IAnswerComposer
{
    Task<string> ComposeAsync(
        UserIntent intent,
        string userMessage,
        IReadOnlyList<KnowledgeSnippet> snippets,
        CancellationToken ct);
}
```

- Async by design for future extensibility.
- P04A `TemplateAnswerComposer` implementation will use templates from `docs/ai-chat-answer-style-reference.md`.

### 2.7 InternalKnowledgeProvider (orchestrator)

```csharp
// TCTEnglish/Services/AI/Internal/InternalKnowledgeProvider.cs
namespace TCTEnglish.Services.AI.Internal;

public sealed class InternalKnowledgeProvider : IAiProviderClient
{
    private readonly IAiQueryClassifier _classifier;
    private readonly IReadOnlyList<IKnowledgeRetriever> _retrievers;
    private readonly IAnswerComposer _composer;

    // Constructor injection via DI

    public async Task<AiProviderReply> GenerateReplyAsync(
        int userId,
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct)
    {
        // 1. Extract last user message from messages list
        // 2. Classify intent
        // 3. Apply confidence threshold (< 0.55 → OutOfScope)
        // 4. Find retriever(s) matching intent via CanHandle()
        // 5. Retrieve knowledge snippets
        // 6. Compose answer
        // 7. Return AiProviderReply with tokens = 0, model = "internal-{classifierName}"
    }
}
```

**Key design decisions:**
- Retrievers are injected as `IReadOnlyList<IKnowledgeRetriever>` (multi-registration via DI).
- The orchestrator dispatches to the first retriever whose `CanHandle()` returns `true`.
- Confidence threshold of 0.55 is hardcoded in the orchestrator, not in the classifier.
- `AiProviderReply` fields: `PromptTokens = 0`, `CompletionTokens = 0`, `TotalTokens = 0`, `Model = "internal-keyword"` or `"internal-mlnet"`.

---

## 3. File Ownership Per Phase

### P03 — Core Internal Provider Shell (GPT-5.3 Codex VS)

**Goal:** Create the `InternalKnowledgeProvider` shell so the app compiles, builds, and routes through the internal pipeline. Gemini is no longer the default DI registration.

| Action | File | Notes |
|---|---|---|
| MODIFY | `Services/AI/IAiProviderClient.cs` | Add `int userId` parameter |
| MODIFY | `Services/AI/GeminiProviderClient.cs` | Accept + ignore `userId` parameter |
| MODIFY | `Services/AI/AiChatService.cs` | Pass `userId` in GenerateReplyAsync call (1 line) |
| NEW | `Services/AI/Internal/UserIntent.cs` | Enum (frozen above) |
| NEW | `Services/AI/Internal/IntentClassification.cs` | Record (frozen above) |
| NEW | `Services/AI/Internal/IAiQueryClassifier.cs` | Interface (frozen above) |
| NEW | `Services/AI/Internal/KnowledgeSnippet.cs` | Record (frozen above) |
| NEW | `Services/AI/Internal/IKnowledgeRetriever.cs` | Interface (frozen above) |
| NEW | `Services/AI/Internal/IAnswerComposer.cs` | Interface (frozen above) |
| NEW | `Services/AI/Internal/InternalKnowledgeProvider.cs` | Orchestrator (skeleton — calls classifier, dispatches retriever, composes) |
| NEW | `Services/AI/Internal/StubIntentClassifier.cs` | Temp: always returns `Greeting` with `confidence=1.0` for compile pass |
| NEW | `Services/AI/Internal/StubAnswerComposer.cs` | Temp: returns greeting template for all intents |
| MODIFY | `Program.cs` | Register `InternalKnowledgeProvider` as `IAiProviderClient`, remove Gemini DI line, register stub classifier + composer + empty retriever list |
| MODIFY | `Tests/TestHelpers/StubAiProviderClient.cs` | Accept `userId` parameter in func signature |
| MODIFY | `Tests/AiPhase2ServiceTests.cs` | Update stub calls to pass `userId` |
| MODIFY | `Tests/GeminiProviderClientTests.cs` | Update to new interface signature |
| NEW | `Tests/InternalKnowledgeProviderTests.cs` | Basic tests: orchestrator routes to retriever, applies confidence threshold |

**Hard gate:** App builds. All existing tests pass. `/AI/Chat/Send` returns a greeting response.

---

### P04A — Deterministic Baseline Runtime (GPT-5.4 Codex App)

**Goal:** Replace stubs with a working deterministic classifier and template composer.

| Action | File | Notes |
|---|---|---|
| NEW | `Services/AI/Internal/DeterministicIntentClassifier.cs` | Keyword + regex classifier for all 9 intents |
| NEW | `Services/AI/Internal/TemplateAnswerComposer.cs` | Template-based answer builder per `docs/ai-chat-answer-style-reference.md` |
| DELETE | `Services/AI/Internal/StubIntentClassifier.cs` | Replaced by DeterministicIntentClassifier |
| DELETE | `Services/AI/Internal/StubAnswerComposer.cs` | Replaced by TemplateAnswerComposer |
| MODIFY | `Program.cs` | Update DI registrations for real classifier + composer |
| NEW | `Tests/DeterministicIntentClassifierTests.cs` | Per-intent keyword coverage tests (≥3 cases per intent) |
| NEW | `Tests/TemplateAnswerComposerTests.cs` | Verify each intent produces correct template shape |

**Hard gate:** All 9 intents produce correct template output. Greeting and OutOfScope work without a retriever.

---

### P04B — Domain Retrievers and Baseline Regression (GPT-5.3 Codex VS)

**Goal:** Implement the 6 retrievers and wire them into DI. Full baseline regression suite.

| Action | File | Notes |
|---|---|---|
| NEW | `Services/AI/Internal/Retrievers/UserVocabularyRetriever.cs` | EF query: user's sets + card counts |
| NEW | `Services/AI/Internal/Retrievers/LearningProgressRetriever.cs` | EF query: mastered/learning/new counts, streak, goals |
| NEW | `Services/AI/Internal/Retrievers/ClassRetriever.cs` | EF query: user's classes + role + member count |
| NEW | `Services/AI/Internal/Retrievers/SpeakingRetriever.cs` | EF query: speaking playlists/videos + user progress |
| NEW | `Services/AI/Internal/Retrievers/WebsiteGuideRetriever.cs` | Load `wwwroot/data/ai/website-guides.json`, fuzzy keyword match |
| NEW | `Services/AI/Internal/Retrievers/CardLookupRetriever.cs` | EF query: search cards by term in user's sets |
| MODIFY | `Program.cs` | Register all 6 retrievers as `IKnowledgeRetriever` |
| NEW | `Tests/AiBaselineRegressionTests.cs` | End-to-end tests: each intent with mock data, verify retriever selected + answer shape |
| NEW | `Tests/WebsiteGuideRetrieverTests.cs` | Fuzzy match accuracy tests against `website-guides.json` |

**Hard gate:** All in-scope scenarios produce correct answers. No-data-found paths work. Cross-user data never returned.

---

### P05A — ML.NET Prep and Approval Gate (GPT-5.3 Codex VS)

**Goal:** Prepare the ML.NET infrastructure. Gated on user approval for `.csproj` changes.

| Action | File | Notes |
|---|---|---|
| MODIFY | `TCTEnglish/TCTEnglish.csproj` | Add `<PackageReference Include="Microsoft.ML" Version="4.0.*" />` — **APPROVAL REQUIRED** |
| MODIFY | `TCTEnglish.Tests/TCTEnglish.Tests.csproj` | Add ML test dependency if needed — **APPROVAL REQUIRED** |
| NEW | `Services/AI/Internal/Data/intent-samples.seed.csv` | Already exists from P01A |
| NEW | `Services/AI/Internal/MlNetIntentClassifier.cs` | Load pre-trained `.zip` model, predict intent, return confidence |
| NEW | `Services/AI/Internal/Data/MlNetTrainingPipeline.cs` | Offline training utility (can be called from test) |
| NEW | `Tests/MlNetIntentClassifierTests.cs` | Verify model loads, verify all intents have ≥50 samples in dataset |

**Hard gate:** ML.NET package approved. Model trains successfully from seed CSV. MlNetIntentClassifier loads model and predicts sample intents.

**APPROVAL DEPENDENCY:** This phase MUST NOT proceed until the user explicitly approves adding `Microsoft.ML` to the `.csproj` files.

---

### P05B — ML.NET Runtime Integration (GPT-5.3 Codex VS)

**Goal:** Wire ML.NET classifier into production DI as the primary classifier.

| Action | File | Notes |
|---|---|---|
| MODIFY | `Program.cs` | Register `MlNetIntentClassifier` as `IAiQueryClassifier` (replaces `DeterministicIntentClassifier`) |
| NEW | `Services/AI/Internal/Data/intent-model.zip` | Pre-trained model artifact |
| MODIFY | `Services/AI/Internal/MlNetIntentClassifier.cs` | Add synonym normalization pre-processing |
| MODIFY | `Services/AI/Internal/InternalKnowledgeProvider.cs` | Ensure confidence < 0.55 → OutOfScope logic is active |
| NEW | `Tests/MlNetRuntimeIntegrationTests.cs` | End-to-end: ML.NET classifies → retriever → composer → answer |
| MODIFY | `Tests/AiBaselineRegressionTests.cs` | Add ML.NET classifier variant tests |

**Hard gate:** ML.NET path produces correct results for all intents. Confidence threshold blocks out-of-scope queries. App startup time < 5s with model loading.

---

## 4. Test Ownership Per Phase

| Phase | Test file(s) | Minimum test count |
|---|---|---|
| P03 | `InternalKnowledgeProviderTests.cs`, updated `AiPhase2ServiceTests.cs`, updated `GeminiProviderClientTests.cs` | 5 new + all existing pass |
| P04A | `DeterministicIntentClassifierTests.cs`, `TemplateAnswerComposerTests.cs` | 3 per intent × 9 intents = 27 classifier + 9 composer = 36 |
| P04B | `AiBaselineRegressionTests.cs`, `WebsiteGuideRetrieverTests.cs` | 9 intent scenarios + 6 no-data scenarios + 2 anti-IDOR = 17 |
| P05A | `MlNetIntentClassifierTests.cs` | 9 per-intent + 1 dataset coverage + 1 model load = 11 |
| P05B | `MlNetRuntimeIntegrationTests.cs` | 9 end-to-end + 2 threshold = 11 |

---

## 5. ML.NET Approval Gate

ML.NET integration is **blocked** until the user explicitly approves:

1. Adding `<PackageReference Include="Microsoft.ML" Version="4.0.*" />` to `TCTEnglish.csproj`.
2. Adding any needed ML.NET test packages to `TCTEnglish.Tests.csproj`.

P03 and P04A/P04B **do not** require ML.NET. They use the deterministic keyword classifier. This means the app will be fully functional (with keyword-based intent classification) even if ML.NET is never approved.

P05A must begin by requesting approval. If denied, P05A marks itself as "blocked-approved" and P06 proceeds with the deterministic classifier as final.

---

## 6. Files Shared Across Phases (Conflict Risk)

| File | Phases that write | Mitigation |
|---|---|---|
| `Program.cs` | P03, P04B, P05A, P05B | Each phase appends DI lines only. P03 sets the initial structure. Later phases add registrations below. |
| `InternalKnowledgeProvider.cs` | P03 (skeleton), P05B (threshold tuning) | P03 writes the final orchestration logic. P05B only modifies the confidence check constant. |
| `AiBaselineRegressionTests.cs` | P04B (create), P05B (add ML variant) | P04B creates. P05B adds new test methods, does not modify existing ones. |

All other files have single-phase ownership — no conflict risk.

---

## 7. Directory Structure (Frozen)

```text
TCTEnglish/Services/AI/
├── AiChatService.cs                    (P03: 1-line edit)
├── IAiProviderClient.cs                (P03: add userId param)
├── GeminiProviderClient.cs             (P03: accept userId, ignore)
├── Internal/
│   ├── UserIntent.cs                   (P03) [FROZEN]
│   ├── IntentClassification.cs         (P03) [FROZEN]
│   ├── IAiQueryClassifier.cs           (P03) [FROZEN]
│   ├── KnowledgeSnippet.cs             (P03) [FROZEN]
│   ├── IKnowledgeRetriever.cs          (P03) [FROZEN]
│   ├── IAnswerComposer.cs              (P03) [FROZEN]
│   ├── InternalKnowledgeProvider.cs    (P03)
│   ├── DeterministicIntentClassifier.cs(P04A)
│   ├── TemplateAnswerComposer.cs       (P04A)
│   ├── MlNetIntentClassifier.cs        (P05A)
│   ├── Retrievers/
│   │   ├── UserVocabularyRetriever.cs  (P04B)
│   │   ├── LearningProgressRetriever.cs(P04B)
│   │   ├── ClassRetriever.cs           (P04B)
│   │   ├── SpeakingRetriever.cs        (P04B)
│   │   ├── WebsiteGuideRetriever.cs    (P04B)
│   │   └── CardLookupRetriever.cs      (P04B)
│   └── Data/
│       ├── intent-samples.seed.csv     (P01A — already exists)
│       ├── MlNetTrainingPipeline.cs    (P05A)
│       └── intent-model.zip            (P05B — generated artifact)
└── (all other existing files untouched)

TCTEnglish.Tests/
├── InternalKnowledgeProviderTests.cs   (P03)
├── DeterministicIntentClassifierTests.cs(P04A)
├── TemplateAnswerComposerTests.cs      (P04A)
├── AiBaselineRegressionTests.cs        (P04B, P05B adds variants)
├── WebsiteGuideRetrieverTests.cs       (P04B)
├── MlNetIntentClassifierTests.cs       (P05A)
├── MlNetRuntimeIntegrationTests.cs     (P05B)
└── TestHelpers/
    └── StubAiProviderClient.cs         (P03: update signature)
```

---

## 8. Security Invariants for All Coding Phases

1. Every retriever query MUST include `.Where(x => x.UserId == userId)` — anti-IDOR.
2. Every retriever query MUST use `.AsNoTracking()` — read-only.
3. Every retriever query MUST use `.Take(N)` — prevent data dump (max N=10 for lists, N=5 for display).
4. `InternalKnowledgeProvider` MUST extract `userId` from the method parameter, NEVER from `messages`.
5. `AiChatService` ownership check on conversation remains unchanged.
6. No new controller endpoints needed — all traffic flows through existing `/AI/Chat/Send`.
7. No new views or partial views needed.

---

## 9. Streaming Compatibility Confirmation

`AiStreamingService` works by:
1. Calling `_chatService.SendAsync()` → receives full reply text
2. Splitting text into 90-char chunks
3. Emitting chunks via SignalR with delays

The internal pipeline returns a complete answer text synchronously (much faster than Gemini), so the streaming simulation will work **identically** with no changes required. The `AiStreamingService` file ownership is **none** — no phase touches it.

---

## 10. Backward Compatibility

- `GeminiProviderClient.cs` is **kept** but not registered in DI by default (after P03).
- To switch back to Gemini, change one line in `Program.cs`: register `GeminiProviderClient` as `IAiProviderClient`.
- All existing conversation history, messages, and request logs remain valid.
- Token fields in `AiMessage` and `AiRequestLog` will contain `0` for internal pipeline responses — this is acceptable and does not require schema changes.
- `AiOptions` fields related to Gemini (`ApiKey`, `BaseUrl`, `Model`, etc.) become unused but are preserved for backward compatibility.
