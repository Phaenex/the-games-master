// Player-log failures that can leave a build technically running while visible scene content is
// absent or replaced. Keep this policy scene-agnostic so every standalone proof inherits it.
using System;

public static class GmRuntimeIntegrityPolicy
{
    public static readonly string[] RenderFailureFragments =
    {
        "couldn't be instanced",
        "contains no valid mesh renderer",
        "shader is not supported on this gpu",
        "has no shader assigned",
        "fallback shader 'hidden/internalerrorshader'",
    };

    public static bool IsRenderFailure(string condition)
    {
        if (string.IsNullOrEmpty(condition)) return false;
        foreach (string fragment in RenderFailureFragments)
            if (condition.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
