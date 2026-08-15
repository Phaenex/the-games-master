// Toll 9 to waking inside. The reveal is arithmetic, not dialogue: the bell rang nine, the clock
// finished nine, no time passed. He was not carried -- he was expected.
// See docs/superpowers/specs/2026-07-17-the-ninth-bell.md ("The crossing").
//
// Sequence, in order:
//   1. CUT to black -- not a fade. fade jumps 0->1 in the same frame GmThreshold.BeginCrossing()
//      calls this coroutine (the code before the first `yield` in a coroutine runs synchronously
//      on StartCoroutine), and sound is killed the same frame via AudioListener.volume. The count
//      does not taper off; it lands.
//   2. ~2s of nothing. No image (still black), no sound (listener silent). Long enough to feel
//      like a mistake, not a transition.
//   3. Hearing returns first, wrong-ended: the listener comes back under a low-pass so it sounds
//      like it is arriving through water, and the whisper bed surfaces in the dark -- there is
//      nothing to look at yet.
//   4. Sight irises in badly (chromatic fringing via the low-pass easing back open, not sharpening
//      cleanly) while the clock's NINTH chime plays. The chime start and the iris start are the
//      SAME instant -- the bell's ninth toll and the clock's ninth chime must overlap, not queue.
//   5. The closing card, on the last chime. Player control returns. He is inside. He never saw a
//      door.
using System.Collections;
using UnityEngine;

public class GmCrossing : MonoBehaviour
{
    public float deadAir = 2.0f;       // "~2s of nothing" -- spec, verbatim
    public float whisperTime = 7.0f;   // spec: whispers run "roughly 6-8 seconds"
    public float irisTime = 6.0f;
    public float cardTime = 5.0f;

    GmDesignRuntime rt;
    GmPlayer player;
    float fade;                         // 1 = black. Owns the screen for the whole sequence --
                                         // GmThreshold hands off and draws nothing of its own once
                                         // this exists, or the two overlays fight and the screen
                                         // never clears: the exact "loading screen" failure mode.
    string card = "";

    public float Fade => fade;
    public string Card => card;
    public bool WhisperLoops => false;

