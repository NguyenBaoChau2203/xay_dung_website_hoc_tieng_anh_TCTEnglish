"""
Print a GLB export checklist and, if a character armature exists, attempt export.

Safe to run in --background. Will not invent a mesh. Exits non-zero if no
exportable armature+mesh is found (expected until modeling is done).
"""

from __future__ import annotations

import sys

import bpy


REQUIRED_ACTIONS = (
    "peek",
    "anticipate",
    "jump",
    "land",
    "greet_wave",
    "idle",
)


def main() -> int:
    print("[tico] GLB export checklist")
    print("  - Applied transforms")
    print("  - Book separate + left hand constrained")
    print("  - Actions:", ", ".join(REQUIRED_ACTIONS))
    print("  - Test in Don McCurdy glTF viewer")

    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    actions = list(bpy.data.actions.keys())

    print(f"[tico] Armatures: {len(armatures)} | Meshes: {len(meshes)} | Actions: {actions}")

    missing = [name for name in REQUIRED_ACTIONS if name not in actions]
    if missing:
        print(f"[tico] Missing actions: {missing}")

    if not armatures or not meshes:
        print("[tico] No exportable character yet — complete modeling/rig/anim first.")
        return 2

    # Prefer exporting selected export collection if present.
    out = bpy.path.abspath("//../../../../TCTEnglish/wwwroot/brand/mascot/3d/tico-welcome.glb")
    bpy.ops.export_scene.gltf(
        filepath=out,
        export_format="GLB",
        export_animations=True,
        export_apply=True,
    )
    print(f"[tico] Exported: {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
