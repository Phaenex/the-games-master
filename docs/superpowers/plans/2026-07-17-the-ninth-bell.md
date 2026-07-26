# The Ninth Bell — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or
> superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.
>
> **Spec:** `docs/superpowers/specs/2026-07-17-the-ninth-bell.md` — read it first. It owns WHY.
> This owns HOW. **Runs after** `2026-07-17-prologue-intro-scene.md` (mansion/gate/car/bounds).

**Goal:** Replace the porch KO with the house's own clock. The invitation says nine. The chapel bell
tolls nine. On the ninth toll the player is taken wherever they stand, and comes to inside the house
as a longcase clock finishes its own ninth chime.

**Architecture:** One authority (`GmBellSummons`) owns the count and drives everything else off it —
symptoms, the crossing, the wake. Symptoms are a pure function of toll number so they can never
desync from the audio. The wake room is a **stub** of the Entry Hall, not the Entry Hall.

**Tech Stack:** Unity 6000.5.3f1 / HDRP · HDRP Volume post (Vignette, ChromaticAberration,
LensDistortion, FilmGrain) · AudioLowPassFilter · numpy + ffmpeg for placeholder audio ·
`scripts/unity-cli.mjs` for every Unity invocation.

---

## Read this first

All constraints from `2026-07-17-prologue-intro-scene.md`'s "Read this before touching anything"
still apply — sandbox off for Unity **and** Blender, Unity is single-instance, never commit, the
three.js/Unity yaw convention, the memory-only VolumeProfile trap.

**One extra, learned the hard way tonight:** `GmMansion.WarmTheWindows` mutated material sub-assets
and never called `AssetDatabase.SaveAssets()`, so the change lived only in the batchmode process that
made it and the tour (a *separate* Unity process) rendered a cold house. **Any editor code that
mutates an asset must SaveAssets() or it did not happen.** This plan touches materials and volume
profiles; assume the same trap is waiting.

## Canon this plan changes

- **`2026-07-14-opening-threshold.md`** — the KO half is superseded. Threshold Refusal's core is
  unchanged and non-negotiable: the doors never open, you never earn the civil doorway. What takes
  you is now the hour, not a hand.
- **`GmRareEvents`' chapel-bell rare event** ("3 tolls knocks you back") is **retired**. It was a
  curiosity; the bell is now the clock. Shipping both would be incoherent.

---

## Task 1: Placeholder audio

Five sounds do not exist. `chapel_bell.ogg` (7.0s) and `porch_breath.ogg` (8.0s) do, and ffmpeg +
numpy are installed. Generate placeholders now; real assets are a later sourcing pass.

**Files:**
- Create: `scripts/gen-bell-audio.py`
- Create (output): 4 `.ogg` files in `~/GamesMaster-Unity/Assets/Resources/Sfx/`

> Note: Task 11 of the intro plan moves SFX to `Assets/Resources/Sfx/`. Write there. If that task has
> not landed yet, write to wherever `GmAmbience.Find()` currently resolves and say so in your report.

- [ ] **Step 1: Write the generator**

