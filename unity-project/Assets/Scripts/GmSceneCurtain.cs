// The black that survives a scene load.
//
// Canon: the front doors never open, and on the ninth bell you wake inside having never been
// admitted (docs/superpowers/specs/2026-07-17-the-ninth-bell.md). For that to be true the player
// must not see the change of rooms — and a scene load destroys everything in the outgoing scene,
// including GmCrossing's own fade and the HUD that draws it. So whatever holds the black has to
// outlive the load, which means it belongs to the one object that already does: the director.
//
// The spec also settles which room you wake in. It calls the prologue's wake room "the Entry Hall's
// STUB, not the Entry Hall", with the real room — nine portraits, the ledger, shard #1 — deferred.
// That room exists now. Showing the stub and then loading the real hall would be two interiors for
// one waking, so in the full game the stub is never seen: the black is the load window, which is
// what a cut to black is for.
//
// IMGUI on purpose. It needs no canvas, no panel settings and no scene to belong to, which is
// exactly the set of things that stop existing halfway through this sequence.
using System.Collections;
using UnityEngine;

public sealed class GmSceneCurtain : MonoBehaviour
{
    public static GmSceneCurtain Instance { get; private set; }

    /// Drawn last. GmPrologueHud renders through UI Toolkit, which is composited above IMGUI, so a
    /// low depth here would put the prologue's own overlay on top of the black it is meant to be
    /// hidden behind for the frame or two before that scene unloads.
    const int OverlayDepth = -1000;

    float alpha;
    string card = "";
    Texture2D pixel;
    GUIStyle cardStyle;

    public float Alpha => alpha;
    public string Card => card;

    /// True once the curtain owns the screen. The arriving scene reads this to know it is waking a
    /// player out of a crossing rather than starting cold, so a room entered any other way is not
    /// forced through a wake it never had.
    public bool IsRaised => alpha > 0.999f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    // Same reason GmSceneDirector clears its own static: Unity reports a destroyed object as null
    // through ==, but C#'s ?. does not, so a stale Instance is a call into a corpse rather than a
    // null check that fails honestly.
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pixel != null) Destroy(pixel);
    }

    /// Instant black. Not a fade — the cut lands on the same frame as the ninth toll.
    public void Raise()
    {
        alpha = 1f;
        card = "";
    }

    public void ShowCard(string text) => card = text ?? "";

    /// Sight easing back badly, not snapping. Mirrors the curve GmCrossing uses for its in-scene
    /// iris so the two paths through this moment feel identical.
    public IEnumerator Open(float seconds)
    {
        if (seconds <= 0f) { alpha = 0f; yield break; }
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            alpha = Mathf.SmoothStep(1f, 0f, t / seconds);
            yield return null;
        }
        alpha = 0f;
    }

    void OnGUI()
    {
        if (alpha <= 0.001f && string.IsNullOrEmpty(card)) return;
        GUI.depth = OverlayDepth;

        if (alpha > 0.001f)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel);
            GUI.color = previous;
        }

        if (string.IsNullOrEmpty(card)) return;
        if (cardStyle == null)
        {
            cardStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.RoundToInt(Screen.height * 0.035f),
            };
            cardStyle.normal.textColor = new Color(0.90f, 0.86f, 0.78f);
        }
        float width = Screen.width * 0.7f;
        GUI.Label(new Rect((Screen.width - width) * 0.5f, Screen.height * 0.42f, width,
            Screen.height * 0.2f), card, cardStyle);
    }
}
