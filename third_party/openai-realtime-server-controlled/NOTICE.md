# OpenAI Realtime server-controlled example

Copyright (c) 2025 OpenAI. MIT License: see [LICENSE](LICENSE).
Source: https://github.com/openai/openai-agents-js/tree/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled

`provenance.json` records exact upstream SHA-256 hashes; `src` and `test` retain
unmodified selected reference files. These TypeScript files are reference material,
not application build inputs or a second server. No upstream dependencies, demo
authentication, CSS, tools, environment files or monorepo setup are imported.

| Upstream | TCT adaptation |
| --- | --- |
| sdpPolicy.ts | VoiceSdpPolicy.cs: C# validation, UTF-8 bound, stricter integer grammar. |
| realtimeCall.ts | OpenAiRealtimeVoiceClient.cs: multipart, private Location, durable callback before answer validation; no browser cancellation after dispatch. |
| sidebandReady.ts | VoiceSessionConfiguration.cs: exact requested effective config, including no tools, VAD and interruption; finite timeout. |
| publicEventProjection.ts / publicEvents.ts | VoicePublicEvent.cs: explicit application event allowlist; playback ends on output buffer events. |
| sessionManager.ts | AiVoiceSessionService/registry/cleanup: adapt ownership, cancellation and retained failed hangup patterns to durable MariaDB state. |
| audioOnlyWebRtc.ts | ai-voice-audio.js: vanilla JavaScript audio-only helper. |
| voiceSessionCoordinator.ts / closeRemoteSession.ts | ai-voice-session.js: vanilla coordinator, TCT antiforgery/API, bounded Stop retry. |
| matching tests | xUnit protocol/lifecycle and node:test browser behavior tests, fake transport only. |

New TCT domain entities, entitlement, quotas, public catalog adapter, transcript
persistence, account deletion, MVC endpoints and Clay UI are integration code.
No VAD/STT/TTS/codec implementation is supplied locally.
