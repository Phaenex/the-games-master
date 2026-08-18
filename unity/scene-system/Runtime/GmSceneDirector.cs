using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GmSceneDirector : MonoBehaviour
{
    private static GmSceneDirector _instance;
    public static GmSceneDirector Instance => _instance;

    public const string PrologueScenePath = "Assets/Scenes/WendHill_Prologue.unity";
    public const string EntryHallScenePath = "Assets/Scenes/EntryHall.unity";
    public const string ParlorScenePath = "Assets/Scenes/Parlor.unity";
    public const string ShutTheBoxScenePath = "Assets/Scenes/ShutTheBox.unity";
    public const string CourtScenePath = "Assets/Scenes/Court.unity";
    public const string HiddenRoomScenePath = "Assets/Scenes/HiddenRoom.unity";
    public const string LabyrinthScenePath = "Assets/Scenes/Labyrinth.unity";
    public const string BootScenePath = "Assets/Scenes/Boot.unity";

    /// Every scene this director is capable of asking for, in play order.
    ///
    /// Exists so the shipped build manifest can be DERIVED from this list rather than maintained
    /// beside it. LoadSceneAsync returns null for a scene that is not in Build Settings, and the
    /// transition below correctly refuses to move when that happens — but refusing to move is a
    /// dead end at runtime, not a warning at build time. A room the director can name and the
    /// player can never enter is the same defect as a room with no player in it, one layer up.
    public static readonly string[] ReachableScenePaths =
    {
        PrologueScenePath,
        EntryHallScenePath,
        ParlorScenePath,
        ShutTheBoxScenePath,
        CourtScenePath,
        HiddenRoomScenePath,
        LabyrinthScenePath,
    };

    public string CurrentSceneId { get; private set; } = "wend-hill-prologue";
    public bool IsTransitioning { get; private set; } = false;

    public event Action<string> OnTransitionStarted;
    public event Action<string> OnTransitionCompleted;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Clears the static when this director goes away. Without it a destroyed director leaves
    /// _instance pointing at a dead object, and the danger is subtle: Unity's == operator reports a
    /// destroyed object as null, but C#'s ?. and NUnit's Assert.IsNull do NOT. So
    /// `Instance?.TransitionTo(...)` would happily call into the corpse and throw
    /// MissingReferenceException, while `Instance == null` right beside it said everything was fine.
    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void TransitionTo(string sceneId, string scenePath)
    {
        if (IsTransitioning) return;
        if (GmHousePersistenceCoordinator.ActiveRun?.Mode == GmParlorAdaptiveMode.Recollection &&
            sceneId != "parlor" && sceneId != "boot")
        {
            sceneId = "boot";
            scenePath = BootScenePath;
        }
        StartCoroutine(TransitionRoutine(sceneId, scenePath));
    }

    public void TransitionToEntryHallFromKO() => TransitionTo("entry-hall", EntryHallScenePath);
    public void TransitionToParlor() => TransitionTo("parlor", ParlorScenePath);
    public void TransitionToShutTheBox() => TransitionTo("shut-the-box", ShutTheBoxScenePath);
    public void TransitionToCourt() => TransitionTo("court", CourtScenePath);
    public void TransitionToHiddenRoom() => TransitionTo("hidden-room", HiddenRoomScenePath);
    public void TransitionToLabyrinth() => TransitionTo("labyrinth", LabyrinthScenePath);

    public GmEndingType ResolveAndShowEnding()
    {
        GmEndingType ending = GmEndingManager.ResolveEnding();
        string title = GmEndingManager.GetEndingTitle(ending);
        Debug.Log($"[GmSceneDirector] CAMPAIGN CONCLUDED -> {title}");
        return ending;
    }

    IEnumerator TransitionRoutine(string sceneId, string scenePath)
    {
        IsTransitioning = true;
        bool recollection = GmHousePersistenceCoordinator.ActiveRun?.Mode ==
            GmParlorAdaptiveMode.Recollection;
        OnTransitionStarted?.Invoke(sceneId);
        Debug.Log($"[GmSceneDirector] Transitioning from {CurrentSceneId} -> {sceneId} ({scenePath})");

        // Recollection is a practice shell over a frozen known package. It must never overwrite the
        // player's campaign Continue checkpoint or create campaign progress of its own.
        if (!recollection)
            GmSaveSystem.SaveGame();

        // Load scene asynchronously. LoadSceneAsync returns null when the scene is not in Build
        // Settings or the path is wrong -- which is the state every scene here except the prologue
        // is in today. Falling through on null would move CurrentSceneId and fire
        // OnTransitionCompleted for a load that never happened, so the run would believe it had
        // changed scene while the player is still standing in the old one.
        AsyncOperation op = SceneManager.LoadSceneAsync(scenePath);
        if (op == null)
        {
            IsTransitioning = false;
            Debug.LogError($"[GmSceneDirector] FAILED: {scenePath} did not load — is it in Build Settings? " +
                           $"Staying in {CurrentSceneId}; {sceneId} was not entered.");
            yield break;
        }

        while (!op.isDone)
        {
            yield return null;
        }

        RecordLoadedScene(sceneId);
        // The pre-load save above protects the run if loading fails. This second write is the one
        // Continue actually needs: it records the room the player reached, not the room they left.
        if (!recollection) GmSaveSystem.SaveGame();
        else if (sceneId == "boot") GmHousePersistenceCoordinator.EndRecollectionShell();
        IsTransitioning = false;
        OnTransitionCompleted?.Invoke(sceneId);
        Debug.Log($"[GmSceneDirector] Transition complete. Active scene: {CurrentSceneId}");
    }

    void RecordLoadedScene(string sceneId)
    {
        CurrentSceneId = sceneId;
        GmRunStore.CurrentSceneId = sceneId;
        GmRunStore.LastCheckpoint = "spawn";
    }
}
