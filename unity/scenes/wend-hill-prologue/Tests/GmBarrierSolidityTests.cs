// Proves the barriers are solid by attacking them, against the REAL builder output.
//
// Two things make this different from the tests that already existed and still missed the bug Nick
// found by hand:
//
// 1. It builds the real mansion via GmMansion.Build rather than assembling a fixture. A test that
//    builds its own box and then proves the box is solid passes cheerfully while the shipped house
//    has no collision at all -- which is exactly what GmPhysicalIntegrityTests did, and exactly the
//    state the project was in this morning.
// 2. It pushes LATERALLY, at offsets beyond the barrier's own width, not just down the centreline.
//    Every automated walk this project has ever run followed the scripted route, which is why a gate
//    with two disconnected piers passed hundreds of them. A bypass lives where nobody authored
//    geometry, because nobody expected a player to go there.
using NUnit.Framework;
using UnityEngine;

public sealed class GmBarrierSolidityTests
{
    GameObject root;
    GameObject player;
    CharacterController controller;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("BarrierSolidityFixture");
        player = new GameObject("ProbePlayer");
        player.transform.SetParent(root.transform);
        controller = player.AddComponent<CharacterController>();
        // Matches the real rig in GmWendBuilder so the probe cannot squeeze through a gap the actual
        // player could not, or be stopped by one the player would walk straight past.
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.stepOffset = 0.3f;
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void TheEstateHouseCannotBeWalkedThrough()
    {
        // The house had NO collision at all until 2026-08-15: the FBX imports with addColliders: 0,
        // nothing added one, and BuildManor actively disabled any it found in the footprint. A player
        // walked to the porch, kept walking, and passed through the front wall and out the back --
        // while GmThreshold printed "The doors did not open" from route projection. Threshold Refusal
        // is the whole opening, so this is the single assertion that protects the premise.
        GameObject mansion = GmMansion.Build(0f, root.transform);
        Assert.IsNotNull(mansion, "the mansion did not build — this test cannot prove anything");

        var colliders = mansion.GetComponentsInChildren<Collider>(true);
        Assert.Greater(colliders.Length, 0,
            "the estate house has no colliders at all — it is a hologram and the player walks through it");

        Bounds bounds = MeasureRenderers(mansion);
        Physics.SyncTransforms();

        // Approach from the drive side (+Z, the way a player actually arrives), aimed at the facade.
        //
        // The barrier plane is the REAR face, not the front. Crossing the front of the bounding box
        // is not "walked through the house" -- it is reaching the porch, which is precisely what the
        // player is meant to do, and the first version of this test failed for exactly that reason:
        // the probe was stopped (blocked=True) after climbing the porch steps from y 0.95 to 2.63 and
        // was then reported as a penetration. What Threshold Refusal actually forbids is getting
        // through the building and out the far side into the field behind it.
        Vector3 forward = Vector3.back;
        float standY = bounds.min.y + controller.height * 0.5f + 0.05f;
        var center = new Vector3(bounds.center.x, standY, bounds.min.z);

        // Offsets deliberately span the WHOLE facade and past both corners. Centre-only is what every
        // previous check did, and centre-only is exactly what a bypass avoids.
        float half = bounds.extents.x;
        float[] offsets =
        {
            0f, half * 0.25f, -half * 0.25f, half * 0.5f, -half * 0.5f,
            half * 0.8f, -half * 0.8f,
        };

        // Backoff clears the whole depth of the house so every push starts out on the drive, in
        // front of the facade, and has to cross the entire building to register a penetration.
        var results = GmPhysicalIntegrityProbe.SweepLateralBypass(
            controller, center, forward, offsets,
            pushDistance: bounds.size.z + 10f, startBackoff: bounds.size.z + 3f);

        foreach (GmPhysicalIntegrityProbe.BypassAttemptResult attempt in results)
        {
            Assert.IsFalse(attempt.penetratedBarrier,
                "a player walked THROUGH the estate house and out the back: "
                + GmPhysicalIntegrityProbe.Describe(attempt));
        }
    }

    [Test]
    public void AClosedGateStopsThePlayerAtEveryOffsetAcrossItsOpening()
    {
        // The gate leaves' barrier is disabled while open and enabled on close, so a test that never
        // closes the gate proves nothing about the lock. Close it, then attack it.
        var rig = new GameObject("EstateGateFixture");
        rig.transform.SetParent(root.transform);

        var barrierObject = new GameObject("GateBarrier");
        barrierObject.transform.SetParent(rig.transform, false);
        barrierObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var barrier = barrierObject.AddComponent<BoxCollider>();
        barrier.size = new Vector3(5.2f, 3.3f, 0.4f);

        var left = new GameObject("LeafLeft").transform;
        left.SetParent(rig.transform, false);
        var right = new GameObject("LeafRight").transform;
        right.SetParent(rig.transform, false);

        var leaves = rig.AddComponent<GmGateLeaves>();
        leaves.Configure(left, right, 96f, 0.55f, barrier);
        Assert.IsFalse(barrier.enabled, "an OPEN gate is already blocking the player");

        leaves.SetClosedImmediate();
        Assert.IsTrue(barrier.enabled, "the gate closed but its barrier stayed disabled");
        Physics.SyncTransforms();

        // Across the opening only. The wings beyond +/-2.6m are a separate, still-open question
        // (they stop at 45m with open terrain past them, TASKBOARD G2) and this test deliberately
        // does not pretend to cover it -- asserting a pass out there would be the lie.
        float[] offsets = { 0f, 1.2f, -1.2f, 2.2f, -2.2f };
        var results = GmPhysicalIntegrityProbe.SweepLateralBypass(
            controller, barrierObject.transform.position, Vector3.forward, offsets,
            pushDistance: 6f, startBackoff: 2f);

        foreach (GmPhysicalIntegrityProbe.BypassAttemptResult attempt in results)
        {
            Assert.IsFalse(attempt.penetratedBarrier,
                "a player walked through the CLOSED estate gate: " + GmPhysicalIntegrityProbe.Describe(attempt));
        }
    }

    [Test]
    public void TheProbeItselfReportsAPenetrationWhenThereIsNothingThere()
    {
        // Without this the two tests above are unfalsifiable: a probe that can never report a
        // penetration would pass them with no barriers in the scene at all. Break it on purpose.
        var empty = new GameObject("NoBarrierHere");
        empty.transform.SetParent(root.transform);
        empty.transform.position = new Vector3(0f, 1f, 0f);
        Physics.SyncTransforms();

        var results = GmPhysicalIntegrityProbe.SweepLateralBypass(
            controller, empty.transform.position, Vector3.forward, new[] { 0f },
            pushDistance: 6f, startBackoff: 2f);

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].penetratedBarrier,
            "the probe walked through thin air and reported it as blocked — every barrier test above is worthless");
    }

    static Bounds MeasureRenderers(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        Assert.Greater(renderers.Length, 0, "nothing rendered — cannot measure the facade");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
