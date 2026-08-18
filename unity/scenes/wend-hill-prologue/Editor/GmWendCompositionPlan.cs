using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Authored composition contract for the complete Wend Hill walk. Markers attach to the rendered
/// objects the opening already built; this plan creates no stand-in geometry and cannot make a
/// missing landmark pass by describing an empty proxy with the same name.
/// </summary>
public static class GmWendCompositionPlan
{
    public const string SceneId = "wend-hill-prologue";

    public static void Author(GameObject owner, GmRouteSpline route)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (route == null) throw new ArgumentNullException(nameof(route));

        GmCompositionAuthoring.Begin(owner, SceneId,
            "A continuous moonlit estate approach whose car, closing gate, chapel, neglected grounds, " +
            "coach house and impossible manor escalate as readable landmarks along one physical route.",
            minZones: 5, minClusters: 5, minElements: 19,
            requireEveryShot: true, requireMotivatedLights: true);

        GameObject car = Require("GmWendOpening/ArrivalCar");
        GameObject gate = Require("GmWendOpening/EstateGate");
        GameObject gatePier = Descendant(gate, "PierLeft");
        GameObject gateLeaf = Descendant(gate, "LeafLeft");
        GameObject gateWing = Descendant(gate, "PerimeterWingLeft");
        GameObject gateWingRight = Descendant(gate, "PerimeterWingRight");
        GameObject chapel = ChurchVisual(GmWorldAnchor.Find("chapel"));
        GameObject weatheredMarker = PoiVisual("weathered-marker");
        GameObject fallenMarker = PoiVisual("fallen-marker");
        GameObject childMarker = PoiVisual("child-marker");
        GameObject scarecrow = PoiVisual("garden-scarecrow");
        GameObject basin = PoiVisual("garden-basin");
        GameObject well = PoiVisual("garden-well");
        GameObject shed = PoiVisual("garden-shed");
        GameObject coach = Require("GmWendOpening/Outbuildings/CoachHouse");
        GameObject coachExterior = Descendant(coach, "ExteriorArt");
        GameObject coachDoor = Descendant(coachExterior, "CoachDoorLeft");
        GameObject coachCart = Descendant(coach, "WoodenCart");
        GameObject coachTrough = Descendant(coach, "FeedingTrough");
        GameObject coachHay = Descendant(coach, "HayStack");
        GmMansionIdentity mansionIdentity = UnityEngine.Object.FindAnyObjectByType<GmMansionIdentity>(
            FindObjectsInactive.Include);
        if (mansionIdentity == null)
            throw new InvalidOperationException("[GmWendComposition] rendered manor identity is missing");
        GameObject mansion = mansionIdentity.gameObject;
        GameObject mansionFacade = LargestRendererObject(mansion);

