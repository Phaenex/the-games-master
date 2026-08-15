#!/usr/bin/env python3
"""Rewrites the prologue design data from estate coordinates into village world coordinates.

The prologue was authored on a flat, straight, north-south estate: the car at z=+78, the gate at
z=+65, the mansion at z=-58, the cemetery east of the drive and the garden west of it. The village
we are now building inside sits somewhere else entirely and runs at about 15 degrees off north.

Every gameplay system in the project is one-dimensional along world Z -- GmThreshold compares
player.transform.position.z to gateZ/arrivalZ, GmDesignRuntime fires drive beats on z-crossings.
That still works in the village WITHOUT touching any of those systems, because the village road runs
97% along -Z, so Z stays monotonic from the car to the mansion. Only the numbers have to change.

This runs outside Unity because it rewrites arbitrary JSON, which JsonUtility cannot round-trip. It
invents nothing: the transform and every building footprint are exported from GmVillageDesign
(Unity side), so GmVillageEstate remains the single source of truth for placement.

    python3 scripts/remap-village-design.py

Reads   Assets/StreamingAssets/prologue-design.json
        Screens/DemoScenes/village-transform.json      (written by GmVillageDesign.ExportTransform)
Writes  Assets/StreamingAssets/village-design.json     (loaded by GmDesignRuntime in the village)
        Screens/DemoScenes/village-pois.json           (read back by GmVillageDesign to place markers)
"""
from __future__ import annotations

import json
import math
import sys
import os
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PROJECT = Path(os.environ.get("GM_UNITY_PROJECT", REPO_ROOT / "unity-project"))
SOURCE = PROJECT / "Assets/StreamingAssets/prologue-design.json"
TRANSFORM = PROJECT / "Screens/DemoScenes/village-transform.json"
OUT_DESIGN = PROJECT / "Assets/StreamingAssets/village-design.json"
OUT_POIS = PROJECT / "Screens/DemoScenes/village-pois.json"

# POIs with a real counterpart in the village get pinned to it. The brief for this whole environment
# pivot was "church -> chapel, houses -> outbuildings"; mapping the chapel by coordinate would drop
# it in a field 40m from the actual church, which defeats the point of moving in here.
# (poi id, building name, metres offset along the cross-road axis to clear the wall)
ANCHORS = {
    "chapel-door": ("SM_Church", 7.5),
    "coach-doors": ("SM_House_02", 7.0),
    "garden-shed": ("SM_House_11", -6.5),
}


def load_transform() -> dict:
    if not TRANSFORM.exists():
        sys.exit(f"missing {TRANSFORM}\nRun GmVillageDesign.ExportTransform in Unity first.")
    return json.loads(TRANSFORM.read_text())


def make_mapper(tf: dict):
    cx, cz = tf["spineCentroid"]
    ax, az = tf["spineAxis"]
    lx, lz = tf["lateralAxis"]

    gate, mansion = tf["anchorGate"], tf["anchorMansion"]
    # Linear map from estate Z onto distance along the village spine, defined by the two placements
    # that were measured from the clearance profile.
    slope = (mansion["t"] - gate["t"]) / (mansion["estateZ"] - gate["estateZ"])

    def estate_z_to_t(z: float) -> float:
        return (z - gate["estateZ"]) * slope + gate["t"]

    def to_village(x: float, z: float) -> tuple[float, float]:
        t = estate_z_to_t(z)
        return (cx + ax * t + lx * x, cz + az * t + lz * x)

    def z_only(z: float) -> float:
        return to_village(0.0, z)[1]

    return estate_z_to_t, to_village, z_only, (lx, lz)


