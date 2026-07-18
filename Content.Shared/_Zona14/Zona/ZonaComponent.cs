using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Zona14.Zona;

[Serializable, NetSerializable]
public enum ZonaUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ZonaTrackedBlipKind : byte
{
    Unknown = 0,

    // Zona14: Fallout faction/rank blips are not used in Zona14
    // values are retained in comments for network compatibility reference
    // Elder = 1,
    // Paladin = 2,
    // Knight = 3,
    // Scribe = 4,
    // Squire = 5,
    // LegionCenturion = 6,
    // LegionDecanus = 7,
    // LegionWarrior = 8,
    // LegionRecruit = 9,
    // PipBoyContact = 10,
    // PipBoyGroupMember = 11,
    // TribalHuntTarget = 12,

    // keep the original numeric value so existing serialized/network data is not reinterpreted
    DeadBody = 13,
}

[Serializable, NetSerializable]
public enum ZonaAnnotationType : byte
{
    Marker,
    Box,
    Draw,
}

// Zona14; Fallout tactical feeds are not used in Zona14
// [Serializable, NetSerializable]
// public enum ZonaTacticalFeedKind : byte
// {
//     None,
//     Brotherhood,
//     Vault,
//     NCR,
//     Enclave,
//     Legion,
//     Followers,
// }

[Serializable, NetSerializable]
public readonly record struct ZonaTrackedBlip(
    float X,
    float Y,
    string Label,
    ZonaTrackedBlipKind Kind);

[Serializable, NetSerializable]
public readonly record struct ZonaAnnotation(
    ZonaAnnotationType Type,
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    string Label,
    uint PackedColor,
    float StrokeWidth,
    float[]? StrokePoints)
{
    public const uint DefaultPackedColor = 0xF27F26FF;
    public const float DefaultStrokeWidth = 3f;
}

[Serializable, NetSerializable]
public sealed class ZonaBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string MapTitle;
    public readonly string MapTexturePath;
    public readonly bool CompactHud;
    public readonly float BoundsLeft;
    public readonly float BoundsBottom;
    public readonly float BoundsRight;
    public readonly float BoundsTop;
    public readonly ZonaTrackedBlip[] TrackedBlips;
    public readonly ZonaAnnotation[] SharedAnnotations;

    public ZonaBoundUserInterfaceState(
        string mapTitle,
        string mapTexturePath,
        bool compactHud,
        float boundsLeft,
        float boundsBottom,
        float boundsRight,
        float boundsTop,
        ZonaTrackedBlip[]? trackedBlips = null,
        ZonaAnnotation[]? sharedAnnotations = null)
    {
        MapTitle = mapTitle;
        MapTexturePath = mapTexturePath;
        CompactHud = compactHud;
        BoundsLeft = boundsLeft;
        BoundsBottom = boundsBottom;
        BoundsRight = boundsRight;
        BoundsTop = boundsTop;
        TrackedBlips = trackedBlips ?? Array.Empty<ZonaTrackedBlip>();
        SharedAnnotations = sharedAnnotations ?? Array.Empty<ZonaAnnotation>();
    }
}

[Serializable, NetSerializable]
public sealed class ZonaAddAnnotationMessage : BoundUserInterfaceMessage
{
    public readonly ZonaAnnotation Annotation;

    public ZonaAddAnnotationMessage(ZonaAnnotation annotation)
    {
        Annotation = annotation;
    }
}

[Serializable, NetSerializable]
public sealed class ZonaClearAnnotationsMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ZonaRemoveAnnotationMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public ZonaRemoveAnnotationMessage(int index)
    {
        Index = index;
    }
}

/// <summary>
/// server-side state for a physical paper map. Its annotations travel with that map entity.
/// </summary>
[RegisterComponent]
public sealed partial class ZonaComponent : Component
{
    [DataField(required: true)]
    public ResPath MapTexturePath = default!;

    [DataField]
    public string MapTitle = "Map";

    [DataField(required: true)]
    public Box2 WorldBounds;

    // Zona14; Fallout faction tracking is not used in Zona14
    // [DataField]
    // public bool TrackBrotherhoodHolotags;

    // [DataField]
    // public ZonaTacticalFeedKind TacticalFeed;

    [DataField]
    public bool CompactHud;

    [DataField]
    public List<ZonaAnnotation> SharedAnnotations = new();
}