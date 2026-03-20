# TCT English - AI Skills System

A multi-IDE AI assistant configuration system for the TCT English platform.

Primary supported environments:
- Antigravity
- Codex
- GitHub Copilot / Visual Studio

Optional legacy surface:
- `.cursor/` if that directory exists in a future workspace

## Directory Map

```text
project-root/
|- AGENTS.md                      <- Universal agent instructions
|- .ai/                           <- Shared context, templates, prompts
|  |- README.md                   <- This file
|  |- context/                    <- Project reference documentation
|  |- templates/                  <- Reusable code boilerplate
|  `- prompts/                    <- Quick prompt snippets
|- .agent/                        <- Antigravity skills and workflows
|  |- skills/
|  `- workflows/
|- .github/
|  `- copilot-instructions.md     <- GitHub Copilot / Visual Studio instructions
`- docs/                          <- Architecture, structure, backlog, handoff docs
```

## IDE Compatibility

| Surface | Antigravity | Codex | GitHub Copilot |
|---|---|---|---|
| `AGENTS.md` | Yes | Yes | Yes |
| `.ai/context/` | Yes | Yes | Yes |
| `.agent/skills/` | Yes | Reference only | No |
| `.agent/workflows/` | Yes | Reference only | No |
| `.github/copilot-instructions.md` | Reference only | Reference only | Yes |
| `.cursor/` | Optional legacy | Optional legacy | Optional legacy |

## Recommended Read Order

1. `AGENTS.md`
2. `docs/project-structure.md`
3. `docs/architecture-prioritized-backlog.md`
4. `.ai/context/known-issues.md`
5. `.ai/context/coding-conventions.md`

## Version History

| Version | Date | Changes |
|---|---|---|
| v1.0 | 2024-01 | Early multi-rule setup |
| v2.0 | 2026-03 | Added shared context, templates, skills, workflows |
| v3.0 | 2026-03 | Added `AGENTS.md`, `.ai/`, `.agent/`, `.github/copilot-instructions.md` |
| v4.0 | 2026-03 | Post-refactor realignment of active AI guidance and historical labeling |

## Notes

- The canonical project namespace is `TCTEnglish.*`.
- Older `TCTVocabulary.*` references may still appear in legacy files and
  should be verified before reuse.
- Treat `docs/project-structure.md` as the canonical structure map.
- Treat `docs/architecture-prioritized-backlog.md` as the active backlog.
- Treat `docs/branch-handoff-architecture-hardening.md` as historical context.
