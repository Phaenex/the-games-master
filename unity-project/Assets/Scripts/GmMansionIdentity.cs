// Durable identity for the one canonical mansion shell.
//
// PrefabUtility provenance disappears when an instance is completely unpacked. A component attached
// to the authored root survives duplication, renaming, reparenting, and prefab unpacking, which lets
// the estate audit reject the exact unpack-and-rename counterexample that defeated its prefab-only
// check. This is an identity marker, not gameplay behaviour.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmMansionIdentity : MonoBehaviour
{
    public const string CanonicalSource = "gravyart/haunted_victorian_house.fbx";

    [SerializeField]
    string source = CanonicalSource;

    public string Source => source;
    public bool IsCanonical => source == CanonicalSource;
}

