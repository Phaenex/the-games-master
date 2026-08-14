public enum GmEndingType
{
    TrappedLoop,        // Default fail / wake again on porch
    HostSuccession,     // High compliance, accepted contract
    DefiantSacrifice,   // High defiance, rejected host, burned ledger
    CorruptedHost,      // Corruption Tier reaches 5
    Madness,            // Sanity drops to 0
    TrueEscape          // All 3 shards + 8+ cheats caught
}

public static class GmEndingManager
{
    public static GmEndingType ResolveEnding()
    {
        // Priority 1: Madness Check
        if (GmRunStore.Sanity <= 0.0f)
        {
            return GmEndingType.Madness;
        }

        // Priority 2: Maximum Corruption Check
        if (GmRunStore.CorruptionTier >= 5)
        {
            return GmEndingType.CorruptedHost;
        }

        // Priority 3: True Escape (Canon Golden Ending)
        if (GmRunStore.AllShardsCollected && GmRunStore.CheatsCaughtCount >= 8)
        {
            return GmEndingType.TrueEscape;
        }

        // Priority 4: Defiant Sacrifice
        if (GmRunStore.DefianceCount > GmRunStore.ComplianceCount)
        {
            return GmEndingType.DefiantSacrifice;
        }

        // Priority 5: Host Succession
        if (GmRunStore.ComplianceCount > GmRunStore.DefianceCount)
        {
            return GmEndingType.HostSuccession;
        }

        // Fallback: Trapped in the Loop
        return GmEndingType.TrappedLoop;
    }

    public static string GetEndingTitle(GmEndingType ending)
    {
        switch (ending)
        {
            case GmEndingType.TrueEscape:
                return "Ending A: The Shattered Cycle (True Escape)";
            case GmEndingType.DefiantSacrifice:
                return "Ending B: Ashes of the Host (Defiant Sacrifice)";
            case GmEndingType.HostSuccession:
                return "Ending C: The Tenth Guest (Host Succession)";
            case GmEndingType.CorruptedHost:
                return "Ending D: The Huntsman's Burden (Corrupted Host)";
            case GmEndingType.Madness:
                return "Ending E: Lost in the Wainscoting (Madness)";
            case GmEndingType.TrappedLoop:
            default:
                return "Ending F: The Midnight Chime (Trapped Loop)";
        }
    }
}
