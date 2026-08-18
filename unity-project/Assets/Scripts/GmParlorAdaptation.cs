using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum GmParlorAdaptiveMode
{
    Ordinary,
    Mirror,
    Recollection,
}

public enum GmParlorPackageProvenance
{
    OrdinaryBaseline,
    MirrorDirector,
    RecollectionKnown,
    LegacyMigration,
}

public enum GmParlorHonestStrategyId
{
    MeasuredCourtesy,
    TrumpReserve,
    PreferredSuitReserveFlames,
    PreferredSuitReserveEyes,
    PreferredSuitReserveTeeth,
    PreferredSuitReserveBones,
    CounterConservation,
    SecondDealLow,
    SecondDealHigh,
    SecondDealReserve,
}

public enum GmParlorCounterPlanId
{
    None,
    LegacyBaseline,
    CourtesyFeint,
    EmberLock,
    ConservationLedger,
    StillHands,
    SecondDeal,
}

public enum GmParlorPresentationPlanId
{
    None,
    StillHandsGesture,
    StillHandsCardContact,
}

public enum GmParlorTendency
{
    None,
    ReadHungry,
    AccuratePatient,
    FalseAccuser,
    SuspiciousAcceptor,
    SuitSpecialistFlames,
    SuitSpecialistEyes,
    SuitSpecialistTeeth,
    SuitSpecialistBones,
    PressurePlayer,
    GestureSpecialist,
    CardContactSpecialist,
    Rematcher,
}

public enum GmParlorTellFamilyId
{
    TraditionalCuff,
    PolitePause,
    StillHands,
    GazeAndCardContact,
}

public enum GmParlorEvidenceFamilyId
{
    Gesture,
    CardContact,
    RuleLedger,
}

public enum GmParlorCheatGrammarId
{
    CanonicalThreatOnly,
}

public enum GmParlorMemoryTokenId
{
    None,
    FirstRecognition,
    AshUnderGlass,
    TheReturnedCard,
}

public enum GmParlorCommittedJudgement
{
    Accept,
    Read,
}

[Serializable]
public sealed class GmParlorAdaptivePackage
{
    public const int CurrentSchemaVersion = 2;
    public const int CurrentCatalogVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public int catalogVersion = CurrentCatalogVersion;
    public GmParlorAdaptiveMode mode;
    public GmParlorPackageProvenance provenance;
    public int attentionTier;
    public GmParlorHonestStrategyId honestStrategyId;
    public GmParlorCounterPlanId primaryCounterPlanId;
    public GmParlorPresentationPlanId secondaryPresentationPlanId;
    public GmParlorTendency targetTendency;
    public GmParlorTendency secondaryTendency;
    public GmParlorCheatGrammarId cheatGrammarId;
    public GmParlorTellFamilyId tellFamilyId;
    public GmParlorEvidenceFamilyId evidenceFamilyId;
    public GmParlorMemoryTokenId memoryTokenId;
    public byte[] historyDigest = new byte[32];
    public long selectedFromReceiptOrdinal;
    public string fallbackReason = string.Empty;

    public int SchemaVersion => schemaVersion;
    public int CatalogVersion => catalogVersion;
    public GmParlorAdaptiveMode Mode => mode;
    public int AttentionTier => attentionTier;
    public GmParlorHonestStrategyId HonestStrategyId => honestStrategyId;
    public GmParlorCounterPlanId PrimaryCounterPlanId => primaryCounterPlanId;
    public GmParlorPresentationPlanId SecondaryPresentationPlanId => secondaryPresentationPlanId;
    public GmParlorTendency TargetTendency => targetTendency;
    public GmParlorTendency SecondaryTendency => secondaryTendency;
    public GmParlorTellFamilyId TellFamilyId => tellFamilyId;
    public GmParlorMemoryTokenId MemoryTokenId => memoryTokenId;
    public string FallbackReason => fallbackReason ?? string.Empty;
    public bool CanTeachProfile => mode == GmParlorAdaptiveMode.Mirror &&
        provenance == GmParlorPackageProvenance.MirrorDirector;

    public static GmParlorAdaptivePackage Baseline(GmParlorAdaptiveMode mode,
        int catalogVersion = CurrentCatalogVersion, string reason = "")
    {
        if (mode == GmParlorAdaptiveMode.Recollection)
            throw new ArgumentException(
                "Recollection requires an explicit known Mirror package", nameof(mode));
        if (mode != GmParlorAdaptiveMode.Ordinary && mode != GmParlorAdaptiveMode.Mirror)
            throw new ArgumentOutOfRangeException(nameof(mode));
        return new GmParlorAdaptivePackage
        {
            catalogVersion = catalogVersion,
            mode = mode,
            provenance = mode == GmParlorAdaptiveMode.Ordinary
                ? GmParlorPackageProvenance.OrdinaryBaseline
                : GmParlorPackageProvenance.MirrorDirector,
            attentionTier = 0,
            honestStrategyId = GmParlorHonestStrategyId.MeasuredCourtesy,
            primaryCounterPlanId = GmParlorCounterPlanId.None,
            secondaryPresentationPlanId = GmParlorPresentationPlanId.None,
            targetTendency = GmParlorTendency.None,
            secondaryTendency = GmParlorTendency.None,
            cheatGrammarId = GmParlorCheatGrammarId.CanonicalThreatOnly,
            tellFamilyId = GmParlorTellFamilyId.TraditionalCuff,
            evidenceFamilyId = GmParlorEvidenceFamilyId.CardContact,
            memoryTokenId = GmParlorMemoryTokenId.None,
            historyDigest = new byte[32],
            fallbackReason = reason ?? string.Empty,
        };
    }

    public static GmParlorAdaptivePackage LegacyBaseline()
    {
        GmParlorAdaptivePackage package = Baseline(GmParlorAdaptiveMode.Ordinary);
        package.provenance = GmParlorPackageProvenance.LegacyMigration;
        package.primaryCounterPlanId = GmParlorCounterPlanId.LegacyBaseline;
        package.fallbackReason = "snapshot-v2-no-adaptation";
        return package;
    }

    public GmParlorAdaptivePackage DeepCopy()
    {
        return new GmParlorAdaptivePackage
        {
            schemaVersion = schemaVersion,
            catalogVersion = catalogVersion,
            mode = mode,
            provenance = provenance,
            attentionTier = attentionTier,
            honestStrategyId = honestStrategyId,
            primaryCounterPlanId = primaryCounterPlanId,
            secondaryPresentationPlanId = secondaryPresentationPlanId,
            targetTendency = targetTendency,
            secondaryTendency = secondaryTendency,
            cheatGrammarId = cheatGrammarId,
            tellFamilyId = tellFamilyId,
            evidenceFamilyId = evidenceFamilyId,
            memoryTokenId = memoryTokenId,
            historyDigest = historyDigest == null ? null : (byte[])historyDigest.Clone(),
            selectedFromReceiptOrdinal = selectedFromReceiptOrdinal,
            fallbackReason = fallbackReason,
        };
    }

    public bool TryValidate(out string error)
    {
        if (schemaVersion != CurrentSchemaVersion)
            return Fail($"adaptive package schema {schemaVersion} is unsupported", out error);
        if (catalogVersion <= 0 || catalogVersion > CurrentCatalogVersion)
            return Fail($"adaptive package catalog {catalogVersion} is unsupported", out error);
        if (!Enum.IsDefined(typeof(GmParlorAdaptiveMode), mode) ||
            !Enum.IsDefined(typeof(GmParlorPackageProvenance), provenance) ||
            !Enum.IsDefined(typeof(GmParlorHonestStrategyId), honestStrategyId) ||
            !Enum.IsDefined(typeof(GmParlorCounterPlanId), primaryCounterPlanId) ||
            !Enum.IsDefined(typeof(GmParlorPresentationPlanId), secondaryPresentationPlanId) ||
            !Enum.IsDefined(typeof(GmParlorTendency), targetTendency) ||
            !Enum.IsDefined(typeof(GmParlorTendency), secondaryTendency) ||
            !Enum.IsDefined(typeof(GmParlorCheatGrammarId), cheatGrammarId) ||
            !Enum.IsDefined(typeof(GmParlorTellFamilyId), tellFamilyId) ||
            !Enum.IsDefined(typeof(GmParlorEvidenceFamilyId), evidenceFamilyId) ||
            !Enum.IsDefined(typeof(GmParlorMemoryTokenId), memoryTokenId))
            return Fail("adaptive package contains an invalid catalog id", out error);
        if (attentionTier < 0 || attentionTier > 3)
            return Fail("adaptive package attention tier is outside 0..3", out error);
        if (historyDigest == null || historyDigest.Length != 32)
            return Fail("adaptive package history digest must contain 32 bytes", out error);
        if (selectedFromReceiptOrdinal < 0)
            return Fail("adaptive package receipt ordinal is negative", out error);
        if (fallbackReason == null)
            return Fail("adaptive package fallback reason is null", out error);
        if (primaryCounterPlanId == GmParlorCounterPlanId.None &&
            targetTendency != GmParlorTendency.None)
            return Fail("adaptive package has a target without a primary counter", out error);
        if (!IsPrimaryCatalogCombinationValid(primaryCounterPlanId, targetTendency,
            honestStrategyId))
            return Fail("adaptive package strategy/plan/target combination is not in the catalog",
                out error);
        bool hasSecondary = secondaryPresentationPlanId != GmParlorPresentationPlanId.None;
        if (hasSecondary != (secondaryTendency != GmParlorTendency.None))
            return Fail("adaptive package secondary plan and tendency are not paired", out error);
        if (hasSecondary && (attentionTier != 3 || secondaryTendency == targetTendency ||
            (secondaryPresentationPlanId == GmParlorPresentationPlanId.StillHandsGesture &&
                secondaryTendency != GmParlorTendency.GestureSpecialist) ||
            (secondaryPresentationPlanId == GmParlorPresentationPlanId.StillHandsCardContact &&
                secondaryTendency != GmParlorTendency.CardContactSpecialist)))
            return Fail("adaptive package secondary presentation combination is not in the catalog",
                out error);
        GmParlorTellFamilyId expectedTell = ExpectedPrimaryTell(primaryCounterPlanId);
        if (tellFamilyId != (hasSecondary
            ? GmParlorTellFamilyId.GazeAndCardContact : expectedTell))
            return Fail("adaptive package tell family does not match its catalog plans", out error);
        bool isLearnedCounter = primaryCounterPlanId != GmParlorCounterPlanId.None &&
            primaryCounterPlanId != GmParlorCounterPlanId.LegacyBaseline;
        if (isLearnedCounter && attentionTier == 0)
            return Fail("adaptive counter exists without an attention tier", out error);
        if (isLearnedCounter && (selectedFromReceiptOrdinal <= 0 ||
            historyDigest.All(value => value == 0)))
            return Fail("adaptive counter exists without a completed-history binding", out error);
        if (attentionTier < 2 && memoryTokenId != GmParlorMemoryTokenId.None)
            return Fail("adaptive memory token exists below attention tier two", out error);
        if (attentionTier >= 2 && isLearnedCounter &&
            memoryTokenId != ExpectedMemory(targetTendency))
            return Fail("adaptive memory token does not match its target", out error);
        if (cheatGrammarId != GmParlorCheatGrammarId.CanonicalThreatOnly ||
            evidenceFamilyId != GmParlorEvidenceFamilyId.CardContact)
            return Fail("adaptive package changes a frozen cheat or hard-proof family", out error);
        if (!TryValidateModeAndProvenance(out error)) return false;
        error = string.Empty;
        return true;
    }