    public IEnumerator Run()
    {
        rt = FindAnyObjectByType<GmDesignRuntime>();
        player = FindAnyObjectByType<GmPlayer>();
        if (player != null) player.SetControlBlocked(true);
        FindAnyObjectByType<GmSymptoms>()?.BeginCrossing();

        // 1. CUT. Runs synchronously before the first yield -- same frame as the ninth toll.
        fade = 1f;
        AudioListener.volume = 0f;
        // If the player is inside an outbuilding when toll nine lands, its lit interior must not
        // survive into the crossing -- costing frames through the black, or visible through a gap
        // when the iris opens in the WakeRoom.
        GmOutbuilding.CloseAllForCrossing();
        Debug.Log($"[GmCrossing] t={Time.time:F2} CUT — black, sound killed");

        // 1a. If a scene director exists, the room he wakes in is the REAL Entry Hall, and the black
        // is the window the load happens inside. The ninth-bell spec calls this scene's wake room
        // "the Entry Hall's STUB, not the Entry Hall" -- nine portraits, the ledger and shard #1 were
        // deferred to the room that now exists. Showing the stub and then loading the hall would be
        // two interiors for one waking.
        //
        // The curtain has to be raised BEFORE the load, and cannot be this component's own `fade`,
        // because the load destroys this object and the HUD that draws it. GmSceneArrival in the
        // arriving scene opens it. Everything below this block is the fallback for the prologue
        // review app, which is deliberately built with one scene and no director -- that build is
        // what Nick walks for the Phase 0 gate and its experience is unchanged.
        GmSceneDirector director = GmSceneDirector.Instance;
        if (director != null)
        {
            GmSceneCurtain curtain = GmSceneCurtain.Instance;
            if (curtain == null)
            {
                // Loud, not silent. A director without a curtain would load the Entry Hall behind a
                // black that does not exist, so the player would watch the house dissolve -- the one
                // thing Threshold Refusal cannot survive.
                Debug.LogError("[GmCrossing] FAILED: a scene director exists but no GmSceneCurtain — " +
                    "the crossing would be visible. Staying in the prologue's wake room.");
            }
            else
            {
                curtain.Raise();
                GmOutbuilding.CloseAllForCrossing();
                yield return new WaitForSeconds(deadAir);
                Debug.Log($"[GmCrossing] t={Time.time:F2} dead air over — crossing into the Entry Hall");
                var handoffLowPass = EnsureListenerLowPass();
                if (handoffLowPass != null) handoffLowPass.cutoffFrequency = 22000f;
                AudioListener.volume = 1f;
                Play("clock_chime", 0.9f, false);
                director.TransitionToEntryHallFromKO();
                Debug.Log($"[GmCrossing] t={Time.time:F2} HANDOFF — GmSceneArrival owns the waking now");
                yield break;
            }
        }

        // 1b. Move him while there is nothing to see -- he never saw a door, so he must never be seen
        // crossing one either. CharacterController fights a direct Transform set (its own collision
        // resolution shoves the object back toward where it thinks it belongs the next time it runs),
        // so it is disabled around the teleport and re-enabled immediately after, same frame.
        var wake = GameObject.Find("WakeRoom/WakePose");
        if (wake != null && player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(wake.transform.position, wake.transform.rotation);
            if (cc != null) cc.enabled = true;
            Debug.Log($"[GmCrossing] t={Time.time:F2} teleported to WakeRoom/WakePose {wake.transform.position}");
        }
        else
        {
            Debug.LogError("[GmCrossing] FAILED: no WakeRoom/WakePose — the player wakes in the mud");
        }

        // 2. Dead air. Nothing.
        yield return new WaitForSeconds(deadAir);
        Debug.Log($"[GmCrossing] t={Time.time:F2} dead air over — hearing returns, wrong-ended");

        // 3. Hearing first, underwater. A low-pass on the listener sells "wrong-ended" -- it opens
        // back up gradually across the iris in step 4, arriving at clean sound and clean sight
        // together rather than snapping hearing back sharp while vision is still ruined.
        var lp = EnsureListenerLowPass();
        if (lp != null) lp.cutoffFrequency = 700f;
        AudioListener.volume = 0.6f;
        var whisper = Play("whisper_bed", 0.5f, WhisperLoops);

        yield return new WaitForSeconds(whisperTime);

        // 4. The ninth chime and the iris begin in the SAME frame -- overlap, not queue. The
        // whisper bed fades out concurrently rather than cutting, so all three (whisper tail,
        // chime, iris) are audibly and visibly layered across the same few seconds, the way the
        // bell's ninth toll and this chime are meant to read as one continuing sound in two rooms.
        Debug.Log($"[GmCrossing] t={Time.time:F2} chime + iris begin together (overlap, not queue)");
        Play("clock_chime", 0.9f, false);
        AudioListener.volume = 1f;
        if (whisper != null) StartCoroutine(FadeOut(whisper, 2f));
        if (player != null) player.SetPitch(-45f);

        for (float t = 0; t < irisTime; t += Time.deltaTime)
        {
            float k = t / irisTime;
            fade = Mathf.SmoothStep(1f, 0f, k);            // sight, easing back badly, not snapping
            if (lp != null)
                lp.cutoffFrequency = Mathf.Lerp(700f, 22000f, k * k);  // hearing opens slower than sight
            if (player != null)
                player.SetPitch(Mathf.Lerp(-45f, 0f, Mathf.SmoothStep(0f, 1f, k)));
            yield return null;
        }
        fade = 0f;
        if (player != null) player.SetPitch(0f);
        if (lp != null) lp.cutoffFrequency = 22000f;
        Debug.Log($"[GmCrossing] t={Time.time:F2} vision resolved — the ninth chime finished");

        // 5. Closing card. Arithmetic, not dialogue.
        card = "Nine o'clock. On the hour. For the first time in my life, I was not late.";
        yield return new WaitForSeconds(cardTime);
        card = "";

        if (player != null) player.SetControlBlocked(false);   // he can move again. He is inside.
        Debug.Log($"[GmCrossing] t={Time.time:F2} CROSSING COMPLETE — player control restored");
    }

    AudioSource Play(string clip, float vol, bool loop)
    {
        var c = GmAmbience.Clip(clip);
        if (c == null) { Debug.LogWarning($"[GmCrossing] missing curated clip '{clip}'"); return null; }
        var go = new GameObject($"cross_{clip}");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = c; src.loop = loop; src.volume = vol; src.spatialBlend = 0f;
        GmAdaptiveIntentAuthoring.Audio(go, $"crossing-{clip}", GmAudioIntentKind.Stinger,
            loop ? GmAudioLoopPolicy.Required : GmAudioLoopPolicy.Never,
            "A bounded crossing cue exists only for the irreversible ninth-bell transition.");
        src.Play();
        if (!loop) Destroy(go, c.length + 0.5f);
        return src;
    }

    // Audio filters affect a source on the same object, or the complete mix when attached to the
    // AudioListener. The old crossing created this filter on the Systems object, which has neither,
    // so the promised underwater return could be a silent no-op. Reuse the symptom listener filter
    // when present and leave it open at 22 kHz after the crossing.
    AudioLowPassFilter EnsureListenerLowPass()
    {
        var listener = FindAnyObjectByType<AudioListener>();
        if (listener == null)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[GmCrossing] FAILED: no AudioListener or main camera for hearing transition");
                return null;
            }
            listener = camera.GetComponent<AudioListener>();
            if (listener == null) listener = camera.gameObject.AddComponent<AudioListener>();
        }
        var filter = listener.GetComponent<AudioLowPassFilter>();
        if (filter == null) filter = listener.gameObject.AddComponent<AudioLowPassFilter>();
        return filter;
    }

    IEnumerator FadeOut(AudioSource s, float secs)
    {
        float v0 = s.volume;
        for (float t = 0; t < secs; t += Time.deltaTime)
        {
            if (s == null) yield break;
            s.volume = Mathf.Lerp(v0, 0, t / secs);
            yield return null;
        }
        if (s != null)
        {
            s.volume = 0;
            s.Stop();
            Destroy(s.gameObject);
        }
    }

}

