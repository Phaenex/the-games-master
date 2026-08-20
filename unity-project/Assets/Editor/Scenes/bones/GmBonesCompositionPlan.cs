using UnityEngine;

public static class GmBonesCompositionPlan
{
    public static void Author(GameObject owner, GameObject table, GameObject dice,
        GameObject carpet, GameObject playerChair, GameObject aldricChair,
        GameObject taskFixture, GameObject taskLight)
    {
        GmCompositionAuthoring.Begin(owner, GmBonesBuilder.SceneId,
            "An intimate Victorian dice table whose stable working light separates honest faces from Aldric's altered six.",
            2, 2, 6, requireEveryShot: false, requireMotivatedLights: false);
        var tableZone = Child("BonesTableZone", owner.transform, new Vector3(0f, 1f, 0f));
        GmCompositionAuthoring.Zone(tableZone, "bones-table-zone", "Readable central play surface",
            new Vector3(4f, 3f, 4f), 1, 3);
        var tableCluster = Child("BonesTableCluster", tableZone.transform, Vector3.zero);
        GmCompositionAuthoring.Cluster(tableCluster, "bones-table-cluster", "bones-table-zone",
            "The table, three dice and unwavering task source", "bones-table", 1, 0,
            requireVariation: true);
        GmCompositionAuthoring.Element(table, "bones-table", "bones-table-cluster", "victorian-furniture",
            "Imported Table_2 anchors the match.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: 0f, groundTolerance: 0.2f);
        GmCompositionAuthoring.Element(dice, "bones-dice", "bones-table-cluster", "authored-dice",
            "Exactly three independently readable physical dice.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: 0.86f, groundTolerance: 0.08f);
        GmCompositionAuthoring.Element(taskFixture, "task-fixture", "bones-table-cluster", "period-fixture",
            "A visible imported fixture motivates the stable task light.", GmCompositionRole.Support,
            GmSpatialRelation.Grounded, surfaceY: 0.86f, groundTolerance: 0.08f);

        var seatingZone = Child("BonesSeatingZone", owner.transform, new Vector3(0f, 1f, 0f));
        GmCompositionAuthoring.Zone(seatingZone, "bones-seating-zone",
            "Opposed player and empty host seats", new Vector3(7f, 3f, 7f), 1, 3);
        var seatingCluster = Child("BonesSeatingCluster", seatingZone.transform, Vector3.zero);
        GmCompositionAuthoring.Cluster(seatingCluster, "bones-seating-cluster", "bones-seating-zone",
            "The occupied viewpoint faces Aldric's honestly empty chair.", "bones-carpet", 1, 1,
            requireVariation: true);
        GmCompositionAuthoring.Element(carpet, "bones-carpet", "bones-seating-cluster", "victorian-textile",
            "Imported carpet grounds the compact room.", GmCompositionRole.Anchor);
        GmCompositionAuthoring.Element(playerChair, "player-chair", "bones-seating-cluster", "victorian-chair",
            "The pulled-aside near chair preserves the approach.", GmCompositionRole.Support);
        GmCompositionAuthoring.Element(aldricChair, "aldric-chair", "bones-seating-cluster", "empty-victorian-chair",
            "The far chair remains physically empty until a character asset exists.", GmCompositionRole.Detail);
        GmCompositionAuthoring.Route(owner, "bones-player-approach",
            "Clear approach from player spawn to readable table edge",
            new[] { new Vector3(0f, 0f, -3.2f), new Vector3(0f, 0f, -1.5f) }, 0.7f);
    }

    static GameObject Child(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        return go;
    }
}
