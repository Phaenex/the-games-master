using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public sealed class GmCharacterAssetContractTests
{
    [Test]
    public void CompleteAldricContractPasses()
    {
        Assert.That(GmCharacterAssetContractValidator.Validate(ValidAldric()), Is.Empty);
    }

    [Test]
    public void UndefinedCharacterRoleFailsClosed()
    {
        GmCharacterAssetFacts facts = ValidAldric();
        facts.Role = (GmCharacterAssetRole)99;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GmCharacterAssetContractValidator.Validate(facts));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GmCharacterAssetContractInspector.Inspect((GmCharacterAssetRole)99,
                "Assets/ThirdParty/Vendor/Product/A.fbx",
                "Assets/GamesMaster/Characters/Aldric/A.prefab",
                "Assets/GamesMaster/Characters/Aldric/provenance.json"));
    }

    [TestCase(nameof(GmCharacterAssetFacts.RawSourceUnderThirdParty), "raw source")]
    [TestCase(nameof(GmCharacterAssetFacts.DerivedPrefabUnderOwnedCharacters), "derived prefab")]
    [TestCase(nameof(GmCharacterAssetFacts.HasCommercialLicenseRecord), "commercial license")]
    [TestCase(nameof(GmCharacterAssetFacts.ProvenanceNamesVendorProductVersion), "vendor, product, and version")]
    [TestCase(nameof(GmCharacterAssetFacts.RedistributionTermsRecorded), "redistribution")]
    public void SourceAndProvenanceRequirementsFailIndividually(string property, string message)
    {
        GmCharacterAssetFacts facts = ValidAldric();
        SetBool(facts, property, false);
        Assert.That(GmCharacterAssetContractValidator.Validate(facts),
            Has.Some.Contains(message).IgnoreCase);
    }

    [TestCase(nameof(GmCharacterAssetFacts.ImporterIsHumanoid), "Humanoid")]
    [TestCase(nameof(GmCharacterAssetFacts.AvatarIsHuman), "human Avatar")]
    [TestCase(nameof(GmCharacterAssetFacts.AvatarIsValid), "valid Avatar")]
    [TestCase(nameof(GmCharacterAssetFacts.HasStableShoulderAndWristChains), "shoulder")]
    [TestCase(nameof(GmCharacterAssetFacts.HasFullThreeJointFingerChains), "finger")]
    public void HumanoidAndHandRequirementsFailIndividually(string property, string message)
    {
        GmCharacterAssetFacts facts = ValidAldric();
        SetBool(facts, property, false);
        Assert.That(GmCharacterAssetContractValidator.Validate(facts),
            Has.Some.Contains(message).IgnoreCase);
    }

    [Test]
    public void FaceRequiresRiggedEyesAndLidsBrowsJawOrDeclaredBlendshapes()
    {
        GmCharacterAssetFacts facts = ValidAldric();
        facts.HasRiggedEyes = false;
        facts.HasLidsBrowsAndJaw = false;
        facts.HasDeclaredFacialBlendshapes = false;
        Assert.That(GmCharacterAssetContractValidator.Validate(facts),
            Has.Some.Contains("face"));
    }

    [TestCase(nameof(GmCharacterAssetFacts.ScaleIsMeters), "scale")]
    [TestCase(nameof(GmCharacterAssetFacts.BoundsAreHumanScale), "bounds")]
    [TestCase(nameof(GmCharacterAssetFacts.HasLod0Lod1Lod2), "LOD0/LOD1/LOD2")]
    [TestCase(nameof(GmCharacterAssetFacts.HasRequiredSockets), "socket")]
    [TestCase(nameof(GmCharacterAssetFacts.HasAnimatorController), "Animator Controller")]
    [TestCase(nameof(GmCharacterAssetFacts.HasUpperBodyAvatarMask), "Avatar Mask")]
    [TestCase(nameof(GmCharacterAssetFacts.HasAnimationRigBuilder), "Animation Rigging")]
    [TestCase(nameof(GmCharacterAssetFacts.AllMaterialsAreHdrpLit), "HDRP")]
    [TestCase(nameof(GmCharacterAssetFacts.TextureBudgetIsTwoK), "2K")]
    public void PrefabAndPresentationRequirementsFailIndividually(string property, string message)
    {
        GmCharacterAssetFacts facts = ValidAldric();
        SetBool(facts, property, false);
        Assert.That(GmCharacterAssetContractValidator.Validate(facts),
            Has.Some.Contains(message).IgnoreCase);
    }

    [Test]
    public void AldricBudgetRejectsExcessGeometrySkinningAndMaterials()
    {
        GmCharacterAssetFacts facts = ValidAldric();
        facts.TriangleCount = 120001;
        facts.SkinnedRendererCount = 7;
        facts.MaterialCount = 9;
        string joined = string.Join(" | ", GmCharacterAssetContractValidator.Validate(facts));
        StringAssert.Contains("120000", joined);
        StringAssert.Contains("6 skinned", joined);
        StringAssert.Contains("8 materials", joined);
    }

    [Test]
    public void PlayerBodyHasItsOwnBudgetAndConnectedBodyRequirements()
    {
        GmCharacterAssetFacts facts = ValidAldric();
        facts.Role = GmCharacterAssetRole.PlayerBody;
        facts.TriangleCount = 80001;
        facts.HasConnectedShouldersArmsAndBodyAnchor = false;
        string joined = string.Join(" | ", GmCharacterAssetContractValidator.Validate(facts));
        StringAssert.Contains("80000", joined);
        StringAssert.Contains("connected shoulders", joined);
    }

    [Test]
    public void SocketInspectionRequiresExactAuthoredNames()
    {
        var root = new GameObject("Character");
        try
        {
            AddChild(root, "NotRightHandGrip");
            AddChild(root, "LeftHandGrip");
            AddChild(root, "LookAtTarget");
            AddChild(root, "SeatedRoot");
            Assert.That(GmCharacterAssetContractInspector.HasRequiredSocketNames(
                root, GmCharacterAssetRole.Aldric), Is.False);
            AddChild(root, "RightHandGrip");
            Assert.That(GmCharacterAssetContractInspector.HasRequiredSocketNames(
                root, GmCharacterAssetRole.Aldric), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void HumanHeightMeasurementIncludesSeparatedRendererTransforms()
    {
        var root = new GameObject("Character");
        try
        {
            SkinnedMeshRenderer legs = AddSkinnedBounds(root, "Legs", 0.45f, 0.9f);
            SkinnedMeshRenderer torso = AddSkinnedBounds(root, "Torso", 1.35f, 0.9f);
            Assert.That(GmCharacterAssetContractInspector.MeasureHumanHeight(
                new[] { legs, torso }), Is.EqualTo(1.8f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RetainedSourceMustStayInsideTheInspectedVendorProductRoot()
    {
        const string raw = "Assets/ThirdParty/Vendor/Product/Exports/Aldric.fbx";
        Assert.That(GmCharacterAssetContractInspector.IsRetainedSourceBound(raw,
            "Assets/ThirdParty/Vendor/Product/Source/Aldric.blend"), Is.True);
        Assert.That(GmCharacterAssetContractInspector.IsRetainedSourceBound(raw,
            "Assets/ThirdParty/Other/Product/Source/Aldric.blend"), Is.False);
        Assert.That(GmCharacterAssetContractInspector.IsRetainedSourceBound(raw,
            "Assets/ThirdParty/Vendor/Other/Source/Aldric.blend"), Is.False);
        Assert.That(GmCharacterAssetContractInspector.IsRetainedSourceBound(raw,
            "Assets/ThirdParty/Vendor/Product/../Other/Aldric.blend"), Is.False);
    }

    [Test]
    public void AssetPathsResolveFromTheUnityProjectInsteadOfTheAmbientWorkingDirectory()
    {
        string original = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            string expected = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Assets/GamesMaster/Characters/Aldric/provenance.json");
            Assert.That(GmCharacterAssetContractInspector.ResolveProjectAssetPath(
                "Assets/GamesMaster/Characters/Aldric/provenance.json"), Is.EqualTo(expected));
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }
    }

    [Test]
    public void HandGripSocketsMustBelongToTheirMappedHands()
    {
        var root = new GameObject("Character");
        try
        {
            Transform leftHand = NewTransform(root, "LeftHand", null);
            Transform rightHand = NewTransform(root, "RightHand", null);
            AddChild(root, "RightHandGrip");
            AddChild(root, "LeftHandGrip");
            AddChild(root, "LookAtTarget");
            AddChild(root, "SeatedRoot");
            Assert.That(GmCharacterAssetContractInspector.HasRequiredSockets(
                root, GmCharacterAssetRole.Aldric, leftHand, rightHand), Is.False);

            UnityEngine.Object.DestroyImmediate(root.transform.Find("RightHandGrip").gameObject);
            UnityEngine.Object.DestroyImmediate(root.transform.Find("LeftHandGrip").gameObject);
            NewTransform(root, "LeftHandGrip", leftHand);
            NewTransform(root, "RightHandGrip", rightHand);
            Assert.That(GmCharacterAssetContractInspector.HasRequiredSockets(
                root, GmCharacterAssetRole.Aldric, leftHand, rightHand), Is.True);

            Transform hips = NewTransform(root, "Hips", null);
            AddChild(root, "BodyAnchor");
            Assert.That(GmCharacterAssetContractInspector.HasRequiredSockets(
                root, GmCharacterAssetRole.PlayerBody, leftHand, rightHand, hips), Is.False);
            root.transform.Find("BodyAnchor").SetParent(hips, false);
            Assert.That(GmCharacterAssetContractInspector.HasRequiredSockets(
                root, GmCharacterAssetRole.PlayerBody, leftHand, rightHand, hips), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void UpperBodyMaskMustActuallySelectBodyHeadArmsAndFingersOnly()
    {
        var mask = new AvatarMask();
        try
        {
            for (int index = 0; index < (int)AvatarMaskBodyPart.LastBodyPart; index++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)index, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            Assert.That(GmCharacterAssetContractInspector.IsUpperBodyMask(mask), Is.True);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
            Assert.That(GmCharacterAssetContractInspector.IsUpperBodyMask(mask), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mask);
        }
    }

    [Test]
    public void RigBuilderNeedsAnActiveOwnedLayerWithAValidConstraint()
    {
        var root = new GameObject("Character");
        try
        {
            root.AddComponent<Animator>();
            var builderObject = new GameObject("RigBuilder");
            builderObject.transform.SetParent(root.transform, false);
            RigBuilder builder = builderObject.AddComponent<RigBuilder>();
            AddChild(root, "RigConstraints");

            Transform constraints = root.transform.Find("RigConstraints");
            Transform leftArm = NewTransform(root, "MappedLeftUpperArm", null);
            Transform leftMid = NewTransform(root, "MappedLeftLowerArm", leftArm);
            Transform leftHand = NewTransform(root, "MappedLeftHand", leftMid);
            Transform rightArm = NewTransform(root, "MappedRightUpperArm", null);
            Transform rightMid = NewTransform(root, "MappedRightLowerArm", rightArm);
            Transform rightHand = NewTransform(root, "MappedRightHand", rightMid);
            Assert.That(GmCharacterAssetContractInspector.HasPopulatedRigBuilder(
                root, leftArm, leftHand, rightArm, rightHand), Is.False);
            var rigObject = new GameObject("CardContactRig");
            rigObject.transform.SetParent(constraints, false);
            Rig rig = rigObject.AddComponent<Rig>();
            TwoBoneIKConstraint constraint = rigObject.AddComponent<TwoBoneIKConstraint>();
            Transform armRoot = NewTransform(rigObject, "UpperArm", null);
            Transform mid = NewTransform(rigObject, "LowerArm", armRoot);
            Transform tip = NewTransform(rigObject, "Hand", mid);
            Transform target = NewTransform(rigObject, "Target", null);
            TwoBoneIKConstraintData data = constraint.data;
            data.root = armRoot;
            data.mid = mid;
            data.tip = tip;
            data.target = target;
            constraint.data = data;
            builder.layers.Add(new RigLayer(rig, true));

            Assert.That(GmCharacterAssetContractInspector.HasPopulatedRigBuilder(
                root, leftArm, leftHand, rightArm, rightHand), Is.False,
                "an unrelated valid constraint is not a mapped hand/card rig");

            data.root = leftArm;
            data.mid = leftMid;
            data.tip = leftHand;
            constraint.data = data;
            rig.weight = 0f;
            Assert.That(GmCharacterAssetContractInspector.HasPopulatedRigBuilder(
                root, leftArm, leftHand, rightArm, rightHand), Is.False);
            rig.weight = 1f;
            Assert.That(GmCharacterAssetContractInspector.HasPopulatedRigBuilder(
                root, leftArm, leftHand, rightArm, rightHand), Is.True);
            builderObject.SetActive(false);
            Assert.That(GmCharacterAssetContractInspector.HasPopulatedRigBuilder(
                root, leftArm, leftHand, rightArm, rightHand), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void HdrpUnlitAndIncompletePbrTexturesFailTheMaterialContract()
    {
        Shader lit = Shader.Find("HDRP/Lit");
        Shader unlit = Shader.Find("HDRP/Unlit");
        Assert.That(lit, Is.Not.Null);
        Assert.That(unlit, Is.Not.Null);
        var litMaterial = new Material(lit);
        var unlitMaterial = new Material(unlit);
        var baseMap = new Texture2D(2, 2);
        var normalMap = new Texture2D(2, 2);
        var maskMap = new Texture2D(2, 2);
        try
        {
            Assert.That(GmCharacterAssetContractInspector.AreMaterialsHdrpLit(
                new[] { unlitMaterial }), Is.False);
            litMaterial.SetTexture("_BaseColorMap", baseMap);
            Assert.That(GmCharacterAssetContractInspector.HaveBudgetedPbrTextures(
                new[] { litMaterial }), Is.False);
            litMaterial.SetTexture("_NormalMap", normalMap);
            litMaterial.SetTexture("_MaskMap", maskMap);
            Assert.That(GmCharacterAssetContractInspector.AreMaterialsHdrpLit(
                new[] { litMaterial }), Is.True);
            Assert.That(GmCharacterAssetContractInspector.AreMaterialsHdrpLit(
                new Material[] { litMaterial, null }), Is.False);
            Assert.That(GmCharacterAssetContractInspector.HaveBudgetedPbrTextures(
                new[] { litMaterial }), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(litMaterial);
            UnityEngine.Object.DestroyImmediate(unlitMaterial);
            UnityEngine.Object.DestroyImmediate(baseMap);
            UnityEngine.Object.DestroyImmediate(normalMap);
            UnityEngine.Object.DestroyImmediate(maskMap);
        }
    }

    [Test]
    public void OnlyTheHdrpLitShaderCanSatisfyTheFirstPbrMapContract()
    {
        Shader layered = Shader.Find("HDRP/LayeredLit");
        if (layered == null) Assert.Ignore("HDRP LayeredLit shader is unavailable in this editor");
        var material = new Material(layered);
        try
        {
            Assert.That(GmCharacterAssetContractInspector.AreMaterialsHdrpLit(
                new[] { material }), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void LodContractRejectsNullOrNonReducingLevels()
    {
        var root = new GameObject("Character");
        var meshes = new System.Collections.Generic.List<Mesh>();
        try
        {
            LODGroup group = root.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.6f, new Renderer[] { null }),
                new LOD(0.3f, new Renderer[] { null }),
                new LOD(0.1f, new Renderer[] { null }),
            });
            Assert.That(GmCharacterAssetContractInspector.HasValidThreeLevelLod(root), Is.False);

            MeshRenderer high = AddMeshLevel(root, "LOD0", 4, meshes);
            MeshRenderer medium = AddMeshLevel(root, "LOD1", 2, meshes);
            MeshRenderer low = AddMeshLevel(root, "LOD2", 1, meshes);
            group.SetLODs(new[]
            {
                new LOD(0.6f, new Renderer[] { high }),
                new LOD(0.3f, new Renderer[] { medium }),
                new LOD(0.1f, new Renderer[] { low }),
            });
            Assert.That(GmCharacterAssetContractInspector.HasValidThreeLevelLod(root), Is.True);

            Assert.That(GmCharacterAssetContractInspector.AreLodTransitionsValid(
                new[] { 0.6f, 0.6f, 0.1f }), Is.False);
            Assert.That(GmCharacterAssetContractInspector.AreLodTransitionsValid(
                new[] { 0.6f, 0.3f, float.NaN }), Is.False);
            Assert.That(GmCharacterAssetContractInspector.AreLodTransitionsValid(
                new[] { 0.6f, 0.3f, 0f }), Is.False);
        }
        finally
        {
            foreach (Mesh mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void LodContractRejectsAnUngroupedShippingBodyRenderer()
    {
        var root = new GameObject("Character");
        var meshes = new System.Collections.Generic.List<Mesh>();
        try
        {
            LODGroup group = root.AddComponent<LODGroup>();
            MeshRenderer high = AddMeshLevel(root, "AccessoryLOD0", 4, meshes);
            MeshRenderer medium = AddMeshLevel(root, "AccessoryLOD1", 2, meshes);
            MeshRenderer low = AddMeshLevel(root, "AccessoryLOD2", 1, meshes);
            AddMeshLevel(root, "ShippingBody", 12, meshes);
            group.SetLODs(new[]
            {
                new LOD(0.6f, new Renderer[] { high }),
                new LOD(0.3f, new Renderer[] { medium }),
                new LOD(0.1f, new Renderer[] { low }),
            });

            Assert.That(GmCharacterAssetContractInspector.HasValidThreeLevelLod(root), Is.False);
        }
        finally
        {
            foreach (Mesh mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void LodContractAllowsAdditionalReducingLevelsAndBudgetsLodZeroOnly()
    {
        var root = new GameObject("Character");
        var meshes = new System.Collections.Generic.List<Mesh>();
        try
        {
            LODGroup group = root.AddComponent<LODGroup>();
            SkinnedMeshRenderer high = AddSkinnedMeshLevel(root, "LOD0", 8, meshes);
            SkinnedMeshRenderer medium = AddSkinnedMeshLevel(root, "LOD1", 4, meshes);
            SkinnedMeshRenderer low = AddSkinnedMeshLevel(root, "LOD2", 2, meshes);
            SkinnedMeshRenderer billboard = AddSkinnedMeshLevel(root, "LOD3", 1, meshes);
            group.SetLODs(new[]
            {
                new LOD(0.6f, new Renderer[] { high }),
                new LOD(0.3f, new Renderer[] { medium }),
                new LOD(0.1f, new Renderer[] { low }),
                new LOD(0.02f, new Renderer[] { billboard }),
            });

            Assert.That(GmCharacterAssetContractInspector.HasValidThreeLevelLod(root), Is.True);
            Assert.That(GmCharacterAssetContractInspector.GetPrimaryLodSkinnedRenderers(root),
                Is.EqualTo(new[] { high }));
        }
        finally
        {
            foreach (Mesh mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ConnectedBodyGeometryRejectsFloatingArmAndHandIslands()
    {
        var root = new GameObject("Character");
        var mesh = new Mesh();
        try
        {
            Transform torso = NewTransform(root, "Chest", null);
            Transform leftShoulder = NewTransform(root, "LeftShoulder", torso);
            Transform leftArm = NewTransform(root, "LeftUpperArm", leftShoulder);
            Transform leftHand = NewTransform(root, "LeftHand", leftArm);
            Transform rightShoulder = NewTransform(root, "RightShoulder", torso);
            Transform rightArm = NewTransform(root, "RightUpperArm", rightShoulder);
            Transform rightHand = NewTransform(root, "RightHand", rightArm);
            Transform[] bones =
                { torso, leftShoulder, leftArm, leftHand, rightShoulder, rightArm, rightHand };
            var bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = bodyObject.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = bones;
            mesh.vertices = new Vector3[8];
            mesh.boneWeights = new[]
            {
                Weight(0), Weight(1), Weight(2), Weight(3), Weight(4), Weight(5),
                Weight(6), Weight(0),
            };
            renderer.sharedMesh = mesh;

            mesh.triangles = new[] { 0, 1, 2, 4, 5, 6 };
            Assert.That(GmCharacterAssetContractInspector.HasConnectedPlayerBodyGeometry(
                new[] { renderer }, torso, leftShoulder, leftArm, leftHand,
                rightShoulder, rightArm, rightHand), Is.False);

            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6 };
            Assert.That(GmCharacterAssetContractInspector.HasConnectedPlayerBodyGeometry(
                new[] { renderer }, torso, leftShoulder, leftArm, leftHand,
                rightShoulder, rightArm, rightHand), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static GmCharacterAssetFacts ValidAldric()
    {
        return new GmCharacterAssetFacts
        {
            Role = GmCharacterAssetRole.Aldric,
            RawSourceUnderThirdParty = true,
            DerivedPrefabUnderOwnedCharacters = true,
            HasCommercialLicenseRecord = true,
            ProvenanceNamesVendorProductVersion = true,
            RedistributionTermsRecorded = true,
            ImporterIsHumanoid = true,
            AvatarIsHuman = true,
            AvatarIsValid = true,
            HasStableShoulderAndWristChains = true,
            HasFullThreeJointFingerChains = true,
            HasRiggedEyes = true,
            HasLidsBrowsAndJaw = true,
            HasDeclaredFacialBlendshapes = false,
            ScaleIsMeters = true,
            BoundsAreHumanScale = true,
            HasLod0Lod1Lod2 = true,
            HasRequiredSockets = true,
            HasAnimatorController = true,
            HasUpperBodyAvatarMask = true,
            HasAnimationRigBuilder = true,
            AllMaterialsAreHdrpLit = true,
            TextureBudgetIsTwoK = true,
            HasConnectedShouldersArmsAndBodyAnchor = true,
            TriangleCount = 120000,
            SkinnedRendererCount = 6,
            MaterialCount = 8,
        };
    }

    static void SetBool(GmCharacterAssetFacts facts, string property, bool value)
    {
        typeof(GmCharacterAssetFacts).GetProperty(property).SetValue(facts, value);
    }

    static void AddChild(GameObject root, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
    }

    static SkinnedMeshRenderer AddSkinnedBounds(GameObject root, string name,
        float localY, float height)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = new Vector3(0f, localY, 0f);
        SkinnedMeshRenderer renderer = child.AddComponent<SkinnedMeshRenderer>();
        renderer.localBounds = new Bounds(Vector3.zero, new Vector3(0.5f, height, 0.3f));
        return renderer;
    }

    static Transform NewTransform(GameObject root, string name, Transform parent)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent != null ? parent : root.transform, false);
        return child.transform;
    }

    static MeshRenderer AddMeshLevel(GameObject root, string name, int triangleCount,
        System.Collections.Generic.List<Mesh> meshes)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        var filter = child.AddComponent<MeshFilter>();
        var renderer = child.AddComponent<MeshRenderer>();
        var mesh = new Mesh();
        var vertices = new Vector3[triangleCount * 3];
        var triangles = new int[vertices.Length];
        for (int index = 0; index < triangles.Length; index++) triangles[index] = index;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        filter.sharedMesh = mesh;
        meshes.Add(mesh);
        return renderer;
    }

    static SkinnedMeshRenderer AddSkinnedMeshLevel(GameObject root, string name,
        int triangleCount, System.Collections.Generic.List<Mesh> meshes)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        var renderer = child.AddComponent<SkinnedMeshRenderer>();
        var mesh = new Mesh();
        var vertices = new Vector3[triangleCount * 3];
        var triangles = new int[vertices.Length];
        for (int index = 0; index < triangles.Length; index++) triangles[index] = index;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        renderer.sharedMesh = mesh;
        meshes.Add(mesh);
        return renderer;
    }

    static BoneWeight Weight(int boneIndex) => new BoneWeight
    {
        boneIndex0 = boneIndex,
        weight0 = 1f,
    };
}
