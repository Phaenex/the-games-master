using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>Versioned, Unity-reference-free serialized authority for an exact Parlor resume.</summary>
[Serializable]
public sealed class GmParlorMatchSnapshot
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public int seed;
    public uint randomState;
    public GmParlorMatchPhase phase;
    public GmReadTestState readTestState;
    public bool readUnlocked;
    public int corruptionTier;
    public int priorCatchCount;
    public int catchesThisMatch;
    public int suspicion;
    public int sanity;
    public int defiance;
    public int compliance;
    public int roundNumber;
    public int playerRounds;
    public int aldricRounds;
    public int playerTricks;
    public int aldricTricks;
    public int trickNumber;
    public bool lastLeadWasAldric;
    public bool aldricCheated;
    public GmTellObservation tellObservation;
    public bool eyesExposeAldricHand;
    public bool bonesReturnedThisRound;
    public GmParlorCheatKind aldricCheatKind;
    public string authoritativeCheatTell = string.Empty;
    public int aldricPaidIndex = -1;
    public bool hasAldricPaidCard;
    public GmCard aldricPaidCard;
    public bool hasCurrentLeadCard;
    public GmCard currentLeadCard;
    public bool hasCurrentFollowCard;
    public GmCard currentFollowCard;
    public GmTrickOwner effectiveWinner;
    public GmTrickOwner lastTrickWinner;
    public GmTrickOwner roundWinner;
    public GmTrickOwner matchWinner;
    public List<GmCard> playerHand = new List<GmCard>();
    public List<GmCard> aldricHand = new List<GmCard>();
    public List<GmCard> revealedAldricCards = new List<GmCard>();
    public ulong outcomeSequence;
    public ulong highestDurableOutcomeSequence;
    public ulong highestAcknowledgedOutcomeSequence;
    public bool outcomePending;
    public GmParlorOutcome lastOutcome;
    public GmParlorAdaptivePackage adaptivePackage;
    public GmParlorBehaviorAccumulator behaviorAccumulator;

    public GmParlorMatchSnapshot DeepCopy()
    {
        return new GmParlorMatchSnapshot
        {
            version = version,
            seed = seed,
            randomState = randomState,
            phase = phase,
            readTestState = readTestState,
            readUnlocked = readUnlocked,
            corruptionTier = corruptionTier,
            priorCatchCount = priorCatchCount,
            catchesThisMatch = catchesThisMatch,
            suspicion = suspicion,
            sanity = sanity,
            defiance = defiance,
            compliance = compliance,
            roundNumber = roundNumber,
            playerRounds = playerRounds,
            aldricRounds = aldricRounds,
            playerTricks = playerTricks,
            aldricTricks = aldricTricks,
            trickNumber = trickNumber,
            lastLeadWasAldric = lastLeadWasAldric,
            aldricCheated = aldricCheated,
            tellObservation = tellObservation,
            eyesExposeAldricHand = eyesExposeAldricHand,
            bonesReturnedThisRound = bonesReturnedThisRound,
            aldricCheatKind = aldricCheatKind,
            authoritativeCheatTell = authoritativeCheatTell,
            aldricPaidIndex = aldricPaidIndex,
            hasAldricPaidCard = hasAldricPaidCard,
            aldricPaidCard = aldricPaidCard,
            hasCurrentLeadCard = hasCurrentLeadCard,
            currentLeadCard = currentLeadCard,
            hasCurrentFollowCard = hasCurrentFollowCard,
            currentFollowCard = currentFollowCard,
            effectiveWinner = effectiveWinner,
            lastTrickWinner = lastTrickWinner,
            roundWinner = roundWinner,
            matchWinner = matchWinner,
            playerHand = playerHand == null ? null : new List<GmCard>(playerHand),
            aldricHand = aldricHand == null ? null : new List<GmCard>(aldricHand),
            revealedAldricCards = revealedAldricCards == null
                ? null : new List<GmCard>(revealedAldricCards),
            outcomeSequence = outcomeSequence,
            highestDurableOutcomeSequence = highestDurableOutcomeSequence,
            highestAcknowledgedOutcomeSequence = highestAcknowledgedOutcomeSequence,
            outcomePending = outcomePending,
            lastOutcome = lastOutcome,
            adaptivePackage = adaptivePackage?.DeepCopy(),
            behaviorAccumulator = behaviorAccumulator?.DeepCopy(),
        };
    }

    public byte[] ToCanonicalBytes()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(version);
            writer.Write(seed);
            writer.Write(randomState);
            writer.Write((int)phase);
            writer.Write((int)readTestState);
            writer.Write(readUnlocked);
            writer.Write(corruptionTier);
            writer.Write(priorCatchCount);
            writer.Write(catchesThisMatch);
            writer.Write(suspicion);
            writer.Write(sanity);
            writer.Write(defiance);
            writer.Write(compliance);
            writer.Write(roundNumber);
            writer.Write(playerRounds);
            writer.Write(aldricRounds);
            writer.Write(playerTricks);
            writer.Write(aldricTricks);
            writer.Write(trickNumber);
            writer.Write(lastLeadWasAldric);
            writer.Write(aldricCheated);
            writer.Write((int)tellObservation);
            writer.Write(eyesExposeAldricHand);
            writer.Write(bonesReturnedThisRound);
            writer.Write((int)aldricCheatKind);
            WriteString(writer, authoritativeCheatTell);
            writer.Write(aldricPaidIndex);
            writer.Write(hasAldricPaidCard);
            WriteCard(writer, aldricPaidCard);
            writer.Write(hasCurrentLeadCard);
            WriteCard(writer, currentLeadCard);
            writer.Write(hasCurrentFollowCard);
            WriteCard(writer, currentFollowCard);
            writer.Write((int)effectiveWinner);
            writer.Write((int)lastTrickWinner);
            writer.Write((int)roundWinner);
            writer.Write((int)matchWinner);
            WriteCards(writer, playerHand);
            WriteCards(writer, aldricHand);
            WriteCards(writer, revealedAldricCards);
            writer.Write(outcomeSequence);
            writer.Write(highestDurableOutcomeSequence);
            writer.Write(highestAcknowledgedOutcomeSequence);
            writer.Write(outcomePending);
            writer.Write((int)lastOutcome.Kind);
            writer.Write(lastOutcome.CatchDelta);
            writer.Write(lastOutcome.CorruptionDelta);
            writer.Write(lastOutcome.SuspicionDelta);
            writer.Write(lastOutcome.SanityDelta);
            writer.Write(lastOutcome.DefianceDelta);
            writer.Write(lastOutcome.ComplianceDelta);
            WritePayload(writer, adaptivePackage?.ToCanonicalBytes());
            WritePayload(writer, behaviorAccumulator?.ToCanonicalBytes());
            return stream.ToArray();
        }
    }

    public byte[] ReplayHash
    {
        get
        {
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(ToCanonicalBytes());
        }
    }

    static void WriteCard(BinaryWriter writer, GmCard card)
    {
        writer.Write((int)card.Suit);
        writer.Write(card.Rank);
    }

    static void WriteCards(BinaryWriter writer, List<GmCard> cards)
    {
        writer.Write(cards?.Count ?? -1);
        if (cards == null) return;
        for (int index = 0; index < cards.Count; index++) WriteCard(writer, cards[index]);
    }

    static void WriteString(BinaryWriter writer, string value)
    {
        if (value == null)
        {
            writer.Write(-1);
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    static void WritePayload(BinaryWriter writer, byte[] bytes)
    {
        writer.Write(bytes?.Length ?? -1);
        if (bytes != null) writer.Write(bytes);
    }
}
