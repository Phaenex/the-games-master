# Blender batch: FBX → GLB (handles old FBX 6.1 that Three.js can't load)
# Usage: blender --background --python scripts/fbx-to-glb-blender.py -- <listfile.txt>
import bpy
import sys
from pathlib import Path

argv = sys.argv
args = argv[argv.index('--') + 1:] if '--' in argv else []
listfile = Path(args[0]) if args else Path('/tmp/unity-missing-glb.txt')
root = Path(__file__).resolve().parents[1]
imp = root / 'assets/models/unity-import'
out = root / 'assets/models/unity'

lines = [ln.strip() for ln in listfile.read_text().splitlines() if ln.strip()]
ok = fail = 0
for rel in lines:
    src = imp / rel
    dest = out / Path(rel).with_suffix('.glb')
    if not src.exists():
        fail += 1
        continue
    if dest.exists() and dest.stat().st_size > 64:
        ok += 1
        continue
    dest.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=str(src))
        bpy.ops.export_scene.gltf(filepath=str(dest), export_format='GLB')
        if dest.exists() and dest.stat().st_size > 64:
            ok += 1
            print('OK', dest.stat().st_size, rel)
        else:
            fail += 1
            print('FAIL empty', rel)
    except Exception as e:
        fail += 1
        print('FAIL', rel, e)

print(f'DONE blender ok={ok} fail={fail}')
