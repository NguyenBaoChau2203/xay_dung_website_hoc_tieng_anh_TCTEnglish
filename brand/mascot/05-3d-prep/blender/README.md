# Blender pipeline for Tico (Phases 4–7)

## Prerequisites

1. Install Blender 4.x or **5.1+** (5.1+ required for official Blender Lab MCP).
2. Set environment variable optional: `BLENDER_EXE` = full path to `blender.exe`.
3. Keep working files under this folder or an ignored local path; commit only reviewed exports.

## Suggested file names

| File | Purpose |
| --- | --- |
| `tico_welcome_blockout.blend` | Optional AI/import blockout |
| `tico_welcome_retopo.blend` | Clean topology + UVs + textures |
| `tico_welcome_rig.blend` | Armature + weights + book constraint |
| `tico_welcome_anim.blend` | Actions: peek, anticipate, jump, land, greet_wave, idle |
| `../../../../TCTEnglish/wwwroot/brand/mascot/3d/tico-welcome.glb` | Web delivery |

## Clip names (must match web)

Export actions with these exact names:

- `peek`
- `anticipate`
- `jump`
- `land`
- `greet_wave`
- `idle`

## Commands

From repo root (PowerShell):

```powershell
pwsh -NoProfile -File brand/mascot/05-3d-prep/blender/run-blender.ps1 -Script export_glb_checklist.py
```

`bootstrap_scene.py` creates an empty production scene skeleton (collections, empty book object, unit scale). It does **not** generate the final character mesh.

## Export checklist

- [ ] Applied scale/rotation
- [ ] Book is separate object parented/constrained to left hand
- [ ] Shape keys or bones for blink/smile present if used
- [ ] Only needed actions exported
- [ ] Materials ≤ few slots; textures ≤ 2k where possible
- [ ] Test open in https://gltf-viewer.donmccurdy.com/
- [ ] Optional: `gltf-transform` meshopt compress before commit
- [ ] Size report recorded (target ~1.5–2.5 MB compressed — measure, do not assume)
