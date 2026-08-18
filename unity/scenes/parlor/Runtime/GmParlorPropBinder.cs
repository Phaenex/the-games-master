using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GmParlorPropBinder : MonoBehaviour
{
    readonly Dictionary<GmCard, GmParlorCardView> views =
        new Dictionary<GmCard, GmParlorCardView>(GmParlorCore.TotalCards);

    public bool IsConfigured { get; private set; }

    void Awake()
    {
        if (!IsConfigured)
            TryConfigure(GetComponentsInChildren<GmParlorCardView>(true), out _);
    }

    public bool TryConfigure(IReadOnlyList<GmParlorCardView> cardViews, out string error)
    {
        if (cardViews == null)
        {
            error = "Parlor card views are null";
            return false;
        }

        var candidate = new Dictionary<GmCard, GmParlorCardView>(GmParlorCore.TotalCards);
        for (int index = 0; index < cardViews.Count; index++)
        {
            GmParlorCardView view = cardViews[index];
            if (view == null)
            {
                error = $"Parlor card view {index} is null";
                return false;
            }
            if (!candidate.TryAdd(view.PhysicalCard, view))
            {
                error = $"Duplicate physical Parlor card view: {view.PhysicalCard}";
                return false;
            }
        }
        if (candidate.Count != GmParlorCore.TotalCards)
        {
            error = $"Parlor needs {GmParlorCore.TotalCards} physical card views, found {candidate.Count}";
            return false;
        }

        foreach (GmSuit suit in new[] { GmSuit.Flames, GmSuit.Eyes, GmSuit.Teeth, GmSuit.Bones })
        {
            for (int rank = 1; rank <= GmParlorCore.RanksPerSuit; rank++)
            {
                GmCard card = new GmCard(suit, rank);
                if (!candidate.ContainsKey(card))
                {
                    error = $"Missing physical Parlor card view: {card}";
                    return false;
                }
            }
        }

        views.Clear();
        foreach (KeyValuePair<GmCard, GmParlorCardView> pair in candidate) views.Add(pair.Key, pair.Value);
        IsConfigured = true;
        error = string.Empty;
        return true;
    }

    public bool TryApply(GmParlorMatchSnapshot snapshot, out string error)
    {
        if (!IsConfigured)
        {
            error = "Parlor prop binder is not configured";
            return false;
        }

        GmParlorCardBinding[] bindings;
        try
        {
            bindings = GmParlorTableLayout.Build(snapshot);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        for (int index = 0; index < bindings.Length; index++)
        {
            GmParlorCardBinding binding = bindings[index];
            if (!views.TryGetValue(binding.PhysicalCard, out GmParlorCardView view))
            {
                error = $"Missing configured view for {binding.PhysicalCard}";
                return false;
            }
            view.ApplyBinding(binding);
        }
        error = string.Empty;
        return true;
    }

    public bool TryGetView(GmCard physicalCard, out GmParlorCardView view)
    {
        if (!views.TryGetValue(physicalCard, out view)) return false;
        return view != null;
    }

    public void SnapAvailable(GmParlorMatchSnapshot snapshot)
    {
        GmParlorCardBinding[] bindings;
        try
        {
            bindings = GmParlorTableLayout.Build(snapshot);
        }
        catch (Exception)
        {
            return;
        }
        for (int index = 0; index < bindings.Length; index++)
        {
            GmParlorCardBinding binding = bindings[index];
            if (views.TryGetValue(binding.PhysicalCard, out GmParlorCardView view) && view != null)
                view.ApplyBinding(binding);
        }
    }
}