```python
# scripts/gen-bell-audio.py
# Placeholder audio for The Ninth Bell. Synthesised, not sourced -- these are stand-ins so the
# sequence can be built and felt. Replace with real assets before ship.
#   python3 scripts/gen-bell-audio.py <out_dir>
import numpy as np, subprocess, sys, os

SR = 44100
out = sys.argv[1]
os.makedirs(out, exist_ok=True)

def write(name, samples):
    samples = np.clip(samples, -1, 1)
    raw = (samples * 32767).astype('<i2').tobytes()
    path = os.path.join(out, name + '.ogg')
    subprocess.run(
        ['ffmpeg', '-y', '-f', 's16le', '-ar', str(SR), '-ac', '1', '-i', 'pipe:0',
         '-c:a', 'libvorbis', '-q:a', '4', path],
        input=raw, check=True, capture_output=True)
    print(f'[gen] {path}  {len(samples)/SR:.2f}s')

def env(n, attack, decay):
    a = int(SR * attack); d = n - a
    return np.concatenate([np.linspace(0, 1, a), np.exp(-np.linspace(0, decay, d))])

# heartbeat: two thumps, low sine with a fast pitch drop. Under everything from toll 5.
n = int(SR * 1.2)
t = np.linspace(0, 1.2, n, endpoint=False)
beat = np.zeros(n)
for start, amp in ((0.0, 1.0), (0.32, 0.62)):
    s = int(start * SR); ln = int(SR * 0.28)
    tt = np.linspace(0, 0.28, ln, endpoint=False)
    f = 62 * np.exp(-tt * 7) + 34            # thump: pitch collapses fast
    beat[s:s+ln] += amp * np.sin(2*np.pi*np.cumsum(f)/SR) * np.exp(-tt*11)
write('heartbeat', beat * 0.85)

# whine: tinnitus. High sine, slight vibrato so it never sits still. Loops from toll 6.
n = int(SR * 4.0)
t = np.linspace(0, 4.0, n, endpoint=False)
vib = 1 + 0.004 * np.sin(2*np.pi*0.7*t)
whine = 0.16 * np.sin(2*np.pi*3150*t*vib) + 0.05 * np.sin(2*np.pi*6300*t*vib)
whine *= np.minimum(1, t/0.8)               # fade in so it can be layered without a click
write('ear_whine', whine)

# clock chime: a longcase is woodier and lower than a church bell. Struck rod: few harmonics,
# long decay. The player only ever hears the NINTH chime, so this is one strike.
n = int(SR * 5.5)
t = np.linspace(0, 5.5, n, endpoint=False)
chime = np.zeros(n)
for mult, amp in ((1.0, 1.0), (2.76, 0.42), (5.4, 0.18), (8.9, 0.07)):
    chime += amp * np.sin(2*np.pi*196.0*mult*t) * np.exp(-t * (1.1 + mult*0.28))
chime += 0.05 * np.random.default_rng(9).standard_normal(n) * np.exp(-t*38)   # hammer contact
write('clock_chime', chime / np.max(np.abs(chime)) * 0.9)

# whisper bed: speech-shaped noise -- the rhythm of talking with none of the content. Intelligible
# whispers are a threat; nearly-intelligible ones are the house. Deliberately has no words.
n = int(SR * 8.0)
rng = np.random.default_rng(41)
noise = rng.standard_normal(n)
syll = np.zeros(n)                           # syllable envelope ~4.5Hz, irregular
pos = 0
while pos < n:
    ln = int(SR * rng.uniform(0.07, 0.19))
    if pos + ln >= n: break
    syll[pos:pos+ln] = np.hanning(ln) * rng.uniform(0.35, 1.0)
    pos += ln + int(SR * rng.uniform(0.03, 0.22))
b = np.sin(2*np.pi*np.linspace(0, 8.0, n, endpoint=False) * 0.11)   # slow formant sweep
whisper = noise * syll * (0.5 + 0.5*b) * 0.30
write('whisper_bed', whisper)
```

- [ ] **Step 2: Generate**

```bash
python3 scripts/gen-bell-audio.py ~/GamesMaster-Unity/Assets/Resources/Sfx
```

Expected: four `[gen]` lines — `heartbeat` 1.20s, `ear_whine` 4.00s, `clock_chime` 5.50s,
`whisper_bed` 8.00s. **Listen is not available to you; verify by duration and file size** (each
should be >4KB — a silent ogg compresses to almost nothing, which is the failure to catch).

- [ ] **Step 3: Verify they are not silent**

```bash
for f in heartbeat ear_whine clock_chime whisper_bed; do
  p=~/GamesMaster-Unity/Assets/Resources/Sfx/$f.ogg
  echo "$f: $(du -h "$p" | cut -f1)  peak=$(ffmpeg -i "$p" -af volumedetect -f null - 2>&1 | grep max_volume)"
done
```

Expected: every `max_volume` between roughly **-1 dB and -14 dB**. A value of `-91 dB` means silence
— the generator ran and produced nothing, which the file's existence would otherwise hide.

---

## Task 2: `GmBellSummons` — the nine-toll authority

One component owns the count. Everything else reads it. This is deliberate: symptoms driven by their
own timers will drift out of sync with the audio and the whole effect dies.

**Files:**
- Create: `Assets/Scripts/GmBellSummons.cs`
- Modify: `Assets/Editor/GmEstateBuilderV2.cs` (attach it)
- Modify: `Assets/Scripts/GmRareEvents.cs` (retire the old bell)

- [ ] **Step 1: Write it**

