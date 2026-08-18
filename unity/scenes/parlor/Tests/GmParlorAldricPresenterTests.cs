using System;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GmParlorAldricPresenterTests
{
    GameObject root;
    GmParlorEvidenceLog log;
    GmParlorAldricPresenter presenter;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("AldricPresenterTest");
        log = root.AddComponent<GmParlorEvidenceLog>();
        presenter = root.AddComponent<GmParlorAldricPresenter>();
        var hand = new GameObject("RightHandCue");
        hand.transform.SetParent(root.transform, false);
        var glove = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glove.name = "GloveCue";
        glove.transform.SetParent(hand.transform, false);
        var contact = new GameObject("CardContactCue");
        contact.transform.SetParent(root.transform, false);
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.name = "SleeveCue";
        sleeve.transform.SetParent(root.transform, false);
        Assert.That(presenter.TryConfigure(hand.transform, sleeve.GetComponent<Renderer>(),
            contact.transform, log, out string error), Is.True, error);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [TestCase(false, GmTellObservation.Calm)]
    [TestCase(true, GmTellObservation.Calm)]
    [TestCase(false, GmTellObservation.Suspicious)]
    [TestCase(true, GmTellObservation.Suspicious)]
    public void HiddenTruthCannotChangeThePreChoicePresentation(bool hiddenCheat,
        GmTellObservation observation)
    {
        GmParlorPresentationCommand command = Command(31, observation);
        Assert.That(presenter.TryPresent(command, GmParlorAccessibilityProfile.Default,
            out GmParlorEvidenceCue first, out string error), Is.True, error);

        // The hidden value deliberately has nowhere to enter the presenter. Cross the truth table
        // anyway so a future API widening cannot quietly give true and planted tells different art.
        _ = hiddenCheat;
        Assert.That(first.Observation, Is.EqualTo(observation));
        Assert.That(first.CommandId, Is.EqualTo(command.Id));
        Assert.That(first.CommandId, Is.Not.Zero);
        Assert.That(first.MinimumReadableSeconds, Is.GreaterThanOrEqualTo(0.5f));
    }

    [Test]
    public void SuspiciousTellCombinesHandAndCardEvidenceWithoutNamingTheConclusion()
    {
        Assert.That(presenter.TryPresent(Command(41, GmTellObservation.Suspicious),
            GmParlorAccessibilityProfile.Default, out GmParlorEvidenceCue cue,
            out string error), Is.True, error);

        CollectionAssert.Contains(cue.Channels, GmParlorEvidenceChannel.HandMotion);
        CollectionAssert.Contains(cue.Channels, GmParlorEvidenceChannel.CardContact);
        CollectionAssert.Contains(cue.Channels, GmParlorEvidenceChannel.FocusLog);
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            "PositionalAudio", "a nonexistent emitter must not even be advertised as a channel");
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            "ControllerPulse", "a nonexistent haptic driver must not be advertised as a channel");
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            "ContrastPulse", "a shipping-disabled renderer must not be advertised as a channel");
        Assert.That(cue.Channels.Distinct().Count(), Is.GreaterThanOrEqualTo(2));
        Assert.That(log.Facts, Has.Count.EqualTo(2));
        Assert.That(log.Facts.Select(fact => fact.Fact), Is.EquivalentTo(new[]
        {
            GmParlorObservedFact.RightHandPausedAboveDeck,
            GmParlorObservedFact.CardContactBroke,
        }));
    }

    [TestCase(true, false, false, true)]
    [TestCase(false, true, false, true)]
    [TestCase(false, false, false, true)]
    [TestCase(true, true, false, true)]
    public void AccessibilityProfilesKeepTwoReadableChannels(bool captions,
        bool reducedMotion, bool vibration, bool monoAudio)
    {
        var profile = new GmParlorAccessibilityProfile(
            captions, reducedMotion, vibration, monoAudio);
        Assert.That(presenter.TryPresent(Command(51, GmTellObservation.Suspicious), profile,
            out GmParlorEvidenceCue cue, out string error), Is.True, error);

        Assert.That(cue.Channels.Distinct().Count(), Is.GreaterThanOrEqualTo(2));
        if (captions) CollectionAssert.Contains(cue.Channels, GmParlorEvidenceChannel.Caption);
        if (reducedMotion)
        {
            CollectionAssert.DoesNotContain(cue.Channels, GmParlorEvidenceChannel.HandMotion);
        }
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            "ControllerPulse");
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            "PositionalAudio");
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(GmParlorEvidenceChannel)),
            "ContrastPulse");
        CollectionAssert.Contains(cue.Channels, GmParlorEvidenceChannel.FocusLog);
        CollectionAssert.Contains(cue.Channels, GmParlorEvidenceChannel.CardContact);
    }

    [Test]
    public void PresenterSourceDoesNotWriteAHiddenRendererAndCallItContrastEvidence()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts/Scenes/parlor/GmParlorAldricPresenter.cs"));
        StringAssert.DoesNotContain("EmissiveColor", source);
    }

    [Test]
    public void PresenterPublicApiHasNoHiddenTruthOrMatchSnapshotInput()
    {
        MethodInfo method = typeof(GmParlorAldricPresenter).GetMethod(
            nameof(GmParlorAldricPresenter.TryPresent));
        Assert.That(method, Is.Not.Null);
        Type[] inputs = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        CollectionAssert.DoesNotContain(inputs, typeof(bool));
        CollectionAssert.DoesNotContain(inputs, typeof(GmParlorMatchSnapshot));
        Assert.That(typeof(GmParlorAldricPresenter).GetProperties(BindingFlags.Public |
            BindingFlags.Instance).Select(property => property.Name),
            Has.None.Contains("Cheat"));
    }

    [Test]
    public void NonJudgementCommandFailsClosedWithoutWritingEvidence()
    {
        GmParlorMatch match = StartedMatch(61);
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(match.ContinueJudgement(), Is.EqualTo(GmParlorActionError.None));
        GmParlorPresentationCommand invalid =
            GmParlorPresentationJournal.Build(before, match.ExportSnapshot()).Single();

        Assert.That(presenter.TryPresent(invalid, GmParlorAccessibilityProfile.Default,
            out _, out string error), Is.False);
        StringAssert.Contains("OpenJudgement", error);
        Assert.That(log.Facts, Is.Empty);
    }

    static GmParlorPresentationCommand Command(ulong id, GmTellObservation observation)
    {
        GmParlorMatch match = StartedMatch(unchecked((int)id));
        GmParlorMatchSnapshot before = match.ExportSnapshot();
        Assert.That(match.PlayPlayerCard(0), Is.EqualTo(GmParlorActionError.None));
        GmParlorMatchSnapshot after = match.ExportSnapshot();
        after.tellObservation = observation;
        return GmParlorPresentationJournal.Build(before, after)
            .Single(command => command.Action == GmParlorPresentationAction.OpenJudgement);
    }

    static GmParlorMatch StartedMatch(int seed)
    {
        var match = new GmParlorMatch(seed == 0 ? 1 : seed, 1, 0, false);
        Assert.That(match.Start(), Is.EqualTo(GmParlorActionError.None));
        return match;
    }
}
