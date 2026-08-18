using System;
using UnityEngine;

/// <summary>
/// Floor 1 library puzzle: five titled books on a sagging brass rail. Arranged I-V they click,
/// the case slides, and <see cref="LeverClueId"/> banks so the under-stair cellar panel can open.
/// The aisle stays walkable whether or not the player solves it.
/// </summary>
[DisallowMultipleComponent]
public sealed class GmWeightedShelf : MonoBehaviour, IGmInteractionReceiver
{
    public const string LeverClueId = "clue:library_lever";
    public const string BookIdPrefix = "weighted-book-";
    public const int SlotCount = 5;

    public static readonly string[] Titles =
    {
        "Opening Gambit",
        "First Principles",
        "The Middlegame",
        "Endgame Studies",
        "The Final Move"
    };

    public static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

    /// Bible starting order: V, I, III, IV, II. Slot 2 already holds III.
    public static readonly int[] StartingOrder = { 4, 0, 2, 3, 1 };

    [SerializeField] Transform[] books = new Transform[SlotCount];
    [SerializeField] Vector3[] slotPositions = new Vector3[SlotCount];
    [SerializeField] Quaternion[] slotRotations = new Quaternion[SlotCount];
    [SerializeField] Transform secretCase;
    [SerializeField] Transform brassLever;
    [SerializeField] Transform[] tiltOnSolve = Array.Empty<Transform>();
    [SerializeField] Vector3 secretOpenOffset = new Vector3(0.38f, 0f, 0f);

    int[] order = (int[])StartingOrder.Clone();
    int selectedSlot = -1;
    bool solved;
    Vector3 secretClosedLocal;

    public bool IsSolved => solved;
    public int SelectedSlot => selectedSlot;
    public int[] CurrentOrder => (int[])order.Clone();

    public void Bind(Transform[] bookRoots, Vector3[] worldSlots, Quaternion[] worldSlotRotations,
        Transform slidingCase, Transform lever, Transform[] atmosphereTilt, Vector3 openOffset)
    {
        if (bookRoots == null || bookRoots.Length != SlotCount)
            throw new ArgumentException("Weighted Shelf needs five book roots", nameof(bookRoots));
        if (worldSlots == null || worldSlots.Length != SlotCount)
            throw new ArgumentException("Weighted Shelf needs five slot positions", nameof(worldSlots));

        books = bookRoots;
        slotPositions = worldSlots;
        slotRotations = worldSlotRotations ?? new Quaternion[SlotCount];
        if (slotRotations.Length != SlotCount)
            slotRotations = new Quaternion[SlotCount];
        secretCase = slidingCase;
        brassLever = lever;
        tiltOnSolve = atmosphereTilt ?? Array.Empty<Transform>();
        secretOpenOffset = openOffset;
        if (secretCase != null)
            secretClosedLocal = secretCase.localPosition;
        order = (int[])StartingOrder.Clone();
        selectedSlot = -1;
        solved = false;
        ApplyPose();
        if (brassLever != null)
            brassLever.gameObject.SetActive(false);
        if (GmRunStore.HasClue(LeverClueId))
            Solve(silent: true);
    }

    public void OnGmInteraction(GmInteractable source)
    {
        if (source == null || solved) return;
        if (!TryParseBookId(source.InteractionId, out int bookId)) return;
        int slot = SlotOf(bookId);
        if (slot < 0) return;
        HandleSlot(slot);
    }

    public bool HandleSlot(int slot)
    {
        if (solved || slot < 0 || slot >= SlotCount) return false;
        if (selectedSlot < 0)
        {
            selectedSlot = slot;
            ShowFeedback($"You lift {Titles[order[slot]]}. The brass rail has a notch waiting.");
            return true;
        }

        if (selectedSlot == slot)
        {
            selectedSlot = -1;
            ShowFeedback($"{Titles[order[slot]]} sits back in its notch.");
            return true;
        }

        SwapSlots(selectedSlot, slot);
        selectedSlot = -1;
        if (OrderIsCorrect())
            Solve(silent: false);
        else
            ShowFeedback("The volumes settle. The shelf still sags.");
        return true;
    }

    public void SwapSlots(int a, int b)
    {
        if (a < 0 || b < 0 || a >= SlotCount || b >= SlotCount || a == b) return;
        int hold = order[a];
        order[a] = order[b];
        order[b] = hold;
        ApplyPose();
    }

    public static bool TryParseBookId(string interactionId, out int bookId)
    {
        bookId = -1;
        if (string.IsNullOrEmpty(interactionId) || !interactionId.StartsWith(BookIdPrefix, StringComparison.Ordinal))
            return false;
        return int.TryParse(interactionId.Substring(BookIdPrefix.Length), out bookId)
            && bookId >= 0 && bookId < SlotCount;
    }

    public static string BookInteractionId(int bookId) => BookIdPrefix + bookId;

    public static string SpineLabel(int bookId) =>
        $"{Numerals[bookId]}  {Titles[bookId]}";

    bool OrderIsCorrect()
    {
        for (int i = 0; i < SlotCount; i++)
            if (order[i] != i) return false;
        return true;
    }

    int SlotOf(int bookId)
    {
        for (int i = 0; i < order.Length; i++)
            if (order[i] == bookId) return i;
        return -1;
    }

    void Solve(bool silent)
    {
        if (solved) return;
        solved = true;
        selectedSlot = -1;
        for (int i = 0; i < SlotCount; i++) order[i] = i;
        ApplyPose();
        if (secretCase != null)
            secretCase.localPosition = secretClosedLocal + secretOpenOffset;
        if (brassLever != null)
            brassLever.gameObject.SetActive(true);
        for (int i = 0; i < tiltOnSolve.Length; i++)
        {
            if (tiltOnSolve[i] == null) continue;
            tiltOnSolve[i].localRotation *= Quaternion.Euler(0f, 0f, 2f);
        }
        GmRunStore.RecordClue(LeverClueId);
        if (!silent)
            ShowFeedback(
                "The shelf clicks. A brass catch releases. Somewhere under the stairs, a latch gives.");
    }

    void ApplyPose()
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            int bookId = order[slot];
            if (books == null || bookId < 0 || bookId >= books.Length || books[bookId] == null)
                continue;
            books[bookId].position = slotPositions[slot];
            if (slotRotations != null && slotRotations.Length == SlotCount)
                books[bookId].rotation = slotRotations[slot];
        }
    }

    void ShowFeedback(string message)
    {
        var hud = FindAnyObjectByType<GmGameHud>();
        if (hud != null)
            hud.ShowPrompt(message, "[E] Interact");
    }
}
