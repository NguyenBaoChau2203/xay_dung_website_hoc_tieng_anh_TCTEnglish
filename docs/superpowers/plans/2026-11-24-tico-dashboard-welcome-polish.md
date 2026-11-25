# Tico Dashboard Welcome Polish — Implementation Ledger

> Execute inline in this checkout. Do not spawn subagents.

**Goal:** Fix framing, materials/lighting, speech bubble, floating UI collision, and polish skinned GLB clips for the post-login Welcome overlay.

**Architecture:** Existing ASP.NET Dashboard + vanilla Three.js ESM + Blender working GLB. No React. Source `mascot_tct.glb` immutable.

## Ledger

- 2026-11-24: Plan approved; start web framing/materials + UI overlay, then Blender rebuild + QA.
- 2026-11-24: Web JS/CSS updated (Box3 framing, ACES, entrance fade, bubble, launcher hide).
- 2026-11-24: Blender rebuild weight gate OK; textures 2048; host recompress → ~5.3 MB GLB; source `mascot_tct.glb` untouched.
- 2026-11-24: Fixed stage collapse (absolute fill), opaque materials (no transparent skinned shards), weight limit-4, bind-pose camera cache, Skip + session checks verified locally on :5100.
- 2026-11-24: Task 01 MCP audit complete (inspect only). Report: `docs/superpowers/plans/2026-11-24-tico-task01-mcp-audit-report.md`. Source/runtime GLB + repo `.blend` untouched. High findings: blend missing durable actions; L/R hand weight asymmetry; wave script uses R_* while sculpted mesh waves left; HASHED material. Next: Task 02 diagnosis.
- 2026-11-24: Task 02 geometry/rigging diagnosis complete (no repair). Report: `docs/superpowers/plans/2026-11-24-tico-task02-geometry-rigging-diagnosis.md`. GLB Wave is R-driven (SUM_R 181 vs SUM_L 4); L_Hand mass ~77× R_Hand; L_raise90 tears mesh; blend actions lost via orphan purge (no fake_user) + empty layered Exit. MCP bridge died mid-task — finished via Blender CLI. Repo originals hash-unchanged. Next: Task 03 repair.
- 2026-11-24: Task 03 character repair complete. Report: `docs/superpowers/plans/2026-11-24-tico-task03-character-repair.md`. Wave→L_* (SUM_L 182 / SUM_R 2); weight refine + gate PASS; fake_user/NLA/keyed OK on reopen; 8 degen removed; L_raise90 no catastrophic tear; residual black shading on ears for Task 04. `mascot_tct.glb` hash unchanged; runtime GLB ~5.25 MB.
- 2026-11-24: Task 04 material/lighting complete. Report: `docs/superpowers/plans/2026-11-24-tico-task04-material-lighting.md`. GLB alphaMode OPAQUE; keep Tripo custom normals (clearing worsened facets); BLEND+alpha1 workaround for Blender 5.3 OPAQUE→HASHED bug; softer metal/rough/normal + DoubleSide + fill-heavy lights. Residual Tripo ear/cheek facets remain. Next: Task 06 Browser QA.
- 2026-11-24: Task 06 Browser QA complete. Report: `docs/superpowers/plans/2026-11-24-tico-task06-browser-qa.md`. Playback L-wave + bubble/Skip/session PASS; responsive 1440/768/390 + reduced-motion PASS; console clean; no code fix; Task 05 not needed. Residual ear/cheek facet PARTIAL. `mascot_tct.glb` hash unchanged.
- 2026-11-24: Owner requested full 3D removal. Removed Dashboard/Landing Three.js welcome, import map, auth `welcomeTico` hooks, `wwwroot/brand/mascot/3d`, `brand/mascot/mascot_tct.glb`, and `brand/mascot/05-3d-prep`. 2D mascot kit kept. Historical plan docs retained as archive only.
