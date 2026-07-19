// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Content.Client._Zona14.WorldMap.Pda;
using Content.Client.UserInterface.Fragments;
using Content.Shared._Zona14.WorldMap;
using Content.Shared._Zona14.WorldMap.Cartridge;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Zona14.WorldMap.Cartridge;

/// <summary>
/// PDA-hosted entry point for the Zone map program. Forwards annotation edits to the
/// server-owned cartridge state so every reader shares the same annotations.
/// </summary>
public sealed partial class WorldMapCartridgeUi : UIFragment
{
    private WorldMapPdaUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new WorldMapPdaUiFragment();
        _fragment.OnAddAnnotation += annotation =>
            Send(userInterface, new WorldMapCartridgeMessageEvent(WorldMapCartridgeAction.Add, annotation, 0));
        _fragment.OnRemoveAnnotation += index =>
            Send(userInterface, new WorldMapCartridgeMessageEvent(WorldMapCartridgeAction.Remove, default, index));
        _fragment.OnClearAnnotations += () =>
            Send(userInterface, new WorldMapCartridgeMessageEvent(WorldMapCartridgeAction.Clear, default, 0));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not WorldMapCartridgeUiState mapState || _fragment == null)
            return;

        var bounds = new Box2(mapState.BoundsLeft, mapState.BoundsBottom, mapState.BoundsRight, mapState.BoundsTop);
        _fragment.SetMap(mapState.MapTitle, new ResPath(mapState.MapTexturePath), bounds);
        _fragment.UpdateAnnotations(mapState.Annotations);
    }

    private static void Send(BoundUserInterface userInterface, WorldMapCartridgeMessageEvent message)
    {
        userInterface.SendMessage(new CartridgeUiMessage(message));
    }
}
