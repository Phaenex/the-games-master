// Small deterministic practical-light motion. This is deliberately restrained: the light should
// feel flame-fed when the player walks past, never pulse like an alarm or advertise a game effect.
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Light))]
public sealed class GmLightFlicker : MonoBehaviour
{
    [Min(0f)] public float baseIntensity = 20f;
    [Range(0f, 0.45f)] public float variation = 0.15f;
    [Min(0.05f)] public float speed = 2.2f;

    Light source;
    float phase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AnimateStartupScene() => EnsureLoadedScenePracticals();

    static void OnSceneLoaded(Scene _, LoadSceneMode __) => EnsureLoadedScenePracticals();

    void Awake()
    {
        source = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = source.intensity;
        phase = Mathf.Abs(transform.position.x * 0.731f + transform.position.z * 0.193f);
    }

    void Update()
    {
        if (source == null) return;
        float slow = Mathf.PerlinNoise(phase, Time.unscaledTime * speed);
        float fast = Mathf.PerlinNoise(phase + 13.7f, Time.unscaledTime * speed * 2.9f);
        float signed = ((slow * 0.72f + fast * 0.28f) - 0.5f) * 2f;
        source.intensity = baseIntensity * (1f + signed * variation);
    }

    /// Adds motion to named flame-fed practicals in an already-authored scene. This runtime seam is
    /// required because full-game builds consume the checked-in generated scenes; a source-only
    /// builder improvement must still affect an existing scene before the next deliberate rebuild.
    public static int EnsureLoadedScenePracticals()
    {
        int added = 0;
        foreach (Light light in Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional || !ShouldFlicker(light.name)) continue;
            GmLightFlicker motion = light.GetComponent<GmLightFlicker>();
            if (motion == null)
            {
                motion = light.gameObject.AddComponent<GmLightFlicker>();
                Configure(motion, light);
                added++;
            }
        }
        return added;
    }

    public static void Configure(GmLightFlicker motion, Light light)
    {
        if (motion == null || light == null) return;
        motion.baseIntensity = light.intensity;
        bool openFlame = ContainsAny(light.name, "fire", "brazier", "torch", "ember");
        motion.variation = openFlame ? 0.16f : 0.08f;
        motion.speed = openFlame ? 2.8f : 1.7f;
    }

    public static bool ShouldFlicker(string lightName)
    {
        if (string.IsNullOrWhiteSpace(lightName)) return false;
        if (ContainsAny(lightName, "moon", "window", "shaft", "ambient", "fill", "rim",
                "door", "entry", "exit", "verdict", "evidence", "mirror", "shard")) return false;
        return ContainsAny(lightName, "sconce", "lantern", "lamp", "chandelier", "fire",
            "brazier", "torch", "ember");
    }

    static bool ContainsAny(string value, params string[] tokens)
    {
        for (int index = 0; index < tokens.Length; index++)
            if (value.IndexOf(tokens[index], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }
}
