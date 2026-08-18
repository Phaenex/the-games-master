using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only deterministic seed catalog generator. Shipping review uses only the literal table
/// in GmParlorReviewProbe, so a player run never searches or rerolls for a favorable case.
/// </summary>
public static class GmParlorReviewSeedCatalogGenerator
{
    const int SearchLimit = 100000;

    static readonly GmParlorReviewCase[] Version1Templates =
    {
        new GmParlorReviewCase("honest-calm-flames", 0, 1, true,
            GmSuit.Flames, false, GmTellObservation.Calm),
        new GmParlorReviewCase("cheat-true-tell-eyes", 0, 4, true,
            GmSuit.Eyes, true, GmTellObservation.Suspicious,
            GmParlorCheatKind.RenegedWithHeldFlame,
            GmParlorReviewDecision.CorrectRead),
        new GmParlorReviewCase("honest-false-tell-teeth", 0, 4, true,
            GmSuit.Teeth, false, GmTellObservation.Suspicious,
            decision: GmParlorReviewDecision.FalseRead),
        new GmParlorReviewCase("cheat-calm-bones", 0, 4, true,
            GmSuit.Bones, true, GmTellObservation.Calm,
            GmParlorCheatKind.ImpossibleEighthRank),
        new GmParlorReviewCase("cheat-late-read-flames", 0, 4, true,
            GmSuit.Flames, true, GmTellObservation.Suspicious,
            GmParlorCheatKind.ImpossibleEighthRank,
            GmParlorReviewDecision.LateRead),
        new GmParlorReviewCase("honest-locked-read-eyes", 0, 1, false,
            GmSuit.Eyes, false, GmTellObservation.Calm,
            decision: GmParlorReviewDecision.LockedRead),
        new GmParlorReviewCase("bones-player-win-rematch", 0, 2, true,
            GmSuit.Bones, false, GmTellObservation.Calm,
            requiredMatchWinner: GmTrickOwner.Player, requireRematch: true),
        new GmParlorReviewCase("aldric-win-restore", 0, 3, true,
            GmSuit.Flames, true, GmTellObservation.Calm,
            GmParlorCheatKind.ImpossibleEighthRank,
            requiredMatchWinner: GmTrickOwner.Aldric, requireRestore: true),
    };

    public static GmParlorReviewCase[] GenerateVersion1()
    {
        var result = new GmParlorReviewCase[Version1Templates.Length];
        for (int index = 0; index < Version1Templates.Length; index++)
        {
            GmParlorReviewCase template = Version1Templates[index];
            int seed = FindSeed(template);
            result[index] = WithSeed(template, seed);
        }
        return result;
    }

    public static bool Validate(GmParlorReviewCase item, out string error)
    {
        if (!TryOpenFirstJudgement(item, out GmParlorMatch match, out error)) return false;
        if (match.AldricCheated != item.ExpectCheat)
            return Fail($"cheat truth was {match.AldricCheated}", out error);
        if (match.TellObservation != item.ExpectedObservation)
            return Fail($"observation was {match.TellObservation}", out error);
        if (match.AldricCheatKind != item.ExpectedCheatKind)
            return Fail($"cheat family was {match.AldricCheatKind}", out error);
        if (!ResolveFirstDecision(match, item, out error)) return false;
        if (item.RequiredMatchWinner.HasValue)
        {
            if (!DriveToMatchResult(match, item.RequiredMatchWinner.Value, out error)) return false;
            if (match.MatchWinner != item.RequiredMatchWinner.Value)
                return Fail($"match winner was {match.MatchWinner}", out error);
            if (item.RequireRematch)
            {
                if (match.StartRematch() != GmParlorActionError.None ||
                    match.Phase == GmParlorMatchPhase.MatchResult)
                    return Fail("rematch did not leave MatchResult", out error);
            }
        }
        error = string.Empty;
        return true;
    }

    [MenuItem("GamesMaster/Scenes/Print Parlor Review Seed Catalog")]
    public static void PrintVersion1()
    {
        foreach (GmParlorReviewCase item in GenerateVersion1())
            Debug.Log($"[GmParlorSeedCatalog] {item.Id} seed={item.Seed}");
    }

    static int FindSeed(GmParlorReviewCase template)
    {
        for (int seed = 1; seed <= SearchLimit; seed++)
        {
            GmParlorReviewCase candidate = WithSeed(template, seed);
            if (Validate(candidate, out _)) return seed;
        }
        throw new InvalidOperationException(
            $"No seed <= {SearchLimit} satisfies Parlor review case {template.Id}");
    }

    static bool TryOpenFirstJudgement(GmParlorReviewCase item, out GmParlorMatch match,
        out string error)
    {
        match = new GmParlorMatch(item.Seed, item.CorruptionTier, 0, item.ReadUnlocked);
        if (match.Start() != GmParlorActionError.None)
            return Fail("match did not start", out error);
        GmParlorMatch activeMatch = match;
        int index = Enumerable.Range(0, activeMatch.PlayerHand.Count)
            .Where(cardIndex => activeMatch.PlayerHand[cardIndex].Suit == item.RequiredSuit)
            .Where(cardIndex => activeMatch.GetPlayerCardError(cardIndex) == GmParlorActionError.None)
            .OrderBy(cardIndex => activeMatch.PlayerHand[cardIndex].Rank)
            .DefaultIfEmpty(-1)
            .First();
        if (index < 0) return Fail($"player has no legal {item.RequiredSuit}", out error);
        if (match.PlayPlayerCard(index) != GmParlorActionError.None ||
            match.Phase != GmParlorMatchPhase.AwaitingAldricJudgement)
            return Fail("public first play did not open judgement", out error);
        error = string.Empty;
        return true;
    }

