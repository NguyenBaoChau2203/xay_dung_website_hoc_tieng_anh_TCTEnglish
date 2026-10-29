---
name: tct-ui-ux-pro-max
description: Use when choosing or reviewing TCT English layout, colors, typography, accessibility or interaction patterns with a local searchable design reference.
---

# Design lookup for TCT English

Read [DESIGN.md](../../../DESIGN.md) and the target page first. The product uses
Razor, Bootstrap 5.3, scoped CSS and vanilla JS/jQuery. The upstream data has no
Razor/Bootstrap stack profile. Use platform-neutral domains; do not apply React,
Tailwind, shadcn or GSAP defaults or install their packages.

From the repository root, query only the concern that matters:

```powershell
uv run --no-project python .agents/skills/tct-ui-ux-pro-max/scripts/search.py "education dashboard readable progress" --domain ux -n 3
```

For a new visual direction explicitly requested by the owner:

```powershell
uv run --no-project python .agents/skills/tct-ui-ux-pro-max/scripts/search.py "employee English learning dashboard" --design-system -p "TCT English"
```

Inspect results as suggestions. Preserve brand, routes, ViewModels, antiforgery,
validation and existing icons. Review generated output against the product before
persisting it; never overwrite DESIGN.md automatically.

Use [quick reference](references/quick-reference.md) for specific UX rules and
[professional checks](references/pro-rules.md) for delivery review. Use
`tct-ui-review` for rendered checks and `tct-ui-motion` for motion changes.
Run Python through uv on this machine: python currently points at the Windows
Store alias. A regular Python installation also works for this search.

Source: [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill).
Original entrypoint and license: .ai/vendor/ui-ux-pro-max/. Scripts, references
and CSV data are pinned in .ai/tooling/sources.lock.json.
