using System;
using System.Collections.Generic;
using UnityEngine;

public enum GmParlorCardZone
{
    HousePile,
    PlayerHand,
    AldricHand,
    Lead,
    Follow,
}

public enum GmParlorCardFacing
{
    FaceDown,
    FaceUp,
}

public readonly struct GmParlorCardBinding : IEquatable<GmParlorCardBinding>
{
    public readonly GmCard PhysicalCard;
    public readonly GmCard DisplayCard;
    public readonly GmParlorCardZone Zone;
    public readonly int Slot;
    public readonly GmParlorCardFacing Facing;

    public GmParlorCardBinding(GmCard physicalCard, GmCard displayCard,
        GmParlorCardZone zone, int slot, GmParlorCardFacing facing)
    {
        PhysicalCard = physicalCard;
        DisplayCard = displayCard;
        Zone = zone;
        Slot = slot;
        Facing = facing;
    }

    public bool Equals(GmParlorCardBinding other)
    {
        return PhysicalCard == other.PhysicalCard && DisplayCard == other.DisplayCard &&
            Zone == other.Zone && Slot == other.Slot && Facing == other.Facing;
    }

    public override bool Equals(object obj) => obj is GmParlorCardBinding other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = PhysicalCard.GetHashCode();
            hash = (hash * 397) ^ DisplayCard.GetHashCode();
            hash = (hash * 397) ^ (int)Zone;
            hash = (hash * 397) ^ Slot;
            return (hash * 397) ^ (int)Facing;
        }
    }

    public static bool operator ==(GmParlorCardBinding left, GmParlorCardBinding right) =>
        left.Equals(right);

    public static bool operator !=(GmParlorCardBinding left, GmParlorCardBinding right) =>
        !left.Equals(right);
}

/// <summary>
/// Maps canonical card identities to logical table zones and measured local poses. Cards absent
/// from the public snapshot stay in one face-down house pile because the match intentionally does
/// not expose whether a hidden card is undealt or already collected.
/// </summary>
public static class GmParlorTableLayout
{
    static readonly GmSuit[] CanonicalSuits =
    {
        GmSuit.Flames,
        GmSuit.Eyes,
        GmSuit.Teeth,
        GmSuit.Bones,
    };

    public static GmParlorCardBinding[] Build(GmParlorMatchSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.playerHand == null || snapshot.aldricHand == null ||
            snapshot.revealedAldricCards == null)
            throw new ArgumentException("Parlor card lists cannot be null", nameof(snapshot));

        var bindings = new Dictionary<GmCard, GmParlorCardBinding>(GmParlorCore.TotalCards);
        int houseSlot = 0;
        foreach (GmSuit suit in CanonicalSuits)
        {
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
            {
                GmCard card = new GmCard(suit, rank);
                bindings.Add(card, new GmParlorCardBinding(card, card,
                    GmParlorCardZone.HousePile, houseSlot++, GmParlorCardFacing.FaceDown));
            }
        }

        for (int index = 0; index < snapshot.playerHand.Count; index++)
            Assign(bindings, snapshot.playerHand[index], snapshot.playerHand[index],
                GmParlorCardZone.PlayerHand, index, GmParlorCardFacing.FaceUp);

        for (int index = 0; index < snapshot.aldricHand.Count; index++)
        {
            GmCard card = snapshot.aldricHand[index];
            bool revealed = snapshot.eyesExposeAldricHand || snapshot.revealedAldricCards.Contains(card);
            Assign(bindings, card, card, GmParlorCardZone.AldricHand, index,
                revealed ? GmParlorCardFacing.FaceUp : GmParlorCardFacing.FaceDown);
        }

        if (snapshot.hasCurrentLeadCard)
        {
            GmCard physicalLead = snapshot.lastLeadWasAldric && snapshot.hasAldricPaidCard
                ? snapshot.aldricPaidCard : snapshot.currentLeadCard;
            AssignTableCard(bindings, physicalLead, snapshot.currentLeadCard,
                GmParlorCardZone.Lead, snapshot, isPlayerTableCard: !snapshot.lastLeadWasAldric);
        }

        if (snapshot.hasCurrentFollowCard)
        {
            GmCard physicalFollow = !snapshot.lastLeadWasAldric && snapshot.hasAldricPaidCard
                ? snapshot.aldricPaidCard : snapshot.currentFollowCard;
            AssignTableCard(bindings, physicalFollow, snapshot.currentFollowCard,
                GmParlorCardZone.Follow, snapshot, isPlayerTableCard: snapshot.lastLeadWasAldric);
        }