```csharp
// Assets/Scripts/GmBellSummons.cs
// The house's clock. The invitation says nine; the chapel bell tolls nine; on the ninth you are
// taken wherever you stand. See docs/superpowers/specs/2026-07-17-the-ninth-bell.md.
//
// Single authority ON PURPOSE: symptoms, the crossing and the wake all read Toll from here rather
// than running their own timers, which would drift out of sync with the audio and kill the effect.
//
// Cadence is fixed real time and ignores route and action. That is the whole point -- no matter what
// the player does, they are counted in at nine. A real bell does not take five minutes to ring nine;
// nobody says so, and the spacing is deniable right up until it isn't.
using UnityEngine;

public class GmBellSummons : MonoBehaviour
{
    public float firstTollDelay = 45f;      // after the gate locks: the drive gets its own beats first
    public float tollInterval = 30f;        // 9 tolls ~= 4m45s of grounds
    public int Toll { get; private set; }   // 0..9, read by everything downstream
    public bool Taken => Toll >= 9;

    // The bell is diegetic: it comes from the chapel and is louder near it. Distance attenuates the
    // volume; it never attenuates the count.
    static readonly Vector3 ChapelPos = new Vector3(28.9f, 6f, 30f);

    GmAmbience amb;
    GmDesignRuntime rt;
    GmThreshold threshold;
    AudioSource bell;
    float next = -1f;
    bool armed;

    void Start()
    {
        amb = FindFirstObjectByType<GmAmbience>();
        rt = FindFirstObjectByType<GmDesignRuntime>();
        threshold = FindFirstObjectByType<GmThreshold>();

        var go = new GameObject("BellSource");
        go.transform.position = ChapelPos;
        bell = go.AddComponent<AudioSource>();
        bell.spatialBlend = 1f;             // 3D: locatable, walkable-to
        bell.rolloffMode = AudioRolloffMode.Logarithmic;
        bell.minDistance = 12f;
        bell.maxDistance = 260f;            // audible from the whole estate, never comfortable
        bell.clip = GmAmbience.Clip("chapel_bell");
        bell.playOnAwake = false;
    }

    /// Armed by GmThreshold when the gate locks: the bell can only count someone the house already
    /// has. Retreating to the car before the gate (the seventh state) beats it outright.
    public void Arm()
    {
        if (armed) return;
        armed = true;
        next = Time.time + firstTollDelay;
        Debug.Log($"[GmBell] armed — first toll in {firstTollDelay}s, then every {tollInterval}s");
    }

    void Update()
    {
        if (!armed || Toll >= 9 || Time.time < next) return;

        Toll++;
        next = Time.time + tollInterval;
        if (bell != null && bell.clip != null) bell.Play();
        Debug.Log($"[GmBell] toll {Toll}/9");

        if (Toll == 9)
        {
            Debug.Log("[GmBell] TAKEN — the count is finished");
            rt?.ShowBeat("The card said nine.",
                "I have never been on time for anything in my life. Ask Mara. Ask the bank. Ask the men who call before sunrise. I was early for this.");
            threshold?.BeginCrossing();
        }
    }
}
```

- [ ] **Step 2: Add the clip accessor `GmBellSummons` needs**

`GmAmbience.Find` is private. Add a public passthrough beside it (do not duplicate the lookup):

```csharp
    /// Public accessor so other systems can own their own AudioSource (the bell is 3D and lives at
    /// the chapel, so it cannot go through GmAmbience's 2D one-shot source).
    public static AudioClip Clip(string name) => Find(name);
```

- [ ] **Step 3: Retire the old bell rare event**

In `GmRareEvents.cs`, delete the chapel-bell block (the forecourt-gated `bellTimer`/`bellTolls`
knock-back). Leave the window-figure and lamp hooks. Add:

```csharp
// The chapel bell used to be a rare event that knocked you back on the third toll. It is now the
// house's clock and belongs to GmBellSummons -- see the ninth-bell spec. Shipping both would ring
// the same bell for two different reasons.
```

- [ ] **Step 4: Attach in the builder**

In `GmEstateBuilderV2.BuildPlayerAndSystems`, before `GmThreshold`:

```csharp
        systems.AddComponent<GmBellSummons>();
        systems.AddComponent<GmThreshold>();
```

- [ ] **Step 5: Rebuild and verify it compiles and arms**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
grep -E 'error CS' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: `✓ done: rebuild`, zero `error CS`.

---

## Task 3: The symptoms

