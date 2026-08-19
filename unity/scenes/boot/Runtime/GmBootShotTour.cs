// The Boot review is a fixed camera because the menu draws in screen space. Its seven states prove
// the title, Ledger interpretation, recovery confirmation, diagnostics feedback and high contrast.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmBootShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        new GmReviewShot("01-title-plate", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("02-ledger-interpretation", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("03-recovery-focus", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("04-recovery-confirm", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("05-diagnostics-preview", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("06-diagnostics-cancelled-hc", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("07-diagnostics-saved", new Vector3(0f, 1.7f, -6f), 0f, 0f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
    protected override bool CaptureReviewBackbuffer => true;

    string tourDirectory;
    GmBootMenu menu;
    bool historyReady;
    bool profileCorrupted;
    bool tourStarted;
    bool cleaned;

    protected override void BeforeTour()
    {
        tourStarted=true;
        // Boot is deliberately sparse. Require at least 0.5% non-black pixels while allowing the
        // percentile range to stay flat; a truly blank frame still fails the near-black ceiling.
        minimumLuminanceRange=0;
        maximumNearBlackFraction=0.995f;
        tourDirectory=Path.Combine(Path.GetTempPath(),
            "gm-boot-tour-"+Guid.NewGuid().ToString("N"));
        GmSaveSystem.ConfigureForTests(Path.Combine(tourDirectory,"continue.json"));
        GmHousePersistenceCoordinator.ConfigureForTests(Path.Combine(tourDirectory,"house"));
        GmRunStore.BeginNewRun();
        menu=FindAnyObjectByType<GmBootMenu>();
        if(menu==null) throw new InvalidOperationException("Boot tour found no menu");
        menu.Refresh();
    }

    protected override void BeforeShot(GmReviewShot shot)
    {
        switch(shot.Name)
        {
            case "01-title-plate":
                menu.Refresh();
                break;
            case "02-ledger-interpretation":
                EnsureMirrorHistory();
                menu.Refresh();
                menu.MoveFocus(1);
                menu.MoveFocus(1);
                if(!menu.Activate()) throw new InvalidOperationException("Ledger Review did not open");
                break;
            case "03-recovery-focus":
                EnsureCorruptProfile();
                menu.Refresh();
                break;
            case "04-recovery-confirm":
                menu.Refresh();
                if(!menu.Activate()) throw new InvalidOperationException("profile restore did not arm");
                break;
            case "05-diagnostics-preview":
                OpenDiagnostics(new TourDiagnosticsPicker(null,null));
                break;
            case "06-diagnostics-cancelled-hc":
                GmAccessibilitySettings.SetHighContrast(true);
                OpenDiagnostics(new TourDiagnosticsPicker(null,"diagnostics export cancelled"));
                menu.ConfirmDiagnosticsExport();
                break;
            case "07-diagnostics-saved":
                GmAccessibilitySettings.SetHighContrast(false);
                string destination=Path.Combine(tourDirectory,"tour-support.json");
                OpenDiagnostics(new TourDiagnosticsPicker(destination,null));
                if(!menu.ConfirmDiagnosticsExport())
                    throw new InvalidOperationException("tour diagnostics export did not complete");
                break;
        }
    }

    protected override IEnumerator BeforeShotSettled(GmReviewShot shot)
    {
        // UI Toolkit can rebuild glyph meshes one frame after a label/state change. Let both layout
        // and repaint finish so the proof never records the transient frame between them.
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(0.15f);
    }

    protected override string ValidateCapturedShot(GmReviewShot shot,Color32[] pixels)
    {
        int width=Screen.width;
        int height=Screen.height;
        if(width<=0||height<=0||pixels.Length!=width*height)
            return "capture dimensions do not match the Boot backbuffer";
        int titlePixels=CountVisible(pixels,width,height,0.2f,0.8f,0.68f,0.94f,400);
        int menuPixels=CountVisible(pixels,width,height,0.2f,0.8f,0.22f,0.72f,100);
        if(titlePixels<200) return "title plate is missing or unreadable";
        if(menuPixels<200) return "menu state is missing or unreadable";
        return null;
    }

    static int CountVisible(Color32[] pixels,int width,int height,
        float minX,float maxX,float minY,float maxY,int minimumRgbSum)
    {
        int x0=Mathf.FloorToInt(width*minX);
        int x1=Mathf.CeilToInt(width*maxX);
        int y0=Mathf.FloorToInt(height*minY);
        int y1=Mathf.CeilToInt(height*maxY);
        int count=0;
        for(int y=y0;y<y1;y++)
        for(int x=x0;x<x1;x++)
        {
            Color32 pixel=pixels[y*width+x];
            if(pixel.r+pixel.g+pixel.b>minimumRgbSum) count++;
        }
        return count;
    }

    void OpenDiagnostics(IGmSupportDiagnosticsDestinationPicker picker)
    {
        menu.Refresh();
        menu.MoveFocus(1);
        menu.MoveFocus(1);
        menu.SetDiagnosticsDestinationPickerForTests(picker);
        if(!menu.Activate()) throw new InvalidOperationException("diagnostics preview did not open");
    }

    void EnsureMirrorHistory()
    {
        if(historyReady) return;
        CompleteRun(GmParlorAdaptiveMode.Ordinary,1701);
        CompleteRun(GmParlorAdaptiveMode.Mirror,1702);
        historyReady=true;
    }

    void CompleteRun(GmParlorAdaptiveMode mode,int seed)
    {
        bool began=mode==GmParlorAdaptiveMode.Mirror
            ? GmHousePersistenceCoordinator.TryBeginMirrorRun(seed,out string beginError)
            : GmHousePersistenceCoordinator.TryBeginOrdinaryRun(seed,out beginError);
        if(!began) throw new InvalidOperationException(beginError);
        GmHouseRunGeneration run=GmHousePersistenceCoordinator.ActiveRun;
        var summary=new GmParlorBehaviorAccumulator(run.FrozenPackage);
        summary.RecordPlayerLead(new GmCard(GmSuit.Bones,7),1,7);
        summary.RecordAccept(GmTellObservation.Calm);
        summary.SealCompletedMatch(run.FrozenPackage,1,0);
        GmRunStore.LastCheckpoint="ending";
        if(!GmHousePersistenceCoordinator.TryCompleteEnding(
            GmEndingType.TrueEscape,summary,out string error))
            throw new InvalidOperationException(error);
    }

    void EnsureCorruptProfile()
    {
        if(profileCorrupted) return;
        EnsureMirrorHistory();
        string generations=Path.Combine(tourDirectory,"house","profile","generations");
        string newest=Directory.GetFiles(generations,"*.bin")
            .OrderBy(path=>path,StringComparer.Ordinal).Last();
        byte[] damaged=new byte[128];
        System.Text.Encoding.ASCII.GetBytes("NOTHOUSE").CopyTo(damaged,0);
        File.WriteAllBytes(newest,damaged);
        GmHousePersistenceCoordinator.ForgetActiveForTests();
        profileCorrupted=true;
    }

    protected override void AfterShotCaptured(GmReviewShot shot,string file,Texture2D captured)
    {
        if(shot.Name=="07-diagnostics-saved") Cleanup();
    }

    void OnDestroy() => Cleanup();

    void Cleanup()
    {
        if(!tourStarted||cleaned) return;
        cleaned=true;
        GmAccessibilitySettings.SetHighContrast(false);
        GmAccessibilitySettings.FlushPendingSave();
        GmHousePersistenceCoordinator.ResetForTests();
        GmSaveSystem.ResetTestConfiguration();
        if(!string.IsNullOrEmpty(tourDirectory)&&Directory.Exists(tourDirectory))
            Directory.Delete(tourDirectory,true);
    }

    sealed class TourDiagnosticsPicker : IGmSupportDiagnosticsDestinationPicker
    {
        readonly string destination;
        readonly string failure;
        public TourDiagnosticsPicker(string destination,string failure)
        { this.destination=destination;this.failure=failure; }
        public bool TryChooseDestination(out string path,out string error)
        {
            path=destination;error=failure??string.Empty;return failure==null;
        }
    }
}

#if UNITY_EDITOR
public static class GmBootShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Boot")]
    public static void ArmAndPlay()
    {
        GmSceneReviewTourMenu.ArmAndPlay<GmBootShotTour>("Assets/Scenes/Boot.unity");
    }
}
#endif
