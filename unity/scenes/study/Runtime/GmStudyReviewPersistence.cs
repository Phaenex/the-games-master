using System;
using System.IO;

/// <summary>Owns an isolated save backend for the direct-review Study scaffold.</summary>
public static class GmStudyReviewPersistence
{
    static int activeScopeCount;
    static string activeSavePath = string.Empty;

    public static bool IsActive => activeScopeCount > 0;
    public static string ActiveSavePath => activeSavePath;

    public static IDisposable BeginScope(string purpose)
    {
        string safePurpose = string.IsNullOrWhiteSpace(purpose) ? "review" : purpose;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safePurpose = safePurpose.Replace(invalid, '-');
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Library",
            "GmSceneIntelligence", "study-review", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string previousReviewPath = activeSavePath;
        string savePath = Path.Combine(directory, safePurpose + "-run-save.json");
        IDisposable saveScope = GmSaveSystem.BeginTemporaryConfiguration(savePath);
        activeScopeCount++;
        activeSavePath = savePath;
        return new ReviewScope(saveScope, previousReviewPath);
    }

    sealed class ReviewScope : IDisposable
    {
        readonly IDisposable saveScope;
        readonly string previousReviewPath;
        bool disposed;

        public ReviewScope(IDisposable saveScope, string previousReviewPath)
        {
            this.saveScope = saveScope;
            this.previousReviewPath = previousReviewPath;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            saveScope.Dispose();
            activeScopeCount = Math.Max(0, activeScopeCount - 1);
            activeSavePath = activeScopeCount == 0 ? string.Empty : previousReviewPath;
        }
    }
}
