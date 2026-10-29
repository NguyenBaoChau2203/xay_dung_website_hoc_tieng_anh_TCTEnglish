---
name: tct-ui-component
description: Use when building reusable Razor partials, Bootstrap forms, modals, feedback states or shared TCT English interface components.
---

# Razor UI components

Read `AGENTS.md` and [DESIGN.md](../../../DESIGN.md). Reuse the target page's
existing partials, Bootstrap 5.3 patterns and icon family. The shared layout loads
Font Awesome and Material Symbols; Bootstrap Icons are not globally guaranteed.
Use typed ViewModels and Tag Helpers, with scoped CSS/JS in `wwwroot/css/` and
`wwwroot/js/`. Do not introduce React, Tailwind, shadcn, animation libraries or a
new icon CDN for a component task.

1. Inspect the existing page and shared layout. Preserve controller/view feature
   boundaries, form actions, antiforgery tokens and validation messages.
2. Build semantic HTML. Use real buttons for actions, anchors for navigation,
   visible labels, `asp-validation-for`, accessible names and `type="button"`
   where a button should not submit. Render user text through Razor encoding.
3. Cover loading, success, empty and error states; prevent duplicate submissions
   and restore enabled state after a failed request. Use existing toast helpers
   only after locating their real implementation.
4. Use Bootstrap modal behavior for focus trapping, Escape and focus return.
   Custom CSS is allowed for product styling; prefer Bootstrap utilities for
   standard layout. Avoid inline styles and page-global selector overrides.
5. For visual choices load `tct-ui-ux-pro-max`; for interface review use
   `tct-ui-review`; load `tct-ui-motion` only for an actual motion change.
6. Check keyboard-only operation, narrow and desktop widths, text zoom,
   long Vietnamese/English text, contrast and reduced motion in a real browser
   when available. Report unavailable browser verification explicitly.
