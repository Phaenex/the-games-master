using UnityEngine;

public static class GmStudyCompositionPlan
{
    public static void Author(GameObject owner, GameObject table, GameObject board,
        GameObject carpet, GameObject playerChair, GameObject aldricChair,
        GameObject taskFixture, GameObject taskLight)
    {
        GmCompositionAuthoring.Begin(owner, GmStudyBuilder.SceneId,
            "An intimate Victorian study whose stable working light separates the honest board from Aldric's arbiter override.",
            2, 2, 6, requireEveryShot: false, requireMotivatedLights: false);
        var tableZone = Child("StudyTableZone", owner.transform, new Vector3(0f, 1f, 0f));
        GmCompositionAuthoring.Zone(tableZone, "study-table-zone", "Readable central play surface",
            new Vector3(4f, 3f, 4f), 1, 3);
        var tableCluster = Child("StudyTableCluster", tableZone.transform, Vector3.zero);
        GmCompositionAuthoring.Cluster(tableCluster, "study-table-cluster", "study-table-zone",
            "The table, the board and its unwavering task source", "study-table", 1, 0,
            requireVariation: true);
        GmCompositionAuthoring.Element(table, "study-table", "study-table-cluster", "victorian-furniture",
            "Imported Table_2 anchors the match.", GmCompositionRole.Anchor,
            GmSpatialRelation.Grounded, surfaceY: 0f, groundTolerance: 0.2f);
        GmCompositionAuthoring.Element(board, "study-board", "study-table-cluster", "authored-board",
            "A 64-tile authored board with five independently readable physical pieces.", GmCompositionRole.Gameplay,
            GmSpatialRelation.Grounded, surfaceY: 0.86f, groundTolerance: 0.08f);
        GmCompositionAuthoring.Element(taskFixture, "task-fixture", "study-table-cluster", "period-fixture",
            "A visible imported fixture motivates the stable task light.", GmCompositionRole.Support,
            GmSpatialRelation.Grounded, surfaceY: 0.86f, groundTolerance: 0.08f);

        var seatingZone = Child("StudySeatingZone", owner.transform, new Vector3(0f, 1f, 0f));
        GmCompositionAuthoring.Zone(seatingZone, "study-seating-zone",
            "Opposed player and empty host seats", new Vector3(7f, 3f, 7f), 1, 3);
        var seatingCluster = Child("StudySeatingCluster", seatingZone.transform, Vector3.zero);
        GmCompositionAuthoring.Cluster(seatingCluster, "study-seating-cluster", "study-seating-zone",
            "The occupied viewpoint faces Aldric's honestly empty chair.", "study-carpet", 1, 1,
            requireVariation: true);
        GmCompositionAuthoring.Element(carpet, "study-carpet", "study-seating-cluster", "victorian-textile",
            "Imported carpet grounds the compact room.", GmCompositionRole.Anchor);
        GmCompositionAuthoring.Element(playerChair, "player-chair", "study-seating-cluster", "victorian-chair",
            "The pulled-aside near chair preserves the approach.", GmCompositionRole.Support);
        GmCompositionAuthoring.Element(aldricChair, "aldric-chair", "study-seating-cluster", "empty-victorian-chair",
            "The far chair remains physically empty until a character asset exists.", GmCompositionRole.Detail);
        GmCompositionAuthoring.Route(owner, "study-player-approach",
            "Clear approach from player spawn to readable board edge",
            new[] { new Vector3(0f, 0f, -3.2f), new Vector3(0f, 0f, -1.5f) }, 0.7f);
    }

    static GameObject Child(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        return go;
    }
}
