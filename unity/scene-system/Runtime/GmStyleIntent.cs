using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmStyleIntent : MonoBehaviour
{
    [SerializeField] string intentId;
    [SerializeField] string elementId;
    [SerializeField] GmEra elementEra;
    [SerializeField] GmEra contextEra;
    [SerializeField] bool deliberateContrast;
    [SerializeField, TextArea] string rationale;
    [SerializeField, Range(0f, 1f)] float maximumMaterialSmoothness = 1f;
    [SerializeField] GmStyleShotBudget[] shotBudgets = Array.Empty<GmStyleShotBudget>();

    public string IntentId => intentId;
    public string ElementId => elementId;
    public GmEra ElementEra => elementEra;
    public GmEra ContextEra => contextEra;
    public bool DeliberateContrast => deliberateContrast;
    public string Rationale => rationale;
    public float MaximumMaterialSmoothness => maximumMaterialSmoothness;
    public IReadOnlyList<GmStyleShotBudget> ShotBudgets => shotBudgets;

    public void Configure(string id, string subjectElementId, GmEra era, GmEra sceneEra,
        bool isDeliberateContrast, string why, float maximumSmoothness, GmStyleShotBudget[] budgets)
    {
        intentId = id;
        elementId = subjectElementId;
        elementEra = era;
        contextEra = sceneEra;
        deliberateContrast = isDeliberateContrast;
        rationale = why;
        maximumMaterialSmoothness = Mathf.Clamp01(maximumSmoothness);
        shotBudgets = budgets == null ? Array.Empty<GmStyleShotBudget>() :
            (GmStyleShotBudget[])budgets.Clone();
    }
}
