// Durable metadata for every generated gameplay scene. The build/audit/tour commands live outside
// Unity in the repository registry; this marker proves that the scene on disk is the scene those
// commands claim it is, even after the file is renamed or copied.
using UnityEngine;

public static class GmSceneCatalog
{
    public const int SchemaVersion = 1;
    public const string WendHillId = "wend-hill";
    public const string WendHillName = "Wend Hill";
    public const string WendHillPath = "Assets/Scenes/WendHill.unity";
}

[DisallowMultipleComponent]
public sealed class GmSceneIdentity : MonoBehaviour
{
    [SerializeField] string sceneId;
    [SerializeField] string displayName;
    [SerializeField] int schemaVersion = GmSceneCatalog.SchemaVersion;

    public string SceneId => sceneId;
    public string DisplayName => displayName;
    public int SchemaVersion => schemaVersion;

    public void Configure(string id, string name, int schema = GmSceneCatalog.SchemaVersion)
    {
        sceneId = id;
        displayName = name;
        schemaVersion = schema;
    }
}
