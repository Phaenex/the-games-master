// The composition contract for a scene that has no composition.
//
// Every other scene in this project declares zones, clusters and grounded elements because it is a
// ROOM -- the contract exists to catch a mirror floating 1.06m off the floor or a gate buried 1.9m
// under it. The boot screen is UI drawn in screen space over an empty stage. It has no geometry to
// ground, no light to motivate and no spatial hierarchy to argue about.
//
// So this declares that, explicitly, with zero minimums, rather than inventing a "title zone" and a
// "menu cluster" to satisfy a shape that does not apply. A contract padded to look uniform teaches
// the reader that the contract is decorative, which is exactly how a real room's floating furniture
// gets waved through.
//
// If the boot screen ever gains real geometry -- a lit object behind the type, a rendered room --
// this becomes a normal plan with normal minimums and the elements get declared like anywhere else.
using UnityEngine;

public static class GmBootCompositionPlan
{
    public static void Author(GameObject owner)
    {
        GmCompositionAuthoring.Begin(owner, "boot",
            "A title plate and three rows on an empty stage. The scene's real job is structural, not "
            + "visual: it instantiates the scene director, so transitions and endings become reachable.",
            minZones: 0, minClusters: 0, minElements: 0);
    }
}
