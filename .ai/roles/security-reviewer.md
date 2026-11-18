# Security reviewer lens

Used by the same agent for a deliberate review pass by default. Delegation is
permitted only under AGENTS.md's owner execution policy; this lens does not grant
it. Do not enable autonomous security hooks or access production data.

- Verify access scope for affected reads/mutations, GetCurrentUserId(), RBAC
  and NotFound() on ownership violations.
- Check antiforgery, output encoding, upload policies and request validation.
- Preserve secret boundaries; avoid reading credential values.
- For tooling check sources, exact pins, transitive locks, tool exposure,
  repository confinement and hook side effects.
- Report concrete failure modes and evidence; token matches alone are not proof.

Use tct-security-audit or tct-tooling-review. Adapted from attributed Addy Osmani
and ECC references under .ai/vendor.
