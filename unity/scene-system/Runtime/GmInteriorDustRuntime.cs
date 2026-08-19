using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives checked-in interior scenes the same bounded dust contract as newly rebuilt scenes.
/// A rebuilt scene containing the owned P_Dust prefab wins; this creates only the lightweight
/// fallback needed when generated scene YAML intentionally is not churned for one decoration.
/// </summary>
public static class GmInteriorDustRuntime
{
    const string DustName = "InteriorDust_Bounded";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Subscribe()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!TryGetPlacement(scene.name, out Vector3 position, out Vector3 size)) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.transform.Find($"Atmosphere/{DustName}") != null || root.name == DustName) return;

        var dust = new GameObject(DustName);
        SceneManager.MoveGameObjectToScene(dust, scene);
        dust.transform.position = position;

        ParticleSystem particles = dust.AddComponent<ParticleSystem>();
        // AddComponent starts a ParticleSystem immediately. Unity asserts if its deterministic seed
        // changes while it is playing, so clear it before authoring the runtime modules.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.startLifetime = 8f;
        main.startSpeed = 0.018f;
        main.startSize = 0.012f;
        main.startColor = new Color(0.82f, 0.78f, 0.67f, 0.18f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        particles.useAutoRandomSeed = false;
        particles.randomSeed = StableSeed(scene.name);

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size;

        var emission = particles.emission;
        emission.rateOverTime = 3f;

        ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 0.03f;
        renderer.sharedMaterial = CreateDustMaterial();
        if (renderer.sharedMaterial == null)
        {
            // A hidden decoration is preferable to HDRP's magenta error material. The scene still
            // keeps lighting, fog and gameplay intact if its render pipeline is unavailable.
            renderer.enabled = false;
        }

        dust.AddComponent<GmAmbientParticleAccessibility>();
    }

    static Material CreateDustMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null || !shader.isSupported) return null;

        var material = new Material(shader)
        {
            name = "Interior Dust Runtime Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent,
        };
        Color tint = new Color(0.82f, 0.78f, 0.67f, 0.18f);
        if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", tint);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_BlendMode")) material.SetFloat("_BlendMode", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");

        Texture2D mote = CreateMoteTexture();
        if (material.HasProperty("_UnlitColorMap")) material.SetTexture("_UnlitColorMap", mote);
        else if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", mote);
        else material.mainTexture = mote;
        return material;
    }

    static Texture2D CreateMoteTexture()
    {
        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Interior Dust Mote",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    public static bool TryGetPlacement(string sceneName, out Vector3 position, out Vector3 size)
    {
        switch (sceneName)
        {
            case "EntryHall":
                position = new Vector3(-2.7f, 1.1f, 0.7f);
                size = new Vector3(4.5f, 2.0f, 5.5f);
                return true;
            case "Court":
                position = new Vector3(2.8f, 1.2f, 2.0f);
                size = new Vector3(3.0f, 1.8f, 4.0f);
                return true;
            case "ShutTheBox":
                position = new Vector3(-2.0f, 1.1f, 1.8f);
                size = new Vector3(2.4f, 1.5f, 2.4f);
                return true;
            case "HiddenRoom":
                position = new Vector3(0f, 1.0f, 1.6f);
                size = new Vector3(2.0f, 1.4f, 1.6f);
                return true;
            case "Parlor":
                position = new Vector3(-2.2f, 1.2f, 0.8f);
                size = new Vector3(3.0f, 1.6f, 3.0f);
                return true;
            default:
                position = default;
                size = default;
                return false;
        }
    }

    static uint StableSeed(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash == 0 ? 1u : hash;
        }
    }
}
