# Prop Grounding, Sizing & Era Authenticity

## 1. Grounding & Placement
- **Terrain Snapping**: All placed props, trees, gravestones, and outbuildings must sample terrain elevation (`SampleHeight`) and raycast to floor geometry.
- **No Floating Geometry**: Disconnected modular ruin pieces (floating ceilings, orphaned upper-story walls) within 18m of the player view corridor must be stripped or properly grounded.
- **Bounding Box Normalization**: Import scale anomalies must be normalized via `GmPropPlacementEngine.NormalizeScale`.

## 2. Period Authenticity (1920s Edwardian/Victorian)
- **Lighting Tools**: Use brass carbide inspection lamps with reflector hoods, kerosene lanterns, and tallow candles. Modern electric flashlights, plastic torches, or LEDs are forbidden.
- **Furnishings**: Dark mahogany, carved oak baseboards, Persian runner rugs, brass drawer pulls, lead crystal clock faces, and vintage "Swan Vestas" wooden matchboxes.
- **Narrative Lore**: Parchment summons letters sealed with crimson wax, inkwell ledgers, and silver pocket watches frozen at 8:59.

## 3. Collision Proxies
- **Convex Hull Collision**: Generate non-inverted, convex box or capsule collision proxies for interactive props and large furniture (e.g. grandfather clocks, bookcases, desks) to prevent player walk-clipping.
- **Trigger Volumes**: Ensure all narrative triggers have `collider.isTrigger = true` so they never act as invisible physical barriers blocking player pathing.

## 4. PBR Materials & Shading Integrity
- **Zero Untextured Fallbacks**: Never leave primitive cubes with default grey/white unlit shaders in playable spaces.
- **HDRP/Lit PBR Standard**: Every interior and exterior surface must use calibrated PBR maps (Albedo, Normal, Smoothness, Metallic) with physically motivated roughness values.
