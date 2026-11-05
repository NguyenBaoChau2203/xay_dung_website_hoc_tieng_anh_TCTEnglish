# Project prompt authoring standard

Owner request: execution prompts must be English and follow current professional
prompting practices. One agent implements/reviews; local guidance changes only.
Preserve the existing untracked team-design-system prompt. No Git/release stage
is granted by this task.

- [x] Inspect current rules/templates and review official OpenAI/Anthropic/Google
      guidance; distinguish provider recommendations from owner language policy.
- [x] Add concise English prompt rules to AGENTS.md for all four clients.
- [x] Add one canonical standard and one adaptable English execution template;
      link them from the routing guide and the active design-system prompt.
- [x] Check document links, language/scope consistency and diff; run required
      agent-tooling validator and Python tests. No application build is needed.
- [x] Prepare the Vietnamese handoff with exact results, files, local-only status
      and historical prompt handling.

Verification: agent_tooling.py passed. Python suite ran 135 tests: 134 passed,
1 platform-dependent test skipped, 0 failures (146.277 seconds). Document links,
English prompt prose, UTF-8, whitespace and credential-pattern review passed.
No native hooks, application code, database or deployment settings changed.
All work remains local on chore/prompt-authoring-standards; no commit/push/PR.

Owner update: the subsequent context-tooling request explicitly authorizes
commit/push/PR/remote CI/merge master for these changes. Track that stage in
2026-11-05-context-sync-and-publish.md; the local-only line above records the
previous handoff, not a restriction overriding the newer owner instruction.
