using NUnit.Framework;
using UnityEditor;

public sealed class GmWendProfileBuildTests
{
    [Test]
    public void ProfileBuildIsDevelopmentOnlyAndCannotOverwriteShippingReviewApp()
    {
        Assert.That(GmWendStandaloneBuild.ReleaseBuildOptions,
            Is.EqualTo(BuildOptions.StrictMode));
        Assert.That(GmWendStandaloneBuild.ProfileBuildOptions,
            Is.EqualTo(BuildOptions.StrictMode | BuildOptions.Development));
        Assert.That(GmWendStandaloneBuild.ProfileOutputPath,
            Is.Not.EqualTo(GmWendStandaloneBuild.OutputPath));
        StringAssert.Contains("Profile", GmWendStandaloneBuild.ProfileOutputPath);
    }
}
