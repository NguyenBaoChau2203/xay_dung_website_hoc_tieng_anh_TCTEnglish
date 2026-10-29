---
name: tct-security-audit
description: Use when reviewing TCT English code for access control, antiforgery, injection or upload security issues.
---

Follow `AGENTS.md` and the current source before applying examples. Paths below
are relative to `TCTEnglish/` unless explicitly prefixed. Use the same agent for
implementation and review. Examples do not authorize changing protected config.


# Security Audit

## When to Use
Use when reviewing code for security vulnerabilities, auditing controllers before release, or doing a full-app security assessment.

## Audit Protocol — OWASP Top 10 + TCT English Checks

### A01 — Broken Access Control (IDOR) 🔴
```csharp
// FIND: var item = await _context.Sets.FindAsync(id);  // ❌ No ownership
// SHOULD: .FirstOrDefaultAsync(s => s.SetId == id && s.OwnerId == currentUserId); // ✅
```
- Every UPDATE/DELETE/parameterized READ: `UserId == currentUserId`
- Admin endpoints: `[Authorize(Roles = "Admin")]`
- Class operations: verify ClassMember role

### A02 — Cryptographic Failures 🔴
- BCrypt `workFactor >= 10`
- No plaintext passwords in logs or ViewBag

### A03 — Injection (XSS, SQLi) 🔴
- `@Html.Raw(userInput)` = XSS danger
- `ExecuteSqlRawAsync($"...{input}")` = SQLi danger

### A05 — Security Misconfiguration 🔴
- `[ValidateAntiForgeryToken]` on ALL mutations
- No hardcoded credentials

### A07 — Authentication Failures 🔴
- `[Authorize]` on all non-public actions
- Verify password reset expiry against the current implementation and product contract

## Output Format
```markdown
## Security Audit Report: [Target]
### 🔴 CRITICAL — Fix Immediately
| # | File | Vulnerability | Fix |
### 🟡 HIGH RISK — Fix Before Release
### 🟢 LOW RISK — Fix When Convenient
### ✅ Security Controls Verified
```

## TCT English Known Patterns
1. GoalsController: use GetCurrentUserId(); never parse claims inline
2. SpeakingVideoManagement: file upload MIME validation
3. ClassDetail: verify ClassMember before read/write
4. AvatarUpload: max file size + extension whitelist
5. Admin area: ALL controllers need `[Area("Admin")][Authorize(Roles = "Admin")]`
