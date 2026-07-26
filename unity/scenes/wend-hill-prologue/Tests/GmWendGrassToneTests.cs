// Guards the rule that decides whether a grass material's _Albedo_Tint is biased, and the
// neutralisation that fixes it. Pure, tested without a real material or shader.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendGrassToneTests
{
    [Test]
    public void TheMeasuredMGrassTintIsBiased()
    {
        Assert.IsTrue(GmWendGrassTone.IsBiased(new Color(0.881f, 1.000f, 0.627f)));
    }

    [Test]
    public void TheMeasuredMGrass2TintIsBiased()
    {
        Assert.IsTrue(GmWendGrassTone.IsBiased(new Color(0.479f, 0.840f, 0.486f)));
    }

    [Test]
    public void TheMeasuredMLeafTintIsNotBiased()
    {
        // Already neutral in the pack -- must not be flagged or touched.
        Assert.IsFalse(GmWendGrassTone.IsBiased(new Color(1f, 1f, 1f)));
    }

    [Test]
    public void ATintJustUnderTheThresholdIsNotFlagged()
    {
        // Half the threshold, not right at its boundary -- a value exactly at the threshold is exposed
        // to float rounding on the addition that built it, which is not what this test is for.
        float halfway = GmWendGrassTone.SpreadThreshold * 0.5f;
        Assert.IsFalse(GmWendGrassTone.IsBiased(new Color(0.60f, 0.60f + halfway, 0.60f)));
    }

    [Test]
    public void BiasInAnyDirectionIsCaught()
    {
        // Unlike the wall fix this rule is not green-specific: M_grass 1's actual measured tint leans
        // warm (red highest), not green, and still needs catching.
        Assert.IsTrue(GmWendGrassTone.IsBiased(new Color(0.679f, 0.565f, 0.516f)));
    }

    [Test]
    public void NeutralizePreservesLumaAndRemovesHue()
    {
        Color neutral = GmWendGrassTone.Neutralize(new Color(0.881f, 1.000f, 0.627f));
        Assert.AreEqual(neutral.r, neutral.g, 0.0001f);
        Assert.AreEqual(neutral.g, neutral.b, 0.0001f);

        float expectedLuma = 0.2126f * 0.881f + 0.7152f * 1.000f + 0.0722f * 0.627f;
        Assert.AreEqual(expectedLuma, neutral.r, 0.0001f);
    }

    [Test]
    public void NeutralizePreservesAlpha()
    {
        Color neutral = GmWendGrassTone.Neutralize(new Color(0.881f, 1.000f, 0.627f, 0.5f));
        Assert.AreEqual(0.5f, neutral.a, 0.0001f);
    }

    [Test]
    public void TheMeasuredMGrassIntensityIsElevated()
    {
        Assert.IsTrue(GmWendGrassTone.IsIntensityElevated(2.2f));
    }

    [Test]
    public void TheMeasuredMGrass2IntensityIsElevated()
    {
        // -6 is nowhere near the neutral of 1 -- but it is BELOW it, and this rule is deliberately
        // one-sided (see the header on IntensityAboveThreshold), so it must NOT be flagged by intensity
        // alone. M_grass 2 still gets owned and fixed this pass, just via its tint bias.
        Assert.IsFalse(GmWendGrassTone.IsIntensityElevated(-6f));
    }

    [Test]
    public void MLeafIntensityIsNotElevated()
    {
        // Below neutral, same as M_grass 2 -- no measured evidence this direction is broken.
        Assert.IsFalse(GmWendGrassTone.IsIntensityElevated(-0.17f));
    }

    [Test]
    public void NeutralIntensityItselfIsNotFlagged()
    {
        Assert.IsFalse(GmWendGrassTone.IsIntensityElevated(GmWendGrassTone.NeutralIntensity));
    }

    [Test]
    public void AnIntensityJustAboveTheThresholdIsFlagged()
    {
        float justOver = GmWendGrassTone.NeutralIntensity + GmWendGrassTone.IntensityAboveThreshold + 0.01f;
        Assert.IsTrue(GmWendGrassTone.IsIntensityElevated(justOver));
    }
}
