using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GmHousePhase
{
    WaitingForCrossing,
    EntryHall,
    EnteringParlor,
    HostIntroduction,
    ParlorGame,
    Complete,
}

public enum GmParlorTurnPhase
{
    None,
    ChooseCard,
    JudgePlay,
    TrickResult,
    RoundResult,
    MatchResult,
}

/// <summary>
/// Owns the complete house-beginning flow: clue-gated exploration, the threshold into Aldric's room,
/// his rules scene and a best-of-three first game. Public review methods drive the same transitions
/// used by the player, so tests and built-player evidence cannot bypass a second implementation.
/// </summary>
[DefaultExecutionOrder(40)]
public sealed class GmHouseBeginning : MonoBehaviour, IGmInteractionReceiver
{
    static readonly string[] IntroSpeakers =
    {
        "THE HOUSE", "THE HOUSE", "ALDRIC VOSS", "ALDRIC VOSS", "ALDRIC VOSS",
        "ALDRIC VOSS", "ALDRIC VOSS", "ALDRIC VOSS", "ALDRIC VOSS",
    };

    static readonly string[] IntroLines =
    {
        "The doors give without a sound—the way a house opens when it has been expecting you.",
        "Firelight. A small round table. Two chairs. One seat already warm.",
        "There you are. I was beginning to think the hall had kept you. It does that—shows a guest the long way round.",
        "Forty-one thousand pounds. Friday. You may have every note of it, if you win it from me. One hand at a time. Simple.",
        "Four suits. Follow the suit led when you can. Flames are trump; a Flame burns any other suit. Highest card takes the trick.",
        "Eyes let you glimpse what is hidden. Teeth expose a card when they win. One lost Bone may return to your hand. Flames need no kindness explained.",
        "Four tricks win a round. Two rounds win the game. For the opening lesson, you lead three; after that, the winner leads the next.",
        "If a play cannot exist, Read the hand before you let the trick pass. A true accusation turns the trick. A false one costs you.",
        "And I will play perfectly fair. …Forgive me. Old habit, that last word. Pick up your hand. We begin.",
    };

    readonly HashSet<string> portraitIds = new HashSet<string>();
    readonly List<GmParlorCard> playerHand = new List<GmParlorCard>();
    readonly List<GmParlorCard> aldricHand = new List<GmParlorCard>();

    GmHouseProgress progress;
    GmPlayer player;
    GmInteractable parlorDoor;
    GameObject parlorDoorLeaf;
    GameObject mirrorShard;
    float nextInputAt;
    int introIndex;
    int roundNumber;
    int trickNumber;
    int playerTricks;
    int aldricTricks;
    int playerRounds;
    int aldricRounds;
    int suspicion;
    bool playerLeads;
    bool bonesReturned;
    bool currentCheat;
    string currentTell = "";
    GmParlorCard? leadCard;
    GmParlorCard? playerCard;
    GmParlorCard? aldricCard;

    public GmHousePhase Phase { get; private set; } = GmHousePhase.WaitingForCrossing;
    public GmParlorTurnPhase TurnPhase { get; private set; }
    public GmHouseProgress Progress => progress;
    public IReadOnlyList<GmParlorCard> PlayerHand => playerHand;
    public IReadOnlyList<GmParlorCard> AldricHand => aldricHand;
    public GmParlorCard? LeadCard => leadCard;
    public GmParlorCard? PlayerCard => playerCard;
    public GmParlorCard? AldricCard => aldricCard;
    public int PortraitsRead => portraitIds.Count;
    public int RoundNumber => roundNumber;
    public int TrickNumber => trickNumber;
    public int PlayerTricks => playerTricks;
    public int AldricTricks => aldricTricks;
    public int PlayerRounds => playerRounds;
    public int AldricRounds => aldricRounds;
    public int Suspicion => suspicion;
    public bool ReadUnlocked => suspicion >= 2;
    public bool CurrentPlayCanBeRead => TurnPhase == GmParlorTurnPhase.JudgePlay && ReadUnlocked;
    public bool ReviewCurrentPlayWasCheat => currentCheat;
    public int IntroIndex => introIndex;
    public int IntroCount => IntroLines.Length;
    public string DialogueSpeaker => Phase == GmHousePhase.HostIntroduction ? IntroSpeakers[introIndex] : "";
    public string DialogueLine => Phase == GmHousePhase.HostIntroduction ? IntroLines[introIndex] : "";
    public string Objective { get; private set; } = "WAKE";
    public string TableMessage { get; private set; } = "";
    public string RevealedCard { get; private set; } = "";
    public int UiRevision { get; private set; }

