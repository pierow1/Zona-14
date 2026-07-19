// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Content.Shared._Zona14.WorldMap;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Zona14.WorldMap.Standalone;

[UsedImplicitly]
public sealed class WorldMapBoundUserInterface : BoundUserInterface
{
    private WorldMapWindow? _window;

    public WorldMapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<WorldMapWindow>();
        _window.OnAddAnnotation += annotation => SendMessage(new WorldMapAddAnnotationMessage(annotation));
        _window.OnRemoveAnnotation += index => SendMessage(new WorldMapRemoveAnnotationMessage(index));
        _window.OnClearAnnotations += () => SendMessage(new WorldMapClearAnnotationsMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not WorldMapBoundUserInterfaceState mapState)
            return;

        var bounds = new Box2(
            mapState.BoundsLeft,
            mapState.BoundsBottom,
            mapState.BoundsRight,
            mapState.BoundsTop);

        _window?.SetMap(
            mapState.MapTitle,
            new ResPath(mapState.MapTexturePath),
            bounds,
            mapState.TrackedBlips,
            mapState.SharedAnnotations,
            mapState.CompactHud);
    }
}
