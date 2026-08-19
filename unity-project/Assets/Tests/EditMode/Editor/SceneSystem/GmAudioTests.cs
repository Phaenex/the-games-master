using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GmAudioTests
{
    [Test]
    public void TableGameAndCourtCueClipsAreShipped()
    {
        string[] resourceNames =
        {
            "parlor_card_snap",
            "stb_bone_dice_roll",
            "court_gavel_strike",
        };

        foreach (string resourceName in resourceNames)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Sfx/{resourceName}");
            Assert.IsNotNull(clip, $"Resources/Sfx/{resourceName} is missing, so the authored cue is silent");
            Assert.Greater(clip.length, 0.1f, $"{resourceName} imported as an empty or unusably short clip");
            Assert.AreEqual(1, clip.channels,
                $"{resourceName} should stay mono so the game can spatialize it");
        }
    }

    // Named for the backing setters, not for a slider: nothing in the runtime binds a UI slider to
    // master/ambience/sfx volume yet, so this only proves the clamp the setters apply.
    [Test]
    public void VolumeSettersClampToUnitRange()
    {
        var audioObj = new GameObject("TestAudioManager");
        var audioMgr = audioObj.AddComponent<GmAudioManager>();

        audioMgr.SetMasterVolume(0.75f);
        Assert.AreEqual(0.75f, audioMgr.MasterVolume, 0.001f);

        audioMgr.SetMasterVolume(1.5f);
        Assert.AreEqual(1.0f, audioMgr.MasterVolume, 0.001f);

        audioMgr.SetMasterVolume(-0.5f);
        Assert.AreEqual(0.0f, audioMgr.MasterVolume, 0.001f);

        audioMgr.SetAmbienceVolume(2.0f);
        Assert.AreEqual(1.0f, audioMgr.AmbienceVolume, 0.001f);
        audioMgr.SetAmbienceVolume(-1.0f);
        Assert.AreEqual(0.0f, audioMgr.AmbienceVolume, 0.001f);

        audioMgr.SetSfxVolume(2.0f);
        Assert.AreEqual(1.0f, audioMgr.SfxVolume, 0.001f);
        audioMgr.SetSfxVolume(-1.0f);
        Assert.AreEqual(0.0f, audioMgr.SfxVolume, 0.001f);

        Object.DestroyImmediate(audioObj);
    }

    [Test]
    public void SfxPlayIncrementsCountAndInvokesEvent()
    {
        var audioObj = new GameObject("TestAudioManager");
        var audioMgr = audioObj.AddComponent<GmAudioManager>();

        string lastPlayed = "";
        audioMgr.OnSoundPlayed += sound => lastPlayed = sound;

        audioMgr.PlayBellToll();
        Assert.AreEqual("ninth-bell-toll", lastPlayed);
        Assert.AreEqual(1, audioMgr.TotalSfxPlayed);

        audioMgr.PlayCardSnap();
        Assert.AreEqual("parlor-card-snap", lastPlayed);
        Assert.AreEqual(2, audioMgr.TotalSfxPlayed);

        Object.DestroyImmediate(audioObj);
    }

    [Test]
    public void ReadFilterTogglesLowPass()
    {
        // IsReadFilterActive is only a mirror of the request. What the player hears is the listener
        // low-pass, so read the filter the manager actually resolved: a regression that decouples the
        // flag from the filter leaves the bool alone and the Read silent.
        var listenerObj = new GameObject("TestAudioListener");
        listenerObj.AddComponent<AudioListener>();
        var audioObj = new GameObject("TestAudioManager");
        var audioMgr = audioObj.AddComponent<GmAudioManager>();
        FieldInfo filterField = typeof(GmAudioManager).GetField("lowPassFilter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(filterField, "GmAudioManager no longer owns a listener low-pass filter");
        AudioLowPassFilter[] before =
            Object.FindObjectsByType<AudioLowPassFilter>(FindObjectsSortMode.None);
        try
        {
            audioMgr.SetReadTimeDilationFilter(true);
            Assert.IsTrue(audioMgr.IsReadFilterActive);
            var filter = (AudioLowPassFilter)filterField.GetValue(audioMgr);
            Assert.IsNotNull(filter, "the Read resolved no listener filter, so nothing dilates the mix");
            Assert.IsNotNull(filter.GetComponent<AudioListener>(),
                "low-pass is not on the AudioListener and therefore cannot filter the game mix");
            Assert.AreEqual(450f, filter.cutoffFrequency, 0.5f);

            audioMgr.SetReadTimeDilationFilter(false);
            Assert.IsFalse(audioMgr.IsReadFilterActive);
            Assert.AreEqual(22000f, filter.cutoffFrequency, 0.5f,
                "the Read ended without handing hearing back open");
        }
        finally
        {
            // The manager attaches its filter to whichever listener it found, which may belong to an
            // open scene. Remove only a filter this test caused, and leave any authored one alone.
            var resolved = (AudioLowPassFilter)filterField.GetValue(audioMgr);
            if (resolved != null && System.Array.IndexOf(before, resolved) < 0)
                Object.DestroyImmediate(resolved);
            Object.DestroyImmediate(audioObj);
            Object.DestroyImmediate(listenerObj);
        }
    }

    [Test]
    public void CueHelpersPlayTheirAuthoredSlugsAndCountEveryShot()
    {
        var audioObj = new GameObject("TestAudioManager");
        var audioMgr = audioObj.AddComponent<GmAudioManager>();

        var played = new System.Collections.Generic.List<string>();
        audioMgr.OnSoundPlayed += sound => played.Add(sound);

        audioMgr.PlayHeartbeat();
        audioMgr.PlayDiceRoll();
        audioMgr.PlayGavelStrike();

        Assert.AreEqual(
            new[] { "sanity-heartbeat-thump", "stb-bone-dice-roll", "court-gavel-strike" },
            played.ToArray());
        Assert.AreEqual(3, audioMgr.TotalSfxPlayed);

        // Ambience is a bed, not a shot: it announces itself without inflating the SFX tally.
        audioMgr.PlayAmbience("wend-hill-wind");
        Assert.AreEqual("wend-hill-wind", audioMgr.CurrentAmbience);
        Assert.AreEqual("wend-hill-wind", played[played.Count - 1]);
        Assert.AreEqual(3, audioMgr.TotalSfxPlayed);

        Object.DestroyImmediate(audioObj);
    }
}
