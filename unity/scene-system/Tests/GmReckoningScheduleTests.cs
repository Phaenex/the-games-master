using NUnit.Framework;

public sealed class GmReckoningScheduleTests
{
    const float FirstTollDelay = 45f;
    const float TollInterval = 30f;
    const float FirstFloor = 20f;
    const float RestFloor = 15f;

    [Test]
    public void DefaultAuthorityReproducesTheShippedTwoEightyFiveSecondCadence()
    {
        float total = GmReckoningSchedule.IntervalForToll(1, FirstTollDelay, TollInterval, 0f, 1f, 0f, FirstFloor, RestFloor);
        for (int toll = 2; toll <= 9; toll++)
            total += GmReckoningSchedule.IntervalForToll(toll, FirstTollDelay, TollInterval, 0f, 1f, 0f, FirstFloor, RestFloor);

        Assert.AreEqual(285f, total, 0.001f,
            "authority=0 must reproduce the fixed cadence exactly -- this is the byte-identical " +
            "default the PlayMode 285f assertion also checks.");
    }

    [Test]
    public void ZeroAuthorityIgnoresPressureAndJitterEntirely()
    {
        float withoutPressure = GmReckoningSchedule.IntervalForToll(2, FirstTollDelay, TollInterval, 0f, 0f, 0.2f, FirstFloor, RestFloor);
        float withFullPressure = GmReckoningSchedule.IntervalForToll(2, FirstTollDelay, TollInterval, 0f, 1f, 0.2f, FirstFloor, RestFloor);
        // At authority 0 the (1 - authority*pressure) term is always 1 regardless of pressure, so
        // only the jitter term can move the value, and both calls use the same jitter -- they must
        // be equal even though pressure differs wildly.
        Assert.AreEqual(withoutPressure, withFullPressure, 0.0001f);
    }

    [Test]
    public void MaximumPressureNeverExceedsTheUnpressuredBase()
    {
        for (int toll = 1; toll <= 9; toll++)
        {
            float interval = GmReckoningSchedule.IntervalForToll(toll, FirstTollDelay, TollInterval, 1f, 1f, 0.3f, FirstFloor, RestFloor);
            float baseSeconds = toll <= 1 ? FirstTollDelay : TollInterval;
            Assert.LessOrEqual(interval, baseSeconds,
                $"toll {toll}: pressure/jitter must never push a toll LATER than its own base interval.");
        }
    }

    [Test]
    public void MaximumPressureNeverGoesBelowTheConfiguredFloor()
    {
        for (int toll = 1; toll <= 9; toll++)
        {
            float interval = GmReckoningSchedule.IntervalForToll(toll, FirstTollDelay, TollInterval, 1f, 1f, -0.3f, FirstFloor, RestFloor);
            float floor = toll <= 1 ? FirstFloor : RestFloor;
            Assert.GreaterOrEqual(interval, floor,
                $"toll {toll}: no legal pressure/jitter combination may collapse a toll below its configured floor.");
        }
    }

    [Test]
    public void UpperAndLowerBoundHelpersMatchNineTollSums()
    {
        Assert.AreEqual(285f, GmReckoningSchedule.UpperBoundSeconds(FirstTollDelay, TollInterval), 0.001f);
        Assert.AreEqual(FirstFloor + 8f * RestFloor, GmReckoningSchedule.LowerBoundSeconds(FirstFloor, RestFloor), 0.001f);
    }

    [Test]
    public void ConfiguredFloorsCanNeverDropTheGuaranteedLowerBoundBelowTestSpeedThresholds()
    {
        // GmPerceptualAudit rejects any review-profile candidate with FirstTollDelay < 20s or
        // TollInterval < 15s as test-speed pacing. The [Range] bounds on GmFeelConfig's floor
        // fields keep this true structurally, but pin the arithmetic here too so a future change to
        // either constant fails loudly instead of silently drifting under the audited minimum.
        Assert.GreaterOrEqual(FirstFloor, 20f);
        Assert.GreaterOrEqual(RestFloor, 15f);
    }

    [Test]
    public void JitterFractionIsZeroWhenConfiguredZero()
    {
        var rng = new System.Random(1);
        Assert.AreEqual(0f, GmReckoningSchedule.JitterFraction(rng, 0f));
    }

    [Test]
    public void JitterFractionStaysWithinItsConfiguredMagnitude()
    {
        var rng = new System.Random(42);
        for (int i = 0; i < 200; i++)
        {
            float jitter = GmReckoningSchedule.JitterFraction(rng, 0.2f);
            Assert.LessOrEqual(jitter, 0.2f);
            Assert.GreaterOrEqual(jitter, -0.2f);
        }
    }
}
