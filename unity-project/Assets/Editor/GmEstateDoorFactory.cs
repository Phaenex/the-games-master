using UnityEngine;

public enum GmEstateDoorFacing
{
    North,
    South,
    East,
    West
}

/// <summary>
/// Builds a period door in a wall opening: frame, swinging leaf, solid closed collider, interactable.
/// Leaf art is Door_1, fitted the same way the parlor leaves are: world AABB by facing, no uniform
/// scale against a 8cm axis that crushes the mesh into a postage stamp.
/// </summary>
public static class GmEstateDoorFactory
{
    public const string DoorModelPath = "Assets/ThirdParty/MetalManVictorianInteriors/Door_1.fbx";
    public const string FramePrefabPath = "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_DoorFrame.prefab";
    public const string LeafPrefabPath = "Assets/LeartesStudios/HauntedVillage/Art/Prefabs/SM_Door_01.prefab";

    public static GmEstateDoor Place(Transform parent, string objectName, Vector3 openingCenter,
        float width, float height, GmEstateDoorFacing intoDestination,
        string doorId, string doorName, bool locked, bool barred, bool secret = false,
        string requiredKey = "", string keyName = "", bool startOpen = false,
        float openAngle = 90f, Material wood = null)
    {
        Vector3 through = Through(intoDestination);
        Vector3 along = AlongWall(intoDestination);
        Vector3 hinge = openingCenter - along * (width * 0.5f);
        Quaternion closedRotation = Quaternion.LookRotation(through, Vector3.up);
        float floorY = openingCenter.y - height * 0.5f;

        var root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(openingCenter, closedRotation);

        if (wood != null)
        {
            GmOwnedPropFactory.PlacePrefab(
                FramePrefabPath, objectName + "Frame", root.transform,
                openingCenter, CardinalSize(intoDestination, width + 0.18f, height, 0.16f),
                closedRotation, ground: true, surfaceY: floorY,
                overrideMaterial: wood);
        }

        var leaf = new GameObject(objectName + "Leaf");
        leaf.transform.SetParent(root.transform, false);
        leaf.transform.position = hinge + through * 0.02f + Vector3.up * 0.02f;
        leaf.transform.rotation = closedRotation;

        Vector3 leafCenter = hinge + along * (width * 0.5f);
        if (wood != null)
        {
            Color tint = wood.HasProperty("_BaseColor") ? wood.GetColor("_BaseColor") : wood.color;
            GmVictorianInteriorKit.PlaceGrounded("Door_1", objectName + "LeafMesh", leaf.transform,
                leafCenter, CardinalSize(intoDestination, width - 0.06f, height - 0.08f, 0.16f),
                closedRotation, "door", surfaceY: floorY, tintOverride: tint);
        }

        var door = root.AddComponent<GmEstateDoor>();
        door.Configure(doorId, doorName, requiredKey, keyName, locked, barred, openAngle,
            leaf.transform, secret, startOpen);
        BoxCollider barrier = door.EnsureBarrier(new Vector3(width - 0.08f, height - 0.1f, 0.14f));
        barrier.transform.localPosition = new Vector3(width * 0.5f, height * 0.02f, 0f);
        return door;
    }

    public static Vector3 Through(GmEstateDoorFacing facing)
    {
        switch (facing)
        {
            case GmEstateDoorFacing.North: return Vector3.forward;
            case GmEstateDoorFacing.South: return Vector3.back;
            case GmEstateDoorFacing.East: return Vector3.right;
            case GmEstateDoorFacing.West: return Vector3.left;
            default: return Vector3.forward;
        }
    }

    public static Vector3 AlongWall(GmEstateDoorFacing facing)
    {
        switch (facing)
        {
            case GmEstateDoorFacing.North: return Vector3.right;
            case GmEstateDoorFacing.South: return Vector3.left;
            case GmEstateDoorFacing.East: return Vector3.back;
            case GmEstateDoorFacing.West: return Vector3.forward;
            default: return Vector3.right;
        }
    }

    static Vector3 CardinalSize(GmEstateDoorFacing facing, float along, float height, float through)
    {
        if (facing == GmEstateDoorFacing.East || facing == GmEstateDoorFacing.West)
            return new Vector3(through, height, along);
        return new Vector3(along, height, through);
    }
}
