# Tico 3D Character Modeling & Realistic Animation — Research Document

**Project:** TCT English  
**Scope:** Research and technical planning only (no product implementation in this phase)  
**Primary deliverable:** This document  
**Official character reference (owner):** `brand/mascot/01-vector/poses/tico-mascot-welcome-color.svg`  
**Status:** Research complete — awaiting owner approval before any modeling, package install, MCP install, or Hero source changes  
**Date:** 2026-11-12  

### Verification legend

| Tag | Meaning |
| --- | --- |
| `[VERIFIED]` | Checked directly against repo source, brand files, or official docs/pages consulted in this research |
| `[UNVERIFIED]` | Not confirmed; must not be treated as fact |
| `[PROPOSAL]` | Technical recommendation for owner decision |
| `[RISK]` | Identified risk |
| `[USER CONFIRMATION]` | Requires explicit owner confirmation before production work |

---

## Chapter 1 — Project Audit

### 1.1 Framework and language

| Item | Finding | Tag |
| --- | --- | --- |
| Application type | ASP.NET Core MVC web app | `[VERIFIED]` |
| Target framework | `net9.0` (`TCTEnglish/TCTEnglish.csproj`) | `[VERIFIED]` |
| Views | Razor `.cshtml` (not React, Next.js, Vue, or SPA shell for Landing) | `[VERIFIED]` |
| Backend language | C# | `[VERIFIED]` |
| Frontend for Landing | HTML + CSS + vanilla JavaScript IIFE | `[VERIFIED]` |
| Broader frontend stack | Bootstrap 5, jQuery (app layouts), SignalR elsewhere; Landing is standalone (`Layout = null`) | `[VERIFIED]` |
| DB / EF | MariaDB via Pomelo; EF Core 9.0.4 — **not in scope** for this 3D Hero research | `[VERIFIED]` |

**Important:** This project must not be treated as a React Three Fiber host by default. `[VERIFIED]`

### 1.2 Project documentation found / not found

| Document | Status | Tag |
| --- | --- | --- |
| `PROJECT_RULES.md` | **Not found** | `[VERIFIED]` |
| `PROJECT_CONTEXT.md` | **Not found** | `[VERIFIED]` |
| `AGENTS.md` | Present — canonical agent/policy instructions | `[VERIFIED]` |
| `README.md` | Present — stack overview (.NET 9, Razor, Bootstrap, MariaDB) | `[VERIFIED]` |
| `DESIGN.md` + `docs/design-system/team-ui-workflow.md` | Present — shared UI rules for later web integration | `[VERIFIED]` |
| `brand/mascot/04-guidelines/*` | Present — persona, colors, expression map | `[VERIFIED]` |
| `docs/TICO_3D_ANIMATION_RESEARCH.md` | This file (created in research phase) | `[VERIFIED]` |

No fabricated substitute was invented for missing `PROJECT_RULES.md` / `PROJECT_CONTEXT.md`.

### 1.3 Frontend architecture (Landing)

Landing is an anonymous marketing page:

- Controller: `HomeController.Landing()` → `return View();` → `Views/Home/landing.cshtml` `[VERIFIED]`
- Styles: `wwwroot/css/landing.css` (scoped `.landing-page` tokens; does not override `tct-learning-ui.css`) `[VERIFIED]`
- Script: `wwwroot/js/landing.js` `[VERIFIED]`
- External CSS: Google Fonts (Be Vietnam Pro, Inter), Font Awesome 6.5.1 CDN `[VERIFIED]`
- Also linked: `ai-chat-launcher.css` `[VERIFIED]`

There is **no** component framework lifecycle on Landing (no React mount/unmount). State is DOM classes + media queries + one IntersectionObserver. `[VERIFIED]`

### 1.4 Existing Hero Section

Structure in `landing.cshtml` (section `.landing-hero`):

1. Background mesh + grid pattern (decorative, `aria-hidden`)
2. Copy column: kicker, H1, lead, CTA buttons
3. Stage (`.landing-hero__stage`):
   - Left card: vocabulary set illustration
   - Center: HTML/CSS smartphone mockup (`.landing-phone`) with flashcard UI chrome
   - Right card: AI chat illustration (uses Tico **head** sticker PNG)
   - Hero mascot: `<img class="landing-tico--hero" … tico-mascot-hero-color.png>`

Phone is **not** a 3D object; it is nested HTML. `[VERIFIED]`

Palette (landing tokens): primary `#4353ff`, accent `#7560f6`, text `#24345b`, surfaces white/light gray. `[VERIFIED]`

### 1.5 Existing mascot implementation

| Location | Asset | Role |
| --- | --- | --- |
| Hero stage | `tico-mascot-hero-color.png` | Decorative portrait, absolute bottom-right of stage |
| Side card AI | `tico-mascot-head-color.png` | Small avatar |
| Progress / CTA sections | success / welcome PNGs | Static illustrations |
| Product UX (app) | welcome / thinking / retry / success per `expression-map.md` | Empty/error/success states |

Hero CSS (desktop):

- `.landing-tico--hero`: `position: absolute; right: 8%; bottom: -2px; z-index: 4;`
- Phone: `z-index: 3`

**Consequence:** Tico currently renders **in front of** the phone, not behind it. `[VERIFIED]`

Mobile (`max-width: 768px`): stage becomes a column; Tico becomes `position: static; order: 3` below the phone — “peek from behind phone” does not map to current layout. `[VERIFIED]`

### 1.6 Animation management today

| Mechanism | Behavior | Tag |
| --- | --- | --- |
| CSS `landing-enter` | Hero copy/stage fade+rise when `data-enter` and `prefers-reduced-motion: no-preference` | `[VERIFIED]` |
| CSS float loops | Side cards + phone float | `[VERIFIED]` |
| `landing.js` | Header scroll class; mobile drawer; smooth hash scroll; IntersectionObserver on `.landing-reveal` (once, then unobserve) | `[VERIFIED]` |
| Reduced motion | `matchMedia('(prefers-reduced-motion: reduce)')` disables smooth scroll + reveal observer path forces visible | `[VERIFIED]` |
| GSAP / Three.js / Lottie | **Not present** on Landing | `[VERIFIED]` |

