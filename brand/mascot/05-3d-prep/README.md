# Tico 3D preparation kit

Owner-authorized Hybrid pipeline kickoff (2026-11-12). Research baseline: `docs/TICO_3D_ANIMATION_RESEARCH.md`. Execution plan: `docs/superpowers/plans/2026-11-12-tico-3d-hybrid-pipeline.md`.

## Locked decisions

- Left hand holds TCT book; right hand waves.
- Welcome identity is the Hero 3D source (not a different fox).
- Mobile Landing: static PNG until a later decision upgrades it.
- Phone on web: CSS mockup + Three.js occluder.

## Turnaround status

| File | Role | Status |
| --- | --- | --- |
| `turnaround/tico-front-official-welcome-512.png` | Official front (copied from raster welcome) | **Official** |
| `brand/mascot/01-vector/poses/tico-mascot-welcome-color.svg` | Vector master | **Official** |
| `turnaround/tico-turnaround-sheet.jpg` | Front / L / R / Back sheet | **Proposal** — side/back inferred; owner must approve before final mesh |
| `turnaround/tico-a-pose-greeting.jpg` | A-pose + greeting | **Proposal** — verify handedness (left book / right wave) |

Do **not** treat proposal sheets as brand-final until owner sign-off. Prefer correcting proposal sheets over inventing new costume details.

## Blender scripts

See `blender/`. Requires Blender installed on PATH or `BLENDER_EXE`.

This checkout’s kickoff machine had **no `blender.exe` found** — mesh/rig/anim export is blocked until Blender is available.

## Runtime web target

Exported GLB (when ready):

`TCTEnglish/wwwroot/brand/mascot/3d/tico-welcome.glb`

Landing loader looks for that path and falls back to welcome PNG if missing.
