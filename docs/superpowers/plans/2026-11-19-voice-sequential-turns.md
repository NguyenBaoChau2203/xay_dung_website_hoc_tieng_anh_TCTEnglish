# Sequential AI voice turns

Owner update: 2026-11-19. Local implementation on feature/ai-voice-conversation,
one agent; no commit, push, PR or deployment. This supersedes the earlier
barge-in requirement. Preserve existing work, protected configuration and data.

The owner reproduced background noise interrupting AI playback and requested
that AI finish before accepting the learner's next turn.

- [x] Reproduce with focused offline regressions: provider interruption disabled,
  noise does not change playback/history state, microphone gating preserves manual
  mute, generation completion does not reopen input before playback completion.
- [x] Set server VAD interrupt_response=false while retaining automatic responses.
  Gate the existing WebRTC microphone during setup/thinking/speaking; reopen on
  listening/idle snapshots, preserving user mute. Keep Stop/cleanup unchanged.
- [x] Ignore speech activity during tracked playback as interruption evidence;
  retain actual cleared/truncated/cancelled/Stop markers and old private history.
- [x] Update shared inline/standalone guidance and operating documentation.
- [x] Run focused .NET and Node checks, review the delta, restart only the owned
  local web through scripts/start-local.ps1, confirm readiness, and verify full,
  floating and standalone UI across desktop/tablet/mobile with fake provider/media.

No schema, API route, package, protected configuration or quota change. No paid
provider request is required for this phase. Real microphone/noise acceptance is
reported separately from synthetic tests.

Official API reference checked: interrupt_response=false lets the response
continue despite VAD start. output_audio_buffer.stopped follows response.done
when the server output buffer drains. The public SSE snapshot polls every700ms;
provider-side interruption protection does not depend on that browser delay.

- https://developers.openai.com/api/reference/resources/realtime/client-events
- https://developers.openai.com/api/reference/resources/realtime/server-events

## Evidence

- Before implementation, new Node regressions failed3/22 (missing independent
  input gate); focused .NET regressions failed2/2 (interrupt_response=true and
  noise replaced speaking/interrupted history). Existing actual-interruption
  dedupe test now uses output_audio_buffer.cleared instead of speech activity.
- Final .NET44/44 pass,0 skips: AiVoiceRealtimeProtocolTests,
  AiVoiceSessionLifecycleTests, AiVoiceApiSecurityTests, AiVoiceInlineRenderTests.
  Build succeeds with existing unrelated repository warnings; no new voice warning.
- Node26/26 pass: audio, coordinator and chat-mode suites. Syntax checks for all
  three edited JS files and git diff --check pass.
- The owned Release web at5107 was verified by executable path before restarting
  through scripts/start-local.ps1 -NoBrowser. MariaDB healthy, committed schema
  already current (no migration applied), /health/ready HTTP200/ready. Prior
  child-only voice allowances retained; no secret/config/protected-file edit.
- Chrome scripted checks9/9,0 page errors: full chat1440 sidebar open/collapsed,
  768/390/375, standalone1440/375 and floating1440/375. Setup input blocked;
  listening opens it; thinking/speaking and generation-done keep it closed;
  manual mute survives the next listening snapshot; unmute cannot bypass the
  speaking gate; keyboard mute, Stop release and horizontal fit verified.
  Nine intercepted fake creates/closes, zero real provider requests.
- Ignored evidence: .appdata/local/voice-sequential-{red-dotnet,green-dotnet,browser}.log,
  .appdata/local/voice-sequential-local-web.log and
  .appdata/local/ai-voice-tests/sequential-{full-390,floating-1440}.png.
  Floating screenshot inspected directly. Native200% zoom, physical microphone,
  room-noise listening and live OpenAI ACK for this new policy remain unobserved.
- No schema/persisted model, API contract, quota, dependency or protected config
  change; old history and actual Stop/cleared/truncated/cancelled markers remain.
  No commit/push/PR/CI/deployment.
