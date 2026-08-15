// The prologue shipped with zero AudioSources -- a fully silent horror walk. This is the runtime half
// of that fix: everything here is BUILT AND WIRED AT EDIT TIME by GmWendAmbience.Apply, so the saved
// scene already carries real AudioSource components with real clips assigned, and
// GmWendSceneContract can verify that by value after a genuine reload, the same way it verifies
// lighting. This component's own Update() only MODULATES what Apply already built; it creates
// nothing, so nothing here can silently fail to serialize the way a runtime-constructed graph could.
//
// The recipe -- one quiet modulated wind bed, sparse wildlife one-shots, distance-driven footsteps,
// never a second continuous bed stacked on top -- is not new. It is the mix the retired village pass
// arrived at after Nick called an earlier version "like being on a spaceship": three looping beds
// (wind, dark drone, crickets) mixed together and never stopping. The volumes and cadences below are
// copied from that already-reviewed recipe (GmAmbience.cs, ~/GamesMaster-Unity/Assets/Scripts), not
// re-guessed, because Wend Hill's problem is having no audio at all, not having the wrong amount of
// it. What IS new here is WHERE the wildlife anchors sit -- see GmWendAmbience for why.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmWendAmbienceSource : MonoBehaviour
{
    public AudioSource windBed;
    public AudioSource footstepSource;
    public AudioSource cricketSource;
    public AudioSource owlSource;
    public AudioClip[] footstepPool = System.Array.Empty<AudioClip>();
    public Vector3[] cricketAnchors = System.Array.Empty<Vector3>();
    public Vector3[] owlAnchors = System.Array.Empty<Vector3>();

    // The mix itself lives on GmFeelConfig -- these names stay so the edit-time builder and this
    // component keep reading one number each, and so the recipe below is still readable in place.
    // Read through a property rather than a field initializer: Unity refuses Resources.Load from a
    // MonoBehaviour's constructor, and a config lookup in an initializer would throw on scene load.
    public static float WindVolumeMin => GmFeelConfig.Active.windVolumeMin;
    public static float WindVolumeMax => GmFeelConfig.Active.windVolumeMax;
    static float CricketVolume => GmFeelConfig.Active.cricketVolume;
    static float OwlVolume => GmFeelConfig.Active.owlVolume;
    static float FootstepVolume => GmFeelConfig.Active.footstepVolume;
    static float FootstepStrideMetres => GmFeelConfig.Active.footstepStrideMetres;

    Transform selfTransform;
    Vector3 lastPosition;
    float strideDistance;
    float windSeed;
    float cricketTimer, owlTimer;
    int lastFootstepIndex = -1;

    void Start()
    {
        selfTransform = transform;
        lastPosition = selfTransform.position;
        windSeed = Random.value * 100f;
        cricketTimer = NextCricketGap();
        owlTimer = NextOwlGap();
        if (windBed != null && windBed.clip != null && !windBed.isPlaying)
        {
            windBed.timeSamples = Random.Range(0, windBed.clip.samples);
            windBed.Play();
        }
    }

    void Update()
    {
        UpdateWind();
        UpdateFootsteps();
        UpdateWildlife(Time.deltaTime);
    }

    void UpdateWind()
    {
        if (windBed == null || windBed.clip == null) return;
        if (!windBed.isPlaying) windBed.Play();
        float drift = Mathf.PerlinNoise(windSeed, Time.unscaledTime * 0.021f);
        windBed.volume = Mathf.Lerp(WindVolumeMin, WindVolumeMax, drift);
    }

    void UpdateFootsteps()
    {
        if (footstepSource == null || footstepPool.Length == 0) return;
        Vector3 position = selfTransform.position;
        float distance = Vector3.Distance(position, lastPosition);
        lastPosition = position;
        if (distance <= 0.001f) return;
        strideDistance += distance;
        if (strideDistance < FootstepStrideMetres) return;
        strideDistance = 0f;

        int index = Random.Range(0, footstepPool.Length);
        if (footstepPool.Length > 1 && index == lastFootstepIndex)
            index = (index + 1 + Random.Range(0, footstepPool.Length - 1)) % footstepPool.Length;
        lastFootstepIndex = index;

        footstepSource.pitch = Random.Range(0.96f, 1.045f);
        footstepSource.PlayOneShot(footstepPool[index], FootstepVolume);
    }

    void UpdateWildlife(float deltaTime)
    {
        cricketTimer -= deltaTime;
        if (cricketTimer <= 0f)
        {
            cricketTimer = NextCricketGap();
            PlayAt(cricketSource, cricketAnchors, CricketVolume);
        }

        owlTimer -= deltaTime;
        if (owlTimer <= 0f)
        {
            owlTimer = NextOwlGap();
            PlayAt(owlSource, owlAnchors, OwlVolume);
        }
    }

    // Minimum plus a random span, not Random.Range(min, max): the first call and every later one have
    // to draw the gap the same way, and a helper is the only way both sites stay that way.
    static float NextCricketGap() =>
        GmFeelConfig.Active.cricketIntervalMinSeconds +
        Random.value * GmFeelConfig.Active.cricketIntervalSpanSeconds;

    static float NextOwlGap() =>
        GmFeelConfig.Active.owlIntervalMinSeconds +
        Random.value * GmFeelConfig.Active.owlIntervalSpanSeconds;

    static void PlayAt(AudioSource source, Vector3[] anchors, float volume)
    {
        if (source == null || source.clip == null || anchors == null || anchors.Length == 0) return;
        source.transform.position = anchors[Random.Range(0, anchors.Length)];
        source.pitch = Random.Range(0.96f, 1.04f);
        source.PlayOneShot(source.clip, volume);
    }
}