    bool TryValidateModeAndProvenance(out string error)
    {
        bool hasHistory = historyDigest.Any(value => value != 0);
        if (provenance == GmParlorPackageProvenance.OrdinaryBaseline)
        {
            if (mode != GmParlorAdaptiveMode.Ordinary || hasHistory ||
                !HasExactBaselineShape() || fallbackReason.Length != 0)
                return Fail("Ordinary package is not the exact clean baseline", out error);
            error = string.Empty;
            return true;
        }
        if (provenance == GmParlorPackageProvenance.LegacyMigration)
        {
            if (mode != GmParlorAdaptiveMode.Ordinary || hasHistory || attentionTier != 0 ||
                honestStrategyId != GmParlorHonestStrategyId.MeasuredCourtesy ||
                primaryCounterPlanId != GmParlorCounterPlanId.LegacyBaseline ||
                secondaryPresentationPlanId != GmParlorPresentationPlanId.None ||
                targetTendency != GmParlorTendency.None ||
                secondaryTendency != GmParlorTendency.None ||
                tellFamilyId != GmParlorTellFamilyId.TraditionalCuff ||
                memoryTokenId != GmParlorMemoryTokenId.None ||
                selectedFromReceiptOrdinal != 0 ||
                fallbackReason != "snapshot-v2-no-adaptation")
                return Fail("LegacyBaseline package is not the exact named migration", out error);
            error = string.Empty;
            return true;
        }
        if (provenance == GmParlorPackageProvenance.MirrorDirector)
        {
            if (mode != GmParlorAdaptiveMode.Mirror ||
                primaryCounterPlanId == GmParlorCounterPlanId.LegacyBaseline)
                return Fail("Mirror package has invalid mode or legacy provenance", out error);
            if (!hasHistory)
            {
                if (!HasExactBaselineShape() || fallbackReason.Length != 0)
                    return Fail("empty-history Mirror package is not the exact baseline", out error);
            }
            else
            {
                if (attentionTier < 1 || selectedFromReceiptOrdinal <= 0)
                    return Fail("Mirror history lacks its tier or receipt ordinal", out error);
                if (primaryCounterPlanId == GmParlorCounterPlanId.None)
                {
                    if (fallbackReason != "no-eligible-tendency-after-cooldown" ||
                        memoryTokenId != GmParlorMemoryTokenId.None ||
                        secondaryPresentationPlanId != GmParlorPresentationPlanId.None)
                        return Fail("untargeted Mirror package has an invalid fallback shape",
                            out error);
                }
                else if (fallbackReason.Length != 0)
                    return Fail("targeted Mirror package carries a fallback reason", out error);
            }
            error = string.Empty;
            return true;
        }
        if (provenance == GmParlorPackageProvenance.RecollectionKnown)
        {
            if (mode != GmParlorAdaptiveMode.Recollection)
                return Fail("known Recollection package has the wrong mode", out error);
            GmParlorAdaptivePackage mirrorSource = DeepCopy();
            mirrorSource.mode = GmParlorAdaptiveMode.Mirror;
            mirrorSource.provenance = GmParlorPackageProvenance.MirrorDirector;
            if (!mirrorSource.TryValidate(out string sourceError))
                return Fail("Recollection package is not a valid known Mirror package: " +
                    sourceError, out error);
            error = string.Empty;
            return true;
        }
        return Fail("adaptive package provenance is unsupported", out error);
    }

    bool HasExactBaselineShape()
    {
        return attentionTier == 0 &&
            honestStrategyId == GmParlorHonestStrategyId.MeasuredCourtesy &&
            primaryCounterPlanId == GmParlorCounterPlanId.None &&
            secondaryPresentationPlanId == GmParlorPresentationPlanId.None &&
            targetTendency == GmParlorTendency.None &&
            secondaryTendency == GmParlorTendency.None &&
            tellFamilyId == GmParlorTellFamilyId.TraditionalCuff &&
            memoryTokenId == GmParlorMemoryTokenId.None &&
            selectedFromReceiptOrdinal == 0;
    }

    public byte[] ToCanonicalBytes()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(schemaVersion);
            writer.Write(catalogVersion);
            writer.Write((int)mode);
            writer.Write((int)provenance);
            writer.Write(attentionTier);
            writer.Write((int)honestStrategyId);
            writer.Write((int)primaryCounterPlanId);
            writer.Write((int)secondaryPresentationPlanId);
            writer.Write((int)targetTendency);
            writer.Write((int)secondaryTendency);
            writer.Write((int)cheatGrammarId);
            writer.Write((int)tellFamilyId);
            writer.Write((int)evidenceFamilyId);
            writer.Write((int)memoryTokenId);
            writer.Write(historyDigest?.Length ?? -1);
            if (historyDigest != null) writer.Write(historyDigest);
            writer.Write(selectedFromReceiptOrdinal);
            WriteString(writer, fallbackReason);
            return stream.ToArray();
        }
    }

    public string DiagnosticCode => Convert.ToBase64String(ToCanonicalBytes());

    public byte[] CanonicalHash
    {
        get
        {
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(ToCanonicalBytes());
        }
    }

    public static bool IsPrimaryCatalogCombinationValid(GmParlorCounterPlanId plan,
        GmParlorTendency target, GmParlorHonestStrategyId strategy)
    {
        if (plan == GmParlorCounterPlanId.None ||
            plan == GmParlorCounterPlanId.LegacyBaseline)
            return target == GmParlorTendency.None &&
                strategy == GmParlorHonestStrategyId.MeasuredCourtesy;
        if (plan == GmParlorCounterPlanId.CourtesyFeint)
            return (target == GmParlorTendency.ReadHungry ||
                target == GmParlorTendency.FalseAccuser) &&
                strategy == GmParlorHonestStrategyId.TrumpReserve;
        if (plan == GmParlorCounterPlanId.StillHands)
            return (target == GmParlorTendency.AccuratePatient ||
                target == GmParlorTendency.SuspiciousAcceptor) &&
                strategy == GmParlorHonestStrategyId.MeasuredCourtesy;
        if (plan == GmParlorCounterPlanId.EmberLock)
        {
            if (!TrySuitCatalog(target, out _, out GmParlorHonestStrategyId expected))
                return false;
            return strategy == expected;
        }
        if (plan == GmParlorCounterPlanId.ConservationLedger)
            return target == GmParlorTendency.PressurePlayer &&
                strategy == GmParlorHonestStrategyId.CounterConservation;
        if (plan == GmParlorCounterPlanId.SecondDeal)
            return target == GmParlorTendency.Rematcher &&
                (strategy == GmParlorHonestStrategyId.SecondDealLow ||
                 strategy == GmParlorHonestStrategyId.SecondDealHigh ||
                 strategy == GmParlorHonestStrategyId.SecondDealReserve);
        return false;
    }

    static GmParlorTellFamilyId ExpectedPrimaryTell(GmParlorCounterPlanId plan)
    {
        if (plan == GmParlorCounterPlanId.CourtesyFeint)
            return GmParlorTellFamilyId.PolitePause;
        if (plan == GmParlorCounterPlanId.StillHands)
            return GmParlorTellFamilyId.StillHands;
        if (plan == GmParlorCounterPlanId.ConservationLedger)
            return GmParlorTellFamilyId.GazeAndCardContact;
        return GmParlorTellFamilyId.TraditionalCuff;
    }

    static bool TrySuitCatalog(GmParlorTendency target, out GmSuit suit,
        out GmParlorHonestStrategyId strategy)
    {
        switch (target)
        {
            case GmParlorTendency.SuitSpecialistFlames:
                suit = GmSuit.Flames;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveFlames;
                return true;
            case GmParlorTendency.SuitSpecialistEyes:
                suit = GmSuit.Eyes;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveEyes;
                return true;
            case GmParlorTendency.SuitSpecialistBones:
                suit = GmSuit.Bones;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveBones;
                return true;
            case GmParlorTendency.SuitSpecialistTeeth:
                suit = GmSuit.Teeth;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveTeeth;
                return true;
            default:
                suit = default;
                strategy = default;
                return false;
        }
    }

    static GmParlorMemoryTokenId ExpectedMemory(GmParlorTendency tendency)
    {
        if (tendency >= GmParlorTendency.SuitSpecialistFlames &&
            tendency <= GmParlorTendency.SuitSpecialistBones)
            return GmParlorMemoryTokenId.AshUnderGlass;
        if (tendency == GmParlorTendency.PressurePlayer)
            return GmParlorMemoryTokenId.TheReturnedCard;
        return GmParlorMemoryTokenId.FirstRecognition;
    }

    static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}

