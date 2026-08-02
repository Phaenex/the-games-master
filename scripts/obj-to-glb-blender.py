# Blender: batch OBJ → GLB
# blender --background --python scripts/obj-to-glb-blender.py -- /tmp/gm-legacy-obj <out-root>
import bpy
import sys
from pathlib import Path

argv = sys.argv
args = argv[argv.index('--') + 1:] if '--' in argv else []
src_root = Path(args[0] if args else '/tmp/gm-legacy-obj')
out_root = Path(args[1] if len(args) > 1 else Path(__file__).resolve().parents[1] / 'assets/models/unity')

ok = fail = skip = 0
for obj in sorted(src_root.rglob('*.obj')):
    rel = obj.relative_to(src_root)
    dest = out_root / rel.with_suffix('.glb')
    if dest.exists() and dest.stat().st_size > 64:
        skip += 1
        continue
    dest.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        # Blender 3.x vs 4+/5 API
        try:
            bpy.ops.wm.obj_import(filepath=str(obj))
        except Exception:
            bpy.ops.import_scene.obj(filepath=str(obj))
        bpy.ops.export_scene.gltf(filepath=str(dest), export_format='GLB')
        if dest.exists() and dest.stat().st_size > 64:
            ok += 1
            if ok <= 5 or ok % 25 == 0:
                print('OK', dest.stat().st_size, rel)
        else:
            fail += 1
            print('FAIL empty', rel)
    except Exception as e:
        fail += 1
        print('FAIL', rel, e)

print(f'DONE obj-glb ok={ok} fail={fail} skip={skip}')
