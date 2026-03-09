---
name: Commit Message
description: Generate conventional commit messages following TCT English scopes and conventions
---

# Commit Message Generator

## When to Use
Use when generating git commit messages for TCT English changes.

## Format
```
<type>(<scope>): <short description ≤72 chars>

[optional body — what and why]
[optional footer — breaking changes, closes issues]
```

## Types
| Type | When |
|------|------|
| `feat` | New feature |
| `fix` | Bug fix |
| `refactor` | No behavior change |
| `perf` | Performance improvement |
| `security` | Security fix |
| `db` | Migration/schema change |
| `seed` | Seeder changes |
| `docs` | Documentation |
| `chore` | Build/tooling |

## TCT English Scopes
| Scope | Area |
|-------|------|
| `auth` | Login, register, OAuth, password reset |
| `vocab` | Sets, cards, flashcard modes |
| `speaking` | Videos, shadowing, dictation |
| `class` | Classroom, SignalR chat |
| `goals` | Daily goals, streaks |
| `admin` | Admin panel, user/video management |
| `api` | API endpoints |
| `ui` | Razor views, Bootstrap, CSS |
| `db` | Entities, migrations |
| `config` | Program.cs, appsettings |

## Rules
- Short description ≤72 characters, imperative mood ("add" not "added")
- Body explains WHY (not what — the diff shows what)
- Breaking changes in footer: `BREAKING CHANGE:`
- Issue refs: `Closes #N` or `Fixes #N`
