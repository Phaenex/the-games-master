using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Applies the prologue's explicit built-player render contract in memory. It never edits the
/// purchased HDRP asset on disk, and it is scene-owned so other Games Master builds do not inherit
/// this opening's budget. The output remains 1920x1080; HDRP renders at 67% internally and upscales
/// with FSR 1. Both performance probes record the resolved internal dimensions and filter.
/// </summary>
public sealed class GmWendRenderBudget : MonoBehaviour
{
    public const float InternalRenderPercentage = 67f;
    public const DynamicResUpscaleFilter UpscaleFilter =
        DynamicResUpscaleFilter.EdgeAdaptiveScalingUpres;
    public static float LastResolvedScale { get; private set; } = 1f;
    public static Vector2Int LastScaledSize { get; private set; }
    public static DynamicResUpscaleFilter LastResolvedFilter { get; private set; } =
        DynamicResUpscaleFilter.CatmullRom;
    Camera playerCamera;

    public static RenderPipelineSettings ConfigureSettings(RenderPipelineSettings settings)
    {
        GlobalDynamicResolutionSettings dynamicResolution = settings.dynamicResolutionSettings;
        dynamicResolution.enabled = true;
        dynamicResolution.useMipBias = true;
        dynamicResolution.minPercentage = InternalRenderPercentage;
        dynamicResolution.maxPercentage = InternalRenderPercentage;
        dynamicResolution.dynResType = DynamicResolutionType.Software;
        dynamicResolution.upsampleFilter = UpscaleFilter;
        dynamicResolution.forceResolution = true;
        dynamicResolution.forcedPercentage = InternalRenderPercentage;
        settings.dynamicResolutionSettings = dynamicResolution;
        return settings;
    }

    void Awake()
    {
        HDRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
        if (pipeline == null)
        {
            Debug.LogError("[GmWendRenderBudget] active pipeline is not HDRP");
            return;
        }

        pipeline.currentPlatformRenderPipelineSettings =
            ConfigureSettings(pipeline.currentPlatformRenderPipelineSettings);
        // The vendor scene uses dense LODGroups. Keep its authored LOD thresholds, but avoid drawing
        // two complete meshes during every cross-fade in this continuously moving first-person view.
        QualitySettings.enableLODCrossFade = false;
        // Do not let an uncapped Metal player race several frames ahead and then report the driver's
        // periodic queue drain as a gameplay hitch. One queued frame also keeps first-person input
        // latency bounded; both built-player probes record and enforce this runtime contract.
        QualitySettings.maxQueuedFrames = 1;
        // The opening targets 60 fps, so 120 is a useful ceiling when vSync is disabled: enough
        // headroom for high-refresh displays without burning cycles on 250+ fps queue churn.
        if (QualitySettings.vSyncCount == 0) Application.targetFrameRate = 120;

        playerCamera = GameObject.Find("Player")?.GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            playerCamera.allowDynamicResolution = true;
            HDAdditionalCameraData hd = playerCamera.GetComponent<HDAdditionalCameraData>();
            if (hd != null) hd.allowDynamicResolution = true;
        }
        RenderPipelineManager.endCameraRendering += RecordResolvedBudget;
        Debug.Log($"[GmWendRenderBudget] output=native internal={InternalRenderPercentage:0}% " +
                  $"upscaler={UpscaleFilter} software lodBias={QualitySettings.lodBias:0.00} " +
                  $"lodCrossFade={QualitySettings.enableLODCrossFade} queuedFrames={QualitySettings.maxQueuedFrames} " +
                  $"targetFps={Application.targetFrameRate}");
    }

    void RecordResolvedBudget(ScriptableRenderContext context, Camera camera)
    {
        if (camera != playerCamera) return;
        DynamicResolutionHandler dynamicResolution = DynamicResolutionHandler.instance;
        Vector2 scale = dynamicResolution.GetResolvedScale();
        LastResolvedScale = Mathf.Min(scale.x, scale.y);
        LastScaledSize = dynamicResolution.GetLastScaledSize();
        LastResolvedFilter = dynamicResolution.filter;
    }

    void OnDestroy() => RenderPipelineManager.endCameraRendering -= RecordResolvedBudget;
}
