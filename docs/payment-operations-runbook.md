# Payment Operations Runbook

This runbook is for production support incidents in TCTEnglish billing.

## 0. How an order is confirmed (read this first)

An order becomes `paid` in exactly one place, `PaymentConfirmationService`. It does so for:

1. a **signed provider callback** (IPN), or
2. a **verified provider query with the right amount** (`PaymentQueryService`): triggered when the buyer returns from the provider with a success code, when the buyer or the order page presses "Kiểm tra lại ngay" (`POST /Billing/Order/{code}/Refresh`), or by the reconciliation worker, or
3. an **admin action** (`ResolveManualReviewConfirmPaid`) on a `manual_review` order.

The browser's return alone never confirms anything. A provider answer that does not settle the order (still processing, "not found yet", query failed) never changes it. Queries are throttled per order: at most one every 20 seconds and 12 in all (each attempt is a `query` event). Events you will see in an order's timeline: `checkout_created`, `return`, `ipn`, `query`, `reconcile` (the confirmation made by a query: key `retq:`/`refq:`/`recon:{OrderCode}`), `grant_premium`, `manual_review`, `manual_confirm`, `manual_reject`.

Status changes follow `PaymentOrderStateMachine`: a failure callback cannot overwrite `manual_review`, `refunded`, `partially_refunded` or `expired` (it is recorded as `ignored`); a success after a failure wins; a success more than 24 h after the order expired (any provider) goes to `manual_review`; `refunded` orders never come back.

What the buyer sees on `/Billing/Order/{code}`: **Chờ thanh toán** (waiting), **Đang xác nhận** (the provider returned "paid" or the order is in manual review; the page re-checks by itself), **Thanh toán thành công** (paid, with the end date) or **Thanh toán chưa hoàn tất** (cancelled, failed, expired, refunded). Buyers may be asked to sign in again after a payment (see known issue `BILLING-ROLE-CHANGE-SIGNS-OUT`); the order page and `/Billing/History` show the right state after they do.

## 1. Customer Charged But No Premium

1. Find order by `OrderCode` in admin billing details.
2. Check `PaymentOrders.Status`, `PaymentEvents` (look for `ipn`, `return`, `query`, `reconcile`) and `UserSubscriptions`.
3. Ask the customer to open `/Billing/History` and the order; "Kiểm tra lại ngay" asks the provider again. If `ReconciliationWorker__Enabled=true` it is also checked every few minutes for orders that are `pending` or expired within 24 h.
4. If the order is `manual_review`, or the provider shows a valid paid transaction but the system still has no confirmation after the order is older than 24 h, use admin manual review resolve as paid with reason (the confirmation is atomic: paid, subscription, `ActivatedAtUtc` and the confirming admin are written together).
5. Record incident ID and root cause in internal tracker.

## 2. Pending Order Too Long

1. Confirm worker status for pending cleanup.
2. Check `CreatedAtUtc`, `ExpiresAtUtc`, and last payment events (including `query` events and the return payload).
3. If provider has no paid evidence, allow transition to `expired`.
4. If provider evidence exists but callback and query are missing, mark `manual_review` and resolve with audit. A return that said "paid" more than one hour past expiry with no confirmation is moved to `manual_review` by the reconciliation worker.

## 3. Invalid Signature Spike

1. Verify environment secrets are correct and not rotated on one side only.
2. Check whether payload fields changed from provider side.
3. Temporarily disable affected provider if risk is high.
4. Keep suspicious requests for forensics; do not bypass signature verification.
5. A MoMo IPN with a body that is not a JSON object, or larger than 64 KB, is answered with 204 and logged as one warning line without the payload; it is not an error to chase.

## 4. Duplicate IPN

1. Confirm idempotency: no duplicate subscription activation.
2. Verify `PaymentEvents` unique keys are preventing duplicate processing (MariaDB "Duplicate entry" is handled; a racing duplicate is acknowledged).
3. Two racing confirmations (IPN and a query) cannot activate twice: only one can insert the unique `grant:{OrderCode}` event.
4. If duplicate changed state incorrectly, revert through audited admin action only.

## 5. Amount Mismatch

1. Do not auto-activate premium. The confirmation service refuses a provider amount that differs from the order (event `failed`, "Amount mismatch"); the order stays as it was.
2. Set/keep order at `manual_review`.
3. Compare plan price snapshot vs provider amount.
4. Resolve with explicit admin reason and audit trail.

## 6. Manual Review Handling

1. Use admin action `ResolveManualReviewConfirmPaid` only with verified evidence.
2. Use `ResolveManualReviewReject` when payment is invalid/failed.
3. Every resolve action must include a reason.
4. Verify subscription is activated at most once.
5. The "chuyển sang kiểm tra thủ công" form is only offered for `pending`, `expired`, `failed` and `cancelled` orders; `paid` and `refunded` orders cannot be sent to review.

## 7. Refund Requests

1. Execute refund in VNPay/MoMo merchant portal.
2. Do not edit payment rows directly in DB.
3. Attach provider transaction proof to incident notes.
4. Update order context/audit notes through admin workflow.

## 8. Disable Provider During Gateway Incident

1. Set `VNPay__Enabled=false` or `MoMo__Enabled=false`.
2. Restart service.
3. Verify `/Admin/BillingManagement/Health` shows disabled.
4. Post incident notice to support/admin channels. Learners see "Cổng thanh toán đang tạm ngưng" and no pay button when no gateway is available.

## 9. End-of-Day Reconciliation

1. List all `pending` and `manual_review` orders created in the last 24h.
2. Reconcile each with provider dashboard/report.
3. Resolve actionable manual-review orders with explicit reasons.
4. Export summary:
   - total paid
   - total failed/cancelled
   - total pending
   - unresolved manual-review count

## 10. Incident Evidence Checklist

- Order code
- Provider transaction ID
- Provider response code
- Payment event timeline (checkout/return/ipn/query/reconcile)
- Admin actions and reasons
- Final status and customer communication timestamp

## 11. Local mock mode (development only)

MoMo mock mode never exists outside `ASPNETCORE_ENVIRONMENT=Development`: the dashed "Chế độ thử nghiệm" box and `POST /Billing/MoMoMockConfirm` answer 404 in any other environment. Enable it for one local run with process environment variables only (never in `appsettings` or user secrets): `MoMo__Enabled=true`, `MoMo__MockModeEnabled=true`, `MoMo__MockSecretKey=<random value for that run>`, then `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`. No real payment is made and no provider is called.
