using UnityEditor;

public static class GmWendReview
{
    [MenuItem("GamesMaster/Wend/Review canonical opening")]
    public static void Run() =>
        GmSceneReviewTourMenu.ArmAndPlay<GmWendStoryTour>(GmWendBuilder.ScenePath);
}
