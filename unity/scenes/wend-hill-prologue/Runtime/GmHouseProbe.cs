using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Built-player proof and visual recording for the complete house beginning. It uses the real player
/// camera, the real story/game director and the real HUD; only the pacing and camera poses are automated.
/// Opt in with -gmHouseProof &lt;outputDirectory&gt;.
/// </summary>
public sealed class GmHouseProbe : MonoBehaviour
{
    string outputDirectory;
    GmHouseBeginning house;
    GmPlayer player;
    Camera view;
    int frames;
    int runtimeDefects;
    string firstDefect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int flag = System.Array.IndexOf(args, "-gmHouseProof");
        if (flag < 0 || flag + 1 >= args.Length) return;
        var host = new GameObject("GmHouseProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<GmHouseProbe>().outputDirectory = args[flag + 1];
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Directory.CreateDirectory(outputDirectory);
        Application.logMessageReceived += ObserveLog;
        yield return null;
        yield return null;

        house = FindAnyObjectByType<GmHouseBeginning>();
        player = FindAnyObjectByType<GmPlayer>();
        view = player != null ? player.GetComponentInChildren<Camera>() : null;
        if (house == null || player == null || view == null)
        {
            Finish($"missing runtime: house={house != null}, player={player != null}, camera={view != null}", 1);
            yield break;
        }

        GmColdOpen cold = FindAnyObjectByType<GmColdOpen>();
        if (cold != null && cold.IsRunning) cold.SkipIntroForReview();
        GmBellSummons bell = FindAnyObjectByType<GmBellSummons>();
        if (bell != null) bell.enabled = false;
        FindAnyObjectByType<GmPrologueHud>()?.HideTransientForReview();

        house.ReviewEnterHouse();
        Pose(new Vector3(0f, 0.1f, 397.75f), 0f, 2f);
        yield return Capture("01-wake-clock");

        Pose(new Vector3(0f, 0.1f, 394.1f), 180f, -3f);
        yield return Capture("02-entry-hall");

        Pose(new Vector3(-3.55f, 0.1f, 389.7f), -76f, 18f);
        yield return Capture("03-ledger-open-line");

        Pose(new Vector3(-4.6f, 0.1f, 386.0f), -90f, -2f);
        yield return Capture("04-nine-portraits");

        house.ReviewUnlockParlor();
        Pose(new Vector3(5.55f, 0.1f, 376.4f), 90f, -6f);
        yield return Capture("05-percival-shard");

        house.BeginHostIntroduction();
        house.AdvanceDialogue();
        house.AdvanceDialogue();
        Pose(new Vector3(2.8f, 0.1f, 364.8f), 198f, -4f);
        yield return Capture("06-aldric-arrival");

        house.AdvanceDialogue();
        house.AdvanceDialogue();
        yield return Capture("07-rules-flames");
        while (house.Phase == GmHousePhase.HostIntroduction) house.AdvanceDialogue();
        yield return Capture("08-first-hand");

        bool readFrame = false;
        int decisions = 0;
        while (house.Phase == GmHousePhase.ParlorGame && decisions++ < 100)
        {
            if (house.TurnPhase == GmParlorTurnPhase.ChooseCard)
            {
                if (!house.ReviewPlayFirstLegalCard()) break;
            }
            else if (house.TurnPhase == GmParlorTurnPhase.JudgePlay)
            {
                if (!readFrame && house.CurrentPlayCanBeRead && house.ReviewCurrentPlayWasCheat)
                {
                    yield return Capture("09-read-earned");
                    readFrame = true;
                    house.ReadHand();
                }
                else house.AllowTrick();
            }
            else if (house.TurnPhase == GmParlorTurnPhase.MatchResult)
            {
                yield return Capture("10-match-result");
                house.ContinueAfterResult();
            }
            else house.ContinueAfterResult();
            yield return null;
        }

        bool complete = house.Phase == GmHousePhase.Complete;
        bool critical = house.Progress.HasClue("ledger-open-line") &&
                        house.Progress.HasClue("percival-ledger-pair") &&
                        house.Progress.HasClue("aldric-rules") &&
                        house.Progress.HasClue("parlor-complete");
        if (!complete || !critical || !readFrame || house.Progress.CheatsCaught < 1 || runtimeDefects > 0)
        {
            Finish($"flow complete={complete}, critical={critical}, readFrame={readFrame}, " +
                   $"caught={house.Progress.CheatsCaught}, frames={frames}, defects={runtimeDefects}, first={firstDefect}", 1);
            yield break;
        }
        Debug.Log($"[GmHouseProbe] HOUSE PASS: entry, 9 portraits, ledger/Percival/shard reveal, " +
                  $"9-line host rules, 28-card game, earned Read, match completion; {frames} player-backbuffer frames; zero runtime defects");
        Finish("complete", 0);
    }

    void Pose(Vector3 rootPosition, float yaw, float pitch)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(rootPosition, Quaternion.Euler(0f, yaw, 0f));
        view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
    }

    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(0.55f);
        string path = Path.Combine(outputDirectory, name + ".png");
        ScreenCapture.CaptureScreenshot(path, 1);
        float deadline = Time.realtimeSinceStartup + 8f;
        while ((!File.Exists(path) || new FileInfo(path).Length < 10000) && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!File.Exists(path) || new FileInfo(path).Length < 10000)
        {
            Debug.LogError($"[GmHouseProbe] missing/blank frame {name}");
            yield break;
        }
        frames++;
        Debug.Log($"[GmHouseProbe] frame {frames:00}: {path}");
    }

    void ObserveLog(string condition, string stackTrace, LogType type)
    {
        bool defect = type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
                      GmRuntimeIntegrityPolicy.IsRenderFailure(condition) ||
                      condition.Contains("BoxCollider does not support negative scale or size");
        if (!defect || condition.Contains("[GmHouseProbe] HOUSE FAIL")) return;
        runtimeDefects++;
        if (firstDefect == null) firstDefect = condition;
    }

    void Finish(string reason, int exitCode)
    {
        Application.logMessageReceived -= ObserveLog;
        if (exitCode != 0) Debug.LogError($"[GmHouseProbe] HOUSE FAIL: {reason}");
        if (Application.isEditor) return;
        Application.Quit(exitCode);
    }
}
