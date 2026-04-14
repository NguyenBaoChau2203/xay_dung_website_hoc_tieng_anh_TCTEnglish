# Phase 03 - Internal Provider Shell

**Assigned model:** GPT-5.3 Codex  
**Platform:** Visual Studio  
**Phase type:** Core implementation, compile-safe refactor, minimal viable shell

## Mission

Create the compile-safe internal runtime shell so the chat feature no longer depends on Gemini as the default runtime answer path.

## Area Hint

`TCTEnglish/Services/AI`, `TCTEnglish/Controllers/AiController.cs`, `TCTEnglish/Program.cs`, `TCTEnglish.Tests/*AI*`

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/feature-scaffold/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/new-feature-flow.md`

Also read the completed P00-P02 logs in `docs/implementation_plan.md`.

## In Scope

1. Create the internal AI shell under the correct AI service boundary.
2. Add the core contracts and placeholder implementations required by the approved architecture.
3. Make the minimal runtime refactor needed so the default path is internal rather than Gemini.
4. Keep `AiController`, chat DTOs, conversation persistence, and streaming contracts stable if possible.
5. Update affected AI tests so the solution builds and targeted tests can run.
6. Update `docs/implementation_plan.md` status board and P03 execution log.

## Likely File Targets

1. `TCTEnglish/Services/AI/AiChatService.cs`
2. `TCTEnglish/Services/AI/IAiProviderClient.cs` or replacement abstraction if P02 locked a different design
3. `TCTEnglish/Services/AI/GeminiProviderClient.cs`
4. `TCTEnglish/Services/AI/Internal/*`
5. `TCTEnglish/Program.cs` for minimal DI changes only
6. `TCTEnglish.Tests/*AI*`

## Explicit Allowance

This phase may make the minimal `Program.cs` DI registration change required to wire the internal runtime shell.

Do not make any unrelated startup/configuration edits.

## Required Behavior At End Of Phase

1. The app should compile.
2. The AI feature should have an internal default runtime path, even if the behavior is still minimal.
3. Gemini code may remain in the repo, but it must no longer be the main runtime dependency for this feature.
4. Placeholder responses must still stay inside the approved website-grounded behavior.

## Out of Scope

1. Full retriever implementation.
2. Full ML.NET integration.
3. `.csproj` edits unless the user explicitly approves them in this run.
4. New UI screens.

## Verification

Run the most relevant compile/test checks available for this phase.

At minimum try to verify:

1. solution or project build
2. targeted AI tests
3. no obvious compile regressions in the AI feature slice

## Exit Gate

Do not mark this phase complete unless:

1. The solution builds or the remaining compile blocker is clearly recorded.
2. Default DI/runtime path is internal.
3. No external Gemini call is required for the main AI chat path.
4. `docs/implementation_plan.md` records the exact remaining work for P04A.

## End-of-Phase Rule

Stop after P03. Do not start P04A in the same run.
