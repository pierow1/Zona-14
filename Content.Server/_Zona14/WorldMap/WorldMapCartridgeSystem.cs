// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Content.Server.CartridgeLoader;
using Content.Shared._Zona14.WorldMap;
using Content.Shared._Zona14.WorldMap.Cartridge;
using Content.Shared.CartridgeLoader;

namespace Content.Server._Zona14.WorldMap;

/// <summary>
/// PDA map program. Stores annotations on the cartridge and relays them to every reader,
/// mirroring the built-in Notekeeper cartridge but with structured annotation data.
/// </summary>
public sealed class WorldMapCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;

    private const int MaxSharedAnnotations = 128;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorldMapCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<WorldMapCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    private void OnUiReady(EntityUid uid, WorldMapCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, WorldMapCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not WorldMapCartridgeMessageEvent message)
            return;

        switch (message.Action)
        {
            case WorldMapCartridgeAction.Add:
                var sanitized = WorldMapAnnotationSanitizer.Sanitize(message.Annotation);
                if (sanitized == null)
                    return;

                component.Annotations.Add(sanitized.Value);
                if (component.Annotations.Count > MaxSharedAnnotations)
                    component.Annotations.RemoveAt(0);
                break;

            case WorldMapCartridgeAction.Remove:
                if (message.Index < 0 || message.Index >= component.Annotations.Count)
                    return;

                component.Annotations.RemoveAt(message.Index);
                break;

            case WorldMapCartridgeAction.Clear:
                if (component.Annotations.Count == 0)
                    return;

                component.Annotations.Clear();
                break;

            default:
                return;
        }

        UpdateUiState(uid, GetEntity(args.LoaderUid), component);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, WorldMapCartridgeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new WorldMapCartridgeUiState(
            component.MapTitle,
            component.MapTexturePath.ToString(),
            component.WorldBounds.Left,
            component.WorldBounds.Bottom,
            component.WorldBounds.Right,
            component.WorldBounds.Top,
            component.Annotations.ToArray());

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }
}
