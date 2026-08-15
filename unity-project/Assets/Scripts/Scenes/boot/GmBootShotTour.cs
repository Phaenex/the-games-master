// A title screen is UI, so these three shots review the only things that can be wrong with one at
// this stage: that it renders at all, that it reads at distance, and that it survives a 16:9 frame.
// They are deliberately a fixed camera on an empty room -- the menu draws in screen space, so the
// review value is in the captured frame, not in where the camera stands.
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GmBootShotTour : GmSceneReviewTour
{
    static readonly GmReviewShot[] Shots =
    {
        new GmReviewShot("01-title-plate", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("02-menu-rows", new Vector3(0f, 1.7f, -6f), 0f, 0f),
        new GmReviewShot("03-focus-state", new Vector3(0f, 1.7f, -6f), 0f, 0f)
    };

    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;
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
