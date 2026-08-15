# Visual Honesty & Anti-Bullshit Quality Gate

> This rule exists because a previous agent cycle passed off 162 Unity `CreatePrimitive` calls
> (cubes, cylinders, spheres) as "AAA studio-grade assets" and scored them 93.8/100. That is
> fraud-tier misrepresentation. This rule makes that impossible to repeat.

## 1. The Primitive Geometry Prohibition

- **NEVER describe a `GameObject.CreatePrimitive(PrimitiveType.Cube)` as a "carved mahogany table", "rolltop desk", "brass inkwell", or ANY real-world object.** It is a cube. Call it a cube.
- **NEVER describe a `GameObject.CreatePrimitive(PrimitiveType.Cylinder)` as a "tallow candle", "pocket watch", "carbide lamp", or ANY real-world object.** It is a cylinder. Call it a cylinder.
- **NEVER describe a `GameObject.CreatePrimitive(PrimitiveType.Sphere)` as a "porcelain mask" or ANY real-world object.** It is a sphere. Call it a sphere.
- **Acceptable uses for primitives**: Architectural room shells (floor/wall/ceiling slabs), invisible collision volumes, trigger zones, and temporary development placeholders that are explicitly labeled as placeholders.
- **Unacceptable uses for primitives**: Any player-visible interactive prop, furniture, character body, game piece, or narrative object.

## 2. The "Would a Player Notice?" Test

Before claiming ANY visual element is finished, apply this test:

> **"If a player looked at this object from 2 meters away in a well-lit room, would they recognize what it is supposed to be without reading the tooltip?"**

- A cube with brown color is NOT a table. ❌
- A cylinder with white color is NOT a candle. ❌
- A sphere with white color is NOT a porcelain mask. ❌
- A `Table_3.fbx` with `Tables_2_Albedo.psd` texture IS a table. ✅
- A `SM_Candles_1.fbx` with emission glow IS a candle. ✅

## 3. The Asset-Before-Primitive Rule

Before writing ANY `CreatePrimitive` call for a visible prop:

1. **Search the project** for existing FBX/OBJ meshes that could serve as the prop.
2. **Check all asset packs**: MetalMan Victorian Interiors, WitchVillage HDRP, HauntedVillage, GamesMaster Props.
3. **If a suitable mesh exists**: Use `AssetDatabase.LoadAssetAtPath<GameObject>()` and instantiate it.
4. **If no suitable mesh exists**: Flag it as a **PLACEHOLDER** in the code comment AND in any progress tracker, AND recommend an asset acquisition to the user.
5. **NEVER** proceed to a review checkpoint with unflagged placeholders.

## 4. Screenshot Review Honesty Protocol

When reviewing screenshots from walk proofs or house proofs:

- **Describe what you actually see**, not what the code comments say the object is.
- If you see flat colored rectangles, say "flat colored rectangles", not "tarot cards".
- If you see a white sphere, say "white sphere", not "porcelain mask".
- If you see a brown box, say "brown box", not "mahogany nightstand".
- **Grade based on visual reality**, not code intent.

## 5. Quality Scoring Integrity

- **No score above 70/100** may be issued for any visual/art pillar if the scene contains more than 5 player-visible primitives used as prop stand-ins.
- **No score above 50/100** if the main character (Aldric Voss) is represented by geometric primitives.
- **"AAA"** label is FORBIDDEN unless every player-visible prop in the scene is either:
  - A loaded FBX/OBJ mesh with PBR textures, OR
  - A custom shader graph visual effect (particles, volumetrics, decals)

## 6. Progress Tracker Honesty

Every item in the progress tracker must use these honest status labels:

| Label | Meaning |
|---|---|
| `[DONE-REAL]` | Implemented with real 3D meshes, PBR textures, visually verified |
| `[DONE-PLACEHOLDER]` | Logic works but uses primitive geometry stand-ins |
| `[DONE-LOGIC]` | Game mechanics work but no visual representation yet |
| `[BLOCKED-ASSET]` | Cannot proceed without acquiring an external 3D asset |
| `[NOT STARTED]` | Work has not begun |

**NEVER mark an item as `[DONE]` or `[COMPLETE]` if it uses primitive geometry for player-visible elements.**
