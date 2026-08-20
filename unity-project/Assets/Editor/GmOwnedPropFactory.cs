using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Deterministic placement and small authored-mesh helpers for player-facing interior props.
/// Imported pack materials are preserved unless the caller deliberately supplies an override.
/// </summary>
public static class GmOwnedPropFactory
{
    public const string VisualPrefix = "OwnedArt_";

    public static GameObject PlacePrefab(string assetPath, string objectName, Transform parent,
        Vector3 targetCenter, Vector3 targetSize, Quaternion worldRotation, bool ground = false,
        float surfaceY = 0f, Material overrideMaterial = null)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            throw new InvalidOperationException($"[GmOwnedPropFactory] no prefab or model at {assetPath}");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = VisualPrefix + objectName;
        instance.transform.SetPositionAndRotation(Vector3.zero, worldRotation);
        instance.transform.localScale = Vector3.one;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true))
            UnityEngine.Object.DestroyImmediate(camera);
        foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            UnityEngine.Object.DestroyImmediate(light);

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException($"[GmOwnedPropFactory] {assetPath} has no renderers");
        foreach (Renderer renderer in renderers)
        {
            if (overrideMaterial != null)
            {
                int slots = Mathf.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(overrideMaterial, slots).ToArray();
            }
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        Bounds bounds = GmPropPlacementEngine.EncapsulateBounds(instance);
        float scale = float.PositiveInfinity;
        if (targetSize.x > 0.001f && bounds.size.x > 0.001f) scale = Mathf.Min(scale, targetSize.x / bounds.size.x);
        if (targetSize.y > 0.001f && bounds.size.y > 0.001f) scale = Mathf.Min(scale, targetSize.y / bounds.size.y);
        if (targetSize.z > 0.001f && bounds.size.z > 0.001f) scale = Mathf.Min(scale, targetSize.z / bounds.size.z);
        if (float.IsInfinity(scale) || scale <= 0f) scale = 1f;
        instance.transform.localScale *= scale;

        bounds = GmPropPlacementEngine.EncapsulateBounds(instance);
        instance.transform.position += targetCenter - bounds.center;
        if (ground)
        {
            bounds = GmPropPlacementEngine.EncapsulateBounds(instance);
            instance.transform.position += Vector3.up * (surfaceY - bounds.min.y);
        }
        return instance;
    }

    public static GameObject CreateRoundedProp(string objectName, Transform parent, Vector3 position,
        Quaternion rotation, Vector3 size, float cornerRadius, Material material)
    {
        return CreateMeshObject(objectName, parent, position, rotation,
            RoundedBoxMesh(size.x, size.y, size.z, cornerRadius), material);
    }

    public static GameObject CreateMirrorShard(string objectName, Transform parent, Vector3 position,
        Quaternion rotation, Vector3 size, Material material)
    {
        Vector2[] outline =
        {
            new Vector2(-0.44f, -0.50f), new Vector2(0.50f, -0.28f), new Vector2(0.12f, 0.50f),
        };
        GameObject shard = CreateMeshObject(objectName, parent, position, rotation,
            ExtrudedPolygon(outline, 0.10f), material);
        shard.transform.localScale = size;
        return shard;
    }

    public static GameObject CreateDisc(string objectName, Transform parent, Vector3 position,
        Quaternion rotation, Vector3 size, Material material, int segments = 32)
    {
        segments = Mathf.Clamp(segments, 12, 96);
        var outline = new Vector2[segments];
        for (int segment = 0; segment < segments; segment++)
        {
            float angle = segment * Mathf.PI * 2f / segments;
            outline[segment] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
        }
        GameObject disc = CreateMeshObject(objectName, parent, position, rotation,
            ExtrudedPolygon(outline, 1f), material);
        disc.transform.localScale = size;
        return disc;
    }

    public static GameObject CreateQuill(string objectName, Transform parent, Vector3 position,
        Quaternion rotation, Vector3 size, Material material)
    {
        Vector2[] feather =
        {
            new Vector2(-0.010f, -0.50f), new Vector2(-0.026f, -0.28f),
            new Vector2(-0.16f, -0.16f), new Vector2(-0.075f, -0.10f),
            new Vector2(-0.22f, 0.02f), new Vector2(-0.09f, 0.08f),
            new Vector2(-0.19f, 0.20f), new Vector2(-0.065f, 0.26f),
            new Vector2(0f, 0.50f),
            new Vector2(0.065f, 0.26f), new Vector2(0.19f, 0.20f),
            new Vector2(0.09f, 0.08f), new Vector2(0.22f, 0.02f),
            new Vector2(0.075f, -0.10f), new Vector2(0.16f, -0.16f),
            new Vector2(0.026f, -0.28f), new Vector2(0.010f, -0.50f),
        };
        GameObject quill = CreateMeshObject(objectName, parent, position, rotation,
            ExtrudedPolygon(feather, 0.035f), material);
        quill.transform.localScale = size;
        return quill;
    }

    public static GameObject CreateCardDeck(string objectName, Transform parent, Vector3 position,
        Material cardBack, IReadOnlyList<Material> suitMaterials)
    {
        if (suitMaterials == null || suitMaterials.Count != 4)
            throw new ArgumentException("Card deck needs exactly four suit materials", nameof(suitMaterials));

        var root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        Mesh cardMesh = RoundedBoxMesh(0.16f, 0.24f, 0.006f, 0.014f);

        for (int i = 0; i < 24; i++)
        {
            float jitterX = ((i % 3) - 1) * 0.0015f;
            float jitterZ = (((i + 1) % 3) - 1) * 0.0012f;
            GameObject card = CreateMeshObject($"DrawCard_{i + 1:00}", root.transform,
                new Vector3(jitterX, i * 0.0032f, jitterZ), Quaternion.Euler(90f, i % 2 == 0 ? -0.8f : 0.8f, 0f),
                cardMesh, cardBack);
            card.transform.localPosition = new Vector3(jitterX, i * 0.0032f, jitterZ);
        }

        for (int suit = 0; suit < 4; suit++)
        {
            float x = (suit - 1.5f) * 0.105f;
            float y = 0.083f + suit * 0.001f;
            float z = 0.12f + Mathf.Abs(suit - 1.5f) * 0.018f;
            GameObject card = CreateMeshObject($"SuitCard_{suit}", root.transform,
                new Vector3(x, y, z),
                Quaternion.Euler(90f, (suit - 1.5f) * 7f, 0f), cardMesh, suitMaterials[suit]);
            card.transform.localPosition = new Vector3(x, y, z);
            CreateCardSuitMark(root.transform, suit, new Vector3(x, y + 0.006f, z), cardBack);
        }
        return root;
    }

    public static GmParlorCardView CreatePhysicalCard(string objectName, Transform parent,
        GmCard physicalCard, Material edgeMaterial, Material faceMaterial, Material backMaterial,
        Material suitMaterial)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(parent, false);

        CreateLocalRoundedProp("CardEdge", root.transform, Vector3.zero, Quaternion.identity,
            new Vector3(0.16f, 0.24f, 0.008f), 0.014f, edgeMaterial);

        var face = new GameObject("Face");
        face.transform.SetParent(root.transform, false);
        CreateLocalRoundedProp("FaceStock", face.transform, new Vector3(0f, 0f, -0.0046f),
            Quaternion.identity, new Vector3(0.153f, 0.233f, 0.0012f), 0.012f, faceMaterial);
        CreateCardCornerLabel(face.transform, physicalCard, new Vector3(-0.052f, 0.078f, -0.0054f),
            Quaternion.identity, suitMaterial);
        CreateCardCornerLabel(face.transform, physicalCard, new Vector3(0.052f, -0.078f, -0.0054f),
            Quaternion.Euler(0f, 0f, 180f), suitMaterial);
        CreateFaceSuitMark(face.transform, physicalCard.Suit, suitMaterial);

        var back = new GameObject("Back");
        back.transform.SetParent(root.transform, false);
        CreateLocalRoundedProp("BackStock", back.transform, new Vector3(0f, 0f, 0.0046f),
            Quaternion.identity, new Vector3(0.153f, 0.233f, 0.0012f), 0.012f, backMaterial);
        CreateLocalRoundedProp("BackInset", back.transform, new Vector3(0f, 0f, 0.0054f),
            Quaternion.identity, new Vector3(0.126f, 0.202f, 0.0008f), 0.010f, edgeMaterial);

        var focus = root.AddComponent<BoxCollider>();
        focus.size = new Vector3(0.17f, 0.25f, 0.016f);
        GmParlorCardView view = root.AddComponent<GmParlorCardView>();
        view.Configure(physicalCard, face.transform, back.transform);
        return view;
    }

    static void CreateCardCornerLabel(Transform parent, GmCard card, Vector3 localPosition,
        Quaternion localRotation, Material material)
    {
        var label = new GameObject($"Rank_{card.Rank}_{card.Suit}");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = localPosition;
        label.transform.localRotation = localRotation;
        bool[] segments = DigitSegments(card.Rank);
        Vector3[] positions =
        {
            new Vector3(0f, 0.015f, 0f),
            new Vector3(0.009f, 0.008f, 0f),
            new Vector3(0.009f, -0.008f, 0f),
            new Vector3(0f, -0.015f, 0f),
            new Vector3(-0.009f, -0.008f, 0f),
            new Vector3(-0.009f, 0.008f, 0f),
            Vector3.zero,
        };
        for (int segment = 0; segment < segments.Length; segment++)
        {
            if (!segments[segment]) continue;
            bool vertical = segment == 1 || segment == 2 || segment == 4 || segment == 5;
            CreateLocalRoundedProp($"Segment_{segment}", label.transform,
                positions[segment], Quaternion.identity,
                vertical ? new Vector3(0.0035f, 0.013f, 0.001f) :
                    new Vector3(0.018f, 0.0035f, 0.001f),
                0.0015f, material);
        }
    }

    static void CreateFaceSuitMark(Transform parent, GmSuit suit, Material material)
    {
        var mark = new GameObject($"Suit_{suit}");
        mark.transform.SetParent(parent, false);
        mark.transform.localPosition = new Vector3(0f, -0.002f, -0.0054f);
        if (suit == GmSuit.Flames)
        {
            GameObject flame = CreateQuill("Flame", mark.transform, Vector3.zero,
                Quaternion.identity, new Vector3(0.18f, 0.12f, 0.012f), material);
            flame.transform.localPosition = Vector3.zero;
            flame.transform.localRotation = Quaternion.identity;
            return;
        }
        if (suit == GmSuit.Eyes)
        {
            GameObject eye = CreateDisc("Eye", mark.transform, Vector3.zero, Quaternion.identity,
                new Vector3(0.070f, 0.034f, 0.001f), material, 32);
            eye.transform.localPosition = Vector3.zero;
            GameObject pupil = CreateDisc("Pupil", mark.transform, Vector3.zero, Quaternion.identity,
                new Vector3(0.018f, 0.018f, 0.0015f), material, 24);
            pupil.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            return;
        }
        if (suit == GmSuit.Bones)
        {
            for (int direction = -1; direction <= 1; direction += 2)
                CreateLocalRoundedProp(direction < 0 ? "BoneA" : "BoneB", mark.transform,
                    Vector3.zero, Quaternion.Euler(0f, 0f, direction * 38f),
                    new Vector3(0.074f, 0.009f, 0.001f), 0.004f, material);
            return;
        }
        for (int tooth = -1; tooth <= 1; tooth++)
            CreateLocalRoundedProp($"Tooth_{tooth + 2}", mark.transform,
                new Vector3(tooth * 0.022f, 0f, 0f), Quaternion.identity,
                new Vector3(0.015f, 0.043f, 0.001f), 0.006f, material);
    }

    public static GameObject CreateLocalRoundedProp(string objectName, Transform parent,
        Vector3 localPosition, Quaternion localRotation, Vector3 size, float cornerRadius,
        Material material)
    {
        GameObject prop = CreateRoundedProp(objectName, parent, Vector3.zero,
            Quaternion.identity, size, cornerRadius, material);
        prop.transform.localPosition = localPosition;
        prop.transform.localRotation = localRotation;
        return prop;
    }

    /// <summary>Creates one readable 10.5cm casino die from authored meshes.</summary>
    public static GmBonesDieView CreatePhysicalDie(string objectName, Transform parent,
        Vector3 localPosition, Material boneMaterial, Material pipMaterial, Material brassMaterial)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;

        GameObject body = CreateLocalRoundedProp("AuthoredDieBody", root.transform, Vector3.zero,
            Quaternion.identity, Vector3.one * 0.105f, 0.014f, boneMaterial);
        MeshFilter bodyMesh = body.GetComponent<MeshFilter>();
        bodyMesh.sharedMesh.name = "GmAuthoredPhysicalDie";

        foreach (var face in GmPhysicalDieFaces.All)
            CreateDieFacePips(root.transform, face.value, face.normal, pipMaterial);

        var evidence = new GameObject("ChangedDieBrassSeam");
        evidence.transform.SetParent(root.transform, false);
        const float edge = 0.055f;
        const float span = 0.092f;
        const float gauge = 0.0035f;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                CreateLocalRoundedProp($"SeamZ_{x}_{y}", evidence.transform,
                    new Vector3(x * edge, y * edge, 0f), Quaternion.identity,
                    new Vector3(gauge, gauge, span), gauge * 0.45f, brassMaterial);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                CreateLocalRoundedProp($"SeamY_{x}_{z}", evidence.transform,
                    new Vector3(x * edge, 0f, z * edge), Quaternion.identity,
                    new Vector3(gauge, span, gauge), gauge * 0.45f, brassMaterial);
        for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                CreateLocalRoundedProp($"SeamX_{y}_{z}", evidence.transform,
                    new Vector3(0f, y * edge, z * edge), Quaternion.identity,
                    new Vector3(span, gauge, gauge), gauge * 0.45f, brassMaterial);
        evidence.SetActive(false);

        var collider = root.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 0.108f;
        var view = root.AddComponent<GmBonesDieView>();
        view.Configure(bodyMesh, evidence);
        return view;
    }

    static readonly Quaternion DiscFaceUp = Quaternion.Euler(-90f, 0f, 0f);

    /// <summary>Creates one turned-silhouette chess piece from stacked authored disc/box meshes.
    /// No King/Queen/Rook/Bishop/Knight meshes exist in the local asset vault (verified
    /// 2026-08-20); Bishop and Knight are unimplemented because Study's three authored positions
    /// never place one.</summary>
    public static GmStudyPieceView CreatePhysicalChessPiece(string objectName, Transform parent,
        GmChessPieceType type, bool isWhite, Material bodyMaterial, Material evidenceMaterial)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var root = new GameObject(objectName);
        root.transform.SetParent(parent, false);

        float y = 0f;
        y = StackDisc(root.transform, "Base", y, 0.052f, 0.010f, bodyMaterial);
        switch (type)
        {
            case GmChessPieceType.Pawn:
                y = StackDisc(root.transform, "Neck", y, 0.024f, 0.026f, bodyMaterial);
                y = StackDisc(root.transform, "Head", y, 0.030f, 0.020f, bodyMaterial);
                break;
            case GmChessPieceType.Rook:
                y = StackDisc(root.transform, "Body", y, 0.034f, 0.052f, bodyMaterial);
                y = StackDisc(root.transform, "Rim", y, 0.040f, 0.008f, bodyMaterial);
                CreateCrenellations(root.transform, y, 0.036f, bodyMaterial);
                y += 0.010f;
                break;
            case GmChessPieceType.Queen:
                y = StackDisc(root.transform, "LowerBody", y, 0.036f, 0.030f, bodyMaterial);
                y = StackDisc(root.transform, "UpperBody", y, 0.026f, 0.030f, bodyMaterial);
                y = StackDisc(root.transform, "Collar", y, 0.032f, 0.006f, bodyMaterial);
                CreateLocalRoundedProp("Crown", root.transform, new Vector3(0f, y + 0.012f, 0f),
                    Quaternion.identity, Vector3.one * 0.024f, 0.012f, bodyMaterial);
                y += 0.024f;
                break;
            case GmChessPieceType.King:
                y = StackDisc(root.transform, "LowerBody", y, 0.036f, 0.030f, bodyMaterial);
                y = StackDisc(root.transform, "UpperBody", y, 0.026f, 0.034f, bodyMaterial);
                y = StackDisc(root.transform, "Collar", y, 0.032f, 0.006f, bodyMaterial);
                CreateLocalRoundedProp("CrossVertical", root.transform,
                    new Vector3(0f, y + 0.014f, 0f), Quaternion.identity,
                    new Vector3(0.007f, 0.028f, 0.007f), 0.003f, bodyMaterial);
                CreateLocalRoundedProp("CrossHorizontal", root.transform,
                    new Vector3(0f, y + 0.020f, 0f), Quaternion.identity,
                    new Vector3(0.020f, 0.007f, 0.007f), 0.003f, bodyMaterial);
                y += 0.028f;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type,
                    $"[GmOwnedPropFactory] no authored chess piece mesh exists for {type}");
        }

        var evidence = new GameObject("MovedPieceEvidenceRing");
        evidence.transform.SetParent(root.transform, false);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            CreateLocalRoundedProp($"EvidenceMark_{i}", evidence.transform,
                new Vector3(Mathf.Cos(angle) * 0.058f, 0.004f, Mathf.Sin(angle) * 0.058f),
                Quaternion.identity, new Vector3(0.012f, 0.008f, 0.012f), 0.004f, evidenceMaterial);
        }
        evidence.SetActive(false);

        var collider = root.AddComponent<CapsuleCollider>();
        collider.height = Mathf.Max(0.05f, y);
        collider.radius = 0.03f;
        collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
        var view = root.AddComponent<GmStudyPieceView>();
        view.Configure(type, isWhite, evidence);
        return view;
    }

    static float StackDisc(Transform parent, string name, float baseY, float diameter, float height,
        Material material)
    {
        // CreateDisc/CreateMeshObject apply their position argument in world space. Create at the
        // origin, then assign localPosition explicitly so this is safe regardless of where the
        // piece root sits in the scene -- see CreateDieFacePips for the same pattern.
        GameObject disc = CreateDisc(name, parent, Vector3.zero, Quaternion.identity,
            new Vector3(diameter, diameter, height), material);
        disc.transform.localPosition = new Vector3(0f, baseY + height * 0.5f, 0f);
        disc.transform.localRotation = DiscFaceUp;
        return baseY + height;
    }

    static void CreateCrenellations(Transform parent, float baseY, float ringRadius, Material material)
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            CreateLocalRoundedProp($"Crenellation_{i}", parent,
                new Vector3(Mathf.Cos(angle) * ringRadius, baseY + 0.005f, Mathf.Sin(angle) * ringRadius),
                Quaternion.identity, new Vector3(0.012f, 0.010f, 0.012f), 0.002f, material);
        }
    }

    static void CreateDieFacePips(Transform root, int value, Vector3 normal, Material material)
    {
        const float surface = 0.0532f;
        const float spacing = 0.024f;
        Vector2[] pattern = DiePipPattern(value);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        for (int index = 0; index < pattern.Length; index++)
        {
            Vector3 position = normal * surface + right * (pattern[index].x * spacing) +
                up * (pattern[index].y * spacing);
            GameObject pip = CreateDisc($"Pip_{value}_{index + 1}", root, Vector3.zero,
                Quaternion.identity, new Vector3(0.0105f, 0.0105f, 0.0022f), material, 20);
            pip.transform.localPosition = position;
            pip.transform.localRotation = rotation;
            pip.AddComponent<GmPhysicalDiePip>().Configure(value, normal);
        }
    }

    static Vector2[] DiePipPattern(int value)
    {
        Vector2 center = Vector2.zero;
        Vector2 nw = new Vector2(-1f, 1f);
        Vector2 ne = new Vector2(1f, 1f);
        Vector2 sw = new Vector2(-1f, -1f);
        Vector2 se = new Vector2(1f, -1f);
        Vector2 w = new Vector2(-1f, 0f);
        Vector2 e = new Vector2(1f, 0f);
        switch (value)
        {
            case 1: return new[] { center };
            case 2: return new[] { nw, se };
            case 3: return new[] { nw, center, se };
            case 4: return new[] { nw, ne, sw, se };
            case 5: return new[] { nw, ne, center, sw, se };
            case 6: return new[] { nw, w, sw, ne, e, se };
            default: throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    static bool[] DigitSegments(int digit)
    {
        // top, upper-right, lower-right, bottom, lower-left, upper-left, middle
        switch (digit)
        {
            case 1: return new[] { false, true, true, false, false, false, false };
            case 2: return new[] { true, true, false, true, true, false, true };
            case 3: return new[] { true, true, true, true, false, false, true };
            case 4: return new[] { false, true, true, false, false, true, true };
            case 5: return new[] { true, false, true, true, false, true, true };
            case 6: return new[] { true, false, true, true, true, true, true };
            case 7: return new[] { true, true, true, false, false, false, false };
            default: throw new ArgumentOutOfRangeException(nameof(digit));
        }
    }

    public static GameObject CreateCurtainPanel(string objectName, Transform parent, Vector3 position,
        Quaternion rotation, Vector2 size, float foldDepth, Material material, int foldCount = 7)
    {
        foldCount = Mathf.Clamp(foldCount, 3, 14);
        const int Rows = 12;
        int columns = foldCount * 4;
        var vertices = new List<Vector3>((columns + 1) * (Rows + 1));
        var uvs = new List<Vector2>((columns + 1) * (Rows + 1));
        var triangles = new List<int>(columns * Rows * 6);

        for (int row = 0; row <= Rows; row++)
        {
            float v = row / (float)Rows;
            float widthScale = Mathf.Lerp(1f, 0.82f, v);
            for (int column = 0; column <= columns; column++)
            {
                float u = column / (float)columns;
                float x = (u - 0.5f) * size.x * widthScale;
                float y = (v - 0.5f) * size.y;
                if (row == 0) y += Mathf.Sin(u * Mathf.PI * 5f) * size.y * 0.012f;
                float wave = Mathf.Sin(u * foldCount * Mathf.PI * 2f);
                float z = wave * foldDepth * Mathf.Lerp(1f, 0.72f, v);
                vertices.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = columns + 1;
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int a = row * stride + column;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                triangles.AddRange(new[] { a, b, c, b, d, c });
            }
        }

        var mesh = new Mesh { name = "GmAuthoredFoldedCurtain" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return CreateMeshObject(objectName, parent, position, rotation, mesh, material);
    }

    static void CreateCardSuitMark(Transform parent, int suit, Vector3 localCenter, Material material)
    {
        Quaternion faceUp = Quaternion.Euler(-90f, 0f, 0f);
        if (suit == 0)
        {
            GameObject flame = CreateQuill("FlameSuitMark", parent, Vector3.zero,
                Quaternion.identity, new Vector3(0.15f, 0.10f, 0.06f), material);
            flame.transform.localPosition = localCenter;
            flame.transform.localRotation = faceUp;
            return;
        }

        if (suit == 1)
        {
            GameObject eye = CreateDisc("EyeSuitMark", parent, Vector3.zero,
                Quaternion.identity, new Vector3(0.09f, 0.045f, 0.004f), material, 32);
            eye.transform.localPosition = localCenter;
            eye.transform.localRotation = faceUp;
            GameObject pupil = CreateDisc("EyeSuitPupil", parent, Vector3.zero,
                Quaternion.identity, new Vector3(0.024f, 0.024f, 0.005f), material, 24);
            pupil.transform.localPosition = localCenter + new Vector3(0f, 0.002f, 0f);
            pupil.transform.localRotation = faceUp;
            return;
        }

        if (suit == 2)
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                GameObject bone = CreateRoundedProp(direction < 0 ? "BoneSuitMarkA" : "BoneSuitMarkB",
                    parent, Vector3.zero, Quaternion.identity, new Vector3(0.11f, 0.014f, 0.004f),
                    0.006f, material);
                bone.transform.localPosition = localCenter;
                bone.transform.localRotation = faceUp * Quaternion.Euler(0f, 0f, direction * 35f);
            }
            return;
        }

        for (int tooth = -1; tooth <= 1; tooth++)
        {
            GameObject mark = CreateRoundedProp($"TeethSuitMark_{tooth + 2}", parent,
                Vector3.zero, Quaternion.identity, new Vector3(0.025f, 0.055f, 0.004f),
                0.008f, material);
            mark.transform.localPosition = localCenter + new Vector3(tooth * 0.033f, 0f, 0f);
            mark.transform.localRotation = faceUp;
        }
    }

    static GameObject CreateMeshObject(string objectName, Transform parent, Vector3 position,
        Quaternion rotation, Mesh mesh, Material material)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    static Mesh RoundedBoxMesh(float width, float height, float depth, float radius)
    {
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(halfW, halfH) * 0.9f);
        const int CornerSegments = 4;
        var outline = new List<Vector2>(CornerSegments * 4);
        Vector2[] centers =
        {
            new Vector2(-halfW + radius, -halfH + radius),
            new Vector2(halfW - radius, -halfH + radius),
            new Vector2(halfW - radius, halfH - radius),
            new Vector2(-halfW + radius, halfH - radius),
        };
        for (int corner = 0; corner < 4; corner++)
        {
            float start = -180f + corner * 90f;
            for (int segment = 0; segment < CornerSegments; segment++)
            {
                float angle = (start + segment * 90f / (CornerSegments - 1)) * Mathf.Deg2Rad;
                outline.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
        return ExtrudedPolygon(outline, depth);
    }

    static Mesh ExtrudedPolygon(IReadOnlyList<Vector2> outline, float depth)
    {
        int count = outline.Count;
        float halfDepth = depth * 0.5f;
        float minX = outline.Min(point => point.x);
        float maxX = outline.Max(point => point.x);
        float minY = outline.Min(point => point.y);
        float maxY = outline.Max(point => point.y);
        float width = Mathf.Max(0.0001f, maxX - minX);
        float height = Mathf.Max(0.0001f, maxY - minY);
        var vertices = new List<Vector3>(count * 2 + 2)
        {
            new Vector3(0f, 0f, -halfDepth),
            new Vector3(0f, 0f, halfDepth),
        };
        var uvs = new List<Vector2>(count * 2 + 2)
        {
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
        };
        for (int i = 0; i < count; i++) vertices.Add(new Vector3(outline[i].x, outline[i].y, -halfDepth));
        for (int i = 0; i < count; i++) vertices.Add(new Vector3(outline[i].x, outline[i].y, halfDepth));
        for (int i = 0; i < count; i++)
            uvs.Add(new Vector2((outline[i].x - minX) / width, (outline[i].y - minY) / height));
        for (int i = 0; i < count; i++)
            uvs.Add(new Vector2(1f - (outline[i].x - minX) / width, (outline[i].y - minY) / height));

        var triangles = new List<int>(count * 12);
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int front = 2 + i;
            int frontNext = 2 + next;
            int back = 2 + count + i;
            int backNext = 2 + count + next;
            triangles.AddRange(new[] { 0, frontNext, front, 1, back, backNext });
            triangles.AddRange(new[] { front, frontNext, backNext, front, backNext, back });
        }

        var mesh = new Mesh { name = "GmAuthoredExtrudedProp" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }
}