### 1.7 Dependencies related to 3D / animation

| Package / library | Present on Landing? | Tag |
| --- | --- | --- |
| Three.js | No | `[VERIFIED]` |
| React / R3F | No | `[VERIFIED]` |
| GSAP | No | `[VERIFIED]` |
| GLTF loaders | No | `[VERIFIED]` |
| Draco / Meshopt runtime | No | `[VERIFIED]` |
| NuGet 3D packages | None in `.csproj` | `[VERIFIED]` |

Adding Three.js later would require **owner-authorized** frontend dependency/CDN or wwwroot vendor decision (Program.cs / package policy per `AGENTS.md`). `[PROPOSAL]`

### 1.8 Existing assets inventory

See Chapter 2. Summary: SVG + PNG kit only; **zero** GLB/GLTF/FBX/OBJ/BLEND in repo. `[VERIFIED]`

### 1.9 Project constraints relevant to 3D Hero

From `AGENTS.md` / design system (apply when implementation is later authorized):

- Shared UI foundation: `tct-learning-ui.css` tokens for product UI; Landing uses **scoped marketing tokens** in `landing.css` — keep Landing styling scoped. `[VERIFIED]`
- Multi-platform UI: desktop + mobile in same change when UI is edited. `[VERIFIED]`
- No push/PR/deploy without owner stage authority; research phase creates docs only. `[VERIFIED]`
- Do not hardcode secrets; not applicable to mascot GLB. `[VERIFIED]`
- Stabilization: local research/docs first. `[VERIFIED]`

### 1.10 Repo MCP configuration

`.mcp.json` currently registers only:

- `tct_git`
- `codegraph`

No Blender or Three.js MCP is configured. `[VERIFIED]`

---

## Chapter 2 — Tico Asset Analysis

### 2.1 Official reference

Owner-designated official reference for 3D conversion:

- `brand/mascot/01-vector/poses/tico-mascot-welcome-color.svg`  
- Web/raster siblings: `…/tico-mascot-welcome-color.png`, `brand/mascot/02-raster/poses/welcome/tico-mascot-welcome-color-512.png`

Hero Landing currently uses **`hero`** pose, not `welcome`. `[VERIFIED]`  
Welcome pose is the correct identity/animation reference for greeting + book. `[USER CONFIRMATION]` that hero page should switch visual identity from hero-portrait to welcome-derived 3D.

SVG geometry: `viewBox="0 0 651 768"`, width 651, height 768. `[VERIFIED]`  
File size ~1.5 MB (complex vector). `[VERIFIED]`

### 2.2 Identity features that must be preserved

From owner brief + brand guidelines (`mascot-persona.md`, `color-and-do-dont.md`):

**Head & face**

- Large soft round fox head; orange fur; pointed ears with cream inner ear
- Cream muzzle; small dark nose; large friendly eyes; eyebrows
- Smiling mouth with pink tongue; forehead tuft

**Body & clothing**

- Blue TCT hoodie/shirt with TCT chest mark
- Yellow scarf
- Brown hands; orange legs; brown feet

**Tail**

- Large fox tail; orange body; cream tip — distinctive silhouette

**Accessory**

- Separate blue TCT book with logo — must remain a distinct 3D object

**Owner locks (already confirmed in research brief)**

- Left hand holds book for entire greeting sequence
- Right hand waves
- Do not reverse hands
- Do not invent accessories or change brand marks

### 2.3 Parts to model

| Group | Parts | Notes |
| --- | --- | --- |
| Head | Skull, muzzle, nose, eyes, brows, ears, mouth, forehead tuft | High visual weight |
| Body | Neck, chest, torso, pelvis, arms, hands, legs, feet | Deform with skinning |
| Secondary | Tail, scarf, shirt/hoodie | Scarf may be separate mesh with soft weights |
| Props | Book + TCT logo on book and chest | Book = separate object; logos must stay crisp |

### 2.4 Details not determinable from a single front-ish 2D view

| Detail | Status |
| --- | --- |
| Back of hoodie / hood shape | `[USER CONFIRMATION]` |
| Exact side silhouette (muzzle depth, ear thickness) | `[USER CONFIRMATION]` |
| Tail attachment point and rest curl from side/back | `[USER CONFIRMATION]` |
| Foot sole / shoe-like vs paw pad | `[USER CONFIRMATION]` |
| Book thickness and spine branding | `[USER CONFIRMATION]` |
| How chest “TCT” wraps in 3D (decal vs emboss) | `[USER CONFIRMATION]` |
| Hand digit count / glove style vs paw | `[USER CONFIRMATION]` — stylize consistently with 2D |
| Eye material (toon vs glossy) | `[PROPOSAL]` stylized toon/PBR hybrid matching 2D |

**Rule:** Do not invent unverified back/side details as “official.” Mark unknowns and wait for turnaround approval.

### 2.5 Existing kit (inventory)

**Vector masters** (`brand/mascot/01-vector/`): poses `hero`, `welcome`, `success`, `retry`, `thinking`; stickers head + cheer/cool/peek/sleep/wink + sheet. `[VERIFIED]`

**Raster** (`brand/mascot/02-raster/`): matching 512/256 PNGs. `[VERIFIED]`

**Web copies** (`TCTEnglish/wwwroot/brand/mascot/`): PNG + SVG duplicates for runtime. `[VERIFIED]`

**Guidelines:** persona, color do/don’t, expression map. `[VERIFIED]`

**Source AI:** `brand/mascot/00-source/README.md` states **no Illustrator `.AI` committed**; SVG is Git vector master. `[VERIFIED]`

**Reference board:** `brand/mascot/references/tico-mascot-board.jpg`. `[VERIFIED]`

**3D / animation production files:** none (`.blend`, `.fbx`, `.obj`, `.glb`, `.gltf` search = 0). `[VERIFIED]`

### 2.6 Conversion feasibility

| Path | Assessment |
| --- | --- |
| Direct Image-to-3D as final hero asset | `[RISK]` Poor topology/identity control for brand mascot |
| Hybrid AI blockout + Blender cleanup | `[PROPOSAL]` Recommended |
| Full manual Blender from SVG/PNG | Feasible; slower; highest control |
| Mixamo auto-rig as primary | `[RISK]` Non-humanoid proportions + book prop |