[Serializable]
public sealed class GmParlorCompletedMatchSummary
{
    public const int CurrentVersion = 2;
    public const int MaxFeatureCount = 10000;
    public const int MaxCompletedRematches = MaxFeatureCount - 1;
    public int version = CurrentVersion;
    public int judgementOpportunities;
    public int readAttempts;
    public int correctReads;
    public int falseReads;
    public int suspiciousObservations;
    public int acceptedSuspicious;
    public int acceptedCalm;
    public int firstReadOpportunityOrdinal;
    public int readFirstThird;
    public int readMiddleThird;
    public int readFinalThird;
    public int firstThirdOpportunityCapacity;
    public int middleThirdOpportunityCapacity;
    public int finalThirdOpportunityCapacity;
    public int firstReadMatchStartOrdinal;
    public int firstReadMatchJudgementOpportunities;
    public int firstReadMatchReadFirstThird;
    public int firstReadMatchReadMiddleThird;
    public int firstReadMatchReadFinalThird;
    public int[] judgementOpportunitiesByMatch = Array.Empty<int>();
    public int[] playerLeadCountBySuit = new int[4];
    public int[] playerPlayedCountBySuit = new int[4];
    public int pressureLeads;
    public int earlyHighRankSpends;
    public int completedRematches;
    public int[] committedEvidenceClaimsByFamily = new int[3];
    public int matchOrdinal;
    public GmParlorCounterPlanId packageId;
    public GmParlorHonestStrategyId honestStrategyId;
    public GmParlorTendency packageTargetTendency;
    public byte[] packageHash = new byte[32];

    public int JudgementOpportunities => judgementOpportunities;
    public int ReadAttempts => readAttempts;
    public int CorrectReads => correctReads;
    public int FalseReads => falseReads;
    public int SuspiciousObservations => suspiciousObservations;
    public int AcceptedSuspicious => acceptedSuspicious;
    public int AcceptedCalm => acceptedCalm;
    public int PressureLeads => pressureLeads;
    public int EarlyHighRankSpends => earlyHighRankSpends;
    public int[] PlayerLeadCountBySuit => (int[])playerLeadCountBySuit.Clone();
    public int[] PlayerPlayedCountBySuit => (int[])playerPlayedCountBySuit.Clone();
    public int[] CommittedEvidenceClaimsByFamily =>
        (int[])committedEvidenceClaimsByFamily.Clone();

    public GmParlorCompletedMatchSummary DeepCopy()
    {
        return new GmParlorCompletedMatchSummary
        {
            version = version,
            judgementOpportunities = judgementOpportunities,
            readAttempts = readAttempts,
            correctReads = correctReads,
            falseReads = falseReads,
            suspiciousObservations = suspiciousObservations,
            acceptedSuspicious = acceptedSuspicious,
            acceptedCalm = acceptedCalm,
            firstReadOpportunityOrdinal = firstReadOpportunityOrdinal,
            readFirstThird = readFirstThird,
            readMiddleThird = readMiddleThird,
            readFinalThird = readFinalThird,
            firstThirdOpportunityCapacity = firstThirdOpportunityCapacity,
            middleThirdOpportunityCapacity = middleThirdOpportunityCapacity,
            finalThirdOpportunityCapacity = finalThirdOpportunityCapacity,
            firstReadMatchStartOrdinal = firstReadMatchStartOrdinal,
            firstReadMatchJudgementOpportunities = firstReadMatchJudgementOpportunities,
            firstReadMatchReadFirstThird = firstReadMatchReadFirstThird,
            firstReadMatchReadMiddleThird = firstReadMatchReadMiddleThird,
            firstReadMatchReadFinalThird = firstReadMatchReadFinalThird,
            judgementOpportunitiesByMatch = judgementOpportunitiesByMatch == null
                ? null : (int[])judgementOpportunitiesByMatch.Clone(),
            playerLeadCountBySuit = playerLeadCountBySuit == null
                ? null : (int[])playerLeadCountBySuit.Clone(),
            playerPlayedCountBySuit = playerPlayedCountBySuit == null
                ? null : (int[])playerPlayedCountBySuit.Clone(),
            pressureLeads = pressureLeads,
            earlyHighRankSpends = earlyHighRankSpends,
            completedRematches = completedRematches,
            committedEvidenceClaimsByFamily = committedEvidenceClaimsByFamily == null
                ? null : (int[])committedEvidenceClaimsByFamily.Clone(),
            matchOrdinal = matchOrdinal,
            packageId = packageId,
            honestStrategyId = honestStrategyId,
            packageTargetTendency = packageTargetTendency,
            packageHash = packageHash == null ? null : (byte[])packageHash.Clone(),
        };
    }

    public bool TryValidate(out string error)
    {
        if (version != CurrentVersion)
            return Fail($"completed match summary version {version} is unsupported", out error);
        if (playerLeadCountBySuit == null || playerLeadCountBySuit.Length != 4 ||
            playerPlayedCountBySuit == null || playerPlayedCountBySuit.Length != 4 ||
            committedEvidenceClaimsByFamily == null ||
            committedEvidenceClaimsByFamily.Length != 3 ||
            judgementOpportunitiesByMatch == null ||
            judgementOpportunitiesByMatch.Length == 0 || packageHash == null ||
            packageHash.Length != 32)
            return Fail("completed match summary arrays have invalid lengths", out error);
        if (packageHash.All(value => value == 0))
            return Fail("completed match summary package hash is empty", out error);
        int[] scalars = { judgementOpportunities, readAttempts, correctReads, falseReads,
            suspiciousObservations, acceptedSuspicious, acceptedCalm,
            firstReadOpportunityOrdinal, readFirstThird, readMiddleThird, readFinalThird,
            firstThirdOpportunityCapacity, middleThirdOpportunityCapacity,
            finalThirdOpportunityCapacity, firstReadMatchStartOrdinal,
            firstReadMatchJudgementOpportunities,
            firstReadMatchReadFirstThird, firstReadMatchReadMiddleThird,
            firstReadMatchReadFinalThird,
            pressureLeads, earlyHighRankSpends, completedRematches, matchOrdinal };
        if (scalars.Any(value => value < 0) || playerLeadCountBySuit.Any(value => value < 0) ||
            playerPlayedCountBySuit.Any(value => value < 0) ||
            committedEvidenceClaimsByFamily.Any(value => value < 0))
            return Fail("completed match summary contains a negative count", out error);
        if (judgementOpportunitiesByMatch.Any(value => value < 0 ||
            value > MaxFeatureCount))
            return Fail("completed match summary match opportunity vector is invalid", out error);
        if (scalars.Any(value => value > MaxFeatureCount) ||
            playerLeadCountBySuit.Any(value => value > MaxFeatureCount) ||
            playerPlayedCountBySuit.Any(value => value > MaxFeatureCount) ||
            committedEvidenceClaimsByFamily.Any(value => value > MaxFeatureCount))
            return Fail("completed match summary exceeds its bounded feature counts", out error);
        if (matchOrdinal < 1 || completedRematches > MaxCompletedRematches ||
            completedRematches > matchOrdinal - 1)
            return Fail("completed match summary rematch identity is invalid", out error);
        if (judgementOpportunitiesByMatch.Length != completedRematches + 1 ||
            judgementOpportunitiesByMatch.Sum() != judgementOpportunities)
            return Fail("completed match summary opportunity vector contradicts rematches",
                out error);
        if (correctReads + falseReads != readAttempts)
            return Fail("correct plus false Reads must equal Read attempts", out error);
        if (readAttempts + acceptedSuspicious + acceptedCalm != judgementOpportunities)
            return Fail("Read and accept counts must equal judgement opportunities", out error);
        if (acceptedSuspicious > suspiciousObservations)
            return Fail("accepted suspicious count exceeds suspicious observations", out error);
        if (suspiciousObservations > judgementOpportunities ||
            acceptedCalm > judgementOpportunities - suspiciousObservations)
            return Fail("observation counts exceed public judgement opportunities", out error);
        if (readFirstThird + readMiddleThird + readFinalThird != readAttempts)
            return Fail("Read timing thirds must equal Read attempts", out error);
        int derivedFirstCapacity = judgementOpportunitiesByMatch.Sum(value => value / 3);
        int derivedFinalCapacity = judgementOpportunitiesByMatch.Sum(value =>
            value - value * 2 / 3);
        int derivedMiddleCapacity = judgementOpportunities - derivedFirstCapacity -
            derivedFinalCapacity;
        if (firstThirdOpportunityCapacity != derivedFirstCapacity ||
            middleThirdOpportunityCapacity != derivedMiddleCapacity ||
            finalThirdOpportunityCapacity != derivedFinalCapacity ||
            readFirstThird > firstThirdOpportunityCapacity ||
            readMiddleThird > middleThirdOpportunityCapacity ||
            readFinalThird > finalThirdOpportunityCapacity)
            return Fail("Read timing exceeds physically available opportunity ordinals",
                out error);
        int leads = playerLeadCountBySuit.Sum();
        if (pressureLeads > leads)
            return Fail("pressure leads exceed player leads", out error);
        if (earlyHighRankSpends > pressureLeads)
            return Fail("early high-rank spends exceed public forcing leads", out error);
        for (int suit = 0; suit < 4; suit++)
            if (playerLeadCountBySuit[suit] > playerPlayedCountBySuit[suit])
                return Fail("player suit leads exceed public suit plays", out error);
        if (committedEvidenceClaimsByFamily.Sum() > readAttempts)
            return Fail("committed evidence claims exceed Read attempts", out error);
        if (readAttempts == 0 && (firstReadOpportunityOrdinal != 0 ||
            firstReadMatchStartOrdinal != 0 || firstReadMatchJudgementOpportunities != 0 ||
            firstReadMatchReadFirstThird != 0 || firstReadMatchReadMiddleThird != 0 ||
            firstReadMatchReadFinalThird != 0))
            return Fail("first Read metadata exists without a Read", out error);
        if (readAttempts > 0 && (firstReadOpportunityOrdinal <= 0 ||
            firstReadOpportunityOrdinal > judgementOpportunities))
            return Fail("first Read ordinal is outside judgement opportunities", out error);
        if (readAttempts > 0)
        {
            if (firstReadMatchJudgementOpportunities <= 0 ||
                firstReadMatchStartOrdinal < 0 ||
                firstReadMatchStartOrdinal + firstReadMatchJudgementOpportunities >
                    judgementOpportunities ||
                firstReadOpportunityOrdinal <= firstReadMatchStartOrdinal ||
                firstReadOpportunityOrdinal > firstReadMatchStartOrdinal +
                    firstReadMatchJudgementOpportunities)
                return Fail("first Read match segment is invalid", out error);
            int segmentStart = 0;
            int firstReadMatchIndex = -1;
            for (int match = 0; match < judgementOpportunitiesByMatch.Length; match++)
            {
                int matchOpportunities = judgementOpportunitiesByMatch[match];
                if (segmentStart == firstReadMatchStartOrdinal &&
                    matchOpportunities == firstReadMatchJudgementOpportunities)
                {
                    firstReadMatchIndex = match;
                    break;
                }
                segmentStart += matchOpportunities;
            }
            if (firstReadMatchIndex < 0)
                return Fail("first Read match segment is absent from the opportunity vector",
                    out error);
            int localOrdinal = firstReadOpportunityOrdinal - firstReadMatchStartOrdinal;
            int firstMatchFirstCapacity = firstReadMatchJudgementOpportunities / 3;
            int firstMatchFinalStart = firstReadMatchJudgementOpportunities * 2 / 3 + 1;
            int firstMatchMiddleEnd = firstMatchFinalStart - 1;
            int firstMatchReadCount = firstReadMatchReadFirstThird +
                firstReadMatchReadMiddleThird + firstReadMatchReadFinalThird;
            if (firstMatchReadCount <= 0 || firstMatchReadCount > readAttempts ||
                firstReadMatchReadFirstThird > readFirstThird ||
                firstReadMatchReadMiddleThird > readMiddleThird ||
                firstReadMatchReadFinalThird > readFinalThird ||
                firstReadMatchReadFirstThird > firstMatchFirstCapacity ||
                firstReadMatchReadMiddleThird >
                    firstMatchMiddleEnd - firstMatchFirstCapacity ||
                firstReadMatchReadFinalThird >
                    firstReadMatchJudgementOpportunities - firstMatchFinalStart + 1)
                return Fail("first Read match timing counts are invalid", out error);
            bool firstBucket = localOrdinal <= firstMatchFirstCapacity;
            bool finalBucket = localOrdinal >= firstMatchFinalStart;
            int availableFirst = 0;
            int availableMiddle = 0;
            int availableFinal = 0;
            for (int match = firstReadMatchIndex + 1;
                match < judgementOpportunitiesByMatch.Length; match++)
            {
                int opportunities = judgementOpportunitiesByMatch[match];
                int laterFirst = opportunities / 3;
                int laterFinal = opportunities - opportunities * 2 / 3;
                availableFirst += laterFirst;
                availableMiddle += opportunities - laterFirst - laterFinal;
                availableFinal += laterFinal;
            }
            int laterFirstCapacity = availableFirst;
            int laterMiddleCapacity = availableMiddle;
            int laterFinalCapacity = availableFinal;
            if (firstBucket)
            {
                availableFirst += firstMatchFirstCapacity - localOrdinal + 1;
                availableMiddle += firstMatchMiddleEnd - firstMatchFirstCapacity;
                availableFinal += firstReadMatchJudgementOpportunities -
                    firstMatchFinalStart + 1;
                if (firstReadMatchReadFirstThird == 0 ||
                    firstReadMatchReadFirstThird > firstMatchFirstCapacity - localOrdinal + 1)
                    return Fail("first Read ordinal contradicts first-third timing", out error);
            }
            else if (!finalBucket)
            {
                availableMiddle += firstMatchMiddleEnd - localOrdinal + 1;
                availableFinal += firstReadMatchJudgementOpportunities -
                    firstMatchFinalStart + 1;
                if (firstReadMatchReadFirstThird != 0 ||
                    firstReadMatchReadMiddleThird == 0 ||
                    firstReadMatchReadMiddleThird > firstMatchMiddleEnd - localOrdinal + 1)
                    return Fail("first Read ordinal contradicts middle-third timing", out error);
            }
            else
            {
                availableFinal += firstReadMatchJudgementOpportunities - localOrdinal + 1;
                if (firstReadMatchReadFirstThird != 0 ||
                    firstReadMatchReadMiddleThird != 0 ||
                    firstReadMatchReadFinalThird == 0 ||
                    firstReadMatchReadFinalThird >
                        firstReadMatchJudgementOpportunities - localOrdinal + 1)
                    return Fail("first Read ordinal contradicts final-third timing", out error);
            }
            if (readFirstThird > availableFirst || readMiddleThird > availableMiddle ||
                readFinalThird > availableFinal)
                return Fail("Read timing cannot occur after the claimed first Read", out error);
            if (readFirstThird - firstReadMatchReadFirstThird > laterFirstCapacity ||
                readMiddleThird - firstReadMatchReadMiddleThird > laterMiddleCapacity ||
                readFinalThird - firstReadMatchReadFinalThird > laterFinalCapacity)
                return Fail("Read timing residual exceeds later rematch capacity", out error);
        }
        if (!Enum.IsDefined(typeof(GmParlorCounterPlanId), packageId) ||
            !Enum.IsDefined(typeof(GmParlorHonestStrategyId), honestStrategyId) ||
            !Enum.IsDefined(typeof(GmParlorTendency), packageTargetTendency))
            return Fail("completed match summary package ids are invalid", out error);
        if (!GmParlorAdaptivePackage.IsPrimaryCatalogCombinationValid(packageId,
            packageTargetTendency, honestStrategyId))
            return Fail("completed match summary package ids are semantically impossible",
                out error);
        error = string.Empty;
        return true;
    }

