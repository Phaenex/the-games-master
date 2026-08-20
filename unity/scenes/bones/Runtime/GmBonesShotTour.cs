using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmBonesShotTour : GmSceneReviewTour
{
    const int ReviewSeed = 1;
    static readonly GmReviewShot[] Shots =
    {
        new GmReviewShot("01-ready-table", new Vector3(0f, 1.58f, -2.65f), 0f, 14f),
        new GmReviewShot("02-readable-faces", new Vector3(0f, 1.28f, -1.42f), 0f, 32f),
        new GmReviewShot("03-focused-choice", new Vector3(-0.2f, 1.55f, -2.7f), 0f, 14f),
        new GmReviewShot("04-round-two", new Vector3(0f, 1.48f, -2.35f), 0f, 20f),
        new GmReviewShot("05-round-three", new Vector3(0.2f, 1.42f, -2.05f), 0f, 24f),
        new GmReviewShot("06-pending-loaded-six", new Vector3(0f, 1.28f, -1.65f), 0f, 34f),
        new GmReviewShot("07-challenge-evidence", new Vector3(-0.35f, 1.35f, -1.8f), 0f, 30f),
        new GmReviewShot("08-proceed-evidence", new Vector3(0.35f, 1.35f, -1.8f), 0f, 30f),
        new GmReviewShot("09-high-contrast-200", new Vector3(0f, 1.55f, -2.55f), 0f, 17f),
        new GmReviewShot("10-reduced-motion-complete", new Vector3(0f, 1.4f, -2.1f), 0f, 25f),
    };
    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
    protected override bool CaptureReviewBackbuffer => true;

    protected override void BeforeTour()
    {
        GmBonesReviewPersistence.EnsureActive("shot-tour");
        GmAccessibilitySettings.SetHighContrast(false);
        GmAccessibilitySettings.SetTextScale(1f);
        GmAccessibilitySettings.SetReducedMotion(false);
        Host.RestartForReview(ReviewSeed);
    }

    protected override void BeforeShot(GmReviewShot shot)
    {
        GmBonesInput input = GetComponent<GmBonesInput>();
        switch (shot.Name)
        {
            case "03-focused-choice": input.HandleNavigationIntent(Vector2.right); input.HandleNavigationIntent(Vector2.zero); break;
            case "04-round-two": input.FocusThenConfirm(0); break;
            case "05-round-three": input.FocusThenConfirm(0); break;
            case "06-pending-loaded-six": input.FocusThenConfirm(0); break;
            case "07-challenge-evidence": input.ChallengeAction(); break;
            case "08-proceed-evidence": ReplayToPendingForReview(input); input.ConfirmAction(); break;
            case "09-high-contrast-200":
                GmAccessibilitySettings.SetHighContrast(true);
                GmAccessibilitySettings.SetTextScale(GmAccessibilitySettings.MaxTextScale);
                break;
            case "10-reduced-motion-complete":
                GmAccessibilitySettings.SetReducedMotion(true);
                ReplayToPendingForReview(input); input.ChallengeAction();
                break;
        }
    }

    protected override IEnumerator BeforeShotSettled(GmReviewShot shot)
    {
        // UI Toolkit needs several panel updates after each public action before the backbuffer
        // contains the new focus, evidence rows, and accessibility layout.
        yield return null; yield return null; yield return null; yield return null;
    }

    public void ReplayToPendingForReview(GmBonesInput input, int confirmedRounds = 3)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        Host.RestartForReview(ReviewSeed);
        for (int round = 0; round < confirmedRounds; round++) input.FocusThenConfirm(0);
        if (Host.Controller.Phase != GmBonesMatchPhase.AwaitingIntervention)
            throw new InvalidOperationException(
                "[GmBonesShotTour] deterministic public action replay did not reach loaded-six intervention");
    }
    GmBonesSceneHost Host => GetComponent<GmBonesSceneHost>();
}

#if UNITY_EDITOR
public static class GmBonesShotTourMenu
{
    [MenuItem("GamesMaster/Scenes/Review Tour Bones")]
    public static void ArmAndPlay() => GmSceneReviewTourMenu.ArmAndPlay<GmBonesShotTour>("Assets/Scenes/Bones.unity");
}
#endif
