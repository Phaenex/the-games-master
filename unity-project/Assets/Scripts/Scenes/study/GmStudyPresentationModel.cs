using System;
using System.Collections.Generic;

public enum GmStudyPresentationPhase
{
    Move,
    Intervention,
    Complete
}

public sealed class GmStudyActionPresentation
{
    public int FocusIndex { get; }
    public string Label { get; }
    public bool Selected { get; }
    public bool Legal { get; }

    public GmStudyActionPresentation(int focusIndex, string label, bool selected, bool legal)
    {
        FocusIndex = focusIndex;
        Label = label ?? string.Empty;
        Selected = selected;
        Legal = legal;
    }
}

/// <summary>Clone-owned, player-observable Study facts. It intentionally contains no replay state.</summary>
public sealed class GmStudyPresentationState
{
    string[] actionLog = Array.Empty<string>();
    string[] evidenceFacts = Array.Empty<string>();
    GmStudyActionPresentation[] actions = Array.Empty<GmStudyActionPresentation>();

    public GmStudyPresentationPhase Phase { get; internal set; }
    public int FocusIndex { get; internal set; }
    public int PositionIndex { get; internal set; }
    public string PositionTitle { get; internal set; } = string.Empty;
    public int CorrectCount { get; internal set; }
    public bool HasResult { get; internal set; }
    public GmStudyMatchResult Result { get; internal set; }
    public bool CanChallenge { get; internal set; }
    public bool CanProceed { get; internal set; }
    public bool HasArbiterMarker { get; internal set; }
    public bool CorrectedByChallenge { get; internal set; }
    public string OverrideFromSquare { get; internal set; } = string.Empty;
    public string OverrideToSquare { get; internal set; } = string.Empty;
    public string CapturedPiece { get; internal set; } = string.Empty;
    public string EvidenceIconId { get; internal set; } = string.Empty;
    public string EvidenceText { get; internal set; } = string.Empty;
    public string ShownFen { get; internal set; } = string.Empty;
    public string ObservedOriginalFen { get; internal set; } = string.Empty;
    public string AlteredFen { get; internal set; } = string.Empty;
    public string[] EvidenceFacts { get => (string[])evidenceFacts.Clone(); internal set => evidenceFacts = CloneStrings(value); }
    public string[] ActionLog { get => (string[])actionLog.Clone(); set => actionLog = CloneStrings(value); }
    public GmStudyActionPresentation[] Actions { get => (GmStudyActionPresentation[])actions.Clone(); internal set => actions = value == null ? Array.Empty<GmStudyActionPresentation>() : (GmStudyActionPresentation[])value.Clone(); }

    static string[] CloneStrings(string[] value) => value == null ? Array.Empty<string>() : (string[])value.Clone();
}

public static class GmStudyPresentationModel
{
    public static GmStudyPresentationState Project(GmStudyController controller)
    {
        if (controller == null || !controller.IsInitialized)
            throw new ArgumentException("Study presentation needs an initialized controller", nameof(controller));
        GmStudyMatchSnapshot snapshot = controller.Snapshot;
        bool intervention = snapshot.phase == GmStudyMatchPhase.AwaitingIntervention;
        bool complete = snapshot.phase == GmStudyMatchPhase.Complete;
        bool challenged = complete && snapshot.session != null && snapshot.session.intervention != null &&
            snapshot.session.intervention.resolved && snapshot.session.intervention.challenged;
        GmStudyPosition position = GmStudyRules.GetPosition(snapshot.positionIndex);
        GmStudyInterventionReceipt receipt = snapshot.interventionReceipt;
        var state = new GmStudyPresentationState
        {
            Phase = intervention ? GmStudyPresentationPhase.Intervention :
                complete ? GmStudyPresentationPhase.Complete : GmStudyPresentationPhase.Move,
            FocusIndex = controller.FocusIndex,
            PositionIndex = snapshot.positionIndex,
            PositionTitle = position.title,
            CorrectCount = snapshot.correctCount,
            HasResult = snapshot.hasResult,
            Result = snapshot.result,
            CanChallenge = intervention,
            CanProceed = intervention,
            HasArbiterMarker = receipt != null,
            CorrectedByChallenge = challenged,
            OverrideFromSquare = receipt?.overrideFromSquare ?? string.Empty,
            OverrideToSquare = receipt?.overrideToSquare ?? string.Empty,
            CapturedPiece = receipt?.capturedPiece ?? string.Empty,
            EvidenceIconId = receipt?.evidenceIconId ?? string.Empty,
            EvidenceFacts = receipt?.evidenceFacts ?? Array.Empty<string>(),
            EvidenceText = receipt == null ? string.Empty :
                challenged ? "Arbiter override challenged. The honest position was restored; the altered board display remains on record." :
                intervention ? "Aldric applied an arbiter override to this position. Challenge or proceed." :
                "The altered arbiter-override board display remains on record.",
            ShownFen = snapshot.currentFen,
            ObservedOriginalFen = challenged ? receipt?.originalFen ?? string.Empty : string.Empty,
            AlteredFen = receipt?.alteredFen ?? string.Empty,
            ActionLog = PresentLog(snapshot.actionJournal),
            Actions = PresentActions(controller.FocusIndex, snapshot.phase, position)
        };
        return state;
    }

    static GmStudyActionPresentation[] PresentActions(int focus, GmStudyMatchPhase phase,
        GmStudyPosition position)
    {
        bool legal = phase == GmStudyMatchPhase.AwaitingMove;
        var result = new GmStudyActionPresentation[position.cards.Length];
        for (int index = 0; index < position.cards.Length; index++)
        {
            GmStudyMoveCard card = position.cards[index];
            result[index] = new GmStudyActionPresentation(index,
                $"{card.notation} — {card.consequence}", focus == index, legal);
        }
        return result;
    }

    static string[] PresentLog(string[] journal)
    {
        if (journal == null || journal.Length == 0) return Array.Empty<string>();
        var rows = new List<string>(journal.Length);
        int position = 0;
        foreach (string action in journal)
        {
            if (action == "intervention:challenge") { rows.Add("Challenged the arbiter override"); continue; }
            if (action == "intervention:proceed") { rows.Add("Proceeded past the arbiter override"); continue; }
            rows.Add($"Position {position + 1}: {NotationFor(position, action)}");
            position++;
        }
        return rows.ToArray();
    }

    static string NotationFor(int positionIndex, string actionId)
    {
        GmStudyRules.TryGetCard(positionIndex, actionId, out GmStudyMoveCard card);
        return card?.notation ?? actionId;
    }
}
