// Generates the night sky the village is lit under.
//
// Until now the sky was a GradientSky: three colours blended vertically. It is a perfectly good
// ambient source and a completely dead thing to look at. In a game whose entire prologue happens
// outdoors at night, with the player repeatedly looking UP at a mansion roofline, a church tower and
// a bell, the sky is a major surface and it had nothing in it.
//
// This bakes a starfield to an equirectangular texture, imports it as a latlong cubemap and drives
// an HDRISky. Procedural rather than a purchased HDRI because the gradient has to keep matching the
// authored night palette in GmVillageNightRecipe, and because a real photographic sky would fight
// the fog and moon direction the scene is already committed to.
//
// Deterministic: fixed seed, so a rebuild produces the identical sky and review frames stay
// comparable across runs.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GmVillageSky
{
    const string LogTag = "GmVillageSky";
    public const string StarTexturePath = "Assets/Scenes/WendHillVillageStars.png";

    const int Width = 2048;
    const int Height = 1024;
    const int StarCount = 5200;
    const int Seed = 31337;

    /// Builds the texture if it is missing, then returns it as a cubemap ready for HDRISky.
    public static Cubemap EnsureStarfield(Color zenith, Color horizon)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(StarTexturePath);
        if (existing != null) return existing;

        Generate(zenith, horizon);
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(StarTexturePath);
        if (cube == null)
            Debug.LogWarning($"[{LogTag}] generated {StarTexturePath} but it did not import as a Cubemap");
        return cube;
    }

    [MenuItem("GamesMaster/Village/Regenerate Star Sky")]
    public static void Regenerate()
    {
        if (File.Exists(StarTexturePath)) AssetDatabase.DeleteAsset(StarTexturePath);
        GmVillageNightRecipe r = GmVillageNightRecipe.Base();
        Generate(r.skyTop, r.skyBottom);
        Debug.Log($"[{LogTag}] regenerated {StarTexturePath}");
    }

    static void Generate(Color zenith, Color horizon)
    {
        var rng = new System.Random(Seed);
        var pixels = new Color[Width * Height];

        // Vertical gradient first. v=0 is the bottom of the sphere, v=1 the top; the horizon band is
        // lifted slightly because even a moonless sky is brighter where it meets the ground haze.
        for (int y = 0; y < Height; y++)
        {
            float v = y / (float)(Height - 1);
            float elevation = Mathf.Abs(v * 2f - 1f);           // 0 at horizon, 1 at either pole
            Color band = Color.Lerp(horizon, zenith, Mathf.SmoothStep(0f, 1f, elevation));
            // A touch of extra lift right at the horizon, falling off fast.
            band += horizon * (0.35f * Mathf.Pow(1f - elevation, 6f));
            for (int x = 0; x < Width; x++) pixels[y * Width + x] = band;
        }

        // A faint band of unresolved starlight. Tilted off the horizon so it reads as a galaxy rather
        // than as a rendering seam, and kept dim enough to be something you only notice on a second
        // look -- which is exactly the register this game wants.
        const float bandTiltDeg = 32f;
        float tilt = bandTiltDeg * Mathf.Deg2Rad;
        for (int y = 0; y < Height; y++)
        {
            float theta = (1f - y / (float)(Height - 1)) * Mathf.PI;   // polar angle
            for (int x = 0; x < Width; x++)
            {
                float phi = x / (float)(Width - 1) * Mathf.PI * 2f;
                Vector3 dir = SphericalToCartesian(theta, phi);
                // Distance from the plane of the band.
                float d = Mathf.Abs(dir.y * Mathf.Cos(tilt) - dir.z * Mathf.Sin(tilt));
                float band = Mathf.Exp(-(d * d) / 0.012f);
                if (band <= 0.002f) continue;
                float mottle = 0.55f + 0.45f * Mathf.PerlinNoise(dir.x * 6f + 11f, dir.z * 6f + 3f);
                pixels[y * Width + x] += new Color(0.055f, 0.060f, 0.082f) * band * mottle;
            }
        }

        // Stars. Directions are sampled uniformly ON THE SPHERE and then converted to uv, rather
        // than picking uv uniformly -- the latter clumps thousands of stars into the poles, which is
        // the classic tell of a procedurally faked sky.
        for (int i = 0; i < StarCount; i++)
        {
            double u1 = rng.NextDouble(), u2 = rng.NextDouble();
            float cosTheta = (float)(1.0 - 2.0 * u1);
            float theta = Mathf.Acos(Mathf.Clamp(cosTheta, -1f, 1f));
            float phi = (float)(u2 * Math.PI * 2.0);

            int x = Mathf.Clamp(Mathf.RoundToInt(phi / (Mathf.PI * 2f) * (Width - 1)), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((1f - theta / Mathf.PI) * (Height - 1)), 0, Height - 1);

            // Brightness distribution: mostly faint, a handful genuinely bright. A uniform
            // distribution gives a flat spray of identical dots and reads as noise, not as sky.
            float roll = (float)rng.NextDouble();
            float mag = Mathf.Pow(roll, 5.5f);
            float brightness = Mathf.Lerp(0.035f, 1.5f, mag);

            // Slight colour spread around white; real starfields are not monochrome.
            float warm = (float)rng.NextDouble();
            var tint = new Color(
                Mathf.Lerp(0.72f, 1f, warm),
                Mathf.Lerp(0.80f, 0.97f, Mathf.Abs(warm - 0.35f) * 1.4f),
                Mathf.Lerp(1f, 0.78f, warm));

            Splat(pixels, x, y, tint * brightness, brightness > 0.55f ? 1 : 0);
        }

        var tex = new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true);
        tex.SetPixels(pixels);
        tex.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(StarTexturePath));
        File.WriteAllBytes(StarTexturePath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(StarTexturePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(StarTexturePath);
        if (importer == null)
        {
            Debug.LogWarning($"[{LogTag}] no importer for {StarTexturePath}");
            return;
        }
        // Cylindrical == latitude/longitude, which is what an equirectangular image is.
        importer.textureShape = TextureImporterShape.TextureCube;
        importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();

        Debug.Log($"[{LogTag}] baked {Width}x{Height} starfield, {StarCount} stars -> {StarTexturePath}");
    }

    /// Writes a star with a soft one-pixel skirt so it survives the cubemap resample. A single hard
    /// pixel mostly disappears into mip filtering and the sky ends up empty at runtime.
    static void Splat(Color[] pixels, int cx, int cy, Color colour, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= Height) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = (cx + dx + Width) % Width;   // wrap in longitude
                float falloff = (dx == 0 && dy == 0) ? 1f : 0.32f;
                pixels[y * Width + x] += colour * falloff;
            }
        }
    }

    static Vector3 SphericalToCartesian(float theta, float phi) => new Vector3(
        Mathf.Sin(theta) * Mathf.Cos(phi),
        Mathf.Cos(theta),
        Mathf.Sin(theta) * Mathf.Sin(phi));
}

