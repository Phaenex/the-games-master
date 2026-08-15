using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds enterable outbuilding interiors on the estate grounds. Slice 1: the coach house only --
/// the site's coaching-inn layer (docs/superpowers/specs/2026-07-14-house-history.md L4) made
/// physical, and the cheapest room on the estate to enter per
/// docs/superpowers/specs/2026-08-13-the-reckoning.md's "privacy, not curiosity" rule. Reuses
/// GmHouseBeginningBuilder's room-shell/material/interactable helpers rather than duplicating them.
/// </summary>
public static class GmWendOutbuildings
{
    const string CoachHouseId = "coach-house";
    const float RoomWidth = 7f;
    const float RoomDepth = 9f;
    const float RoomHeight = 4f;
    const float DoorwayWidth = 2.6f;
    const float WallT = 0.32f;

    public static void Build(Transform openingRoot, GmRouteSpline route)
    {
        GameObject stale = GameObject.Find("Outbuildings");
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

        var outbuildingsRoot = new GameObject("Outbuildings").transform;
        outbuildingsRoot.SetParent(openingRoot, true);

        BuildCoachHouse(outbuildingsRoot, route);
    }

    static void BuildCoachHouse(Transform parent, GmRouteSpline route)
    {
        GmWorldAnchor doorAnchor = GmWorldAnchor.Find("coach-doors");
        if (doorAnchor == null)
            throw new InvalidOperationException("coach house interior requires the 'coach-doors' POI anchor to exist first");

        // Same lateral-offset math GmWendOpening.Offset uses to place the facade POI: the room
        // extends further along the route's local "right" than the door itself, so the doorway
        // sits near the existing facade and the room grows away from the walked line.
        Vector3 doorPosition = doorAnchor.transform.position;
        Vector3 tangent = route.TangentAt(338f);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Quaternion roomRotation = Quaternion.LookRotation(right, Vector3.up);

        var room = new GameObject("CoachHouse").transform;
        room.SetParent(parent, true);
        // Local +Z (the axis GmHouseBeginningBuilder.BuildRoomShell carves doors along) now points
        // along world `right`, so a south doorway at local Z ~ -halfDepth sits close to the anchor.
        room.SetPositionAndRotation(doorPosition + right * (RoomDepth * 0.5f + 2.5f), roomRotation);

        Material wall = GmHouseBeginningBuilder.Mat("Coach_Wall", new Color(0.20f, 0.17f, 0.14f), 0.12f);
        Material floor = GmHouseBeginningBuilder.Mat("Coach_Floor", new Color(0.16f, 0.12f, 0.09f), 0.10f);
        Material darkWood = GmHouseBeginningBuilder.Mat("Coach_DarkWood", new Color(0.14f, 0.10f, 0.07f), 0.18f);
        Material chalk = GmHouseBeginningBuilder.Mat("Coach_Chalk", new Color(0.62f, 0.58f, 0.52f), 0.05f);

        BuildRoomShellWithFloorMaterial(room, "CoachHouse", 0f, RoomWidth, RoomDepth, RoomHeight,
            DoorwayWidth, wall, floor);

        var interiorRoot = new GameObject("Interior").transform;
        interiorRoot.SetParent(room, false);

        // Stalls: three timber partitions along the east wall, tally marks chalked on the last one.
        for (int i = 0; i < 3; i++)
        {
            float localZ = -2.6f + i * 2.6f;
            GmHouseBeginningBuilder.Slab($"Stall_{i}", new Vector3(2.0f, RoomHeight * 0.3f, localZ),
                new Vector3(0.12f, RoomHeight * 0.6f, 2.2f), darkWood, interiorRoot, false);
        }
        GameObject tallyBoard = GmHouseBeginningBuilder.Slab("TallyBoard", new Vector3(2.55f, 1.5f, -2.6f),
            new Vector3(0.05f, 1.0f, 1.6f), chalk, interiorRoot);

        // Departures board: the coach house's multi-line clue, escalating cost per line read.
        GameObject departuresBoard = GmHouseBeginningBuilder.Slab("DeparturesBoard",
            new Vector3(-2.6f, 1.6f, 3.2f), new Vector3(1.4f, 1.0f, 0.06f), chalk, interiorRoot);

        // A single warm lantern -- the only light source, on while the interior is active.
        GmHouseBeginningBuilder.AddWarmLight(interiorRoot, "CoachLantern",
            new Vector3(0f, RoomHeight - 0.6f, 0f), 260f, 8f);

        interiorRoot.gameObject.SetActive(false);

        // Trigger volume covering the whole room footprint plus a short approach apron, so the
        // interior activates a step before the player actually crosses the doorway threshold.
        var volumeGo = room.gameObject;
        BoxCollider trigger = volumeGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, RoomHeight * 0.5f, 1.5f);
        trigger.size = new Vector3(RoomWidth + 1.5f, RoomHeight + 1f, RoomDepth + 4f);
        GmOutbuilding outbuilding = volumeGo.AddComponent<GmOutbuilding>();
        outbuilding.Configure(CoachHouseId, interiorRoot.gameObject);