    public byte[] ToCanonicalBytes()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(version);
            writer.Write(judgementOpportunities);
            writer.Write(readAttempts);
            writer.Write(correctReads);
            writer.Write(falseReads);
            writer.Write(suspiciousObservations);
            writer.Write(acceptedSuspicious);
            writer.Write(acceptedCalm);
            writer.Write(firstReadOpportunityOrdinal);
            writer.Write(readFirstThird);
            writer.Write(readMiddleThird);
            writer.Write(readFinalThird);
            writer.Write(firstThirdOpportunityCapacity);
            writer.Write(middleThirdOpportunityCapacity);
            writer.Write(finalThirdOpportunityCapacity);
            writer.Write(firstReadMatchStartOrdinal);
            writer.Write(firstReadMatchJudgementOpportunities);
            writer.Write(firstReadMatchReadFirstThird);
            writer.Write(firstReadMatchReadMiddleThird);
            writer.Write(firstReadMatchReadFinalThird);
            WriteArray(writer, judgementOpportunitiesByMatch);
            WriteArray(writer, playerLeadCountBySuit);
            WriteArray(writer, playerPlayedCountBySuit);
            writer.Write(pressureLeads);
            writer.Write(earlyHighRankSpends);
            writer.Write(completedRematches);
            WriteArray(writer, committedEvidenceClaimsByFamily);
            writer.Write(matchOrdinal);
            writer.Write((int)packageId);
            writer.Write((int)honestStrategyId);
            writer.Write((int)packageTargetTendency);
            writer.Write(packageHash.Length);
            writer.Write(packageHash);
            return stream.ToArray();
        }
    }

    static void WriteArray(BinaryWriter writer, int[] values)
    {
        writer.Write(values?.Length ?? -1);
        if (values == null) return;
        for (int index = 0; index < values.Length; index++) writer.Write(values[index]);
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}

[Serializable]
public sealed class GmParlorBehaviorAccumulator
{
    public const int CurrentVersion = 2;
    public const int MaxStoredMatchSummaries = 32;
    public const ulong MaxEventSequence = long.MaxValue;
    public int version = CurrentVersion;
    public byte[] boundPackageHash = new byte[32];
    public ulong eventSequence;
    public int judgementOpportunities;
    public int readAttempts;
    public int correctReads;
    public int falseReads;
    public int suspiciousObservations;
    public int acceptedSuspicious;
    public int acceptedCalm;
    public int firstReadOpportunityOrdinal;
    public List<int> readOpportunityOrdinals = new List<int>();
    public int[] playerLeadCountBySuit = new int[4];
    public int[] playerPlayedCountBySuit = new int[4];
    public int pressureLeads;
    public int earlyHighRankSpends;
    public int[] committedEvidenceClaimsByFamily = new int[3];
    public int matchOrdinal = 1;
    public bool currentMatchSealed;
    public GmParlorCompletedMatchSummary sealedSummary;
    public List<GmParlorCompletedMatchSummary> completedMatchSummaries =
        new List<GmParlorCompletedMatchSummary>();

    public ulong EventSequence => eventSequence;

    public GmParlorBehaviorAccumulator()
    {
    }

