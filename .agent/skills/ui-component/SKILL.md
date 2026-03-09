---
name: UI Component
description: Build reusable Razor partial views and Bootstrap 5 UI components for TCT English
---

# UI Component Builder

## When to Use
Use when building reusable UI components: modals, toasts, cards, loading skeletons, empty states, or any shared Razor partials.

## UI Stack
- **CSS**: Bootstrap 5.3 (CDN via `_Layout.cshtml`)
- **Icons**: Bootstrap Icons (`bi-*` classes)
- **JS**: Vanilla JS + jQuery (globally loaded)
- **Animations**: CSS transitions only

## File Locations
```
Views/Shared/_<ComponentName>.cshtml     ← Shared partials
Views/<Feature>/_<Partial>.cshtml        ← Feature-specific
wwwroot/css/<feature>.css                ← Feature CSS
wwwroot/js/<feature>.js                  ← Feature JS
```

## Common Components

### Toast Notification
```javascript
function showToast(type, message, duration = 3000) {
    // Bootstrap Toast component, auto-dismiss
    // Types: success (bg-success), error (bg-danger), warning, info
}
```

### Delete Confirmation Modal
```cshtml
<!-- Bootstrap modal with form action, anti-forgery token -->
function showDeleteModal(itemName, deleteUrl) { ... }
```

### Loading Skeleton
```css
.skeleton-line {
    background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
    animation: shimmer 1.5s infinite;
}
```

### Empty State
```cshtml
<div class="text-center py-5 text-muted">
    <i class="bi bi-inbox fs-1"></i>
    <h5>@title</h5>
    <p>@message</p>
</div>
```

## Checklist
- [ ] Mobile-first: test at 375px before 1440px
- [ ] No inline styles except for specific EdTech UI
- [ ] Modals: focus trap + ESC key dismiss
- [ ] Forms: `asp-validation-for` under each input
- [ ] Loading states during async operations
- [ ] Error states: user-friendly message, never raw exception
