# Task 01 — MCP Connection & Project Audit Report

**Date:** 2026-11-24  
**Scope:** Welcome / Tico 3D only — inspect + report. No asset repair, no final export, no overwrite of originals.  
**Blender:** 5.3.0 · AI Bridge 1.0.0 · MCP `project-0-web_tctEnglish-blender`

## 1. pstack (đã chốt)

- Budget: `large (xhigh)`
- Mọi role: `inherit-parent`
- File `~/.cursor/rules/pstack-models.mdc` khớp — không rewrite

## 2. MCP capability (đã xác minh bằng lệnh thật)

| Kiểm tra | Kết quả |
|---|---|
| `ping` | OK — MCP server sống |
| `ping_blender` | OK — Blender 5.3.0, bridge 1.0.0 |
| `get_bridge_status` | OK — `127.0.0.1:9876`, `allow_outside_reads: false` |
| Workspace | `C:\Users\ASUS\blender-asset-mcp\workspace` (previews / checkpoints / exports / logs) |

**Tool / action thực tế dùng được trong audit:** scene/object/material/image/modifier inspect, `analyze_game_asset`, `execute_blender_python`, `begin_operation` / `cancel_operation` / checkpoint, `create_preview_lighting`, `render_preview`, export GLB/glTF (chưa dùng), `list_bridge_actions` (116 actions).

