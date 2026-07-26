using System.Collections.Generic;
using NUnit.Framework;

namespace GamesMaster.ShutTheBox.Tests
{
    public sealed class ShutBoxRulesTests
    {
        [Test]
        public void FreshBoxOpensTilesOneThroughNine()
        {
            var box = ShutBoxRules.FreshBox();
            foreach (var tile in ShutBoxRules.Tiles)
            {
                Assert.That(box.IsOpen(tile), Is.True, "tile " + tile);
            }
        }

        [Test]
        public void FreshBoxHasSumFortyFive()
        {
            var box = ShutBoxRules.FreshBox();
            Assert.That(box.Sum, Is.EqualTo(45));
            Assert.That(ShutBoxRules.OpenSum(box), Is.EqualTo(45));
        }

        [Test]
        public void LegalMovesForSevenIncludesSeven()
        {
            Assert.That(ContainsMove(ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7), 7), Is.True);
        }

        [Test]
        public void LegalMovesForSevenIncludesOneAndSix()
        {
            Assert.That(ContainsMove(ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7), 1, 6), Is.True);
        }

        [Test]
        public void LegalMovesForSevenIncludesTwoAndFive()
        {
            Assert.That(ContainsMove(ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7), 2, 5), Is.True);
        }

        [Test]
        public void LegalMovesForSevenIncludesThreeAndFour()
        {
            Assert.That(ContainsMove(ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7), 3, 4), Is.True);
        }

        [Test]
        public void LegalMovesForSevenIncludesOneTwoAndFour()
        {
            Assert.That(ContainsMove(ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7), 1, 2, 4), Is.True);
        }

        [Test]
        public void EveryLegalMoveForSevenActuallySumsToSeven()
        {
            foreach (var move in ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7))
            {
                var sum = 0;
                foreach (var tile in move)
                {
                    sum += tile;
                }

                Assert.That(sum, Is.EqualTo(7));
            }
        }

        [Test]
        public void ApplyMoveOneAndSixSucceeds()
        {
            var box = ShutBoxRules.FreshBox();
            var result = ShutBoxRules.ApplyMove(box, new[] { 1, 6 });
            Assert.That(result.Ok, Is.True);
            Assert.That(result.Sum, Is.EqualTo(38));
        }

        [Test]
        public void ApplyMoveShutsOnlySelectedTiles()
        {
            var box = ShutBoxRules.FreshBox();
            ShutBoxRules.ApplyMove(box, new[] { 1, 6 });
            Assert.That(box.IsOpen(1), Is.False);
            Assert.That(box.IsOpen(6), Is.False);
            Assert.That(box.IsOpen(2), Is.True);
        }

        [Test]
        public void ApplyMoveCannotShutAnAlreadyShutTile()
        {
            var box = ShutBoxRules.FreshBox();
            ShutBoxRules.ApplyMove(box, new[] { 1, 6 });
            var result = ShutBoxRules.ApplyMove(box, new[] { 1 });
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Reason, Is.EqualTo("tile-shut"));
            Assert.That(result.Tile, Is.EqualTo(1));
        }

        [Test]
        public void GreedyAiReturnsAMoveWhenOneExists()
        {
            var move = ShutBoxRules.PickLegalMove(ShutBoxRules.FreshBox(), 9);
            Assert.That(move, Is.Not.Null);
            Assert.That(move.Length, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void GreedyAiMoveSumsToTheRoll()
        {
            var move = ShutBoxRules.PickLegalMove(ShutBoxRules.FreshBox(), 9);
            var sum = 0;
            foreach (var tile in move)
            {
                sum += tile;
            }

            Assert.That(sum, Is.EqualTo(9));
        }

        [Test]
        public void GreedyAiLeavesAtMostThirtySixOpen()
        {
            var box = ShutBoxRules.FreshBox();
            var move = ShutBoxRules.PickLegalMove(box, 9);
            var trial = ShutBoxRules.FreshBox();
            ShutBoxRules.ApplyMove(trial, move);
            Assert.That(ShutBoxRules.OpenSum(trial), Is.LessThanOrEqualTo(36));
        }

        [Test]
        public void HoldCorrectlyCatchesAPalm()
        {
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.Palm,
                new HoldClaim(),
                new HoldTruth(didPalm: true));
            Assert.That(result.Correct, Is.True);
            Assert.That(result.Penalty, Is.False);
        }

        [Test]
        public void HoldPenalizesAnIncorrectPalmCall()
        {
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.Palm,
                new HoldClaim(),
                new HoldTruth(didPalm: false));
            Assert.That(result.Correct, Is.False);
            Assert.That(result.Penalty, Is.True);
        }

        [Test]
        public void HoldCatchesAnIllegalFalseCall()
        {
            var legal = ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 6);
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.FalseCall,
                new HoldClaim(tiles: new[] { 5 }),
                new HoldTruth(legalForRoll: legal));
            Assert.That(result.Correct, Is.True);
            Assert.That(result.Penalty, Is.False);
        }

        [Test]
        public void HoldPenalizesCallingALegalAnnouncementFalse()
        {
            var legal = ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 6);
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.FalseCall,
                new HoldClaim(tiles: new[] { 6 }),
                new HoldTruth(legalForRoll: legal));
            Assert.That(result.Correct, Is.False);
            Assert.That(result.Penalty, Is.True);
        }

        [Test]
        public void TileNineTamperCatchOpensTheHiddenDoor()
        {
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.Tamper,
                new HoldClaim(tile: 9),
                new HoldTruth(tile: 9, wasShut: true, isOpenNow: true));
            Assert.That(result.Correct, Is.True);
            Assert.That(result.OpensHiddenDoor, Is.True);
        }

        [Test]
        public void OtherTileTamperCatchDoesNotOpenTheHiddenDoor()
        {
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.Tamper,
                new HoldClaim(tile: 3),
                new HoldTruth(tile: 3, wasShut: true, isOpenNow: true));
            Assert.That(result.Correct, Is.True);
            Assert.That(result.OpensHiddenDoor, Is.False);
        }

        [Test]
        public void FakeTamperCallIsPenalized()
        {
            var result = ShutBoxRules.EvaluateHold(
                HoldKind.Tamper,
                new HoldClaim(tile: 9),
                new HoldTruth(tile: 9, wasShut: false, isOpenNow: true));
            Assert.That(result.Correct, Is.False);
            Assert.That(result.Penalty, Is.True);
        }

        [Test]
        public void TileOneMapsToMarr()
        {
            Assert.That(ShutBoxRules.GuestForTile(1), Is.EqualTo("Marr"));
        }

        [Test]
        public void TileNineMapsToPercival()
        {
            Assert.That(ShutBoxRules.GuestForTile(9), Is.EqualTo("Percival"));
        }

        private static bool ContainsMove(IReadOnlyList<int[]> moves, params int[] expected)
        {
            foreach (var move in moves)
            {
                if (move.Length != expected.Length)
                {
                    continue;
                }

                var matches = true;
                for (var index = 0; index < move.Length; index++)
                {
                    if (move[index] != expected[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
