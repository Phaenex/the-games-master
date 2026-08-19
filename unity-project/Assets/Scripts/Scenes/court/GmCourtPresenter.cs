using System.Linq;
using UnityEngine;

public sealed class GmCourtPresenter : MonoBehaviour
{
    GmCourtController court;
    Renderer[] gavelRenderers;
    Renderer[] seals;
    Light roleLight;
    Quaternion roleTarget;

    public bool IsBound => court != null && gavelRenderers != null && gavelRenderers.Length > 0 &&
        seals != null && seals.Length == 3 && roleLight != null;

    void Start()
    {
        court = GetComponent<GmCourtController>() ?? FindAnyObjectByType<GmCourtController>();
        GameObject gavel = GameObject.Find("BrassGavel");
        gavelRenderers = gavel != null ? gavel.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        seals = new[] { "WaxSeals", "WaxSeal_2", "WaxSeal_3" }
            .Select(GameObject.Find).Where(item => item != null)
            .SelectMany(item => item.GetComponentsInChildren<Renderer>(true)).Take(3).ToArray();
        roleLight = GameObject.Find("JudgeChandelierLight")?.GetComponent<Light>();
        if (court != null) court.OnStateChanged += Refresh;
        Refresh();
    }

    void Update()
    {
        if (roleLight != null)
            roleLight.transform.rotation = Quaternion.Slerp(roleLight.transform.rotation,
                roleTarget, Time.deltaTime * 2.8f);
    }

    void Refresh()
    {
        if (court == null) return;
        Color gavel = court.GavelIsTarnished
            ? new Color(0.16f, 0.17f, 0.14f) : new Color(0.32f, 0.17f, 0.07f);
        foreach (Renderer renderer in gavelRenderers ?? new Renderer[0])
            foreach (Material material in renderer.materials) material.SetColor("_BaseColor", gavel);

        int cracked = 3 - court.WaxSealsRemaining;
        for (int index = 0; seals != null && index < seals.Length; index++)
        {
            bool split = index < cracked;
            foreach (Material material in seals[index].materials)
                material.SetColor("_BaseColor", split
                    ? new Color(0.20f, 0.055f, 0.035f) : new Color(0.75f, 0.08f, 0.08f));
            seals[index].transform.localRotation = split
                ? Quaternion.Euler(0f, 18f * (index + 1), 10f) : Quaternion.identity;
        }

        if (roleLight != null)
        {
            Vector3[] roles = { new Vector3(0f, 0.8f, -1f), new Vector3(0f, 0.8f, 2.5f),
                new Vector3(0f, 1.6f, 5.7f) };
            Vector3 target = roles[Mathf.Clamp(court.CurrentArgumentIndex, 0, roles.Length - 1)];
            roleTarget = Quaternion.LookRotation(target - roleLight.transform.position);
        }
    }

    void OnDestroy()
    {
        if (court != null) court.OnStateChanged -= Refresh;
    }
}