    public GmParlorBehaviorAccumulator(GmParlorAdaptivePackage package)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));
        if (!package.TryValidate(out string error))
            throw new ArgumentException("cannot bind invalid package: " + error,
                nameof(package));
        boundPackageHash = package.CanonicalHash;
    }

    public void RecordPlayerLead(GmCard card, int roundTrickOrdinal,
        int roundTrickCapacity)
    {
        EnsureRecordable();
        ValidateCard(card);
        if (roundTrickOrdinal < 1 || roundTrickCapacity < 1 ||
            roundTrickOrdinal > roundTrickCapacity)
            throw new ArgumentOutOfRangeException(nameof(roundTrickOrdinal));
        EnsureCountCapacity(playerLeadCountBySuit[(int)card.Suit],
            playerPlayedCountBySuit[(int)card.Suit]);
        if (card.Rank >= 6) EnsureCountCapacity(pressureLeads);
        if (card.Rank >= 6 && roundTrickOrdinal <= roundTrickCapacity / 2)
            EnsureCountCapacity(earlyHighRankSpends);
        playerLeadCountBySuit[(int)card.Suit]++;
        playerPlayedCountBySuit[(int)card.Suit]++;
        // A durable behavior feature must be reproducible from the player's public action alone.
        // Rank six or seven is the authored forcing-lead signal; Aldric's unseen hand is irrelevant.
        if (card.Rank >= 6) pressureLeads++;
        if (card.Rank >= 6 && roundTrickOrdinal <= roundTrickCapacity / 2)
            earlyHighRankSpends++;
        AdvanceEventSequence();
    }

    public void RecordPlayerFollow(GmCard card)
    {
        EnsureRecordable();
        ValidateCard(card);
        EnsureCountCapacity(playerPlayedCountBySuit[(int)card.Suit]);
        playerPlayedCountBySuit[(int)card.Suit]++;
        AdvanceEventSequence();
    }

    public void RecordAccept(GmTellObservation observation)
    {
        EnsureRecordable();
        EnsureCountCapacity(judgementOpportunities,
            observation == GmTellObservation.Suspicious ? acceptedSuspicious : acceptedCalm,
            suspiciousObservations);
        RecordPublicObservation(observation);
        if (observation == GmTellObservation.Suspicious) acceptedSuspicious++;
        else acceptedCalm++;
        AdvanceEventSequence();
    }

    public void RecordRead(GmTellObservation observation, GmParlorOutcomeKind publicOutcome,
        int judgementOrdinal = 0)
    {
        EnsureRecordable();
        if (publicOutcome != GmParlorOutcomeKind.CheatCaught &&
            publicOutcome != GmParlorOutcomeKind.FalseReadPenalty)
            throw new ArgumentException("Read requires a resolved public Read outcome",
                nameof(publicOutcome));
        EnsureCountCapacity(judgementOpportunities, readAttempts,
            publicOutcome == GmParlorOutcomeKind.CheatCaught ? correctReads : falseReads,
            suspiciousObservations);
        int ordinal = RecordPublicObservation(observation, judgementOrdinal);
        readAttempts++;
        readOpportunityOrdinals.Add(ordinal);
        if (firstReadOpportunityOrdinal == 0) firstReadOpportunityOrdinal = ordinal;
        if (publicOutcome == GmParlorOutcomeKind.CheatCaught) correctReads++;
        else falseReads++;
        AdvanceEventSequence();
    }

    int RecordPublicObservation(GmTellObservation observation, int judgementOrdinal = 0)
    {
        if (currentMatchSealed) throw new InvalidOperationException("current match is sealed");
        if (!Enum.IsDefined(typeof(GmTellObservation), observation))
            throw new ArgumentOutOfRangeException(nameof(observation));
        int nextOrdinal = checked(judgementOpportunities + 1);
        int ordinal = judgementOrdinal > 0 ? judgementOrdinal : nextOrdinal;
        if (ordinal != nextOrdinal)
            throw new ArgumentOutOfRangeException(nameof(judgementOrdinal));
        judgementOpportunities = nextOrdinal;
        if (observation == GmTellObservation.Suspicious) suspiciousObservations++;
        return ordinal;
    }

    public void RecordCommittedEvidenceClaim(GmParlorEvidenceFamilyId family)
    {
        EnsureRecordable();
        if (!Enum.IsDefined(typeof(GmParlorEvidenceFamilyId), family))
            throw new ArgumentOutOfRangeException(nameof(family));
        if (committedEvidenceClaimsByFamily.Sum() >= readAttempts)
            throw new InvalidOperationException(
                "a committed evidence claim requires an unclaimed public Read");
        EnsureCountCapacity(committedEvidenceClaimsByFamily[(int)family]);
        committedEvidenceClaimsByFamily[(int)family]++;
        AdvanceEventSequence();
    }

    public GmParlorCompletedMatchSummary SealCompletedMatch(
        GmParlorAdaptivePackage package, int matchOrdinal, int completedRematches)
    {
        if (currentMatchSealed) return sealedSummary.DeepCopy();
        EnsureEventCapacity();
        if (!TryValidate(out string accumulatorError))
            throw new InvalidOperationException(
                "cannot seal invalid behavior accumulator: " + accumulatorError);
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (!package.TryValidate(out string packageError))
            throw new ArgumentException("cannot seal with invalid package: " + packageError,
                nameof(package));
        if (boundPackageHash == null ||
            !boundPackageHash.SequenceEqual(package.CanonicalHash))
            throw new InvalidOperationException(
                "cannot seal behavior under a different adaptive package");
        int first = 0;
        int middle = 0;
        int final = 0;
        for (int index = 0; index < readOpportunityOrdinals.Count; index++)
        {
            int ordinal = readOpportunityOrdinals[index];
            if (ordinal * 3 <= judgementOpportunities) first++;
            else if (ordinal * 3 > judgementOpportunities * 2) final++;
            else middle++;
        }
        int firstCapacity = judgementOpportunities / 3;
        int finalCapacity = judgementOpportunities - (judgementOpportunities * 2 / 3);
        int middleCapacity = judgementOpportunities - firstCapacity - finalCapacity;
        var candidate = new GmParlorCompletedMatchSummary
        {
            judgementOpportunities = judgementOpportunities,
            readAttempts = readAttempts,
            correctReads = correctReads,
            falseReads = falseReads,
            suspiciousObservations = suspiciousObservations,
            acceptedSuspicious = acceptedSuspicious,
            acceptedCalm = acceptedCalm,
            firstReadOpportunityOrdinal = firstReadOpportunityOrdinal,
            readFirstThird = first,
            readMiddleThird = middle,
            readFinalThird = final,
            firstThirdOpportunityCapacity = firstCapacity,
            middleThirdOpportunityCapacity = middleCapacity,
            finalThirdOpportunityCapacity = finalCapacity,
            firstReadMatchStartOrdinal = readAttempts > 0 ? 0 : 0,
            firstReadMatchJudgementOpportunities = readAttempts > 0
                ? judgementOpportunities : 0,
            firstReadMatchReadFirstThird = readAttempts > 0 ? first : 0,
            firstReadMatchReadMiddleThird = readAttempts > 0 ? middle : 0,
            firstReadMatchReadFinalThird = readAttempts > 0 ? final : 0,
            judgementOpportunitiesByMatch = new[] { judgementOpportunities },
            playerLeadCountBySuit = (int[])playerLeadCountBySuit.Clone(),
            playerPlayedCountBySuit = (int[])playerPlayedCountBySuit.Clone(),
            pressureLeads = pressureLeads,
            earlyHighRankSpends = earlyHighRankSpends,
            completedRematches = completedRematches,
            committedEvidenceClaimsByFamily = (int[])committedEvidenceClaimsByFamily.Clone(),
            matchOrdinal = matchOrdinal,
            packageId = package.primaryCounterPlanId,
            honestStrategyId = package.honestStrategyId,
            packageTargetTendency = package.targetTendency,
            packageHash = package.CanonicalHash,
        };
        if (!candidate.TryValidate(out string error))
            throw new InvalidOperationException("cannot seal invalid behavior: " + error);
        List<GmParlorCompletedMatchSummary> nextSummaries = completedMatchSummaries
            .Select(item => item.DeepCopy()).ToList();
        if (nextSummaries.Count >= MaxStoredMatchSummaries)
        {
            GmParlorCompletedMatchSummary folded = CombineSummaries(
                nextSummaries.Take(2).ToArray());
            nextSummaries.RemoveRange(0, 2);
            nextSummaries.Insert(0, folded);
        }
        nextSummaries.Add(candidate.DeepCopy());
        sealedSummary = candidate;
        completedMatchSummaries = nextSummaries;
        currentMatchSealed = true;
        AdvanceEventSequence();
        return sealedSummary.DeepCopy();
    }

    public GmParlorCompletedMatchSummary CreateCompletedRunSummary()
    {
        if (!currentMatchSealed || completedMatchSummaries.Count == 0)
            throw new InvalidOperationException(
                "a terminal run summary requires a sealed Parlor match");
        return CombineSummaries(completedMatchSummaries);
    }

    public void BeginRematch()
    {
        if (!currentMatchSealed)
            throw new InvalidOperationException("cannot begin rematch before sealing completed match");
        EnsureEventCapacity();
        if (!TryValidate(out string error))
            throw new InvalidOperationException("cannot rematch invalid behavior: " + error);
        EnsureCountCapacity(matchOrdinal);
        matchOrdinal++;
        judgementOpportunities = 0;
        readAttempts = 0;
        correctReads = 0;
        falseReads = 0;
        suspiciousObservations = 0;
        acceptedSuspicious = 0;
        acceptedCalm = 0;
        firstReadOpportunityOrdinal = 0;
        readOpportunityOrdinals.Clear();
        Array.Clear(playerLeadCountBySuit, 0, playerLeadCountBySuit.Length);
        Array.Clear(playerPlayedCountBySuit, 0, playerPlayedCountBySuit.Length);
        pressureLeads = 0;
        earlyHighRankSpends = 0;
        Array.Clear(committedEvidenceClaimsByFamily, 0,
            committedEvidenceClaimsByFamily.Length);
        currentMatchSealed = false;
        sealedSummary = null;
        AdvanceEventSequence();
    }

    public GmParlorBehaviorAccumulator DeepCopy()
    {
        GmParlorCompletedMatchSummary ownedSummary =
            IsEmptySummaryPlaceholder(sealedSummary) ? null : sealedSummary?.DeepCopy();
        return new GmParlorBehaviorAccumulator
        {
            version = version,
            boundPackageHash = boundPackageHash == null
                ? null : (byte[])boundPackageHash.Clone(),
            eventSequence = eventSequence,
            judgementOpportunities = judgementOpportunities,
            readAttempts = readAttempts,
            correctReads = correctReads,
            falseReads = falseReads,
            suspiciousObservations = suspiciousObservations,
            acceptedSuspicious = acceptedSuspicious,
            acceptedCalm = acceptedCalm,
            firstReadOpportunityOrdinal = firstReadOpportunityOrdinal,
            readOpportunityOrdinals = readOpportunityOrdinals == null
                ? null : new List<int>(readOpportunityOrdinals),
            playerLeadCountBySuit = playerLeadCountBySuit == null
                ? null : (int[])playerLeadCountBySuit.Clone(),
            playerPlayedCountBySuit = playerPlayedCountBySuit == null
                ? null : (int[])playerPlayedCountBySuit.Clone(),
            pressureLeads = pressureLeads,
            earlyHighRankSpends = earlyHighRankSpends,
            committedEvidenceClaimsByFamily = committedEvidenceClaimsByFamily == null
                ? null : (int[])committedEvidenceClaimsByFamily.Clone(),
            matchOrdinal = matchOrdinal,
            currentMatchSealed = currentMatchSealed,
            sealedSummary = ownedSummary,
            completedMatchSummaries = completedMatchSummaries == null ? null :
                completedMatchSummaries.Select(item => item?.DeepCopy()).ToList(),
        };
    }

    public bool TryValidate(out string error)
    {
        if (version != CurrentVersion)
            return Fail($"behavior accumulator version {version} is unsupported", out error);
        if (eventSequence > MaxEventSequence)
            return Fail("behavior accumulator event sequence exceeds its durable bound", out error);
        if (boundPackageHash == null || boundPackageHash.Length != 32 ||
            boundPackageHash.All(value => value == 0) ||
            readOpportunityOrdinals == null || completedMatchSummaries == null ||
            completedMatchSummaries.Count > MaxStoredMatchSummaries ||
            playerLeadCountBySuit == null ||
            playerLeadCountBySuit.Length != 4 || playerPlayedCountBySuit == null ||
            playerPlayedCountBySuit.Length != 4 || committedEvidenceClaimsByFamily == null ||
            committedEvidenceClaimsByFamily.Length != 3)
            return Fail("behavior accumulator arrays are invalid", out error);
        if (judgementOpportunities < 0 || readAttempts < 0 || correctReads < 0 ||
            falseReads < 0 || suspiciousObservations < 0 || acceptedSuspicious < 0 ||
            acceptedCalm < 0 || pressureLeads < 0 ||
            earlyHighRankSpends < 0 || matchOrdinal < 1 ||
            playerLeadCountBySuit.Any(value => value < 0) ||
            playerPlayedCountBySuit.Any(value => value < 0) ||
            committedEvidenceClaimsByFamily.Any(value => value < 0))
            return Fail("behavior accumulator contains invalid counts", out error);
        int[] boundedScalars = { judgementOpportunities, readAttempts, correctReads,
            falseReads, suspiciousObservations, acceptedSuspicious, acceptedCalm,
            firstReadOpportunityOrdinal, pressureLeads, earlyHighRankSpends, matchOrdinal };
        if (boundedScalars.Any(value => value > GmParlorCompletedMatchSummary.MaxFeatureCount) ||
            playerLeadCountBySuit.Any(value =>
                value > GmParlorCompletedMatchSummary.MaxFeatureCount) ||
            playerPlayedCountBySuit.Any(value =>
                value > GmParlorCompletedMatchSummary.MaxFeatureCount) ||
            committedEvidenceClaimsByFamily.Any(value =>
                value > GmParlorCompletedMatchSummary.MaxFeatureCount))
            return Fail("behavior accumulator exceeds its bounded feature counts", out error);
        if (correctReads + falseReads != readAttempts ||
            readAttempts + acceptedSuspicious + acceptedCalm != judgementOpportunities ||
            readOpportunityOrdinals.Count != readAttempts ||
            acceptedSuspicious > suspiciousObservations ||
            suspiciousObservations > judgementOpportunities ||
            acceptedCalm > judgementOpportunities - suspiciousObservations ||
            pressureLeads > playerLeadCountBySuit.Sum() ||
            earlyHighRankSpends > pressureLeads ||
            committedEvidenceClaimsByFamily.Sum() > readAttempts)
            return Fail("behavior accumulator counts are contradictory", out error);
        for (int suit = 0; suit < 4; suit++)
            if (playerLeadCountBySuit[suit] > playerPlayedCountBySuit[suit])
                return Fail("behavior accumulator suit counts are contradictory", out error);
        for (int index = 0; index < readOpportunityOrdinals.Count; index++)
            if (readOpportunityOrdinals[index] <= 0 ||
                readOpportunityOrdinals[index] > judgementOpportunities ||
                (index > 0 && readOpportunityOrdinals[index] <= readOpportunityOrdinals[index - 1]))
                return Fail("behavior accumulator Read ordinals are invalid", out error);
        if ((readAttempts == 0 && firstReadOpportunityOrdinal != 0) ||
            (readAttempts > 0 && firstReadOpportunityOrdinal != readOpportunityOrdinals[0]))
            return Fail("behavior accumulator first Read ordinal is contradictory", out error);
        bool hasSummary = sealedSummary != null && !IsEmptySummaryPlaceholder(sealedSummary);
        if (currentMatchSealed != hasSummary)
            return Fail("behavior accumulator seal state is contradictory", out error);
        if (hasSummary && !sealedSummary.TryValidate(out error)) return false;
        for (int index = 0; index < completedMatchSummaries.Count; index++)
        {
            if (completedMatchSummaries[index] == null)
                return Fail("behavior accumulator contains a null completed match", out error);
            if (!completedMatchSummaries[index].TryValidate(out error)) return false;
            if (index > 0 && completedMatchSummaries[index].matchOrdinal <=
                completedMatchSummaries[index - 1].matchOrdinal)
                return Fail("completed match ordinals are not strictly increasing", out error);
        }
        if (hasSummary && completedMatchSummaries.Count == 0)
            return Fail("sealed behavior is missing its completed match summary", out error);
        if (hasSummary && !completedMatchSummaries[completedMatchSummaries.Count - 1]
            .ToCanonicalBytes().SequenceEqual(sealedSummary.ToCanonicalBytes()))
            return Fail("sealed behavior does not match its latest completed match", out error);
        if (completedMatchSummaries.Count > 0)
        {
            int lastOrdinal = completedMatchSummaries[completedMatchSummaries.Count - 1]
                .matchOrdinal;
            int expected = currentMatchSealed ? matchOrdinal : matchOrdinal - 1;
            if (lastOrdinal != expected)
                return Fail("completed match history does not match current rematch ordinal",
                    out error);
        }
        error = string.Empty;
        return true;
    }

    static GmParlorCompletedMatchSummary CombineSummaries(
        IReadOnlyList<GmParlorCompletedMatchSummary> summaries)
    {
        if (summaries == null || summaries.Count == 0)
            throw new ArgumentException("completed match summaries are empty", nameof(summaries));
        GmParlorCompletedMatchSummary first = summaries[0];
        var combined = new GmParlorCompletedMatchSummary
        {
            playerLeadCountBySuit = new int[4],
            playerPlayedCountBySuit = new int[4],
            committedEvidenceClaimsByFamily = new int[3],
            packageId = first.packageId,
            honestStrategyId = first.honestStrategyId,
            packageTargetTendency = first.packageTargetTendency,
            packageHash = first.packageHash == null ? null : (byte[])first.packageHash.Clone(),
        };
        int precedingJudgements = 0;
        for (int index = 0; index < summaries.Count; index++)
        {
            GmParlorCompletedMatchSummary item = summaries[index];
            if (item == null)
                throw new InvalidOperationException("invalid completed match summary: null");
            if (!item.TryValidate(out string error))
                throw new InvalidOperationException("invalid completed match summary: " + error);
            if (item.packageId != combined.packageId ||
                item.honestStrategyId != combined.honestStrategyId ||
                item.packageTargetTendency != combined.packageTargetTendency ||
                !item.packageHash.SequenceEqual(combined.packageHash))
                throw new InvalidOperationException("rematch changed its frozen adaptive package");
            if (combined.firstReadOpportunityOrdinal == 0 && item.readAttempts > 0)
            {
                combined.firstReadOpportunityOrdinal = precedingJudgements +
                    item.firstReadOpportunityOrdinal;
                combined.firstReadMatchStartOrdinal = precedingJudgements +
                    item.firstReadMatchStartOrdinal;
                combined.firstReadMatchJudgementOpportunities =
                    item.firstReadMatchJudgementOpportunities;
                combined.firstReadMatchReadFirstThird =
                    item.firstReadMatchReadFirstThird;
                combined.firstReadMatchReadMiddleThird =
                    item.firstReadMatchReadMiddleThird;
                combined.firstReadMatchReadFinalThird =
                    item.firstReadMatchReadFinalThird;
            }
            combined.judgementOpportunities += item.judgementOpportunities;
            combined.readAttempts += item.readAttempts;
            combined.correctReads += item.correctReads;
            combined.falseReads += item.falseReads;
            combined.suspiciousObservations += item.suspiciousObservations;
            combined.acceptedSuspicious += item.acceptedSuspicious;
            combined.acceptedCalm += item.acceptedCalm;
            combined.readFirstThird += item.readFirstThird;
            combined.readMiddleThird += item.readMiddleThird;
            combined.readFinalThird += item.readFinalThird;
            combined.firstThirdOpportunityCapacity += item.firstThirdOpportunityCapacity;
            combined.middleThirdOpportunityCapacity += item.middleThirdOpportunityCapacity;
            combined.finalThirdOpportunityCapacity += item.finalThirdOpportunityCapacity;
            for (int suit = 0; suit < 4; suit++)
            {
                combined.playerLeadCountBySuit[suit] += item.playerLeadCountBySuit[suit];
                combined.playerPlayedCountBySuit[suit] += item.playerPlayedCountBySuit[suit];
            }
            combined.pressureLeads += item.pressureLeads;
            combined.earlyHighRankSpends += item.earlyHighRankSpends;
            for (int family = 0; family < 3; family++)
                combined.committedEvidenceClaimsByFamily[family] +=
                    item.committedEvidenceClaimsByFamily[family];
            combined.completedRematches += item.completedRematches;
            combined.matchOrdinal = Math.Max(combined.matchOrdinal, item.matchOrdinal);
            precedingJudgements += item.judgementOpportunities;
        }
        combined.judgementOpportunitiesByMatch = summaries.SelectMany(item =>
            item.judgementOpportunitiesByMatch).ToArray();
        combined.completedRematches += summaries.Count - 1;
        if (!combined.TryValidate(out string combinedError))
            throw new InvalidOperationException("invalid completed run summary: " + combinedError);
        return combined;
    }

    public byte[] ToCanonicalBytes()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(version);
            writer.Write(boundPackageHash?.Length ?? -1);
            if (boundPackageHash != null) writer.Write(boundPackageHash);
            writer.Write(eventSequence);
            writer.Write(judgementOpportunities);
            writer.Write(readAttempts);
            writer.Write(correctReads);
            writer.Write(falseReads);
            writer.Write(suspiciousObservations);
            writer.Write(acceptedSuspicious);
            writer.Write(acceptedCalm);
            writer.Write(firstReadOpportunityOrdinal);
            WriteList(writer, readOpportunityOrdinals);
            WriteArray(writer, playerLeadCountBySuit);
            WriteArray(writer, playerPlayedCountBySuit);
            writer.Write(pressureLeads);
            writer.Write(earlyHighRankSpends);
            WriteArray(writer, committedEvidenceClaimsByFamily);
            writer.Write(matchOrdinal);
            writer.Write(currentMatchSealed);
            WriteSummary(writer, sealedSummary);
            writer.Write(completedMatchSummaries?.Count ?? -1);
            if (completedMatchSummaries != null)
                for (int index = 0; index < completedMatchSummaries.Count; index++)
                    WriteSummary(writer, completedMatchSummaries[index]);
            return stream.ToArray();
        }
    }

    static void WriteArray(BinaryWriter writer, int[] values)
    {
        writer.Write(values?.Length ?? -1);
        if (values == null) return;
        for (int index = 0; index < values.Length; index++) writer.Write(values[index]);
    }

    static void WriteList(BinaryWriter writer, List<int> values)
    {
        writer.Write(values?.Count ?? -1);
        if (values == null) return;
        for (int index = 0; index < values.Count; index++) writer.Write(values[index]);
    }

    static void WriteSummary(BinaryWriter writer, GmParlorCompletedMatchSummary summary)
    {
        if (summary == null)
        {
            writer.Write(-1);
            return;
        }
        byte[] bytes = summary.ToCanonicalBytes();
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    static void ValidateCard(GmCard card)
    {
        if (!Enum.IsDefined(typeof(GmSuit), card.Suit) || card.Rank < 1 || card.Rank > 7)
            throw new ArgumentOutOfRangeException(nameof(card));
    }

    void EnsureRecordable()
    {
        if (currentMatchSealed) throw new InvalidOperationException("current match is sealed");
        EnsureEventCapacity();
    }

    static void EnsureCountCapacity(params int[] values)
    {
        if (values.Any(value => value >= GmParlorCompletedMatchSummary.MaxFeatureCount))
            throw new InvalidOperationException("behavior feature count is exhausted");
    }

    void AdvanceEventSequence()
    {
        EnsureEventCapacity();
        eventSequence++;
    }

    void EnsureEventCapacity()
    {
        if (eventSequence >= MaxEventSequence)
            throw new InvalidOperationException("behavior event sequence is exhausted");
    }

    static bool IsEmptySummaryPlaceholder(GmParlorCompletedMatchSummary summary)
    {
        return summary != null && summary.judgementOpportunities == 0 &&
            summary.readAttempts == 0 && summary.playerLeadCountBySuit != null &&
            summary.playerLeadCountBySuit.All(value => value == 0) &&
            summary.playerPlayedCountBySuit != null &&
            summary.playerPlayedCountBySuit.All(value => value == 0) &&
            summary.matchOrdinal == 0 && !currentPlaceholderSeal(summary);
    }

    static bool currentPlaceholderSeal(GmParlorCompletedMatchSummary summary) =>
        summary.packageId != GmParlorCounterPlanId.None ||
        summary.honestStrategyId != GmParlorHonestStrategyId.MeasuredCourtesy ||
        summary.packageTargetTendency != GmParlorTendency.None;

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}