**Verdict for assets alone:** conversion is **feasible with limitations** until turnaround sheets are approved. `[PROPOSAL]`

---

## Chapter 3 — MCP Research

MCP = Model Context Protocol server exposing tools to an AI client.  
**Not** the same as a library, Blender add-on alone, Cursor skill, or npm package used only as a runtime dependency.

**Do not install any MCP in this research phase.** Installation requires later owner approval.

### 3.1 Summary table

| Name | Repository / docs | Author / steward | Purpose | Verified capabilities (from official pages) | Installation (high level) | Cursor compatibility | Dependencies | Security | Limitations | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Blender Lab MCP** | Docs: https://www.blender.org/lab/mcp-server/ ; Source: https://projects.blender.org/lab/blender_mcp | Blender Lab / Blender Foundation project | Natural-language access to Blender Python API + bundled docs search | Add-on in Blender + separate MCP stdio server over TCP; scene analysis, API/manual search tools documented | Clone/setup per Lab wiki; Blender add-on; MCP client config | Stdio MCP clients including Cursor-style `mcpServers` configs (same protocol) | Blender **5.1+**, Python MCP server package, running Blender session | **Official warning:** executes LLM-generated code in Blender **without guards**; use isolated VM / non-sensitive data | Not a modeling magic wand; quality depends on LLM; needs live Blender | **Recommended** for DCC assist during modeling/rig/anim **after** owner approves install, on an isolated machine |
| **Community mcp-for-blender** (repo still named `blender-mcp`) | https://github.com/ahujasid/blender-mcp | Community (ahujasid et al.); **not** Blender Foundation | Control Blender via addon socket + MCP server | Addon socket server + Python MCP server; community docs describe execute/inspect workflows | PyPI / uvx patterns described in repo README; enable addon in Blender | Commonly documented for Cursor `.cursor/mcp.json` / `mcpServers` | Blender + Python env; version coupling varies | Same class: remote code execution into Blender session | Third-party; not official Lab; still not a substitute for artist judgment | **Optional alternative** if Lab MCP Blender 5.1+ is unavailable |
| **mcp-three** | https://github.com/basementstudio/mcp-three | basementstudio | GLTF/GLB → React Three Fiber JSX + structure inspect | `gltfjsx`, `get-model-structure` per README | `npx mcp-three` in MCP config | Yes (stdio) | Node | Local file access to models | Targets **R3F/React** output | **Not recommended** for TCT Landing (no React) |
| **mcp-threejs** | https://github.com/baryhuang/mcp-threejs | baryhuang | Search Sketchfab downloadable models for Three.js | Sketchfab search / GLTF URL helpers (OAuth) | pip / Docker per README | Possible via MCP config | Sketchfab OAuth | Third-party API credentials | Wrong problem (stock models ≠ Tico IP) | **Not recommended** for proprietary Tico |
| **3DAssets.dev MCP** | https://3dassets.dev/docs/mcp ; registry id noted as `dev.3dassets/catalogue` | 3DAssets.dev | Hosted asset catalogue search / CDN URLs | `search_assets`, `get_asset`, loader snippets for three.js etc. | HTTP MCP URL or `npx -y 3dassets-mcp` | Cursor `mcp.json` URL form documented | Network | Public catalogue; license attribution rules | Not for exclusive brand mascot generation | **Not recommended** as Tico source |
| **threejs-devtools-mcp** | https://github.com/dmitriygolub/threejs-devtools-mcp | DmitriyGolub | Live Three.js scene inspect / preview tweaks | Many inspect/live tools claimed in README; verify before relying | npm / MCP registry per project docs | Documented MCP registry presence | Browser/session bridge | Live page instrumentation | Useful only after a Three.js scene exists | **Maybe later** during web integration QA — not for research phase |
| **threejs-mcp (CharlieKerfoot)** | https://github.com/CharlieKerfoot/threejs-mcp | CharlieKerfoot | Three.js code-generation helpers | Scene/camera/geometry/loader snippet tools listed in README | Clone + `uv run` script | Possible | Python/uv | Generates code; review required | Not scene inspection of our Landing; not brand modeling | **Low priority / skip** for Tico identity pipeline |

### 3.2 Groups not found as dedicated MCP

| Search focus | Result |
| --- | --- |
| Dedicated “character facial rig MCP” | **Not found** as a credible standalone MCP in this research pass |
| Dedicated “weight painting MCP” | **Not found** |
| Official Three.js Foundation MCP | **Not found**; only community servers |

If a tool was not verified, it is omitted rather than invented.

### 3.3 Security note (all Blender MCP variants)

`[RISK]` Any Blender MCP that executes generated Python can delete files, exfiltrate scene data, or damage projects. Prefer disposable project files; never point at production secrets or the only copy of brand masters.

### 3.4 Recommendation summary

1. **Use (later):** Blender Lab MCP for assisted Blender workflows.  
2. **Fallback:** Community mcp-for-blender if Lab version constraints block.  
3. **Do not use** catalogue/Sketchfab MCPs as Tico geometry sources.  
4. **Do not use** mcp-three as integration path (R3F).  
5. **Optional later:** threejs-devtools-mcp during Hero WebGL debugging.  
6. **Now:** keep `.mcp.json` unchanged.

---

## Chapter 4 — AI Skills Research

**Skill** = agent instruction package (`SKILL.md` / skill kit) guiding how an AI works.  
Distinct from MCP tools, Blender add-ons, and runtime libraries.

**Do not install external skills in this research phase.**

### 4.1 External skills evaluated