**Giới hạn:** MCP file tools không đọc ngoài workspace. Task 01 đã **copy** working assets vào `workspace\tico_audit_task01\` rồi mở bản copy — không Save đè repo.

## 3. Repository inventory

| File | Size | LastWrite (local) | Vai trò |
|---|---:|---|---|
| `brand/mascot/mascot_tct.glb` | 19 723 220 | 2026-11-24 01:12 | Source immutable (~262 807 verts, không skin/anim) |
| `.../tico_dashboard_welcome_rig.blend` | 14 007 124 | 2026-11-24 02:25 | Working Blender project |
| `.../tico_dashboard_welcome_rig.blend1` | 14 045 756 | 2026-11-24 02:17 | Autosave |
| `.../tico_welcome_bootstrap.blend` | 86 421 | 2026-11-12 | Bootstrap nhỏ |
| `.../tico_dashboard_welcome_gate.json` | 222 | 2026-11-24 02:25 | Weight gate (PASS torso_drift 0.01042 / limit 0.045) |
| `.../build_dashboard_welcome.py` | 31 059 | 2026-11-24 02:24 | Pipeline build |
| `.../recompress_welcome_glb.py` | 4 016 | 2026-11-24 02:18 | Host recompress |
| `wwwroot/.../tico-dashboard-welcome.glb` | 5 254 768 | 2026-11-24 02:25 | Runtime GLB |
| `wwwroot/js/dashboard-tico-welcome.js` | 13 361 | 2026-11-24 02:29 | Three.js mixer |
| `wwwroot/css/dashboard-tico-welcome.css` | 6 765 | 2026-11-24 02:26 | Overlay styles |

**SHA256 sau audit (repo không đổi):**

- `mascot_tct.glb` → `B5620826…EE814F`
- `tico-dashboard-welcome.glb` → `490231AE…FB21CF`
- `tico_dashboard_welcome_rig.blend` → `DE738FEB…12EDDCD2`

## 4. Pipeline & frontend contract

**Build pipeline** (`build_dashboard_welcome.py`):

1. Import `mascot_tct.glb` → `Tico_Mesh`
2. UV-safe decimate (target ~56 000 verts; actual working mesh **64 720**)
3. Custom armature `Tico_Armature` (18 deform bones)
4. `ARMATURE_AUTO` weights → limit **4** influences + normalize
5. Pose gate (raise right arm) → `tico_dashboard_welcome_gate.json`
6. Actions: `Tico_Entrance` (36f), `Tico_Welcome_Wave` (80f), `Tico_Idle` (24f), `Tico_Exit` (36f) @ 24 fps
7. Export → `wwwroot/brand/mascot/3d/tico-dashboard-welcome.glb`

**Frontend** (`dashboard-tico-welcome.js`):

- `CLIP_ORDER`: Entrance → Welcome_Wave → Idle → Exit
- Bubble tại `Tico_Welcome_Wave`
- ACES exposure 0.92; force materials **opaque** (tránh white shards)
- Bind-pose `Box3` framing; scale 0.92
- Hook: `Views/Home/Index.cshtml`

## 5. Scene inspection (working blend copy)

| Hạng mục | Giá trị |
|---|---|
| Objects | `Tico_Mesh`, `Tico_Armature` |
| Collections | `CHAR_Tico`, `RIG`, `EXPORT` |
| Verts / tris | 64 720 / 106 830 (all triangles) |
| UV | `UVMap` present |
| Modifier | Armature → `Tico_Armature` |
| Bones | 18 (Root→… Head; L/R arms & legs) — khớp vertex groups |
| Material | 1× Tripo Principled; maps packed 2048² basecolor / normal / RM |
| Lights / camera in blend | **None** (preview lighting tạm thời rồi restore) |
| Actions trong `.blend` | Chỉ `Tico_Exit` — **layered action rỗng (0 fcurves)**; NLA trống |
| Actions trong runtime GLB | Cả 4 clip, mỗi clip 54 channels |
| Skin joints trong GLB | 19 (= 18 bones + `neutral_bone`) |

`analyze_game_asset`: missing UVs / unapplied transforms / non-manifold list tool = empty; topology detail: **boundary edges 22 338**, **degenerate faces 8**, open mesh (`is_closed: false`), ~4676 duplicate-coordinate sets.

## 6. Preview evidence (đã mở ảnh)

Workspace previews (sau `cancel_operation` scene restore):

- `previews/tico_audit_bind_front_*.png`
- `previews/tico_audit_bind_three_quarter_*.png`
- `previews/tico_audit_rarm_raise_*.png`
- `previews/tico_audit_rarm_raise_tq_*.png`

Quan sát render:

- Nhận diện Tico giữ đúng (lông cam, hoodie xanh TCT, khăn vàng, sách xanh, tư thế chào).
- Mesh rest đã **sculpt sẵn tư thế vẫy tay trái** — không phải T-pose.
- Góc three-quarter lộ artifact xám/bạc và vệt tối trên đầu/mặt; specular “plastic/wet” mạnh.
- Stress pose quay `R_UpperArm` làm bbox cạnh phải thu lại; biến dạng không cho tư thế nâng tay sạch (cần Task 02 đo chi tiết).

## 7. Findings (severity chứng + confidence)

| ID | Severity | Finding | Evidence | Root cause (dự kiến) | Confidence |
|---|---|---|---|---|---|
| F1 | **High** | Working `.blend` mất/không giữ được 4 animation actions (Exit rỗng; NLA trống) | `bpy.data.actions` chỉ `Tico_Exit` với 0 fcurves; GLB vẫn có 4 clips | Export/NLA cleanup + Blender 5.3 layered Action không được ghi lại vào `.blend` sau build | **High** |
| F2 | **High** | Weight lệch nặng L/R tay | `L_Hand` ~12 949 verts ảnh hưởng (bbox gần cả thân); `R_Hand` 659 verts, max_w≈0.25 | Automatic Weights trên mesh đã posed + prune 4 influences; bleed sang trái (tay vẫy sculpt) | **High** |
| F3 | **High** | Animation script vẫy **tay phải**, mesh visual vẫy **tay trái** | `build_welcome_wave` dùng `R_*`; render bind thấy left hand raised + book under right | Rig/anim không khớp sculpted handedness | **High** |
| F4 | **Medium** | 1 350 verts weight = 0 (chủ yếu torso/legs band) | Histogram + z-bands | Auto-weight gaps sau limit-4 | **Medium-High** |
| F5 | **Medium** | Material `blend_method=HASHED` / `DITHERED` dù Alpha=1 | `list_materials` / principled alpha | Tripo import defaults → EEVEE dither + rủi ro transparent sorting nếu không force opaque (JS đã mitigate) | **High** |
| F6 | **Medium** | Specular/plastic blowout trên fur & hoodie | Preview PNGs | Roughness/metal từ RM map + studio lights; JS đã clamp metalness/roughness một phần | **Medium** |
| F7 | **Medium** | Artifact xám/vệt trên đầu ở góc nghiêng | three_quarter previews | Có thể normals/overlapping/decimate UV + HASHED shading; chưa loại trừ geometry thật | **Medium** |
| F8 | **Low** | 8 degenerate faces; mesh mở (boundary lớn) | `analyze_game_asset` topology | Scan/decimate Tripo — thường gặp; chưa chứng minh tear runtime | **Medium** |
| F9 | **Low** | `neutral_bone` trong GLB | glTF skin joints 19 | Export glTF phụ; thường vô hại nếu không bind | **High** |
| F10 | **Info** | Gate PASS chỉ đo torso drift khi raise **R** arm | `gate.json` torso_drift 0.01042 | Gate không bắt handedness mismatch hay L_Hand bleed | **High** |

## 8. QA snapshot (Task 01 — chưa Browser QA)

| Tiêu chí | Trạng thái | Ghi chú |
|---|---|---|
| Mesh integrity | **FAIL** (partial) | Degenerate + open boundaries + nghi ngờ artifact đầu |
| Weight painting | **FAIL** | L/R asymmetry + zero weights |
| Arm deformation | **FAIL** | Stress R-arm không cho raise sạch |
| Shoulder deformation | **FAIL** | Cần Task 02 đo tiếp |
| Wrist deformation | **FAIL** | R_Hand weight yếu |
| Material | **WARN** | HASHED + plastic sheen |
| Brightness | **WARN** | Studio OK nhưng specular cao |
| Texture | **PASS** (packed) | 2048 maps present |
| Animation smoothness | **UNKNOWN** in blend / **UNVERIFIED** in browser | Clips chỉ chắc trong GLB |
| Browser rendering | **NOT RUN** | Thuộc Task 06 |

## 9. Đề xuất Task 02 (Geometry & Rigging Diagnosis)

Thứ tự ưu tiên:

1. **Handedness & clip audit:** Import runtime GLB vào scene phụ; scrub từng clip; xác nhận tay nào thực sự animate; đo drift/intersection từng frame wave.
2. **Weight diagnosis map:** Heatmap `L_Hand`/`R_Hand`/`R_ForeArm` bleed; liệt kê vùng zero-weight; so với limit-4 histogram.
3. **Geometry hotspots:** Inspect degenerate 8 faces + vùng đầu/tai có artifact góc nghiêng; normals; overlapping.
4. **Blend vs GLB divergence:** Vì sao `.blend` mất actions trên Blender 5.3; đề xuất quy trình lưu NLA/actions bền trước Task 05.
5. **Gate gaps:** Mở rộng gate (L arm, shoulder pinch, max influence asymmetry) — chỉ thiết kế, chưa sửa ở Task 02 nếu Task 02 là diagnosis-only.

**Không làm ở Task 02:** redesign nhân vật, đổi màu/trang phục, overwrite `mascot_tct.glb`, export final production.

## 10. Limits / chưa quan sát

- Browser/WebGL playback chưa chạy trong Task 01.
- Stress poses A–H đầy đủ chưa làm (chỉ probe R-arm).
- Không so sánh pixel-level source GLB mesh (262k) vs decimated (65k) ngoài metadata.
- Scene sau audit đang ở checkpoint restore path trong Blender (workspace), không phải file repo.
