// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Zona14.WorldMap;

[Serializable, NetSerializable]
public enum WorldMapUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum MapTrackedBlipKind : byte
{
    Unknown = 0,

    // Fallout faction/rank blips are not used in Zona14; values are retained in
    // comments for network-compatibility reference.
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

    // Keep the original numeric value so existing serialized/network data is not reinterpreted.
    DeadBody = 13,
}

[Serializable, NetSerializable]
public enum WorldMapAnnotationType : byte
{
    Marker,
    Box,
    Draw,
}

// Fallout tactical feeds are not used in Zona14.
// [Serializable, NetSerializable]
// public enum MapTacticalFeedKind : byte
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
public readonly record struct MapTrackedBlip(
    float X,
    float Y,
    string Label,
    MapTrackedBlipKind Kind);

[Serializable, NetSerializable]
public readonly record struct WorldMapAnnotation(
    WorldMapAnnotationType Type,
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
public sealed class WorldMapBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string MapTitle;
    public readonly string MapTexturePath;
    public readonly bool CompactHud;
    public readonly float BoundsLeft;
    public readonly float BoundsBottom;
    public readonly float BoundsRight;
    public readonly float BoundsTop;
    public readonly MapTrackedBlip[] TrackedBlips;
    public readonly WorldMapAnnotation[] SharedAnnotations;

    public WorldMapBoundUserInterfaceState(
        string mapTitle,
        string mapTexturePath,
        bool compactHud,
        float boundsLeft,
        float boundsBottom,
        float boundsRight,
        float boundsTop,
        MapTrackedBlip[]? trackedBlips = null,
        WorldMapAnnotation[]? sharedAnnotations = null)
    {
        MapTitle = mapTitle;
        MapTexturePath = mapTexturePath;
        CompactHud = compactHud;
        BoundsLeft = boundsLeft;
        BoundsBottom = boundsBottom;
        BoundsRight = boundsRight;
        BoundsTop = boundsTop;
        TrackedBlips = trackedBlips ?? Array.Empty<MapTrackedBlip>();
        SharedAnnotations = sharedAnnotations ?? Array.Empty<WorldMapAnnotation>();
    }
}

[Serializable, NetSerializable]
public sealed class WorldMapAddAnnotationMessage : BoundUserInterfaceMessage
{
    public readonly WorldMapAnnotation Annotation;

    public WorldMapAddAnnotationMessage(WorldMapAnnotation annotation)
    {
        Annotation = annotation;
    }
}

[Serializable, NetSerializable]
public sealed class WorldMapClearAnnotationsMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class WorldMapRemoveAnnotationMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public WorldMapRemoveAnnotationMessage(int index)
    {
        Index = index;
    }
}

/// <summary>
/// Server-side state for a physical paper map. Its annotations travel with that map entity.
/// </summary>
[RegisterComponent]
public sealed partial class WorldMapComponent : Component
{
    [DataField(required: true)]
    public ResPath MapTexturePath = default!;

    [DataField]
    public string MapTitle = "Map";

    [DataField(required: true)]
    public Box2 WorldBounds;

    [DataField]
    public bool CompactHud;

    [DataField]
    public List<WorldMapAnnotation> SharedAnnotations = new();
}
