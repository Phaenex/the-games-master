using System;
using System.Collections.Generic;

namespace GamesMaster.ShutTheBox
{
    /// <summary>
    /// Mutable state for one player's nine-tile box.
    /// Mutations are intentionally restricted to <see cref="ShutBoxRules"/>.
    /// </summary>
    public sealed class ShutBoxState
    {
        private readonly bool[] _open = new bool[10];

        internal ShutBoxState()
        {
            for (var tile = 1; tile <= 9; tile++)
            {
                _open[tile] = true;
            }

            Sum = 45;
        }

        private ShutBoxState(ShutBoxState source)
        {
            Array.Copy(source._open, _open, source._open.Length);
            Locked = source.Locked;
            Sum = source.Sum;
        }

        public bool Locked { get; internal set; }

        public int Sum { get; internal set; }

        public bool IsOpen(int tile)
        {
            return tile >= 1 && tile <= 9 && _open[tile];
        }

        internal void Shut(int tile)
        {
            _open[tile] = false;
        }

        internal ShutBoxState Clone()
        {
            return new ShutBoxState(this);
        }
    }

    public sealed class MoveResult
    {
        private MoveResult(bool ok, int sum, string reason, int? tile)
        {
            Ok = ok;
            Sum = sum;
            Reason = reason;
            Tile = tile;
        }

        public bool Ok { get; private set; }

        public int Sum { get; private set; }

        public string Reason { get; private set; }

        public int? Tile { get; private set; }

        internal static MoveResult Success(int sum)
        {
            return new MoveResult(true, sum, null, null);
        }

        internal static MoveResult Failure(string reason, int sum, int? tile = null)
        {
            return new MoveResult(false, sum, reason, tile);
        }

        /// <summary>
        /// Rejection raised by a caller outside this assembly -- a turn or phase gate, not a rules
        /// verdict. Kept separate from <see cref="Failure"/> so only the rules engine authors rule
        /// outcomes, while a consumer can still return a first-class negative instead of faking one.
        /// </summary>
        public static MoveResult Rejected(string reason, int sum)
        {
            return new MoveResult(false, sum, reason, null);
        }
    }

    public enum HoldKind
    {
        Palm,
        FalseCall,
        Tamper
    }

    public sealed class HoldClaim
    {
        public HoldClaim(IReadOnlyList<int> tiles = null, int? tile = null)
        {
            Tiles = tiles;
            Tile = tile;
        }

        public IReadOnlyList<int> Tiles { get; private set; }

        public int? Tile { get; private set; }
    }

    public sealed class HoldTruth
    {
        public HoldTruth(
            bool didPalm = false,
            IReadOnlyList<int[]> legalForRoll = null,
            int? tile = null,
            bool wasShut = false,
            bool isOpenNow = false)
        {
            DidPalm = didPalm;
            LegalForRoll = legalForRoll;
            Tile = tile;
            WasShut = wasShut;
            IsOpenNow = isOpenNow;
        }

        public bool DidPalm { get; private set; }

        public IReadOnlyList<int[]> LegalForRoll { get; private set; }

        public int? Tile { get; private set; }

        public bool WasShut { get; private set; }

        public bool IsOpenNow { get; private set; }
    }

    public sealed class HoldResult
    {
        internal HoldResult(bool correct, bool penalty, bool opensHiddenDoor = false)
        {
            Correct = correct;
            Penalty = penalty;
            OpensHiddenDoor = opensHiddenDoor;
        }

        public bool Correct { get; private set; }

        public bool Penalty { get; private set; }

        public bool OpensHiddenDoor { get; private set; }
    }

    /// <summary>
    /// Engine-independent Shut-the-Box rules ported from gm-shutbox-logic.js.
    /// </summary>
    public static class ShutBoxRules
    {
        private static readonly int[] TileValues = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        private static readonly string[] LedgerNames =
        {
            "Marr",
            "Dufresne",
            "Pike",
            "Hale",
            "Gall",
            "Quill",
            "Thale",
            "Aubrey-Locke",
            "Percival"
        };

        public static IReadOnlyList<int> Tiles
        {
            get { return TileValues; }
        }

        public static IReadOnlyList<string> Ledger
        {
            get { return LedgerNames; }
        }

        public static ShutBoxState FreshBox()
        {
            return new ShutBoxState();
        }

        public static int OpenSum(ShutBoxState box)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            var sum = 0;
            foreach (var tile in TileValues)
            {
                if (box.IsOpen(tile))
                {
                    sum += tile;
                }
            }

