# Unity HDRP & Engine Standards

## 1. Engine Environment
- **Unity Version**: 6000.5.3f1
- **Render Pipeline**: High Definition Render Pipeline (HDRP), Linear Color Space
- **Target Platform**: macOS Standalone (`Wend Hill Prologue.app`), Metal
- **Shaders**: All materials must use `HDRP/Lit` or project-owned custom HDRP shader graphs. Never allow default built-in Standard shaders (which render magenta).

## 2. Motivated Lighting Palettes
- **Victorian Paraffin / Gas Flame**: Point lights at 2100K color temperature (`Color(1.0f, 0.88f, 0.72f)`), soft shadows, controlled lumen intensity, multi-frequency organic Perlin flicker via `GmPeriodLampFlicker`.
- **Tallow Candlestick**: Warm 1800K flame (`Color(1.0f, 0.55f, 0.20f)`), 25 lumens, 3.5m range, soft shadows.
- **Directional Nocturnal Moonlight**: Cold 6800K slate-blue (`Color(0.55f, 0.70f, 0.95f)`), volumetric scattering enabled (`affectsVolumetric = true`, dimmer = 0.55f) to cast moonbeams through forest branches.
- **Shadow Governor**: Enforce concurrent active shadow-casting lights budget to maintain 60+ FPS frame rates.

## 3. Draw Distance & Culling Rules
- **Draw Corridor**: Far clip plane set to 220m; render corridor set to 140m+.
- **Never Disable Outdoor Renderers**: Previous 20m distance disable loops are forbidden. All outdoor estate trees, lamp posts, fences, and props must remain permanently loaded. Room-scoped culling is strictly limited to indoor house spaces (e.g. `HouseBeginning`).
- **Route Clearance**: Avenue corridor must maintain 4.2m clearance from all route waypoints to ensure 0 controller stalls.
