// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using Content.Shared._Zona14.WorldMap;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Zona14.WorldMap;

/// <summary>
/// Owns annotations for physical map entities. Anyone opening the same item sees the same annotations.
/// </summary>
public sealed class WorldMapSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    private const int MaxSharedAnnotations = 128;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorldMapComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<WorldMapComponent, WorldMapAddAnnotationMessage>(OnAddAnnotationMessage);
        SubscribeLocalEvent<WorldMapComponent, WorldMapRemoveAnnotationMessage>(OnRemoveAnnotationMessage);
        SubscribeLocalEvent<WorldMapComponent, WorldMapClearAnnotationsMessage>(OnClearAnnotationsMessage);
    }

    private void OnAfterOpen(EntityUid uid, WorldMapComponent component, AfterActivatableUIOpenEvent args)
    {
        _uiSystem.SetUiState(uid, WorldMapUiKey.Key, BuildState(component));
    }

    private void OnAddAnnotationMessage(EntityUid uid, WorldMapComponent component, WorldMapAddAnnotationMessage args)
    {
        var sanitized = WorldMapAnnotationSanitizer.Sanitize(args.Annotation);
        if (sanitized == null)
            return;

        component.SharedAnnotations.Add(sanitized.Value);
        if (component.SharedAnnotations.Count > MaxSharedAnnotations)
            component.SharedAnnotations.RemoveAt(0);

        UpdateMapUi(uid, component);
    }

    private void OnRemoveAnnotationMessage(EntityUid uid, WorldMapComponent component, WorldMapRemoveAnnotationMessage args)
    {
        if (args.Index < 0 || args.Index >= component.SharedAnnotations.Count)
            return;

        component.SharedAnnotations.RemoveAt(args.Index);
        UpdateMapUi(uid, component);
    }

    private void OnClearAnnotationsMessage(EntityUid uid, WorldMapComponent component, WorldMapClearAnnotationsMessage args)
    {
        if (component.SharedAnnotations.Count == 0)
            return;

        component.SharedAnnotations.Clear();
        UpdateMapUi(uid, component);
    }

    private static WorldMapBoundUserInterfaceState BuildState(WorldMapComponent component)
    {
        return new WorldMapBoundUserInterfaceState(
            component.MapTitle,
            component.MapTexturePath.ToString(),
            component.CompactHud,
            component.WorldBounds.Left,
            component.WorldBounds.Bottom,
            component.WorldBounds.Right,
            component.WorldBounds.Top,
            Array.Empty<MapTrackedBlip>(),
            component.SharedAnnotations.ToArray());
    }

    private void UpdateMapUi(EntityUid uid, WorldMapComponent component)
    {
        if (!TryComp<UserInterfaceComponent>(uid, out var ui))
            return;

        _uiSystem.SetUiState((uid, ui), WorldMapUiKey.Key, BuildState(component));
    }
}
