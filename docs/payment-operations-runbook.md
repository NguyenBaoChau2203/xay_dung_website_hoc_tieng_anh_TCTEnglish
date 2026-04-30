# Payment Operations Runbook

This runbook is for production support incidents in TCTEnglish billing.

## 1. Customer Charged But No Premium

1. Find order by `OrderCode` in admin billing details.
2. Check `PaymentOrders.Status`, `PaymentEvents`, and `UserSubscriptions`.
3. If IPN is missing and reconciliation is available, trigger reconciliation path.
4. If evidence shows valid paid transaction but system not activated, use admin manual review resolve as paid with reason.
5. Record incident ID and root cause in internal tracker.

## 2. Pending Order Too Long

1. Confirm worker status for pending cleanup.
2. Check `CreatedAtUtc`, `ExpiresAtUtc`, and last payment events.
3. If provider has no paid evidence, allow transition to `expired`.
4. If provider evidence exists but callback missing, mark `manual_review` and resolve with audit.

## 3. Invalid Signature Spike

1. Verify environment secrets are correct and not rotated on one side only.
2. Check whether payload fields changed from provider side.
3. Temporarily disable affected provider if risk is high.
4. Keep suspicious requests for forensics; do not bypass signature verification.

## 4. Duplicate IPN

1. Confirm idempotency: no duplicate subscription activation.
2. Verify `PaymentEvents` unique keys are preventing duplicate processing.
3. If duplicate changed state incorrectly, revert through audited admin action only.

## 5. Amount Mismatch

1. Do not auto-activate premium.
2. Set/keep order at `manual_review`.
3. Compare plan price snapshot vs provider amount.
4. Resolve with explicit admin reason and audit trail.

## 6. Manual Review Handling

1. Use admin action `ResolveManualReviewConfirmPaid` only with verified evidence.
2. Use `ResolveManualReviewReject` when payment is invalid/failed.
3. Every resolve action must include a reason.
4. Verify subscription is activated at most once.

## 7. Refund Requests

1. Execute refund in VNPay/MoMo merchant portal.
2. Do not edit payment rows directly in DB.
3. Attach provider transaction proof to incident notes.
4. Update order context/audit notes through admin workflow.

## 8. Disable Provider During Gateway Incident

1. Set `VNPay__Enabled=false` or `MoMo__Enabled=false`.
2. Restart service.
3. Verify `/Admin/BillingManagement/Health` shows disabled.
4. Post incident notice to support/admin channels.

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
- Payment event timeline (checkout/return/ipn/reconcile)
- Admin actions and reasons
- Final status and customer communication timestamp
