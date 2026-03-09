# Security Checklist — TCT English

Quick reference for security review. Based on OWASP Top 10 adapted for ASP.NET Core + EF Core.

## Critical Checks

### A01 — Broken Access Control (IDOR)
- [ ] Every parameterized READ: `.Where(x => x.UserId == currentUserId && x.Id == id)`
- [ ] Every UPDATE/DELETE: ownership check before `SaveChangesAsync()`
- [ ] Class operations: `ClassMember` role verified (Owner vs Member)
- [ ] Admin endpoints: `[Authorize(Roles = "Admin")]` at controller level

### A02 — Cryptographic Failures
- [ ] BCrypt with `workFactor >= 10`
- [ ] No plaintext passwords in logs, ViewBag, or error messages
- [ ] OAuth tokens never stored beyond authentication

### A03 — Injection
- [ ] No `@Html.Raw()` with user input (XSS)
- [ ] No raw SQL with string interpolation (`ExecuteSqlRawAsync($"...{input}")`)
- [ ] Use parameterized queries: `ExecuteSqlRawAsync("...", input)`

### A05 — Security Misconfiguration
- [ ] `[ValidateAntiForgeryToken]` on ALL POST/PUT/DELETE actions
- [ ] No hardcoded credentials in source code
- [ ] `DeveloperExceptionPage` disabled in production

### A07 — Authentication Failures
- [ ] `[Authorize]` on all non-public actions
- [ ] Cookie: `HttpOnly=true`, `Secure=true` in production
- [ ] Password reset tokens expire after 1 hour

## TCT-Specific Patterns
- Use `GetCurrentUserId()` from `BaseController` — never parse claims inline
- Return `NotFound()` for IDOR violations (never `Forbid()`)
- File uploads: validate MIME type + extension + max size
- Admin area: ALL controllers must have `[Area("Admin")][Authorize(Roles = "Admin")]`
