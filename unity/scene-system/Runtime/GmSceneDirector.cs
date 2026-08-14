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

    public void TransitionTo(string sceneId, string scenePath)
    {
        if (IsTransitioning) return;
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
        OnTransitionStarted?.Invoke(sceneId);
        Debug.Log($"[GmSceneDirector] Transitioning from {CurrentSceneId} -> {sceneId} ({scenePath})");

        // Save progress before scene change
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

        CurrentSceneId = sceneId;
        IsTransitioning = false;
        OnTransitionCompleted?.Invoke(sceneId);
        Debug.Log($"[GmSceneDirector] Transition complete. Active scene: {CurrentSceneId}");
    }
}
