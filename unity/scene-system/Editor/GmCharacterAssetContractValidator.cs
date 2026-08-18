using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public enum GmCharacterAssetRole
{
    Aldric,
    PlayerBody,
}

/// <summary>Measured import/prefab facts. Tests exercise the policy without fabricating an asset.</summary>
public sealed class GmCharacterAssetFacts
{
    public GmCharacterAssetRole Role { get; set; }
    public bool RawSourceUnderThirdParty { get; set; }
    public bool DerivedPrefabUnderOwnedCharacters { get; set; }
    public bool HasCommercialLicenseRecord { get; set; }
    public bool ProvenanceNamesVendorProductVersion { get; set; }
    public bool RedistributionTermsRecorded { get; set; }
    public bool ImporterIsHumanoid { get; set; }
    public bool AvatarIsHuman { get; set; }
    public bool AvatarIsValid { get; set; }
    public bool HasStableShoulderAndWristChains { get; set; }
    public bool HasFullThreeJointFingerChains { get; set; }
    public bool HasRiggedEyes { get; set; }
    public bool HasLidsBrowsAndJaw { get; set; }
    public bool HasDeclaredFacialBlendshapes { get; set; }
    public bool ScaleIsMeters { get; set; }
    public bool BoundsAreHumanScale { get; set; }
    public bool HasLod0Lod1Lod2 { get; set; }
    public bool HasRequiredSockets { get; set; }
    public bool HasAnimatorController { get; set; }
    public bool HasUpperBodyAvatarMask { get; set; }
    public bool HasAnimationRigBuilder { get; set; }
    public bool AllMaterialsAreHdrpLit { get; set; }
    public bool TextureBudgetIsTwoK { get; set; }
    public bool HasConnectedShouldersArmsAndBodyAnchor { get; set; }
    public int TriangleCount { get; set; }
    public int SkinnedRendererCount { get; set; }
    public int MaterialCount { get; set; }
}

[Serializable]
public sealed class GmCharacterProvenanceRecord
{
    public string vendor;
    public string product;
    public string version;
    public string licenseName;
    public string licenseUrl;
    public string sourceFile;
    public string redistributionTerms;
    public bool commercialUseAllowed;
}

public static class GmCharacterAssetContractValidator
{
    public static IReadOnlyList<string> Validate(GmCharacterAssetFacts facts)
    {
        if (facts == null) throw new ArgumentNullException(nameof(facts));
        ValidateRole(facts.Role);
        var issues = new List<string>(24);
        Require(facts.RawSourceUnderThirdParty, issues,
            "raw source must remain under Assets/ThirdParty/<Vendor>/<Product>");
        Require(facts.DerivedPrefabUnderOwnedCharacters, issues,
            "derived prefab must live under Assets/GamesMaster/Characters/<Character>");
        Require(facts.HasCommercialLicenseRecord, issues,
            "commercial license record is missing or does not permit commercial use");
        Require(facts.ProvenanceNamesVendorProductVersion, issues,
            "provenance must name vendor, product, and version");
        Require(facts.RedistributionTermsRecorded, issues,
            "redistribution terms and retained source file must be recorded");

        Require(facts.ImporterIsHumanoid, issues, "source importer must use Humanoid animation");
        Require(facts.AvatarIsHuman, issues, "prefab needs a human Avatar");
        Require(facts.AvatarIsValid, issues, "prefab needs a valid Avatar");
        Require(facts.HasStableShoulderAndWristChains, issues,
            "Humanoid map needs stable shoulder, elbow, wrist, and hand chains on both sides");
        Require(facts.HasFullThreeJointFingerChains, issues,
            "both hands need full three-joint finger chains");
        Require(facts.HasRiggedEyes &&
            (facts.HasLidsBrowsAndJaw || facts.HasDeclaredFacialBlendshapes), issues,
            "face needs rigged eyes plus lids, brows, and jaw or declared facial blendshapes");

        Require(facts.ScaleIsMeters, issues, "import scale must be authored and verified in meters");
        Require(facts.BoundsAreHumanScale, issues, "prefab bounds must be plausible human scale");
        Require(facts.HasLod0Lod1Lod2, issues, "prefab needs populated LOD0/LOD1/LOD2 levels");
        Require(facts.HasRequiredSockets, issues,
            "required hand grips, gaze, seated-root, and body-anchor sockets are missing");
        Require(facts.HasAnimatorController, issues, "runtime Animator Controller is missing");
        Require(facts.HasUpperBodyAvatarMask, issues, "upper-body Avatar Mask is missing");
        Require(facts.HasAnimationRigBuilder, issues,
            "Animation Rigging RigBuilder/constraint root is missing");
        Require(facts.AllMaterialsAreHdrpLit, issues,
            "every character material must use the HDRP/Lit shader");
        Require(facts.TextureBudgetIsTwoK, issues,
            "character PBR textures must be present and stay within the default 2K budget");

        if (facts.Role == GmCharacterAssetRole.Aldric)
        {
            if (facts.TriangleCount > 120000) issues.Add("Aldric exceeds the 120000 triangle budget");
            if (facts.SkinnedRendererCount > 6) issues.Add("Aldric exceeds 6 skinned renderers");
            if (facts.MaterialCount > 8) issues.Add("Aldric exceeds 8 materials");
        }
        else
        {
            if (facts.TriangleCount > 80000) issues.Add("player body exceeds the 80000 triangle budget");
            Require(facts.HasConnectedShouldersArmsAndBodyAnchor, issues,
                "player body needs connected shoulders, arms, hands, and a seated body anchor");
        }
        return issues;
    }

