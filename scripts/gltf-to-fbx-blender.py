# scripts/gltf-to-fbx-blender.py
# Headless glTF -> FBX for Unity import. Blender is Z-up and glTF is Y-up; the FBX exporter's
# axis_forward/axis_up below hand Unity a Y-up, -Z-forward model so the native orientation survives
# the round trip. Textures are embedded so the FBX is self-contained inside Assets/.
#
# apply_scale_options MUST be 'FBX_SCALE_UNITS', not left at the exporter's own default
# ('FBX_SCALE_NONE'). Verified empirically (not guessed): with FBX_SCALE_NONE, Blender bakes the
# meters->FBX-native-centimetres factor (100x, from units_blender_to_fbx_factor) into each
# unparented object's Lcl Scaling and declares UnitScaleFactor=1 in the file. Unity's ModelImporter
# did not round-trip that combination correctly -- it imported with fileScale=0.01 and the mansion
# came in at 0.16 x 0.17 x 0.13 instead of ~16 x 17 x 13 (confirmed via ModelImporter.fileScale and
# actual renderer bounds on a rebuilt scene). FBX_SCALE_UNITS instead leaves object transforms
# unbaked and writes the 100x factor directly into the file's UnitScaleFactor property, which Unity
# DOES read correctly: fileScale comes back 1 and the renderer bounds land at 16.00 x 17.12 x 13.18.
# If a future model still imports at 1/100 scale, check this setting first before touching Unity's
# ModelImporter -- fixing it there instead would silently reintroduce the same defect for every
# other model that goes through this script (see Task 4, which reuses it for the gate and car).
#
#   blender --background --python scripts/gltf-to-fbx-blender.py -- <in.gltf> <out.fbx>
import bpy, sys, os

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    path_mode='COPY',
    embed_textures=True,
    axis_forward='-Z',
    axis_up='Y',
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',
    bake_space_transform=False,
    object_types={'MESH', 'EMPTY'},
    use_mesh_modifiers=True,
)

# Report the bbox so the caller can verify orientation survived rather than trusting the exporter.
xs, ys, zs = [], [], []
for o in bpy.context.scene.objects:
    if o.type != 'MESH':
        continue
    for c in o.bound_box:
        w = o.matrix_world @ __import__('mathutils').Vector(c)
        xs.append(w.x); ys.append(w.y); zs.append(w.z)
print(f"[gltf2fbx] BBOX x {min(xs):.2f}..{max(xs):.2f} y {min(ys):.2f}..{max(ys):.2f} z {min(zs):.2f}..{max(zs):.2f}")
print(f"[gltf2fbx] WROTE {dst}")
