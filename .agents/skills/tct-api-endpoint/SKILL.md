---
name: tct-api-endpoint
description: Use when adding or modifying TCT English JSON endpoints called from Razor fetch or AJAX flows.
---

Follow `AGENTS.md` and the current source before applying examples. Paths below
are relative to `TCTEnglish/` unless explicitly prefixed. Use the same agent for
implementation and review. Examples do not authorize changing protected config.


# API Endpoint

## When to Use
Use when creating API endpoints called via `fetch` or `$.ajax` from Razor views for AJAX operations.

## API Naming Convention
```
GET    /api/resource          → Get list
GET    /api/resource/{id}     → Get single
POST   /api/resource          → Create
PUT    /api/resource/{id}     → Update
DELETE /api/resource/{id}     → Delete
POST   /api/resource/{id}/action → Custom action
```

## Controller Template
```csharp
[Authorize]
[HttpPost("api/cards/{cardId}/mark-learned")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> MarkCardLearned(int cardId)
{
    var userId = GetCurrentUserId();

    // Anti-IDOR: verify ownership
    var card = await _context.Cards
        .Include(c => c.Set)
        .FirstOrDefaultAsync(c => c.CardId == cardId && c.Set.OwnerId == userId);

    if (card == null) return NotFound(new { error = "Card not found." });

    // Business logic...
    await _context.SaveChangesAsync();
    return Ok(new { success = true, cardId, status = "mastered" });
}
```

## JavaScript Caller
```javascript
async function markCardLearned(cardId) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const response = await fetch(`/api/cards/${cardId}/mark-learned`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }
    });
    if (!response.ok) { showToast('error', 'Failed'); return; }
    const data = await response.json();
    if (data.success) showToast('success', 'Saved!');
}
```

## API Design Rules
1. Always return JSON: `{ success, data }` or `{ error, details }`
2. Anti-IDOR: never trust `id` without ownership verification
3. Anti-CSRF: all mutations require `RequestVerificationToken` header
4. Status codes: 200 OK, 400 Bad Request, 404 Not Found, 500 (global handler)
5. User ID: always from `GetCurrentUserId()`, never from request body

Preserve existing JSON contracts and status codes when editing an endpoint; the
envelope above is guidance for a new contract, not permission to break callers.
Keep business rules in the appropriate service and check the configured
antiforgery header name before copying the JavaScript example.