| Name | Repository | Author | Update signal | Install (documented) | Cursor | Helps with | Cannot do | Fit | Risks | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **blender-kit** | https://github.com/Muhrashya/blender-kit | Muhrashya | Repo active (check GitHub for latest commit before install) | `npx skills@latest add Muhrashya/blender-kit -a cursor` (and variants in README) | Documented | Topology budgets, retopo, UV/PBR, rig, anim, glTF/web-three export profiles | Guarantees brand-perfect Tico; does not replace owner art direction | High for Blender production SOPs | May pull community MCP suggestions; review before enabling | **Recommended to evaluate** when modeling phase starts |
| **blender-skills** (AIGC pack) | https://github.com/full-aigc-skills/blender-skills | full-aigc-skills | Check repo for release/version pinning | Host-specific install docs in README (Claude/Codex/Cursor listed) | Claimed | Character rigging/animation, UV/material, export, MCP setup skill modules | Automatic legal/brand compliance | Medium–high if paired with Lab MCP | Heavy pack; quality varies; MCP coupling | **Optional** subset only (rig/anim/export), not whole pack blind install |
| **blender-web-pipeline** (skill doc) | https://github.com/freshtechbro/claudedesignskills/blob/main/.claude/skills/blender-web-pipeline/SKILL.md | freshtechbro / claudedesignskills | Check parent repo | Copy/adapt `SKILL.md` into agent skills dir (manual) | Compatible as markdown skill | glTF/GLB export practices, Draco notes, Three.js load snippets | Create Tico mesh | Medium as checklist | Unofficial; verify against Blender/Three current docs | **Useful as reference checklist**, not sole authority |
| **blendops** | https://github.com/ThanhNguyxnOrg/blendops | ThanhNguyxnOrg | Check repo | Skill ZIP / install scripts documented | Documented targets | Points to Lab MCP + community MCP paths; GLB handoff toolkit claims | Replace Blender artist | Medium as meta-tooling | Large surface; do not treat redirects as verified capability without reading upstream | **Defer** unless team wants standardized Blender agent ops |

### 4.2 In-repo skills (for later web integration only)

| Skill | Path | Relevance |
| --- | --- | --- |
| `tct-ui-component` | `.claude/skills` → `.agents/skills/tct-ui-component` | Landing component patterns when implementing |
| `tct-ui-motion` | same pattern | Motion / reduced-motion |
| `tct-ui-review` | same pattern | Visual QA |
| `tct-superpowers` | planning/verification | Multi-step execution later |
| `design-taste-frontend` | `.agents/skills/design-taste-frontend` | Contains WebGL budget/perf references — **advisory only**; Landing is marketing page with own CSS |

These do **not** create 3D characters. `[VERIFIED]`

### 4.3 Recommendation

- Modeling phase: evaluate **blender-kit** (+ official Blender docs) as primary skill guidance.  
- Web phase: use **tct-ui-*** + DESIGN.md.  
- Do not confuse skills with MCP.  
- No skill install now.

---

## Chapter 5 — 3D Technology Comparison

| Technology | Role | Pros for Tico | Cons / limits | Decision |
| --- | --- | --- | --- | --- |
| **Blender** | Modeling, retopo, UV, texture, rig, weight paint, anim, GLB export | Full control; industry DCC; free | Needs artist time / skill | **Required** — source of truth |
| **Mixamo** | Auto-rig + clip library | Fast humanoid motion | Tico is stylized fox, big head, scarf, book; Mixamo humanoid assumption fails | **Reject as primary**; optional humanoid timing reference only |
| **AI Image-to-3D (e.g. Meshy)** | Fast mesh blockout from images | Speed; GLB export exists; paid commercial options documented on Meshy help/docs | Topology often non-deformation-ready; identity drift; auto-rig not tailored to book prop / brand logos | **Blockout only** under paid commercial terms if used; always cleanup in Blender |
| **Three.js** | WebGL runtime | Fits vanilla Landing JS; `GLTFLoader`, `AnimationMixer`, lights, shadows | Bundle size; WebGL device variance | **Recommended runtime** |
| **React Three Fiber** | React renderer for Three | DX in React apps | Project Landing is Razor + vanilla JS | **Reject for Landing** |
| **GLTF / GLB** | Delivery format | Skeleton, skins, morphs, animations, materials in one binary | Export settings must be disciplined | **Required delivery** |
| **Draco** | Geometry compression | Smaller downloads | Decoder cost; extra loader setup | **Optional** if mesh size needs it |
| **Meshopt** | Geometry/animation compression | Good for skinned characters | Tooling pipeline (gltf-transform) | **Preferred compression path** `[PROPOSAL]` |
| **GSAP** | DOM/timeline tweening | Bubble fade, UI sync | Must not drive bone transforms as fake skeletal anim | **Optional UI only** |

### 5.1 Locked production + runtime stack `[PROPOSAL]`

```text
SVG/PNG refs → (optional) AI Image-to-3D blockout → Blender retopo/UV/texture/rig/anim
  → optimized GLB (clips) → Vanilla Three.js on Landing Hero
  → PNG fallback + prefers-reduced-motion
```

### 5.2 Meshy licensing note (if AI blockout used)

Per Meshy public help/docs consulted in research: paid plans describe private commercial ownership of generated assets; free plan uses CC BY 4.0 attribution. Source image rights remain the user’s responsibility. **Re-verify current terms at purchase time** — terms can change. `[VERIFIED]` as of research reading; `[USER CONFIRMATION]` before any paid tool spend.

---

## Chapter 6 — Character Modeling Plan

### 6.1 Character reference sheet (gate)

Produce and **owner-approve** before final mesh:

| Sheet | Purpose | Status |
| --- | --- | --- |
| Front | Proportions, face, hoodie, scarf, book | Can draft from welcome/hero SVG |
| Left / right side | Depth, muzzle, ear, book thickness | Needs design work + `[USER CONFIRMATION]` |
| Back | Hoodie back, tail, scarf knot | Needs design work + `[USER CONFIRMATION]` |
| Three-quarter | Hero marketing angle | Draft from front + side |
| Neutral A-pose | Rig bind pose | `[PROPOSAL]` prefer A-pose over T-pose for stylized arms |
| Greeting pose | Wave + book | Align with welcome art |
| Book hold close-up | Grip / no clip | Required |

**Inferable from current art:** front colors, hoodie+scarf+TCT mark, ear shapes, smile, book silhouette, overall chibi proportions.  
**Not inferable:** accurate orthographic side/back — do not silently invent as final brand truth.

### 6.2 Modeling workflow `[PROPOSAL]`

