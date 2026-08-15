#!/usr/bin/env python3
"""Convert one or more FBX files to GLB via Blender (handles Unity FBX Three rejects).

Usage:
  blender --background --python scripts/fbx-blender-to-glb.py -- \\
    --in path/to/file.fbx --out path/to/file.glb
  blender --background --python scripts/fbx-blender-to-glb.py -- \\
    --list /tmp/fbx-list.txt --out-root assets/models/unity/slug --in-root assets/models/unity-import/slug
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


def convert_one(src: Path, dest: Path, keep_name: str | None = None) -> bool:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    dest.parent.mkdir(parents=True, exist_ok=True)
    try:
        bpy.ops.import_scene.fbx(filepath=str(src))
    except Exception as e:
        print(f"FAIL import {src}: {e}", flush=True)
        return False
    if keep_name:
        available = [(obj.name, obj.data.name) for obj in bpy.context.scene.objects if obj.type == "MESH"]
        for obj in list(bpy.context.scene.objects):
            if obj.type == "MESH" and obj.name != keep_name and obj.data.name != keep_name:
                bpy.data.objects.remove(obj, do_unlink=True)
        kept = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        if not kept:
            print(f"FAIL keep-name {keep_name!r}: no matching mesh in {src}; available={available}", flush=True)
            return False
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(dest),
            export_format="GLB",
            export_apply=True,
        )
    except Exception as e:
        print(f"FAIL export {src}: {e}", flush=True)
        return False
    ok = dest.exists() and dest.stat().st_size > 64
    print(f"{'OK' if ok else 'FAIL'} {src.name} → {dest} ({dest.stat().st_size if dest.exists() else 0})", flush=True)
    return ok


def main(argv: list[str]) -> int:
    # Blender puts its own args before "--"
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src")
    ap.add_argument("--out", dest="dest")
    ap.add_argument("--keep-name")
    ap.add_argument("--list")
    ap.add_argument("--in-root")
    ap.add_argument("--out-root")
    args = ap.parse_args(argv)

    if args.src and args.dest:
        return 0 if convert_one(Path(args.src), Path(args.dest), args.keep_name) else 1

    if not (args.list and args.in_root and args.out_root):
        print("Need --in/--out or --list + --in-root + --out-root", flush=True)
        return 2

    in_root = Path(args.in_root)
    out_root = Path(args.out_root)
    ok = fail = skip = 0
    for line in Path(args.list).read_text().splitlines():
        rel = line.strip().replace("\\", "/")
        if not rel:
            continue
        src = in_root / rel
        dest = out_root / Path(rel).with_suffix(".glb")
        if dest.exists() and dest.stat().st_size > 64:
            skip += 1
            continue
        if not src.exists():
            print(f"SKIP missing {src}", flush=True)
            fail += 1
            continue
        if convert_one(src, dest):
            ok += 1
        else:
            fail += 1
    print(f"DONE ok={ok} fail={fail} skip={skip}", flush=True)
    return 0 if fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
