# Account review and repair — 2026-11-11

Mode: IMPLEMENT_LOCAL, one agent. The owner returned after Cursor execution and
explicitly requested review and fixes. Carry forward the original Account plan
and its narrow protected-file scope; no Git/release or real-delivery authority.

Baseline: fix/staging-uat-polish at c908f66, 73 changed/untracked files fingerprinted
in ignored .appdata/local/account-review-20261111/baseline.json. Preserve all UI,
loader, BTH3 and other pre-existing work. Initial targeted Account tests: 29 passed.
The execution ledger does not establish MariaDB/browser/provider acceptance.

## Findings and ordered repair

- [x] R1 Security: atomically consume proof/challenge and serialize account changes;
  reject stale password state, revoke stale security versions on HTTP/hub paths,
  and provide current-address verification for weekly email without changing email.
- [x] R2 Activity: remove zero-elapsed time credit; serialize per-user acceptance,
  prevent duplicate/overlapping multi-tab intervals; repair idle/hidden/pending-start
  JS lifecycle and test production script. Preserve legacy reporting compatibility.
- [x] R3 Habits/history: inspect clipping at day/hour/window boundaries and goal
  baseline/revision concurrency; expose working 7/28-day selection and honest coverage.
- [x] R4 Push privacy: reject foreign subscription takeover; validate encryption
  material; bind notification display to the currently authenticated account and
  fix permission/registration/unsubscribe/error paths in the real client.
- [x] R5 Delivery: recover expired leases without blind resend, fence settlement,
  recheck daily opt-out/goals/expiry immediately before dispatch, isolate transport
  failures, bound scheduling and make weekly period/link semantics deterministic.
- [x] R6 Deletion: run the full FK/privacy/provider assessment, preserve shared
  consumers and last-admin safety, support retrying execution strategies, and
  accurately record file-cleanup failures rather than marking exhausted work done.
- [x] R7 Provider/UI: review the existing migration before applying it; prove
  disposable MariaDB upgrade/repeat/preservation and meaningful concurrency; run
  build/regressions, schema pending-model check, dependency audit and browser flows.
- [x] R8 Closeout: append bug evidence, correct execution ledger/known issues and
  original checklist statuses; leave local running the reviewed version when ready.

Each source-derived finding must have a meaningful reproduction where practical;
write failing regressions before fixes when adding tests. Keep exact evidence in
the execution ledger. Missing credentials/live channel authority remains a stated
provider-live limit; do not fabricate success, send real users messages, reset DBs,
or change secrets/configuration to obtain a pass.

## Review outcome and outstanding acceptance

Local repairs are complete; see the execution ledger for measured coverage.
Final .NET:1,247 passed,0 failed,28 environment-dependent skips. Node182/182.
Separate MariaDB10.5.23 run:9/9. Upgrade/repeat/data preservation and final
pending-model checks passed. Local web remains running the reviewed version.

- [ ] P2 Native200% browser zoom: inspect Account Profile/Settings, dialogs,
  keyboard and focus at actual browser zoom. Device-emulation shortcuts did not
  establish native zoom; a CSS probe is not a pass. Touch scoped account-ui.css
  only if an actual issue is reproduced; recheck1440/768/390 after any repair.
- [ ] P2 Live Push and weekly mail: use configured canonical URL, SMTP and VAPID
  through the supported secret store, then a dedicated consenting test account.
  Verify actual device/mailbox receipt, opt-out, logout/account switching and
  worker restart; preserve Unknown outcomes until a receipt is investigated.
  No real outbound message or secret change is authorized by this document.
  Follow-up11/11: the owner separately authorized one self-addressed SMTP test.
  Production sender/transport returned Sent; matching Gmail INBOX receipt was
  verified with marker TCT-SMTP-20261111-8c20ac037e. Credential/config was not saved.
  Complete weekly workflow and Web Push acceptance remain pending.
- [ ] P2 Existing dependencies: assess AngleSharp1.4.0
  GHSA-pgww-w46g-26qg (fixed1.5.0) and MySqlConnector2.4.0
  GHSA-473q-m89c-ghf8 (fixed2.6.1). Source scope: dependency resolution from
  TCTEnglish/TCTEnglish.csproj, sanitizer consumers and MariaDB provider paths.
  Obtain explicit package-maintenance authority before changing protected pins;
  retain net9/EF9/Pomelo9/MariaDB10.5 boundaries. Verify sanitizer adversarial input,
  provider compatibility, full regressions and a fresh vulnerability audit. Do not
  rewrite host credentials/TLS settings, migrations or existing data to get a pass.
  The earlier minimum AngleSharp1.5.0 suggestion is superseded by the focused
  maintenance plan: a later maintainer advisory also affects1.5.0. Proposed checked
  stable targets are AngleSharp1.10.1 and MySqlConnector2.6.2, with framework/provider
  versions retained. See2026-11-11-dependency-security-maintenance.md in this directory;
  the plan does not grant protected-package changes.

## Short client continuation

```text
Read docs/superpowers/plans/2026-11-11-account-review-repair.md and the Account
execution ledger. Local repairs are already implemented. Review the current diff
and continue only the outstanding acceptance items that I explicitly authorize.
Use one agent, preserve existing work and protected configuration, verify changed
behavior, and report results and remaining limits in Vietnamese.
```