        var ordered = new GmParlorCardBinding[GmParlorCore.TotalCards];
        int output = 0;
        foreach (GmSuit suit in CanonicalSuits)
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
                ordered[output++] = bindings[new GmCard(suit, rank)];
        return ordered;
    }

    public static Vector3 LocalPosition(GmParlorCardBinding binding)
    {
        switch (binding.Zone)
        {
            case GmParlorCardZone.PlayerHand:
                return new Vector3((binding.Slot - 3f) * 0.14f,
                    0.026f + binding.Slot * 0.0005f, -0.25f + Math.Abs(binding.Slot - 3f) * 0.008f);
            case GmParlorCardZone.AldricHand:
                return new Vector3((3f - binding.Slot) * 0.14f,
                    0.026f + binding.Slot * 0.0005f, 0.25f - Math.Abs(binding.Slot - 3f) * 0.008f);
            case GmParlorCardZone.Lead:
                return new Vector3(-0.19f, 0.035f, 0f);
            case GmParlorCardZone.Follow:
                return new Vector3(0.19f, 0.036f, 0f);
            default:
                return new Vector3(0.40f, 0.018f + binding.Slot * 0.0032f, 0.14f);
        }
    }

    public static Quaternion LocalRotation(GmParlorCardBinding binding)
    {
        float yaw;
        switch (binding.Zone)
        {
            case GmParlorCardZone.PlayerHand:
                yaw = (binding.Slot - 3f) * -3.5f;
                break;
            case GmParlorCardZone.AldricHand:
                yaw = (binding.Slot - 3f) * 3.5f + 180f;
                break;
            case GmParlorCardZone.Lead:
                yaw = -3f;
                break;
            case GmParlorCardZone.Follow:
                yaw = 4f;
                break;
            default:
                yaw = binding.Slot % 2 == 0 ? -0.8f : 0.8f;
                break;
        }
        return Quaternion.Euler(binding.Facing == GmParlorCardFacing.FaceUp ? 90f : -90f, yaw, 0f);
    }

    static void Assign(Dictionary<GmCard, GmParlorCardBinding> bindings, GmCard physicalCard,
        GmCard displayCard, GmParlorCardZone zone, int slot, GmParlorCardFacing facing)
    {
        if (!bindings.TryGetValue(physicalCard, out GmParlorCardBinding existing))
            throw new ArgumentException($"Physical card is outside the canonical deck: {physicalCard}");
        if (existing.Zone != GmParlorCardZone.HousePile)
            throw new ArgumentException($"Physical card is assigned twice: {physicalCard}");
        bindings[physicalCard] = new GmParlorCardBinding(
            physicalCard, displayCard, zone, slot, facing);
    }

    static void AssignTableCard(Dictionary<GmCard, GmParlorCardBinding> bindings,
        GmCard physicalCard, GmCard displayCard, GmParlorCardZone zone,
        GmParlorMatchSnapshot snapshot, bool isPlayerTableCard)
    {
        if (!bindings.TryGetValue(physicalCard, out GmParlorCardBinding existing))
            throw new ArgumentException($"Physical card is outside the canonical deck: {physicalCard}");
        bool resolvedPhase = snapshot.phase == GmParlorMatchPhase.TrickResult ||
            snapshot.phase == GmParlorMatchPhase.RoundResult ||
            snapshot.phase == GmParlorMatchPhase.MatchResult;
        GmCard actualPlayerTableCard = snapshot.lastLeadWasAldric
            ? snapshot.currentFollowCard : snapshot.currentLeadCard;
        bool isVisibleReturnedBones = resolvedPhase && snapshot.bonesReturnedThisRound &&
            snapshot.lastTrickWinner == GmTrickOwner.Aldric && isPlayerTableCard &&
            physicalCard == actualPlayerTableCard && physicalCard.Suit == GmSuit.Bones &&
            existing.Zone == GmParlorCardZone.PlayerHand;
        if (existing.Zone != GmParlorCardZone.HousePile && !isVisibleReturnedBones)
            throw new ArgumentException($"Physical card is assigned twice: {physicalCard}");
        bindings[physicalCard] = new GmParlorCardBinding(physicalCard, displayCard,
            zone, 0, GmParlorCardFacing.FaceUp);
    }
}
