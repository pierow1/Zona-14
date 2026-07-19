// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
namespace Content.Shared._Zona14.WorldMap;

/// <summary>
/// Server-side clamping/validation for incoming map annotations. Shared by the paper-map
/// system and the PDA cartridge so both enforce the same bounds and caps.
/// </summary>
public static class WorldMapAnnotationSanitizer
{
    public const int MaxStrokeCoordinates = 192;
    private const int MaxLabelCharacters = 64;

    public static WorldMapAnnotation? Sanitize(WorldMapAnnotation annotation)
    {
        if (annotation.Type is not WorldMapAnnotationType.Marker and
            not WorldMapAnnotationType.Box and
            not WorldMapAnnotationType.Draw)
            return null;

        if (!float.IsFinite(annotation.StrokeWidth))
            return null;

        var label = (annotation.Label ?? string.Empty).Trim();
        if (label.Length > MaxLabelCharacters)
            label = label[..MaxLabelCharacters].TrimEnd();

        if (annotation.Type == WorldMapAnnotationType.Draw)
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

            return new WorldMapAnnotation(
                WorldMapAnnotationType.Draw,
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
            label = annotation.Type == WorldMapAnnotationType.Marker ? "Marker" : "Box";

        return new WorldMapAnnotation(
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
