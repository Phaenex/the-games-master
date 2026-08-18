using NUnit.Framework;
using UnityEngine;

public sealed class GmWeightedShelfTests
{
    GameObject root;
    GmWeightedShelf shelf;
    Transform[] books;
    Transform sliding;
    Transform lever;

    [SetUp]
    public void SetUp()
    {
        GmRunStore.BeginNewRun();
        root = new GameObject("WeightedShelfRoot");
        shelf = root.AddComponent<GmWeightedShelf>();
        books = new Transform[GmWeightedShelf.SlotCount];
        var slots = new Vector3[GmWeightedShelf.SlotCount];
        var rotations = new Quaternion[GmWeightedShelf.SlotCount];
        for (int i = 0; i < books.Length; i++)
        {
            var book = new GameObject("Book_" + i);
            book.transform.SetParent(root.transform, false);
            books[i] = book.transform;
            slots[i] = new Vector3(i, 0f, 0f);
            rotations[i] = Quaternion.identity;
        }

        sliding = new GameObject("SecretCase").transform;
        sliding.SetParent(root.transform, false);
        lever = new GameObject("BrassLever").transform;
        lever.SetParent(root.transform, false);
        shelf.Bind(books, slots, rotations, sliding, lever, null, new Vector3(0.4f, 0f, 0f));
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        GmRunStore.BeginNewRun();
    }

    [Test]
    public void TheShelfStartsInTheBibleOrderWithMiddlegameAlreadyCorrect()
    {
        int[] order = shelf.CurrentOrder;
        CollectionAssert.AreEqual(GmWeightedShelf.StartingOrder, order);
        Assert.AreEqual(2, order[2], "III / The Middlegame should already sit in the middle notch");
        Assert.IsFalse(shelf.IsSolved);
        Assert.IsFalse(GmRunStore.HasClue(GmWeightedShelf.LeverClueId));
        Assert.IsFalse(lever.gameObject.activeSelf, "the brass catch must stay hidden until the shelf clicks");
    }

    [Test]
    public void ArrangingIThroughVBanksTheCellarLeverClueAndSlidesTheCase()
    {
        Vector3 closed = sliding.localPosition;
        // Start V, I, III, IV, II. Lift slot 0 then 1 → I, V, III, IV, II.
        Assert.IsTrue(shelf.HandleSlot(0));
        Assert.IsTrue(shelf.HandleSlot(1));
        // Lift the V now in slot 1 and swap with slot 4's II → I, II, III, IV, V.
        Assert.IsTrue(shelf.HandleSlot(1));
        Assert.IsTrue(shelf.HandleSlot(4));

        Assert.IsTrue(shelf.IsSolved);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, shelf.CurrentOrder);
        Assert.IsTrue(GmRunStore.HasClue(GmWeightedShelf.LeverClueId));
        Assert.That((sliding.localPosition - closed).x, Is.GreaterThan(0.3f));
        Assert.IsTrue(lever.gameObject.activeSelf);
    }

    [Test]
    public void APersistedSolveRebuildsAlreadyOpen()
    {
        GmRunStore.RecordClue(GmWeightedShelf.LeverClueId);
        shelf.Bind(books, Slots(), Rotations(), sliding, lever, null, new Vector3(0.4f, 0f, 0f));
        Assert.IsTrue(shelf.IsSolved);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, shelf.CurrentOrder);
        Assert.IsTrue(lever.gameObject.activeSelf);
        Assert.IsFalse(shelf.HandleSlot(0), "a solved rail should ignore further swaps");
    }

    [Test]
    public void ResetPuzzleReturnsTheBibleOrderAndHidesTheLever()
    {
        Assert.IsTrue(shelf.HandleSlot(0));
        Assert.IsTrue(shelf.HandleSlot(1));
        Assert.IsTrue(shelf.HandleSlot(1));
        Assert.IsTrue(shelf.HandleSlot(4));
        Assert.IsTrue(shelf.IsSolved);

        shelf.ResetPuzzle();
        Assert.IsFalse(shelf.IsSolved);
        CollectionAssert.AreEqual(GmWeightedShelf.StartingOrder, shelf.CurrentOrder);
        Assert.IsFalse(lever.gameObject.activeSelf);
    }

    [Test]
    public void SelectingTheSameSlotTwicePutsTheBookBack()
    {
        Assert.IsTrue(shelf.HandleSlot(0));
        Assert.AreEqual(0, shelf.SelectedSlot);
        Assert.IsTrue(shelf.HandleSlot(0));
        Assert.AreEqual(-1, shelf.SelectedSlot);
        CollectionAssert.AreEqual(GmWeightedShelf.StartingOrder, shelf.CurrentOrder);
    }

    [Test]
    public void BookInteractionIdsRoundTrip()
    {
        Assert.IsTrue(GmWeightedShelf.TryParseBookId(GmWeightedShelf.BookInteractionId(4), out int bookId));
        Assert.AreEqual(4, bookId);
        Assert.IsFalse(GmWeightedShelf.TryParseBookId("library-door", out _));
        Assert.That(GmWeightedShelf.SpineLabel(0), Does.Contain("I"));
        Assert.That(GmWeightedShelf.SpineLabel(0), Does.Contain("Opening Gambit"));
    }

    Vector3[] Slots()
    {
        var slots = new Vector3[GmWeightedShelf.SlotCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = new Vector3(i, 0f, 0f);
        return slots;
    }

    Quaternion[] Rotations()
    {
        var rotations = new Quaternion[GmWeightedShelf.SlotCount];
        for (int i = 0; i < rotations.Length; i++) rotations[i] = Quaternion.identity;
        return rotations;
    }
}