[Serializable]
public sealed class GmParlorCompletedRunReceipt
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long runOrdinal;
    public string receiptId = string.Empty;
    public GmParlorCompletedMatchSummary summary;

    public long RunOrdinal => runOrdinal;
    public string ReceiptId => receiptId ?? string.Empty;
    public GmParlorCompletedMatchSummary Summary => summary?.DeepCopy();

    public bool TryValidate(out string error)
    {
        if (version != CurrentVersion) return Fail("run receipt version is unsupported", out error);
        if (runOrdinal <= 0 || string.IsNullOrWhiteSpace(receiptId) ||
            receiptId.Length > 128 || receiptId.Any(char.IsControl))
            return Fail("run receipt identity is invalid", out error);
        if (summary == null) return Fail("run receipt summary is missing", out error);
        if (!summary.TryValidate(out error)) return false;
        if (summary.completedRematches != summary.matchOrdinal - 1)
            return Fail("terminal run receipt rematch identity is contradictory", out error);
        error = string.Empty;
        return true;
    }

    public byte[] ToCanonicalBytes()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(version);
            writer.Write(runOrdinal);
            byte[] id = Encoding.UTF8.GetBytes(receiptId ?? string.Empty);
            writer.Write(id.Length);
            writer.Write(id);
            byte[] payload = summary?.ToCanonicalBytes() ?? Array.Empty<byte>();
            writer.Write(payload.Length);
            writer.Write(payload);
            return stream.ToArray();
        }
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}

