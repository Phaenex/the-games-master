using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmCompositionElement : MonoBehaviour
{
    [SerializeField] string elementId;
    [SerializeField] string clusterId;
    [SerializeField] string assetFamily;
    [SerializeField, TextArea] string rationale;
    [SerializeField] GmCompositionRole role = GmCompositionRole.Detail;
    [SerializeField] GmSpatialRelation relation = GmSpatialRelation.Grounded;
    [SerializeField] string relationTargetId;
    [SerializeField] float maximumRelationDistance = 5f;
    [SerializeField] float declaredSurfaceY;
    [SerializeField] float groundTolerance = 0.2f;
    [SerializeField] bool blocksRoutes = true;

    public string ElementId => elementId;
    public string ClusterId => clusterId;
    public string AssetFamily => assetFamily;
    public string Rationale => rationale;
    public GmCompositionRole Role => role;
    public GmSpatialRelation Relation => relation;
    public string RelationTargetId => relationTargetId;
    public float MaximumRelationDistance => maximumRelationDistance;
    public float DeclaredSurfaceY => declaredSurfaceY;
    public float GroundTolerance => groundTolerance;
    public bool BlocksRoutes => blocksRoutes;

    public void Configure(string id, string cluster, string family, string why, GmCompositionRole elementRole,
        GmSpatialRelation spatialRelation = GmSpatialRelation.Grounded, string targetId = "",
        float maxRelationDistance = 5f, float surfaceY = 0f, float groundingTolerance = 0.2f,
        bool blocksReservedRoutes = true)
    {
        elementId = id;
        clusterId = cluster;
        assetFamily = family;
        rationale = why;
        role = elementRole;
        relation = spatialRelation;
        relationTargetId = targetId;
        maximumRelationDistance = Mathf.Max(0.05f, maxRelationDistance);
        declaredSurfaceY = surfaceY;
        groundTolerance = Mathf.Max(0.01f, groundingTolerance);
        blocksRoutes = blocksReservedRoutes;
    }
}
