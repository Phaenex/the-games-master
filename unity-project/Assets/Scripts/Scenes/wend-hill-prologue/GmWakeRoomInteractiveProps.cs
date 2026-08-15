using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Runtime interactive components for the Victorian wake room items:
/// Invitation letter, stopped pocket watch, matchbox, and collectible brass carbide inspection lamp.
/// </summary>
public sealed class GmWakeRoomInteractiveProps : MonoBehaviour
{
    [Header("Letter")]
    public GameObject invitationLetter;
    [TextArea(2, 4)]
    public string letterText = "You have answered the summons to Wend Hill. The estate awaits its final game.";

    [Header("Pocket Watch")]
    public GameObject pocketWatch;
    [TextArea(2, 4)]
    public string watchText = "The crystal face is cracked, the hands frozen at 8:59. Silence holds the balance.";

    [Header("Carbide Inspection Lamp")]
    public GameObject carbideLamp;
    public Light playerCarbideBeam;
    public bool hasCarbideLamp = false;
    public bool isLampOn = false;

    void Start()
    {
        if (playerCarbideBeam != null)
            playerCarbideBeam.enabled = false;
    }

    void Update()
    {
        // Toggle equipped carbide lamp with 'F' key once collected
        if (hasCarbideLamp && Input.GetKeyDown(KeyCode.F))
        {
            ToggleCarbideLamp();
        }
    }

    public void InspectLetter()
    {
        GmDesignRuntime runtime = FindAnyObjectByType<GmDesignRuntime>();
        if (runtime != null) runtime.ShowExamine("wake-letter", letterText);
        Debug.Log("[GmWakeRoomInteractive] Letter inspected: " + letterText);
    }

    public void InspectWatch()
    {
        GmDesignRuntime runtime = FindAnyObjectByType<GmDesignRuntime>();
        if (runtime != null) runtime.ShowExamine("wake-watch", "Pocket Watch: " + watchText);
        Debug.Log("[GmWakeRoomInteractive] Watch inspected: " + watchText);
    }

    public void CollectCarbideLamp()
    {
        hasCarbideLamp = true;
        isLampOn = true;
        if (carbideLamp != null) carbideLamp.SetActive(false);
        if (playerCarbideBeam != null) playerCarbideBeam.enabled = true;

        GmDesignRuntime runtime = FindAnyObjectByType<GmDesignRuntime>();
        if (runtime != null) runtime.ShowExamine("wake-carbide-lamp", "Acquired Antique Brass Carbide Inspection Lamp. Press [F] to toggle beam.");
        Debug.Log("[GmWakeRoomInteractive] Carbide inspection lamp collected and equipped.");
    }

    public void ToggleCarbideLamp()
    {
        if (!hasCarbideLamp || playerCarbideBeam == null) return;
        isLampOn = !isLampOn;
        playerCarbideBeam.enabled = isLampOn;
        Debug.Log("[GmWakeRoomInteractive] Carbide lamp toggled: " + (isLampOn ? "ON" : "OFF"));
    }
}