Toll 4 is the first symptom. Every toll after is worse. **Monotonic — never recovers.** Nothing the
player does helps, and that is the point.

| Toll | Vignette | Chromatic ab. | Lens distortion | Lowpass (Hz) | Audio |
|---|---|---|---|---|---|
| 0-3 | 0 | 0 | 0 | 22000 | normal |
| 4 | 0.18 | 0.10 | 0 | 9000 | — |
| 5 | 0.30 | 0.20 | -0.05 | 6000 | heartbeat in |
| 6 | 0.42 | 0.34 | -0.10 | 4000 | whine in |
| 7 | 0.55 | 0.48 | -0.16 | 2400 | ambience out |
| 8 | 0.72 | 0.66 | -0.24 | 1200 | footsteps silent, walk speed 60% |
| 9 | 1.0 (cut) | — | — | 0 | everything cuts |

**Files:**
- Create: `Assets/Scripts/GmSymptoms.cs`

- [ ] **Step 1: Write it**

```csharp
// Assets/Scripts/GmSymptoms.cs
// Head and hearing coming apart, one toll at a time. Reads GmBellSummons.Toll rather than running
// its own timer -- a second timer would drift against the audio and the effect dies.
//
// Monotonic by construction: values are a pure function of the toll number, so nothing can recover
// between tolls. There is no cure and the player must not be able to find one.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class GmSymptoms : MonoBehaviour
{
    static readonly float[] VignetteAt   = { 0, 0, 0, 0, 0.18f, 0.30f, 0.42f, 0.55f, 0.72f, 1.00f };
    static readonly float[] ChromaticAt  = { 0, 0, 0, 0, 0.10f, 0.20f, 0.34f, 0.48f, 0.66f, 0.66f };
    static readonly float[] DistortionAt = { 0, 0, 0, 0, 0.00f, -0.05f, -0.10f, -0.16f, -0.24f, -0.24f };
    static readonly float[] LowpassAt    = { 22000, 22000, 22000, 22000, 9000, 6000, 4000, 2400, 1200, 500 };

    GmBellSummons bell;
    GmPlayer player;
    Vignette vignette;
    ChromaticAberration chromatic;
    LensDistortion distortion;
    AudioLowPassFilter lowpass;
    AudioSource heart, whine;
    float baseWalkSpeed;
    int applied = -1;

    void Start()
    {
        bell = FindFirstObjectByType<GmBellSummons>();
        player = FindFirstObjectByType<GmPlayer>();
        if (player != null) baseWalkSpeed = player.walkSpeed;

        // Own volume, high priority: must win over the estate's NightVolume without editing it.
        var volGo = new GameObject("SymptomVolume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;   // runtime-only: no asset, so the SaveAssets trap does not apply

        vignette = profile.Add<Vignette>(true);   vignette.intensity.Override(0);
        chromatic = profile.Add<ChromaticAberration>(true); chromatic.intensity.Override(0);
        distortion = profile.Add<LensDistortion>(true); distortion.intensity.Override(0);

        var cam = Camera.main;
        if (cam != null)
        {
            lowpass = cam.gameObject.AddComponent<AudioLowPassFilter>();
            lowpass.cutoffFrequency = 22000;
        }

        heart = MakeLoop("heartbeat", 0f);
        whine = MakeLoop("ear_whine", 0f);
    }

    AudioSource MakeLoop(string clip, float vol)
    {
        var go = new GameObject($"sym_{clip}");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = GmAmbience.Clip(clip);
        src.loop = true;
        src.spatialBlend = 0f;   // in his head, not in the world
        src.volume = vol;
        if (src.clip != null) src.Play();
        else Debug.LogWarning($"[GmSymptoms] missing clip '{clip}' — run scripts/gen-bell-audio.py");
        return src;
    }

    void Update()
    {
        if (bell == null) return;
        int t = Mathf.Clamp(bell.Toll, 0, 9);
        if (t == applied) return;
        applied = t;

        vignette?.intensity.Override(VignetteAt[t]);
        chromatic?.intensity.Override(ChromaticAt[t]);
        distortion?.intensity.Override(DistortionAt[t]);
        if (lowpass != null) lowpass.cutoffFrequency = LowpassAt[t];

        if (heart != null) heart.volume = t >= 5 ? Mathf.Min(0.85f, 0.25f * (t - 4)) : 0f;
        if (whine != null) whine.volume = t >= 6 ? Mathf.Min(0.55f, 0.16f * (t - 5)) : 0f;

        // Toll 7: the world goes away and leaves him with the inside of his own head.
        var amb = FindFirstObjectByType<GmAmbience>();
        if (amb != null) amb.SetBedVolume(t >= 7 ? 0f : 1f);

        // Toll 8: his legs stop being his.
        if (player != null) player.walkSpeed = t >= 8 ? baseWalkSpeed * 0.6f : baseWalkSpeed;

        Debug.Log($"[GmSymptoms] toll {t}: vig={VignetteAt[t]:F2} chrom={ChromaticAt[t]:F2} lowpass={LowpassAt[t]}Hz");
    }
}
```

