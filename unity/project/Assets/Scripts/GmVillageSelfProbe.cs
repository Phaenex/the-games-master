// Minimal standalone-only self-screenshot for the M1 village walk build. macOS `screencapture` from
// this automation environment appears unable to capture third-party window content (every attempt
// returned the desktop wallpaper despite CoreGraphics confirming the window is real, onscreen,
// correctly bounded) -- a screen-recording permission gap in the automation environment, not a game
// bug. ScreenCapture.CaptureScreenshot reads the app's own GPU backbuffer directly and needs no OS
// screen-recording permission, exactly like the proven pattern in GmStandaloneReviewProbe.cs. Opt-in
// via command-line flag only; inert on a normal launch (including Nick's).
using System.Collections;
using System.IO;
using UnityEngine;

public sealed class GmVillageSelfProbe : MonoBehaviour
{
    string outputPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallWhenRequested()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int flag = System.Array.IndexOf(args, "-gmVillageSelfProbe");
        if (flag < 0 || flag + 1 >= args.Length) return;
        var host = new GameObject("GmVillageSelfProbe");
        Object.DontDestroyOnLoad(host);
        var probe = host.AddComponent<GmVillageSelfProbe>();
        probe.outputPath = args[flag + 1];
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(3.0f);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        ScreenCapture.CaptureScreenshot(outputPath, 1);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!File.Exists(outputPath) && Time.realtimeSinceStartup < deadline) yield return null;
        Debug.Log(File.Exists(outputPath)
            ? $"[GmVillageSelfProbe] captured {outputPath} bytes={new FileInfo(outputPath).Length}"
            : $"[GmVillageSelfProbe] FAILED: screenshot never appeared at {outputPath}");
        Application.Quit(File.Exists(outputPath) ? 0 : 1);
    }
}

