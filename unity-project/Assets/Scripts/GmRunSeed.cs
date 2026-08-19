// One seed per run, one independent stream per named consumer, so adding a new randomized system
// later can never shift another system's sequence by drawing from a generator they share.
//
// Resolution order: -gmSeed=<int> launch arg, then GM_SEED env var, then the clock. Logged once so
// a playtest report can name the exact run ("seed 8837 felt too fast").
//
// Streams are derived with a hand-rolled FNV-1a hash rather than string.GetHashCode(), which .NET
// permits to differ across processes/platforms/runtimes -- that would make "same seed, same run"
// a promise this class could not actually keep.
using System;

public static class GmRunSeed
{
    static int? resolved;

    public static int Value
    {
        get
        {
            if (resolved.HasValue) return resolved.Value;
            resolved = Resolve();
            GmExperienceTelemetry.Record("run-seed", resolved.Value.ToString());
            return resolved.Value;
        }
    }

    static int Resolve()
    {
        string[] args = Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (!arg.StartsWith("-gmSeed=", StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(arg.Substring("-gmSeed=".Length), out int fromArg)) return fromArg;
        }
        string env = Environment.GetEnvironmentVariable("GM_SEED");
        if (!string.IsNullOrEmpty(env) && int.TryParse(env, out int fromEnv)) return fromEnv;
        return unchecked((int)DateTime.UtcNow.Ticks);
    }

    /// An independent, deterministic random stream for one named consumer. Two stream names never
    /// draw the same sequence from the same run seed, and the same (seed, name) pair always
    /// produces the same sequence -- the property every seeded-replay test in this system relies on.
    public static System.Random ForStream(string streamName)
    {
        return new System.Random(SeedForStream(streamName));
    }

    public static int SeedForStream(string streamName) =>
        unchecked(Value * 397 + StableHash(streamName ?? ""));

    static int StableHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in s) { hash ^= c; hash *= 16777619; }
            return (int)hash;
        }
    }

    /// Test/review only: force a specific seed regardless of launch args or environment. Mirrors
    /// the review-profile precedent (GmBellSummons.SetReviewProfile) for deterministic gates/tours.
    public static void ForceForReview(int seed) => resolved = seed;

    /// Test only: drop the cached seed so the next access re-resolves from args/env/clock.
    public static void ResetForTests() => resolved = null;
}
