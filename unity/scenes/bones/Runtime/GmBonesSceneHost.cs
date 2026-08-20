using UnityEngine;

public sealed class GmBonesSceneHost : MonoBehaviour
{
    GmBonesController controller;

    public GmBonesController Controller => controller;
    public bool IsConfigured { get; private set; }

    void Awake()
    {
        if (!IsConfigured) InitializeCurrentRun();
    }

    public GmBonesInitializeResult InitializeCurrentRun()
    {
        controller = new GmBonesController();
        GmBonesInitializeResult result = controller.InitializeOrRestore();
        IsConfigured = result == GmBonesInitializeResult.StartedNew ||
            result == GmBonesInitializeResult.Restored;
        if (IsConfigured) ConfigureViews();
        return result;
    }

    public GmBonesInitializeResult RestartForReview(int runSeed)
    {
        GmRunSeed.ForceForReview(runSeed);
        GmRunStore.BeginNewRun();
        IsConfigured = false;
        return InitializeCurrentRun();
    }

    void ConfigureViews()
    {
        GmBonesInput input = GetComponent<GmBonesInput>();
        GmBonesHud hud = GetComponent<GmBonesHud>();
        GmBonesPresenter presenter = GetComponent<GmBonesPresenter>();
        GmBonesAudio audio = GetComponent<GmBonesAudio>();
        if (input == null || hud == null || presenter == null || audio == null)
        {
            IsConfigured = false;
            return;
        }
        if (!input.TryConfigure(controller, out _) ||
            !hud.TryConfigure(controller, input, out _) ||
            !presenter.TryConfigure(controller, out _) ||
            !audio.TryConfigure(controller, out _))
            IsConfigured = false;
    }
}
