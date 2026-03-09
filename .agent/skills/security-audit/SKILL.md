---
name: Security Audit
description: Full OWASP-based security audit of a TCT English controller, service, or view with severity-based reporting
---

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
- Password reset tokens expire after 1 hour

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
1. GoalsController: verify UserId parsed as int
2. SpeakingVideoManagement: file upload MIME validation
3. ClassDetail: verify ClassMember before read/write
4. AvatarUpload: max file size + extension whitelist
5. Admin area: ALL controllers need `[Area("Admin")][Authorize(Roles = "Admin")]`