    public bool CanEnterParlor => progress != null && progress.HasClue("ledger-open-line") && PortraitsRead >= 3;

    void Awake()
    {
        progress = GetComponent<GmHouseProgress>() ?? gameObject.AddComponent<GmHouseProgress>();
        parlorDoor = transform.Find("EntryHall/ParlorDoor")?.GetComponent<GmInteractable>();
        parlorDoorLeaf = transform.Find("EntryHall/ParlorDoor/DoorLeaf")?.gameObject;
        mirrorShard = transform.Find("EntryHall/Portraits/Portrait_Percival/MirrorShard")?.gameObject;
        if (mirrorShard != null) mirrorShard.SetActive(false);
    }

    void Start()
    {
        player = FindAnyObjectByType<GmPlayer>();
    }

    void Update()
    {
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        if (Phase == GmHousePhase.WaitingForCrossing && player != null && player.transform.position.z > 393f)
            EnterHall();

        if (Time.unscaledTime < nextInputAt) return;
        bool advance = (Keyboard.current != null &&
                        (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame ||
                         Keyboard.current.eKey.wasPressedThisFrame)) ||
                       (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (Phase == GmHousePhase.HostIntroduction && advance) AdvanceDialogue();
        else if (Phase == GmHousePhase.ParlorGame)
        {
            if (TurnPhase == GmParlorTurnPhase.ChooseCard && Keyboard.current != null)
            {
                for (int i = 0; i < Mathf.Min(7, playerHand.Count); i++)
                    if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SelectCard(i);
            }
            if (TurnPhase == GmParlorTurnPhase.JudgePlay)
            {
                bool read = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
                if (read && ReadUnlocked) ReadHand();
                else if (advance) AllowTrick();
            }
            else if ((TurnPhase == GmParlorTurnPhase.TrickResult || TurnPhase == GmParlorTurnPhase.RoundResult ||
                      TurnPhase == GmParlorTurnPhase.MatchResult) && advance)
                ContinueAfterResult();
        }
    }

    void EnterHall()
    {
        Phase = GmHousePhase.EntryHall;
        FindAnyObjectByType<GmPrologueHud>()?.SetHouseMode();
        Objective = "SEARCH THE ENTRY HALL — THE LEDGER AND THREE PORTRAITS";
        TableMessage = "Nine faces watch from the walls. One line in the ledger waits unfinished.";
        TouchUi();
        GmExperienceTelemetry.Record("chapter", "entry-hall");
    }

    public void OnGmInteraction(GmInteractable source)
    {
        if (source == null) return;
        string id = source.InteractionId;
        if (id == "hall-ledger") progress.Discover("ledger-open-line");
        else if (id.StartsWith("hall-portrait-", StringComparison.Ordinal))
        {
            portraitIds.Add(id);
            progress.Discover(id);
        }
        else if (id == "hall-mirror-shard") progress.FindShard("mirror-shard-01");
        else if (id == "hall-parlor-door")
        {
            if (CanEnterParlor) OpenParlor();
            else
            {
                int remaining = Mathf.Max(0, 3 - PortraitsRead);
                source.BindContent(
                    "The latch refuses. The ledger's blank line and the watching portraits feel like a question.",
                    progress.HasClue("ledger-open-line")
                        ? $"The latch gives a fraction. {remaining} more portrait{(remaining == 1 ? "" : "s")} demand an answer."
                        : "The door waits for the open line in the ledger.");
            }
        }

        if (progress.HasClue("ledger-open-line") && portraitIds.Contains("hall-portrait-percival"))
        {
            if (progress.Discover("percival-ledger-pair") && mirrorShard != null) mirrorShard.SetActive(true);
        }
        if (CanEnterParlor && Phase == GmHousePhase.EntryHall)
            Objective = "THE PARLOR DOOR IS READY";
        TouchUi();
    }

    void OpenParlor()
    {
        if (Phase != GmHousePhase.EntryHall) return;
        Phase = GmHousePhase.EnteringParlor;
        Objective = "ENTER THE PARLOR";
        if (parlorDoorLeaf != null)
        {
            parlorDoorLeaf.transform.localRotation = Quaternion.Euler(0f, -105f, 0f);
            foreach (Collider leafCollider in parlorDoorLeaf.GetComponentsInChildren<Collider>(true))
                leafCollider.enabled = false;
        }
        if (parlorDoor != null)
        {
            Collider doorCollider = parlorDoor.GetComponent<Collider>();
            if (doorCollider != null) doorCollider.enabled = false;
            parlorDoor.enabled = false;
        }
        GmExperienceTelemetry.Record("threshold", "parlor-open");
        TouchUi();
    }

    public void BeginHostIntroduction()
    {
        if (Phase != GmHousePhase.EnteringParlor && Phase != GmHousePhase.EntryHall) return;
        if (Phase == GmHousePhase.EntryHall && !CanEnterParlor) return;
        Phase = GmHousePhase.HostIntroduction;
        introIndex = 0;
        Objective = "LISTEN TO ALDRIC";
        nextInputAt = Time.unscaledTime + 0.35f;
        player?.SetControlBlocked(true);
        GmExperienceTelemetry.Record("chapter", "parlor-introduction");
        TouchUi();
    }

    public void AdvanceDialogue()
    {
        if (Phase != GmHousePhase.HostIntroduction) return;
        if (introIndex < IntroLines.Length - 1)
        {
            introIndex++;
            nextInputAt = Time.unscaledTime + 0.12f;
            TouchUi();
            return;
        }
        StartMatch();
    }

    void StartMatch()
    {
        Phase = GmHousePhase.ParlorGame;
        Objective = "WIN TWO ROUNDS — READ AN IMPOSSIBLE PLAY";
        playerRounds = 0;
        aldricRounds = 0;
        suspicion = 0;
        progress.Discover("aldric-rules");
        BeginRound();
        GmExperienceTelemetry.Record("game-start", "parlor");
    }

    void BeginRound()
    {
        roundNumber++;
        trickNumber = 0;
        playerTricks = 0;
        aldricTricks = 0;
        bonesReturned = false;
        playerLeads = roundNumber == 1 || playerRounds >= aldricRounds;
        playerHand.Clear();
        aldricHand.Clear();
        if (roundNumber == 1)
        {
            // Authored tutorial deal. The player's first three sorted leads cannot be beaten by a
            // legal response. Aldric's constraint therefore forces two deniable contradictions and
            // then a third, catchable play once Read is earned. Nothing here grants him permission to
            // cheat over a legal winner; the same pure rules method decides all three responses.
            playerHand.AddRange(new[]
            {
                new GmParlorCard(GmCardSuit.Flames, 1),
                new GmParlorCard(GmCardSuit.Eyes, 5), new GmParlorCard(GmCardSuit.Eyes, 7),
                new GmParlorCard(GmCardSuit.Teeth, 5), new GmParlorCard(GmCardSuit.Teeth, 7),
                new GmParlorCard(GmCardSuit.Bones, 5), new GmParlorCard(GmCardSuit.Bones, 7),
            });
            aldricHand.AddRange(new[]
            {
                new GmParlorCard(GmCardSuit.Eyes, 1), new GmParlorCard(GmCardSuit.Eyes, 2),
                new GmParlorCard(GmCardSuit.Eyes, 3), new GmParlorCard(GmCardSuit.Teeth, 1),
                new GmParlorCard(GmCardSuit.Teeth, 2), new GmParlorCard(GmCardSuit.Bones, 1),
                new GmParlorCard(GmCardSuit.Bones, 2),
            });
        }
        else
        {
            List<GmParlorCard> deck = GmParlorRules.ShuffledDeck(41009 + roundNumber * 97);
            for (int i = 0; i < GmParlorRules.HandSize; i++)
            {
                playerHand.Add(deck[i * 2]);
                aldricHand.Add(deck[i * 2 + 1]);
            }
        }
        playerHand.Sort((a, b) => a.Suit != b.Suit ? a.Suit.CompareTo(b.Suit) : a.Rank.CompareTo(b.Rank));
        TurnPhase = GmParlorTurnPhase.ChooseCard;
        SetupTrick();
    }

    void SetupTrick()
    {
        if (roundNumber == 1 && trickNumber < 3) playerLeads = true;
        leadCard = null;
        playerCard = null;
        aldricCard = null;
        currentCheat = false;
        currentTell = "";
        RevealedCard = "";
        if (!playerLeads)
        {
            int choice = GmParlorRules.ChooseAldricLead(aldricHand);
            aldricCard = aldricHand[choice];
            leadCard = aldricCard;
            aldricHand.RemoveAt(choice);
            TableMessage = $"Aldric leads {aldricCard.Value.ShortName}. Follow {aldricCard.Value.Suit} if you can.";
        }
        else TableMessage = "Your lead. Choose a card.";
        TurnPhase = GmParlorTurnPhase.ChooseCard;
        TouchUi();
    }

    public bool SelectCard(int index)
    {
        if (Phase != GmHousePhase.ParlorGame || TurnPhase != GmParlorTurnPhase.ChooseCard ||
            index < 0 || index >= playerHand.Count) return false;
        if (!GmParlorRules.IsLegal(playerHand, index, leadCard))
        {
            TableMessage = $"You must follow {leadCard.Value.Suit} while you hold it.";
            TouchUi();
            return false;
        }

        playerCard = playerHand[index];
        playerHand.RemoveAt(index);
        if (playerLeads)
        {
            leadCard = playerCard;
            GmAldricPlay response = GmParlorRules.ChooseAldricFollow(aldricHand, playerCard.Value, true);
            aldricCard = response.Card;
            aldricHand.RemoveAt(response.RemovedIndex);
            currentCheat = response.Cheated;
            currentTell = response.Tell;
        }
        TableMessage = currentCheat && ReadUnlocked
            ? "The cards disagree with themselves. Allow the trick—or Read Aldric's hand."
            : $"You played {playerCard.Value.ShortName}. Aldric played {aldricCard.Value.ShortName}.";
        TurnPhase = GmParlorTurnPhase.JudgePlay;
        TouchUi();
        return true;
    }

    public void AllowTrick()
    {
        if (TurnPhase != GmParlorTurnPhase.JudgePlay) return;
        if (currentCheat)
        {
            suspicion++;
            progress.MissCheat();
        }
        ResolveTrick(false);
    }

    public void ReadHand()
    {
        if (TurnPhase != GmParlorTurnPhase.JudgePlay || !ReadUnlocked) return;
        if (currentCheat)
        {
            progress.CatchCheat($"parlor-cheat-{progress.CheatsCaught + 1:D2}");
            TableMessage = $"READ CORRECT. {currentTell} The trick turns to you.";
            ResolveTrick(true);
        }
        else
        {
            progress.FalseRead();
            TableMessage = "FALSE READ. The hand is legal. The mistake costs you.";
            ResolveTrick(false);
        }
    }

    void ResolveTrick(bool caught)
    {
        if (!playerCard.HasValue || !aldricCard.HasValue) return;
        GmTrickOwner winner;
        if (caught) winner = GmTrickOwner.Player;
        else if (playerLeads) winner = GmParlorRules.Winner(playerCard.Value, aldricCard.Value);
        else winner = GmParlorRules.Winner(aldricCard.Value, playerCard.Value) == GmTrickOwner.Player
            ? GmTrickOwner.Aldric : GmTrickOwner.Player;

        if (playerCard.Value.Suit == GmCardSuit.Eyes && aldricHand.Count > 0)
            RevealedCard = $"EYES: the edge of {aldricHand[0].ShortName} shows in Aldric's hand.";
        if (winner == GmTrickOwner.Player && playerCard.Value.Suit == GmCardSuit.Teeth && aldricHand.Count > 0)
            RevealedCard = $"TEETH: Aldric must expose {aldricHand[aldricHand.Count - 1].ShortName}.";
        if (winner == GmTrickOwner.Aldric && playerCard.Value.Suit == GmCardSuit.Bones && !bonesReturned)
        {
            bonesReturned = true;
            playerHand.Add(playerCard.Value);
            RevealedCard = "BONES: the lost card crawls back into your hand.";
        }

        if (winner == GmTrickOwner.Player) playerTricks++;
        else aldricTricks++;
        playerLeads = winner == GmTrickOwner.Player;
        trickNumber++;

        string verdict = winner == GmTrickOwner.Player ? "You take the trick." : "Aldric takes the trick.";
        if (!string.IsNullOrEmpty(TableMessage) && (TableMessage.StartsWith("READ") || TableMessage.StartsWith("FALSE")))
            TableMessage += " " + verdict;
        else TableMessage = verdict;

        if (playerTricks >= 4 || aldricTricks >= 4 || trickNumber >= 7 || playerHand.Count == 0 || aldricHand.Count == 0)
        {
            bool won = playerTricks > aldricTricks;
            if (won) playerRounds++; else aldricRounds++;
            TableMessage += won ? " You win the round." : " Aldric wins the round.";
            TurnPhase = playerRounds >= 2 || aldricRounds >= 2
                ? GmParlorTurnPhase.MatchResult : GmParlorTurnPhase.RoundResult;
        }
        else
        {
            TurnPhase = GmParlorTurnPhase.TrickResult;
            TableMessage += " Continue to the next trick.";
        }
        TouchUi();
    }

    public void ContinueAfterResult()
    {
        if (TurnPhase == GmParlorTurnPhase.TrickResult)
        {
            SetupTrick();
            return;
        }
        if (TurnPhase == GmParlorTurnPhase.RoundResult)
        {
            BeginRound();
            return;
        }
        if (TurnPhase != GmParlorTurnPhase.MatchResult) return;
        Phase = GmHousePhase.Complete;
        Objective = playerRounds > aldricRounds ? "THE FIRST GAME IS WON" : "THE HOUSE TAKES THE FIRST GAME";
        TableMessage = playerRounds > aldricRounds
            ? "Aldric gathers the cards too carefully. “A promising beginning.” The open doorway behind him is new."
            : "Aldric squares the deck. “The house is patient.” Your debt remains—and now the table knows your hands.";
        progress.Discover("parlor-complete");
        player?.SetControlBlocked(false);
        GmExperienceTelemetry.Record("game-complete", playerRounds > aldricRounds ? "player" : "aldric");
        TouchUi();
    }

    public void ReviewEnterHouse()
    {
        if (player == null) player = FindAnyObjectByType<GmPlayer>();
        Transform pose = GameObject.Find("WakeRoom/WakePose")?.transform;
        if (player != null && pose != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (controller != null) controller.enabled = true;
        }
        EnterHall();
    }

    public void ReviewUnlockParlor()
    {
        progress.Discover("ledger-open-line");
        foreach (string id in new[] { "hall-portrait-edwin-marr", "hall-portrait-constance", "hall-portrait-percival" })
        {
            portraitIds.Add(id);
            progress.Discover(id);
        }
        progress.Discover("percival-ledger-pair");
        if (mirrorShard != null) mirrorShard.SetActive(true);
        if (Phase == GmHousePhase.WaitingForCrossing) EnterHall();
        OpenParlor();
    }

    public bool ReviewPlayFirstLegalCard()
    {
        if (TurnPhase != GmParlorTurnPhase.ChooseCard) return false;
        for (int i = 0; i < playerHand.Count; i++) if (GmParlorRules.IsLegal(playerHand, i, leadCard)) return SelectCard(i);
        return false;
    }

    void TouchUi() => UiRevision++;
}

public sealed class GmHouseThreshold : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<GmPlayer>() != null)
            FindAnyObjectByType<GmHouseBeginning>()?.BeginHostIntroduction();
    }
}