    internal static void ValidateRole(GmCharacterAssetRole role)
    {
        if (role != GmCharacterAssetRole.Aldric && role != GmCharacterAssetRole.PlayerBody)
            throw new ArgumentOutOfRangeException(nameof(role));
    }

    static void Require(bool condition, List<string> issues, string issue)
    {
        if (!condition) issues.Add(issue);
    }
}

/// <summary>
/// Reads a real imported model, owned prefab, controller, materials, and provenance JSON into the
/// contract above. Nothing is generated or repaired here: a missing final asset stays a hard red.
/// </summary>
public static class GmCharacterAssetContractInspector
{
    static readonly HumanBodyBones[] ArmBones =
    {
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
    };

    static readonly HumanBodyBones[] FingerBones =
    {
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate,
        HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexProximal,
        HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate,
        HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingProximal,
        HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate,
        HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightThumbProximal,
        HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate,
        HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleProximal,
        HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate,
        HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleProximal,
        HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
    };

    public static IReadOnlyList<string> ValidateFromPaths(GmCharacterAssetRole role,
        string rawModelPath, string derivedPrefabPath, string provenanceJsonPath)
    {
        GmCharacterAssetFacts facts = Inspect(role, rawModelPath, derivedPrefabPath,
            provenanceJsonPath);
        return GmCharacterAssetContractValidator.Validate(facts);
    }

