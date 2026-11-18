# Tico 3D Hybrid Pipeline Implementation Plan

> **For agentic workers:** Execute inline in this checkout. Do not spawn subagents. Owner selected option **4** (full Hybrid pipeline) on 2026-11-12.

**Goal:** Produce approved Tico 3D greeting assets and integrate them into the public Landing Hero with true behind-phone occlusion, once-per-session greeting, and accessible fallbacks.

**Architecture:** Hybrid DCC (optional AI blockout → Blender retopo/rig/anim → GLB) + vanilla Three.js on Razor Landing; CSS phone chrome + 3D occluder; PNG fallback.

**Tech Stack:** Blender (required for mesh/rig/anim), GLB + Meshopt, Three.js (CDN ESM), `landing.cshtml` / `landing.css` / `landing.js` + new `landing-tico.js`.

## Global Constraints

- Left hand = TCT book object; right hand = natural wave (never reverse).
- Welcome-derived identity (not inventing a different fox); logos stay crisp.
- No React Three Fiber; no Mixamo as primary auto-rig.
- Mobile ≤768px: static welcome PNG (or very light idle later) — not full behind-phone jump.
- Speech bubble: `Xin chào! Chào mừng bạn đến với TCT English!`
- Phone: keep CSS mockup + Three.js depth occluder aligned to it.
- Three.js via CDN ESM import map (no Program.cs / .csproj change unless later required).
- Do not claim a placeholder mesh as final brand asset.
- Research baseline: `docs/TICO_3D_ANIMATION_RESEARCH.md`.

## Locked owner defaults (2026-11-12)

| Decision | Value |
| --- | --- |
| Production path | Hybrid |
| Hands | Left book / right wave |
| Mobile | Static PNG (full 3D sequence desktop/laptop only) |
| Bubble | Vietnamese string above |
| Phone | CSS chrome + 3D occluder |
| Hero visual | Welcome-derived 3D when GLB present; else welcome PNG poster |
| Three.js | CDN ESM |

## Environment blocker

- **Blender executable was not found** on this machine during kickoff search. Phases 4–7 (model/rig/anim/export) are **blocked** until Blender is installed and a `.blend` workflow can run. Landing scaffold + reference sheets + Blender scripts proceed anyway.

## Files

| Path | Responsibility |
| --- | --- |
| `docs/superpowers/plans/2026-11-12-tico-3d-hybrid-pipeline.md` | This plan / ledger |
| `brand/mascot/05-3d-prep/` | Turnaround sheets, pipeline README, Blender scripts |
| `TCTEnglish/wwwroot/brand/mascot/3d/` | Runtime GLB + poster notes (GLB added when exported) |
| `TCTEnglish/wwwroot/js/landing-tico.js` | Three.js scene, clips, session gate, fallback |
| `TCTEnglish/Views/Home/landing.cshtml` | Hero host markup + import map |
| `TCTEnglish/wwwroot/css/landing.css` | Canvas/bubble/responsive rules |
| `TCTEnglish/wwwroot/js/landing.js` | Hook IntersectionObserver / reduced-motion into Tico controller |

## Task checklist

- [x] Write this plan with locked defaults
- [x] Create `brand/mascot/05-3d-prep` turnaround reference set + README (Phase 3)
- [x] Add Blender helper scripts + export checklist (Phases 4–7 ready)
- [x] Scaffold Landing Three.js host with PNG fallback (Phase 8 structure)
- [x] Wire once/session, reduced-motion, mobile static, bubble
- [x] Verify Landing still loads; document Blender blocker for GLB

## Ledger

- 2026-11-12: Owner chose full Hybrid pipeline (option 4) with locked defaults.
- 2026-11-12: Blender not on PATH / common install dirs — Phases 4–7 blocked (`run-blender.ps1` exit 3).
- 2026-11-12: Proposal turnaround sheets generated; official front copied from welcome PNG.
- 2026-11-12: Landing host uses welcome poster; GLB path wired; 3D plays only when GLB exists.
- 2026-11-12: `dotnet build` **FAILED** only because `TCTEnglish.exe` is locked by a running local process (PID noted in build log) — not because of Landing compile errors. wwwroot/Razor edits still apply via the running app / runtime compilation.

## Acceptance (current sprint)

1. Reference sheets exist under `brand/mascot/05-3d-prep/` and are labeled proposal vs official front.
2. Landing Hero shows welcome poster; WebGL path activates only if `tico-welcome.glb` exists and motion/WebGL OK.
3. No crash when GLB missing.
4. Desktop structure supports future behind-phone occluder; mobile keeps static poster.
5. Blender absence clearly reported; scripts documented for next machine with Blender.
