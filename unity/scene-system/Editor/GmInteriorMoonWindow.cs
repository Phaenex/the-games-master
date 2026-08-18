using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Builds a visible high window and a cool moon shaft for fixed-exposure interiors.
/// The frame uses authored meshes rather than Unity primitive renderers so it remains valid
/// player-facing architecture under the room asset gate.
/// </summary>
public static class GmInteriorMoonWindow
{
    public readonly struct Result
    {
        public readonly GameObject fixture;
        public readonly GameObject light;

        public Result(GameObject fixture, GameObject light)
        {
            this.fixture = fixture;
            this.light = light;
        }
    }

    public static Result Build(Transform parent, string name, Vector3 position, Vector3 target,
        Vector2 size, float lumens, Vector3 inward = default)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("window name is required", nameof(name));

        var fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = position;

        Material frame = Material(name + "_Frame", new Color(0.12f, 0.065f, 0.035f), 0.05f, 0.38f);
        Material glass = Material(name + "_Glass", new Color(0.055f, 0.085f, 0.16f), 0.05f, 0.72f);
        const float rail = 0.11f;
        const float depth = 0.09f;

        GmOwnedPropFactory.CreateRoundedProp("MoonlitGlass", fixture.transform,
            position, Quaternion.identity, new Vector3(size.x, size.y, 0.045f), 0.01f, glass);
        GmOwnedPropFactory.CreateRoundedProp("FrameLeft", fixture.transform,
            position + Vector3.left * size.x * 0.5f, Quaternion.identity,
            new Vector3(rail, size.y + rail, depth), 0.018f, frame);
        GmOwnedPropFactory.CreateRoundedProp("FrameRight", fixture.transform,
            position + Vector3.right * size.x * 0.5f, Quaternion.identity,
            new Vector3(rail, size.y + rail, depth), 0.018f, frame);
        GmOwnedPropFactory.CreateRoundedProp("FrameTop", fixture.transform,
            position + Vector3.up * size.y * 0.5f, Quaternion.identity,
            new Vector3(size.x + rail, rail, depth), 0.018f, frame);
        GmOwnedPropFactory.CreateRoundedProp("FrameBottom", fixture.transform,
            position + Vector3.down * size.y * 0.5f, Quaternion.identity,
            new Vector3(size.x + rail, rail, depth), 0.018f, frame);
        GmOwnedPropFactory.CreateRoundedProp("Mullion", fixture.transform,
            position, Quaternion.identity, new Vector3(rail * 0.72f, size.y, depth), 0.014f, frame);

        inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.back;
        var lightObject = new GameObject("MoonWindowLight_" + name);
        lightObject.transform.SetParent(fixture.transform, true);
        lightObject.transform.position = position + inward * 0.24f;
        lightObject.transform.rotation = Quaternion.LookRotation(
            (target - lightObject.transform.position).normalized, Vector3.up);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(0.56f, 0.69f, 0.92f);
        light.range = Mathf.Max(8f, Vector3.Distance(position, target) + 5f);
        light.spotAngle = 68f;
        light.innerSpotAngle = 42f;
        light.shadows = LightShadows.Soft;
        light.lightUnit = LightUnit.Lumen;
        light.intensity = lumens;
        var hd = lightObject.AddComponent<HDAdditionalLightData>();
        hd.lightUnit = LightUnit.Lumen;
        hd.intensity = lumens;
        hd.range = light.range;
        hd.affectsVolumetric = true;
        GmAdaptiveIntentAuthoring.Light(lightObject, "environmental-" + name.ToLowerInvariant(),
            GmLightIntentKind.Environmental,
            "Cool moonlight enters through the visible high window and separates dark furniture from the warm oil practicals.");
        return new Result(fixture, lightObject);
    }

    static Material Material(string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new InvalidOperationException("[GmInteriorMoonWindow] HDRP/Lit is unavailable");
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}
