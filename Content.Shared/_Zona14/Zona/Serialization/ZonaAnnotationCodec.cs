using System.Text;

namespace Content.Shared._Zona14.Zona.Serialization;

/// <summary>
/// encodes map annotations into the existing Notekeeper cartridge's server-owned string list
/// the format intentionally uses plain text and primitive parsing only so Content.Shared passes
/// Robust's sandbox type checks on both the client and server. a chud hack made by a chud man
/// </summary>
public static class ZonaAnnotationCodec
{
    private const string Prefix = "Z14MAP2:";
    private const int MaxLabelCharacters = 64;
    private const int MaxStrokeCoordinates = 192;
    private const string HexDigits = "0123456789ABCDEF";

    public static string Encode(ZonaAnnotation annotation)
    {
        var label = (annotation.Label ?? string.Empty).Trim();
        if (label.Length > MaxLabelCharacters)
            label = label[..MaxLabelCharacters].TrimEnd();

        var points = annotation.Type == ZonaAnnotationType.Draw
            ? annotation.StrokePoints
            : null;
        var coordinateCount = points == null
            ? 0
            : Math.Min(points.Length & ~1, MaxStrokeCoordinates);

        var builder = new StringBuilder(128 + label.Length * 4 + coordinateCount * 6);
        builder.Append(Prefix);
        builder.Append((byte) annotation.Type);
        builder.Append('|');
        builder.Append(QuantizeUv(annotation.StartX));
        builder.Append('|');
        builder.Append(QuantizeUv(annotation.StartY));
        builder.Append('|');
        builder.Append(QuantizeUv(annotation.EndX));
        builder.Append('|');
        builder.Append(QuantizeUv(annotation.EndY));
        builder.Append('|');
        builder.Append(annotation.PackedColor);
        builder.Append('|');
        builder.Append((byte) Math.Clamp((int) MathF.Round(annotation.StrokeWidth * 4f), 4, 48));
        builder.Append('|');
        AppendLabel(builder, label);
        builder.Append('|');
        builder.Append(coordinateCount);

        for (var i = 0; i < coordinateCount; i++)
        {
            builder.Append('|');
            builder.Append(QuantizeUv(points![i]));
        }

        return builder.ToString();
    }

    public static bool TryDecode(string record, out ZonaAnnotation annotation)
    {
        annotation = default;

        if (!record.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var fields = record[Prefix.Length..].Split('|');
        if (fields.Length < 9)
            return false;

        if (!byte.TryParse(fields[0], out var rawType) ||
            !ushort.TryParse(fields[1], out var rawStartX) ||
            !ushort.TryParse(fields[2], out var rawStartY) ||
            !ushort.TryParse(fields[3], out var rawEndX) ||
            !ushort.TryParse(fields[4], out var rawEndY) ||
            !uint.TryParse(fields[5], out var packedColor) ||
            !byte.TryParse(fields[6], out var rawStrokeWidth) ||
            !TryDecodeLabel(fields[7], out var label) ||
            !int.TryParse(fields[8], out var coordinateCount))
        {
            return false;
        }

        var type = (ZonaAnnotationType) rawType;
        if (type is not ZonaAnnotationType.Marker and
            not ZonaAnnotationType.Box and
            not ZonaAnnotationType.Draw)
        {
            return false;
        }

        if (coordinateCount < 0 ||
            coordinateCount > MaxStrokeCoordinates ||
            (coordinateCount & 1) != 0 ||
            fields.Length != 9 + coordinateCount)
        {
            return false;
        }

        float[]? points = null;
        if (type == ZonaAnnotationType.Draw)
        {
            if (coordinateCount < 4)
                return false;

            points = new float[coordinateCount];
            for (var i = 0; i < coordinateCount; i++)
            {
                if (!ushort.TryParse(fields[9 + i], out var rawPoint))
                    return false;

                points[i] = DequantizeUv(rawPoint);
            }
        }
        else if (coordinateCount != 0)
        {
            return false;
        }

        annotation = new ZonaAnnotation(
            type,
            DequantizeUv(rawStartX),
            DequantizeUv(rawStartY),
            DequantizeUv(rawEndX),
            DequantizeUv(rawEndY),
            label,
            packedColor,
            Math.Clamp(rawStrokeWidth / 4f, 1f, 12f),
            points);
        return true;
    }

    private static void AppendLabel(StringBuilder builder, string label)
    {
        foreach (var character in label)
        {
            builder.Append(HexDigits[(character >> 12) & 0xF]);
            builder.Append(HexDigits[(character >> 8) & 0xF]);
            builder.Append(HexDigits[(character >> 4) & 0xF]);
            builder.Append(HexDigits[character & 0xF]);
        }
    }

    private static bool TryDecodeLabel(string encoded, out string label)
    {
        label = string.Empty;

        if ((encoded.Length & 3) != 0 || encoded.Length > MaxLabelCharacters * 4)
            return false;

        if (encoded.Length == 0)
            return true;

        var characters = new char[encoded.Length / 4];
        for (var i = 0; i < characters.Length; i++)
        {
            var offset = i * 4;
            var a = ParseHexDigit(encoded[offset]);
            var b = ParseHexDigit(encoded[offset + 1]);
            var c = ParseHexDigit(encoded[offset + 2]);
            var d = ParseHexDigit(encoded[offset + 3]);

            if (a < 0 || b < 0 || c < 0 || d < 0)
                return false;

            characters[i] = (char) ((a << 12) | (b << 8) | (c << 4) | d);
        }

        label = new string(characters);
        return true;
    }

    private static int ParseHexDigit(char value)
    {
        if (value is >= '0' and <= '9')
            return value - '0';
        if (value is >= 'A' and <= 'F')
            return value - 'A' + 10;
        if (value is >= 'a' and <= 'f')
            return value - 'a' + 10;
        return -1;
    }

    private static ushort QuantizeUv(float value)
    {
        if (!float.IsFinite(value))
            value = 0f;

        return (ushort) Math.Clamp(
            (int) MathF.Round(Math.Clamp(value, 0f, 1f) * ushort.MaxValue),
            0,
            ushort.MaxValue);
    }

    private static float DequantizeUv(ushort value)
    {
        return value / (float) ushort.MaxValue;
    }
}
