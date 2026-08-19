using System;
using System.Collections.Generic;

public enum GmBonesPresentationPhase
{
    PlayerChoice,
    Intervention,
    Complete
}

public sealed class GmBonesActionPresentation
{
    public int FocusIndex { get; }
    public string Label { get; }
    public bool Selected { get; }
    public bool Legal { get; }

    public GmBonesActionPresentation(int focusIndex, string label, bool selected, bool legal)
    {
        FocusIndex = focusIndex;
        Label = label ?? string.Empty;
        Selected = selected;
        Legal = legal;
    }
}

/// <summary>Clone-owned, player-observable Bones facts. It intentionally contains no replay state.</summary>
public sealed class GmBonesPresentationState
{
    int[] dice = Array.Empty<int>();
    int[] displayedReroll = Array.Empty<int>();
    int[] observedHonestReroll = Array.Empty<int>();
    string[] actionLog = Array.Empty<string>();
    GmBonesActionPresentation[] actions = Array.Empty<GmBonesActionPresentation>();

    public GmBonesPresentationPhase Phase { get; internal set; }
    public int FocusIndex { get; internal set; }
    public int Round { get; internal set; }
    public int PlayerTotal { get; internal set; }
    public int AldricTotal { get; internal set; }
    public bool HasResult { get; internal set; }
    public GmBonesMatchResult Result { get; internal set; }
    public bool CanChallenge { get; internal set; }
    public bool CanProceed { get; internal set; }
    public bool HasLoadedSixMarker { get; internal set; }
    public bool CorrectedByChallenge { get; internal set; }
    public int ChangedDieSlot { get; internal set; } = -1;
    public string EvidenceText { get; internal set; } = string.Empty;
    public int[] Dice { get => (int[])dice.Clone(); internal set => dice = Clone(value); }
    public int[] DisplayedReroll { get => (int[])displayedReroll.Clone(); internal set => displayedReroll = Clone(value); }
    public int[] ObservedHonestReroll { get => (int[])observedHonestReroll.Clone(); internal set => observedHonestReroll = Clone(value); }
    public string[] ActionLog { get => (string[])actionLog.Clone(); set => actionLog = value == null ? Array.Empty<string>() : (string[])value.Clone(); }
    public GmBonesActionPresentation[] Actions { get => (GmBonesActionPresentation[])actions.Clone(); internal set => actions = value == null ? Array.Empty<GmBonesActionPresentation>() : (GmBonesActionPresentation[])value.Clone(); }

    static int[] Clone(int[] value) => value == null ? Array.Empty<int>() : (int[])value.Clone();
}

public static class GmBonesPresentationModel
{
    public static GmBonesPresentationState Project(GmBonesController controller)
    {
        if (controller == null || !controller.IsInitialized)
            throw new ArgumentException("Bones presentation needs an initialized controller", nameof(controller));
        GmBonesMatchSnapshot snapshot = controller.Snapshot;
        bool intervention = snapshot.phase == GmBonesMatchPhase.AwaitingIntervention;
        bool complete = snapshot.phase == GmBonesMatchPhase.Complete;
        bool challenged = complete && snapshot.session != null && snapshot.session.intervention != null &&
            snapshot.session.intervention.resolved && snapshot.session.intervention.challenged;
        var state = new GmBonesPresentationState
        {
            Phase = intervention ? GmBonesPresentationPhase.Intervention :
                complete ? GmBonesPresentationPhase.Complete : GmBonesPresentationPhase.PlayerChoice,
            FocusIndex = controller.FocusIndex,
            Round = snapshot.round,
            PlayerTotal = snapshot.playerTotal,
            AldricTotal = snapshot.aldricTotal,
            HasResult = snapshot.hasResult,
            Result = snapshot.result,
            CanChallenge = intervention,
            CanProceed = intervention,
            HasLoadedSixMarker = snapshot.interventionReceipt != null,
            CorrectedByChallenge = challenged,
            ChangedDieSlot = ChangedTableSlot(snapshot.interventionReceipt),
            EvidenceText = snapshot.interventionReceipt == null ? string.Empty :
                challenged ? "Loaded six challenged. The honest result was restored; the altered table display remains on record." :
                intervention ? "Aldric's displayed reroll includes a loaded six. Challenge or proceed." :
                "The altered loaded-six table display remains on record.",
            Dice = ReceiptDice(snapshot, challenged),
            DisplayedReroll = snapshot.interventionReceipt?.displayedReroll,
            ObservedHonestReroll = challenged
                ? snapshot.interventionReceipt?.honestReroll : Array.Empty<int>(),
            ActionLog = PresentLog(snapshot.actionJournal),
            Actions = PresentActions(controller.FocusIndex, snapshot.phase)
        };
        return state;
    }

    static int[] ReceiptDice(GmBonesMatchSnapshot snapshot, bool challenged)
    {
        GmBonesInterventionReceipt receipt = snapshot.interventionReceipt;
        if (receipt == null) return snapshot.currentDice;
        var shown = new int[3];
        shown[receipt.lockedSlot] = snapshot.currentDice[receipt.lockedSlot];
        int[] reroll = challenged ? receipt.honestReroll : receipt.displayedReroll;
        int next = 0;
        for (int index = 0; index < shown.Length; index++)
            if (index != receipt.lockedSlot) shown[index] = reroll[next++];
        return shown;
    }

    static int ChangedTableSlot(GmBonesInterventionReceipt receipt)
    {
        if (receipt == null) return -1;
        int rerollSlot = 0;
        for (int tableSlot = 0; tableSlot < 3; tableSlot++)
        {
            if (tableSlot == receipt.lockedSlot) continue;
            if (rerollSlot++ == receipt.changedSlot) return tableSlot;
        }
        return -1;
    }

    static GmBonesActionPresentation[] PresentActions(int focus, GmBonesMatchPhase phase)
    {
        bool legal = phase == GmBonesMatchPhase.AwaitingPlayerChoice;
        return new[]
        {
            new GmBonesActionPresentation(0, "Bank this throw", focus == 0, legal),
            new GmBonesActionPresentation(1, "Press, lock die 1", focus == 1, legal),
            new GmBonesActionPresentation(2, "Press, lock die 2", focus == 2, legal),
            new GmBonesActionPresentation(3, "Press, lock die 3", focus == 3, legal)
        };
    }

    static string[] PresentLog(string[] journal)
    {
        if (journal == null || journal.Length == 0) return Array.Empty<string>();
        var rows = new List<string>(journal.Length);
        foreach (string action in journal)
        {
            string[] parts = action.Split(':');
            string round = parts[0].Replace("round-", "Round ");
            rows.Add(parts[1] == "bank" ? $"{round}: Bank" :
                $"{round}: Press, locked die {int.Parse(parts[2]) + 1}");
        }
        return rows.ToArray();
    }
}