        AddClue(interiorRoot, tallyBoard, "coach-house-tally-marks", CoachHouseId, "Examine",
            "Tally marks chalked in groups of seven, worn into the wood.",
            "The freshest group is unweathered. Someone marked this tonight.");
        AddClue(interiorRoot, departuresBoard, "coach-house-departures-line-1", CoachHouseId, "Read",
            "A departures board, chalk long dry. Coaches, timetabled, to towns that don't run coaches anymore.",
            "One line is rewritten over and over in the same hand: LAST CALL, WEND HILL.");
        AddClue(interiorRoot, GmHouseBeginningBuilder.Slab("WheelLedger", new Vector3(1.2f, 0.9f, 3.6f),
            new Vector3(0.5f, 0.05f, 0.4f), chalk, interiorRoot), "coach-house-wheel-ledger", CoachHouseId, "Read",
            "A leather ledger of wheel repairs, dated across two centuries in three different hands.",
            "The most recent entry is dated tonight, and signed with your own initials.");

        Debug.Log($"[GmWendOutbuildings] PASS: coach house built at {room.position}, 3 clues, interior starts inactive");
    }

    static void AddClue(Transform interiorParent, GameObject prop, string clueId, string buildingId,
        string verb, string first, string second)
    {
        GmHouseBeginningBuilder.AddInteractable(prop, $"outbuilding-{clueId}", verb, first, second, 3.2f, 10f);
        GmOutbuildingClue clue = prop.AddComponent<GmOutbuildingClue>();
        clue.Configure($"outbuilding-{clueId}", buildingId);
    }

    /// GmHouseBeginningBuilder.BuildRoomShell always uses its own shared plaster/floor materials --
    /// this local carve mirrors its wall/doorway geometry exactly but with the coach house's own
    /// materials, since the shared instance fields it reads (plaster/floor) are private to that class.
    static void BuildRoomShellWithFloorMaterial(Transform parent, string prefix, float centreZ,
        float width, float depth, float height, float doorwayWidth, Material wallMaterial, Material floorMaterial)
    {
        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;
        GmHouseBeginningBuilder.Slab(prefix + "Floor", new Vector3(0f, -WallT * 0.5f, centreZ),
            new Vector3(width + WallT, WallT, depth + WallT), floorMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "Ceiling", new Vector3(0f, height + WallT * 0.5f, centreZ),
            new Vector3(width + WallT, WallT, depth + WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "WestWall", new Vector3(-halfW - WallT * 0.5f, height * 0.5f, centreZ),
            new Vector3(WallT, height, depth + WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "EastWall", new Vector3(halfW + WallT * 0.5f, height * 0.5f, centreZ),
            new Vector3(WallT, height, depth + WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "NorthWall", new Vector3(0f, height * 0.5f, centreZ + halfD + WallT * 0.5f),
            new Vector3(width + WallT, height, WallT), wallMaterial, parent);

        // South wall carries the doorway -- the big coach doors, open, facing back toward the route.
        float side = (width - doorwayWidth) * 0.5f;
        float southZ = centreZ - halfD - WallT * 0.5f;
        GmHouseBeginningBuilder.Slab(prefix + "SouthWallWest",
            new Vector3(-(doorwayWidth * 0.5f + side * 0.5f), height * 0.5f, southZ),
            new Vector3(side, height, WallT), wallMaterial, parent);
        GmHouseBeginningBuilder.Slab(prefix + "SouthWallEast",
            new Vector3(doorwayWidth * 0.5f + side * 0.5f, height * 0.5f, southZ),
            new Vector3(side, height, WallT), wallMaterial, parent);
        float lintelHeight = Mathf.Max(0.2f, height - 3.6f);
        GmHouseBeginningBuilder.Slab(prefix + "Lintel", new Vector3(0f, 3.6f + lintelHeight * 0.5f, southZ),
            new Vector3(doorwayWidth, lintelHeight, WallT), wallMaterial, parent);
    }
}