public sealed class GmParlorProfileView
{
    readonly GmParlorCompletedRunReceipt[] receipts;
    readonly byte[] historyDigest;

    public GmParlorProfileView(IEnumerable<GmParlorCompletedRunReceipt> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        receipts = source.OrderBy(item => item?.runOrdinal ?? long.MinValue)
            .ThenBy(item => item?.receiptId ?? string.Empty, StringComparer.Ordinal)
            .Select(OwnValidated).ToArray();
        var receiptIds = new HashSet<string>(StringComparer.Ordinal);
        var ordinals = new HashSet<long>();
        for (int index = 0; index < receipts.Length; index++)
        {
            if (!receiptIds.Add(receipts[index].receiptId))
                throw new ArgumentException("duplicate Parlor terminal receipt id " +
                    receipts[index].receiptId);
            if (!ordinals.Add(receipts[index].runOrdinal))
                throw new ArgumentException("duplicate Parlor terminal run ordinal " +
                    receipts[index].runOrdinal);
        }
        using (SHA256 sha = SHA256.Create())
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            for (int index = 0; index < receipts.Length; index++)
            {
                byte[] bytes = receipts[index].ToCanonicalBytes();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            writer.Flush();
            historyDigest = sha.ComputeHash(stream.ToArray());
        }
    }

    public IReadOnlyList<GmParlorCompletedRunReceipt> Receipts =>
        receipts.Select(OwnValidated).ToArray();
    public byte[] HistoryDigest => (byte[])historyDigest.Clone();

    static GmParlorCompletedRunReceipt OwnValidated(GmParlorCompletedRunReceipt receipt)
    {
        if (receipt == null)
            throw new ArgumentException("invalid Parlor terminal receipt: null");
        if (!receipt.TryValidate(out string error))
            throw new ArgumentException("invalid Parlor terminal receipt: " + error);
        return new GmParlorCompletedRunReceipt
        {
            version = receipt.version,
            runOrdinal = receipt.runOrdinal,
            receiptId = receipt.receiptId,
            summary = receipt.summary.DeepCopy(),
        };
    }
}

public static class GmParlorAdaptiveDirector
{
    sealed class Candidate
    {
        public GmParlorTendency Tendency;
        public int Confidence;
        public GmParlorCounterPlanId Plan;
        public GmParlorHonestStrategyId Strategy;
    }

