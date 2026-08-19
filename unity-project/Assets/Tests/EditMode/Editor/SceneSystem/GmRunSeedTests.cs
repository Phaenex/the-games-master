using NUnit.Framework;

public sealed class GmRunSeedTests
{
    [TearDown]
    public void TearDown() => GmRunSeed.ResetForTests();

    [Test]
    public void SameSeedAndStreamProduceTheSameSequence()
    {
        GmRunSeed.ForceForReview(8837);
        var a = GmRunSeed.ForStream("toll-jitter");
        GmRunSeed.ForceForReview(8837);
        var b = GmRunSeed.ForStream("toll-jitter");

        for (int i = 0; i < 20; i++)
            Assert.AreEqual(a.Next(), b.Next(), $"draw {i} diverged for the same (seed, stream) pair.");
    }

    [Test]
    public void DifferentStreamsFromTheSameSeedDoNotShareASequence()
    {
        GmRunSeed.ForceForReview(1);
        var a = GmRunSeed.ForStream("toll-jitter");
        var b = GmRunSeed.ForStream("something-else-entirely");

        bool anyDifferent = false;
        for (int i = 0; i < 20; i++)
            if (a.Next() != b.Next()) { anyDifferent = true; break; }

        Assert.IsTrue(anyDifferent, "two named streams from the same seed must not draw identical sequences.");
    }

    [Test]
    public void DifferentSeedsProduceDifferentSequencesForTheSameStream()
    {
        GmRunSeed.ForceForReview(1);
        var a = GmRunSeed.ForStream("toll-jitter");
        GmRunSeed.ForceForReview(2);
        var b = GmRunSeed.ForStream("toll-jitter");

        bool anyDifferent = false;
        for (int i = 0; i < 20; i++)
            if (a.Next() != b.Next()) { anyDifferent = true; break; }

        Assert.IsTrue(anyDifferent, "two different seeds must not draw identical sequences for the same stream.");
    }

    [Test]
    public void ExactSeededScheduleReplayIsReproducible()
    {
        // (seed, ordered toll events) -> exact schedule must be a pure function: run the same nine
        // draws twice from the same forced seed and assert byte-identical results.
        float[] Draw()
        {
            GmRunSeed.ForceForReview(42);
            System.Random rng = GmRunSeed.ForStream("toll-jitter");
            float[] intervals = new float[9];
            for (int toll = 1; toll <= 9; toll++)
                intervals[toll - 1] = GmReckoningSchedule.IntervalForToll(toll, 45f, 30f, 1f, 1f,
                    GmReckoningSchedule.JitterFraction(rng, 0.2f), 20f, 15f);
            return intervals;
        }

        float[] first = Draw();
        float[] second = Draw();
        CollectionAssert.AreEqual(first, second);
    }

    [Test]
    public void ScalarStreamSeedPreservesTheExistingForStreamSequence()
    {
        GmRunSeed.ForceForReview(8837);
        int scalar = GmRunSeed.SeedForStream("toll-jitter");
        Assert.That(scalar, Is.EqualTo(-770589582));
        var fromPublicScalar = new System.Random(scalar);
        var legacyFactory = GmRunSeed.ForStream("toll-jitter");
        for (int index = 0; index < 20; index++)
            Assert.That(fromPublicScalar.Next(), Is.EqualTo(legacyFactory.Next()));
    }
}
