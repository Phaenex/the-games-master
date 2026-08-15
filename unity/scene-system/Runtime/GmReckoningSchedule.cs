// Pure schedule math for the ninth bell's contextual cadence -- no MonoBehaviour, no Time.time, so
// it is fully testable without PlayMode. See docs/superpowers/specs/2026-08-13-the-reckoning.md.
//
//   Iᵢ = clamp( baseᵢ × (1 − authority×pressure) × (1 + jitterᵢ),  floorᵢ,  baseᵢ )
//
// The clamp's own ceiling is baseᵢ, so pressure and jitter can only ever pull a toll EARLIER, never
// later -- exploring can hurry the house, never delay it. That is half of the inevitability
// guarantee; the floor is the other half, stopping the count from collapsing into a stopwatch.
using UnityEngine;

public static class GmReckoningSchedule
{
    /// The interval before the given toll. tollIndex is 1-indexed: 1 uses firstTollDelay as its
    /// base, 2..9 use tollInterval. Defends its own bounds even if a caller passes an
    /// already-clamped pressure/jitter, since this is the one place the guarantee has to hold.
    public static float IntervalForToll(int tollIndex, float firstTollDelay, float tollInterval,
        float authority, float pressure01, float jitterFraction, float floorFirst, float floorRest)
    {
        float baseSeconds = tollIndex <= 1 ? firstTollDelay : tollInterval;
        float floorSeconds = tollIndex <= 1 ? floorFirst : floorRest;
        float a = Mathf.Clamp01(authority);
        float p = Mathf.Clamp01(pressure01);
        float scaled = baseSeconds * (1f - a * p);
        float jittered = scaled * (1f + jitterFraction);
        return Mathf.Clamp(jittered, Mathf.Min(floorSeconds, baseSeconds), baseSeconds);
    }

    /// The longest the whole nine-count can ever take, given the two base literals -- pressure and
    /// jitter can only shrink each interval, never grow it past its base. Bound tests check the
    /// real configured values against this, rather than trusting a second, separately-declared pair
    /// of "promise" constants that could silently drift out of sync with the actual bases/floors.
    public static float UpperBoundSeconds(float firstTollDelay, float tollInterval)
        => firstTollDelay + 8f * tollInterval;

    /// The shortest the whole nine-count can ever take, given the two floor literals.
    public static float LowerBoundSeconds(float floorFirst, float floorRest)
        => floorFirst + 8f * floorRest;

    /// One seeded jitter fraction for a specific toll, in [-jitterFraction, +jitterFraction]. Draw
    /// this once per toll boundary from a GmRunSeed.ForStream("toll-jitter") stream -- never inside
    /// a per-frame Update() -- so (seed, ordered toll events) -> exact schedule stays a pure,
    /// replayable function.
    public static float JitterFraction(System.Random rng, float jitterFraction)
    {
        if (rng == null || jitterFraction <= 0f) return 0f;
        double sample = rng.NextDouble() * 2.0 - 1.0; // [-1, 1)
        return (float)(sample * jitterFraction);
    }
}