        Vector3 arrivalClusterCenter = route.PointAt(12f);
        Bounds gateBoundaryBounds = SourceBounds(gateWing);
        gateBoundaryBounds.Encapsulate(SourceBounds(gateWingRight));
        float arrivalRadius = Vector3.Distance(arrivalClusterCenter, gateBoundaryBounds.center) +
                              gateBoundaryBounds.extents.magnitude + 5f;
        GameObject arrivalZone = Zone(owner, route, "ArrivalGateZone", "arrival-gate-zone", 18f,
            "The abandoned car and estate threshold establish that retreat is possible before the gate closes.",
            new Vector3(arrivalRadius * 2f, 18f, 90f), 4);
        Cluster(arrivalZone, "arrival-gate-cluster", "arrival-gate-zone",
            "The displaced car is answered by masonry, hinged iron and a boundary that continues into the trees.",
            "arrival-car", arrivalClusterCenter, arrivalRadius);
        Element(car, "arrival-car", "arrival-gate-cluster", "period-vehicle",
            "The abandoned period car is the player's last readable means of retreat.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: GmWorldAnchor.Find("arrival-car").transform.position.y,
            groundTolerance: 0.35f, blocksRoutes: false);
        Element(gatePier, "gate-pier", "arrival-gate-cluster", "estate-masonry",
            "A stone pier gives the moving iron leaves enough visual and physical weight.", GmCompositionRole.Support,
            GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(gateLeaf, "gate-leaf", "arrival-gate-cluster", "wrought-iron",
            "The independently hinged leaf makes Threshold Refusal a visible action rather than a state flag.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(gateWing, "gate-boundary", "arrival-gate-cluster", "estate-boundary",
            "The continuous fence wing prevents the gate from reading as an isolated stage prop.",
            GmCompositionRole.Boundary, GmSpatialRelation.FramesRoute, "estate-walk", blocksRoutes: false);

        GameObject chapelZone = Zone(owner, route, "ChapelCemeteryZone", "chapel-cemetery-zone", 190f,
            "Graves accumulate into a chapel landmark before the road bends toward the working grounds.",
            new Vector3(210f, 30f, 170f), 4);
        Cluster(chapelZone, "chapel-cemetery-cluster", "chapel-cemetery-zone",
            "The chapel silhouette anchors a loose sequence of named and unnamed graves beside the avenue.",
            "chapel-building", route.PointAt(GmWendOpening.ChapelMetres), 125f);
        Element(chapel, "chapel-building", "chapel-cemetery-cluster", "chapel-architecture",
            "The church mass is the bell's visible source and the cemetery's architectural anchor.",
            GmCompositionRole.Anchor, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(weatheredMarker, "weathered-marker", "chapel-cemetery-cluster", "grave-markers",
            "A weathered cross begins the cemetery rhythm before the chapel resolves through the trees.",
            GmCompositionRole.Support, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(fallenMarker, "fallen-marker", "chapel-cemetery-cluster", "grave-debris",
            "The fallen marker breaks the repeated upright silhouette and implies long neglect.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(childMarker, "child-marker", "chapel-cemetery-cluster", "grave-markers",
            "The smaller grave marker changes the scale and turns anonymous dressing into human loss.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);

        GameObject groundsZone = Zone(owner, route, "WorkingGroundsZone", "working-grounds-zone", 280f,
            "The garden and yard show that somebody maintained the estate long after it should have emptied.",
            new Vector3(120f, 24f, 120f), 4);
        Cluster(groundsZone, "working-grounds-cluster", "working-grounds-zone",
            "Scarecrow, basin, shed and well form a legible work-yard sequence instead of uniform scatter.",
            "garden-scarecrow", route.PointAt(280f), 55f);
        Element(scarecrow, "garden-scarecrow", "working-grounds-cluster", "garden-figures",
            "The lone scarecrow supplies the grounds with a human-shaped anchor at approach distance.",
            GmCompositionRole.Anchor, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(basin, "garden-basin", "working-grounds-cluster", "work-yard-vessels",
            "The basin pulls the player's eye down from the scarecrow into evidence they can inspect.",
            GmCompositionRole.Support, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(well, "garden-well", "working-grounds-cluster", "garden-architecture",
            "The well closes the garden cluster with a durable structure rather than loose clutter.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(shed, "garden-shed", "working-grounds-cluster", "outbuilding-doors",
            "The shed door suggests a worked service yard beyond the ornamental house approach.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);

        GameObject coachZone = FreeZone(owner, "CoachHouseZone", "coach-house-zone", coach.transform.position,
            coach.transform.rotation, "The coaching-inn remnant turns roadside dressing into the first optional interior.",
            new Vector3(34f, 14f, 34f), 5);
        Cluster(coachZone, "coach-house-cluster", "coach-house-zone",
            "Timber facade, open door, cart, feed and hay describe the same abandoned working stable.",
            "coach-house", coach.transform.position, 18f);
        Element(coachExterior, "coach-house", "coach-house-cluster", "timber-outbuilding",
            "The complete timber shell and tiled roof establish the coaching-inn service building from the road.",
            GmCompositionRole.Anchor, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(coachDoor, "coach-door", "coach-house-cluster", "outbuilding-doors",
            "A real swung barn leaf reveals an enterable opening instead of a painted black rectangle.",
            GmCompositionRole.Support, GmSpatialRelation.AgainstBoundary, "coach-house", 8f, blocksRoutes: false);
        Element(coachCart, "coach-cart", "coach-house-cluster", "stable-equipment",
            "The wooden cart gives the interior a large working silhouette and a reason for the wide doors.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(coachTrough, "coach-trough", "coach-house-cluster", "stable-feeding",
            "The feeding trough explains the stall divisions as animal space rather than generic partitions.",
            GmCompositionRole.Gameplay, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(coachHay, "coach-hay", "coach-house-cluster", "stable-feed",
            "A measured hay stack completes the feed corner without intruding into the player route.",
            GmCompositionRole.Detail, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);

        GameObject manorZone = Zone(owner, route, "ManorThresholdZone", "manor-threshold-zone", 425f,
            "The avenue releases into a symmetrical facade whose lit porch refuses the player entry.",
            new Vector3(74f, 30f, 74f), 2);
        Cluster(manorZone, "manor-threshold-cluster", "manor-threshold-zone",
            "The full house mass and its detailed facade hold the final approach after the tree corridor opens.",
            "manor", route.PointAt(430f), 45f, minDetails: 0, requireVariation: false);
        Element(mansion, "manor", "manor-threshold-cluster", "manor-architecture",
            "The singular Victorian manor is the prologue's final visual answer and physical refusal boundary.",
            GmCompositionRole.Anchor, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
        Element(mansionFacade, "manor-facade", "manor-threshold-cluster", "manor-facade-detail",
            "The largest facade section carries the readable porch, window and roof rhythm at approach distance.",
            GmCompositionRole.Support, GmSpatialRelation.BackgroundLayer, blocksRoutes: false);

        var routePoints = new List<Vector3>();
        for (float metres = 0f; metres < route.Length; metres += 10f)
            routePoints.Add(route.PointAt(metres));
        routePoints.Add(route.PointAt(route.Length));
        GmCompositionAuthoring.Route(owner, "estate-walk",
            "The complete controller-width path from the abandoned car to the manor porch.",
            routePoints.ToArray(), 0.8f);

        AuthorLighting(owner, route);

        Claim(owner, "01-arrival", "arrival-car", new Vector2(0.50f, 0.45f), new Vector2(0.28f, 0.30f),
            0.05f, 0.70f, "The abandoned car is readable as the player's last route back.");
        Claim(owner, "02-gate", "gate-pier", new Vector2(0.50f, 0.48f), new Vector2(0.30f, 0.34f),
            0.08f, 0.90f, "The gate reads as hinged iron held by masonry and continued by solid fence wings.");
        Claim(owner, "03-lookback", "arrival-car", new Vector2(0.50f, 0.48f), new Vector2(0.34f, 0.34f),
            0.03f, 0.55f, "Looking back keeps the car visible but increasingly remote behind the gate.");
        Claim(owner, "04-route", "chapel-building", new Vector2(0.50f, 0.46f), new Vector2(0.38f, 0.36f),
            0.03f, 0.80f, "The road composition leads toward the chapel rather than dissolving into forest scatter.");
        Claim(owner, "05-chapel", "chapel-building", new Vector2(0.48f, 0.46f), new Vector2(0.34f, 0.36f),
            0.08f, 0.90f, "The chapel itself, not merely its surrounding trees, dominates the cemetery turn.");
        Claim(owner, "06-grounds", "garden-basin", new Vector2(0.50f, 0.48f), new Vector2(0.34f, 0.36f),
            0.03f, 0.70f, "The inspectable basin reads within a coherent neglected work-yard cluster.");
        Claim(owner, "07-manor", "manor", new Vector2(0.50f, 0.50f), new Vector2(0.28f, 0.34f),
            0.14f, 1.20f, "The distant manor resolves as the route's singular destination through the final trees.");
        Claim(owner, "08-porch", "manor", new Vector2(0.50f, 0.52f), new Vector2(0.28f, 0.36f),
            0.40f, 1.60f, "The clear facade and sealed front door dominate the final threshold composition.");
        Claim(owner, "09-coach-approach", "coach-house", new Vector2(0.50f, 0.50f), new Vector2(0.36f, 0.38f),
            0.12f, 1.20f, "The road view reads a complete tiled timber coach house with an open stable entrance.");
        Claim(owner, "10-coach-interior", "coach-cart", new Vector2(0.50f, 0.48f), new Vector2(0.40f, 0.40f),
            0.05f, 0.90f, "The interior view resolves real working props and stalls rather than a primitive shell.");
    }

    static GameObject Zone(GameObject owner, GmRouteSpline route, string name, string id, float metres,
        string purpose, Vector3 size, int minimumElements)
    {
        Vector3 position = route.PointAt(Mathf.Clamp(metres, 0f, route.Length));
        Quaternion rotation = Quaternion.LookRotation(route.TangentAt(Mathf.Clamp(metres, 0f, route.Length)), Vector3.up);
        return FreeZone(owner, name, id, position, rotation, purpose, size, minimumElements);
    }

    static GameObject FreeZone(GameObject owner, string name, string id, Vector3 position,
        Quaternion rotation, string purpose, Vector3 size, int minimumElements)
    {
        var zone = new GameObject(name);
        zone.transform.SetParent(owner.transform, true);
        zone.transform.SetPositionAndRotation(position, rotation);
        GmCompositionAuthoring.Zone(zone, id, purpose, size, minClusters: 1, minElements: minimumElements);
        return zone;
    }

    static void Cluster(GameObject zone, string id, string zoneId, string purpose, string anchorId,
        Vector3 position, float radius, int minDetails = 1, bool requireVariation = true)
    {
        var cluster = new GameObject(id);
        cluster.transform.SetParent(zone.transform, true);
        cluster.transform.position = position;
        GmCompositionAuthoring.Cluster(cluster, id, zoneId, purpose, anchorId,
            minSupports: 1, minDetails: minDetails, maxMembers: 12, maxRadius: radius,
            requireVariation: requireVariation, maxFamilyShare: 0.75f);
    }

    static void Element(GameObject go, string id, string cluster, string family, string rationale,
        GmCompositionRole role, GmSpatialRelation relation, string target = "",
        float maxDistance = 5f, float surfaceY = 0f, float groundTolerance = 0.2f,
        bool blocksRoutes = true) =>
        GmCompositionAuthoring.Element(go, id, cluster, family, rationale, role, relation, target,
            maxDistance, surfaceY, groundTolerance, blocksRoutes);

    static void Claim(GameObject owner, string shot, string primary, Vector2 target, Vector2 tolerance,
        float minHeight, float maxHeight, string claim) =>
        GmCompositionAuthoring.Claim(owner, shot, claim, primary, Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), target, tolerance, minHeight, maxHeight);

    static GameObject Require(string path)
    {
        GameObject go = GameObject.Find(path);
        if (go == null) throw new InvalidOperationException($"[GmWendComposition] missing rendered object '{path}'");
        RequireRenderer(go, path);
        return go;
    }

    static GameObject Descendant(GameObject root, string name)
    {
        Transform transform = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == name ||
                candidate.name == GmOwnedPropFactory.VisualPrefix + name);
        if (transform == null)
            throw new InvalidOperationException($"[GmWendComposition] '{root.name}' has no '{name}' descendant");
        RequireRenderer(transform.gameObject, name);
        return transform.gameObject;
    }

    static GameObject PoiVisual(string anchorId)
    {
        GmWorldAnchor anchor = GmWorldAnchor.Find(anchorId);
        if (anchor == null) throw new InvalidOperationException($"[GmWendComposition] missing POI '{anchorId}'");
        Renderer renderer = anchor.GetComponentsInChildren<Renderer>(true).FirstOrDefault();
        if (renderer == null)
            throw new InvalidOperationException($"[GmWendComposition] POI '{anchorId}' has no rendered evidence");
        Transform root = renderer.transform;
        while (root.parent != null && root.parent != anchor.transform) root = root.parent;
        return root.gameObject;
    }

    static GameObject ChurchVisual(GmWorldAnchor anchor)
    {
        if (anchor == null) throw new InvalidOperationException("[GmWendComposition] chapel anchor is missing");
        Renderer renderer = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)
            .Where(candidate => candidate.enabled && candidate.name.StartsWith("SM_Church", StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.bounds.SqrDistance(anchor.transform.position))
            .FirstOrDefault();
        if (renderer == null)
            throw new InvalidOperationException("[GmWendComposition] no rendered SM_Church geometry exists near the chapel anchor");
        Debug.Log($"[GmWendComposition] chapel primary '{renderer.name}' center={renderer.bounds.center} " +
                  $"anchor={anchor.transform.position}");
        return renderer.gameObject;
    }

    static GameObject LargestRendererObject(GameObject root)
    {
        Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
            .Where(candidate => candidate.enabled)
            .OrderByDescending(candidate => candidate.bounds.size.x * candidate.bounds.size.y * candidate.bounds.size.z)
            .FirstOrDefault();
        if (renderer == null)
            throw new InvalidOperationException($"[GmWendComposition] '{root.name}' has no enabled facade renderer");
        return renderer.gameObject;
    }

    static void AuthorLighting(GameObject owner, GmRouteSpline route)
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
            .Where(light => light.gameObject.scene == owner.scene && light.enabled &&
                light.gameObject.activeInHierarchy && light.type != LightType.Directional)
            .OrderBy(light => HierarchyPath(light.transform), StringComparer.Ordinal)
            .ToArray();

        var practicals = new List<PracticalLight>();
        int environmentalIndex = 0;
        foreach (Light light in lights)
        {
            string path = HierarchyPath(light.transform);
            if (IsEnvironmentalLight(light, path))
            {
                GmAdaptiveIntentAuthoring.Light(light.gameObject,
                    $"wend-environmental-{environmentalIndex++:D2}", GmLightIntentKind.Environmental,
                    EnvironmentalRationale(path));
                continue;
            }

            Renderer source = FindPracticalSource(light);
            if (source == null)
            {
                if (path.StartsWith("Lights/", StringComparison.Ordinal))
                {
                    GmAdaptiveIntentAuthoring.Light(light.gameObject,
                        $"wend-environmental-{environmentalIndex++:D2}", GmLightIntentKind.Environmental,
                        "This imported local fill has no readable period fixture within four metres and is retained explicitly as environmental village shaping.");
                    continue;
                }
                string nearest = string.Join(", ", UnityEngine.Object
                    .FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                    .Where(renderer => renderer.enabled && renderer.gameObject.scene == owner.scene)
                    .Where(RendererLooksLikePracticalSource)
                    .OrderBy(renderer => renderer.bounds.SqrDistance(light.transform.position))
                    .Take(5)
                    .Select(renderer => $"{HierarchyPath(renderer.transform)} " +
                        $"({Mathf.Sqrt(renderer.bounds.SqrDistance(light.transform.position)):F2}m)"));
                throw new InvalidOperationException(
                    $"[GmWendComposition] practical light '{path}' has no visible lamp, flame, " +
                    $"sconce or chandelier within 4m at {light.transform.position}; route=" +
                    $"{route.ProjectDistance(light.transform.position):F1}m. Nearest source-like renderers: {nearest}");
            }
            practicals.Add(new PracticalLight(light, PracticalSourceOwner(source), LightingGroup(light, route)));
        }

        if (practicals.Count == 0) return;

        Vector3 lightingCenter = route.PointAt(route.Length * 0.5f);
        var lightingZone = new GameObject("LightingInfrastructureZone");
        lightingZone.transform.SetParent(owner.transform, true);
        lightingZone.transform.position = lightingCenter;

        IGrouping<string, PracticalLight>[] groups = practicals
            .GroupBy(item => item.group, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        GmCompositionAuthoring.Zone(lightingZone, "lighting-infrastructure-zone",
            "Visible period fixtures motivate every local practical across the route and the three playable interiors.",
            new Vector3(1600f, 240f, 1600f), groups.Length, practicals.Select(item => item.source).Distinct().Count());

        var sourceIds = new Dictionary<GameObject, string>();
        int sourceIndex = 0;
        int lightIndex = 0;
        foreach (IGrouping<string, PracticalLight> group in groups)
        {
            PracticalLight[] members = group.ToArray();
            GameObject firstSource = members[0].source;
            string firstSourceId = SourceId(firstSource, group.Key, sourceIndex++);
            sourceIds[firstSource] = firstSourceId;

            Vector3 center = members.Select(item => SourceBounds(item.source).center)
                .Aggregate(Vector3.zero, (sum, value) => sum + value) / members.Length;
            float radius = members.Max(item => Vector3.Distance(center, SourceBounds(item.source).center)) + 4f;
            var clusterObject = new GameObject($"PracticalCluster_{group.Key}");
            clusterObject.transform.SetParent(lightingZone.transform, true);
            clusterObject.transform.position = center;
            GmCompositionAuthoring.Cluster(clusterObject, $"practical-{group.Key}-cluster",
                "lighting-infrastructure-zone",
                PracticalClusterPurpose(group.Key), firstSourceId,
                minSupports: members.Select(item => item.source).Distinct().Count() > 1 ? 1 : 0,
                minDetails: 0, maxMembers: members.Select(item => item.source).Distinct().Count(),
                maxRadius: Mathf.Max(4f, radius), requireVariation: false, maxFamilyShare: 1f);

            bool first = true;
            foreach (GameObject source in members.Select(item => item.source).Distinct())
            {
                if (!sourceIds.TryGetValue(source, out string sourceId))
                {
                    sourceId = SourceId(source, group.Key, sourceIndex++);
                    sourceIds[source] = sourceId;
                }
                GmCompositionAuthoring.Element(source, sourceId,
                    $"practical-{group.Key}-cluster", "period-lighting-fixtures",
                    "A visible period fixture is the measured source for one or more local practical lights.",
                    first ? GmCompositionRole.Anchor : GmCompositionRole.Support,
                    GmSpatialRelation.BackgroundLayer, blocksRoutes: false);
                first = false;
            }

            foreach (PracticalLight practical in members)
            {
                string sourceId = sourceIds[practical.source];
                float sourceDistance = Mathf.Sqrt(SourceBounds(practical.source).SqrDistance(
                    practical.light.transform.position));
                GmAdaptiveIntentAuthoring.Light(practical.light.gameObject,
                    $"wend-practical-{lightIndex++:D2}", GmLightIntentKind.Practical,
                    "This local practical is emitted by the nearby visible period fixture rather than a floating bulb.",
                    sourceId, maximumSourceDistance: Mathf.Max(0.35f, sourceDistance + 0.15f));
            }
        }
    }

    static bool IsEnvironmentalLight(Light light, string path)
    {
        string[] tokens =
        {
            "Moonlight", "MoonBounce", "MoonFill", "WakeClockFill", "LedgerMoonFill",
            "RouteFill", "ParlorFill", "AmbientFill", "ShardGlint", "MoonlightShaft",
        };
        return (path.StartsWith("Lights/", StringComparison.Ordinal) && light.type != LightType.Point) ||
            tokens.Any(token => path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static string EnvironmentalRationale(string path)
    {
        if (path.StartsWith("Lights/", StringComparison.Ordinal))
            return "The source pack's broad area or spot light shapes the village environment and is not presented as a practical fixture.";
        if (path.IndexOf("Glint", StringComparison.OrdinalIgnoreCase) >= 0)
            return "A restrained cool glint makes the mirror shard readable without pretending to be a diegetic flame.";
        if (path.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0)
            return "A deliberately low-energy fill preserves route and prop separation between the named practical pools.";
        return "Cool moonlight enters from the authored night windows and separates the estate from its warm practicals.";
    }

    static Renderer FindPracticalSource(Light light)
    {
        const float MaximumDistance = 4f;
        return UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(renderer => renderer.enabled && renderer.gameObject.scene == light.gameObject.scene)
            .Where(RendererLooksLikePracticalSource)
            .Select(renderer => new
            {
                renderer,
                distance = renderer.bounds.SqrDistance(light.transform.position),
            })
            .Where(candidate => candidate.distance <= MaximumDistance * MaximumDistance)
            .OrderBy(candidate => candidate.distance)
            .ThenBy(candidate => HierarchyPath(candidate.renderer.transform), StringComparer.Ordinal)
            .Select(candidate => candidate.renderer)
            .FirstOrDefault();
    }

    static bool RendererLooksLikePracticalSource(Renderer renderer)
    {
        string path = HierarchyPath(renderer.transform);
        string[] tokens =
        {
            "Lantern", "Lamp", "LightPole", "Sconce", "Chandelier", "Candle", "Fire",
            "Hearth", "Ember", "Flame", "Torch", "Brazier", "Bulb",
        };
        return tokens.Any(token => path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static GameObject PracticalSourceOwner(Renderer renderer)
    {
        Transform owner = renderer.transform;
        while (owner.parent != null && NameLooksLikePracticalSource(owner.parent.name))
            owner = owner.parent;
        return owner.gameObject;
    }

    static bool NameLooksLikePracticalSource(string name)
    {
        string[] tokens =
        {
            "Lantern", "Lamp", "LightPole", "Sconce", "Chandelier", "Candle", "Fire",
            "Hearth", "Ember", "Flame", "Torch", "Brazier", "Bulb",
        };
        return tokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static Bounds SourceBounds(GameObject source)
    {
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
        if (renderers.Length == 0)
            throw new InvalidOperationException($"[GmWendComposition] light source '{source.name}' lost its visible renderer");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static string LightingGroup(Light light, GmRouteSpline route)
    {
        string path = HierarchyPath(light.transform);
        if (path.StartsWith("HouseBeginning/EntryHall/", StringComparison.Ordinal)) return "entry-hall";
        if (path.StartsWith("HouseBeginning/Parlor/", StringComparison.Ordinal)) return "parlor";
        if (path.StartsWith("WakeRoom/", StringComparison.Ordinal)) return "wake-room";
        if (path.IndexOf("CoachHouse", StringComparison.OrdinalIgnoreCase) >= 0) return "coach-house";

        float metres = route.ProjectDistance(light.transform.position);
        if (metres < 100f) return "arrival-gate";
        if (metres < 235f) return "chapel-cemetery";
        if (metres < 320f) return "working-grounds";
        if (metres < 380f) return "coach-approach";
        return "manor-threshold";
    }

    static string PracticalClusterPurpose(string group) => group switch
    {
        "entry-hall" => "Chandeliers, sconces and hearth sources structure the long Entry Hall without floating light.",
        "parlor" => "The table, chandelier and hearth practicals establish the first game's warm focal pool.",
        "wake-room" => "Candle and hearth fixtures remain subordinate to the Wake Room's cool window shaft.",
        "coach-house" => "A visible coach-house lantern signals the optional stable entrance from the road.",
        _ => "Period lanterns and existing village fixtures articulate this route segment at night.",
    };

    static string SourceId(GameObject source, string group, int index)
    {
        string clean = new string(source.name.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray()).Trim('-');
        return $"practical-source-{group}-{clean}-{index:D2}";
    }

    static string HierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Push(current.name);
        return string.Join("/", names);
    }

    readonly struct PracticalLight
    {
        public readonly Light light;
        public readonly GameObject source;
        public readonly string group;

        public PracticalLight(Light light, GameObject source, string group)
        {
            this.light = light;
            this.source = source;
            this.group = group;
        }
    }

    static void RequireRenderer(GameObject go, string label)
    {
        if (go.GetComponentsInChildren<Renderer>(true).Length == 0)
            throw new InvalidOperationException($"[GmWendComposition] '{label}' has no renderer; proxy intent is forbidden");
    }
}
