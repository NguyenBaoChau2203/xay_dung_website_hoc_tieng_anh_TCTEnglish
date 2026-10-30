# Writing local evaluation (L2)

Environment / configuration (do **not** put provider keys in `appsettings`):

| Key | Default | Meaning |
| --- | --- | --- |
| `Learning:Writing:EvaluationMode` | `Local` | Authoritative local policy decides pass/XP |
| `Learning:Writing:CloudEnhancementEnabled` | `false` | When `true`, optional AI feedback may run with a short timeout; in Local mode it does **not** change `passed` |
| `Learning:Writing:CloudEnhancementTimeoutSeconds` | `3` | Documented ceiling. The evaluate path enforces one 3-second budget for cloud enhancement, LanguageTool and Ollama together; it does not add a separate cloud timer |

Example (Development user-secrets or env):

```text
Learning__Writing__EvaluationMode=Local
Learning__Writing__CloudEnhancementEnabled=false
Learning__Writing__CloudEnhancementTimeoutSeconds=3
```

Does not change global `AI:*` binding or AI Chat.
