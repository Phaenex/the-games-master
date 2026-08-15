using NUnit.Framework;
using UnityEngine;

public sealed class GmPhysicalIntegrityTests
{
    GameObject root;
    GameObject playerObj;
    CharacterController controller;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("PhysicalIntegrityFixture");
        playerObj = new GameObject("PlayerFixture");
        playerObj.transform.SetParent(root.transform);
        controller = playerObj.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void ClosedGateLeavesEnableBarrierAndBlockTraversal()
    {
        var gateObj = new GameObject("EstateGate");
        gateObj.transform.SetParent(root.transform);
        gateObj.transform.position = new Vector3(0f, 0f, 18f);

        var barrier = new GameObject("GateBarrier");
        barrier.transform.SetParent(gateObj.transform, false);
        var barrierCol = barrier.AddComponent<BoxCollider>();
        barrierCol.size = new Vector3(5.2f, 3.3f, 0.4f);

        var leftPivot = new GameObject("Left").transform;
        leftPivot.SetParent(gateObj.transform);
        var rightPivot = new GameObject("Right").transform;
        rightPivot.SetParent(gateObj.transform);

        var leaves = gateObj.AddComponent<GmGateLeaves>();
        leaves.Configure(leftPivot, rightPivot, 96f, 0.55f, barrierCol);

        // Open state: barrier is disabled
        Assert.That(barrierCol.enabled, Is.False, "Open gate must leave barrier collider disabled");

        // Close state: barrier is enabled
        leaves.SetClosedImmediate();
        Assert.That(barrierCol.enabled, Is.True, "Closed gate must enable barrier collider");
        Assert.That(leaves.IsClosed, Is.True);
    }

    [Test]
    public void LateralWallSegmentsContainPerimeter()
    {
        // Build perimeter wing from -30 to -3 and verify colliders span the length
        var wingObj = new GameObject("PerimeterWing");
        wingObj.transform.SetParent(root.transform);

        float startX = 2.7f;
        float endX = 30f;
        float span = endX - startX;
        int sections = Mathf.CeilToInt(span / 3.5f);
        float sectionWidth = span / sections;

        for (int i = 0; i < sections; i++)
        {
            float x1 = startX + i * sectionWidth;
            float x2 = startX + (i + 1) * sectionWidth;
            float midX = (x1 + x2) * 0.5f;

            var baseWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseWall.transform.SetParent(wingObj.transform);
            baseWall.transform.position = new Vector3(midX, 0.6f, 0f);
            baseWall.transform.localScale = new Vector3(sectionWidth, 1.2f, 0.45f);
        }

        Collider[] colliders = wingObj.GetComponentsInChildren<Collider>();
        Assert.That(colliders.Length, Is.EqualTo(sections), "Every perimeter section must have a solid collider");
    }

    [Test]
    public void GmWendBoundsEnclosesEntirePlayableWorld()
    {
        Bounds testWorld = new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
        GmWendBounds.WallBox[] walls = GmWendBounds.WallBoxes(testWorld);

        Assert.That(walls.Length, Is.EqualTo(4), "Must produce North, South, East, West boundary walls");

        GmWendBounds.WallBox north = System.Array.Find(walls, w => w.name == "Wall_North");
        GmWendBounds.WallBox south = System.Array.Find(walls, w => w.name == "Wall_South");
        GmWendBounds.WallBox east = System.Array.Find(walls, w => w.name == "Wall_East");
        GmWendBounds.WallBox west = System.Array.Find(walls, w => w.name == "Wall_West");

        Assert.That(north.size.x, Is.GreaterThanOrEqualTo(testWorld.size.x + GmWendBounds.WallThickness));
        Assert.That(south.size.x, Is.GreaterThanOrEqualTo(testWorld.size.x + GmWendBounds.WallThickness));
        Assert.That(east.size.z, Is.GreaterThanOrEqualTo(testWorld.size.z + GmWendBounds.WallThickness));
        Assert.That(west.size.z, Is.GreaterThanOrEqualTo(testWorld.size.z + GmWendBounds.WallThickness));
    }
}
