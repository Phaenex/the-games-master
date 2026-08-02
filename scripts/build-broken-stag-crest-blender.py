"""Build Wend Hill's broken-stag crest from the owned Abandoned Asylum antler mount.

Usage:
  blender --background --python scripts/build-broken-stag-crest-blender.py -- source.glb output.fbx

The source contains a coherent wooden shield and a sculpted antler pair. The story requires one
antler to have been deliberately snapped off, so this keeps the right-side base as a blunt stump and
removes its branches. Output is deterministic and may also be .glb for neutral previewing.
"""

import bmesh
import bpy
import os
import sys


argv = sys.argv[sys.argv.index("--") + 1:]
if len(argv) != 2:
    raise SystemExit("expected: source.glb output.fbx|output.glb")

source, output = argv
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)

antlers = next(
    (obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.startswith("Antlers_2")),
    None,
)
if antlers is None:
    raise RuntimeError("Antlers_2 mesh is missing from the owned source")

# Source authoring space is in metres at millimetre-scale coordinates. The shield is centred on X;
# the positive-X antler is the story's broken side. Preserve its base up to 1.55 mm above origin so
# the result reads as a snapped antler rather than a mysteriously one-antlered animal.
mesh = bmesh.new()
mesh.from_mesh(antlers.data)
to_remove = [vertex for vertex in mesh.verts if vertex.co.x > 0.00020 and vertex.co.y > 0.00155]
if not to_remove:
    raise RuntimeError("broken-side selection matched no vertices; source coordinates changed")
bmesh.ops.delete(mesh, geom=to_remove, context="VERTS")
mesh.to_mesh(antlers.data)
mesh.free()
antlers.data.update()
antlers.name = "BrokenStagAntlers"
antlers.data.name = "BrokenStagAntlers"

output_dir = os.path.dirname(output)
if output_dir:
    os.makedirs(output_dir, exist_ok=True)
if output.lower().endswith(".glb"):
    bpy.ops.export_scene.gltf(filepath=output, export_format="GLB", use_selection=False)
else:
    bpy.ops.export_scene.fbx(
        filepath=output,
        path_mode="COPY",
        embed_textures=True,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        # This is a new isolated prop, not one of the legacy models whose root transform must be
        # preserved. Bake the glTF/Y-up conversion into the mesh so Unity receives an identity root.
        bake_space_transform=True,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True,
    )

print(f"[broken-stag] removed {len(to_remove)} right-antler vertices")
print(f"[broken-stag] wrote {output}")
