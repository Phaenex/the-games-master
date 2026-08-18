using UnityEngine;

public sealed class GmParlorCardView : MonoBehaviour
{
    [SerializeField] GmSuit physicalSuit;
    [SerializeField] int physicalRank;
    [SerializeField] Transform faceVisual;
    [SerializeField] Transform backVisual;

    public GmCard PhysicalCard => new GmCard(physicalSuit, physicalRank);
    public GmCard DisplayCard { get; private set; }
    public GmParlorCardBinding Binding { get; private set; }
    public bool IsFaceUp => Binding.Facing == GmParlorCardFacing.FaceUp;
    public bool IsFaceVisualVisible => faceVisual != null && faceVisual.gameObject.activeSelf;
    public bool IsBackVisualVisible => backVisual != null && backVisual.gameObject.activeSelf;

    public void Configure(GmCard physicalCard, Transform face = null, Transform back = null)
    {
        physicalSuit = physicalCard.Suit;
        physicalRank = physicalCard.Rank;
        faceVisual = face;
        backVisual = back;
        DisplayCard = physicalCard;
        Binding = new GmParlorCardBinding(physicalCard, physicalCard,
            GmParlorCardZone.HousePile, 0, GmParlorCardFacing.FaceDown);
        ApplyVisualFacing(false);
    }

    public void ApplyBinding(GmParlorCardBinding binding)
    {
        if (binding.PhysicalCard != PhysicalCard)
            throw new System.ArgumentException("Binding physical identity does not match this card view",
                nameof(binding));
        Binding = binding;
        DisplayCard = binding.DisplayCard;
        transform.localPosition = GmParlorTableLayout.LocalPosition(binding);
        transform.localRotation = GmParlorTableLayout.LocalRotation(binding);
        ApplyVisualFacing(binding.Facing == GmParlorCardFacing.FaceUp);
    }

    void ApplyVisualFacing(bool faceUp)
    {
        if (faceVisual != null) faceVisual.gameObject.SetActive(faceUp);
        if (backVisual != null) backVisual.gameObject.SetActive(!faceUp);
    }
}
