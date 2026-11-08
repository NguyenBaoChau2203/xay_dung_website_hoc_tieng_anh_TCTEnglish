# Phase 7 local completion round 2

## Owner authority and outcome

The owner requests direct repairs, a running local web and sample questions for
manual testing. Execute inline as one agent. Repair confirmed defects and verify
the actual local application before handoff. No paid text/embedding/speech call,
commit/push/CI/deployment, owner-data reset or protected-file change is granted.
Keep live evaluation separately BLOCKED until a concrete grant and real evidence.

Input163-entry fingerprint:
`41386db1939220cc3d9f7e3f31bf5e00ac48425a81a6896572a92101c7bd9a35`.
Predecessor155-entry fingerprint:
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`.
Both were verified before edits on feature/hybrid-rag-chatbot, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`.

## Ordered checklist

- [x] Review input identities, provider clock, run guard and tests; retain submitted
      evidence and distinguish supported claims from missing runtime checks.
- [x] Reproduce run-guard deadline/admission/publication defects with meaningful
      red tests, then fix the default monotonic clock and duration enforcement.
- [x] Wire run admission/usage to actual fake HTTP requests made by production
      clients, retain unknown usage and verify aggregate caps across stages/retries.
      Guard code must not be an unconnected integer-counter simulation.
- [x] Review fixture-versus-local protocol and envelope. Correct metadata/stage
      claims and explicitly leave unmeasured live coverage pending. Preserve frozen
      revision2 labels/split and quota/role contracts.
- [x] Restore available local Docker runtime using hidden/background launch and
      preserve existing workloads. Use the required start-local.ps1 launcher on a
      free port. Saved secrets remain in their supported store, never printed.
- [x] Run real nonpaid embed-dry-run against serving MariaDB and compare snapshot;
      do not build a paid vector index. Record readiness and speech metadata.
- [x] Verify browser full/embed, source/history and representative deterministic
      questions against current local data. Observe no automated paid calls.
- [x] Reproduce and repair the complete Vietnamese definition frame: seven new
      answer/evidence regressions failed first; preserve complete multiword terms
      and negative controls for partial targets.
- [x] Repair the browser-observed example lookup being overridden as Greeting by
      the shipped ML classifier. Keep explicit lookup framing authoritative while
      leaving bare ambiguous text eligible for model classification.
- [x] Correct the login Bootstrap stylesheet SRI after a real blocked-resource
      browser error; verify the downloaded stylesheet loads with integrity enabled.
- [x] Run relevant provider/budget/access regressions, Node chat tests, build and
      final full app suite; review diff and update actual bug evidence.
- [x] Leave the owner-requested local web running. Deliver sample questions with
      expected behavior and separate dense/live limitations; refresh manifest,
      handoff and review state. Do not claim every possible answer or live quality
      verified from passing local checks.

## Final local evidence

Final app suite: 1,161 passed / 22 opt-in skipped / 0 failed, total 1,183,
Phase7LocalCompletion, full-classifier-final.trx. Focused 109/109; Node14/14.
The shipped-classifier regression was added after the focused run and is included
in the final full suite. No test/build success overrides missing live evidence.
The standard Release launcher built successfully, migration repeat applied none,
MariaDB dry-run matched snapshot, and EF pending-model changes is false.

Owner-facing server is http://localhost:5199, PID23344 at handoff. It uses existing
User Secrets, gpt-6-luna/low and configured gpt-transcribe. No saved key was
rewritten. Browser is logged into the fresh private manual account, empty chat,
with no automated question sent after restoring normal provider configuration.
The final loopback counter before shutdown is 0 POST / 0 paid calls. Live/dense
acceptance remains BLOCKED. See the local-completion handoff for final identity.
