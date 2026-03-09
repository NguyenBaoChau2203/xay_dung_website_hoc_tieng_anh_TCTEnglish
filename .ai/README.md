# TCT English — AI Skills System

A multi-IDE AI assistant configuration system for the **TCT English EdTech Platform**.
Compatible with: **Cursor**, **Antigravity**, **VS Code + Codex**, **Visual Studio 2026 + Copilot**.

---

## Directory Map

```
project-root/
├── AGENTS.md                     ← Universal agent instructions (all IDEs read this)
│
├── .ai/                          ← Shared context & templates (IDE-agnostic)
│   ├── README.md                 ← This file
│   ├── context/                  ← Project reference documentation
│   │   ├── project-overview.md   → Stack, entities, features, file structure
│   │   ├── coding-conventions.md → Naming, architecture, EF patterns
│   │   ├── known-issues.md       → Known bugs, tech debt, warnings
│   │   └── domain-glossary.md    → EdTech & TCT-specific terminology
│   ├── templates/                ← Reusable code boilerplate
│   │   ├── controller.cs.md      → Controller with full CRUD actions
│   │   ├── service.cs.md         → Service interface + implementation
│   │   ├── viewmodel.cs.md       → ViewModel with Data Annotations
│   │   ├── razor-view.cshtml.md  → Razor view with Bootstrap 5
│   │   └── ef-entity.cs.md       → EF Core entity with DbContext config
│   └── prompts/                  ← Reusable prompt snippets
│       ├── security-checklist.md → OWASP Top 10 checklist for TCT English
│       └── code-style.md         → Quick code style reference
│
├── .agent/                       ← Antigravity IDE skills & workflows
│   ├── skills/                   ← SKILL.md format (on-demand)
│   │   ├── feature-scaffold/SKILL.md
│   │   ├── ef-migration/SKILL.md
│   │   ├── security-audit/SKILL.md
│   │   ├── data-seeder/SKILL.md
│   │   ├── api-endpoint/SKILL.md
│   │   ├── speaking-feature/SKILL.md
│   │   ├── vocabulary-feature/SKILL.md
│   │   ├── admin-panel/SKILL.md
│   │   ├── ui-component/SKILL.md
│   │   └── commit-message/SKILL.md
│   └── workflows/                ← Multi-step process guides
│       ├── new-feature-flow.md
│       ├── bug-investigation.md
│       ├── code-review-flow.md
│       ├── db-migration-flow.md
│       └── performance-audit.md
│
├── .cursor/                      ← Cursor IDE specific
│   └── rules/                    ← Auto-triggered .mdc rules
│       ├── core-dev.mdc, debug.mdc, optimize.mdc
│       ├── refactor.mdc, release.mdc, review.mdc, test.mdc
│       └── (skills/, workflows/, etc. — legacy, see .agent/)
│
└── .github/
    └── copilot-instructions.md   ← VS Code / VS 2026 Copilot instructions
```

## IDE Compatibility

| Feature | Cursor | Antigravity | VS Code + Codex | VS 2026 |
|---------|--------|-------------|-----------------|---------|
| `AGENTS.md` | ✅ | ✅ | ✅ | ✅ (via Copilot) |
| `.ai/context/` | ✅ ref | ✅ ref | ✅ ref | ✅ ref |
| `.agent/skills/` | — | ✅ native | — | — |
| `.agent/workflows/` | — | ✅ native | — | — |
| `.cursor/rules/` | ✅ native | — | — | — |
| `.github/copilot-instructions.md` | — | — | ✅ native | ✅ native |

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2024-01 | 7 Cursor Rules (core-dev, debug, optimize, refactor, release, review, test) |
| v2.0 | 2026-03 | Full AI Skills System: +10 skills, +5 workflows, +5 agents, +4 context, +5 templates |
| v3.0 | 2026-03 | Multi-IDE restructure: AGENTS.md, .ai/, .agent/, .github/copilot-instructions.md |

---

*Inspired by: [skills.sh](https://skills.sh) • [cursor.directory](https://cursor.directory) • [agents.md](https://agents.md) • Antigravity Skills*