1. Collect approved orthographic sheets.  
2. Optional: AI Image-to-3D from approved front (+ multi-view if tool supports) → **blockout only**.  
3. Blender retopology: clean quads, deformation loops at shoulders/elbows/hips/knees/mouth.  
4. Separate book object; keep logo islands readable (dedicated UV).  
5. UV unwrap; bake or paint textures to match 2D kit colors.  
6. Materials: limited set (body, cloth, scarf, eyes, book) to control draw calls.  
7. Export test GLB early (even without anim) for scale checks in a throwaway Three.js page — **after** owner authorizes tooling.

### 6.3 Topology targets `[PROPOSAL]`

| Region | Guidance |
| --- | --- |
| Head | Enough loops for blink/smile; avoid dense sculpt topology in production mesh |
| Body | Game-style deformation topology |
| Hands | Simplified digits sufficient for wave + book grip |
| Tail | Segmentable for bones |
| Budget | Start aiming ~15k–40k tris character+book before compression; adjust after profiling — **not yet measured** |

### 6.4 Materials / textures

- Prefer atlas or few 1k–2k maps over many 4k maps for web. `[PROPOSAL]`  
- Preserve hoodie blue / scarf yellow / fur orange / cream muzzle from kit; prefer sampling SVG/PNG over eye-matched HEX alone (`color-and-do-dont.md`). `[VERIFIED]` guideline  
- Logos: crisp decals; no stretched AI mush.

### 6.5 Accessories

- **Book:** separate mesh; parent/constrain to left hand bone; collision-aware animation.  
- **Scarf:** separate or loosely weighted mesh for secondary motion.  
- **Shirt:** typically body-wear with cloth weights; avoid full cloth sim on web runtime.

---

## Chapter 7 — Rigging Plan

### 7.1 Body rig `[PROPOSAL]`

Bones (minimum useful set):

- `root`, `hips`, `spine`, `chest`, `neck`, `head`
- `shoulder_L/R`, `upper_arm_L/R`, `lower_arm_L/R`, `wrist_L/R`, `hand_L/R` (+ finger bones if wave needs them)
- `upper_leg_L/R`, `lower_leg_L/R`, `ankle_L/R`, `foot_L/R` (+ toe if needed)

FK for staged acting; IK for feet plant and arm wave targeting. Elbow/knee poles required for stable IK.

### 7.2 Facial rig `[PROPOSAL]`

Keep light for web:

- Eye aim bones or morphs  
- Blink (morph or lid bones)  
- Smile / friendly mouth morph  
- Optional brow morph  

**No lip-sync / voice** unless later owner request — brief forbids inventing VO. `[VERIFIED]` from owner brief

### 7.3 Tail rig

Chain of 3–6 bones; soft falloff weights; animated with follow-through on jump/land/idle. `[PROPOSAL]`

### 7.4 Ear rig

Base + tip (or single bone with soft weights) for jump/land reaction. `[PROPOSAL]`

### 7.5 Accessory rig

- Book: child of `hand_L` (or copy-transform constraint); counter-rotate if needed so logo stays readable  
- Scarf: 1–3 bones or soft body-like weighted mesh (exported as skinned, not live cloth sim)

### 7.6 Skinning

- Weight paint carefully at shoulders, hips, scarf junction, tail base  
- Validate with extreme poses (squat, wave, jump apex)  
- Mixamo auto-weights: **not** relied upon as final `[PROPOSAL]`

---

## Chapter 8 — Animation Plan

Owner experience mapped to exportable clips. Naming `[PROPOSAL]`:

| Clip | Phase | Goal | Motion | Parts | Technique | Done when |
| --- | --- | --- | --- | --- | --- | --- |
| `peek` | Hidden / peek | Mostly occluded behind phone; ears/head readable | Slight lean/bob | Head, ears, spine | Small keys; hold book | Silhouette correct behind occluder; no phone clipping |
| `anticipate` | Anticipation | Prep jump right | Crouch, COM down, arms counter, book stays | Legs, hips, spine, arms, tail | Anticipation + squash (mild) | Clear crouch readable at Hero scale |
| `jump` | Jump out | Arc to right of phone | Launch, airborne arc, tucked legs | Full body + tail/ears secondary | Ballistic arc + overlapping action | Leaves occluder cleanly; no rigid root-only translate without body anim |
| `land` | Landing | Plant & absorb | Feet contact, knee bend, hip settle, follow-through | Legs, hips, spine, head, tail, ears | Timing/spacing + settle | No foot through floor; book no clip; recover to stand |
| `greet_wave` | Greeting | Face user, wave right, hold book left | Head tilt, smile, blink, right arm FK/IK wave with wrist/fingers | Face, right arm, left arm hold, tail idle | Overlap on wave; not whole-arm spin | Wave loops or holds naturally; identity readable |
| `idle` | Idle | Rest companion | Breath, blink, soft tail, occasional head tilt | Spine, head, eyes, tail | Looping cycle | Seamless loop; no re-jump |

**Speech bubble (DOM, not 3D):** suggested copy from brief — `Xin chào! Chào mừng bạn đến với TCT English!` — `[USER CONFIRMATION]` for final wording/language.

**Sequencing on web:** play once `peek→anticipate→jump→land→greet_wave` then crossfade to looping `idle`. Do not restart on scroll. `[PROPOSAL]`

---

## Chapter 9 — Web Integration Plan

### 9.1 Architecture `[PROPOSAL]`

**Hybrid compositing (chosen):**

1. Keep Landing copy, CTAs, side cards as DOM.  
2. Replace hero Tico `<img>` with a WebGL canvas bound to `.landing-hero__stage` (or a dedicated `.landing-tico-stage` region).  
3. In Three.js scene: Tico GLB + **phone occluder** (simple box/plane matching phone screen bounds) so Tico can start **behind** and jump out with real depth testing.  
4. CSS phone mockup may remain as visual chrome **camera-aligned** to the occluder, **or** be simplified into the 3D scene — owner chooses (see Ch.13). Prefer true depth over CSS `z-index` fakery.  
5. Lighting: soft hemisphere + key light matching blue/white marketing look; contact shadow optional.  
6. Fallback: existing PNG if WebGL unavailable or reduced motion.

