using NUnit.Framework;

public sealed class GmDisplayCalibrationTests
{
    [Test]
    public void BrightnessRangeStaysNarrowAndCentredOnAuthoredExposure()
    {
        Assert.AreEqual(-2, GmDisplayCalibration.ClampLevel(-100));
        Assert.AreEqual(0, GmDisplayCalibration.ClampLevel(0));
        Assert.AreEqual(2, GmDisplayCalibration.ClampLevel(100));
        Assert.AreEqual(-0.5f, GmDisplayCalibration.OffsetForLevel(-2));
        Assert.AreEqual(0f, GmDisplayCalibration.OffsetForLevel(0));
        Assert.AreEqual(0.5f, GmDisplayCalibration.OffsetForLevel(2));
    }
}
