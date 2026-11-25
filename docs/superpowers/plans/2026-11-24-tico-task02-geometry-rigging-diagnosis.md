# Task 02 — Geometry & Rigging Diagnosis

**Date:** 2026-11-24  
**Scope:** Welcome / Tico 3D — đo lường + render + báo cáo. Không repair, không export production, không overwrite asset gốc.  
**Input:** [Task 01 audit](2026-11-24-tico-task01-mcp-audit-report.md)

## 1. Method

| Bước | Công cụ | Ghi chú |
|---|---|---|
| Setup | Copy vào `C:\Users\ASUS\blender-asset-mcp\workspace\tico_diag_task02\` | Repo không đụng |
| MCP | `ping_blender` OK lúc đầu; sau `read_factory_settings` + glTF import fail, **AI Bridge port 9876 chết** | Blender GUI vẫn mở nhưng listener down |
| Clip scrub | Parse binary GLB (Python) + metrics `task02_glb_clip_travel.json` | Import glTF qua MCP fail (Blender 5.3 `bpy.context.object`) |
| Weights / topology / stress A–H | Blender 5.3 CLI `--background` + `task02_diagnose.py` | Fallback hợp lệ khi MCP down |
| Previews | `workspace\previews\tico_t02_*.png` | Đã xem ảnh bind, C_raise90, L_raise90, H_combo |

**Policy handedness (đã chốt):** sculpted **left-hand wave** = nhận diện đúng; animation `R_*` = lệch cần sửa ở Task 03.

## 2. GLB clip scrub (handedness evidence)

Runtime GLB đủ 4 clip: `Tico_Entrance`, `Tico_Welcome_Wave`, `Tico_Idle`, `Tico_Exit` (mỗi clip 54 channels TRS).

Góc quay max từ key đầu (degrees) — `task02_glb_clip_travel.json`:

| Clip | SUM_R (Shoulder+Upper+Fore+Hand) | SUM_L | Bone travel lớn nhất |
|---|---:|---:|---|
| Entrance | 0.03 | 0.03 | Head 14.4° |
| **Welcome_Wave** | **181.51** | **4.03** | **R_UpperArm 89.4°**, R_ForeArm 41.9°, R_Hand 39.2° |
| Idle | 0.05 | 0.05 | Head 2.8° |
| Exit | 0.05 | 0.05 | Head 9.4° |

Mốc Wave (góc từ rest): peak ~frame 26 `R_UpperArm≈89°`; wave 32/38 chủ yếu `R_Hand` / `R_ForeArm`; `L_*` gần như đứng yên.

**Kết luận F3 (confidence High):** clip Welcome_Wave **animate tay phải**, trong khi mesh rest đã sculpt vẫy **tay trái** + sách tay phải. Frontend đang phát clip “vẫy” không khớp tư thế nhận diện.

## 3. Weight heatmap (working blend copy)

Histogram influences (max=4, không >4): `{0:1350, 1:10077, 2:22070, 3:12313, 4:18910}`.

| Group | Verts | Avg | Max | Mass |
|---|---:|---:|---:|---:|
| L_Hand | 12949 | 0.394 | 1.000 | 5102.5 |
| R_Hand | 659 | 0.100 | **0.250** | **65.8** |
| L_ForeArm | 13611 | 0.408 | 0.889 | 5559.2 |
| R_ForeArm | 7137 | 0.219 | 1.000 | 1561.3 |
| L_UpperArm | 15612 | 0.385 | 1.000 | 6006.4 |
| R_UpperArm | 15153 | 0.411 | 1.000 | 6220.6 |

Bleed heuristic (wrong side / midline): `L_Hand` 899/12949 (6.9%); `R_Hand` 0/659.

Zero-weight: **1350** verts; samples tập trung vùng x≈0.18–0.20, y≈−0.19, z≈0.40 (cạnh phải thân / sách-armpit).

**Kết luận F2 (High):** `L_Hand` ôm quá rộng (gần cả thân); `R_Hand` quá yếu (max 0.25) → Wave trên `R_*` không điều khiển bàn tay/sách sạch; raise `L_*` kéo theo bleed.

## 4. Stress poses A–H (+ L_raise90)

Avg vertex drift (model units):

| Pose | torso | r_arm | l_arm | head |
|---|---:|---:|---:|---:|
| A raise30 R | 0.004 | 0.060 | 0 | 0.002 |
| B raise60 R | 0.007 | 0.115 | 0 | 0.003 |
| C raise90 R | 0.010 | 0.166 | 0 | 0.005 |
| D elbow | 0.006 | 0.098 | 0 | 0.002 |
| E wrist | 0.006 | 0.102 | 0 | 0.003 |
| F head | 0.002 | 0.008 | 0.019 | 0.095 |
| G torso twist | **0.104** | 0.183 | 0.178 | **0.282** |
| H combo | 0.020 | 0.169 | 0.022 | 0.056 |
| **L raise90** | **0.064** | 0 | **0.212** | 0.004 |

Gate cũ chỉ PASS torso_drift R-raise (~0.01 < 0.045) — **không bắt** L_raise / G_torso / weight asymmetry.

**Render quan sát (đã mở ảnh):**

- `tico_t02_C_raise90_front.png`: tư thế visual vẫn tay trái giơ (sculpt); artifact trắng trên hoodie.
- `tico_t02_L_raise90_front.png` / `_tq`: **FAIL nặng** — mesh tear, streak vàng/xanh bay, clip đầu/vai.
- `tico_t02_H_combo_tq.png`: pinch vai trái, artifact nhỏ gần tay, specular cao.

## 5. Geometry hotspots

8 degenerate faces (area≈0):

| face_index | center (approx) | Ghi chú vùng |
|---:|---|---|
| 17923–17930 | (−0.32, 0.29, 0.17) | Cạnh trái thấp (gần tay/đuôi trái) |
| 56383–56387 | (0.33, −0.13, 0.60) | Cạnh phải cao (vai/sách) |

- Non-manifold / boundary edges: 22338 (mesh mở — đặc trưng Tripo).
- Head inward-normal samples: ≥30 faces (dot với vector ra ngoài < −0.2) — nghi ngờ liên quan artifact góc nghiêng Task 01.
- Không remesh trong Task 02.

## 6. Lệch blend ↔ GLB (root cause)

**Quan sát file:**

- Runtime GLB: 4 animations đầy đủ, Welcome_Wave có motion R_* rõ.
- Working `.blend`: chỉ còn `Tico_Exit`, `is_action_layered=True`, **`keyed_channels=0`**, `fake_user=False`, NLA trống, active=`Tico_Exit`.

**Đối chiếu pipeline** [`build_dashboard_welcome.py`](../../brand/mascot/05-3d-prep/blender/build_dashboard_welcome.py):

1. `make_action` lần lượt gán `arm.animation_data.action =` từng clip → chỉ action **cuối (`Tico_Exit`)** giữ `users≥1`.
2. Entrance / Wave / Idle trở thành **orphan** (`users=0`) trước `save_as_mainfile` (khoảng dòng 817).
3. Blender purge orphan datablocks khi ghi `.blend` nếu không `use_fake_user` → disk mất 3 clip.
4. In-memory sau save vẫn đủ để `export_glb` (dòng 819) lấy `bpy.data.actions.get(name)` → GLB còn 4 clip.
5. Thêm rủi ro Blender 5.3 **layered Action**: Exit lưu lại nhưng channelbag rỗng (`keyed_channels=0`) — keyframe_insert/`slots` không bền khi reload.

**Confidence:** High cho orphan-without-fake_user; Medium-High cho layered-action empty keys.

### Quy trình bền đề xuất (Task 05 — chưa implement)

1. Sau mỗi `make_action`: `action.use_fake_user = True`.
2. Gắn NLA strip cho đủ 4 clip **trước** `save_as_mainfile` (strip giữ user).
3. Assert `keyed_channels(action) > 0` (API Blender 5.3 channelbag) trước save và sau reopen probe.
4. Save blend **sau** export (hoặc save lần 2) với NLA còn nguyên; không phụ thuộc `animation_data.action` active duy nhất.
5. Optional: verify GLB animation names + non-zero travel trên bone chính sau export.

## 7. Gate design mở rộng (chỉ design)

| Metric | Đề xuất fail nếu |
|---|---|
| `hand_mass_ratio` | `mass(L_Hand)/mass(R_Hand)` ngoài khoảng 0.5–2.0 (hiện ~77.5) |
| `r_hand_max_weight` | max(R_Hand) < 0.85 |
| `zero_weight_pct` | > 1% verts (hiện ~2.1%) |
| `l_raise90_torso_drift` | > 0.045 (hiện 0.064) |
| `l_raise90_visual` | render checksum / bbox spike (tear) |
| `action_integrity_blend` | thiếu 4 tên hoặc keyed_channels==0 |
| `action_integrity_glb` | thiếu clip hoặc Wave SUM_R/SUM_L không khớp handedness policy |
| `shoulder_pinch_proxy` | torso verts gần armpit drift bất thường khi raise 90° |

## 8. QA snapshot (diagnosis)

| Tiêu chí | Status | Evidence |
|---|---|---|
| Mesh integrity | FAIL | 8 degen + head normals + L_raise tear |
| Weight painting | FAIL | L/R hand asymmetry, 1350 zero |
| Arm deformation | FAIL | L_raise90 catastrophic; R wave không khớp sculpt |
| Shoulder | FAIL | pinch + artifacts trong preview |
| Wrist | FAIL | R_Hand max 0.25 |
| Animation blend source | FAIL | actions không bền trong `.blend` |
| Animation GLB channels | PASS (data) | 4 clips; Wave R-driven |
| Browser | NOT RUN | Task 06 |

## 9. Prioritized repair list → Task 03

1. **Weights trước:** thu hẹp `L_Hand`/`L_ForeArm` bleed; tăng `R_Hand` localization OR (ưu tiên theo policy) chuyển Wave sang **L_*** và khóa `R_*` giữ sách.
2. **Handedness:** sửa `build_welcome_wave` theo sculpt left-wave; cập nhật gate.
3. **Action persistence:** fake_user + NLA-before-save + keyed assert (có thể làm sớm trong Task 03 tooling trước Task 05).
4. **Geometry:** sửa 8 degen faces; kiểm tra head normals / overlapping gây artifact góc nghiêng.
5. **Stress re-run** A–H + L_raise sau weight fix; chỉ khi PASS mới sang Material (Task 04) / Animation polish (Task 05).

## 10. Limits

- MCP bridge down giữa Task 02 → không scrub clip bằng AnimationMixer trong Blender GUI; dùng GLB binary + CLI renders.
- Không Browser/WebGL.
- Repo originals không sửa; metrics/previews nằm trong MCP workspace.

## 11. Artifact paths

- Metrics: `C:\Users\ASUS\blender-asset-mcp\workspace\tico_diag_task02\task02_diagnosis_metrics.json`
- GLB travel: `...\task02_glb_clip_travel.json`
- Script: `...\task02_diagnose.py`
- Previews: `C:\Users\ASUS\blender-asset-mcp\workspace\previews\tico_t02_*.png`
