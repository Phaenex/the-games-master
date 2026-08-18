using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public sealed class GmParlorPropBinderTests
{
    [Test]
    public void StartedRoundBindsAllTwentyEightPhysicalCardsExactlyOnce()
    {
        GmParlorMatch match = NewStartedMatch(42);

        GmParlorCardBinding[] bindings = GmParlorTableLayout.Build(match.ExportSnapshot());

        Assert.That(bindings, Has.Length.EqualTo(GmParlorCore.TotalCards));
        Assert.That(bindings.Select(binding => binding.PhysicalCard).Distinct().Count(),
            Is.EqualTo(GmParlorCore.TotalCards));
        Assert.That(bindings.Count(binding => binding.Zone == GmParlorCardZone.PlayerHand), Is.EqualTo(7));
        Assert.That(bindings.Count(binding => binding.Zone == GmParlorCardZone.AldricHand), Is.EqualTo(7));
        Assert.That(bindings.Count(binding => binding.Zone == GmParlorCardZone.HousePile), Is.EqualTo(14));
        Assert.That(bindings.Where(binding => binding.Zone == GmParlorCardZone.PlayerHand),
            Is.All.Matches<GmParlorCardBinding>(binding =>
                binding.Facing == GmParlorCardFacing.FaceUp));
        Assert.That(bindings.Where(binding => binding.Zone == GmParlorCardZone.AldricHand),
            Is.All.Matches<GmParlorCardBinding>(binding =>
                binding.Facing == GmParlorCardFacing.FaceDown));
    }

    [Test]
    public void AldricImpossibleEightUsesThePaidPhysicalCardWithAVisibleDisplayOverride()
    {
        GmParlorMatch match = FindImpossibleEight();
        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();

        GmParlorCardBinding follow = GmParlorTableLayout.Build(snapshot)
            .Single(binding => binding.Zone == GmParlorCardZone.Follow);

        Assert.That(snapshot.currentFollowCard.Rank, Is.EqualTo(8));
        Assert.That(follow.PhysicalCard, Is.EqualTo(snapshot.aldricPaidCard));
        Assert.That(follow.DisplayCard, Is.EqualTo(snapshot.currentFollowCard));
        Assert.That(follow.Facing, Is.EqualTo(GmParlorCardFacing.FaceUp));
    }

    [Test]
    public void EyesRevealAndTeethMemoryTurnOnlyCanonicalAldricCardsFaceUp()
    {
        GmParlorMatch match = NewStartedMatch(91);
        GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
        snapshot.eyesExposeAldricHand = true;
        snapshot.revealedAldricCards.Add(snapshot.aldricHand[0]);

        GmParlorCardBinding[] bindings = GmParlorTableLayout.Build(snapshot);

        Assert.That(bindings.Where(binding => binding.Zone == GmParlorCardZone.AldricHand),
            Is.All.Matches<GmParlorCardBinding>(binding =>
                binding.Facing == GmParlorCardFacing.FaceUp));

        snapshot.eyesExposeAldricHand = false;
        bindings = GmParlorTableLayout.Build(snapshot);
        Assert.That(bindings.Single(binding =>
            binding.PhysicalCard == snapshot.revealedAldricCards[0]).Facing,
            Is.EqualTo(GmParlorCardFacing.FaceUp));
        Assert.That(bindings.Count(binding => binding.Zone == GmParlorCardZone.AldricHand &&
            binding.Facing == GmParlorCardFacing.FaceUp), Is.EqualTo(1));
    }

    [Test]
    public void ReturnedBonesPriorityRequiresExactResolvedAldricWinPlayerTableCard()
    {
        GmParlorMatchSnapshot valid = FindReturnedBonesSnapshot();
        GmCard playerTableCard = valid.lastLeadWasAldric
            ? valid.currentFollowCard : valid.currentLeadCard;
        Assert.That(valid.phase, Is.EqualTo(GmParlorMatchPhase.TrickResult));
        Assert.That(valid.bonesReturnedThisRound, Is.True);
        Assert.That(valid.lastTrickWinner, Is.EqualTo(GmTrickOwner.Aldric));
        Assert.That(valid.playerHand, Does.Contain(playerTableCard));
        Assert.That(GmParlorTableLayout.Build(valid).Single(binding =>
            binding.PhysicalCard == playerTableCard).Zone,
            Is.EqualTo(valid.lastLeadWasAldric
                ? GmParlorCardZone.Follow : GmParlorCardZone.Lead));

        GmParlorMatchSnapshot wrongPhase = valid.DeepCopy();
        wrongPhase.phase = GmParlorMatchPhase.AwaitingAldricJudgement;
        Assert.That(() => GmParlorTableLayout.Build(wrongPhase),
            Throws.ArgumentException.With.Message.Contains("assigned twice"));

        GmParlorMatchSnapshot wrongWinner = valid.DeepCopy();
        wrongWinner.lastTrickWinner = GmTrickOwner.Player;
        Assert.That(() => GmParlorTableLayout.Build(wrongWinner),
            Throws.ArgumentException.With.Message.Contains("assigned twice"));

        GmParlorMatchSnapshot wrongPhysical = valid.DeepCopy();
        GmCard paidReplacement = FindUnusedBones(wrongPhysical);
        wrongPhysical.playerHand.Add(paidReplacement);
        wrongPhysical.aldricPaidCard = paidReplacement;
        wrongPhysical.hasAldricPaidCard = true;
        Assert.That(() => GmParlorTableLayout.Build(wrongPhysical),
            Throws.ArgumentException.With.Message.Contains("assigned twice"));
    }

    [Test]
    public void BinderRejectsMissingOrDuplicatePhysicalViewsBeforeMovingAnything()
    {
        GameObject root = new GameObject("ParlorTableRoot");
        try
        {
            GmParlorCardView[] views = CreateViews(root.transform).ToArray();
            views[27].Configure(views[0].PhysicalCard);
            var binder = root.AddComponent<GmParlorPropBinder>();

            Assert.That(binder.TryConfigure(views, out string error), Is.False);
            StringAssert.Contains("duplicate", error.ToLowerInvariant());
            Assert.That(binder.IsConfigured, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void BinderAppliesEveryCanonicalPoseAndDisplayIdentity()
    {
        GameObject root = new GameObject("ParlorTableRoot");
        try
        {
            GmParlorCardView[] views = CreateViews(root.transform).ToArray();
            var binder = root.AddComponent<GmParlorPropBinder>();
            Assert.That(binder.TryConfigure(views, out string configureError), Is.True, configureError);
            GmParlorMatch match = NewStartedMatch(117);
            Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));

            Assert.That(binder.TryApply(match.ExportSnapshot(), out string applyError), Is.True, applyError);

            foreach (GmParlorCardView view in views)
            {
                GmParlorCardBinding expected = GmParlorTableLayout.Build(match.ExportSnapshot())
                    .Single(binding => binding.PhysicalCard == view.PhysicalCard);
                Assert.That(view.Binding, Is.EqualTo(expected));
                Assert.That(view.transform.localPosition,
                    Is.EqualTo(GmParlorTableLayout.LocalPosition(expected)).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(view.DisplayCard, Is.EqualTo(expected.DisplayCard));
                Assert.That(view.IsFaceUp, Is.EqualTo(expected.Facing == GmParlorCardFacing.FaceUp));
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void EveryPlayableCardPoseStaysInsideTheMeasuredBaizeFootprint()
    {
        GmParlorMatchSnapshot snapshot = NewStartedMatch(42).ExportSnapshot();
        snapshot.hasCurrentLeadCard = true;
        snapshot.currentLeadCard = snapshot.playerHand[0];
        snapshot.playerHand.RemoveAt(0);
        snapshot.hasCurrentFollowCard = true;
        snapshot.currentFollowCard = snapshot.aldricHand[0];
        snapshot.hasAldricPaidCard = true;
        snapshot.aldricPaidCard = snapshot.aldricHand[0];
        snapshot.aldricHand.RemoveAt(0);
        snapshot.lastLeadWasAldric = false;

        foreach (GmParlorCardBinding binding in GmParlorTableLayout.Build(snapshot))
        {
            Vector3 position = GmParlorTableLayout.LocalPosition(binding);
            Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(0.50f),
                $"{binding.PhysicalCard} leaves the baize on X in {binding.Zone}");
            Assert.That(Mathf.Abs(position.z), Is.LessThanOrEqualTo(0.36f),
                $"{binding.PhysicalCard} leaves the baize on Z in {binding.Zone}");
        }
    }

    static GmParlorMatch NewStartedMatch(int seed)
    {
        var match = new GmParlorMatch(seed, 4, 8, true);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }

    static GmParlorMatch FindImpossibleEight()
    {
        for (int seed = 1; seed < 50000; seed++)
        {
            GmParlorMatch match = NewStartedMatch(seed);
            for (int card = 0; card < match.PlayerHand.Count; card++)
            {
                GmParlorMatch candidate = NewStartedMatch(seed);
                if (candidate.PlayPlayerCard(card) == GmParlorActionError.None &&
                    candidate.AldricCheatKind == GmParlorCheatKind.ImpossibleEighthRank)
                    return candidate;
            }
        }
        Assert.Fail("No deterministic impossible-eight fixture found");
        return null;
    }

    static GmParlorMatchSnapshot FindReturnedBonesSnapshot()
    {
        for (int seed = 1; seed <= 1000; seed++)
        {
            GmParlorMatch match = NewStartedMatch(seed);
            int guard = 400;
            while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
            {
                if (match.Phase == GmParlorMatchPhase.PlayerLeads ||
                    match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
                {
                    int legal = Enumerable.Range(0, match.PlayerHand.Count).First(index =>
                        match.GetPlayerCardError(index) == GmParlorActionError.None);
                    Assert.That(match.PlayPlayerCard(legal), Is.EqualTo(GmParlorActionError.None));
                }
                else if (match.Phase == GmParlorMatchPhase.AwaitingAldricJudgement)
                    Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
                else
                    Assert.That(match.Continue(), Is.EqualTo(GmParlorActionError.None));
                if (match.TryPeekOutcome(out _, out ulong sequence))
                {
                    Assert.That(match.MarkOutcomeDurable(sequence), Is.True);
                    Assert.That(match.AcknowledgeOutcome(sequence), Is.True);
                }
                GmParlorMatchSnapshot snapshot = match.ExportSnapshot();
                if (snapshot.phase != GmParlorMatchPhase.TrickResult ||
                    !snapshot.bonesReturnedThisRound ||
                    snapshot.lastTrickWinner != GmTrickOwner.Aldric) continue;
                GmCard playerCard = snapshot.lastLeadWasAldric
                    ? snapshot.currentFollowCard : snapshot.currentLeadCard;
                if (snapshot.playerHand.Contains(playerCard)) return snapshot;
            }
        }
        Assert.Fail("No deterministic returned-Bones snapshot found");
        return null;
    }

    static GmCard FindUnusedBones(GmParlorMatchSnapshot snapshot)
    {
        for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
        {
            var card = new GmCard(GmSuit.Bones, rank);
            if (snapshot.playerHand.Contains(card) || snapshot.aldricHand.Contains(card) ||
                (snapshot.hasCurrentLeadCard && snapshot.currentLeadCard == card) ||
                (snapshot.hasCurrentFollowCard && snapshot.currentFollowCard == card) ||
                (snapshot.hasAldricPaidCard && snapshot.aldricPaidCard == card)) continue;
            return card;
        }
        throw new AssertionException("No unused Bones card for negative duplicate proof");
    }

    static System.Collections.Generic.IEnumerable<GmParlorCardView> CreateViews(Transform parent)
    {
        foreach (GmSuit suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones })
        {
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
            {
                var card = new GameObject($"{suit}_{rank}");
                card.transform.SetParent(parent, false);
                GmParlorCardView view = card.AddComponent<GmParlorCardView>();
                view.Configure(new GmCard(suit, rank));
                yield return view;
            }
        }
    }
}