- [ ] **Step 2: Add the ambience duck `GmSymptoms` calls**

`GmAmbience` needs `SetBedVolume(float)`. Read the file, find its bed AudioSource(s), and add:

```csharp
    /// Toll 7 takes the world away and leaves him with the inside of his own head.
    public void SetBedVolume(float scale)
    {
        // Multiply the beds' authored volumes rather than assigning, so this cannot become the new
        // source of truth for how loud the night is.
        ...
    }
```

Implement against the real field names in that file. Do not guess them — read it.

- [ ] **Step 3: Attach + verify**

Add `systems.AddComponent<GmSymptoms>();` after `GmBellSummons` in the builder. Rebuild, confirm no
`error CS`.

- [ ] **Step 4: Prove the curve fires without waiting 5 minutes**

Temporarily set `firstTollDelay = 2f; tollInterval = 2f;` in the builder, rebuild, tour, then grep:

```bash
grep -E '\[GmBell\] toll|\[GmSymptoms\] toll' ~/GamesMaster-Unity/Logs/cli-tour.log
```

Expected: tolls 1..9 in order, symptoms rising monotonically, `TAKEN` at 9. **Then put the real
values back and rebuild.** Leaving the test cadence shipped would ring nine bells in 18 seconds.

---

## Task 4: `GmThreshold` rewrite — the porch KO retires

Threshold Refusal's core survives: the doors never open. What dies is the porch-only trigger, the
settle glide, and the hand at the collar.

**Files:**
- Modify: `Assets/Scripts/GmThreshold.cs`

- [ ] **Step 1: Rewrite**

Keep: the gate lock (first backward move past `gateZ`, `gate_slam` + `gate_lock`, the beat). Add
`bell.Arm()` there — the bell can only count someone the house already has.

Delete: `arrivalZ` auto-settle, the glide, `glimpseSaid`/`thresholdSaid`/`knockStarted`, the collar
beats, `ko_thud`, the camera-roll knockdown.

Add `public void BeginCrossing()`, called by `GmBellSummons` on toll 9. Move the fade there.

Keep the porch beat **as a beat, not an ending** — reaching the porch now says the doors don't open
and then leaves you standing there while the count continues, which is worse:

```csharp
        // Reaching the porch is no longer an ending -- it is a discovery. The doors do not open, and
        // the hour arrives anyway, wherever you happen to be standing when it does.
        if (!porchSaid && z <= arrivalZ)
        {
            porchSaid = true;
            rt?.ShowBeat("The doors did not open.",
                "Callers waited. Guests were taken. Warm light held in the seam, then went still. I stood there like a man with an appointment, which is what I was.");
        }
```

