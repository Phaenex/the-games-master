// Guards the rule that decides whether a wall's _BaseTint is green-biased, and the neutralisation
// that fixes it. Pure, tested without a real material or shader, so a future pack update that ships a
// new green-biased material is caught by the same rule rather than needing a name added to a list.
using NUnit.Framework;
using UnityEngine;

public sealed class GmWendWallTintTests
{
    [Test]
    public void TheMeasuredDefectIsGreenBiased()
    {
        // M_Wall_02's actual shipped tint, the one that read as a mint-green wall at 330m, 345m and
        // the 360m close pass.
        Assert.IsTrue(GmWendWallTint.IsGreenBiased(new Color(0.596f, 0.635f, 0.525f)));
    }

    [Test]
    public void NeutralGreysAreNotFlagged()
    {
        // M_Wall_01 and M_Wall_03's actual shipped tints, both genuinely neutral and correctly left
        // alone.
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.66f, 0.66f, 0.66f)));
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.6f, 0.6f, 0.6f)));
    }

    [Test]
    public void AWarmBiasIsNotGreenBiased()
    {
        // M_Church_Wall's actual shipped tint: red leads, a deliberate sandstone warmth. The rule must
        // not treat every non-grey tint as the same defect.
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.612f, 0.533f, 0.302f)));
    }

    [Test]
    public void ABlueBiasIsNotGreenBiased()
    {
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.5f, 0.5f, 0.7f)));
    }

    [Test]
    public void ATintJustUnderTheThresholdIsNotFlagged()
    {
        // Green leads red by exactly the threshold, not past it -- the rule is "green must lead by
        // MORE than the threshold", so an exact match on either axis must not trip it.
        float t = GmWendWallTint.GreenBiasThreshold;
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.60f, 0.60f + t, 0.55f)));
    }

    [Test]
    public void GreenMustLeadBothOtherChannelsNotJustOne()
    {
        // Green ahead of red but not blue (or vice versa) is not the mossy-stone signature this rule
        // exists to catch.
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.50f, 0.60f, 0.65f)));
        Assert.IsFalse(GmWendWallTint.IsGreenBiased(new Color(0.65f, 0.60f, 0.50f)));
    }

    [Test]
    public void NeutralizePreservesLumaAndRemovesHue()
    {
        Color neutral = GmWendWallTint.Neutralize(new Color(0.596f, 0.635f, 0.525f));
        Assert.AreEqual(neutral.r, neutral.g, 0.0001f, "a neutralised tint must be equal on every channel");
        Assert.AreEqual(neutral.g, neutral.b, 0.0001f);

        float expectedLuma = 0.2126f * 0.596f + 0.7152f * 0.635f + 0.0722f * 0.525f;
        Assert.AreEqual(expectedLuma, neutral.r, 0.0001f, "must preserve luma rather than flattening to white");
    }

    [Test]
    public void NeutralizePreservesAlpha()
    {
        // _BaseTint's alpha is part of the shader's blend math, not a colour channel. Only hue is this
        // rule's business.
        Color neutral = GmWendWallTint.Neutralize(new Color(0.596f, 0.635f, 0.525f, 0.73f));
        Assert.AreEqual(0.73f, neutral.a, 0.0001f);
    }
}
