using System;
using UnityEngine;

[ExecuteAlways]
public sealed class GmBonesSceneHost : MonoBehaviour
{
    [SerializeField] bool directReviewOnly;
    GmBonesController controller;
    IDisposable reviewScope;

    public GmBonesController Controller => controller;
    public bool IsConfigured { get; private set; }
    public bool IsDirectReviewOnly => directReviewOnly;

    public void ConfigureForDirectReview() => directReviewOnly = true;

    public void ActivateDirectReviewPersistence()
    {
        if (reviewScope == null)
            reviewScope = GmBonesReviewPersistence.BeginScope("direct-review-host");
    }

    public void ReleaseDirectReviewPersistence()
    {
        IDisposable owned = reviewScope;
        reviewScope = null;
        owned?.Dispose();
    }

    void Awake()
    {
        if (!Application.isPlaying) return;
        if (directReviewOnly) ActivateDirectReviewPersistence();
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
        ActivateDirectReviewPersistence();
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

    // OnDisable fires for DestroyImmediate in EditMode even when this component never received
    // Awake. OnDestroy remains the runtime/domain-reload lifetime boundary; both are idempotent.
    void OnDisable() => ReleaseDirectReviewPersistence();
    void OnDestroy() => ReleaseDirectReviewPersistence();
}
