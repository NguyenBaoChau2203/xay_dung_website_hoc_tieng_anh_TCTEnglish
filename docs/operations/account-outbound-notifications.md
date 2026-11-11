# Account outbound notifications

This note describes configuration names only. It does not contain keys or recipients.

## Settings

- `Notifications:Outbound:VapidPublicKey`
- `Notifications:Outbound:VapidPrivateKey`
- `Notifications:Outbound:VapidSubject` (for example `mailto:support@example.com`)
- `Notifications:Outbound:PublicBaseUrl` (canonical HTTPS application URL, without query/fragment; HTTP loopback allowed locally)
- Existing `SmtpSettings:Host`, `SmtpSettings:SenderEmail`, `SmtpSettings:Password`, `SmtpSettings:Port`

VAPID must be an unpadded base64url P-256 public/private pair with a real contact.
Placeholder/mismatched keys do not count as configured. SMTP and PublicBaseUrl are
required for weekly mail. A saved checkbox is not a sent notification. Never put
keys/passwords in tracked configuration, shell arguments, screenshots or reports.

## Opt-in

Daily push and weekly email are off until the user saves them. Push also needs a browser permission grant and a device subscription created from the account button. Weekly email is sent only to an address with `EmailVerifiedAt` set.

Unsubscribe is a POST on Account Settings. Opening a link does not turn the email off.
The device button also unregisters that browser subscription. Endpoint ownership
cannot be transferred to a different account; the browser obtains a fresh endpoint
when switching users. The service worker verifies the logged-in account before
display and drops a queued message after logout/account switch.

Use the Account email verification form to verify an existing login address before
weekly mail. Email change confirms the new address separately. The production
proof flow needs working SMTP; offline tests use a fake sender and do not deliver.

## Schedule

The worker runs about once a minute while the host process is running. A daily reminder is attempted only within 60 minutes after the saved local time. A weekly summary is attempted on Monday from 08:00 for 24 hours in the reminder time zone, using the previous Monday–Sunday in the Vietnam learning clock. Missed windows are marked expired. They are not replayed for older days or weeks.

Two workers share a database lease, with a fresh token on each claim and conditional
settlement. Expired leases, SMTP timeout/connection loss and ambiguous outcomes stay
Unknown and are not retried blindly. Investigate the provider receipt before any
manual resend. Push404/410 revoke only the unchanged subscription. Each dispatch
rechecks opt-out, reminder enablement, goals, account status and expiry. An exception
in one delivery does not stop the batch. Weekly summaries retain their stored week
and clip/merge time at week boundaries; all settings links use PublicBaseUrl.

A powered-off device, a sleeping IIS worker, or a denied browser permission can delay or drop a push. This is not a guaranteed delivery clock.

## Cleanup

Account deletion persists an avatar cleanup job within the deletion transaction;
filesystem removal follows commit. Missing files count as already cleaned. Failed
removal retries up to five times and then remains Failed for investigation. It only
deletes owned files under the avatar URL prefix.

## Local verification and rollout

Use the standard launcher, preserve the existing local DB and apply the committed
migration with its Designer/snapshot. The worker runs only while the host is awake;
IIS sleep/recycle can miss a schedule window. Monitor Pending/Failed/Unknown counts,
lease expiry and cleanup Failed jobs without logging endpoints, encryption keys,
message tokens or recipient addresses. The sensitive action limiter is local to
each host process; challenge issuance also has a persistent per-user limit.

The11/11/2026 review proved offline transport behavior and MariaDB10.5.23 upgrade,
repeat and concurrency. It did not prove real push receipt, SMTP authentication or
inbox placement. A release acceptance run needs a dedicated consenting test account,
configured keys/canonical URL, actual device permission and mailbox observations.
See the Account execution ledger; do not infer live delivery from a green test suite.
