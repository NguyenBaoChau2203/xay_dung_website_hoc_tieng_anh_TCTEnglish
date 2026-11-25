# Task 03 — Character Repair Report

**Date:** 2026-11-24  
**Scope:** Weights + Welcome_Wave → `L_*` + action persistence + degenerate/normals + rebuild.  
**Không làm:** redesign màu/trang phục; overwrite `mascot_tct.glb`; Material polish (Task 04); Browser QA (Task 06).

## 1. Changes

| File | Change |
|---|---|
| [`brand/mascot/05-3d-prep/blender/build_dashboard_welcome.py`](../../brand/mascot/05-3d-prep/blender/build_dashboard_welcome.py) | `refine_weights`, `fix_geometry`, wave L_*, gate mở rộng, `fake_user`/`persist_actions_nla`/`assert_actions_keyed`, save sau export |
| [`tico_dashboard_welcome_rig.blend`](../../brand/mascot/05-3d-prep/blender/tico_dashboard_welcome_rig.blend) | Rebuild |
| [`tico_dashboard_welcome_gate.json`](../../brand/mascot/05-3d-prep/blender/tico_dashboard_welcome_gate.json) | Gate PASS (L wave) |
| [`tico-dashboard-welcome.glb`](../../TCTEnglish/wwwroot/brand/mascot/3d/tico-dashboard-welcome.glb) | Rebuild + JPEG recompress (~5.25 MB) |

**Source unchanged:** `mascot_tct.glb` SHA256 `B5620826…EE814F` (khớp baseline Task 03).

## 2. Metrics before → after

| Metric | Task 02 | Task 03 |
|---|---|---|
| Wave travel SUM_L / SUM_R | 4 / 181 | **182 / 2** |
| `hand_mass_ratio` L/R | ~77.5 | **0.275** |
| `R_Hand` max weight | 0.25 | **0.8545** |
| Zero-weight % | ~2.1% | **0.0%** |
| Degenerate faces removed | 8 found | **8 removed** |
| Gate torso_drift (wave arm) | R-only 0.010 | L **0.036** (limit 0.045) PASS |
| Actions in `.blend` after reopen | Exit empty | **4 clips, keyed 18/30/18/12, fake_user** |
| NLA tracks | empty | 4 strips |

## 3. Stress / preview

Workspace previews: `C:\Users\ASUS\blender-asset-mcp\workspace\previews\tico_t03_*.png`

- **L_raise90:** không còn tear/streak catastrophic như Task 02 (PASS chính).
- **wave_peak:** tay trái nâng, sách/tay phải giữ cạnh thân.
- **Residual:** vùng đen/facet trên tai/má/hoodie ở góc nghiêng — nghi normals/Tripo topology + specular; **chưa** coi Material PASS (Task 04).

Stress JSON: `workspace\tico_repair_task03\task03_stress.json`.

## 4. Acceptance checklist

| Tiêu chí | Status |
|---|---|
| Weights refine + gate PASS | PASS |
| Wave L_* in GLB | PASS |
| Action persistence (fake_user/NLA/keyed) | PASS |
| 8 degenerate removed | PASS |
| L_raise90 no catastrophic tear | PASS |
| `mascot_tct.glb` untouched | PASS |
| Head shading fully clean | **PARTIAL** (residual black patches) |
| Browser QA | NOT RUN |

## 5. Next — Task 04

Material/lighting: giảm specular blowout, xử lý shading đen tai/má (normals/HASHED), giữ nhận diện màu. Sau đó Task 05 polish timing nếu cần; Task 06 Browser QA.
