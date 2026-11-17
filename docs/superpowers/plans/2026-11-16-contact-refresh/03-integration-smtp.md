# Step 03: Integrated repair and authorized real SMTP smoke test

Executor: GPT 6.1 Sol High. Mode when invoked: `IMPLEMENT_LOCAL` plus the specific
real Contact SMTP test authorized in [README](README.md). This is review AND fix
authority for Contact integration; do not stop at another repair plan for bugs
within this scope. Preserve Gemini's approved design and unrelated work.

Execution checkpoint 2026-11-17: local integration repairs, Release build,
125 focused .NET passes (2 skips), 11 Node passes and Chrome viewport/keyboard
checks are complete. The one actual form smoke POST returned 503/Unavailable
before transport: zero real SMTP dispatches due to missing saved sender/password.
Subsequent owner-authorized credential configuration resolved that blocker:
one actual form send returned SMTP Accepted and the owner confirmed Gmail Inbox
arrival. No automatic retry or second mail; native 200% zoom and protected shared
footer cleanup remain outside verified scope.
See [the evidence and dispatch ledger](../../../feature-audits/2026-11-16-contact-refresh/03-integration-smtp-ledger.md).

## Review and fix before live mail

- [ ] Read the actual backend/UI ledgers and inspect the complete Contact diff
  since their recorded baselines. Recheck current branch and app HEAD/working
  tree; remain on the same branch. Use current source, not the earlier plan's
  proposed symbols, as the implemented contract.
- [ ] Follow the GET -> rendered form -> antiforgery/validation -> service ->
  configured recipient -> real transport -> result/render/redirect path. Check
  guest, authenticated visitor and blocked-account appeal entry points.
- [ ] Reproduce and fix concrete Contact issues with focused regressions.
  Confirm field limits/names/subject keys, nonce/replay/throttle semantics,
  no raw HTML or header injection, no lost message on failure and no duplicate
  send from double-click/refresh/back. Keep unknown delivery conservative.
- [ ] Verify public information has a legitimate source; no test Gmail/phone
  placeholder, invented office hours or 24-hour guarantee became public copy.
- [ ] Run the Release build, relevant .NET/JS tests and available browser
  acceptance. Summarize exact results, environment skips and any unverified
  process/restart boundary. No schema change means no empty migration/provider
  run; model changes must follow the established MariaDB migration workflow.

## Real SMTP procedure

1. **Use existing local settings securely.** Read configuration presence and
   resolved nonsecret routing without dumping User Secrets or environment.
   Retrieve SMTP credentials only through their supported existing store/API;
   never echo them, place them in argv, write them into prompts/logs/Git or
   copy the old app password from chat. Historical success is not a configuration
   source. If missing/rejected, report a secure local-configuration blocker;
   do not ask for a password pasted into chat or weaken credential protection.
2. **Control destination for this test only.** The only authorized recipient is
   `baochau1512v@gmail.com`. Preserve the configured sender identity. If the
   current SupportEmail differs, use a reversible child-process override
   `SmtpSettings__SupportEmail=baochau1512v@gmail.com` for the owned local test
   host, then restore the caller environment. Never change tracked appsettings,
   public-contact metadata or another running process to redirect delivery.
   Verify there is one To and no Cc/Bcc; no visitor auto-reply.
3. **Run the current code.** If needed start with
   `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` and check
   `/health/ready`. Preserve existing listeners. For a separate owned instance,
   use `-Configuration Release -WebPort <verified-free-port>` and explicitly
   identify that URL. Do not kill an unrelated server or mistake an older build
   for the final implementation. Startup does not authorize speech/AI calls.
4. **Submit the completed Contact form.** Use Chrome DevTools MCP if available,
   or the available browser. Fill synthetic name/message and the owner-approved
   Gmail as Reply-To; choose a real subject key. Include a unique opaque marker
   such as `TCT-CONTACT-YYYYMMDD-<random>` using the actual test date, a Vietnamese
   paragraph and line break, and label the message as a synthetic local test.
   Include no account proof, password or genuine learner data. Submit once with
   the real antiforgery/nonce contract and production service/SMTP transport.
   An isolated SendMail helper is insufficient evidence of form integration.
5. **Separate acceptance levels.** Record the app outcome, test marker, UTC and
   Asia/Bangkok send time, elapsed time and transport acknowledgment category.
   `Sent` means SMTP accepted the message, not proof of Gmail Inbox placement,
   human reading or visual correctness. Ask the owner to look up the marker in
   Inbox/Spam and confirm appearance. Do not read private Gmail contents or
   request Gmail credentials to manufacture receipt evidence. If no mailbox
   evidence is available, report Gmail arrival as pending, not passed.
6. **Avoid duplicate real sends.** Default one message. At most two total under
   this work pack when a specific corrected issue justifies the second. Count
   all actual dispatch attempts, including unknown outcomes; do not reset the
   budget between chats or recreate the same request on another host. Persist
   the nonsecret attempt marker/outcome in the ledger. Never automatically
   resend after timeout, connection loss or an unknown acknowledgment. All
   further failure/race/refresh tests use fake transport.
7. **Clean up test routing.** Restore transient destination overrides and stop
   only a temporary instance you own when appropriate. Keep the owner's normal
   local test server available if it was already running. No secret-store
   rewrite, DB reset, contact shared-data export or hosting change.

## Completion evidence

Write `docs/feature-audits/2026-11-16-contact-refresh/03-integration-smtp-ledger.md`
with the actual contract, fixes/files, build/test/browser matrix, real attempt
count/marker/result, cleanup and unresolved checks. Include only redacted metadata;
mailbox screenshots, recipient lists, headers and raw form messages stay out of Git.
Update actual fixed bug-memory entries. Report in Vietnamese, clearly separating:

- local backend/HTTP acceptance;
- UI and accessibility actually observed;
- real SMTP acknowledgment;
- Gmail arrival/rendering owner-confirmed or still pending;
- unperformed CI/staging/production work.

Do not call fake delivery, a skipped browser check, or SMTP acceptance alone
complete end-to-end inbox acceptance. No commit/push/PR/merge/deploy is authorized.