- [ ] **Step 2: Verify the gate still locks and the bell arms**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
grep -E 'error CS' ~/GamesMaster-Unity/Logs/cli-rebuild.log
```

Expected: builds clean, zero `error CS`. `GmThreshold` must still reference `gate_slam`/`gate_lock`.

---

## Task 5: The crossing

The most important seam in the Prologue. Get it wrong and it is a loading screen.

**Files:**
- Create: `Assets/Scripts/GmCrossing.cs`

- [ ] **Step 1: Write it**

```csharp
// Assets/Scripts/GmCrossing.cs
// Toll 9 to waking inside. The reveal is arithmetic, not dialogue: the bell rang nine, the clock
// finished nine, no time passed. He was not carried -- he was expected.
//
// Sequence (spec: The crossing):
//   cut to black (NOT a fade) -> ~2s of nothing -> hearing returns wrong-ended -> whispers in the
//   dark -> sight irises in badly -> the clock's NINTH chime resolves as vision does.
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GmCrossing : MonoBehaviour
{
    public float deadAir = 2.0f;      // long enough to feel like a mistake
    public float whisperTime = 7.0f;
    public float irisTime = 6.0f;

    GmDesignRuntime rt;
    GmPlayer player;
    float fade;                        // 1 = black
    string card = "";

    public IEnumerator Run()
    {
        rt = FindFirstObjectByType<GmDesignRuntime>();
        player = FindFirstObjectByType<GmPlayer>();
        if (player != null) player.enabled = false;

        // 1. CUT. Not a fade -- the count does not taper off, it lands.
        fade = 1f;
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(deadAir);

        // 2. Hearing first, wrong-ended.
        AudioListener.volume = 0.6f;
        var whisper = Play("whisper_bed", 0.5f, true);
        yield return new WaitForSeconds(whisperTime);
        if (whisper != null) StartCoroutine(FadeOut(whisper, 2f));

        // 3. The ninth chime, and sight coming back to meet it. The bell's ninth toll and the
        //    clock's ninth chime are the same nine in two rooms; they must overlap, not queue.
        Play("clock_chime", 0.9f, false);
        AudioListener.volume = 1f;
        for (float t = 0; t < irisTime; t += Time.deltaTime)
        {
            fade = Mathf.SmoothStep(1f, 0f, t / irisTime);
            yield return null;
        }
        fade = 0f;

        card = "Nine o'clock. On the hour. For the first time in my life, I was not late.";
        yield return new WaitForSeconds(5f);
        card = "";
        if (player != null) player.enabled = true;   // he can move again. He is inside.
    }

    AudioSource Play(string clip, float vol, bool loop)
    {
        var c = GmAmbience.Clip(clip);
        if (c == null) { Debug.LogWarning($"[GmCrossing] missing clip '{clip}'"); return null; }
        var go = new GameObject($"cross_{clip}");
        var src = go.AddComponent<AudioSource>();
        src.clip = c; src.loop = loop; src.volume = vol; src.spatialBlend = 0f;
        src.Play();
        return src;
    }

    IEnumerator FadeOut(AudioSource s, float secs)
    {
        float v0 = s.volume;
        for (float t = 0; t < secs; t += Time.deltaTime) { s.volume = Mathf.Lerp(v0, 0, t/secs); yield return null; }
        s.volume = 0;
    }

    void OnGUI()
    {
        if (fade > 0.01f)
        {
            var c = GUI.color; GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }
        if (!string.IsNullOrEmpty(card))
        {
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Italic, wordWrap = true };
            st.normal.textColor = new Color(0.48f, 0.44f, 0.36f);
            GUI.Label(new Rect(Screen.width*0.15f, Screen.height*0.42f, Screen.width*0.7f, 160), card, st);
        }
    }
}
```

- [ ] **Step 2: Wire `GmThreshold.BeginCrossing` to start it**

```csharp
    public void BeginCrossing()
    {
        var cross = FindFirstObjectByType<GmCrossing>();
        if (cross == null) { Debug.LogError("[GmThreshold] FAILED: no GmCrossing — toll 9 has nowhere to go"); return; }
        StartCoroutine(cross.Run());
    }
```

Attach `GmCrossing` in the builder beside the others.

- [ ] **Step 3: Verify with the fast cadence**

Set the 2s test cadence again, rebuild, tour, then:

```bash
grep -E '\[GmBell\]|\[GmCrossing\]|error CS' ~/GamesMaster-Unity/Logs/cli-tour.log
```

Expected: tolls 1-9, `TAKEN`, no missing-clip warnings. **Restore the real cadence afterward.**

---

## Task 6: The wake room (Entry Hall **stub**)

He comes to *inside*. The Entry Hall proper — nine portraits, the ledger, 22 POIs, shard #1 — is
**Phase 1** and is not this task. This is the room the crossing needs to land in, and nothing more.

**Files:**
- Create: `Assets/Editor/GmWakeRoom.cs`

- [ ] **Step 1: Build a dark interior with a clock**

A small room built from Leartes HDRP interior pieces via `GmEstateBuilderV2.FindAssetPrefab` (which
already prefers HDRP and warns on built-in). Requirements, not decoration:

- Placed **far from the estate** (e.g. `z = +400`) so its walls cannot leak into the grounds' shots.
- Marble-ish floor: he wakes on it. (`"Cold. The marble had my cheek."` is the Entry Hall's line —
  do **not** use it here; that beat belongs to Phase 1.)
- **A longcase clock**, prominent, visible from the wake pose. Search the owned library:
  `find ~/Projects/the-games-master/assets/models/unity -iname '*clock*'`. If nothing suitable
  exists, build a placeholder from primitives and **log loudly that it is a placeholder** — do not
  quietly ship a box and call it a clock.
- One warm light. Nothing else. It is a stub.

- [ ] **Step 2: Move the player there on crossing**

The player teleports during dead air, under black. Add to `GmCrossing.Run()` right after the cut:

```csharp
        var wake = GameObject.Find("WakeRoom/WakePose");
        if (wake != null && player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;             // CharacterController fights teleports
            player.transform.SetPositionAndRotation(wake.transform.position, wake.transform.rotation);
            if (cc != null) cc.enabled = true;
        }
        else Debug.LogError("[GmCrossing] FAILED: no WakeRoom/WakePose — the player wakes in the mud");
