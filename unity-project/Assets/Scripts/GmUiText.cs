// Unity 6 UI Toolkit text, in a player build, without 9,906 exceptions in twenty seconds.
//
// Every HUD in this project creates its PanelSettings at runtime with CreateInstance, because none
// of them belong to a scene that ships one. Runtime-created PanelSettings do not carry Unity 6's
// optional ICU payload, and the ADVANCED text generator needs it: without it UITKTextHandle.ShapeText
// throws inside a job, once per text element per frame, and draws nothing. The build does not crash
// and nothing on screen explains it -- the text is simply absent.
//
// GmPrologueHud and GmHouseHud each found this independently and each fixed it on their own labels.
// The boot menu I wrote for Phase A1 did not, and reintroduced it: measured on the real macOS build,
// 9,906 NullReferenceExceptions in a twenty-second run of the title screen. GmCreditsUI has it too.
//
// A fix that lives in a comment in one file is not a fix for the next file. This applies it to a
// whole subtree at once, so it also covers Buttons and any TextElement added later, rather than
// depending on every future label-construction site remembering.
using UnityEngine;
using UnityEngine.UIElements;

public static class GmUiText
{
    /// Forces the standard text generator across a subtree.
    ///
    /// The standard generator is fully adequate for this game's Latin-script UI and is deterministic
    /// in player builds, which is the trade the two HUDs that hit this already chose. Call it after
    /// the tree is built, and again after rebuilding any part of it.
    public static void UseStandardGenerator(VisualElement root)
    {
        if (root == null) return;
        foreach (TextElement element in root.Query<TextElement>().Build())
            element.style.unityTextGenerator = TextGeneratorType.Standard;
    }
}
