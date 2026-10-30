# Optional Writing engines (L7)

Bound from `Learning:WritingEngines`. **Default off.** Core Local assessment (L2) does not require these.

| Key | Default | Notes |
| --- | --- | --- |
| `GrammarEnabled` | false | LanguageTool `/v2/check`, en-US, 2s |
| `LanguageToolBaseUrl` | empty | Internal only |
| `LocalLlmEnabled` | false | Ollama `/api/chat` |
| `OllamaBaseUrl` / `OllamaModel` | empty | Must be approved digest/quantization before enable |

Grammar findings never set `passed`. Missing engine → draft/attempt preserved; core Local still works.

Evaluate calls these adapters only as optional feedback inside one **3 second** budget that also covers cloud enhancement. A missing `matches` array or a non-allowlisted Ollama payload is unavailable/`invalid_schema`, not empty success or raw JSON. Default off means zero HTTP. Internal adapter timeout returns unavailable; caller cancellation propagates. Enabling config is **code-integrated**, not proof the engine is installed or that a 40-sentence corpus passed. Generation of bilingual exercises still uses the existing cloud provider and is outside this feedback path.
