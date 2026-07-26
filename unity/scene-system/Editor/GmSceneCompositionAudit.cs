// Objective composition checks shared by generated scenes. These checks reject missing intent,
// orphan props, unanchored clusters, palette monoculture, blocked routes, violated negative space,
// unmotivated local lights and review shots that do not frame their declared subjects. They are a
// floor beneath visual review, not a numeric claim that the scene is beautiful.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmSceneCompositionAudit
{
    const float PositionEpsilon = 0.01f;

    public static List<string> ValidateOpenScene(string expectedSceneId,
        GmSceneReviewTour reviewTour = null, Camera reviewCamera = null)
    {
        return AnalyzeOpenScene(expectedSceneId, reviewTour, reviewCamera).findings
            .FindAll(item => item.severity == GmAuditSeverity.Error)
            .ConvertAll(item => string.IsNullOrWhiteSpace(item.actual) && string.IsNullOrWhiteSpace(item.expected) ?
                item.message : $"{item.message} (actual {item.actual}; expected {item.expected})");
    }

    public static GmSceneAuditReport AnalyzeOpenScene(string expectedSceneId,
        GmSceneReviewTour reviewTour = null, Camera reviewCamera = null)
    {
        var report = new GmSceneAuditReport {
            sceneId = expectedSceneId,
            fingerprint = GmSceneFingerprint.Current(),
        };
        foreach (string issue in ValidateLegacy(expectedSceneId, reviewTour, reviewCamera))
            report.Add("structural", GmAuditSeverity.Error, expectedSceneId, issue);
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            GmPerceptualAudit.Analyze(scene, reviewTour,
                reviewCamera != null ? reviewCamera : Camera.main, report);
        return report;
    }

    static List<string> ValidateLegacy(string expectedSceneId,
        GmSceneReviewTour reviewTour = null, Camera reviewCamera = null)
    {
        var issues = new List<string>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            issues.Add("composition audit cannot inspect an invalid active scene");
            return issues;
        }

        var manifests = FindInScene<GmSceneComposition>(scene);
        if (manifests.Count != 1)
        {
            issues.Add($"expected exactly one GmSceneComposition, found {manifests.Count}");
            return issues;
        }

        GmSceneComposition manifest = manifests[0];
        ValidateText(manifest.SceneId, "composition scene id", issues);
        ValidateText(manifest.VisualIntent, "composition visual intent", issues, 12);
        if (manifest.SceneId != expectedSceneId)
            issues.Add($"composition scene id is '{manifest.SceneId}', expected '{expectedSceneId}'");

        var zones = FindInScene<GmCompositionZone>(scene);
        var clusters = FindInScene<GmCompositionCluster>(scene);
        var elements = FindInScene<GmCompositionElement>(scene);
        var routes = FindInScene<GmRouteReservation>(scene);
        var spaces = FindInScene<GmNegativeSpace>(scene);
        var motivatedLights = FindInScene<GmMotivatedLight>(scene);
        var lightIntents = FindInScene<GmLightIntent>(scene);
        var audioIntents = FindInScene<GmAudioIntent>(scene);
        var adaptiveSlots = FindInScene<GmAdaptiveSlot>(scene);
        var claims = FindInScene<GmReviewCompositionClaim>(scene);

        if (zones.Count < manifest.MinimumZones)
            issues.Add($"composition has {zones.Count} zone(s), needs at least {manifest.MinimumZones}");
        if (clusters.Count < manifest.MinimumClusters)
            issues.Add($"composition has {clusters.Count} cluster(s), needs at least {manifest.MinimumClusters}");
        if (elements.Count < manifest.MinimumElements)
            issues.Add($"composition has {elements.Count} element(s), needs at least {manifest.MinimumElements}");

        var zoneMap = UniqueMap(zones, zone => zone.ZoneId, "zone", issues);
        var clusterMap = UniqueMap(clusters, cluster => cluster.ClusterId, "cluster", issues);
        var elementMap = UniqueMap(elements, element => element.ElementId, "element", issues);
        var routeMap = UniqueMap(routes, route => route.RouteId, "route", issues);
        UniqueMap(spaces, space => space.SpaceId, "negative space", issues);
        UniqueMap(motivatedLights, marker => marker.LightId, "motivated light", issues);
        UniqueMap(lightIntents, marker => marker.IntentId, "light intent", issues);
        UniqueMap(audioIntents, marker => marker.IntentId, "audio intent", issues);
        UniqueMap(adaptiveSlots, marker => marker.SlotId, "adaptive slot", issues);
        var claimMap = UniqueMap(claims, claim => claim.ShotName, "review claim", issues);

        ValidateZones(zones, clusters, elements, issues);
        ValidateClusters(clusters, elements, zoneMap, elementMap, issues);
        ValidateElements(elements, clusterMap, elementMap, routeMap, issues);
        ValidateTransformCollisions(elements, issues);
        ValidateNegativeSpace(spaces, elements, issues);
        ValidateRoutes(routes, elements, issues);
        ValidateLights(manifest, motivatedLights, lightIntents, elements, elementMap, scene, issues);
        ValidateAudio(audioIntents, elementMap, scene, issues);
        ValidateAdaptiveSlots(adaptiveSlots, zoneMap, clusterMap, elementMap, issues);
        ValidateReviewClaims(manifest, reviewTour, reviewCamera, claimMap, elementMap, issues);
        return issues;
    }

    static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
            if (candidate.gameObject.scene == scene) result.Add(candidate);
        return result;
    }

    static Dictionary<string, T> UniqueMap<T>(IReadOnlyList<T> items, Func<T, string> id,
        string label, List<string> issues) where T : Component
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (T item in items)
        {
            string value = id(item);
            ValidateText(value, $"{label} id on {Path(item.transform)}", issues);
            if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value)) continue;
            if (!map.TryAdd(value, item))
                issues.Add($"duplicate {label} id '{value}' on {Path(item.transform)} and {Path(map[value].transform)}");
        }
        return map;
    }

    static void ValidateZones(IReadOnlyList<GmCompositionZone> zones,
        IReadOnlyList<GmCompositionCluster> clusters, IReadOnlyList<GmCompositionElement> elements,
        List<string> issues)
    {
        foreach (GmCompositionZone zone in zones)
        {
            ValidateText(zone.Purpose, $"zone '{zone.ZoneId}' purpose", issues, 12);
            int clusterCount = 0;
            int elementCount = 0;
            foreach (GmCompositionCluster cluster in clusters)
                if (cluster.ZoneId == zone.ZoneId) clusterCount++;
            foreach (GmCompositionElement element in elements)
                foreach (GmCompositionCluster cluster in clusters)
                    if (element.ClusterId == cluster.ClusterId && cluster.ZoneId == zone.ZoneId)
                    {
                        elementCount++;
                        break;
                    }
            if (clusterCount < zone.MinimumClusters)
                issues.Add($"zone '{zone.ZoneId}' has {clusterCount} cluster(s), needs {zone.MinimumClusters}");
            if (elementCount < zone.MinimumElements)
                issues.Add($"zone '{zone.ZoneId}' has {elementCount} element(s), needs {zone.MinimumElements}");
        }
    }

    static void ValidateClusters(IReadOnlyList<GmCompositionCluster> clusters,
        IReadOnlyList<GmCompositionElement> elements, IReadOnlyDictionary<string, GmCompositionZone> zones,
        IReadOnlyDictionary<string, GmCompositionElement> elementMap, List<string> issues)
    {
        foreach (GmCompositionCluster cluster in clusters)
        {
            ValidateText(cluster.Purpose, $"cluster '{cluster.ClusterId}' purpose", issues, 12);
            if (!zones.TryGetValue(cluster.ZoneId, out GmCompositionZone zone))
                issues.Add($"cluster '{cluster.ClusterId}' references missing zone '{cluster.ZoneId}'");
            else if (!zone.Contains(cluster.WorldCenter, 0.05f))
                issues.Add($"cluster '{cluster.ClusterId}' origin lies outside zone '{cluster.ZoneId}'");

            var members = new List<GmCompositionElement>();
            foreach (GmCompositionElement element in elements)
                if (element.ClusterId == cluster.ClusterId) members.Add(element);
            if (members.Count > cluster.MaximumMembers)
                issues.Add($"cluster '{cluster.ClusterId}' has {members.Count} members, maximum is {cluster.MaximumMembers}; split its story job");

            int supports = 0;
            int details = 0;
            var familyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (GmCompositionElement member in members)
            {
                if (member.Role == GmCompositionRole.Support || member.Role == GmCompositionRole.RouteCue)
                    supports++;
                if (member.Role == GmCompositionRole.Detail) details++;
                if (!string.IsNullOrWhiteSpace(member.AssetFamily))
                {
                    familyCounts.TryGetValue(member.AssetFamily, out int count);
                    familyCounts[member.AssetFamily] = count + 1;
                }
                Renderer[] memberRenderers = member.GetComponentsInChildren<Renderer>(true);
                bool hasVisibleRenderer = false;
                foreach (Renderer renderer in memberRenderers)
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) { hasVisibleRenderer = true; break; }
                Vector3 memberCenter = hasVisibleRenderer ? CombinedBounds(memberRenderers).center : member.transform.position;
                if (Vector3.Distance(memberCenter, cluster.WorldCenter) > cluster.MaximumRadius)
                    issues.Add($"element '{member.ElementId}' lies outside cluster '{cluster.ClusterId}' radius {cluster.MaximumRadius:F1}m");
            }
            if (supports < cluster.MinimumSupports)
                issues.Add($"cluster '{cluster.ClusterId}' has {supports} support/route-cue element(s), needs {cluster.MinimumSupports}");
            if (details < cluster.MinimumDetails)
                issues.Add($"cluster '{cluster.ClusterId}' has {details} detail element(s), needs {cluster.MinimumDetails}");

            if (!elementMap.TryGetValue(cluster.AnchorElementId, out GmCompositionElement anchor))
                issues.Add($"cluster '{cluster.ClusterId}' references missing anchor '{cluster.AnchorElementId}'");
            else
            {
                if (anchor.ClusterId != cluster.ClusterId)
                    issues.Add($"cluster '{cluster.ClusterId}' anchor '{anchor.ElementId}' belongs to '{anchor.ClusterId}'");
                if (anchor.Role != GmCompositionRole.Anchor && anchor.Role != GmCompositionRole.Gameplay)
                    issues.Add($"cluster '{cluster.ClusterId}' anchor '{anchor.ElementId}' has role {anchor.Role}, expected Anchor or Gameplay");
            }

            if (cluster.RequireAssetFamilyVariation && members.Count >= 4)
            {
                int dominant = 0;
                string dominantFamily = "<unset>";
                foreach (var pair in familyCounts)
                    if (pair.Value > dominant) { dominant = pair.Value; dominantFamily = pair.Key; }
                float share = members.Count == 0 ? 0f : dominant / (float)members.Count;
                if (familyCounts.Count < 2 || share > cluster.MaximumDominantFamilyShare + 0.0001f)
                    issues.Add($"cluster '{cluster.ClusterId}' is palette-monotonous: family '{dominantFamily}' owns {dominant}/{members.Count} members " +
                        $"(maximum share {cluster.MaximumDominantFamilyShare:P0})");
            }
        }
    }

    static void ValidateElements(IReadOnlyList<GmCompositionElement> elements,
        IReadOnlyDictionary<string, GmCompositionCluster> clusters,
        IReadOnlyDictionary<string, GmCompositionElement> elementMap,
        IReadOnlyDictionary<string, GmRouteReservation> routes, List<string> issues)
    {
        foreach (GmCompositionElement element in elements)
        {
            ValidateText(element.AssetFamily, $"element '{element.ElementId}' asset family", issues, 2);
            ValidateText(element.Rationale, $"element '{element.ElementId}' rationale", issues, 12);
            if (!clusters.ContainsKey(element.ClusterId))
                issues.Add($"element '{element.ElementId}' references missing cluster '{element.ClusterId}'");

            Renderer[] renderers = element.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                if (element.GetComponent<GmClearedPathGuide>() != null) continue;
                issues.Add($"composition element '{element.ElementId}' has no renderer below {Path(element.transform)}");
                continue;
            }
            Bounds bounds = CombinedBounds(renderers);
            if (element.Relation == GmSpatialRelation.Grounded &&
                Mathf.Abs(bounds.min.y - element.DeclaredSurfaceY) > element.GroundTolerance)
                issues.Add($"grounded element '{element.ElementId}' renderer base y={bounds.min.y:F2}, declared surface " +
                    $"y={element.DeclaredSurfaceY:F2} ± {element.GroundTolerance:F2}");

            if (element.Relation == GmSpatialRelation.AgainstBoundary ||
                element.Relation == GmSpatialRelation.FlanksAnchor ||
                element.Relation == GmSpatialRelation.LeadsToAnchor)
            {
                if (!elementMap.TryGetValue(element.RelationTargetId, out GmCompositionElement target))
                    issues.Add($"element '{element.ElementId}' relation {element.Relation} references missing element '{element.RelationTargetId}'");
                else if (Vector3.Distance(bounds.center, CombinedBounds(target.GetComponentsInChildren<Renderer>(true)).center) >
                    element.MaximumRelationDistance)
                    issues.Add($"element '{element.ElementId}' is too far from relation target '{target.ElementId}' " +
                        $"(maximum {element.MaximumRelationDistance:F1}m)");
            }
            if (element.Relation == GmSpatialRelation.FramesRoute && !routes.ContainsKey(element.RelationTargetId))
                issues.Add($"element '{element.ElementId}' frames missing route '{element.RelationTargetId}'");
        }
    }

    static void ValidateTransformCollisions(IReadOnlyList<GmCompositionElement> elements, List<string> issues)
    {
        var transforms = new Dictionary<string, GmCompositionElement>(StringComparer.Ordinal);
        foreach (GmCompositionElement element in elements)
        {
            Transform t = element.transform;
            Vector3 p = t.position;
            Vector3 r = t.eulerAngles;
            Vector3 s = t.lossyScale;
            // A candle and a table may deliberately share a parent-space origin. Treat an exact
            // transform as a duplicate only inside the same asset family, which is the accidental
            // copy/paste and scatter failure this guard is meant to catch.
            string key = $"{element.AssetFamily}|{Round(p.x)},{Round(p.y)},{Round(p.z)}|" +
                $"{Round(r.x, 0.1f)},{Round(r.y, 0.1f)},{Round(r.z, 0.1f)}|" +
                $"{Round(s.x)},{Round(s.y)},{Round(s.z)}";
            if (transforms.TryGetValue(key, out GmCompositionElement previous))
                issues.Add($"elements '{previous.ElementId}' and '{element.ElementId}' have the same world transform; overlapping duplicate placement suspected");
            else transforms[key] = element;
        }
    }

    static float Round(float value, float step = PositionEpsilon) => Mathf.Round(value / step) * step;

    static void ValidateNegativeSpace(IReadOnlyList<GmNegativeSpace> spaces,
        IReadOnlyList<GmCompositionElement> elements, List<string> issues)
    {
        foreach (GmNegativeSpace space in spaces)
        {
            ValidateText(space.Purpose, $"negative space '{space.SpaceId}' purpose", issues, 12);
            foreach (GmCompositionElement element in elements)
            {
                if (element.ElementId == space.AllowedElementId || element.Role == GmCompositionRole.Ground ||
                    element.Role == GmCompositionRole.Background || !element.BlocksRoutes) continue;
                Renderer[] renderers = element.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0 && IntersectsOrientedBox(CombinedBounds(renderers), space.transform,
                    space.Size, space.LocalCenterOffset))
                    issues.Add($"negative space '{space.SpaceId}' is occupied by element '{element.ElementId}'");
            }
        }
    }

    static void ValidateRoutes(IReadOnlyList<GmRouteReservation> routes,
        IReadOnlyList<GmCompositionElement> elements, List<string> issues)
    {
        foreach (GmRouteReservation route in routes)
        {
            ValidateText(route.Purpose, $"route '{route.RouteId}' purpose", issues, 12);
            if (route.LocalPoints == null || route.LocalPoints.Count < 2)
            {
                issues.Add($"route '{route.RouteId}' needs at least two points");
                continue;
            }
            for (int segment = 1; segment < route.LocalPoints.Count; segment++)
            {
                Vector3 from = route.WorldPoint(segment - 1);
                Vector3 to = route.WorldPoint(segment);
                float distance = Vector3.Distance(from, to);
                int steps = Mathf.Max(2, Mathf.CeilToInt(distance / 0.4f));
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 p = Vector3.Lerp(from, to, step / (float)steps);
                    bool blocked = false;
                    foreach (GmCompositionElement element in elements)
                    {
                        if (!element.BlocksRoutes || element.Role == GmCompositionRole.Ground ||
                            element.Role == GmCompositionRole.Background) continue;
                        Renderer[] renderers = element.GetComponentsInChildren<Renderer>(true);
                        if (renderers.Length == 0) continue;
                        Bounds b = CombinedBounds(renderers);
                        if (b.max.y < p.y + 0.2f) continue;
                        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
                        float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
                        if (new Vector2(dx, dz).magnitude < route.HalfWidth)
                        {
                            issues.Add($"route '{route.RouteId}' is obstructed near ({p.x:F1},{p.z:F1}) by element '{element.ElementId}'");
                            blocked = true;
                            break;
                        }
                    }
                    if (blocked) break;
                }
            }
        }
    }

    static void ValidateLights(GmSceneComposition manifest,
        IReadOnlyList<GmMotivatedLight> markers, IReadOnlyList<GmLightIntent> intents,
        IReadOnlyList<GmCompositionElement> elements,
        IReadOnlyDictionary<string, GmCompositionElement> elementMap, Scene scene, List<string> issues)
    {
        var covered = new HashSet<Light>();
        foreach (GmMotivatedLight marker in markers)
        {
            ValidateText(marker.Purpose, $"motivated light '{marker.LightId}' purpose", issues, 12);
            Light light = marker.GetComponent<Light>() ?? marker.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                issues.Add($"motivated light '{marker.LightId}' has no Light component");
                continue;
            }
            covered.Add(light);
            if (!elementMap.TryGetValue(marker.SourceElementId, out GmCompositionElement source))
            {
                issues.Add($"motivated light '{marker.LightId}' references missing source element '{marker.SourceElementId}'");
                continue;
            }
            Renderer[] sourceRenderers = source.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers.Length == 0) continue;
            float distance = CombinedBounds(sourceRenderers).SqrDistance(light.transform.position);
            if (distance > marker.MaximumSourceDistance * marker.MaximumSourceDistance)
                issues.Add($"motivated light '{marker.LightId}' is farther than {marker.MaximumSourceDistance:F1}m from visible source '{source.ElementId}'");
        }

        foreach (GmLightIntent intent in intents)
        {
            ValidateText(intent.Rationale, $"light intent '{intent.IntentId}' rationale", issues, 12);
            Light light = intent.GetComponent<Light>() ?? intent.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                issues.Add($"light intent '{intent.IntentId}' has no Light component");
                continue;
            }
            covered.Add(light);
            if (intent.Kind == GmLightIntentKind.Practical)
            {
                if (!elementMap.TryGetValue(intent.SourceElementId, out GmCompositionElement source))
                    issues.Add($"practical light intent '{intent.IntentId}' references missing visible source '{intent.SourceElementId}'");
                else if (CombinedBounds(source.GetComponentsInChildren<Renderer>(true)).SqrDistance(light.transform.position) >
                    intent.MaximumSourceDistance * intent.MaximumSourceDistance)
                    issues.Add($"practical light intent '{intent.IntentId}' is farther than {intent.MaximumSourceDistance:F1}m from source '{source.ElementId}'");
            }
            else if (intent.Kind == GmLightIntentKind.CompositionFill)
            {
                if (!elementMap.TryGetValue(intent.SubjectElementId, out GmCompositionElement subject))
                    issues.Add($"composition fill '{intent.IntentId}' references missing subject '{intent.SubjectElementId}'");
                else if (Vector3.Distance(light.transform.position,
                    CombinedBounds(subject.GetComponentsInChildren<Renderer>(true)).center) > intent.MaximumSubjectDistance)
                    issues.Add($"composition fill '{intent.IntentId}' exceeds its {intent.MaximumSubjectDistance:F1}m subject bound");
            }
        }

        if (!manifest.RequireMotivatedLocalLights) return;
        foreach (Light light in FindInScene<Light>(scene))
        {
            if (!light.enabled || !light.gameObject.activeInHierarchy || light.type == LightType.Directional) continue;
            if (!covered.Contains(light))
                issues.Add($"enabled local light '{Path(light.transform)}' has no GmMotivatedLight or authored GmLightIntent");
        }
    }

    static void ValidateAudio(IReadOnlyList<GmAudioIntent> intents,
        IReadOnlyDictionary<string, GmCompositionElement> elementMap, Scene scene, List<string> issues)
    {
        if (intents.Count == 0) return;
        var covered = new HashSet<AudioSource>();
        foreach (GmAudioIntent intent in intents)
        {
            ValidateText(intent.Rationale, $"audio intent '{intent.IntentId}' rationale", issues, 12);
            AudioSource source = intent.GetComponent<AudioSource>() ?? intent.GetComponentInChildren<AudioSource>(true);
            if (source == null)
            {
                issues.Add($"audio intent '{intent.IntentId}' has no AudioSource component");
                continue;
            }
            covered.Add(source);
            if ((intent.Kind == GmAudioIntentKind.Diegetic || intent.Kind == GmAudioIntentKind.Foley) &&
                !string.IsNullOrWhiteSpace(intent.SourceElementId) && !elementMap.ContainsKey(intent.SourceElementId))
                issues.Add($"audio intent '{intent.IntentId}' references missing source element '{intent.SourceElementId}'");
            if (intent.LoopPolicy == GmAudioLoopPolicy.Never && source.loop)
                issues.Add($"audio intent '{intent.IntentId}' forbids looping but its AudioSource loops");
            if (intent.LoopPolicy == GmAudioLoopPolicy.Required && !source.loop)
                issues.Add($"audio intent '{intent.IntentId}' requires looping but its AudioSource does not loop");
        }
        foreach (AudioSource source in FindInScene<AudioSource>(scene))
            if (source.enabled && source.gameObject.activeInHierarchy && !covered.Contains(source))
                issues.Add($"enabled audio source '{Path(source.transform)}' has no authored GmAudioIntent");
    }

    static void ValidateAdaptiveSlots(IReadOnlyList<GmAdaptiveSlot> slots,
        IReadOnlyDictionary<string, GmCompositionZone> zones,
        IReadOnlyDictionary<string, GmCompositionCluster> clusters,
        IReadOnlyDictionary<string, GmCompositionElement> elements, List<string> issues)
    {
        foreach (GmAdaptiveSlot slot in slots)
        {
            ValidateText(slot.Rationale, $"adaptive slot '{slot.SlotId}' rationale", issues, 12);
            if (slot.Role != GmCompositionRole.Detail && slot.Role != GmCompositionRole.Support &&
                slot.Role != GmCompositionRole.RouteCue)
                issues.Add($"adaptive slot '{slot.SlotId}' uses forbidden role {slot.Role}; only Detail, Support, and RouteCue may vary");
            if (!zones.ContainsKey(slot.ZoneId))
                issues.Add($"adaptive slot '{slot.SlotId}' references missing zone '{slot.ZoneId}'");
            if (!clusters.ContainsKey(slot.ClusterId))
                issues.Add($"adaptive slot '{slot.SlotId}' references missing cluster '{slot.ClusterId}'");
            if (slot.AllowedAssetFamilies.Count == 0)
                issues.Add($"adaptive slot '{slot.SlotId}' has no allowed asset families");
            if (slot.AllowedSwapElementIds.Count == 0)
                issues.Add($"adaptive slot '{slot.SlotId}' has no explicit swap elements");
            if (slot.Candidates.Count == 0 || slot.Candidates.Count > 3)
                issues.Add($"adaptive slot '{slot.SlotId}' must define one to three deterministic candidates");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string elementId in slot.AllowedSwapElementIds)
            {
                if (!elements.TryGetValue(elementId, out GmCompositionElement element))
                    issues.Add($"adaptive slot '{slot.SlotId}' references missing swap element '{elementId}'");
                else if (element.Role != GmCompositionRole.Detail && element.Role != GmCompositionRole.Support &&
                    element.Role != GmCompositionRole.RouteCue)
                    issues.Add($"adaptive slot '{slot.SlotId}' cannot swap protected {element.Role} element '{elementId}'");
            }
            foreach (GmAdaptiveCandidate candidate in slot.Candidates)
            {
                ValidateText(candidate.CandidateId, $"adaptive slot '{slot.SlotId}' candidate id", issues);
                ValidateText(candidate.AssetGuid, $"adaptive slot '{slot.SlotId}' candidate '{candidate.CandidateId}' GUID", issues);
                if (!ids.Add(candidate.CandidateId))
                    issues.Add($"adaptive slot '{slot.SlotId}' repeats candidate id '{candidate.CandidateId}'");
                bool familyAllowed = false;
                foreach (string family in slot.AllowedAssetFamilies)
                    if (family == candidate.AssetFamily) { familyAllowed = true; break; }
                if (!familyAllowed)
                    issues.Add($"adaptive slot '{slot.SlotId}' candidate '{candidate.CandidateId}' uses forbidden family '{candidate.AssetFamily}'");
                Vector3 offset = candidate.LocalOffset;
                Vector3 max = slot.MaximumLocalOffset;
                if (Mathf.Abs(offset.x) > max.x || Mathf.Abs(offset.y) > max.y || Mathf.Abs(offset.z) > max.z)
                    issues.Add($"adaptive slot '{slot.SlotId}' candidate '{candidate.CandidateId}' exceeds local offset bounds");
                if (Mathf.Abs(candidate.YawOffset) > slot.MaximumYawOffset)
                    issues.Add($"adaptive slot '{slot.SlotId}' candidate '{candidate.CandidateId}' exceeds yaw bound");
                Vector3 scale = candidate.ScaleMultiplier;
                if (scale.x < slot.ScaleRange.x || scale.y < slot.ScaleRange.x || scale.z < slot.ScaleRange.x ||
                    scale.x > slot.ScaleRange.y || scale.y > slot.ScaleRange.y || scale.z > slot.ScaleRange.y)
                    issues.Add($"adaptive slot '{slot.SlotId}' candidate '{candidate.CandidateId}' exceeds scale bounds");
            }
        }
    }

    static void ValidateReviewClaims(GmSceneComposition manifest, GmSceneReviewTour tour, Camera camera,
        IReadOnlyDictionary<string, GmReviewCompositionClaim> claims,
        IReadOnlyDictionary<string, GmCompositionElement> elements, List<string> issues)
    {
        if (!manifest.RequireReviewClaimForEveryShot && claims.Count == 0) return;
        if (tour == null)
        {
            issues.Add("composition requires review claims but no GmSceneReviewTour was supplied");
            return;
        }
        if (camera == null) camera = Camera.main;
        if (camera == null)
        {
            issues.Add("composition requires review claims but no review camera exists");
            return;
        }

        var shotNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (GmReviewShot shot in tour.ShotsForAudit)
        {
            shotNames.Add(shot.Name);
            if (!claims.TryGetValue(shot.Name, out GmReviewCompositionClaim claim))
            {
                if (manifest.RequireReviewClaimForEveryShot)
                    issues.Add($"review shot '{shot.Name}' has no authored composition claim");
                continue;
            }
            ValidateText(claim.Claim, $"review shot '{shot.Name}' claim", issues, 12);
            if (!elements.TryGetValue(claim.PrimaryElementId, out GmCompositionElement primary))
            {
                issues.Add($"review shot '{shot.Name}' references missing primary element '{claim.PrimaryElementId}'");
                continue;
            }
            ValidateShot(camera, tour, shot, claim, primary, elements, issues);
        }
        foreach (var pair in claims)
            if (!shotNames.Contains(pair.Key)) issues.Add($"review claim '{pair.Key}' has no matching tour shot");
    }

    static void ValidateShot(Camera camera, GmSceneReviewTour tour, GmReviewShot shot,
        GmReviewCompositionClaim claim, GmCompositionElement primary,
        IReadOnlyDictionary<string, GmCompositionElement> elements, List<string> issues)
    {
        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        float oldAspect = camera.aspect;
        try
        {
            camera.transform.position = shot.Position;
            camera.transform.rotation = Quaternion.Euler(shot.Pitch, shot.Yaw + tour.ReviewYawOffsetForAudit, 0f);
            camera.aspect = tour.ShotHeight == 0 ? 16f / 9f : tour.ShotWidth / (float)tour.ShotHeight;
            Bounds primaryBounds = CombinedBounds(primary.GetComponentsInChildren<Renderer>(true));
            if (!Visible(camera, primaryBounds, out Vector2 center, out float viewportHeight))
            {
                issues.Add($"review shot '{shot.Name}' does not see primary element '{primary.ElementId}'");
                return;
            }
            Vector2 delta = center - claim.TargetViewport;
            if (Mathf.Abs(delta.x) > claim.ViewportTolerance.x || Mathf.Abs(delta.y) > claim.ViewportTolerance.y)
                issues.Add($"review shot '{shot.Name}' frames primary '{primary.ElementId}' at ({center.x:F2},{center.y:F2}), " +
                    $"outside target ({claim.TargetViewport.x:F2},{claim.TargetViewport.y:F2}) ± ({claim.ViewportTolerance.x:F2},{claim.ViewportTolerance.y:F2})");
            if (viewportHeight < claim.MinimumViewportHeight || viewportHeight > claim.MaximumViewportHeight)
                issues.Add($"review shot '{shot.Name}' primary '{primary.ElementId}' viewport height {viewportHeight:F2} " +
                    $"is outside {claim.MinimumViewportHeight:F2}..{claim.MaximumViewportHeight:F2}");

            float primaryDepth = Vector3.Distance(camera.transform.position, primaryBounds.center);
            ValidateClaimElements(camera, shot.Name, "foreground", claim.ForegroundElementIds,
                elements, issues, depth => depth < primaryDepth - 0.15f);
            ValidateClaimElements(camera, shot.Name, "support", claim.SupportingElementIds,
                elements, issues, null);
            ValidateClaimElements(camera, shot.Name, "background", claim.BackgroundElementIds,
                elements, issues, depth => depth > primaryDepth + 0.15f);
        }
        finally
        {
            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            camera.aspect = oldAspect;
        }
    }

    static void ValidateClaimElements(Camera camera, string shotName, string layer,
        IReadOnlyList<string> ids, IReadOnlyDictionary<string, GmCompositionElement> elements,
        List<string> issues, Func<float, bool> depthRule)
    {
        foreach (string id in ids)
        {
            if (!elements.TryGetValue(id, out GmCompositionElement element))
            {
                issues.Add($"review shot '{shotName}' {layer} references missing element '{id}'");
                continue;
            }
            Renderer[] renderers = element.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || !Visible(camera, CombinedBounds(renderers), out _, out _))
            {
                issues.Add($"review shot '{shotName}' does not see declared {layer} element '{id}'");
                continue;
            }
            float depth = Vector3.Distance(camera.transform.position, CombinedBounds(renderers).center);
            if (depthRule != null && !depthRule(depth))
                issues.Add($"review shot '{shotName}' declared {layer} element '{id}' at the wrong depth ({depth:F1}m)");
        }
    }

    static bool Visible(Camera camera, Bounds bounds, out Vector2 viewportCenter, out float viewportHeight)
    {
        viewportCenter = Vector2.zero;
        viewportHeight = 0f;
        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds)) return false;
        Vector3 center = camera.WorldToViewportPoint(bounds.center);
        if (center.z <= 0f) return false;
        viewportCenter = new Vector2(center.x, center.y);
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        foreach (Vector3 corner in BoundsCorners(bounds))
        {
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            if (viewport.z <= 0f) continue;
            minY = Mathf.Min(minY, viewport.y);
            maxY = Mathf.Max(maxY, viewport.y);
        }
        viewportHeight = float.IsInfinity(minY) ? 0f : Mathf.Clamp01(maxY - minY);
        return center.x >= -0.1f && center.x <= 1.1f && center.y >= -0.1f && center.y <= 1.1f;
    }

    static Bounds CombinedBounds(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return new Bounds();
        Renderer first = null;
        foreach (Renderer renderer in renderers)
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            { first = renderer; break; }
        if (first == null) return new Bounds();
        Bounds bounds = first.bounds;
        foreach (Renderer renderer in renderers)
            if (renderer != null && renderer != first && renderer.enabled && renderer.gameObject.activeInHierarchy)
                bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static bool IntersectsOrientedBox(Bounds bounds, Transform boxTransform, Vector3 boxSize,
        Vector3 localCenterOffset)
    {
        Vector3 half = boxSize * 0.5f;
        foreach (Vector3 corner in BoundsCorners(bounds))
        {
            Vector3 local = boxTransform.InverseTransformPoint(corner) - localCenterOffset;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z)
                return true;
        }
        foreach (Vector3 local in LocalBoxCorners(half))
            if (bounds.Contains(boxTransform.TransformPoint(local + localCenterOffset))) return true;
        return bounds.Contains(boxTransform.TransformPoint(localCenterOffset));
    }

    static IEnumerable<Vector3> BoundsCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;
        yield return new Vector3(min.x, min.y, min.z);
        yield return new Vector3(min.x, min.y, max.z);
        yield return new Vector3(min.x, max.y, min.z);
        yield return new Vector3(min.x, max.y, max.z);
        yield return new Vector3(max.x, min.y, min.z);
        yield return new Vector3(max.x, min.y, max.z);
        yield return new Vector3(max.x, max.y, min.z);
        yield return new Vector3(max.x, max.y, max.z);
    }

    static IEnumerable<Vector3> LocalBoxCorners(Vector3 h)
    {
        yield return new Vector3(-h.x, -h.y, -h.z);
        yield return new Vector3(-h.x, -h.y, h.z);
        yield return new Vector3(-h.x, h.y, -h.z);
        yield return new Vector3(-h.x, h.y, h.z);
        yield return new Vector3(h.x, -h.y, -h.z);
        yield return new Vector3(h.x, -h.y, h.z);
        yield return new Vector3(h.x, h.y, -h.z);
        yield return new Vector3(h.x, h.y, h.z);
    }

    static void ValidateText(string value, string label, List<string> issues, int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() != value || value.Length < minimumLength)
            issues.Add($"{label} must be a deliberate trimmed value of at least {minimumLength} characters");
        else if (IsPlaceholder(value)) issues.Add($"{label} still contains placeholder text '{value}'");
    }

    static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string lower = value.ToLowerInvariant();
        return lower.Contains("replace-me") || lower.Contains("todo") || lower.Contains("tbd") ||
            lower.Contains("placeholder");
    }

    static string Path(Transform transform)
    {
        string result = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            result = transform.name + "/" + result;
        }
        return result;
    }
}
