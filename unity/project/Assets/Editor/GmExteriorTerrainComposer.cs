using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Deterministic exterior terrain grammar shared by Wend Hill and the non-shipping craft lab.
/// Macro form is authored with explicit masks; this class never scatters arbitrary decoration.
/// </summary>
public static class GmExteriorTerrainComposer
{
    const string AuthoredTerrainPath =
        "Assets/LeartesStudios/WitchVillage/HDRP/Art/Terrain/New Terrain.asset";
    const string GeneratedTerrainPath = "Assets/Scenes/WendHill_AuthoredTerrain.asset";
    const string RoadAlbedoPath =
        "Assets/LeartesStudios/WitchVillage/HDRP/Art/Textures/T_SwampMud_B.PNG";
    const string RoadNormalPath =
        "Assets/LeartesStudios/WitchVillage/HDRP/Art/Textures/T_SwampMud_N.PNG";
    const string AcreageAlbedoPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Muddy_Ground02_B.PNG";
    const string AcreageNormalPath =
        "Assets/LeartesStudios/Abandoned Village/HDRP/Art/Textures/T_Muddy_Ground02_N.png";
    const string MossNormalPath =
        "Assets/LeartesStudios/WitchVillage/HDRP/Art/Textures/T_Moss_N.PNG";
    const float TerrainWorldCenterZ = 15f;

    static TerrainData activeTerrainData;
    static Vector3 activeTerrainOrigin;
    static readonly string[] EstateFoliageVariantNames =
        { "GrassWild", "GrassWheat", "Reed01", "Reed02", "Reed03", "Reed04" };

    /// Delete generated estate children before the environment lab regenerates their source
    /// prefabs. Older versions were accidental Prefab Variants, so deleting the source first made
    /// Unity report six missing parents during test setup. New copies are unpacked, but this ordering
    /// remains the safe clean-build contract and also heals workspaces carrying an older variant.
    public static void ClearEstateTerrainFoliageVariants()
    {
        foreach (string name in EstateFoliageVariantNames)
            AssetDatabase.DeleteAsset($"Assets/Scenes/WendHill_EstateFoliage_{name}.prefab");
    }

    public static float GroundHeight(float x, float z)
    {
        // Batchmode saved-scene audits start in a fresh editor process, so the build-time static
        // cache is empty even though the serialized Ground Terrain is valid. Falling through to the
        // analytical flat-room fallback made the entire 1.82m cemetery hollow look ungrounded and
        // rendered audit-saved unusable. Rehydrate the cache from the scene before sampling.
        if (activeTerrainData == null)
        {
            Terrain savedTerrain = GameObject.Find("Ground")?.GetComponent<Terrain>();
            if (savedTerrain != null && savedTerrain.terrainData != null)
            {
                activeTerrainData = savedTerrain.terrainData;
                activeTerrainOrigin = savedTerrain.transform.position;
            }
        }
        if (activeTerrainData != null)
        {
            float u = Mathf.InverseLerp(activeTerrainOrigin.x,
                activeTerrainOrigin.x + activeTerrainData.size.x, x);
            float v = Mathf.InverseLerp(activeTerrainOrigin.z,
                activeTerrainOrigin.z + activeTerrainData.size.z, z);
            return activeTerrainOrigin.y + activeTerrainData.GetInterpolatedHeight(u, v);
        }

        // Keep authored gameplay rooms level while the acreage rolls outside them. The transition
        // is smooth enough that background props can sit on it without exposing a plane seam.
        float flank = Smooth01(25f, 68f, Mathf.Abs(x));
        float broad = Mathf.Sin(x * 0.071f + z * 0.024f) * 0.72f;
        float cross = Mathf.Sin(x * 0.029f - z * 0.063f) * 0.38f;
        float acreage = flank * (0.36f + broad + cross);

        // Shallow drainage belongs beside the avenue, but the arrival-car pad remains level.
        float edgeDistance = Mathf.Abs(Mathf.Abs(x) - 4.65f);
        float alongDrive = 1f - Smooth01(52f, 64f, Mathf.Abs(z - 20f));
        float ditch = -0.13f * Mathf.Exp(-edgeDistance * edgeDistance / 0.55f) * alongDrive;
        float carPad = 1f - Smooth01(2.2f, 4.8f,
            Vector2.Distance(new Vector2(x, z), new Vector2(-4.8f, 76.5f)));
        ditch *= 1f - carPad;
        return acreage + ditch;
    }

    // Unity's Mathf.SmoothStep interpolates from->to using a normalized t. Scene masks need the
    // shader-style smoothstep(edge0, edge1, value), so normalize explicitly instead of feeding a
    // world-space distance into t (which can amplify a 13cm ditch into tens of metres).
    static float Smooth01(float edge0, float edge1, float value)
    {
        float t = Mathf.InverseLerp(edge0, edge1, value);
        return t * t * (3f - 2f * t);
    }

    public static GameObject BuildEstateGround(Material material)
    {
        TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>(AuthoredTerrainPath);
        if (source != null) return BuildAuthoredEstateTerrain(source);

        Debug.LogWarning($"[GmExteriorTerrain] authored terrain missing at {AuthoredTerrainPath}; " +
            "falling back to the legacy generated mesh");
        activeTerrainData = null;
        activeTerrainOrigin = Vector3.zero;
        const float minX = -75f, maxX = 75f, minZ = -90f, maxZ = 120f, step = 2f;
        int xSteps = Mathf.RoundToInt((maxX - minX) / step);
        int zSteps = Mathf.RoundToInt((maxZ - minZ) / step);
        Mesh mesh = BuildGrid("WendHill_PlayableTerrain", minX, maxX, minZ, maxZ, xSteps, zSteps,
            GroundHeight);
        SaveMesh(mesh, "Assets/Scenes/WendHill_PlayableTerrain.asset");

        var ground = new GameObject("Ground");
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial = material;
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;
        ground.AddComponent<GmSurfaceTag>().Configure(GmSurfaceKind.PackedMud,
            "Weathered estate earth beneath the authored route and exterior rooms.");
        return ground;
    }

    static GameObject BuildAuthoredEstateTerrain(TerrainData source)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var data = UnityEngine.Object.Instantiate(source);
        data.name = "WendHill_AuthoredTerrain";

        float originX = -data.size.x * 0.5f;
        float originZ = TerrainWorldCenterZ - data.size.z * 0.5f;
        float spawnU = Mathf.InverseLerp(originX, originX + data.size.x, 0f);
        float spawnV = Mathf.InverseLerp(originZ, originZ + data.size.z, 72f);
        float normalizedAnchor = source.size.y > 0.001f
            ? source.GetInterpolatedHeight(spawnU, spawnV) / source.size.y : 0f;
        // The purchased heightmap has excellent broad shapes but its demo TerrainData compresses
        // the entire 254m landscape into under two vertical metres. Expand only our clone so the
        // estate has real folds and rises; the central route and structure pads are flattened below.
        Vector3 authoredSize = data.size;
        authoredSize.y = Mathf.Max(5.2f, authoredSize.y);
        data.size = authoredSize;
        float anchorHeight = normalizedAnchor * data.size.y;
        Vector3 origin = new Vector3(originX, -anchorHeight, originZ);

        SculptForEstate(data, source, origin, anchorHeight);
        data.terrainLayers = CloneNightTerrainLayers(source.terrainLayers);

        // Delete by path without first importing the previous generated TerrainData. The generated
        // asset can still reference last build's regenerated foliage prefabs; importing it only to
        // discover whether it exists makes Unity emit a false "Tree prefab ... is missing" error
        // during deterministic rebuild tests. DeleteAsset is already a safe no-op when absent.
        AssetDatabase.DeleteAsset(GeneratedTerrainPath);
        AssetDatabase.CreateAsset(data, GeneratedTerrainPath);
        AssetDatabase.SaveAssets();

        activeTerrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(GeneratedTerrainPath);
        // Unity does not resize the alphamap layer storage immediately after assigning a longer
        // terrainLayers array to an unsaved clone. Painting before this reload silently returned a
        // three-layer buffer, so the new road and every ecological blend serialized as zero. Paint
        // the persisted/reloaded data whose four-layer storage is authoritative.
        PaintForEstate(activeTerrainData, origin);
        BuildEstateTerrainFoliage(activeTerrainData, origin);
        EditorUtility.SetDirty(activeTerrainData);
        AssetDatabase.SaveAssets();
        activeTerrainOrigin = origin;
        var ground = Terrain.CreateTerrainGameObject(activeTerrainData);
        ground.name = "Ground";
        ground.transform.position = origin;
        var terrain = ground.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3f;
        terrain.basemapDistance = 160f;
        terrain.treeDistance = 85f;
        terrain.treeBillboardDistance = 52f;
        terrain.treeCrossFadeLength = 10f;
        terrain.treeMaximumFullLODCount = 1400;
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        ground.AddComponent<GmSurfaceTag>().Configure(GmSurfaceKind.PackedMud,
            "A cloned authored HDRP landscape: swamp mud, dead leaves and desaturated moss shaped around the estate route.");
        Debug.Log($"[GmExteriorTerrain] authored base cloned from {AuthoredTerrainPath}: " +
            $"size={activeTerrainData.size} heightmap={activeTerrainData.heightmapResolution} " +
            $"layers={activeTerrainData.terrainLayers.Length} origin={origin}");
        return ground;
    }

    /// <summary>
    /// Estate-wide transfer of the donor-proven Terrain foliage mechanism. Abandoned Village proves
    /// this family through 23,203 Terrain instances; Wend uses a smaller budget and its own habitat
    /// masks, but it must still read as one continuous piece of land. The old 760-instance proof
    /// occupied under two percent of the Terrain and left every story room on an exposed floor.
    /// This pass samples the complete playable acreage, preserves only real circulation/footprints,
    /// and lets soil, moisture, woodland and land-use fields determine local density.
    /// </summary>
    static void BuildEstateTerrainFoliage(TerrainData data, Vector3 origin)
    {
        GameObject[] foliage = PrepareEstateTerrainFoliage();
        float[] bends = { 0.18f, 0.12f, 0.24f, 0.22f, 0.26f, 0.20f };
        var prototypes = new TreePrototype[foliage.Length];
        for (int i = 0; i < foliage.Length; i++)
            prototypes[i] = new TreePrototype { prefab = foliage[i], bendFactor = bends[i] };
        data.treePrototypes = prototypes;

        var random = new System.Random(202607245);
        var instances = new List<TreeInstance>();
        const int attempts = 165000;
        const int maximumInstances = 7800;
        int reserveRejects = 0, internalGapRejects = 0;
        int vergeInstances = 0, acreageInstances = 0, cemeteryInstances = 0;
        int gardenInstances = 0, workYardInstances = 0, porchInstances = 0;
        for (int attempt = 0; attempt < attempts && instances.Count < maximumInstances; attempt++)
        {
            // Weight most candidates toward land that can contribute to the player's 122m walk,
            // while retaining a wider band to stop side views terminating at a dressed rectangle.
            bool playerAcreage = random.NextDouble() < 0.84;
            float x = playerAcreage
                ? Mathf.Lerp(-58f, 58f, (float)random.NextDouble())
                : Mathf.Lerp(-78f, 78f, (float)random.NextDouble());
            float z = playerAcreage
                ? Mathf.Lerp(-78f, 105f, (float)random.NextDouble())
                : Mathf.Lerp(-94f, 116f, (float)random.NextDouble());
            if (IsEstateFoliageReservedAt(x, z))
            {
                reserveRejects++;
                continue;
            }

            float local = x - DriveCentre(z);
            float routeDistance = Mathf.Abs(local);
            float routeEdge = Mathf.Max(0f, routeDistance - EstateDriveEdge(z));
            float woodland = EstateWoodlandPotential(x, z);
            float broad = FractalHabitatNoise(x - 19f, z + 37f);
            float moisture = Mathf.Clamp01(
                (1f - Smooth01(0.20f, 5.0f, routeEdge)) * 0.58f +
                Mathf.PerlinNoise((x + 41f) * 0.032f, (z - 73f) * 0.041f) * 0.42f);
            float meadow = Mathf.Clamp01(
                Mathf.PerlinNoise((x - 22f) * 0.027f, (z + 51f) * 0.024f) * 0.62f +
                broad * 0.38f);
            float verge = 1f - Smooth01(0.35f, 5.6f, routeEdge);
            float cemetery = EllipseInfluence(x, z, 16.5f, 27.0f, 10.6f, 17.8f);
            float garden = EllipseInfluence(x, z, -16.0f, 25.4f, 12.0f, 15.2f);
            float coach = EllipseInfluence(x, z, -28.5f, 48.0f, 13.4f, 13.8f);
            float chapel = EllipseInfluence(x, z, 32.8f, 29.5f, 11.0f, 12.0f);
            float porch = EllipseInfluence(x, z, 0f, -49f, 19f, 18f);
            float roomRecovery = Mathf.Max(Mathf.Max(cemetery * 0.42f, garden * 0.28f),
                Mathf.Max(Mathf.Max(coach * 0.35f, chapel * 0.30f), porch * 0.30f));

            // The field is continuous but not uniform. Meadows carry overlapping native-scale
            // masses, forest cores thin to leaf floor, verges become wetter/taller, and former work
            // rooms recover at different rates. A low acreage floor prevents the old empty-plane
            // gaps between those stronger communities.
            float acreageFloor = Mathf.Lerp(0.18f, 0.46f, Smooth01(0.18f, 0.78f, meadow));
            float forestFloor = woodland > 0.60f
                ? Mathf.Lerp(0.06f, 0.18f, 1f - woodland)
                : Mathf.Lerp(0.20f, 0.72f, 1f - Mathf.Abs(woodland * 2f - 1f));
            float density = Mathf.Max(acreageFloor * forestFloor,
                Mathf.Max(verge * Mathf.Lerp(0.48f, 0.92f, moisture), roomRecovery));
            density *= Mathf.Lerp(0.64f, 1f, broad);
            if (routeDistance < EstateDriveEdge(z))
            {
                // Sparse, low centre recovery and a few shoulder breaches are enough to break the
                // runway. Do not turn the newly-permitted space between wheel tracks into another
                // continuous lawn.
                float centreRecovery = 1f - Smooth01(0.28f, 0.66f, routeDistance);
                float outerFray = Smooth01(1.55f, EstateDriveEdge(z), routeDistance);
                density *= Mathf.Lerp(0.045f, 0.19f, Mathf.Max(centreRecovery, outerFray));
            }

            // A second octave cuts irregular gaps without isolating the habitat into visible oval
            // stamps. Room recovery resists the cut so cemetery/garden margins do not become bare.
            float gap = Mathf.PerlinNoise((x + 11f) * 0.19f, (z - 67f) * 0.17f);
            if (gap > 0.79f && roomRecovery < 0.55f && random.NextDouble() < 0.72)
            {
                internalGapRejects++;
                continue;
            }
            if (random.NextDouble() > density) continue;

            bool wetEdge = routeDistance > 1.55f && routeEdge < 3.7f && moisture > 0.42f;
            bool workedGround = garden > 0.32f || coach > 0.35f || chapel > 0.42f;
            int prototypeIndex;
            double speciesRoll = random.NextDouble();
            if (cemetery > 0.28f)
                prototypeIndex = speciesRoll < 0.72 ? 0 : speciesRoll < 0.94 ? 1 : 2 + random.Next(4);
            else if (garden > 0.28f)
                prototypeIndex = speciesRoll < 0.20 ? 0 : speciesRoll < 0.88 ? 1 : 2 + random.Next(4);
            else if (wetEdge)
                prototypeIndex = speciesRoll < 0.42 ? 0 : speciesRoll < 0.68 ? 1 : 2 + random.Next(4);
            else if (workedGround)
                prototypeIndex = speciesRoll < 0.30 ? 0 : speciesRoll < 0.90 ? 1 : 2 + random.Next(4);
            else if (woodland > 0.62f)
                prototypeIndex = speciesRoll < 0.77 ? 0 : speciesRoll < 0.94 ? 1 : 2 + random.Next(4);
            else
                prototypeIndex = speciesRoll < 0.53 ? 0 : speciesRoll < 0.90 ? 1 : 2 + random.Next(4);

            // The transfer's first estate pass used donor-scale clumps everywhere. Continuity was
            // solved, but the same pale spiky silhouette then became the subject of every room.
            // Keep the donor mechanism while making tall clumps a minority and giving each land-use
            // zone its own stature. Low wild grass carries cemetery turf and woodland floor; failed
            // grain is strongest in the garden; reeds belong primarily to wet road margins.
            float minScale = wetEdge ? 0.42f : woodland > 0.62f ? 0.24f : 0.34f;
            float maxScale = wetEdge ? 0.86f : woodland > 0.62f ? 0.56f : 0.82f;
            if (cemetery > 0.28f) { minScale = 0.25f; maxScale = 0.58f; }
            else if (garden > 0.28f) { minScale = 0.32f; maxScale = 0.74f; }
            else if (workedGround) { minScale = 0.28f; maxScale = 0.66f; }
            float primaryScale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
            if (prototypeIndex >= 2) primaryScale *= Mathf.Lerp(0.64f, 0.80f,
                (float)random.NextDouble());
            double stature = random.NextDouble();
            if (stature < 0.18) primaryScale *= Mathf.Lerp(0.52f, 0.72f,
                (float)random.NextDouble());
            else if (stature > 0.91) primaryScale *= Mathf.Lerp(1.08f, 1.22f,
                (float)random.NextDouble());
            float value = Mathf.Lerp(0.58f, 0.80f, broad);
            Color colour = wetEdge
                ? new Color(value * 0.64f, value * 0.76f, value * 0.48f, 1f)
                : workedGround
                    ? new Color(value * 0.82f, value * 0.68f, value * 0.38f, 1f)
                    : new Color(value * 0.72f, value * 0.76f, value * 0.44f, 1f);
            instances.Add(new TreeInstance
            {
                position = new Vector3((x - origin.x) / data.size.x, 0f,
                    (z - origin.z) / data.size.z),
                prototypeIndex = prototypeIndex,
                widthScale = primaryScale * Mathf.Lerp(0.78f, 1.24f,
                    (float)random.NextDouble()),
                heightScale = primaryScale * Mathf.Lerp(0.82f, 1.28f,
                    (float)random.NextDouble()),
                rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                color = colour,
                lightmapColor = Color.white,
            });
            if (verge > 0.28f) vergeInstances++;
            else acreageInstances++;
            if (cemetery > 0.20f) cemeteryInstances++;
            if (garden > 0.20f) gardenInstances++;
            if (coach > 0.20f || chapel > 0.20f) workYardInstances++;
            if (porch > 0.20f) porchInstances++;
        }
        int estateCount = instances.Count;
        if (estateCount < 7000 || estateCount > maximumInstances)
            throw new InvalidOperationException($"estate Terrain foliage budget failed: " +
                $"instances={estateCount}");
        data.SetTreeInstances(instances.ToArray(), true);
        data.RefreshPrototypes();
        Debug.Log($"[GmExteriorTerrain] estate-wide donor foliage={estateCount} " +
            $"prototypes={prototypes.Length} verge={vergeInstances} acreage={acreageInstances} " +
            $"cemetery={cemeteryInstances} garden={gardenInstances} workYards={workYardInstances} " +
            $"porch={porchInstances} reserve={reserveRejects} gaps={internalGapRejects}");
    }

    /// Real gameplay negative space. This deliberately does not know about review cameras: a camera
    /// may reveal a defect, but it must never reshape the authored world around itself.
    public static bool IsEstateFoliageReservedAt(float x, float z)
    {
        float local = x - DriveCentre(z);
        // Preserve the two actual wheel/walking tracks, not a ruler-straight room-width carpet.
        // The non-colliding Terrain foliage may recover sparsely in the centre and fray the outer
        // shoulder, which is what lets the road belong to the land without blocking movement.
        if (Mathf.Abs(local - 0.94f) < 0.48f || Mathf.Abs(local + 0.94f) < 0.48f) return true;
        float gateX = x / 4.8f;
        float gateZ = (z - 65f) / 3.2f;
        if (gateX * gateX + gateZ * gateZ < 1f) return true;
        Vector4[] structures = {
            new(0f, -59f, 15.5f, 12.5f), new(33f, 30f, 9.4f, 7.8f),
            new(-33.5f, 50f, 8.0f, 6.8f), new(-20.5f, 17.5f, 5.8f, 5.1f),
            new(-4.8f, 76.5f, 4.4f, 5.8f),
        };
        foreach (Vector4 structure in structures)
            if (InsideEllipse(x, z, structure.x, structure.y, structure.z, structure.w)) return true;
        // Keep only the actual turning/walking core clear. The former 6.2 x 6.5 metre reserve
        // erased all low ecology in the porch camera and made the mansion sit behind a clean dirt
        // forecourt. Foundation dressing owns the outer apron; the two route tracks above and this
        // compact centre reserve preserve traversal without drawing an empty circular room.
        if (InsideEllipse(x, z, 0f, -46f, 3.7f, 4.4f)) return true;

        bool cemetery = x > 7.8f && x < 25.4f && z > 11.8f && z < 41.5f;
        if (cemetery && (Mathf.Abs(x - 16.2f) < 1.42f || Mathf.Abs(z - 28.5f) < 1.18f))
            return true;
        bool garden = x > -24.5f && x < -7.7f && z > 14.8f && z < 35.6f;
        if (garden && (Mathf.Abs(z - 22.1f) < 1.20f ||
            (Mathf.Abs(x + 15.6f) < 0.82f && z > 22f) ||
            Vector2.Distance(new Vector2(x, z), new Vector2(-16f, 27.2f)) < 2.7f))
            return true;
        bool coachLane = x > -40.5f && x < -20.5f && Mathf.Abs(z - 47.2f) < 1.35f;
        bool chapelLane = x > 24.5f && x < 39.5f && Mathf.Abs(z - 29.2f) < 1.30f;
        if (coachLane || chapelLane) return true;
        return false;
    }

    static float EstateDriveEdge(float z)
    {
        return 2.18f + Mathf.Sin(z * 0.061f + 0.4f) * 0.20f +
            Mathf.Sin(z * 0.143f - 0.9f) * 0.10f;
    }

    /// <summary>
    /// Wend owns its colour/exposure calibration instead of borrowing the daylight lab variants
    /// directly. The purchased source prefabs and the reusable environment-lab assets remain
    /// untouched; deterministic project-owned copies receive a warmer, darker, rougher night
    /// palette. Six silhouettes remain available, but their value no longer converges to icy blue.
    /// </summary>
    static GameObject[] PrepareEstateTerrainFoliage()
    {
        GameObject[] sources = GmEnvironmentGrammarLabBuilder.LoadTransferTerrainFoliage();
        string[] names = EstateFoliageVariantNames;
        Color[] tints = {
            new(0.090f, 0.108f, 0.038f, 1f), new(0.145f, 0.108f, 0.034f, 1f),
            new(0.080f, 0.104f, 0.040f, 1f), new(0.125f, 0.092f, 0.030f, 1f),
            new(0.072f, 0.100f, 0.046f, 1f), new(0.132f, 0.104f, 0.040f, 1f),
        };
        float[] intensities = { 0.43f, 0.39f, 0.36f, 0.34f, 0.35f, 0.34f };
        var result = new GameObject[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            string materialPath = $"Assets/Scenes/WendHill_EstateFoliage_{names[i]}.mat";
            string prefabPath = $"Assets/Scenes/WendHill_EstateFoliage_{names[i]}.prefab";
            AssetDatabase.DeleteAsset(prefabPath);
            AssetDatabase.DeleteAsset(materialPath);
            Renderer sourceRenderer = sources[i].GetComponentInChildren<Renderer>(true);
            if (sourceRenderer == null || sourceRenderer.sharedMaterial == null)
                throw new InvalidOperationException($"estate foliage source has no material: {sources[i].name}");
            Material material = i >= 2
                ? BuildNightReedMaterial(sourceRenderer.sharedMaterial,
                    $"WendHill_EstateFoliage_{names[i]}", tints[i] * 1.65f)
                : new Material(sourceRenderer.sharedMaterial)
                {
                    name = $"WendHill_EstateFoliage_{names[i]}",
                    enableInstancing = true,
                };
            if (material.HasProperty("_Albedo_Tint")) material.SetColor("_Albedo_Tint", tints[i]);
            else if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tints[i]);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", tints[i]);
            if (material.HasProperty("_Albedo_Intensity"))
                material.SetFloat("_Albedo_Intensity", intensities[i]);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.025f);
            if (material.HasProperty("_Roughness")) material.SetFloat("_Roughness", 0.96f);
            if (material.HasProperty("_Roughness_Intensity"))
                material.SetFloat("_Roughness_Intensity", 0.96f);
            if (material.HasProperty("_TransmissionEnable")) material.SetFloat("_TransmissionEnable", 0f);
            if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            AssetDatabase.CreateAsset(material, materialPath);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sources[i]);
            instance.name = $"WendHill_EstateFoliage_{names[i]}";
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var replacements = new Material[renderer.sharedMaterials.Length];
                for (int m = 0; m < replacements.Length; m++) replacements[m] = material;
                renderer.sharedMaterials = replacements;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            EnsureRuntimeTerrainPrototypeRoot(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (result[i] == null)
                throw new InvalidOperationException($"failed to create estate foliage variant: {prefabPath}");
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    /// The purchased tall-grass ShaderGraph exposes _BaseMap but no base-colour multiplier; the
    /// earlier generic tint code therefore changed nothing. Once the missing root LODs were fixed,
    /// those plants finally rendered as saturated green tufts. A project-owned HDRP/Lit alpha-cutout
    /// keeps the owned albedo/normal and silhouette while making colour, roughness and emission
    /// explicit under Wend Hill's fixed night exposure. Purchased material assets remain untouched.
    static Material BuildNightReedMaterial(Material source, string name, Color tint)
    {
        Shader lit = Shader.Find("HDRP/Lit");
        if (lit == null) throw new InvalidOperationException("HDRP/Lit unavailable for estate reed material");
        var material = new Material(lit) { name = name, enableInstancing = true };
        Texture albedo = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
        Texture normal = source.HasProperty("_Normal") ? source.GetTexture("_Normal") :
            source.HasProperty("_NormalTex") ? source.GetTexture("_NormalTex") : null;
        if (albedo == null)
            throw new InvalidOperationException($"estate reed source '{source.name}' has no owned albedo texture");
        material.SetTexture("_BaseColorMap", albedo);
        material.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
        if (normal != null)
        {
            material.SetTexture("_NormalMap", normal);
            material.SetFloat("_NormalScale", 0.72f);
        }
        material.SetFloat("_Smoothness", 0.035f);
        material.SetFloat("_AlphaCutoffEnable", 1f);
        material.SetFloat("_AlphaCutoff", 0.36f);
        material.SetFloat("_DoubleSidedEnable", 1f);
        material.SetFloat("_CullMode", 0f);
        material.SetFloat("_CullModeForward", 0f);
        material.SetFloat("_TransmissionEnable", 0f);
        if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.doubleSidedGI = true;
        if (!HDShaderUtils.ResetMaterialKeywords(material))
            throw new InvalidOperationException($"HDRP rejected estate reed material '{name}'");
        material.EnableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_DOUBLESIDED_ON");
        material.renderQueue = 2450;
        return material;
    }

    /// Unity Terrain does not inspect arbitrary child renderers when validating a tree prototype, and
    /// a root LODGroup that only references child renderers is NOT reliably accepted by the built
    /// player either: it silently logs "couldn't be instanced ... no valid mesh renderer" and draws
    /// nothing. The four single-mesh reed sources hit exactly this, and an earlier root-LODGroup
    /// attempt still failed in the player. The robust, always-accepted structure is an explicit
    /// MeshRenderer + MeshFilter on the prototype root. The reed mesh children sit at the prototype
    /// origin as direct children, so hoist the mesh up to the root and drop the redundant child.
    /// The two grass families already own a working multi-LOD root LODGroup and are left untouched.
    static void EnsureRuntimeTerrainPrototypeRoot(GameObject instance)
    {
        // A root renderer or an existing (working, multi-LOD) root LODGroup is already instancable;
        // never disturb the grass families, whose renderers live under a source LODGroup at the root.
        if (instance.GetComponent<Renderer>() != null || instance.GetComponent<LODGroup>() != null)
            return;

        MeshRenderer meshRenderer = instance.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(renderer => renderer.GetComponent<MeshFilter>()?.sharedMesh != null);
        if (meshRenderer == null)
            throw new InvalidOperationException($"terrain prototype '{instance.name}' has no mesh renderer");
        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
        Transform source = meshRenderer.transform;

        // Hoisting only preserves the silhouette when the mesh child is a direct child at the
        // prototype origin. Anything else would shift the prototype, so fall back to a root LODGroup.
        bool canHoist = source != instance.transform && source.parent == instance.transform &&
            source.localPosition == Vector3.zero && source.localRotation == Quaternion.identity &&
            source.localScale == Vector3.one;
        if (!canHoist)
        {
            var lod = instance.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.None;
            lod.animateCrossFading = false;
            lod.SetLODs(new[] { new LOD(0.0135f, new Renderer[] { meshRenderer }) });
            lod.RecalculateBounds();
            return;
        }

        var rootFilter = instance.AddComponent<MeshFilter>();
        rootFilter.sharedMesh = meshFilter.sharedMesh;
        var rootRenderer = instance.AddComponent<MeshRenderer>();
        rootRenderer.sharedMaterials = meshRenderer.sharedMaterials;
        rootRenderer.shadowCastingMode = ShadowCastingMode.Off;
        rootRenderer.receiveShadows = true;
        UnityEngine.Object.DestroyImmediate(source.gameObject);
    }

    static readonly Vector4[] EstateWoodlandMasses = {
        new(-38f,  87f, 23f, 19f), new( 35f,  78f, 24f, 20f),
        new(-48f,  52f, 24f, 23f), new( 48f,  43f, 24f, 22f),
        new(-42f,  13f, 25f, 25f), new( 46f,   5f, 25f, 24f),
        new(-43f, -25f, 25f, 24f), new( 45f, -31f, 26f, 25f),
        new(-29f, -66f, 21f, 18f), new( 30f, -69f, 22f, 19f),
    };

    static float EstateWoodlandPotential(float x, float z)
    {
        float strongest = 0f;
        foreach (Vector4 mass in EstateWoodlandMasses)
        {
            float dx = (x - mass.x) / mass.z;
            float dz = (z - mass.y) / mass.w;
            strongest = Mathf.Max(strongest, Mathf.Exp(-(dx * dx + dz * dz) * 1.32f));
        }
        return Mathf.Clamp01(strongest);
    }

    static float FractalHabitatNoise(float x, float z)
    {
        return Mathf.Clamp01(
            Mathf.PerlinNoise(x * 0.018f, z * 0.020f) * 0.52f +
            Mathf.PerlinNoise((x + 71f) * 0.051f, (z - 29f) * 0.047f) * 0.31f +
            Mathf.PerlinNoise((x - 17f) * 0.113f, (z + 43f) * 0.097f) * 0.17f);
    }

    static float EllipseInfluence(float x, float z, float centreX, float centreZ,
        float radiusX, float radiusZ)
    {
        float dx = (x - centreX) / radiusX;
        float dz = (z - centreZ) / radiusZ;
        return 1f - Smooth01(0.52f, 1.16f, Mathf.Sqrt(dx * dx + dz * dz));
    }

    static bool InsideEllipse(float x, float z, float centreX, float centreZ,
        float radiusX, float radiusZ)
    {
        float dx = (x - centreX) / radiusX;
        float dz = (z - centreZ) / radiusZ;
        return dx * dx + dz * dz < 1f;
    }

    static TerrainLayer[] CloneNightTerrainLayers(TerrainLayer[] sourceLayers)
    {
        // The fourth layer deliberately reuses the surrounding swamp-mud surface with a darker,
        // wetter remap. A directional road photograph made every camera angle reveal repeated
        // crosswise streaks; the broken twin-track paint mask already supplies direction, while a
        // related soil texture lets the lane belong to the same landscape.
        // Keep a separate, project-owned worked-soil treatment. It references the same purchased
        // texture pair as the acreage but compresses the darkest values for old traffic and work
        // zones. Painting this locally is safer than lifting the acreage material globally: the
        // arrival and chapel frames can stay locked while the porch, garden and coach yard recover
        // a readable ground plane.
        var result = new TerrainLayer[sourceLayers.Length + 3];
        for (int i = 0; i < sourceLayers.Length; i++)
        {
            TerrainLayer source = sourceLayers[i];
            if (source == null) continue;
            var layer = UnityEngine.Object.Instantiate(source);
            layer.name = $"WendHill_{source.name}";
            string texture = source.diffuseTexture == null ? "" : source.diffuseTexture.name.ToLowerInvariant();
            if (texture.Contains("moss"))
            {
                // The donor TerrainLayer omitted the normal that ships beside this texture. That
                // made open moss read as one chalky value under the moon even though the albedo was
                // detailed. Wend owns this clone, so reconnect the matching donor normal and keep
                // enough warm/olive separation to say damp turf rather than blue snow.
                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MossNormalPath);
                layer.tileSize = new Vector2(6.4f, 6.4f);
                layer.normalScale = 0.32f;
                layer.diffuseRemapMin = new Vector4(0.010f, 0.010f, 0.004f, 0f);
                layer.diffuseRemapMax = new Vector4(0.175f, 0.175f, 0.070f, 1f);
                layer.smoothness = 0.035f;
            }
            else if (texture.Contains("leaves"))
            {
                layer.tileSize = new Vector2(5.2f, 5.2f);
                layer.diffuseRemapMin = new Vector4(0.014f, 0.006f, 0.002f, 0f);
                layer.diffuseRemapMax = new Vector4(0.360f, 0.160f, 0.055f, 1f);
                layer.smoothness = 0.018f;
            }
            else
            {
                // The Witch swamp albedo is useful on the drive, but repeated over the acreage it
                // loses nearly all middle-value structure. Abandoned Village supplies a matching
                // soil albedo/normal pair with compacted patches and fine clods. Reference those
                // purchased textures from our clone; never alter the source package asset.
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AcreageAlbedoPath);
                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AcreageNormalPath);
                layer.tileSize = new Vector2(7.6f, 7.6f);
                layer.normalScale = 0.36f;
                layer.diffuseRemapMin = new Vector4(0.018f, 0.010f, 0.005f, 0f);
                layer.diffuseRemapMax = new Vector4(0.390f, 0.235f, 0.125f, 1f);
                layer.smoothness = 0.045f;
            }
            string path = $"Assets/Scenes/WendHill_TerrainLayer_{i + 1:00}.terrainlayer";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(layer, path);
            result[i] = layer;
            Debug.Log($"[GmExteriorTerrain] source layer {i}: " +
                $"{(source.diffuseTexture == null ? "<null>" : source.diffuseTexture.name)} -> " +
                $"{(layer.diffuseTexture == null ? "<null>" : layer.diffuseTexture.name)}");
        }
        var road = new TerrainLayer
        {
            name = "WendHill_MuddyRoad",
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RoadAlbedoPath),
            normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RoadNormalPath),
            tileSize = new Vector2(3.8f, 3.8f),
            diffuseRemapMax = new Vector4(0.16f, 0.045f, 0.015f, 1f),
            smoothness = 0.19f,
            normalScale = 0.92f,
        };
        string roadPath = $"Assets/Scenes/WendHill_TerrainLayer_{sourceLayers.Length + 1:00}.terrainlayer";
        if (AssetDatabase.LoadMainAssetAtPath(roadPath) != null) AssetDatabase.DeleteAsset(roadPath);
        AssetDatabase.CreateAsset(road, roadPath);
        result[sourceLayers.Length] = road;

        var workedSoil = new TerrainLayer
        {
            name = "WendHill_WorkedSoil",
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AcreageAlbedoPath),
            normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AcreageNormalPath),
            tileSize = new Vector2(7.6f, 7.6f),
            normalScale = 0.16f,
            diffuseRemapMin = new Vector4(0.052f, 0.031f, 0.016f, 0f),
            diffuseRemapMax = new Vector4(0.270f, 0.162f, 0.085f, 1f),
            smoothness = 0.038f,
        };
        string workedPath = $"Assets/Scenes/WendHill_TerrainLayer_{sourceLayers.Length + 2:00}.terrainlayer";
        if (AssetDatabase.LoadMainAssetAtPath(workedPath) != null) AssetDatabase.DeleteAsset(workedPath);
        AssetDatabase.CreateAsset(workedSoil, workedPath);
        result[sourceLayers.Length + 1] = workedSoil;

        // Dedicated backbuffer soil. Reweighting the existing moss/leaf layers could not remove the
        // blue-white moon response at the Terrain's outer ridge because all three near layers are
        // intentionally readable at player distance. This clone uses the same owned soil albedo but
        // a compressed diffuse range and almost no normal response; it is painted only outside the
        // playable acreage and never touches the route or story rooms.
        var distantSoil = new TerrainLayer
        {
            name = "WendHill_DistantSoil",
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AcreageAlbedoPath),
            normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AcreageNormalPath),
            tileSize = new Vector2(10.5f, 10.5f),
            normalScale = 0.05f,
            diffuseRemapMin = new Vector4(0.004f, 0.003f, 0.002f, 0f),
            diffuseRemapMax = new Vector4(0.055f, 0.038f, 0.026f, 1f),
            smoothness = 0.005f,
        };
        string distantPath = $"Assets/Scenes/WendHill_TerrainLayer_{result.Length:00}.terrainlayer";
        if (AssetDatabase.LoadMainAssetAtPath(distantPath) != null) AssetDatabase.DeleteAsset(distantPath);
        AssetDatabase.CreateAsset(distantSoil, distantPath);
        result[result.Length - 1] = distantSoil;
        return result;
    }

    static void SculptForEstate(TerrainData data, TerrainData source, Vector3 origin,
        float anchorHeight)
    {
        int resolution = data.heightmapResolution;
        float[,] heights = source.GetHeights(0, 0, resolution, resolution);
        float heightScale = data.size.y;
        var pads = new[]
        {
            // Structures get small foundations, not room-sized flat rectangles.
            new Vector4(33f, 30f, 8.8f, 7.2f),
            new Vector4(-33.5f, 50f, 7.4f, 6.2f),
            new Vector4(-20.5f, 17.5f, 5.2f, 4.5f),
            new Vector4(-4.8f, 76.5f, 3.8f, 5.2f),
        };
        var workedRooms = new[]
        {
            // Old worked ground retains roughly a quarter of the imported micro-relief. Each mask
            // is elliptical and feathers beyond the playable area, never a rectangular room pad.
            new Vector4(16.5f, 27.0f, 9.8f, 16.8f),   // cemetery
            new Vector4(-16.0f, 25.4f, 11.2f, 14.0f), // kitchen garden
            new Vector4(-28.5f, 48.0f, 12.0f, 12.5f), // coach work yard
            new Vector4(32.8f, 29.5f, 9.5f, 10.5f),   // chapel forecourt
        };
        float[] padTargets = new float[pads.Length];
        for (int p = 0; p < pads.Length; p++)
        {
            float u = Mathf.InverseLerp(origin.x, origin.x + data.size.x, pads[p].x);
            float v = Mathf.InverseLerp(origin.z, origin.z + data.size.z, pads[p].y);
            padTargets[p] = source.size.y > 0.001f
                ? source.GetInterpolatedHeight(u, v) / source.size.y : 0f;
        }
        float[] roomTargets = new float[workedRooms.Length];
        for (int room = 0; room < workedRooms.Length; room++)
        {
            float u = Mathf.InverseLerp(origin.x, origin.x + data.size.x, workedRooms[room].x);
            float v = Mathf.InverseLerp(origin.z, origin.z + data.size.z, workedRooms[room].y);
            roomTargets[room] = source.size.y > 0.001f
                ? source.GetInterpolatedHeight(u, v) / source.size.y : 0f;
        }

        for (int iz = 0; iz < resolution; iz++)
            for (int ix = 0; ix < resolution; ix++)
            {
                float x = origin.x + ix / (float)(resolution - 1) * data.size.x;
                float z = origin.z + iz / (float)(resolution - 1) * data.size.z;
                float h = heights[iz, ix];

                // A narrow level crown protects the 139m arrival route. Broad shoulders still keep
                // the source heightmap, so the road sits in land instead of on a flat stage.
                float driveWeight = 1f - Smooth01(3.25f, 6.35f,
                    Mathf.Abs(x - DriveCentre(z)));
                h = Mathf.Lerp(h, anchorHeight / heightScale, driveWeight);

                // A shallow drainage swale is terrain, not a painted edge stripe. It breaks the
                // perfect road/floor join while remaining outside the player corridor.
                float swaleDistance = Mathf.Abs(Mathf.Abs(x) - 5.25f);
                float swale = Mathf.Exp(-(swaleDistance * swaleDistance) / 0.72f) *
                    (1f - Smooth01(96f, 112f, Mathf.Abs(z - 18f)));
                h = Mathf.Max(0f, h - swale * (0.10f / heightScale));

                for (int room = 0; room < workedRooms.Length; room++)
                {
                    float nx = (x - workedRooms[room].x) / workedRooms[room].z;
                    float nz = (z - workedRooms[room].y) / workedRooms[room].w;
                    float radius = Mathf.Sqrt(nx * nx + nz * nz);
                    float roomWeight = (1f - Smooth01(0.58f, 1.22f, radius)) * 0.74f;
                    h = Mathf.Lerp(h, roomTargets[room], roomWeight);
                }

                for (int p = 0; p < pads.Length; p++)
                {
                    float nx = Mathf.Abs(x - pads[p].x) / pads[p].z;
                    float nz = Mathf.Abs(z - pads[p].y) / pads[p].w;
                    float radius = Mathf.Sqrt(nx * nx + nz * nz);
                    float weight = 1f - Smooth01(0.62f, 1f, radius);
                    h = Mathf.Lerp(h, padTargets[p], weight);
                }

                heights[iz, ix] = Mathf.Clamp01(h);
            }
        data.SetHeights(0, 0, heights);
    }

    static void PaintForEstate(TerrainData data, Vector3 origin)
    {
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        int layers = data.alphamapLayers;
        if (layers < 5) return;
        float[,,] alpha = data.GetAlphamaps(0, 0, width, height);
        for (int zIndex = 0; zIndex < height; zIndex++)
            for (int xIndex = 0; xIndex < width; xIndex++)
            {
                float x = origin.x + xIndex / (float)(width - 1) * data.size.x;
                float z = origin.z + zIndex / (float)(height - 1) * data.size.z;
                float centreDistance = Mathf.Abs(x - DriveCentre(z));
                float drive = 1f - Smooth01(3.2f, 7.4f, centreDistance);
                float mottling = Mathf.PerlinNoise((x + 91.7f) * 0.045f, (z - 43.2f) * 0.045f);
                float fineMottling = Mathf.PerlinNoise((x - 27.1f) * 0.093f, (z + 12.6f) * 0.082f);
                float macroMottling = Mathf.PerlinNoise((x + 33.7f) * 0.018f, (z - 8.4f) * 0.019f);
                // Broad thresholded biomes preserve recognizable mud, leaf and moss families.
                // Blending every pixel from all three at similar weights produced one uniform
                // mauve-brown field even though the layers and textures were technically present.
                float leafBiome = Smooth01(0.34f, 0.73f,
                    mottling * 0.62f + macroMottling * 0.38f);
                float mossBiome = Smooth01(0.46f, 0.80f,
                    (1f - fineMottling) * 0.70f + macroMottling * 0.30f);
                float leafWeight = Mathf.Lerp(0.090f, 0.64f, leafBiome);
                float mossWeight = Mathf.Lerp(0.014f, 0.16f, mossBiome) *
                    Mathf.Lerp(1f, 0.42f, leafBiome);
                // Authored rooms share the same soil family but not the same land use. Family plots
                // have returned to patchy turf and leaf fall; the kitchen garden and coach yard are
                // still visibly worked/compacted. Elliptical feathering avoids drawing rectangular
                // room carpets while restoring a readable ground hierarchy between destinations.
                float cemeteryRadius = Mathf.Sqrt(Mathf.Pow((x - 16.5f) / 10.4f, 2f) +
                    Mathf.Pow((z - 27.0f) / 17.4f, 2f));
                // A full-strength ellipse was legible as a painter mask from inside the plot.
                // Cemetery ecology now influences a broader area at half strength, so individual
                // graves and vegetation carry the room instead of a dark oval on the ground.
                float cemetery = (1f - Smooth01(0.50f, 1.35f, cemeteryRadius)) * 0.52f;
                float cemeteryLeaf = Mathf.Lerp(0.24f, 0.38f, mottling);
                float cemeteryMoss = Mathf.Lerp(0.13f, 0.23f, 1f - fineMottling);
                leafWeight = Mathf.Lerp(leafWeight, cemeteryLeaf, cemetery);
                mossWeight = Mathf.Lerp(mossWeight, cemeteryMoss, cemetery);

                float gardenRadius = Mathf.Sqrt(Mathf.Pow((x + 16.0f) / 11.8f, 2f) +
                    Mathf.Pow((z - 25.4f) / 14.8f, 2f));
                float garden = 1f - Smooth01(0.64f, 1.12f, gardenRadius);
                leafWeight = Mathf.Lerp(leafWeight, Mathf.Lerp(0.060f, 0.150f, mottling), garden);
                mossWeight = Mathf.Lerp(mossWeight, Mathf.Lerp(0.010f, 0.035f, 1f - fineMottling), garden);

                float coachRadius = Mathf.Sqrt(Mathf.Pow((x + 28.5f) / 12.6f, 2f) +
                    Mathf.Pow((z - 48.0f) / 13.2f, 2f));
                float coach = 1f - Smooth01(0.62f, 1.10f, coachRadius);
                leafWeight = Mathf.Lerp(leafWeight, Mathf.Lerp(0.045f, 0.115f, mottling), coach);
                mossWeight = Mathf.Lerp(mossWeight, Mathf.Lerp(0.008f, 0.026f, 1f - fineMottling), coach);

                // The fifth layer is a contrast-compressed version of the acreage soil. Broad room
                // masks keep the garden and coach yard from falling into black, while two rounded,
                // feathered crossings make the garden's negative-space paths distinct from its
                // darker loose beds. The terminal approach is treated as old compacted traffic;
                // this removes the conspicuous dark speckle without changing the already-approved
                // arrival acreage or the global material remaps.
                float entryCentreZ = 22.1f + Mathf.Sin((x + 4.1f) * 0.61f) * 0.11f;
                float workCentreX = -15.6f + Mathf.Sin((z - 3.7f) * 0.57f) * 0.10f;
                float gardenEntryPath =
                    (1f - Smooth01(6.2f, 7.6f, Mathf.Abs(x + 16.2f))) *
                    (1f - Smooth01(0.25f, 0.82f, Mathf.Abs(z - entryCentreZ)));
                float gardenWorkPath =
                    (1f - Smooth01(0.20f, 0.72f, Mathf.Abs(x - workCentreX))) *
                    (1f - Smooth01(4.85f, 6.55f, Mathf.Abs(z - 28.0f)));
                float gardenPathWear = Mathf.Lerp(0.76f, 1f,
                    Mathf.PerlinNoise((x + 38.2f) * 0.19f, (z - 11.7f) * 0.16f));
                float gardenPath = Mathf.Max(gardenEntryPath, gardenWorkPath) * garden * gardenPathWear;
                float porchRadius = Mathf.Sqrt(Mathf.Pow(x / 5.2f, 2f) +
                    Mathf.Pow((z + 43.0f) / 17.5f, 2f));
                float porchApproach = 1f - Smooth01(0.56f, 1.18f, porchRadius);
                float workedSoil = Mathf.Clamp01(Mathf.Max(
                    Mathf.Max(garden * 0.62f, gardenPath * 0.92f),
                    Mathf.Max(coach * 0.78f, porchApproach * 0.66f)));

                float localX = x - DriveCentre(z);
                float shoulder = 1f - Smooth01(2.05f, 3.55f, Mathf.Abs(localX));
                float leftTrack = Mathf.Exp(-Mathf.Pow(localX + 0.92f + Mathf.Sin(z * 0.22f) * 0.07f, 2f) / 0.13f);
                float rightTrack = Mathf.Exp(-Mathf.Pow(localX - 0.92f + Mathf.Sin(z * 0.17f + 1.2f) * 0.08f, 2f) / 0.13f);
                float leftPersistence = Mathf.Lerp(0.18f, 1f,
                    Smooth01(0.32f, 0.70f, Mathf.PerlinNoise(0.17f, (z + 81f) * 0.074f)));
                float rightPersistence = Mathf.Lerp(0.16f, 1f,
                    Smooth01(0.30f, 0.72f, Mathf.PerlinNoise(0.71f, (z - 24f) * 0.069f)));
                float trackWear = Mathf.Clamp01(leftTrack * leftPersistence +
                    rightTrack * rightPersistence);

                // Traffic suppresses ecology only in two broken wheel tracks. The centre receives
                // mottled low recovery; irregular leaf banks collect outside the tyres and around
                // the terminal apron. This creates visible wet/dry/leaf transitions instead of one
                // mauve strip fading into one mauve field.
                leafWeight = Mathf.Lerp(leafWeight, Mathf.Lerp(0.025f, 0.075f, mottling), trackWear);
                mossWeight = Mathf.Lerp(mossWeight, Mathf.Lerp(0.004f, 0.018f, fineMottling), trackWear);
                float centreRecovery = (1f - Smooth01(0.20f, 0.62f, Mathf.Abs(localX))) *
                    Mathf.Lerp(0.36f, 1f, fineMottling);
                leafWeight = Mathf.Lerp(leafWeight, Mathf.Lerp(0.22f, 0.41f, mottling), centreRecovery * 0.58f);
                mossWeight = Mathf.Lerp(mossWeight, Mathf.Lerp(0.09f, 0.18f, 1f - fineMottling), centreRecovery * 0.52f);
                float leafBank = Mathf.Exp(-Mathf.Pow(Mathf.Abs(localX) - 3.15f, 2f) / 1.15f) *
                    Smooth01(0.43f, 0.72f, Mathf.PerlinNoise((x + 8f) * 0.13f, (z - 31f) * 0.11f));
                float porchLeafBank = Smooth01(0.54f, 0.78f, porchRadius) *
                    (1f - Smooth01(0.88f, 1.18f, porchRadius)) *
                    Mathf.Lerp(0.45f, 1f, mottling);
                float accumulatedLeaves = Mathf.Max(leafBank, porchLeafBank);
                leafWeight = Mathf.Lerp(leafWeight, Mathf.Lerp(0.40f, 0.67f, mottling), accumulatedLeaves);
                mossWeight = Mathf.Lerp(mossWeight, Mathf.Lerp(0.035f, 0.095f, 1f - fineMottling), accumulatedLeaves);
                // Distant imported Terrain previously retained the same bright acreage mix all the
                // way to the sky, drawing a pale world-edge band. Outside the playable estate it
                // transitions into the darkest damp-moss family before the ridge mesh begins. This
                // is distance ecology, not a camera mask, and applies identically in every view.
                float farBoundary = Mathf.Max(
                    Mathf.Max(Smooth01(108f, 150f, z), Smooth01(96f, 142f, -z)),
                    Smooth01(82f, 142f, Mathf.Abs(x)));
                workedSoil *= 1f - farBoundary * 0.92f;
                float mudWeight = 1f - leafWeight - mossWeight;
                // Preserve mud and leaves through the road texture rather than painting a clean
                // opaque stripe. Perlin breakup makes the lane fray into its shoulders.
                float erosion = Mathf.Lerp(0.70f, 0.94f,
                    Mathf.PerlinNoise((x - 17.2f) * 0.18f, (z + 48.1f) * 0.085f));
                // Two broken wheel tracks carry the route; a weak central wash joins them without
                // returning to a uniformly painted strip. This mirrors the owned village reference
                // scene's road grammar while retaining Wend Hill's narrower estate scale.
                float road = Mathf.Clamp01(shoulder * 0.025f + trackWear * 0.84f) * erosion;
                // Wheel-track breakup is useful on the long estate lane, but under the porch camera
                // the same mask becomes repeated black bands competing with the facade. Centuries of
                // turning and foot traffic have blended the terminal apron into compacted soil, so
                // dissolve the explicit tracks only inside that already-authored local treatment.
                road *= Mathf.Lerp(1f, 0.18f, porchApproach);
                float ecological = (1f - road) * (1f - workedSoil);
                float distant = farBoundary * 0.94f;
                float near = 1f - distant;
                alpha[zIndex, xIndex, 0] = mudWeight * ecological * near;
                alpha[zIndex, xIndex, 1] = mossWeight * ecological * near;
                alpha[zIndex, xIndex, 2] = leafWeight * ecological * near;
                alpha[zIndex, xIndex, 3] = road * near;
                alpha[zIndex, xIndex, 4] = workedSoil * (1f - road) * near;
                alpha[zIndex, xIndex, 5] = distant;
            }
        data.SetAlphamaps(0, 0, alpha);
    }

    static float DriveCentre(float z)
    {
        return Mathf.Sin(z * 0.035f + 0.8f) * 0.68f +
            Mathf.Sin(z * 0.081f - 0.35f) * 0.30f;
    }

    public static float DriveCentreAt(float z) => DriveCentre(z);

    public static GameObject BuildDrive(Material material, float startZ, float endZ)
    {
        float north = Mathf.Max(startZ, endZ) + 32f;
        float south = Mathf.Min(startZ, endZ) - 22f;
        const int across = 16;
        int along = Mathf.CeilToInt((north - south) / 1.25f);
        var vertices = new Vector3[(across + 1) * (along + 1)];
        var uv = new Vector2[vertices.Length];
        for (int iz = 0; iz <= along; iz++)
        {
            float tz = iz / (float)along;
            float z = Mathf.Lerp(south, north, tz);
            // A nineteenth-century estate lane should not resolve as a ruler-straight black
            // trapezoid. The centre wanders slowly, the width contracts at irregular intervals,
            // and the painted terrain beneath remains visible as a soft muddy shoulder.
            float centre = DriveCentre(z);
            float width = 2.88f + Mathf.Sin(z * 0.071f) * 0.20f +
                Mathf.Sin(z * 0.029f + 1.2f) * 0.14f;
            for (int ix = 0; ix <= across; ix++)
            {
                float tx = ix / (float)across;
                float signed = tx * 2f - 1f;
                // Both edges carry different low-frequency erosion so the lane never presents two
                // perfectly parallel silhouettes. Keep the disturbance smooth between rows to
                // avoid a procedural saw-tooth edge.
                float edgeErosion = signed < 0f
                    ? Mathf.Sin(z * 0.193f + 1.4f) * 0.10f
                    : Mathf.Sin(z * 0.157f - 0.7f) * 0.12f;
                float x = centre + signed * (width + edgeErosion);
                float crown = 0.035f + (1f - Mathf.Abs(signed)) * 0.065f;
                float localX = x - centre;
                float leftRut = Mathf.Exp(-Mathf.Pow(localX + 1.02f, 2f) / 0.11f);
                float rightRut = Mathf.Exp(-Mathf.Pow(localX - 1.02f, 2f) / 0.11f);
                float rut = (leftRut + rightRut) * 0.046f * (0.74f + 0.26f * Mathf.Sin(z * 0.32f));
                int index = iz * (across + 1) + ix;
                vertices[index] = new Vector3(x, GroundHeight(x, z) + crown - rut, z);
                uv[index] = new Vector2(tx * 2f, tz * 18f);
            }
        }
        Mesh mesh = BuildMesh("WendHill_CrownedDrive", vertices, uv, across, along);
        SaveMesh(mesh, "Assets/Scenes/WendHill_CrownedDrive.asset");
        var drive = new GameObject("Drive");
        drive.AddComponent<MeshFilter>().sharedMesh = mesh;
        // Terrain layer four draws the lane. Retain this precisely-grounded invisible mesh only as
        // the gravel footstep/collision semantic, never as a second opaque surface over the land.
        var renderer = drive.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.enabled = false;
        drive.AddComponent<MeshCollider>().sharedMesh = mesh;
        drive.AddComponent<GmSurfaceTag>().Configure(GmSurfaceKind.Gravel,
            "A terrain-painted muddy lane with feathered shoulders and a hidden crowned footstep surface.");
        return drive;
    }

    public static Transform BuildGardenFurrows(Transform parent, Material material)
    {
        var root = new GameObject("GardenSculptedFurrows").transform;
        root.SetParent(parent);
        // Retain a semantic surface marker for footsteps/audits, but draw no generated soil here.
        // Two real GPU passes proved that even narrow tapered crowns read first as black holes at
        // this fixed exposure. Plant silhouettes, row stakes, twig edges, straw and missing places
        // now carry cultivation honestly; the engine must prefer no geometry over visible grammar
        // that makes the art worse.
        root.gameObject.AddComponent<GmSurfaceTag>().Configure(GmSurfaceKind.WetMud,
            "Failed crop rows are carried by owned growth, stakes and work traces without painted soil bars.");
        return root;
    }

    public static Transform BuildCemeteryGrounding(Transform parent, Material material)
    {
        var root = new GameObject("CemeteryGrounding").transform;
        root.SetParent(parent);
        // Ancient rural graves have returned to turf. Generated family benches and smaller scars
        // both rendered as repeated black islands; only the recent open grave should cut a dark
        // shape into this room. Family history now lives in marker form, spacing, leaning, growth
        // and leaf accumulation rather than a procedural floor treatment.
        root.gameObject.AddComponent<GmSurfaceTag>().Configure(GmSurfaceKind.DeadGrass,
            "Old family plots have returned to turf; marker families and growth carry burial history.");
        return root;
    }

    static void CreateFurrowSegment(Transform parent, string name, float x, float z, float width,
        float length, Material material, int seed, float height = 0.075f, float endWidth = 0.18f)
    {
        const int across = 8;
        int along = Mathf.Max(8, Mathf.CeilToInt(length / 0.35f));
        var random = new System.Random(seed);
        var vertices = new Vector3[(across + 1) * (along + 1)];
        var uv = new Vector2[vertices.Length];
        float wander = 0f;
        for (int iz = 0; iz <= along; iz++)
        {
            float tz = iz / (float)along;
            wander = Mathf.Lerp(wander, ((float)random.NextDouble() - 0.5f) * 0.18f, 0.34f);
            for (int ix = 0; ix <= across; ix++)
            {
                float tx = ix / (float)across;
                float signed = tx * 2f - 1f;
                float endFade = Mathf.SmoothStep(0f, 1f, Mathf.Sin(tz * Mathf.PI));
                float footprint = Mathf.Lerp(endWidth, 1f, endFade);
                float crown = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(signed)), 0.7f) * endFade;
                float clod = ((float)random.NextDouble() - 0.5f) * 0.012f * crown;
                int index = iz * (across + 1) + ix;
                vertices[index] = new Vector3(x + signed * width * 0.5f * footprint + wander,
                    0.018f + crown * height + clod, z + Mathf.Lerp(-length * 0.5f, length * 0.5f, tz));
                uv[index] = new Vector2(tx, tz * Mathf.Max(1f, length / width));
            }
        }
        Mesh mesh = BuildMesh(name, vertices, uv, across, along);
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Mesh BuildGrid(string name, float minX, float maxX, float minZ, float maxZ,
        int xSteps, int zSteps, Func<float, float, float> height)
    {
        var vertices = new Vector3[(xSteps + 1) * (zSteps + 1)];
        var uv = new Vector2[vertices.Length];
        for (int iz = 0; iz <= zSteps; iz++)
            for (int ix = 0; ix <= xSteps; ix++)
            {
                float tx = ix / (float)xSteps, tz = iz / (float)zSteps;
                float x = Mathf.Lerp(minX, maxX, tx), z = Mathf.Lerp(minZ, maxZ, tz);
                int index = iz * (xSteps + 1) + ix;
                vertices[index] = new Vector3(x, height(x, z), z);
                uv[index] = new Vector2((x - minX) / 5f, (z - minZ) / 5f);
            }
        return BuildMesh(name, vertices, uv, xSteps, zSteps);
    }

    static Mesh BuildMesh(string name, Vector3[] vertices, Vector2[] uv, int xSteps, int zSteps)
    {
        var triangles = new int[xSteps * zSteps * 6];
        int ti = 0;
        for (int z = 0; z < zSteps; z++)
            for (int x = 0; x < xSteps; x++)
            {
                int a = z * (xSteps + 1) + x;
                int b = a + 1;
                int c = a + xSteps + 1;
                int d = c + 1;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
            }
        var mesh = new Mesh { name = name };
        mesh.indexFormat = vertices.Length > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SaveMesh(Mesh mesh, string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }
}