    static bool ResolveFirstDecision(GmParlorMatch match, GmParlorReviewCase item,
        out string error)
    {
        GmParlorActionError action;
        switch (item.Decision)
        {
            case GmParlorReviewDecision.CorrectRead:
                action = match.Read();
                if (action != GmParlorActionError.None ||
                    match.LastOutcome.Kind != GmParlorOutcomeKind.CheatCaught)
                    return Fail($"correct Read produced {action}/{match.LastOutcome.Kind}", out error);
                break;
            case GmParlorReviewDecision.FalseRead:
                action = match.Read();
                if (action != GmParlorActionError.None ||
                    match.LastOutcome.Kind != GmParlorOutcomeKind.FalseReadPenalty)
                    return Fail($"false Read produced {action}/{match.LastOutcome.Kind}", out error);
                break;
            case GmParlorReviewDecision.LateRead:
                if (match.ContinueJudgement() != GmParlorActionError.None ||
                    match.Read() != GmParlorActionError.WrongPhase)
                    return Fail("late Read was not rejected after accept", out error);
                break;
            case GmParlorReviewDecision.LockedRead:
                if (match.Read() != GmParlorActionError.ReadLocked ||
                    match.ContinueJudgement() != GmParlorActionError.None)
                    return Fail("locked Read did not reject then preserve accept", out error);
                break;
            default:
                if (match.ContinueJudgement() != GmParlorActionError.None)
                    return Fail("accept did not resolve judgement", out error);
                break;
        }
        Acknowledge(match);
        error = string.Empty;
        return true;
    }

    static bool DriveToMatchResult(GmParlorMatch match, GmTrickOwner desiredWinner,
        out string error)
    {
        int guard = 256;
        while (match.Phase != GmParlorMatchPhase.MatchResult && guard-- > 0)
        {
            switch (match.Phase)
            {
                case GmParlorMatchPhase.PlayerLeads:
                case GmParlorMatchPhase.PlayerFollowsAldricLead:
                    int index = ChoosePlayerCard(match, desiredWinner);
                    if (index < 0 || match.PlayPlayerCard(index) != GmParlorActionError.None)
                        return Fail("deterministic player policy could not play", out error);
                    break;
                case GmParlorMatchPhase.AwaitingAldricJudgement:
                    if (match.ContinueJudgement() != GmParlorActionError.None)
                        return Fail("deterministic player policy could not accept", out error);
                    break;
                case GmParlorMatchPhase.TrickResult:
                case GmParlorMatchPhase.RoundResult:
                    if (match.Continue() != GmParlorActionError.None)
                        return Fail("deterministic player policy could not continue", out error);
                    break;
                default:
                    return Fail($"unexpected phase {match.Phase}", out error);
            }
            Acknowledge(match);
        }
        if (match.Phase != GmParlorMatchPhase.MatchResult)
            return Fail("match policy exhausted its transition guard", out error);
        error = string.Empty;
        return true;
    }

    static int ChoosePlayerCard(GmParlorMatch match, GmTrickOwner desiredWinner)
    {
        var legal = new List<int>();
        for (int index = 0; index < match.PlayerHand.Count; index++)
            if (match.GetPlayerCardError(index) == GmParlorActionError.None) legal.Add(index);
        if (legal.Count == 0) return -1;
        if (match.Phase == GmParlorMatchPhase.PlayerFollowsAldricLead)
        {
            bool WantsWin(int index) => !GmParlorCore.LeadWins(
                match.CurrentLeadCard.Value, match.PlayerHand[index]);
            IEnumerable<int> preferred = desiredWinner == GmTrickOwner.Player
                ? legal.Where(WantsWin) : legal.Where(index => !WantsWin(index));
            if (preferred.Any()) legal = preferred.ToList();
        }
        return desiredWinner == GmTrickOwner.Player
            ? legal.OrderByDescending(index => match.PlayerHand[index].Suit == GmSuit.Flames)
                .ThenByDescending(index => match.PlayerHand[index].Rank).First()
            : legal.OrderBy(index => match.PlayerHand[index].Suit == GmSuit.Flames)
                .ThenBy(index => match.PlayerHand[index].Rank).First();
    }

    static void Acknowledge(GmParlorMatch match)
    {
        if (!match.TryPeekOutcome(out _, out ulong sequence)) return;
        match.MarkOutcomeDurable(sequence);
        match.AcknowledgeOutcome(sequence);
    }

    static GmParlorReviewCase WithSeed(GmParlorReviewCase item, int seed) =>
        new GmParlorReviewCase(item.Id, seed, item.CorruptionTier, item.ReadUnlocked,
            item.RequiredSuit, item.ExpectCheat, item.ExpectedObservation,
            item.ExpectedCheatKind, item.Decision, item.RequiredMatchWinner,
            item.RequireRematch, item.RequireRestore);

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