    public static GmParlorAdaptivePackage Select(GmParlorProfileView profile, int seed,
        GmParlorAdaptiveMode mode, int catalogVersion)
    {
        if (catalogVersion != GmParlorAdaptivePackage.CurrentCatalogVersion)
            throw new ArgumentOutOfRangeException(nameof(catalogVersion));
        if (!Enum.IsDefined(typeof(GmParlorAdaptiveMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == GmParlorAdaptiveMode.Recollection)
            throw new InvalidOperationException(
                "Recollection requires SelectKnownForRecollection");
        if (mode == GmParlorAdaptiveMode.Ordinary)
            return GmParlorAdaptivePackage.Baseline(mode, catalogVersion);
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (profile.Receipts.Count == 0)
            return GmParlorAdaptivePackage.Baseline(mode, catalogVersion);

        GmParlorCompletedRunReceipt[] receipts = profile.Receipts
            .Skip(Math.Max(0, profile.Receipts.Count - 5)).ToArray();
        List<Candidate> candidates = BuildCandidates(receipts);
        ApplyCooldown(candidates, receipts);
        Candidate primary = candidates.OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.Tendency).FirstOrDefault();

        int tier = receipts.Length == 0 ? 0 : receipts.Length == 1 ? 1 :
            receipts.Length <= 3 ? 2 : 3;
        var package = GmParlorAdaptivePackage.Baseline(mode, catalogVersion);
        package.attentionTier = tier;
        package.historyDigest = profile.HistoryDigest;
        package.selectedFromReceiptOrdinal = receipts[receipts.Length - 1].runOrdinal;
        if (primary == null)
        {
            package.fallbackReason = "no-eligible-tendency-after-cooldown";
            return package;
        }

        package.primaryCounterPlanId = primary.Plan;
        package.targetTendency = primary.Tendency;
        package.honestStrategyId = primary.Strategy;
        ConfigurePrimaryPresentation(package, primary);
        if (tier >= 2) package.memoryTokenId = MemoryFor(primary.Tendency);

        if (tier >= 3)
        {
            Candidate presentation = BuildPresentationCandidates(receipts)
                .Where(item => item.Tendency != primary.Tendency)
                .OrderByDescending(item => item.Confidence)
                .ThenBy(item => item.Tendency).FirstOrDefault();
            if (presentation != null)
            {
                package.secondaryTendency = presentation.Tendency;
                package.secondaryPresentationPlanId = presentation.Tendency ==
                    GmParlorTendency.GestureSpecialist
                    ? GmParlorPresentationPlanId.StillHandsGesture
                    : GmParlorPresentationPlanId.StillHandsCardContact;
                package.tellFamilyId = GmParlorTellFamilyId.GazeAndCardContact;
            }
        }
        return package;
    }

    public static GmParlorAdaptivePackage SelectKnownForRecollection(
        GmParlorAdaptivePackage knownPackage)
    {
        if (knownPackage == null)
            throw new ArgumentNullException(nameof(knownPackage));
        if (!knownPackage.TryValidate(out string error))
            throw new ArgumentException("invalid known Parlor package: " + error,
                nameof(knownPackage));
        if (knownPackage.mode != GmParlorAdaptiveMode.Mirror ||
            knownPackage.provenance != GmParlorPackageProvenance.MirrorDirector)
            throw new ArgumentException(
                "Recollection requires a director-issued Mirror package",
                nameof(knownPackage));
        GmParlorAdaptivePackage selected = knownPackage.DeepCopy();
        selected.mode = GmParlorAdaptiveMode.Recollection;
        selected.provenance = GmParlorPackageProvenance.RecollectionKnown;
        return selected;
    }

    static List<Candidate> BuildCandidates(GmParlorCompletedRunReceipt[] receipts)
    {
        var result = new List<Candidate>();
        int totalReads = receipts.Sum(item => item.summary.readAttempts);
        int totalJudgements = receipts.Sum(item => item.summary.judgementOpportunities);
        int totalSuspicious = receipts.Sum(item => item.summary.suspiciousObservations);
        int totalLeads = receipts.Sum(item => item.summary.playerLeadCountBySuit.Sum());
        if (totalReads >= 3)
        {
            int readRate = WeightedRatio(receipts, item => item.readAttempts,
                item => item.judgementOpportunities);
            if (readRate >= 5500)
                result.Add(New(GmParlorTendency.ReadHungry, readRate,
                    GmParlorCounterPlanId.CourtesyFeint, GmParlorHonestStrategyId.TrumpReserve));
            int falseRate = WeightedRatio(receipts, item => item.falseReads, item => item.readAttempts);
            if (falseRate >= 4000)
                result.Add(New(GmParlorTendency.FalseAccuser, falseRate,
                    GmParlorCounterPlanId.CourtesyFeint, GmParlorHonestStrategyId.TrumpReserve));
            int accuracy = WeightedCountRatio(receipts, item => item.correctReads,
                item => item.readAttempts);
            int patience = WeightedCountRatio(receipts, item => item.readFinalThird,
                item => item.readAttempts);
            if (accuracy >= 7500 && patience >= 6000)
                result.Add(New(GmParlorTendency.AccuratePatient, Math.Min(accuracy, patience),
                    GmParlorCounterPlanId.StillHands,
                    GmParlorHonestStrategyId.MeasuredCourtesy));
        }
        if (totalSuspicious >= 3)
        {
            int rate = WeightedRatio(receipts, item => item.acceptedSuspicious,
                item => item.suspiciousObservations);
            if (rate >= 5000)
                result.Add(New(GmParlorTendency.SuspiciousAcceptor, rate,
                    GmParlorCounterPlanId.StillHands,
                    GmParlorHonestStrategyId.MeasuredCourtesy));
        }
        if (totalLeads >= 3)
        {
            int[] suitScore = new int[4];
            int[] weightedLeads = new int[4];
            for (int suit = 0; suit < 4; suit++)
            {
                int captured = suit;
                suitScore[suit] = WeightedRatio(receipts,
                    item => item.playerLeadCountBySuit[captured],
                    item => item.playerLeadCountBySuit.Sum());
                for (int index = 0; index < receipts.Length; index++)
                    weightedLeads[suit] += receipts[index].summary.playerLeadCountBySuit[suit] *
                        (index + 1);
            }
            int best = Enumerable.Range(0, 4).OrderByDescending(suit => suitScore[suit])
                .ThenBy(suit => suit).First();
            int runner = Enumerable.Range(0, 4).Where(suit => suit != best)
                .Max(suit => weightedLeads[suit]);
            if (suitScore[best] >= 4000 && weightedLeads[best] >= runner + 2)
            {
                SuitCatalog((GmSuit)best, out GmParlorTendency tendency,
                    out GmParlorHonestStrategyId strategy);
                result.Add(New(tendency, suitScore[best], GmParlorCounterPlanId.EmberLock,
                    strategy));
            }
            int pressure = WeightedRatio(receipts, item => item.pressureLeads,
                item => item.playerLeadCountBySuit.Sum());
            if (pressure >= 4500)
                result.Add(New(GmParlorTendency.PressurePlayer, pressure,
                    // Deliberate trick sacrifice remains disabled until a bounded honest
                    // best-of-three solver can prove it never throws the match.
                    GmParlorCounterPlanId.ConservationLedger,
                    GmParlorHonestStrategyId.CounterConservation));
        }
        if (receipts.Any(item => item.summary.completedRematches >= 2))
        {
            int rotation = receipts.Length % 3;
            GmParlorHonestStrategyId strategy = rotation == 0
                ? GmParlorHonestStrategyId.SecondDealLow : rotation == 1
                    ? GmParlorHonestStrategyId.SecondDealHigh
                    : GmParlorHonestStrategyId.SecondDealReserve;
            result.Add(New(GmParlorTendency.Rematcher, 5000,
                GmParlorCounterPlanId.SecondDeal, strategy));
        }
        return result;
    }

    static IEnumerable<Candidate> BuildPresentationCandidates(
        GmParlorCompletedRunReceipt[] receipts)
    {
        int totalClaims = receipts.Sum(item => item.summary.committedEvidenceClaimsByFamily.Sum());
        if (totalClaims < 3) yield break;
        for (int family = 0; family < 2; family++)
        {
            int captured = family;
            GmParlorCompletedRunReceipt[] claimBearing = receipts.Where(item =>
                item.summary.committedEvidenceClaimsByFamily.Sum() > 0).ToArray();
            int rate = WeightedRatio(claimBearing,
                item => item.committedEvidenceClaimsByFamily[captured],
                item => item.committedEvidenceClaimsByFamily.Sum());
            if (rate < 6000) continue;
            yield return New(family == 0 ? GmParlorTendency.GestureSpecialist :
                GmParlorTendency.CardContactSpecialist, rate,
                GmParlorCounterPlanId.StillHands,
                GmParlorHonestStrategyId.MeasuredCourtesy);
        }
    }

    static void ApplyCooldown(List<Candidate> candidates,
        GmParlorCompletedRunReceipt[] receipts)
    {
        if (candidates.Count == 0 || receipts.Length == 0) return;
        GmParlorTendency last = receipts[receipts.Length - 1].summary.packageTargetTendency;
        bool twoSame = receipts.Length >= 2 && last != GmParlorTendency.None &&
            receipts[receipts.Length - 2].summary.packageTargetTendency == last;
        if (twoSame)
        {
            candidates.RemoveAll(item => item.Tendency == last);
            return;
        }
        if (last == GmParlorTendency.None) return;
        bool alternative = candidates.Any(item => item.Tendency != last);
        if (alternative) candidates.RemoveAll(item => item.Tendency == last);
    }

    static int WeightedRatio(GmParlorCompletedRunReceipt[] receipts,
        Func<GmParlorCompletedMatchSummary, int> numerator,
        Func<GmParlorCompletedMatchSummary, int> denominator)
    {
        long weighted = 0;
        int weights = 0;
        for (int index = 0; index < receipts.Length; index++)
        {
            int d = denominator(receipts[index].summary);
            if (d <= 0) continue;
            int ratio = (int)((long)numerator(receipts[index].summary) * 10000 / d);
            int weight = index + 1;
            weighted += (long)ratio * weight;
            weights += weight;
        }
        return weights == 0 ? 0 : (int)(weighted / weights);
    }

    static int WeightedCountRatio(GmParlorCompletedRunReceipt[] receipts,
        Func<GmParlorCompletedMatchSummary, int> numerator,
        Func<GmParlorCompletedMatchSummary, int> denominator)
    {
        long n = 0;
        long d = 0;
        for (int index = 0; index < receipts.Length; index++)
        {
            int weight = index + 1;
            n += (long)numerator(receipts[index].summary) * weight;
            d += (long)denominator(receipts[index].summary) * weight;
        }
        return d == 0 ? 0 : (int)(n * 10000 / d);
    }

    static Candidate New(GmParlorTendency tendency, int confidence,
        GmParlorCounterPlanId plan, GmParlorHonestStrategyId strategy)
    {
        return new Candidate
        {
            Tendency = tendency,
            Confidence = confidence,
            Plan = plan,
            Strategy = strategy,
        };
    }

    static void SuitCatalog(GmSuit suit, out GmParlorTendency tendency,
        out GmParlorHonestStrategyId strategy)
    {
        switch (suit)
        {
            case GmSuit.Flames:
                tendency = GmParlorTendency.SuitSpecialistFlames;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveFlames;
                return;
            case GmSuit.Eyes:
                tendency = GmParlorTendency.SuitSpecialistEyes;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveEyes;
                return;
            case GmSuit.Bones:
                tendency = GmParlorTendency.SuitSpecialistBones;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveBones;
                return;
            case GmSuit.Teeth:
                tendency = GmParlorTendency.SuitSpecialistTeeth;
                strategy = GmParlorHonestStrategyId.PreferredSuitReserveTeeth;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(suit));
        }
    }

    static void ConfigurePrimaryPresentation(GmParlorAdaptivePackage package,
        Candidate primary)
    {
        switch (primary.Plan)
        {
            case GmParlorCounterPlanId.CourtesyFeint:
                package.tellFamilyId = GmParlorTellFamilyId.PolitePause;
                break;
            case GmParlorCounterPlanId.StillHands:
                package.tellFamilyId = GmParlorTellFamilyId.StillHands;
                break;
            case GmParlorCounterPlanId.ConservationLedger:
                package.tellFamilyId = GmParlorTellFamilyId.GazeAndCardContact;
                break;
        }
    }

    static GmParlorMemoryTokenId MemoryFor(GmParlorTendency tendency)
    {
        if (tendency >= GmParlorTendency.SuitSpecialistFlames &&
            tendency <= GmParlorTendency.SuitSpecialistBones)
            return GmParlorMemoryTokenId.AshUnderGlass;
        if (tendency == GmParlorTendency.PressurePlayer)
            return GmParlorMemoryTokenId.TheReturnedCard;
        return GmParlorMemoryTokenId.FirstRecognition;
    }
}
