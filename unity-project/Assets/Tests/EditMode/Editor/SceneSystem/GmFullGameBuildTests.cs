// The assertion that was missing when Phase A1 and A2 were reported done.
//
// Both phases were real, both were fully tested, and neither was in any build. A1 shipped a boot
// scene whose only job is to construct the GmSceneDirector; A2 put a player in six rooms. The
// macOS app contained one scene, so the boot scene never loaded, the director never existed, and
// ResolveEnding() -- the thing A1 was built to make reachable -- was still never called outside the
// editor.
//
// Nothing was red. "Tests pass" and "a person can reach it" are different claims, and until this
// file existed the project could only check the first one.
using System.Collections.Generic;
using NUnit.Framework;

public sealed class GmFullGameBuildTests
{
    [Test]
    public void TheStartupSceneIsBoot()
    {
        // In a built player, index 0 IS the startup scene; there is no separate setting. Boot sat at
        // index 8 of the editor's build settings, which looks like "it is in the build" and is not
        // the same thing at all.
        Assert.AreEqual(GmFullGameBuild.BootScenePath, GmFullGameBuild.ScenePaths()[0],
            "the game must start at the boot scene, because only GmBootMenu constructs the scene " +
            "director and nothing else in the project ever will");
    }

    [Test]
    public void EverySceneTheDirectorCanSendThePlayerToIsInTheBuild()
    {
        // GmSceneDirector.TransitionRoutine handles a scene missing from Build Settings by refusing
        // to move, which is the right runtime behaviour and a dead end for the player. The build is
        // where that has to be caught.
        List<string> manifest = GmFullGameBuild.ScenePaths();
        var missing = new List<string>();
        foreach (string path in GmSceneDirector.ReachableScenePaths)
            if (!manifest.Contains(path)) missing.Add(path);

        Assert.IsEmpty(missing, "the director can send the player to scenes that are not in the " +
            "build, so those transitions dead-end at runtime:\n- " + string.Join("\n- ", missing));
    }

    [Test]
    public void TheManifestHasNoDuplicates()
    {
        // BuildPipeline treats a repeated scene as an error, and the derivation in ScenePaths()
        // deliberately de-duplicates because Boot could one day also appear in the director's list.
        List<string> manifest = GmFullGameBuild.ScenePaths();
        CollectionAssert.AllItemsAreUnique(manifest,
            "a duplicate scene in the manifest fails the build: " + string.Join(", ", manifest));
    }

    [Test]
    public void EveryManifestSceneExistsOnDisk()
    {
        var missing = new List<string>();
        foreach (string path in GmFullGameBuild.ScenePaths())
            if (!System.IO.File.Exists(path)) missing.Add(path);

        Assert.IsEmpty(missing, "the game manifest names scenes that do not exist — rebuild them:\n- " +
            string.Join("\n- ", missing));
    }

    [Test]
    public void TheManifestCoversEveryRoomThatWasGivenAPlayer()
    {
        // Ties this file to the one next door. GmPlayerRigTests proves six rooms contain a player;
        // this proves those same six rooms are in the thing a player runs. Either check alone still
        // permits "built, tested, unreachable" -- which is exactly the state A2 shipped in.
        List<string> manifest = GmFullGameBuild.ScenePaths();
        string[] playableScenes =
        {
            "Assets/Scenes/EntryHall.unity",
            "Assets/Scenes/Parlor.unity",
            "Assets/Scenes/Court.unity",
            "Assets/Scenes/ShutTheBox.unity",
            "Assets/Scenes/HiddenRoom.unity",
            "Assets/Scenes/Labyrinth.unity",
        };

        var unreachable = new List<string>();
        foreach (string path in playableScenes)
            if (!manifest.Contains(path)) unreachable.Add(path);

        Assert.IsEmpty(unreachable, "these rooms have a player rig and no way to be loaded:\n- " +
            string.Join("\n- ", unreachable));
    }
}
