# AI voice PR and master merge

Owner authorization: commit all remaining changes, create a PR and merge into
master. Push/remote CI and normal PR integration are authorized for this stage.
One agent. Production deployment is not authorized; never enable its gate.

- [x] Review/verify remaining floating-companion UI changes; prepare their commit.
- [ ] Integrate current origin/master, preserving the Listening work and all
  voice/UI evidence. Resolve conflicts without deleting either feature.
- [ ] Run focused application/Node checks, model alignment and shared package
  validation on the combined state. Existing schema proof remains in the voice
  implementation ledger; no new persisted-model change is planned.
- [ ] Push the feature branch and create a reviewable PR targeting master.
- [ ] Follow every PR check, fix relevant failures and verify exact final head.
- [ ] Recheck production gate=false and merge through the PR with head-SHA guard.
- [ ] Inspect master CI and gated staging/production jobs, synchronize the local
  checkout using the reviewed shared-data workflow when pulling, and report
  PR/merge SHA, evidence, gate outcomes and remaining limits in Vietnamese.

Initial facts: branch feature/ai-voice-conversation, five existing local commits
through eece67a; five uncommitted companion UI/evidence files. origin/master is
7fff8da with the newer Listening redesign. Production gate=false, staging=true.
The existing staging pipeline may run after a successful master build; no gate
or hosting configuration will be changed by this task.

Companion review: two new behavior regressions reproduced standalone visibility
and reduced-motion scrolling defects. Clamp the container threshold to the viewport,
respect prefers-reduced-motion, reuse Clay/semantic tokens and a44px scroll button.
Keep one primary live status announcement. Node35/35 and focused .NET13/13 pass;
existing compiler warnings are unrelated. No schema or protected-config change.
Chrome9/9 full/floating/standalone checks also pass with0 page errors and9 fake
creates/closes. Companion visibility matches whether the hero is off-screen;
five cases exercise the visible companion and keyboard return under reduced
motion, including standalone and floating375px. No real provider requests.