    public static GmCharacterAssetFacts Inspect(GmCharacterAssetRole role,
        string rawModelPath, string derivedPrefabPath, string provenanceJsonPath)
    {
        GmCharacterAssetContractValidator.ValidateRole(role);
        var facts = new GmCharacterAssetFacts { Role = role };
        string ownedFolder = role == GmCharacterAssetRole.Aldric ? "Aldric" : "Player";
        facts.RawSourceUnderThirdParty = IsUnder(rawModelPath, "Assets/ThirdParty/");
        facts.DerivedPrefabUnderOwnedCharacters = IsUnder(derivedPrefabPath,
            $"Assets/GamesMaster/Characters/{ownedFolder}/");

        ReadProvenance(provenanceJsonPath, rawModelPath, facts);
        ModelImporter importer = AssetImporter.GetAtPath(rawModelPath) as ModelImporter;
        facts.ImporterIsHumanoid = importer != null && importer.animationType == ModelImporterAnimationType.Human;
        facts.ScaleIsMeters = importer != null && Mathf.Approximately(importer.globalScale, 1f);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(derivedPrefabPath);
        if (prefab == null) return facts;
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        facts.HasAnimatorController = animator != null && animator.runtimeAnimatorController != null;
        facts.AvatarIsHuman = animator != null && animator.avatar != null && animator.avatar.isHuman;
        facts.AvatarIsValid = animator != null && animator.avatar != null && animator.avatar.isValid;
        if (facts.AvatarIsHuman && facts.AvatarIsValid)
        {
            facts.HasStableShoulderAndWristChains = AllBonesPresent(animator, ArmBones);
            facts.HasFullThreeJointFingerChains = AllBonesPresent(animator, FingerBones);
            facts.HasRiggedEyes = animator.GetBoneTransform(HumanBodyBones.LeftEye) != null &&
                animator.GetBoneTransform(HumanBodyBones.RightEye) != null;
            facts.HasLidsBrowsAndJaw = animator.GetBoneTransform(HumanBodyBones.Jaw) != null &&
                HasTransformContaining(prefab, "lid") && HasTransformContaining(prefab, "brow");
        }

        facts.HasLod0Lod1Lod2 = HasValidThreeLevelLod(prefab);
        Renderer[] primaryRenderers = GetPrimaryLodRenderers(prefab);
        SkinnedMeshRenderer[] skinned = primaryRenderers.OfType<SkinnedMeshRenderer>().ToArray();
        facts.SkinnedRendererCount = skinned.Length;
        facts.TriangleCount = primaryRenderers.Sum(item => (int)TriangleCount(item));
        facts.HasDeclaredFacialBlendshapes = skinned.Any(HasFacialBlendshapes);
        facts.BoundsAreHumanScale = MeasureHumanHeight(skinned) is >= 1.45f and <= 2.20f;

        Transform leftHand = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
        Transform rightHand = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        Transform hips = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        facts.HasRequiredSockets = HasRequiredSockets(prefab, role, leftHand, rightHand, hips);
        Transform torso = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.Chest) ??
              animator.GetBoneTransform(HumanBodyBones.Spine) : null;
        Transform leftArm = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
        Transform leftShoulder = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.LeftShoulder) : null;
        Transform rightArm = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
        Transform rightShoulder = animator != null && facts.AvatarIsHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightShoulder) : null;
        facts.HasConnectedShouldersArmsAndBodyAnchor = role == GmCharacterAssetRole.PlayerBody &&
            HasOwnedBodyAnchor(prefab, hips) && HasConnectedPlayerBodyGeometry(
                skinned, torso, leftShoulder, leftArm, leftHand,
                rightShoulder, rightArm, rightHand);

        facts.HasUpperBodyAvatarMask = HasUpperBodyMask(animator?.runtimeAnimatorController);
        facts.HasAnimationRigBuilder = HasPopulatedRigBuilder(
            prefab, leftArm, leftHand, rightArm, rightHand);
        Material[] materialSlots = primaryRenderers.SelectMany(item => item.sharedMaterials).ToArray();
        facts.MaterialCount = materialSlots.Where(material => material != null).Distinct().Count();
        facts.AllMaterialsAreHdrpLit = AreMaterialsHdrpLit(materialSlots);
        facts.TextureBudgetIsTwoK = HaveBudgetedPbrTextures(materialSlots);
        return facts;
    }

    static void ReadProvenance(string path, string rawModelPath, GmCharacterAssetFacts facts)
    {
        string absolutePath = ResolveProjectAssetPath(path);
        if (string.IsNullOrWhiteSpace(path) || !IsUnder(path, "Assets/GamesMaster/Characters/") ||
            !File.Exists(absolutePath)) return;
        try
        {
            GmCharacterProvenanceRecord record = JsonUtility.FromJson<GmCharacterProvenanceRecord>(
                File.ReadAllText(absolutePath));
            if (record == null) return;
            facts.HasCommercialLicenseRecord = record.commercialUseAllowed &&
                !string.IsNullOrWhiteSpace(record.licenseName) &&
                !string.IsNullOrWhiteSpace(record.licenseUrl);
            facts.ProvenanceNamesVendorProductVersion = !string.IsNullOrWhiteSpace(record.vendor) &&
                !string.IsNullOrWhiteSpace(record.product) && !string.IsNullOrWhiteSpace(record.version);
            facts.RedistributionTermsRecorded = !string.IsNullOrWhiteSpace(record.redistributionTerms) &&
                IsRetainedSourceBound(rawModelPath, record.sourceFile) &&
                File.Exists(ResolveProjectAssetPath(record.sourceFile));
        }
        catch (Exception)
        {
            // Malformed provenance is a failed fact set, not a reason to guess a license.
        }
    }

    static bool AllBonesPresent(Animator animator, IEnumerable<HumanBodyBones> bones) =>
        bones.All(bone => animator.GetBoneTransform(bone) != null);

    public static bool IsRetainedSourceBound(string rawModelPath, string retainedSourcePath)
    {
        string raw = NormalizeAssetPath(rawModelPath);
        string retained = NormalizeAssetPath(retainedSourcePath);
        if (raw == null || retained == null) return false;
        string[] rawParts = raw.Split('/');
        if (rawParts.Length < 5 || rawParts[0] != "Assets" || rawParts[1] != "ThirdParty")
            return false;
        string productRoot = string.Join("/", rawParts.Take(4));
        return retained.StartsWith(productRoot + "/", StringComparison.Ordinal);
    }

    static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        if (parts.Any(part => part.Length == 0 || part == "." || part == "..")) return null;
        return string.Join("/", parts);
    }

    public static string ResolveProjectAssetPath(string assetPath)
    {
        string normalized = NormalizeAssetPath(assetPath);
        if (normalized == null || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            return string.Empty;
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return projectRoot == null ? string.Empty : Path.Combine(projectRoot, normalized);
    }

    public static bool HasRequiredSocketNames(GameObject root, GmCharacterAssetRole role)
    {
        if (root == null) return false;
        return HasTransformNamed(root, "RightHandGrip") &&
            HasTransformNamed(root, "LeftHandGrip") &&
            HasTransformNamed(root, "LookAtTarget") &&
            HasTransformNamed(root, "SeatedRoot") &&
            (role == GmCharacterAssetRole.Aldric || HasTransformNamed(root, "BodyAnchor"));
    }

    public static bool HasRequiredSockets(GameObject root, GmCharacterAssetRole role,
        Transform leftHand, Transform rightHand, Transform bodyRoot = null)
    {
        if (root == null || leftHand == null || rightHand == null ||
            !leftHand.IsChildOf(root.transform) || !rightHand.IsChildOf(root.transform)) return false;
        Transform leftGrip = FindUniqueTransform(root, "LeftHandGrip");
        Transform rightGrip = FindUniqueTransform(root, "RightHandGrip");
        Transform lookAt = FindUniqueTransform(root, "LookAtTarget");
        Transform seatedRoot = FindUniqueTransform(root, "SeatedRoot");
        Transform bodyAnchor = role == GmCharacterAssetRole.PlayerBody
            ? FindUniqueTransform(root, "BodyAnchor") : null;
        return leftGrip != null && leftGrip.IsChildOf(leftHand) && IsSocketPoseValid(leftGrip) &&
            rightGrip != null && rightGrip.IsChildOf(rightHand) && IsSocketPoseValid(rightGrip) &&
            lookAt != null && IsSocketPoseValid(lookAt) &&
            seatedRoot != null && IsSocketPoseValid(seatedRoot) &&
            (role == GmCharacterAssetRole.Aldric ||
             (bodyAnchor != null && bodyRoot != null && bodyAnchor.IsChildOf(bodyRoot) &&
              IsSocketPoseValid(bodyAnchor)));
    }

    static bool HasOwnedBodyAnchor(GameObject root, Transform bodyRoot)
    {
        Transform bodyAnchor = FindUniqueTransform(root, "BodyAnchor");
        return bodyRoot != null && bodyAnchor != null && bodyAnchor.IsChildOf(bodyRoot) &&
            IsSocketPoseValid(bodyAnchor);
    }

    static Transform FindUniqueTransform(GameObject root, string exactName)
    {
        Transform[] matches = root.GetComponentsInChildren<Transform>(true).Where(transform =>
            string.Equals(transform.name, exactName, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    static bool IsSocketPoseValid(Transform socket)
    {
        Vector3 position = socket.localPosition;
        Quaternion rotation = socket.localRotation;
        Vector3 scale = socket.localScale;
        return IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z) &&
            IsFinite(rotation.x) && IsFinite(rotation.y) && IsFinite(rotation.z) &&
            IsFinite(rotation.w) && IsFinite(scale.x) && IsFinite(scale.y) && IsFinite(scale.z) &&
            Vector3.SqrMagnitude(scale - Vector3.one) <= 0.000001f;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static bool HasTransformNamed(GameObject root, string exactName) =>
        root.GetComponentsInChildren<Transform>(true).Any(transform =>
            string.Equals(transform.name, exactName, StringComparison.Ordinal));

    static bool HasTransformContaining(GameObject root, string token) =>
        root.GetComponentsInChildren<Transform>(true).Any(transform =>
            transform.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

    public static bool HasPopulatedRigBuilder(GameObject root, Transform leftArm,
        Transform leftHand, Transform rightArm, Transform rightHand)
    {
        if (root == null || leftArm == null || leftHand == null ||
            rightArm == null || rightHand == null) return false;
        Transform constraintsRoot = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(
            transform => string.Equals(transform.name, "RigConstraints", StringComparison.Ordinal));
        if (constraintsRoot == null) return false;
        foreach (RigBuilder builder in root.GetComponentsInChildren<RigBuilder>(true))
        {
            if (!builder.enabled || !builder.gameObject.activeInHierarchy ||
                builder.layers == null) continue;
            foreach (RigLayer layer in builder.layers)
            {
                Rig rig = layer?.rig;
                if (rig == null || !layer.active || !rig.enabled || rig.weight <= 0f ||
                    !rig.gameObject.activeInHierarchy ||
                    !rig.transform.IsChildOf(constraintsRoot)) continue;
                bool validConstraint = rig.GetComponentsInChildren<TwoBoneIKConstraint>(true)
                    .Any(constraint => IsMappedHandConstraint(constraint, constraintsRoot,
                        leftArm, leftHand, rightArm, rightHand));
                if (validConstraint) return true;
            }
        }
        return false;
    }

    static bool IsMappedHandConstraint(TwoBoneIKConstraint constraint, Transform constraintsRoot,
        Transform leftArm, Transform leftHand, Transform rightArm, Transform rightHand)
    {
        if (constraint == null || !constraint.enabled || !constraint.gameObject.activeInHierarchy ||
            constraint.weight <= 0f || !constraint.IsValid()) return false;
        TwoBoneIKConstraintData data = constraint.data;
        bool mappedChain = (data.root == leftArm && data.tip == leftHand) ||
            (data.root == rightArm && data.tip == rightHand);
        return mappedChain && data.mid != null && data.mid.IsChildOf(data.root) &&
            data.tip.IsChildOf(data.mid) && data.target != null &&
            data.target.IsChildOf(constraintsRoot);
    }

    public static bool HasValidThreeLevelLod(GameObject root)
    {
        if (root == null) return false;
        LODGroup group = root.GetComponentInChildren<LODGroup>(true);
        if (group == null) return false;
        LOD[] levels = group.GetLODs();
        if (levels.Length < 3) return false;
        if (!AreLodTransitionsValid(levels.Select(level =>
            level.screenRelativeTransitionHeight))) return false;
        var seen = new HashSet<Renderer>();
        long previousTriangles = long.MaxValue;
        for (int index = 0; index < levels.Length; index++)
        {
            Renderer[] renderers = levels[index].renderers;
            if (renderers == null || renderers.Length == 0 || renderers.Any(renderer => renderer == null))
                return false;
            long triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                if (!seen.Add(renderer)) return false;
                triangles += TriangleCount(renderer);
            }
            if (triangles <= 0 || triangles >= previousTriangles) return false;
            previousTriangles = triangles;
        }
        Renderer[] shippingRenderers = root.GetComponentsInChildren<Renderer>(true);
        return shippingRenderers.Length > 0 && shippingRenderers.All(seen.Contains);
    }

    public static bool AreLodTransitionsValid(IEnumerable<float> transitions)
    {
        if (transitions == null) return false;
        int count = 0;
        float previous = float.PositiveInfinity;
        foreach (float transition in transitions)
        {
            if (float.IsNaN(transition) || float.IsInfinity(transition) || transition <= 0f ||
                transition > 1f || transition >= previous) return false;
            previous = transition;
            count++;
        }
        return count >= 3;
    }

    public static SkinnedMeshRenderer[] GetPrimaryLodSkinnedRenderers(GameObject root)
    {
        return GetPrimaryLodRenderers(root).OfType<SkinnedMeshRenderer>().ToArray();
    }

    static Renderer[] GetPrimaryLodRenderers(GameObject root)
    {
        if (root == null) return Array.Empty<Renderer>();
        LODGroup group = root.GetComponentInChildren<LODGroup>(true);
        if (group == null) return Array.Empty<Renderer>();
        LOD[] levels = group.GetLODs();
        if (levels.Length == 0 || levels[0].renderers == null)
            return Array.Empty<Renderer>();
        return levels[0].renderers;
    }

    public static bool HasConnectedPlayerBodyGeometry(
        IEnumerable<SkinnedMeshRenderer> renderers, Transform torso, Transform leftShoulder,
        Transform leftArm, Transform leftHand, Transform rightShoulder, Transform rightArm,
        Transform rightHand)
    {
        if (renderers == null || torso == null || leftShoulder == null || leftArm == null ||
            leftHand == null || rightShoulder == null || rightArm == null || rightHand == null)
            return false;
        Transform[] targets =
            { torso, leftShoulder, leftArm, leftHand, rightShoulder, rightArm, rightHand };
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMesh == null || renderer.bones == null) continue;
            int[] targetBoneIndices = targets.Select(target => Array.IndexOf(renderer.bones, target))
                .ToArray();
            if (targetBoneIndices.Any(index => index < 0)) continue;
            Mesh mesh = renderer.sharedMesh;
            BoneWeight[] weights = mesh.boneWeights;
            int vertexCount = mesh.vertexCount;
            if (vertexCount == 0 || weights.Length != vertexCount) continue;
            int[] parents = Enumerable.Range(0, vertexCount).ToArray();
            int[] triangles = mesh.triangles;
            if (triangles.Length == 0 || triangles.Length % 3 != 0 ||
                triangles.Any(index => index < 0 || index >= vertexCount)) continue;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Union(parents, triangles[index], triangles[index + 1]);
                Union(parents, triangles[index], triangles[index + 2]);
            }

            int[] componentMasks = new int[vertexCount];
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                BoneWeight weight = weights[vertex];
                int mask = 0;
                for (int target = 0; target < targetBoneIndices.Length; target++)
                {
                    int boneIndex = targetBoneIndices[target];
                    if ((weight.boneIndex0 == boneIndex && weight.weight0 > 0.01f) ||
                        (weight.boneIndex1 == boneIndex && weight.weight1 > 0.01f) ||
                        (weight.boneIndex2 == boneIndex && weight.weight2 > 0.01f) ||
                        (weight.boneIndex3 == boneIndex && weight.weight3 > 0.01f))
                        mask |= 1 << target;
                }
                componentMasks[Find(parents, vertex)] |= mask;
            }
            int completeMask = (1 << targets.Length) - 1;
            if (componentMasks.Any(mask => mask == completeMask)) return true;
        }
        return false;
    }

    static int Find(int[] parents, int value)
    {
        int root = value;
        while (parents[root] != root) root = parents[root];
        while (parents[value] != value)
        {
            int next = parents[value];
            parents[value] = root;
            value = next;
        }
        return root;
    }

    static void Union(int[] parents, int left, int right)
    {
        int leftRoot = Find(parents, left);
        int rightRoot = Find(parents, right);
        if (leftRoot != rightRoot) parents[rightRoot] = leftRoot;
    }

    static long TriangleCount(Renderer renderer)
    {
        Mesh mesh = renderer is SkinnedMeshRenderer skinned
            ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        return mesh == null ? 0 : mesh.triangles.LongLength / 3;
    }

    static bool HasFacialBlendshapes(SkinnedMeshRenderer renderer)
    {
        Mesh mesh = renderer.sharedMesh;
        if (mesh == null) return false;
        bool eye = false;
        bool lidOrBlink = false;
        bool brow = false;
        bool jawOrMouth = false;
        for (int index = 0; index < mesh.blendShapeCount; index++)
        {
            string name = mesh.GetBlendShapeName(index).ToLowerInvariant();
            eye |= name.Contains("eye");
            lidOrBlink |= name.Contains("lid") || name.Contains("blink");
            brow |= name.Contains("brow");
            jawOrMouth |= name.Contains("jaw") || name.Contains("mouth");
        }
        return eye && lidOrBlink && brow && jawOrMouth;
    }

    public static float MeasureHumanHeight(IEnumerable<SkinnedMeshRenderer> renderers)
    {
        if (renderers == null) throw new ArgumentNullException(nameof(renderers));
        bool hasBounds = false;
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer == null) continue;
            Bounds bounds = renderer.localBounds;
            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    center.x + ((corner & 1) == 0 ? -extents.x : extents.x),
                    center.y + ((corner & 2) == 0 ? -extents.y : extents.y),
                    center.z + ((corner & 4) == 0 ? -extents.z : extents.z));
                float y = matrix.MultiplyPoint3x4(local).y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }
            hasBounds = true;
        }
        return hasBounds ? max - min : 0f;
    }

    static bool HasUpperBodyMask(RuntimeAnimatorController controller)
    {
        AnimatorController authored = controller as AnimatorController;
        return authored != null && authored.layers.Any(layer => IsUpperBodyMask(layer.avatarMask));
    }

    public static bool IsUpperBodyMask(AvatarMask mask)
    {
        if (mask == null) return false;
        AvatarMaskBodyPart[] required =
        {
            AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
        };
        AvatarMaskBodyPart[] rejected =
        {
            AvatarMaskBodyPart.Root, AvatarMaskBodyPart.LeftLeg, AvatarMaskBodyPart.RightLeg,
            AvatarMaskBodyPart.LeftFootIK, AvatarMaskBodyPart.RightFootIK,
        };
        return required.All(mask.GetHumanoidBodyPartActive) &&
            rejected.All(part => !mask.GetHumanoidBodyPartActive(part));
    }

    public static bool AreMaterialsHdrpLit(IEnumerable<Material> materials)
    {
        if (materials == null) return false;
        Material[] slots = materials.ToArray();
        Material[] owned = slots.Where(material => material != null).ToArray();
        if (owned.Length == 0 || owned.Length != slots.Length) return false;
        return owned.All(material => material.shader != null &&
            material.shader.name == "HDRP/Lit");
    }

    public static bool HaveBudgetedPbrTextures(IEnumerable<Material> materials)
    {
        if (materials == null) return false;
        int materialCount = 0;
        foreach (Material material in materials)
        {
            if (material == null) return false;
            materialCount++;
            string[] requiredMaps = { "_BaseColorMap", "_NormalMap", "_MaskMap" };
            for (int index = 0; index < requiredMaps.Length; index++)
            {
                if (!material.HasProperty(requiredMaps[index])) return false;
                Texture texture = material.GetTexture(requiredMaps[index]);
                if (texture == null) return false;
                if (texture.width > 2048 || texture.height > 2048) return false;
            }
        }
        return materialCount > 0;
    }

    static bool IsUnder(string path, string prefix) => !string.IsNullOrWhiteSpace(path) &&
        path.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal);
}