```

- [ ] **Step 3: Verify he is actually inside**

Rebuild with the 2s cadence, tour, and grep for the FAILED line (must be absent). Then confirm by
measurement, not vibes — write a temp editor script that logs `WakeRoom/WakePose`'s world position
and confirms a floor renderer exists beneath it. **Delete the temp script after.**

---

## Task 7: Branch beats

All 5 existing beats are z-line triggers on the drive, so a player who turns off it walks in silence.
Each branch gets one entry beat, fired once, on entering its rect.

**Files:**
- Modify: `Assets/Scripts/GmDesignRuntime.cs`
- Modify: `unity/design-data/prologue-design.json` **and** `Assets/StreamingAssets/prologue-design.json`

- [ ] **Step 1: Add `branchBeats` to BOTH design-data copies**

```json
  "branchBeats": [
    { "rect": [3, 13.2, 26.3, 30.7],    "main": "A gravel path, going right.",
      "sub": "Toward the stones. I told myself I was being thorough. I was being slow." },
    { "rect": [8.6, 24.6, 12.6, 40.6],  "main": "They buried people here. They still are.",
      "sub": "One hole open and waiting, and no name cut for it yet. I did the arithmetic I always do: how much, and who for." },
    { "rect": [24.6, 38.5, 26, 34],     "main": "The bell is right above me.",
      "sub": "Close enough to touch the door it hangs behind. It rang anyway. It did not care that I was here to hear it." },
    { "rect": [-13.2, -3, 19.8, 24.2],  "main": "A path left, into the dark of the garden.",
      "sub": "Mara would have known what half of it used to be. She knew the names of things. I only ever learned the odds." },
    { "rect": [-23.6, -8.6, 15.6, 34.6],"main": "Someone kept this garden once.",
      "sub": "Then stopped, and nobody came after. There is a version of my house that looks like this in about a year." },
    { "rect": [-26, -21, 34, 41],       "main": "The long way round the flank.",
      "sub": "Every step out here is a step I am not taking toward that door. I noticed. I kept doing it." },
    { "rect": [-33.5, -23, 40, 56],     "main": "Coach doors, and chalk under the moss.",
      "sub": "Numbers. Forty-one. Nine. A third one scratched out so hard it tore the wood. Somebody else's arithmetic, or mine, written before I got here." }
  ]