**Rejected alone:** CSS z-index stacking of 2D image “behind” phone while claiming 3D character — violates owner brief.

### 9.2 Why not R3F

Landing is Razor + vanilla JS without React. Introducing React only for the mascot is disproportionate. `[VERIFIED]` architecture + `[PROPOSAL]` decision.

### 9.3 Trigger & state machine `[PROPOSAL]`

States: `unmounted → poster → loading → ready → playing → idle → disposed`

| Event | Behavior |
| --- | --- |
| Hero intersects viewport | Start lazy load GLB (IntersectionObserver; reuse pattern from `landing.js`) |
| Load success + motion OK | Play greeting sequence once |
| Session flag | `sessionStorage` key e.g. `tct-tico-greeted=1` — skip to idle/poster on revisit same tab session |
| Scroll away/back | Do **not** replay full jump |
| `prefers-reduced-motion: reduce` | Show static welcome/hero PNG; no WebGL anim |
| WebGL fail | Static PNG |
| Page hide / teardown | Cancel RAF; dispose geometries/materials/renderer |

Component lifecycle on Landing = script init on DOMContentLoaded equivalent (current IIFE), not React effects.

### 9.4 Responsive `[PROPOSAL]`

| Breakpoint (current CSS) | Hero layout fact | 3D plan |
| --- | --- | --- |
| Desktop wide | Grid: cards + phone; Tico absolute over stage | Full peek→jump→greet→idle |
| ≤1100px | Narrower stage | Same sequence; scale camera/character |
| ≤768px | Column; Tico static below phone | **Reduced:** static PNG or short idle-only — full behind-phone jump fights layout |
| ≤480px | Further tighten | Prefer static / very light idle |

Do not assume desktop animation ports unchanged to mobile. `[VERIFIED]` layout conflict.

### 9.5 Accessibility

- Honor `prefers-reduced-motion` (already used on Landing). `[VERIFIED]` pattern exists  
- No autoplay voice.  
- Decorative canvas: `aria-hidden="true"` with meaningful static `img` alt available for fallback.  
- Do not cover CTA hit targets; keep pointer-events none on canvas if non-interactive. `[PROPOSAL]`  
- Speech bubble must not trap focus; pure visual.

### 9.6 Antiforgery / auth

Landing is `[AllowAnonymous]` GET content; GLB is static wwwroot — no new authenticated API required for playback. `[VERIFIED]` for Landing action; future upload pipelines would need separate security review.

---

## Chapter 10 — Performance Plan

### 10.1 Asset optimization `[PROPOSAL]`

| Lever | Guidance |
| --- | --- |
| Polycount | Retopo for web; avoid sculpt density |
| Textures | Prefer ≤2k; compress (WebP/KTX2 if pipeline supports; else optimized PNG/JPEG in GLB) |
| Materials | Few unique materials |
| Skeleton | Avoid excessive bones; trim unused |
| Clips | Only needed actions; remove scrub curves |
| Compression | Meshopt first; Draco if still large |
| Delivery | Single GLB under approximate **1.5–2.5 MB** goal before claiming success — **measure later** |

### 10.2 Loading strategy `[PROPOSAL]`

1. Paint CSS layout + PNG poster immediately.  
2. Lazy-load Three.js module + GLB when Hero near viewport.  
3. Do not block LCP text/CTA.  
4. Show greeting only after decode + first frame ready (or crossfade from poster).

### 10.3 Rendering optimization `[PROPOSAL]`

- Cap `pixelRatio` (e.g. `Math.min(devicePixelRatio, 1.5–2)`)  
- Pause RAF when tab hidden or hero off-screen after idle settled (optional)  
- One WebGL context for Hero only  
- Dispose on long navigation away if SPA-like behavior appears later (today full page loads)

### 10.4 Mobile

- Prefer fallback/static on low-end or ≤768px unless profiling proves headroom.  
- Target 30–60 FPS when 3D enabled — **unverified until tested**.

### 10.5 Acceptance targets (to measure in Phase 9 — not claimed now)

| Metric | Target |
| --- | --- |
| Desktop FPS during idle | Aim 60 |
| Mobile FPS (if 3D on) | Aim 30–60 |
| Main thread blocking on first load | Avoid long tasks from sync parse; use async load |
| Layout shift | Poster sized to final canvas box |

---

## Chapter 11 — Risk Assessment

| Risk | Tag | Mitigation |
| --- | --- | --- |
| Single 2D view → wrong 3D volumes | `[RISK]` | Mandatory turnaround approval |
| Brand identity drift (colors, logo, proportions) | `[RISK]` | Checklist vs SVG; reject AI finals |
| Bad topology → broken deformation | `[RISK]` | Retopo + deformation tests before anim polish |
| Rig complexity vs web budget | `[RISK]` | Minimal viable bone set |
| Animation looks robotic | `[RISK]` | Anticipation, overlap, secondary motion reviews |
| CSS phone vs 3D depth mismatch | `[RISK]` | Occluder alignment tests; or unify phone in 3D |
| WebGL unsupported / flaky GPUs | `[RISK]` | PNG fallback |
| Perf regressions on Landing | `[RISK]` | Lazy load, budgets, mobile reduce |
| MCP executes unsafe Blender Python | `[RISK]` | Isolated machine; no secrets; disposable `.blend` |
| Mixamo misuse | `[RISK]` | Do not use as primary auto-rig |
| Scope creep (VO, lip-sync, physics cloth) | `[RISK]` | Explicit non-goals until requested |
| Mobile layout cannot show behind-phone beat | `[RISK]` | Separate mobile treatment |
| Commercial AI tool terms change | `[RISK]` | Re-read license at time of use |

---

## Chapter 12 — Implementation Roadmap

No calendar estimates (staffing/tooling unknown). Each phase lists goal, work, tech, inputs, outputs, dependencies, risks, acceptance.

### Phase 1 — Project & Asset Audit

