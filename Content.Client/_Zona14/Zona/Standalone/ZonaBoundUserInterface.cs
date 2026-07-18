using Content.Shared._Zona14.Zona;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Zona14.Zona.Standalone;

[UsedImplicitly]
public sealed class ZonaBoundUserInterface : BoundUserInterface
{
    private ZonaWindow? _window;

    public ZonaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ZonaWindow>();
        _window.OnAddAnnotation += annotation => SendMessage(new ZonaAddAnnotationMessage(annotation));
        _window.OnRemoveAnnotation += index => SendMessage(new ZonaRemoveAnnotationMessage(index));
        _window.OnClearAnnotations += () => SendMessage(new ZonaClearAnnotationsMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ZonaBoundUserInterfaceState mapState)
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
