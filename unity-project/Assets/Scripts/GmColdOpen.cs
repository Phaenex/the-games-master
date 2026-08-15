// The four cards before the walk: the debt, the daughter, the wife, the letter. Holds the player
// still until they finish -- the walk's beats have no setup without them. Skippable, because a
// player on their second run should not be made to sit through it; the pacing fix in the web build
// learned that the hard way.
using UnityEngine;
using UnityEngine.InputSystem;

public class GmColdOpen : MonoBehaviour
{
    public float cardSeconds = 5.2f;
    GmDesignRuntime rt;
    GmPlayer player;
    int index = -1;
    float next;
    bool done;

    public bool IsRunning => !done;
    public int CardNumber => done ? 0 : index + 1;
    public int CardCount => rt != null ? rt.coldOpen.Count : 0;

    void Start()
    {
        rt = FindAnyObjectByType<GmDesignRuntime>();
        player = FindAnyObjectByType<GmPlayer>();
        if (rt == null || rt.coldOpen.Count == 0) { done = true; return; }
        if (player != null) player.SetControlBlocked(true);
        Advance();
    }

    void Update()
    {
        if (done) return;
        var keyboard = Keyboard.current;
        if ((player != null && player.CancelPressedThisFrame) ||
            (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)) { Finish(); return; }
        bool advance = keyboard != null &&
                       (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                        keyboard.numpadEnterKey.wasPressedThisFrame);
        advance |= Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        advance |= player != null && player.InteractPressedThisFrame;
        if (advance || Time.time >= next) Advance();
    }

    void Advance()
    {
        index++;
        if (index >= rt.coldOpen.Count) { Finish(); return; }
        rt.ShowAftermath(rt.coldOpen[index]);   // reuses the centred card presentation
        next = Time.time + cardSeconds;
    }

    void Finish()
    {
        if (done) return;
        done = true;
        rt.ShowAftermath("");
        if (player != null) player.SetControlBlocked(false);
        Debug.Log("[GmColdOpen] complete — player released");
    }

    public void SkipIntroForReview() => Finish();
}

