# Task 04 — Material & Lighting Report

**Date:** 2026-11-24  
**Scope:** Opaque/runtime material harden, specular soften, lighting tune; rebuild GLB/blend.  
**Không làm:** đổi màu nhận diện; overwrite `mascot_tct.glb`; Animation rewrite; Browser QA đầy đủ.

## 1. Changes

| File | Change |
|---|---|
| [`build_dashboard_welcome.py`](../../brand/mascot/05-3d-prep/blender/build_dashboard_welcome.py) | `soften_tripo_materials` (BLEND+alpha1, metal×0.18, rough max 0.58, specular≤0.28, normal strength≤0.62, no backface cull); keep Tripo custom normals; `force_glb_opaque_materials`; `reinforce_book_hand` |
| [`dashboard-tico-welcome.js`](../../TCTEnglish/wwwroot/js/dashboard-tico-welcome.js) | roughness≥0.62, metalness≤0.05, normalScale≤0.7, DoubleSide, softer key / stronger fill, exposure 0.9 |
| Working `.blend` + runtime GLB | Rebuild + recompress (~5.25 MB) |

**Source:** `mascot_tct.glb` SHA256 unchanged (`B5620826…EE814F`).

## 2. Blender 5.3 note

Assigning `blend_method='OPAQUE'` **silently becomes HASHED** in Blender 5.3.0. Workaround:

- Scene/EEVEE: `BLEND` + `surface_render_method=BLENDED` + alpha=1 (tránh dither HASHED).
- Runtime GLB: post-export force `alphaMode: "OPAQUE"` (verified).

## 3. Normals lesson

Clearing custom split normals **làm xấu** tai/má (lỗ đen lớn). Task 04 **giữ** custom normals; chỉ xóa degenerate + shade smooth.

## 4. Verify renders

`workspace/previews/tico_t04_*.png` (front / front-left / front-right / side / three_quarter + wave).

| So với Task 03 residual | Kết quả |
|---|---|
| Lỗ đen lớn / hollow mesh góc nghiêng | **Cải thiện rõ** (không còn void lớn sau khi giữ custom normals + tắt backface cull) |
| Specular plastic blowout | **Giảm** (rough/metal clamp + soft lights) |
| Facet nhẹ tai–má / AO sâu | **Residual Tripo topology** — vẫn thấy ở góc nghiêng, không che bằng exposure |

## 5. Acceptance

| Tiêu chí | Status |
|---|---|
| GLB `alphaMode=OPAQUE` | PASS |
| Custom normals kept | PASS |
| Specular soften + frontend lights | PASS |
| Identity colors preserved | PASS |
| `mascot_tct.glb` untouched | PASS |
| Ear/cheek fully clean every angle | **PARTIAL** (residual facet Tripo) |
| Browser QA | NOT RUN → Task 06 |

## 6. Next

**Task 06 Browser QA** (playback, responsive, console). Task 05 chỉ nếu cần polish timing clip thêm — animation L-wave đã có từ Task 03.
