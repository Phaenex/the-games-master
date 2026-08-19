using UnityEngine;
using UnityEngine.SceneManagement;

public static class GmEntryHallCanonBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CleanStartupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name == "EntryHall") RemoveRetiredShardProxy(scene);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode _) => RemoveRetiredShardProxy(scene);

    public static bool RemoveRetiredShardProxy(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "EntryHall") return false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "MirrorShard_2") continue;
                child.gameObject.SetActive(false);
                return true;
            }
        }
        return false;
    }
}
