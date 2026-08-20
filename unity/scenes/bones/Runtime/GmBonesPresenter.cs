using UnityEngine;

public sealed class GmBonesPresenter : MonoBehaviour
{
    [SerializeField] GmBonesDieView[] dice = new GmBonesDieView[0];
    GmBonesController controller;

    public bool IsConfigured => controller != null && dice != null && dice.Length == 3 &&
        dice[0] != null && dice[1] != null && dice[2] != null;
    public bool SupportsAccessibility => true;

    public void BindDice(GmBonesDieView[] dieViews) => dice = dieViews ?? new GmBonesDieView[0];

    public bool TryConfigure(GmBonesController tableController, out string error)
    {
        if (tableController == null || !tableController.IsInitialized || dice == null || dice.Length != 3)
        {
            error = "Bones presenter needs an initialized controller and exactly three dice";
            return false;
        }
        if (controller != null) controller.OnStateChanged -= Refresh;
        controller = tableController;
        controller.OnStateChanged += Refresh;
        GmAccessibilitySettings.OnChanged -= Refresh;
        GmAccessibilitySettings.OnChanged += Refresh;
        Refresh();
        error = string.Empty;
        return true;
    }

    public void Refresh()
    {
        if (!IsConfigured)
        {
            GmAccessibilitySettings.OnChanged -= Refresh;
            return;
        }
        GmBonesPresentationState state = GmBonesPresentationModel.Project(controller);
        for (int index = 0; index < dice.Length; index++)
            dice[index].SetFace(state.Dice[index], index == state.ChangedDieSlot,
                GmAccessibilitySettings.ReducedMotion);
    }

    void OnDestroy()
    {
        if (controller != null) controller.OnStateChanged -= Refresh;
        GmAccessibilitySettings.OnChanged -= Refresh;
    }
}
