using System;
using GamesMaster.ShutTheBox;
using UnityEngine;

public enum GmShutBoxPhase
{
    NotStarted,
    PlayerRolling,
    PlayerSelecting,
    HostTurn,
    HoldingAccusation,
    GameOver,
    SecretDoorOpened
}

public sealed class GmShutTheBoxController : MonoBehaviour
{
    public ShutBoxState PlayerBoard { get; private set; }
    public ShutBoxState HostBoard { get; private set; }

    public int Die1 { get; private set; } = 1;
    public int Die2 { get; private set; } = 1;
    public int DiceSum => Die1 + Die2;

    public GmShutBoxPhase Phase { get; private set; } = GmShutBoxPhase.NotStarted;
    public bool SecretDoorUnlocked { get; private set; } = false;

    public event Action OnStateChanged;
    public event Action<string> OnSecretDoorTriggered;

    void Awake()
    {
        ResetMatch();
    }

    public void ResetMatch()
    {
        PlayerBoard = ShutBoxRules.FreshBox();
        HostBoard = ShutBoxRules.FreshBox();
        Phase = GmShutBoxPhase.PlayerRolling;
        SecretDoorUnlocked = false;
        OnStateChanged?.Invoke();
    }

    public void RollDice(int seed = 42)
    {
        if (Phase != GmShutBoxPhase.PlayerRolling && Phase != GmShutBoxPhase.HostTurn) return;

        var rng = new System.Random(seed);
        Die1 = rng.Next(1, 7);
        Die2 = rng.Next(1, 7);

        if (Phase == GmShutBoxPhase.PlayerRolling)
        {
            Phase = GmShutBoxPhase.PlayerSelecting;
        }

        OnStateChanged?.Invoke();
    }

    public MoveResult PlayPlayerMove(params int[] tiles)
    {
        // An out-of-turn call is refused outright. Routing it through ApplyMove with an empty
        // selection used to report Ok, because the engine treats zero tiles as vacuously valid.
        if (Phase != GmShutBoxPhase.PlayerSelecting)
            return MoveResult.Rejected($"wrong-phase:{Phase}", PlayerBoard.Sum);

        MoveResult result = ShutBoxRules.ApplyMove(PlayerBoard, tiles);
        if (result.Ok)
        {
            if (PlayerBoard.Sum == 0)
            {
                Phase = GmShutBoxPhase.GameOver;
            }
            else
            {
                Phase = GmShutBoxPhase.HostTurn;
            }
            OnStateChanged?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// Executes the Hold verb on a specific tile. Holding Tile 9 checks for the secret door
    /// trigger; holding other tiles tests a cheat accusation (palm / false call / tamper).
    /// The caller supplies the accusation kind plus the real claim and ground truth from
    /// actual game state -- this method forwards to <see cref="ShutBoxRules.EvaluateHold"/>
    /// rather than inferring truth itself, since inventing that detection here would make
    /// every accusation report correct by construction.
    /// </summary>
    public HoldResult ExecuteHold(int tileNumber, HoldKind kind, HoldClaim claim, HoldTruth truth)
    {
        HoldResult result = ShutBoxRules.EvaluateHold(kind, claim, truth);

        if (result.OpensHiddenDoor)
        {
            SecretDoorUnlocked = true;
            Phase = GmShutBoxPhase.SecretDoorOpened;
            GmRunStore.RecordCatch("stb-tile-9-door-latch");
            OnSecretDoorTriggered?.Invoke("Phase 6 Hidden Room unlocked via Tile 9");
            Debug.Log("[GmShutBoxController] TILE 9 HOLD TRIGGERED: Secret Panel Door Unlatched!");
        }
        else if (result.Correct)
        {
            GmRunStore.RecordCatch($"stb-cheat-tile-{tileNumber}");
            GmRunStore.ApplySanityDelta(GmFeelConfig.Active.shutTheBoxCatchSanityGain);
            Debug.Log($"[GmShutBoxController] Correct Hold accusation on Tile {tileNumber}!");
        }
        else
        {
            GmRunStore.RecordMiss();
            GmRunStore.RaiseCorruption($"False hold on Tile {tileNumber}");
            Debug.LogWarning($"[GmShutBoxController] False Hold penalty: {result.Penalty}");
        }

        OnStateChanged?.Invoke();
        return result;
    }
}
