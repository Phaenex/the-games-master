using System;
using UnityEngine;

[ExecuteAlways]
public sealed class GmStudySceneHost : MonoBehaviour
{
    [SerializeField] bool directReviewOnly;
    GmStudyController controller;
    IDisposable reviewScope;
    bool mutationSurfacesSuspended;

    public GmStudyController Controller => controller;
    public bool IsConfigured { get; private set; }
    public bool IsDirectReviewOnly => directReviewOnly;
    public bool MutationSurfacesSuspended => mutationSurfacesSuspended;

    public void ConfigureForDirectReview() => directReviewOnly = true;

    public void ActivateDirectReviewPersistence()
    {
        if (reviewScope == null)
            reviewScope = GmStudyReviewPersistence.BeginScope("direct-review-host");
        ResumeMutationSurfaces();
    }

    public void ReleaseDirectReviewPersistence()
    {
        SuspendMutationSurfaces();
        IDisposable owned = reviewScope;
        reviewScope = null;
        owned?.Dispose();
    }

    void OnEnable()
    {
        if (!Application.isPlaying || !directReviewOnly) return;
        ActivateDirectReviewPersistence();
    }

    void Awake()
    {
        if (!Application.isPlaying) return;
        if (directReviewOnly) ActivateDirectReviewPersistence();
        if (!IsConfigured) InitializeCurrentRun();
    }

    public GmStudyInitializeResult InitializeCurrentRun()
    {
        controller = new GmStudyController();
        GmStudyInitializeResult result = controller.InitializeOrRestore();
        IsConfigured = result == GmStudyInitializeResult.StartedNew ||
            result == GmStudyInitializeResult.Restored;
        if (IsConfigured) ConfigureViews();
        return result;
    }

    public GmStudyInitializeResult RestartForReview(int runSeed)
    {
        ActivateDirectReviewPersistence();
        GmRunSeed.ForceForReview(runSeed);
        GmRunStore.BeginNewRun();
        IsConfigured = false;
        return InitializeCurrentRun();
    }

    void ConfigureViews()
    {
        GmStudyInput input = GetComponent<GmStudyInput>();
        GmStudyHud hud = GetComponent<GmStudyHud>();
        GmStudyPresenter presenter = GetComponent<GmStudyPresenter>();
        GmStudyAudio audio = GetComponent<GmStudyAudio>();
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

    void SuspendMutationSurfaces()
    {
        GmStudyInput input = GetComponent<GmStudyInput>();
        GmStudyHud hud = GetComponent<GmStudyHud>();
        GmStudyPresenter presenter = GetComponent<GmStudyPresenter>();
        GmStudyAudio audio = GetComponent<GmStudyAudio>();
        input?.SuspendInteractions();
        hud?.SuspendInteractions();
        presenter?.SuspendPresentation();
        audio?.SuspendAudio();
        if (input != null) input.enabled = false;
        if (hud != null) hud.enabled = false;
        if (presenter != null) presenter.enabled = false;
        if (audio != null) audio.enabled = false;
        mutationSurfacesSuspended = true;
    }

    void ResumeMutationSurfaces()
    {
        GmStudyInput input = GetComponent<GmStudyInput>();
        GmStudyHud hud = GetComponent<GmStudyHud>();
        GmStudyPresenter presenter = GetComponent<GmStudyPresenter>();
        GmStudyAudio audio = GetComponent<GmStudyAudio>();
        if (input != null) { input.enabled = true; input.ResumeInteractions(); }
        if (hud != null) { hud.enabled = true; hud.ResumeInteractions(); }
        if (presenter != null) { presenter.enabled = true; presenter.ResumePresentation(); }
        if (audio != null) { audio.enabled = true; audio.ResumeAudio(); }
        mutationSurfacesSuspended = false;
    }

    // Sibling mutation surfaces are suspended before the prior backend is made visible again.
    void OnDisable() => ReleaseDirectReviewPersistence();
    void OnDestroy() => ReleaseDirectReviewPersistence();
}
