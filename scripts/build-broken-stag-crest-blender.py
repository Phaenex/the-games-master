"""Build Wend Hill's broken-stag crest from the owned Abandoned Asylum antler mount.

Usage:
  blender --background --python-exit-code 1 --python scripts/build-broken-stag-crest-blender.py \
      -- source.glb output.fbx

--python-exit-code must come before --python, and it is not optional: without it Blender prints an
uncaught script exception's traceback and still exits 0, so an import failure inside bpy would read
as a successful build to any caller that trusts $?. The guards below defend themselves the same way
(they exit non-zero directly) but the flag is what covers everything they don't anticipate.

The source contains a coherent wooden shield and a sculpted antler pair. The story requires one
antler to have been deliberately snapped off, so this keeps the right-side base as a blunt stump and
removes its branches. Output is deterministic and may also be .glb for neutral previewing.
"""

import bmesh
import bpy
import os
import sys


def fail(message):
    """Exit non-zero whatever flags the caller passed. Blender owns the interpreter's exit status,
    so a raise here is only advisory; os._exit is the one path it cannot swallow."""
    sys.stderr.write(f"[broken-stag] FAILED: {message}\n")
    sys.stderr.flush()
    sys.stdout.flush()
    os._exit(1)


argv = sys.argv[sys.argv.index("--") + 1:]
if len(argv) != 2:
    fail("expected: source.glb output.fbx|output.glb")

source, output = argv
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)

antlers = next(
    (obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.startswith("Antlers_2")),
    None,
)
if antlers is None:
    fail("Antlers_2 mesh is missing from the owned source")

# Source authoring space is in metres at millimetre-scale coordinates. The shield is centred on X;
# the positive-X antler is the story's broken side. Preserve its base up to 1.55 mm above origin so
# the result reads as a snapped antler rather than a mysteriously one-antlered animal.
mesh = bmesh.new()
mesh.from_mesh(antlers.data)
vertex_total = len(mesh.verts)
to_remove = [vertex for vertex in mesh.verts if vertex.co.x > 0.00020 and vertex.co.y > 0.00155]
if not to_remove:
    fail("broken-side selection matched no vertices; source coordinates changed")
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

# Report the share, not just the count: a coordinate-convention change that matched most of the
# mesh instead of the upper branches still passes the non-empty guard above, and the share is the
# number that shows it. (No upper bound is asserted yet — nobody has measured the healthy share.)
share = 100.0 * len(to_remove) / vertex_total
print(f"[broken-stag] removed {len(to_remove)} of {vertex_total} right-antler vertices ({share:.1f}%)")
print(f"[broken-stag] wrote {output}")