1. **Goal:** Confirm codebase + asset ground truth.  
2. **Work:** Done in this document’s Ch.1–2.  
3. **Tech:** Repo inspection.  
4. **Inputs:** Source tree, brand kit.  
5. **Outputs:** Audit + inventory + missing info list.  
6. **Deps:** None.  
7. **Risks:** Stale assumptions if Landing changes later — re-audit before coding.  
8. **Acceptance:** Framework/Hero/mascot/deps accurately recorded with `[VERIFIED]` tags.

### Phase 2 — Tool & Technology Selection

1. **Goal:** Owner-approve stack + MCP/skills.  
2. **Work:** Review Ch.3–5 recommendations.  
3. **Tech:** Blender, optional Meshy, Three.js, Meshopt.  
4. **Inputs:** This research.  
5. **Outputs:** Written stack decision.  
6. **Deps:** Phase 1.  
7. **Risks:** Premature MCP install.  
8. **Acceptance:** Owner sign-off on Hybrid + vanilla Three.js; install list explicit.

### Phase 3 — Character Reference Design

1. **Goal:** Approved turnaround + poses.  
2. **Work:** Draw/generate sheets; mark unknowns.  
3. **Tech:** Illustrator/SVG/Blender grease or 2D tools.  
4. **Inputs:** Welcome SVG + guidelines.  
5. **Outputs:** Front/side/back/¾/A-pose/greeting sheets.  
6. **Deps:** Phase 2 decision.  
7. **Risks:** Inventing back detail.  
8. **Acceptance:** Owner written approval of sheets.

### Phase 4 — 3D Modeling

1. **Goal:** Deformation-ready Tico + book.  
2. **Work:** Blockout → retopo → UV → textures.  
3. **Tech:** Blender; optional AI blockout.  
4. **Inputs:** Approved sheets.  
5. **Outputs:** `.blend` + textured meshes; test GLB.  
6. **Deps:** Phase 3 gate.  
7. **Risks:** Identity drift.  
8. **Acceptance:** Side-by-side vs reference; clean topology checklist.

### Phase 5 — Rigging

1. **Goal:** Body/face/tail/ear/book controls.  
2. **Work:** Armature, constraints, weights.  
3. **Tech:** Blender rigging.  
4. **Inputs:** Final mesh.  
5. **Outputs:** Rigged `.blend`.  
6. **Deps:** Phase 4.  
7. **Risks:** Weight artifacts; book detach.  
8. **Acceptance:** Extreme pose tests; book stays glued; blink works.

### Phase 6 — Animation Production

1. **Goal:** Full greeting clip set.  
2. **Work:** Keyframe clips per Ch.8.  
3. **Tech:** Blender NLA/actions.  
4. **Inputs:** Rig.  
5. **Outputs:** Named actions.  
6. **Deps:** Phase 5.  
7. **Risks:** Robotic motion; mobile readability.  
8. **Acceptance:** Animation principles checklist; no continuous re-jump.

### Phase 7 — GLB Export & Optimization

1. **Goal:** Web-ready binary.  
2. **Work:** Export, compress, clip prune, size report.  
3. **Tech:** glTF exporter, gltf-transform (Meshopt/Draco).  
4. **Inputs:** Animated blend.  
5. **Outputs:** `tico-welcome.glb` (+ report).  
6. **Deps:** Phase 6.  
7. **Risks:** Broken skins on export; huge files.  
8. **Acceptance:** Loads in Three.js editor/viewer; clips listed; size within agreed budget.

### Phase 8 — Web Integration

1. **Goal:** Hero greeting experience.  
2. **Work:** Canvas host, loader, occluder, state machine, bubble DOM, fallback.  
3. **Tech:** Three.js on Landing; extend `landing.js` / CSS.  
4. **Inputs:** Optimized GLB; owner UI authority.  
5. **Outputs:** Integrated Landing Hero.  
6. **Deps:** Phase 7 + UI authority (DESIGN.md / multi-platform).  
7. **Risks:** Occlusion misalignment; CTA occlusion.  
8. **Acceptance:** Behind-phone → jump → wave → idle once/session; a11y fallback works.

### Phase 9 — Responsive & Performance Testing

1. **Goal:** Evidence on real devices.  
2. **Work:** Desktop/tablet/mobile; FPS; load; reduced motion.  
3. **Tech:** Browser profiling.  
4. **Inputs:** Integrated build.  
5. **Outputs:** FPS/load notes.  
6. **Deps:** Phase 8.  
7. **Risks:** Low-end Android.  
8. **Acceptance:** Targets measured (pass/fail recorded honestly).

### Phase 10 — Final QA

1. **Goal:** Ship-ready sign-off.  
2. **Work:** Identity, animation, integration, performance checklists.  
3. **Tech:** Manual QA.  
4. **Inputs:** Phases 8–9 evidence.  
5. **Outputs:** Acceptance report.  
6. **Deps:** Phase 9.  
7. **Risks:** Last-minute brand nits.  
8. **Acceptance:** Owner approval; no silent scope expansions.

---

## Chapter 13 — Final Recommendation

### 13.1 Feasibility

**Feasible with limitations.**

Limitations:

- No existing 3D/rig/animation assets  
- Side/back turnarounds not yet approved  
- Current Hero layout places 2D Tico in front of phone and demotes Tico on mobile  
- No Three.js dependency yet — integration needs later owner authority  

### 13.2 Recommended technology

| Layer | Choice |
| --- | --- |
| DCC | Blender |
| Blockout (optional) | AI Image-to-3D under commercial terms, then discard topology for retopo |
| Auto-rig SaaS | Not Mixamo-primary |
| Delivery | GLB + Meshopt (Draco optional) |
| Runtime | Vanilla Three.js (`GLTFLoader`, `AnimationMixer`) |
| UI orchestration | CSS/DOM + optional GSAP for bubble only |
| R3F | No |

### 13.3 MCP to use (later)

- Primary: **Blender Lab MCP** (isolated environment)  
- Fallback: community **mcp-for-blender**  
- Skip catalogue/Sketchfab MCPs for Tico mesh  
- Optional later: threejs-devtools-mcp  

### 13.4 Skills to use (later)

- Evaluate **blender-kit** for production SOPs  
- Use in-repo **tct-ui-*** for Landing integration  
- Treat blender-web-pipeline docs as export checklist reference  

