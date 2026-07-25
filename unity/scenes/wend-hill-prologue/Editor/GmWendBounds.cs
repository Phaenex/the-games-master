// Closes the edge of the map: invisible walls around the playable area, and a catch height under it.
//
// The defect: the prologue has no floor and no boundary at the map edge. A walk went off it and fell
// 690m, and the frames it captured falling read mean luma 0.005, which a luma check alone calls a
// darker night rather than an absent world. Falling out of a scene is not a lighting result.
//
// Walls rather than a kill plane alone, because the prologue is a guided walk into a village: stopping
// the player costs them nothing and needs no death UI, no fade and no restart path, none of which
// exist in this scene. The catch height under the terrain stays as the failsafe for a gap in the
// walls, and it is loud when it fires. See GmWendCatchPlane.
//
// NO HARDCODED COORDINATES. The boundary is derived from the terrain every build, because this scene
// is recopied from the purchased source on every run and any baked position would go stale the moment
// the pack is updated. That failure has already happened in this project more than once.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GmWendBounds
{
    const string LogTag = "GmWendBounds";

    public const string RootName = "GmWendBounds";

    /// Clearance above the highest point of the world. The walls span the full height of the terrain
    /// plus this, so no rise in the landscape can put the top of a wall within reach.
    public const float WallClearance = 60f;
    public const float WallThickness = 4f;

    /// How far below the LOWEST point of the world the catch height sits. Anything under this is not
    /// standing on anything, because the terrain does not reach there.
    public const float CatchDepth = 30f;

    public struct WallBox
    {
        public string name;
        public Vector3 centre;
        public Vector3 size;
    }

    /// The four walls for a given world box, sealed at the corners.
    ///
    /// Pure, so the geometry can be tested without a terrain. Each side spans its full edge PLUS one
    /// thickness, which is what closes the corners: four walls sized to their exact edge leave a
    /// thickness-wide diagonal gap at each corner, and a gap in a boundary is the whole defect.
    public static WallBox[] WallBoxes(Bounds world)
    {
        float height = world.size.y + WallClearance;
        float centreY = world.min.y + height * 0.5f;
        float spanX = world.size.x + WallThickness;
        float spanZ = world.size.z + WallThickness;

        return new[]
        {
            new WallBox
            {
                name = "Wall_North",
                centre = new Vector3(world.center.x, centreY, world.max.z),
                size = new Vector3(spanX, height, WallThickness),
            },
            new WallBox
            {
                name = "Wall_South",
                centre = new Vector3(world.center.x, centreY, world.min.z),
                size = new Vector3(spanX, height, WallThickness),
            },
            new WallBox
            {
                name = "Wall_East",
                centre = new Vector3(world.max.x, centreY, world.center.z),
                size = new Vector3(WallThickness, height, spanZ),
            },
            new WallBox
            {
                name = "Wall_West",
                centre = new Vector3(world.min.x, centreY, world.center.z),
                size = new Vector3(WallThickness, height, spanZ),
            },
        };
    }

    /// The height below which the player is not standing on the world. Pure, for the same reason.
    public static float CatchY(Bounds world) => world.min.y - CatchDepth;

    /// The playable box, from the terrain when there is one.
    ///
    /// The terrain is the floor, so its footprint is the only honest definition of "the world". The
    /// renderer fallback exists because a scene with no terrain would otherwise get no boundary at
    /// all, and silently shipping no boundary is the defect this file was written to remove.
    public static Bounds WorldBounds()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 size = terrain.terrainData.size;
            Vector3 origin = terrain.transform.position;
            var bounds = new Bounds();
            bounds.SetMinMax(origin, origin + size);
            return bounds;
        }

        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.gameObject.activeInHierarchy)
            .ToArray();
        if (all.Length == 0)
            throw new System.InvalidOperationException(
                "cannot derive a boundary: the scene has no terrain and no active renderers");

        Bounds combined = all[0].bounds;
        foreach (Renderer r in all) combined.Encapsulate(r.bounds);
        Debug.LogWarning($"[{LogTag}] no terrain; boundary derived from {all.Length} renderer bounds instead");
        return combined;
    }

    /// Builds the boundary into the open scene. Returns the number of walls created.
    public static int Apply()
    {
        GameObject stale = GameObject.Find(RootName);
        if (stale != null) Object.DestroyImmediate(stale);

        Bounds world = WorldBounds();
        var root = new GameObject(RootName);

        WallBox[] boxes = WallBoxes(world);
        foreach (WallBox box in boxes)
        {
            var go = new GameObject(box.name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = box.centre;
            // A collider with no renderer: solid to the player, invisible in every frame, and it adds
            // nothing to the lighting census the builder asserts on.
            go.AddComponent<BoxCollider>().size = box.size;
        }

        // The same route definition the spawn and the walk use, so a recovery lands somewhere the
        // player could actually have been standing.
        List<Vector3> route = GmWendRoute.Build(out _);
        float catchY = CatchY(world);
        root.AddComponent<GmWendCatchPlane>().Configure(catchY, route);

        Debug.Log($"[{LogTag}] boundary applied: {boxes.Length} walls around " +
                  $"{world.size.x:0}x{world.size.z:0}m, catch height {catchY:0} " +
                  $"({CatchDepth:0}m under the terrain), {route.Count} recovery point(s)");
        return boxes.Length;
    }
}
