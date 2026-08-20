using GamesMaster.ShutTheBox;
using NUnit.Framework;
using UnityEngine;

public sealed class GmShutTheBoxControllerTests
{
    [SetUp]
    public void SetUp() => GmRunStore.BeginNewRun();

    [Test]
    public void AHeadToHeadMatchDoesNotFinishWhenOnlyThePlayersBoxLocks()
    {
        var host = new GameObject("ShutTheBoxController");
        var controller = host.AddComponent<GmShutTheBoxController>();
        try
        {
            controller.ResetMatch();
            Assert.IsTrue(controller.BankPlayerBox());
            Assert.IsTrue(controller.PlayerBoard.Locked);
            Assert.AreEqual(GmShutBoxPhase.HostTurn, controller.Phase);
            Assert.AreEqual(GmShutBoxOutcome.None, controller.Outcome,
                "the design says the other player keeps rolling after one box gets stuck");
            Assert.IsFalse(GmRunStore.IsRoomComplete("shut-the-box"));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void PlayerCannotShutTilesThatDoNotExactlyMatchTheActiveRoll()
    {
        var host = new GameObject("ShutTheBoxController");
        var controller = host.AddComponent<GmShutTheBoxController>();
        try
        {
            controller.ResetMatch();
            controller.RollDice(seed: 42);
            int before = controller.PlayerBoard.Sum;

            MoveResult wrongTotal = controller.PlayPlayerMove(1);
            Assert.IsFalse(wrongTotal.Ok);
            Assert.That(wrongTotal.Reason, Does.StartWith("illegal-for-roll:"));
            Assert.AreEqual(before, controller.PlayerBoard.Sum);
            Assert.IsTrue(controller.PlayerBoard.IsOpen(1));

            MoveResult duplicateTile = controller.PlayPlayerMove(1, 1);
            Assert.IsFalse(duplicateTile.Ok);
            Assert.That(duplicateTile.Reason, Does.StartWith("illegal-for-roll:"));
            Assert.AreEqual(before, controller.PlayerBoard.Sum);
            Assert.IsTrue(controller.PlayerBoard.IsOpen(1));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ResolvingBothBoxesCompletesOneTableGameAndReportsTheWinner()
    {
        var host = new GameObject("ShutTheBoxController");
        var controller = host.AddComponent<GmShutTheBoxController>();
        int completed = 0;
        GmShutBoxOutcome reported = GmShutBoxOutcome.None;
        controller.OnGameCompleted += result => { completed++; reported = result; };
        try
        {
            controller.ResetMatch();
            Assert.IsTrue(controller.BankPlayerBox());
            Assert.IsTrue(controller.BankHostBox());

            Assert.AreEqual(GmShutBoxPhase.GameOver, controller.Phase);
            Assert.AreEqual(GmShutBoxOutcome.Draw, controller.Outcome);
            Assert.AreEqual(controller.Outcome, reported);
            Assert.AreEqual(1, completed);
            Assert.IsTrue(GmRunStore.IsRoomComplete("shut-the-box"));
            Assert.AreEqual(1, GmRunStore.TableGameIndex);
            Assert.AreEqual(1, GmRunStore.Sovereigns);

            Assert.IsFalse(controller.BankHostBox(), "a finished match completed itself twice");
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, GmRunStore.TableGameIndex);
            Assert.AreEqual(1, GmRunStore.Sovereigns,
                "a rejected repeat completion attempt must not grant a second sovereign");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void TileNineOpensTheSecretWithoutEndingOrFreezingTheMatch()
    {
        var host = new GameObject("ShutTheBoxController");
        var controller = host.AddComponent<GmShutTheBoxController>();
        try
        {
            controller.ResetMatch();
            GmShutBoxPhase before = controller.Phase;
            HoldResult result = controller.ExecuteHold(9, HoldKind.Tamper,
                new HoldClaim(tile: 9), new HoldTruth(tile: 9, wasShut: true, isOpenNow: true));

            Assert.IsTrue(result.OpensHiddenDoor);
            Assert.IsTrue(controller.SecretDoorUnlocked);
            Assert.AreEqual(before, controller.Phase,
                "finding the optional Hidden Room replaced the live match phase and made play impossible");
            Assert.IsFalse(GmRunStore.IsRoomComplete("shut-the-box"));
            Assert.AreEqual(0, GmRunStore.TableGameIndex);
            Assert.IsTrue(GmRunStore.HasCatch("stb-tile-9-door-latch"));
        }
        finally { Object.DestroyImmediate(host); }
    }
}