### 13.5 Modeling / rigging / animation process

Hybrid reference-locked pipeline in Ch.6–8; book always left-hand constrained; right-hand natural wave; clips `peek → anticipate → jump → land → greet_wave → idle`.

### 13.6 Web integration architecture

Hybrid DOM + WebGL with **3D phone occluder** for true behind-phone start; once-per-session greeting; PNG + reduced-motion fallbacks; mobile reduced experience.

### 13.7 Why these choices

- Matches **verified** ASP.NET Razor/vanilla stack  
- Protects **brand identity** better than AI-final or Mixamo-final  
- Satisfies owner animation quality bar (skeletal, not CSS translate puppet)  
- Reuses Landing’s existing IntersectionObserver / reduced-motion patterns  

### 13.8 Items needing user confirmation

1. Approve **Hybrid** production path (default) or override (manual-only / AI-first / external artist).  
2. Approve **side/back/turnaround** sheets before final modeling.  
3. Confirm **left book / right wave** vs any mirrored welcome art interpretation.  
4. Mobile policy: **static PNG** vs reduced 3D vs attempt full sequence.  
5. Final **speech bubble** copy/language.  
6. Permission to spend on **AI blockout** (paid commercial plan) vs Blender-only.  
7. Available **Blender version** (Lab MCP wants 5.1+) vs community MCP.  
8. Phone representation: **CSS chrome + 3D occluder** vs **full 3D phone**.  
9. Confirm Hero should move from current **hero PNG** to **welcome-derived 3D**.  
10. Authority to add **Three.js** (CDN vs vendored wwwroot) when implementation starts.

### 13.9 Explicit non-goals (this phase)

- No source/CSS/JS edits  
- No package/MCP/skill installation  
- No fake placeholder 3D model claimed as final  
- No VO / lip-sync  

---

## Chapter 14 — References

Sources actually consulted for this research. Each entry notes what it supported.

### 14.1 Project / brand (local)

| Source | Used for |
| --- | --- |
| `AGENTS.md` | Stack boundaries, UI rules, authority gates |
| `README.md` | Published stack description |
| `TCTEnglish/TCTEnglish.csproj` | net9.0; NuGet absences for 3D |
| `TCTEnglish/Controllers/HomeController.cs` | `Landing()` action |
| `TCTEnglish/Views/Home/landing.cshtml` | Hero markup, Tico `<img>`, assets |
| `TCTEnglish/wwwroot/css/landing.css` | z-index, responsive Tico/phone behavior, motion |
| `TCTEnglish/wwwroot/js/landing.js` | IntersectionObserver, reduced motion |
| `.mcp.json` | Existing MCP servers only |
| `brand/mascot/00-source/README.md` | No AI masters committed |
| `brand/mascot/04-guidelines/mascot-persona.md` | Persona / costume |
| `brand/mascot/04-guidelines/color-and-do-dont.md` | Color / do-not rules |
| `brand/mascot/04-guidelines/expression-map.md` | Pose usage map |
| `brand/mascot/01-vector/poses/tico-mascot-welcome-color.svg` | Official welcome reference dimensions |
| `docs/README.md` | Docs folder conventions |

### 14.2 Official product / project pages

| URL | Used for |
| --- | --- |
| https://www.blender.org/lab/mcp-server/ | Official Blender MCP purpose, Blender 5.1+, security warning, install outline |
| https://projects.blender.org/lab/blender_mcp | Official source layout, addon + stdio server architecture |
| https://projects.blender.org/lab/blender_mcp/wiki/Setup | Setup/clone instructions overview |
| https://docs.meshy.ai/en | Meshy product/export overview |
| https://docs.meshy.ai/en/webapp/pricing | Credits / plan license table |
| https://help.meshy.ai/en/articles/16102098-can-i-use-meshy-assets-commercially | Commercial use / attribution notes |
| https://3dassets.dev/docs/mcp | Hosted 3DAssets MCP capabilities / Cursor config |

### 14.3 GitHub repositories

| URL | Used for |
| --- | --- |
| https://github.com/ahujasid/blender-mcp | Community Blender MCP (mcp-for-blender) existence & architecture summary |
| https://github.com/basementstudio/mcp-three | GLB→R3F MCP — rejected for stack fit |
| https://github.com/baryhuang/mcp-threejs | Sketchfab search MCP — rejected for IP fit |
| https://github.com/dmitriygolub/threejs-devtools-mcp | Later debugging MCP candidate |
| https://github.com/CharlieKerfoot/threejs-mcp | Codegen MCP — low priority |
| https://github.com/Muhrashya/blender-kit | Agent skills for Blender game/web pipelines |
| https://github.com/full-aigc-skills/blender-skills | Broader Blender skill pack listing |
| https://github.com/freshtechbro/claudedesignskills/blob/main/.claude/skills/blender-web-pipeline/SKILL.md | Blender→glTF→Three export guidance reference |
| https://github.com/ThanhNguyxnOrg/blendops | Meta Blender agent ops / MCP path pointers |

### 14.4 Technical docs / articles

| Topic | Note |
| --- | --- |
| Three.js GLTFLoader / AnimationMixer | Standard Three.js docs to be used at implementation time; not re-copied here |
| Khronos glTF 2.0 | Industry delivery format for skinned meshes + animations |

No research paper was required for this planning pass; none are cited.

---

## Appendix A — Missing information list

1. Approved orthographic turnarounds  
2. Owner stack override (if not Hybrid)  
3. Blender version available on artist machine  
4. Commercial AI tool budget/approval  
5. Mobile 3D policy  
6. Final bubble copy  
7. Three.js distribution method authority  
8. Phone CSS vs 3D ownership decision  

## Appendix B — Identity checklist (for later QA)

- [ ] Head shape / ear / muzzle / tuft match reference  
- [ ] Hoodie blue + yellow scarf + TCT marks correct  
- [ ] Tail orange + cream tip  
- [ ] Book separate, logo readable, left hand  
- [ ] Right hand wave natural  
- [ ] No extra accessories  
- [ ] Colors within brand kit tolerance  

---

*End of research document. Implementation requires explicit owner approval of the next phase.*
