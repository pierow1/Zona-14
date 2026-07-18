using Content.Shared._Zona14.Zona;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Zona14.Zona;

/// <summary>
/// owns annotations for physical map entities. anyone opening the same item receives the same
/// </summary>
public sealed class ZonaSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    private const int MaxSharedAnnotations = 128;
    private const int MaxStrokeCoordinates = 192;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZonaComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<ZonaComponent, ZonaAddAnnotationMessage>(OnAddAnnotationMessage);
        SubscribeLocalEvent<ZonaComponent, ZonaRemoveAnnotationMessage>(OnRemoveAnnotationMessage);
        SubscribeLocalEvent<ZonaComponent, ZonaClearAnnotationsMessage>(OnClearAnnotationsMessage);
    }

    private void OnAfterOpen(EntityUid uid, ZonaComponent component, AfterActivatableUIOpenEvent args)
    {
        _uiSystem.SetUiState(uid, ZonaUiKey.Key, BuildState(component));
    }

    private void OnAddAnnotationMessage(
        EntityUid uid,
        ZonaComponent component,
        ZonaAddAnnotationMessage args)
    {
        var sanitized = SanitizeAnnotation(args.Annotation);
        if (sanitized == null)
            return;

        component.SharedAnnotations.Add(sanitized.Value);
        if (component.SharedAnnotations.Count > MaxSharedAnnotations)
            component.SharedAnnotations.RemoveAt(0);

        UpdateMapUi(uid, component);
    }

    private void OnRemoveAnnotationMessage(
        EntityUid uid,
        ZonaComponent component,
        ZonaRemoveAnnotationMessage args)
    {
        if (args.Index < 0 || args.Index >= component.SharedAnnotations.Count)
            return;

        component.SharedAnnotations.RemoveAt(args.Index);
        UpdateMapUi(uid, component);
    }

    private void OnClearAnnotationsMessage(
        EntityUid uid,
        ZonaComponent component,
        ZonaClearAnnotationsMessage args)
    {
        if (component.SharedAnnotations.Count == 0)
            return;

        component.SharedAnnotations.Clear();
        UpdateMapUi(uid, component);
    }

    private static ZonaBoundUserInterfaceState BuildState(ZonaComponent component)
    {
        return new ZonaBoundUserInterfaceState(
            component.MapTitle,
            component.MapTexturePath.ToString(),
            component.CompactHud,
            component.WorldBounds.Left,
            component.WorldBounds.Bottom,
            component.WorldBounds.Right,
            component.WorldBounds.Top,
            Array.Empty<ZonaTrackedBlip>(),
            component.SharedAnnotations.ToArray());
    }

    private void UpdateMapUi(EntityUid uid, ZonaComponent component)
    {
        if (!TryComp<UserInterfaceComponent>(uid, out var ui))
            return;

        _uiSystem.SetUiState((uid, ui), ZonaUiKey.Key, BuildState(component));
    }

    private static ZonaAnnotation? SanitizeAnnotation(ZonaAnnotation annotation)
    {
        if (annotation.Type is not ZonaAnnotationType.Marker and
            not ZonaAnnotationType.Box and
            not ZonaAnnotationType.Draw)
            return null;

        if (!float.IsFinite(annotation.StrokeWidth))
            return null;

        var label = annotation.Label.Trim();
        if (label.Length > 64)
            label = label[..64].TrimEnd();

        if (annotation.Type == ZonaAnnotationType.Draw)
        {
            var points = annotation.StrokePoints;
            if (points == null || points.Length < 4)
                return null;

            var count = Math.Min(points.Length & ~1, MaxStrokeCoordinates);
            var sanitizedPoints = new float[count];

            for (var i = 0; i < count; i++)
            {
                if (!float.IsFinite(points[i]))
                    return null;

                sanitizedPoints[i] = Math.Clamp(points[i], 0f, 1f);
            }

            if (string.IsNullOrWhiteSpace(label))
                label = "Drawing";

            return new ZonaAnnotation(
                ZonaAnnotationType.Draw,
                0f,
                0f,
                0f,
                0f,
                label,
                annotation.PackedColor,
                Math.Clamp(annotation.StrokeWidth, 1f, 12f),
                sanitizedPoints);
        }

        if (!float.IsFinite(annotation.StartX) ||
            !float.IsFinite(annotation.StartY) ||
            !float.IsFinite(annotation.EndX) ||
            !float.IsFinite(annotation.EndY))
            return null;

        if (string.IsNullOrWhiteSpace(label))
            label = annotation.Type == ZonaAnnotationType.Marker ? "Marker" : "Box";

        return new ZonaAnnotation(
            annotation.Type,
            Math.Clamp(annotation.StartX, 0f, 1f),
            Math.Clamp(annotation.StartY, 0f, 1f),
            Math.Clamp(annotation.EndX, 0f, 1f),
            Math.Clamp(annotation.EndY, 0f, 1f),
            label,
            annotation.PackedColor,
            Math.Clamp(annotation.StrokeWidth, 1f, 12f),
            null);
    }
}
