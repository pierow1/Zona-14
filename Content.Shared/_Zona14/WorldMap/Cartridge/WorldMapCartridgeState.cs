// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Zona14.WorldMap.Cartridge;

[Serializable, NetSerializable]
public enum WorldMapCartridgeAction : byte
{
    Add,
    Remove,
    Clear,
}

/// <summary>
/// State pushed to the PDA map program: the backdrop, its world bounds and the shared annotations.
/// Uses <see cref="WorldMapAnnotation"/> directly — no text codec, unlike the earlier Notekeeper hack.
/// </summary>
[Serializable, NetSerializable]
public sealed class WorldMapCartridgeUiState : BoundUserInterfaceState
{
    public readonly string MapTitle;
    public readonly string MapTexturePath;
    public readonly float BoundsLeft;
    public readonly float BoundsBottom;
    public readonly float BoundsRight;
    public readonly float BoundsTop;
    public readonly WorldMapAnnotation[] Annotations;

    public WorldMapCartridgeUiState(
        string mapTitle,
        string mapTexturePath,
        float boundsLeft,
        float boundsBottom,
        float boundsRight,
        float boundsTop,
        WorldMapAnnotation[] annotations)
    {
        MapTitle = mapTitle;
        MapTexturePath = mapTexturePath;
        BoundsLeft = boundsLeft;
        BoundsBottom = boundsBottom;
        BoundsRight = boundsRight;
        BoundsTop = boundsTop;
        Annotations = annotations;
    }
}

[Serializable, NetSerializable]
public sealed class WorldMapCartridgeMessageEvent : CartridgeMessageEvent
{
    public readonly WorldMapCartridgeAction Action;
    public readonly WorldMapAnnotation Annotation;
    public readonly int Index;

    public WorldMapCartridgeMessageEvent(WorldMapCartridgeAction action, WorldMapAnnotation annotation, int index)
    {
        Action = action;
        Annotation = annotation;
        Index = index;
    }
}