def push_clear(x: float, z: float, buildings: list[dict], lateral, anchored: bool):
    """Nudge a point out of any building footprint it landed inside.

    The estate laid its cemetery and garden out on empty ground. At the same offsets the village has
    actual buildings, so without this an examine prompt can end up inside a wall where the player can
    never reach it. Anchored POIs use a tighter margin because they are MEANT to sit against their
    own building -- they only need clearing out of the others.
    """
    lx, lz = lateral
    for _ in range(12):
        moved = False
        for b in buildings:
            keep = b["radius"] + (1.0 if anchored else 3.0)
            dx, dz = x - b["x"], z - b["z"]
            d = math.hypot(dx, dz)
            if d >= keep:
                continue
            if d < 0.01:
                dx, dz, d = lx, lz, 1.0
            x = b["x"] + dx / d * keep
            z = b["z"] + dz / d * keep
            moved = True
        if not moved:
            break
    return x, z


def remap_rect(rect, to_village):
    """A rect is [xMin, xMax, zMin, zMax]. Both corners go through the map, then it is re-squared,
    because the systems test it component-wise against a position."""
    x0, x1, z0, z1 = (float(v) for v in rect)
    pts = [to_village(x0, z0), to_village(x1, z1), to_village(x0, z1), to_village(x1, z0)]
    xs = [p[0] for p in pts]
    zs = [p[1] for p in pts]
    return [min(xs), max(xs), min(zs), max(zs)]


def main() -> None:
    tf = load_transform()
    _, to_village, z_only, lateral = make_mapper(tf)
    buildings = tf["buildings"]
    by_name = {b["name"]: b for b in buildings}

    doc = json.loads(SOURCE.read_text())
    markers = []

    for poi in doc.get("pois", []):
        anchored = False
        if poi["id"] in ANCHORS:
            name, offset = ANCHORS[poi["id"]]
            b = by_name.get(name)
            if b is not None:
                x = b["x"] + lateral[0] * offset
                z = b["z"] + lateral[1] * offset
                anchored = True
            else:
                print(f"  ! anchor building {name} not found for {poi['id']}; mapping by coordinate")
                x, z = to_village(float(poi["x"]), float(poi["z"]))
        else:
            x, z = to_village(float(poi["x"]), float(poi["z"]))

        x, z = push_clear(x, z, buildings, lateral, anchored)
        poi["x"], poi["z"] = round(x, 3), round(z, 3)
        markers.append({
            "id": poi["id"],
            "verb": poi.get("verb", "Examine"),
            "x": poi["x"], "y": 0.0, "z": poi["z"],
            "radius": float(poi.get("radius", 2.5)),
            "anchored": anchored,
        })
        print(f"  {'ANCHOR' if anchored else 'map   '} {poi['id']:<18} -> ({x:8.2f}, {z:8.2f})")

    for beat in doc.get("beats", []):
        beat["z"] = round(z_only(float(beat["z"])), 3)

    for bb in doc.get("branchBeats", []):
        if isinstance(bb.get("rect"), list) and len(bb["rect"]) == 4:
            bb["rect"] = [round(v, 3) for v in remap_rect(bb["rect"], to_village)]

    doc["walkRects"] = [
        [round(v, 3) for v in remap_rect(r, to_village)]
        for r in doc.get("walkRects", []) if isinstance(r, list) and len(r) == 4
    ]

    world = doc.get("world", {})
    for key in ("spawnZ", "gateZ", "carZ", "arrivalZ", "mansionZ", "doorWallZ"):
        if key in world:
            world[key] = round(z_only(float(world[key])), 3)

    OUT_DESIGN.write_text(json.dumps(doc, indent=2))
    OUT_POIS.write_text(json.dumps({"items": markers}, indent=2))

    anchored_count = sum(1 for m in markers if m["anchored"])
    print(f"\nPASS: {len(markers)} POIs ({anchored_count} anchored), "
          f"{len(doc.get('beats', []))} beats, {len(doc['walkRects'])} walk rects")
    print(f"  -> {OUT_DESIGN}")
    print(f"  -> {OUT_POIS}")
    print(f"  world: gateZ={world.get('gateZ')} arrivalZ={world.get('arrivalZ')} "
          f"carZ={world.get('carZ')} mansionZ={world.get('mansionZ')}")


if __name__ == "__main__":
    main()