            return sum;
        }

        /// <summary>
        /// Returns every non-empty subset of currently open tiles that sums to total.
        /// Tiles and combinations retain the deterministic ordering of the JavaScript rules.
        /// </summary>
        public static List<int[]> LegalMoves(ShutBoxState box, int total)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            var open = new List<int>();
            foreach (var tile in TileValues)
            {
                if (box.IsOpen(tile))
                {
                    open.Add(tile);
                }
            }

            var moves = new List<int[]>();
            var subsetCount = 1 << open.Count;
            for (var mask = 1; mask < subsetCount; mask++)
            {
                var sum = 0;
                var pick = new List<int>();
                for (var index = 0; index < open.Count; index++)
                {
                    if ((mask & (1 << index)) == 0)
                    {
                        continue;
                    }

                    sum += open[index];
                    pick.Add(open[index]);
                }

                if (sum == total)
                {
                    moves.Add(pick.ToArray());
                }
            }

            return moves;
        }

        public static MoveResult ApplyMove(ShutBoxState box, IReadOnlyList<int> tiles)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            if (box.Locked)
            {
                return MoveResult.Failure("locked", box.Sum);
            }

            // Validate the complete selection before mutating, matching the JavaScript module.
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (!box.IsOpen(tile))
                {
                    return MoveResult.Failure("tile-shut", box.Sum, tile);
                }
            }

            for (var index = 0; index < tiles.Count; index++)
            {
                box.Shut(tiles[index]);
            }

            box.Sum = OpenSum(box);
            return MoveResult.Success(box.Sum);
        }

        public static int LockBox(ShutBoxState box)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            box.Locked = true;
            box.Sum = OpenSum(box);
            return box.Sum;
        }

        /// <summary>
        /// Greedy legal AI: choose the first move producing the lowest remaining open sum.
        /// </summary>
        public static int[] PickLegalMove(ShutBoxState box, int total)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            var moves = LegalMoves(box, total);
            if (moves.Count == 0)
            {
                return null;
            }

            var best = moves[0];
            var bestSum = int.MaxValue;
            foreach (var move in moves)
            {
                var trial = box.Clone();
                trial.Locked = false;
                ApplyMove(trial, move);
                var sum = OpenSum(trial);
                if (sum < bestSum)
                {
                    bestSum = sum;
                    best = move;
                }
            }

            return best;
        }

        public static HoldResult EvaluateHold(HoldKind kind, HoldClaim claim, HoldTruth truth)
        {
            if (truth == null)
            {
                throw new ArgumentNullException(nameof(truth));
            }

            switch (kind)
            {
                case HoldKind.Palm:
                    return new HoldResult(truth.DidPalm, !truth.DidPalm);

                case HoldKind.FalseCall:
                {
                    var claimedTiles = claim != null ? claim.Tiles : null;
                    var hasClaim = claimedTiles != null && claimedTiles.Count > 0;
                    var legal = false;

                    if (hasClaim && truth.LegalForRoll != null)
                    {
                        foreach (var move in truth.LegalForRoll)
                        {
                            if (SameTiles(claimedTiles, move))
                            {
                                legal = true;
                                break;
                            }
                        }
                    }

                    var caught = hasClaim && !legal;
                    return new HoldResult(caught, !caught);
                }

                case HoldKind.Tamper:
                {
                    var claimedTile = claim != null && claim.Tile.HasValue
                        ? claim.Tile
                        : truth.Tile;
                    var caught = claimedTile.HasValue
                        && truth.Tile.HasValue
                        && truth.WasShut
                        && truth.IsOpenNow
                        && claimedTile.Value == truth.Tile.Value;
                    return new HoldResult(caught, !caught, caught && claimedTile.Value == 9);
                }

                default:
                    return new HoldResult(false, true);
            }
        }

        public static string GuestForTile(int tile)
        {
            return tile >= 1 && tile <= LedgerNames.Length ? LedgerNames[tile - 1] : null;
        }

        private static bool SameTiles(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            var leftCopy = new int[left.Count];
            var rightCopy = new int[right.Count];
            for (var index = 0; index < left.Count; index++)
            {
                leftCopy[index] = left[index];
                rightCopy[index] = right[index];
            }

            Array.Sort(leftCopy);
            Array.Sort(rightCopy);
            for (var index = 0; index < leftCopy.Length; index++)
            {
                if (leftCopy[index] != rightCopy[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