```

**Voice check before this ships** (`/voice-check`). The coach-yard beat is the load-bearing one: 41
and 9 are his debts, chalked decades before he arrived.

- [ ] **Step 2: Fire them on rect entry**

`GmDesignRuntime` already parses walk rects with a regex over 4-float arrays. That regex will now
also match `branchBeats[].rect` — **check `ParseWalkRects`' filter still yields exactly 8 walk rects**
or the walk bounds will silently gain 7 extra boxes. Verify with the log line
(`rects=8`), and if it now reads 15, scope the parse to the `walkRects` key.

Add a `branchBeats` list, hydrate it, and in `Update()` fire a beat when the player enters its rect
(once). Reuse `ShowBeat`.

- [ ] **Step 3: Verify**

```bash
node scripts/unity-cli.mjs rebuild 2>&1 | grep -E '✓|✗'
node scripts/unity-cli.mjs tour 2>&1 | grep -E '✓|✗'
grep -E '\[GmDesignRuntime\]' ~/GamesMaster-Unity/Logs/cli-tour.log
```

Expected: `rects=8` still (NOT 15), and `branchBeats=7`. The tour teleports through several rects, so
`[Beat]` lines should appear for branches it passes through.

---

## Task 8: The invitation

**Files:**
- Modify: both `prologue-design.json` copies (`coldOpen`)

- [ ] **Step 1: Add the card after the existing line 4**

The existing line 4 ends `"...Only a crest in red wax: a stag, one antler snapped."` Add a fifth
entry — the letter itself:

```
"THE HOUSE ON WEND HILL\n\nNine o'clock.\n\nTake the old road. It will turn you.\nCome as you are. Bring nothing.\n\n— a friend"
```

Canon (`house-history.md`): **never a street address, never a family name for the house.** "Wend" is
an old word for a turn in a road. No "do not be late" — the compulsion is never named by the house.
"— a friend" is the three-stage reveal's signature (story bible §6); it is NOT "the friend in the
walls."

- [ ] **Step 2: Verify both copies and the count**

```bash
diff unity/design-data/prologue-design.json ~/GamesMaster-Unity/Assets/StreamingAssets/prologue-design.json && echo "✓ identical"
node scripts/unity-cli.mjs rebuild && node scripts/unity-cli.mjs tour
grep '\[GmDesignRuntime\]' ~/GamesMaster-Unity/Logs/cli-tour.log
```

Expected: identical, and `coldOpen=5`.

---

## Task 9: Tests

**Files:**
- Modify: `Assets/Tests/EditMode/GmEstateBuildTests.cs`

- [ ] **Step 1: Add these tests**

```csharp
    /// The bell is the ending now. If it is not in the scene the Prologue cannot finish at all.
    [Test]
    public void BellSummonsAndCrossingExist()
    {
        Assert.IsNotNull(Object.FindFirstObjectByType<GmBellSummons>(), "no GmBellSummons — the Prologue has no ending");
        Assert.IsNotNull(Object.FindFirstObjectByType<GmCrossing>(), "no GmCrossing — toll 9 has nowhere to go");
        Assert.IsNotNull(Object.FindFirstObjectByType<GmSymptoms>(), "no GmSymptoms — the bell tolls with no consequence");
    }

    /// A shipped test cadence would ring nine bells in 18 seconds. Easy to leave behind, invisible
    /// in a log, and it would destroy the scene's entire pace.
    [Test]
    public void BellCadenceIsShippable()
    {
        var bell = Object.FindFirstObjectByType<GmBellSummons>();
        Assert.GreaterOrEqual(bell.tollInterval, 20f, "toll interval is test-speed — the real one is 30s");
        Assert.GreaterOrEqual(bell.firstTollDelay, 20f, "first toll delay is test-speed");
    }

    /// He must wake somewhere with a floor. Waking in the mud is the failure this catches.
    [Test]
    public void WakeRoomExistsWithAPose()
    {
        Assert.IsNotNull(GameObject.Find("WakeRoom/WakePose"), "no wake pose — the crossing lands nowhere");
    }
```

- [ ] **Step 2: Run**

```bash
node scripts/unity-cli.mjs test 2>&1 | tail -8
```

Expected: **35/35** (23 Shut the Box + 9 estate + 3 bell).

---

## Final verification

- [ ] `npm run test:all` — C# 23, JS 23, browser 47
- [ ] `node scripts/unity-cli.mjs test` — 35/35
- [ ] Fast-cadence tour: tolls 1-9 in order, symptoms monotonic, `TAKEN`, no missing clips
- [ ] **Restore the real cadence** (`firstTollDelay=45`, `tollInterval=30`) and confirm the test catches it if not
- [ ] Read all 12 shots by eye — the grounds must be unchanged by all of this
- [ ] Confirm nothing committed

## Deliberately out of scope

- **The Entry Hall proper.** Nine portraits, the ledger, Percival's nameplate, 22 POIs, shard #1.
  Phase 1. This plan ships a stub with a clock and nothing else.
- **Real VO.** The whispers are unintelligible by design, so they are processed audio, not
  performance. Real VO is a Phase 9 decision and belongs to Aldric. Built VO-ready; not committed.
- **Real audio.** All five sounds here are synthesised placeholders. A sourcing pass replaces them.
- **The letter as an object.** The three-stage reveal is Phase 1; this is the card only.
