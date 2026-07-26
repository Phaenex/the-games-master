using System;
using System.Collections.Generic;

namespace GamesMaster.ShutTheBox.Tests
{
    /// <summary>
    /// Standalone parity runner for environments where the Unity Test Runner is not active.
    /// scripts/test-shutbox-csharp.mjs compiles this file as an executable in a temp directory.
    /// </summary>
    public static class ShutBoxParityRunner
    {
        private static int _passed;
        private static int _failed;

        public static int Main()
        {
            TestFreshBox();
            TestLegalMoves();
            TestApplyMove();
            TestGreedyAi();
            TestPalmHold();
            TestFalseCallHold();
            TestTamperHold();
            TestLedger();

            Console.WriteLine();
            Console.WriteLine("Shut the Box C# parity: " + _passed + " passed, " + _failed + " failed");
            return _failed == 0 ? 0 : 1;
        }

        private static void TestFreshBox()
        {
            var box = ShutBoxRules.FreshBox();
            var allOpen = true;
            foreach (var tile in ShutBoxRules.Tiles)
            {
                allOpen &= box.IsOpen(tile);
            }

            Check("freshBox opens 1-9", allOpen);
            Check("freshBox sum 45", box.Sum == 45 && ShutBoxRules.OpenSum(box) == 45);
        }

        private static void TestLegalMoves()
        {
            var moves = ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 7);
            Check("legalMoves(7) includes [7]", ContainsMove(moves, 7));
            Check("legalMoves(7) includes [1,6]", ContainsMove(moves, 1, 6));
            Check("legalMoves(7) includes [2,5]", ContainsMove(moves, 2, 5));
            Check("legalMoves(7) includes [3,4]", ContainsMove(moves, 3, 4));
            Check("legalMoves(7) includes [1,2,4]", ContainsMove(moves, 1, 2, 4));

            var allSumToSeven = true;
            foreach (var move in moves)
            {
                var sum = 0;
                foreach (var tile in move)
                {
                    sum += tile;
                }

                allSumToSeven &= sum == 7;
            }

            Check("no illegal 7+something", allSumToSeven);
        }

        private static void TestApplyMove()
        {
            var box = ShutBoxRules.FreshBox();
            var result = ShutBoxRules.ApplyMove(box, new[] { 1, 6 });
            Check("applyMove [1,6] ok", result.Ok && result.Sum == 38);
            Check("tiles shut", !box.IsOpen(1) && !box.IsOpen(6) && box.IsOpen(2));

            var bad = ShutBoxRules.ApplyMove(box, new[] { 1 });
            Check("cannot re-shut", !bad.Ok && bad.Reason == "tile-shut");
        }

        private static void TestGreedyAi()
        {
            var box = ShutBoxRules.FreshBox();
            var move = ShutBoxRules.PickLegalMove(box, 9);
            Check("pickLegalMove returns array", move != null && move.Length >= 1);

            var sum = 0;
            foreach (var tile in move)
            {
                sum += tile;
            }

            Check("pick sums to 9", sum == 9);

            var trial = ShutBoxRules.FreshBox();
            ShutBoxRules.ApplyMove(trial, move);
            Check("greedy leaves open sum <= 36", ShutBoxRules.OpenSum(trial) <= 36);
        }

        private static void TestPalmHold()
        {
            var hit = ShutBoxRules.EvaluateHold(
                HoldKind.Palm,
                new HoldClaim(),
                new HoldTruth(didPalm: true));
            var miss = ShutBoxRules.EvaluateHold(
                HoldKind.Palm,
                new HoldClaim(),
                new HoldTruth(didPalm: false));
            Check("Hold palm correct", hit.Correct && !hit.Penalty);
            Check("Hold palm false = penalty", !miss.Correct && miss.Penalty);
        }

        private static void TestFalseCallHold()
        {
            var legal = ShutBoxRules.LegalMoves(ShutBoxRules.FreshBox(), 6);
            var badCall = ShutBoxRules.EvaluateHold(
                HoldKind.FalseCall,
                new HoldClaim(tiles: new[] { 5 }),
                new HoldTruth(legalForRoll: legal));
            var goodAnnouncement = ShutBoxRules.EvaluateHold(
                HoldKind.FalseCall,
                new HoldClaim(tiles: new[] { 6 }),
                new HoldTruth(legalForRoll: legal));
            Check("falseCall catch on illegal shut", badCall.Correct && !badCall.Penalty);
            Check("falseCall miss when announced legal", !goodAnnouncement.Correct && goodAnnouncement.Penalty);
        }

        private static void TestTamperHold()
        {
            var door = ShutBoxRules.EvaluateHold(
                HoldKind.Tamper,
                new HoldClaim(tile: 9),
                new HoldTruth(tile: 9, wasShut: true, isOpenNow: true));
            var noDoor = ShutBoxRules.EvaluateHold(
                HoldKind.Tamper,
                new HoldClaim(tile: 3),
                new HoldTruth(tile: 3, wasShut: true, isOpenNow: true));
            var fake = ShutBoxRules.EvaluateHold(
                HoldKind.Tamper,
                new HoldClaim(tile: 9),
                new HoldTruth(tile: 9, wasShut: false, isOpenNow: true));
            Check("tile-9 Hold opens door", door.Correct && door.OpensHiddenDoor);
            Check("other tile Hold no door", noDoor.Correct && !noDoor.OpensHiddenDoor);
            Check("fake tamper penalties", !fake.Correct && fake.Penalty);
        }

        private static void TestLedger()
        {
            Check("guestForTile(1)=Marr", ShutBoxRules.GuestForTile(1) == "Marr");
            Check("guestForTile(9)=Percival", ShutBoxRules.GuestForTile(9) == "Percival");
        }

        private static void Check(string name, bool condition)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            else
            {
                _failed++;
                Console.WriteLine("FAIL " + name);
            }
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
                    matches &= move[index] == expected[index];
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
