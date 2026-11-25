# Task 06 — Browser QA Report

**Date:** 2026-11-24  
**Scope:** Runtime Welcome overlay trên local app (`http://localhost:5100`) sau Task 04.  
**Không làm:** Task 05 animation rewrite; redesign topology facet; overwrite `mascot_tct.glb`; commit/push/PR/deploy.

## 1. Environment

| Item | Value |
|---|---|
| App | Local Release/dev on `:5100` — `/health/ready` = 200 |
| Account | Standard learner (`student@tct.local` / Local Standard) |
| Trigger | `sessionStorage` pending hoặc `/Home/Index?welcomeTico=1` (không dùng `/` Landing) |
| Tool | Chrome DevTools MCP (emulate viewport + console/network) |
| Runtime GLB | `tico-dashboard-welcome.glb` ~5.01 MB (5 254 796 bytes) |
| Source | `mascot_tct.glb` SHA256 `B5620826…EE814F` — **unchanged** |

## 2. Playback (1440-class desktop)

Timeline probe (setTimeout ×3 chỉ để quan sát; production không đổi):

| t (ms, slowed) | State |
|---|---|
| ~327 | Overlay `is-active`; `.ai-launcher` = `none`; lock on |
| ~5200 | Bubble visible — `Xin chào Local Standard! Chào mừng bạn quay trở lại TCT English!` |
| ~17800 | `is-fading`; bubble hide (Exit) |
| ~23600 | Overlay hidden; `greeted=1`; launcher `block` again |

Visual (wave frame):

- Full-body framing ổn; camera không nhảy theo pose wave.
- Wave **tay trái**; sách tay phải.
- Material: opaque body (pixel sample α=255 trên head/torso/legs); không void/hollow lớn.
- Specular dịu (Task 04 lights/exposure).
- Facet nhẹ tai–má vẫn thấy — **PARTIAL** (Tripo topology; không tăng exposure).

Clip order theo code + bubble gate: `Tico_Entrance` → `Tico_Welcome_Wave` → `Tico_Idle` → `Tico_Exit`. Timing/crossfade ổn → **không mở Task 05**.

## 3. Skip / session

| Check | Result |
|---|---|
| Skip giữa sequence | Overlay đóng (~fade); `tct-tico-welcome-greeted=1`; launcher hiện lại |
| Reload `/Home/Index` sau greeted | **Không** replay |
| `/Home/Index?welcomeTico=1` | Replay; URL strip query; overlay `is-active` |
| Logout click → `clearWelcomeFlags` | `greeted`/`pending` = null trước khi rời trang |

## 4. Responsive + console

| Viewport | Bubble / Skip | Overflow X | Console error | GLB |
|---|---|---|---|---|
| ~1440 desktop | Bubble phải–trên; Skip góc | OK | 0 | 200 |
| Emulate 768×900 | Bubble tablet phải; không đè mặt | OK (`overflowX=false`) | 0 | cached |
| Emulate 390×844 (DPR2 mobile) | Bubble **trên** (top≈12px); Skip bottom-right trong viewport | OK | 0 | 304 |
| `prefers-reduced-motion: reduce` | Skip im lặng; `greeted=1`; **không** fetch Three/GLB | n/a | 0 | not requested |

Launcher ẩn khi `tico-welcome-lock` trên mọi viewport đã chạy.

## 5. Acceptance

| Tiêu chí | Status |
|---|---|
| 4 clip + L-wave + bubble + Skip | **PASS** |
| Session greeted / welcomeTico replay / logout clear | **PASS** |
| Responsive 1440 / 768 / 390 | **PASS** |
| Console sạch error | **PASS** |
| Material opaque / soft specular (Task 04) | **PASS** |
| Ear/cheek facet-free mọi góc | **PARTIAL** (known Tripo residual) |
| Reduced-motion fallback | **PASS** |
| `mascot_tct.glb` untouched | **PASS** |
| Code fix / Task 05 | **NOT NEEDED** |

## 6. Limits / chưa quan sát

- Không chạy keyboard-only / 200% zoom matrix đầy đủ.
- Không cố tình fail GLB timeout path (chỉ đọc code path `console.warn` + skip).
- Screenshot filePath bị MCP deny ngoài workspace root; bằng chứng bằng inline screenshot + DOM/pixel metrics + network/console.
- Emulate CSS width đôi khi lệch `window.innerWidth` (vd. 768 → inner 853); layout vẫn đo bằng `getBoundingClientRect` vs `innerWidth`.

## 7. Next

Welcome 3D polish chain **Task 01→04 + 06 complete**. Không còn blocker runtime. Facet tai–má chỉ cải thiện nếu sau này rebuild topology (ngoài scope).
