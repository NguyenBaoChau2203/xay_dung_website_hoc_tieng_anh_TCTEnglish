# Payment Go-Live Checklist

Use this checklist before enabling real-money traffic.

## Staging Prerequisite (Current Local-Only Situation)

- [ ] Current environment is local-only; real IPN cannot be validated from providers yet.
- [ ] Create a public HTTPS staging domain before IPN E2E (placeholder for planning: `https://staging.example.com`).
- [ ] Configure provider callback URLs to the staging domain:
  - VNPay Return: `https://staging.example.com/Billing/VnPayReturn`
  - VNPay IPN: `https://staging.example.com/api/billing/vnpay/ipn`
  - MoMo Return: `https://staging.example.com/Billing/MoMoReturn`
  - MoMo IPN: `https://staging.example.com/api/billing/momo/ipn`

## Security

- [ ] No live secret in repository files.
- [ ] All previously exposed secrets rotated.
- [ ] Production secrets stored in env vars/secret manager.
- [ ] Signature verification enabled for VNPay and MoMo.

## Build and Test

- [ ] `dotnet build -c Release` passes.
- [ ] `dotnet test -c Release` passes.
- [ ] Billing-focused tests pass (`MoMo`, `VnPay`, `Ipn`, `Billing`, `Subscription`).

## Database and Migration

- [ ] Database backup completed.
- [ ] Required migrations applied successfully.
- [ ] `PremiumPlans` seeded (`premium_monthly`, `premium_yearly`).

## Provider Configuration

- [ ] VNPay config complete (`TmnCode`, `HashSecret`, URLs, QueryDR fields).
- [ ] MoMo config complete (`PartnerCode`, `AccessKey`, `SecretKey`, URLs).
- [ ] Return URLs are correct and HTTPS.
- [ ] IPN URLs are public HTTPS and reachable.
- [ ] `/Admin/BillingManagement/Health` is green for enabled providers.

## Background Jobs

- [ ] `PendingPaymentCleanupWorker` enabled and running.
- [ ] `PremiumExpiryWorker` enabled and running.
- [ ] `PaymentReconciliationWorker` configured as intended.

## Sandbox E2E

- [ ] VNPay sandbox success payment activates premium through IPN path.
- [ ] MoMo sandbox success payment activates premium through IPN path.
- [ ] Cancelled/failed payment does not activate premium.
- [ ] Duplicate IPN is idempotent.
- [ ] Amount mismatch is blocked/manual-review.
- [ ] Return URL does not activate premium by itself.

## Production Deployment

- [ ] Deploy with both providers disabled initially.
- [ ] Smoke test non-payment site features.
- [ ] Enable VNPay first and run real smoke transaction.
- [ ] Monitor 24h-48h before enabling MoMo.
- [ ] Enable MoMo and run real smoke transaction.

## Monitoring and Alerting

- [ ] Alert for pending orders older than threshold.
- [ ] Alert for paid orders without active subscription.
- [ ] Alert for invalid signature spike.
- [ ] Alert for IPN endpoint 5xx.
- [ ] Alert for worker exceptions.
- [ ] Dashboard for `manual_review` backlog.

## Rollback Preparedness

- [ ] Fast toggle procedure documented (`VNPay__Enabled=false`, `MoMo__Enabled=false`).
- [ ] Artifact rollback plan validated.
- [ ] DB restore plan and owner on-call confirmed.
