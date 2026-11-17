"""
Bootstrap an empty Tico production scene in Blender.

Does NOT invent final character geometry. Creates collections, a book empty,
and unit scale so artists start from a consistent layout.

Run:
  blender --background --python bootstrap_scene.py
"""

from __future__ import annotations

import os

import bpy


SCENE_NAME = "TicoWelcome"
COLLECTIONS = (
    "CHAR_Tico",
    "PROP_Book",
    "RIG",
    "REF_Images",
    "EXPORT",
)


def ensure_collection(name: str) -> bpy.types.Collection:
    existing = bpy.data.collections.get(name)
    if existing:
        return existing
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = SCENE_NAME
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    for name in COLLECTIONS:
        ensure_collection(name)

    # Book placeholder empty (artist replaces with mesh; left-hand attachment).
    book = bpy.data.objects.new("PROP_Book_Root", None)
    book.empty_display_type = "CUBE"
    book.empty_display_size = 0.12
    book.location = (-0.25, 0.15, 0.9)
    ensure_collection("PROP_Book").objects.link(book)

    # Root empty for character placement relative to phone occluder on web.
    root = bpy.data.objects.new("CHAR_Root", None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.25
    ensure_collection("CHAR_Tico").objects.link(root)

    # Marker notes as text objects for clip naming.
    notes = bpy.data.objects.new("NOTE_ClipNames", None)
    notes["tico_clips"] = "peek,anticipate,jump,land,greet_wave,idle"
    notes["handedness"] = "left_book_right_wave"
    ensure_collection("EXPORT").objects.link(notes)

    # Save next to this script (not CWD), so background runs are predictable.
    script_dir = os.path.dirname(os.path.abspath(__file__))
    out = os.path.join(script_dir, "tico_welcome_bootstrap.blend")
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print(f"[tico] Saved bootstrap scene to {out}")


if __name__ == "__main__":
    main()
