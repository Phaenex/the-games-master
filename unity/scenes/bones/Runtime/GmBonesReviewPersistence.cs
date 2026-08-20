using System;
using System.IO;

/// <summary>Owns an isolated save backend for the direct-review Bones scaffold.</summary>
public static class GmBonesReviewPersistence
{
    static bool active;
    static string activeSavePath = string.Empty;

    public static bool IsActive => active;
    public static string ActiveSavePath => activeSavePath;

    public static string EnsureActive(string purpose)
    {
        if (active) return activeSavePath;
        string safePurpose = string.IsNullOrWhiteSpace(purpose) ? "review" : purpose;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safePurpose = safePurpose.Replace(invalid, '-');
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "bones-review", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        activeSavePath = Path.Combine(directory, safePurpose + "-run-save.json");
        GmSaveSystem.ConfigureForTests(activeSavePath);
        active = true;
        return activeSavePath;
    }

    public static IDisposable BeginScope(string purpose)
    {
        bool ownsConfiguration = !active;
        EnsureActive(purpose);
        return new ReviewScope(ownsConfiguration);
    }

    public static void EndReview()
    {
        if (!active) return;
        GmSaveSystem.ResetTestConfiguration();
        active = false;
        activeSavePath = string.Empty;
    }

    sealed class ReviewScope : IDisposable
    {
        readonly bool ownsConfiguration;
        bool disposed;

        public ReviewScope(bool ownsConfiguration) => this.ownsConfiguration = ownsConfiguration;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ownsConfiguration) EndReview();
        }
    }
}
