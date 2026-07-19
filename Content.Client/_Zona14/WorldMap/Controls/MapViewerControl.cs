// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Misfit-Sanctuary/nuclear-14 @ <source-commit> (AGPL-3.0). See CONTRIBUTING.md §5.
using System.Numerics;
using Content.Shared._Zona14.WorldMap;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Zona14.WorldMap.Controls;

public sealed class MapViewerControl : Control
{
    public enum AnnotationMode : byte
    {
        None,
        Marker,
        Box,
        Draw,
        Erase,
    }

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public event Action<WorldMapAnnotation>? OnAddAnnotation;
    public event Action<int>? OnRemoveAnnotation;
    public event Action? OnClearAnnotations;

    private readonly Font _labelFont;
    private Texture? _texture;
    private Box2 _worldBounds;
    private MapTrackedBlip[] _trackedBlips = Array.Empty<MapTrackedBlip>();
    private WorldMapAnnotation[] _annotations = Array.Empty<WorldMapAnnotation>();
    private AnnotationMode _annotationMode;
    private string _pendingAnnotationText = string.Empty;
    private Vector2? _annotationDragStartUv;
    private Vector2? _annotationDragCurrentUv;
    private readonly List<Vector2> _freeDrawUvPoints = new List<Vector2>();
    private bool _freeDrawActive;
    private int _hoveredAnnotationIndex = -1;
    private Color _currentColor = new Color(0.95f, 0.50f, 0.15f, 1f);
    private float _currentStrokeWidth = 3f;
    private float _zoom = 1f;
    private Vector2 _pan = Vector2.Zero;
    private bool _dragging;
    private Vector2 _dragStart;
    private Vector2 _panAtDragStart;

    private const int MaxStrokeUvPoints = 96;
    private const float ZoomStep = 1.15f;
    private const float ZoomMin = 0.5f;
    private const float ZoomMax = 16f;

    public MapViewerControl()
    {
        IoCManager.InjectDependencies(this);
        _labelFont = new VectorFont(_resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 8);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void SetTexture(Texture? texture, Box2 worldBounds)
    {
        _texture = texture;
        _worldBounds = worldBounds;
        _zoom = 1f;
        _pan = Vector2.Zero;
    }

    public void SetTrackedBlips(MapTrackedBlip[] trackedBlips) => _trackedBlips = trackedBlips;
    public void SetAnnotations(WorldMapAnnotation[] annotations) => _annotations = annotations;

    public void SetAnnotationMode(AnnotationMode mode)
    {
        _annotationMode = mode;
        _annotationDragStartUv = null;
        _annotationDragCurrentUv = null;
        _freeDrawActive = false;
        _freeDrawUvPoints.Clear();
        _hoveredAnnotationIndex = -1;
        _dragging = false;
    }

    public void SetPendingAnnotationText(string text) => _pendingAnnotationText = text;
    public void SetAnnotationColor(Color color) => _currentColor = color;
    public void SetAnnotationStrokeWidth(float width) => _currentStrokeWidth = Math.Clamp(width, 1f, 12f);
    public void ClearAnnotations() => OnClearAnnotations?.Invoke();

    private float FitScale => _texture == null || Size.X <= 0 || Size.Y <= 0
        ? 1f
        : Math.Min(Size.X / _texture.Width, Size.Y / _texture.Height);

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_texture == null)
            return;

        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3Helpers.CreateScale(new Vector2(UIScale)) * oldTransform);

        var (x, y, drawW, drawH) = GetDrawRect();
        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(x, y, drawW, drawH));

        if (TryGetPlayerUv(out var playerUv, out var playerHeading))
        {
            var markerPos = new Vector2(x + playerUv.X * drawW, y + playerUv.Y * drawH);
            DrawPlayerMarker(handle, markerPos, playerHeading);
        }

        foreach (var blip in _trackedBlips)
        {
            if (!TryGetUv(new Vector2(blip.X, blip.Y), out var uv))
                continue;

            var pos = new Vector2(x + uv.X * drawW, y + uv.Y * drawH);
            DrawBlip(handle, pos, blip.Kind);
            DrawLabel(handle, pos + new Vector2(14f, -14f), blip.Label, GetTrackedBlipColor(blip.Kind));
        }

        foreach (var annotation in _annotations)
            DrawAnnotation(handle, annotation, x, y, drawW, drawH);

        if (_annotationMode == AnnotationMode.Box && _annotationDragStartUv.HasValue && _annotationDragCurrentUv.HasValue)
        {
            DrawAnnotation(handle, new WorldMapAnnotation(
                WorldMapAnnotationType.Box,
                _annotationDragStartUv.Value.X,
                _annotationDragStartUv.Value.Y,
                _annotationDragCurrentUv.Value.X,
                _annotationDragCurrentUv.Value.Y,
                GetAnnotationLabel("Box"),
                ToPackedColor(_currentColor),
                _currentStrokeWidth,
                null), x, y, drawW, drawH, true);
        }

        if (_annotationMode == AnnotationMode.Erase && _hoveredAnnotationIndex >= 0 && _hoveredAnnotationIndex < _annotations.Length)
            DrawAnnotation(handle, _annotations[_hoveredAnnotationIndex], x, y, drawW, drawH, true, new Color(1f, 0.15f, 0.15f, 0.90f));

        if (_annotationMode == AnnotationMode.Draw && _freeDrawActive && _freeDrawUvPoints.Count >= 2)
        {
            var color = new Color(_currentColor.R, _currentColor.G, _currentColor.B, 0.70f);
            for (var i = 0; i < _freeDrawUvPoints.Count - 1; i++)
            {
                var p0 = new Vector2(x + _freeDrawUvPoints[i].X * drawW, y + _freeDrawUvPoints[i].Y * drawH);
                var p1 = new Vector2(x + _freeDrawUvPoints[i + 1].X * drawW, y + _freeDrawUvPoints[i + 1].Y * drawH);
                DrawThickLine(handle, p0, p1, color, _currentStrokeWidth);
            }
        }

        handle.SetTransform(oldTransform);
    }

    private (float X, float Y, float W, float H) GetDrawRect()
    {
        if (_texture == null)
            return (0, 0, 0, 0);

        var scale = FitScale * _zoom;
        var drawW = _texture.Width * scale;
        var drawH = _texture.Height * scale;
        var x = (Size.X - drawW) / 2f + _pan.X;
        var y = (Size.Y - drawH) / 2f + _pan.Y;
        return (x, y, drawW, drawH);
    }

    private bool TryGetPlayerUv(out Vector2 uv, out Vector2 screenHeading)
    {
        uv = default;
        screenHeading = Vector2.UnitX;
        if (_worldBounds.Width <= 0 || _worldBounds.Height <= 0)
            return false;

        var localEntity = _playerManager.LocalEntity;
        if (!localEntity.HasValue || !_entityManager.TryGetComponent<TransformComponent>(localEntity.Value, out var xform))
            return false;

        // Robust angles are in world Y-up coordinates, while UI Y grows downward.
        var worldHeading = xform.LocalRotation.ToVec();
        screenHeading = new Vector2(worldHeading.X, -worldHeading.Y);

        // Zona14; the map is north-up, but the entity-facing vector is offset by
        // 90 degrees for the chevron... rotated it counter-clockwise in screen space:
        // north now points up and south now points down. yes, i know...a very chud hack, dont mpreg me   -pierow
        screenHeading = new Vector2(-screenHeading.Y, screenHeading.X);

        if (screenHeading.LengthSquared() < 0.001f)
            screenHeading = Vector2.UnitX;
        else
            screenHeading = Vector2.Normalize(screenHeading);

        var xformSystem = _entityManager.System<SharedTransformSystem>();

        // world map can contain several grids loaded from separate YAML files.
        // prefer map/world coordinates so the marker does not reset to each grid's local origin. we'll see if it works
		// once the map is done...
        if (TryGetUv(xformSystem.GetMapCoordinates(xform).Position, out uv))
            return true;

        if (TryGetUv(xformSystem.GetWorldPosition(xform), out uv))
            return true;

        // fallback failsafe for legacy single-grid maps whose configured bounds are grid-local.
        return TryGetUv(xform.Coordinates.Position, out uv);
    }
    private bool TryGetUv(Vector2 position, out Vector2 uv)
    {
        var u = (position.X - _worldBounds.Left) / _worldBounds.Width;
        var v = 1f - (position.Y - _worldBounds.Bottom) / _worldBounds.Height;
        uv = new Vector2(u, v);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    private bool TryControlToUv(Vector2 controlPosition, out Vector2 uv)
    {
        uv = default;
        if (_texture == null)
            return false;

        var (x, y, drawW, drawH) = GetDrawRect();
        var u = (controlPosition.X - x) / drawW;
        var v = (controlPosition.Y - y) / drawH;
        uv = new Vector2(u, v);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (_texture == null)
            return;

        var oldZoom = _zoom;
        if (args.Delta.Y > 0)
            _zoom = Math.Min(_zoom * ZoomStep, ZoomMax);
        else if (args.Delta.Y < 0)
            _zoom = Math.Max(_zoom / ZoomStep, ZoomMin);

        var centerOffset = args.RelativePosition - Size / 2f;
        _pan = ((_pan - centerOffset) * (_zoom / oldZoom)) + centerOffset;
        ClampPan();
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            if (TryGetAnnotationIndexAt(args.RelativePosition, out var index))
            {
                OnRemoveAnnotation?.Invoke(index);
                args.Handle();
            }
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_annotationMode == AnnotationMode.Erase)
        {
            if (_hoveredAnnotationIndex >= 0)
                OnRemoveAnnotation?.Invoke(_hoveredAnnotationIndex);
            args.Handle();
            return;
        }

        if (_annotationMode == AnnotationMode.Draw)
        {
            if (TryControlToUv(args.RelativePosition, out var drawUv))
            {
                _freeDrawUvPoints.Clear();
                _freeDrawUvPoints.Add(drawUv);
                _freeDrawActive = true;
                args.Handle();
            }
            return;
        }

        if (_annotationMode == AnnotationMode.Marker)
        {
            if (TryControlToUv(args.RelativePosition, out var uv))
            {
                OnAddAnnotation?.Invoke(new WorldMapAnnotation(WorldMapAnnotationType.Marker, uv.X, uv.Y, uv.X, uv.Y, GetAnnotationLabel("Marker"), ToPackedColor(_currentColor), _currentStrokeWidth, null));
                args.Handle();
            }
            return;
        }

        if (_annotationMode == AnnotationMode.Box)
        {
            if (TryControlToUv(args.RelativePosition, out var uv))
            {
                _annotationDragStartUv = uv;
                _annotationDragCurrentUv = uv;
                args.Handle();
            }
            return;
        }

        _dragging = true;
        _dragStart = args.RelativePosition;
        _panAtDragStart = _pan;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_annotationMode == AnnotationMode.Draw && _freeDrawActive)
        {
            _freeDrawActive = false;
            if (_freeDrawUvPoints.Count >= 2)
            {
                var pts = new float[_freeDrawUvPoints.Count * 2];
                for (var i = 0; i < _freeDrawUvPoints.Count; i++)
                {
                    pts[i * 2] = _freeDrawUvPoints[i].X;
                    pts[i * 2 + 1] = _freeDrawUvPoints[i].Y;
                }
                OnAddAnnotation?.Invoke(new WorldMapAnnotation(WorldMapAnnotationType.Draw, 0f, 0f, 0f, 0f, GetAnnotationLabel("Drawing"), ToPackedColor(_currentColor), _currentStrokeWidth, pts));
            }
            _freeDrawUvPoints.Clear();
            args.Handle();
            return;
        }

        if (_annotationMode == AnnotationMode.Box && _annotationDragStartUv.HasValue && _annotationDragCurrentUv.HasValue)
        {
            OnAddAnnotation?.Invoke(new WorldMapAnnotation(WorldMapAnnotationType.Box, _annotationDragStartUv.Value.X, _annotationDragStartUv.Value.Y, _annotationDragCurrentUv.Value.X, _annotationDragCurrentUv.Value.Y, GetAnnotationLabel("Box"), ToPackedColor(_currentColor), _currentStrokeWidth, null));
            _annotationDragStartUv = null;
            _annotationDragCurrentUv = null;
            args.Handle();
            return;
        }

        _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_annotationMode == AnnotationMode.Erase)
        {
            TryGetAnnotationIndexAt(args.RelativePosition, out _hoveredAnnotationIndex);
            return;
        }

        if (_annotationMode == AnnotationMode.Draw && _freeDrawActive)
        {
            if (TryControlToUv(args.RelativePosition, out var uv) && _freeDrawUvPoints.Count < MaxStrokeUvPoints)
            {
                if (_freeDrawUvPoints.Count == 0 || Vector2.DistanceSquared(uv, _freeDrawUvPoints[_freeDrawUvPoints.Count - 1]) > 0.00005f)
                    _freeDrawUvPoints.Add(uv);
            }
            return;
        }

        if (_annotationMode == AnnotationMode.Box && _annotationDragStartUv.HasValue)
        {
            if (TryControlToUv(args.RelativePosition, out var uv))
                _annotationDragCurrentUv = uv;
            return;
        }

        if (!_dragging)
            return;

        _pan = _panAtDragStart + (args.RelativePosition - _dragStart);
        ClampPan();
    }

    private void ClampPan()
    {
        if (_texture == null)
            return;

        var scale = FitScale * _zoom;
        _pan.X = Math.Clamp(_pan.X, -_texture.Width * scale / 2f, _texture.Width * scale / 2f);
        _pan.Y = Math.Clamp(_pan.Y, -_texture.Height * scale / 2f, _texture.Height * scale / 2f);
    }

    private bool TryGetAnnotationIndexAt(Vector2 controlPosition, out int index)
    {
        index = -1;
        if (_texture == null || _annotations.Length == 0)
            return false;

        var (x, y, drawW, drawH) = GetDrawRect();
        var bestDistance = float.PositiveInfinity;
        const float maxDistance = 24f;

        for (var i = 0; i < _annotations.Length; i++)
        {
            var distance = GetAnnotationDistance(_annotations[i], controlPosition, x, y, drawW, drawH);
            if (distance > maxDistance || distance >= bestDistance)
                continue;

            bestDistance = distance;
            index = i;
        }

        return index >= 0;
    }

    private void DrawPlayerMarker(DrawingHandleScreen handle, Vector2 pos, Vector2 heading)
    {
        var pulse = (float) (0.5 + 0.5 * Math.Sin(_gameTiming.RealTime.TotalSeconds * 4.0));
        handle.DrawCircle(pos, 10f + 2f * pulse, new Color(1f, 0.15f, 0.15f, 0.18f + 0.12f * pulse));

        var perpendicular = new Vector2(-heading.Y, heading.X);
        var tip = pos + heading * 9f;
        var wingCenter = pos - heading * 5.5f;
        var left = wingCenter + perpendicular * 6f;
        var right = wingCenter - perpendicular * 6f;

        // black underlay keeps the smaller chevron readable over light map areas.
        DrawThickLine(handle, left, tip, new Color(0f, 0f, 0f, 0.80f), 6f);
        DrawThickLine(handle, tip, right, new Color(0f, 0f, 0f, 0.80f), 6f);
        DrawThickLine(handle, left, tip, new Color(1f, 0.12f, 0.12f, 1f), 3f);
        DrawThickLine(handle, tip, right, new Color(1f, 0.12f, 0.12f, 1f), 3f);
    }

    private static void DrawBlip(DrawingHandleScreen handle, Vector2 pos, MapTrackedBlipKind kind)
    {
        var color = GetTrackedBlipColor(kind);
        handle.DrawCircle(pos, 16f, new Color(0f, 0f, 0f, 0.55f));
        handle.DrawCircle(pos, 11f, color);
        handle.DrawCircle(pos, 3.5f, Color.White);
    }

    private void DrawAnnotation(DrawingHandleScreen handle, WorldMapAnnotation annotation, float x, float y, float drawW, float drawH, bool preview = false, Color? overrideColor = null)
    {
        var baseColor = overrideColor ?? FromPackedColor(annotation.PackedColor);
        var color = new Color(baseColor.R, baseColor.G, baseColor.B, preview ? 0.55f : 0.90f);

        switch (annotation.Type)
        {
            case WorldMapAnnotationType.Marker:
            {
                var pos = new Vector2(x + annotation.StartX * drawW, y + annotation.StartY * drawH);
                handle.DrawCircle(pos, 13f, new Color(0f, 0f, 0f, 0.6f));
                handle.DrawCircle(pos, 9f, color);
                handle.DrawCircle(pos, 3f, Color.White);
                DrawLabel(handle, pos + new Vector2(16f, 10f), annotation.Label, color);
                break;
            }

            case WorldMapAnnotationType.Box:
            {
                var start = new Vector2(x + annotation.StartX * drawW, y + annotation.StartY * drawH);
                var end = new Vector2(x + annotation.EndX * drawW, y + annotation.EndY * drawH);
                var topLeft = Vector2.Min(start, end);
                var bottomRight = Vector2.Max(start, end);
                DrawBorder(handle, new UIBox2(topLeft, bottomRight), color, annotation.StrokeWidth);
                DrawLabel(handle, topLeft + new Vector2(6f, 6f), annotation.Label, color);
                break;
            }

            case WorldMapAnnotationType.Draw:
            {
                var pts = annotation.StrokePoints;
                if (pts == null || pts.Length < 4)
                    break;

                for (var i = 0; i < pts.Length - 2; i += 2)
                {
                    var p0 = new Vector2(x + pts[i] * drawW, y + pts[i + 1] * drawH);
                    var p1 = new Vector2(x + pts[i + 2] * drawW, y + pts[i + 3] * drawH);
                    DrawThickLine(handle, p0, p1, color, annotation.StrokeWidth);
                }

                break;
            }
        }
    }

    private void DrawLabel(DrawingHandleScreen handle, Vector2 position, string label, Color color)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var textDimensions = handle.GetDimensions(_labelFont, label, 1f);
        var padding = new Vector2(4f, 2f);
        handle.DrawRect(new UIBox2(position - padding, position + textDimensions + padding), new Color(0f, 0f, 0f, 0.82f));
        handle.DrawString(_labelFont, position, label, color);
    }

    private static void DrawThickLine(DrawingHandleScreen handle, Vector2 p0, Vector2 p1, Color color, float width)
    {
        if (width <= 1.5f)
        {
            handle.DrawLine(p0, p1, color);
            return;
        }

        var dir = p1 - p0;
        var len = dir.Length();

        if (len < 0.001f)
        {
            handle.DrawCircle(p0, width / 2f, color);
            return;
        }

        dir /= len;
        var perp = new Vector2(-dir.Y, dir.X) * (width / 2f);

        handle.DrawPrimitives(
            DrawPrimitiveTopology.TriangleList,
            new[]
            {
                p0 - perp,
                p0 + perp,
                p1 + perp,
                p0 - perp,
                p1 + perp,
                p1 - perp,
            },
            color);

        handle.DrawCircle(p0, width / 2f, color);
        handle.DrawCircle(p1, width / 2f, color);
    }

    private static void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color, float width)
    {
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + width), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - width, rect.Right, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + width, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Right - width, rect.Top, rect.Right, rect.Bottom), color);
    }

    private static float GetAnnotationDistance(WorldMapAnnotation annotation, Vector2 position, float x, float y, float drawW, float drawH)
    {
        switch (annotation.Type)
        {
            case WorldMapAnnotationType.Marker:
                return Vector2.Distance(position, new Vector2(x + annotation.StartX * drawW, y + annotation.StartY * drawH));

            case WorldMapAnnotationType.Box:
                return DistanceToBox(annotation, position, x, y, drawW, drawH);

            case WorldMapAnnotationType.Draw:
                return DistanceToStroke(annotation, position, x, y, drawW, drawH);

            default:
                return float.PositiveInfinity;
        }
    }

    private static float DistanceToBox(WorldMapAnnotation annotation, Vector2 position, float x, float y, float drawW, float drawH)
    {
        var start = new Vector2(x + annotation.StartX * drawW, y + annotation.StartY * drawH);
        var end = new Vector2(x + annotation.EndX * drawW, y + annotation.EndY * drawH);
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);

        var dx = Math.Max(min.X - position.X, Math.Max(0f, position.X - max.X));
        var dy = Math.Max(min.Y - position.Y, Math.Max(0f, position.Y - max.Y));

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float DistanceToStroke(WorldMapAnnotation annotation, Vector2 position, float x, float y, float drawW, float drawH)
    {
        var pts = annotation.StrokePoints;
        if (pts == null || pts.Length < 4)
            return float.PositiveInfinity;

        var minDist = float.PositiveInfinity;

        for (var i = 0; i < pts.Length - 2; i += 2)
        {
            var p0 = new Vector2(x + pts[i] * drawW, y + pts[i + 1] * drawH);
            var p1 = new Vector2(x + pts[i + 2] * drawW, y + pts[i + 3] * drawH);
            minDist = Math.Min(minDist, DistanceToLineSegment(position, p0, p1));
        }

        return minDist;
    }

    private static float DistanceToLineSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = Vector2.Dot(ab, ab);

        if (lenSq < 0.0001f)
            return Vector2.Distance(p, a);

        var t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
        return Vector2.Distance(p, a + t * ab);
    }

    private string GetAnnotationLabel(string fallback)
    {
        return string.IsNullOrWhiteSpace(_pendingAnnotationText)
            ? fallback
            : _pendingAnnotationText.Trim();
    }

    private static uint ToPackedColor(Color c)
    {
        return ((uint)(Math.Clamp(c.R, 0f, 1f) * 255) << 24)
             | ((uint)(Math.Clamp(c.G, 0f, 1f) * 255) << 16)
             | ((uint)(Math.Clamp(c.B, 0f, 1f) * 255) << 8)
             | (uint)(Math.Clamp(c.A, 0f, 1f) * 255);
    }

    private static Color FromPackedColor(uint packed)
    {
        return new Color(
            ((packed >> 24) & 0xFF) / 255f,
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            (packed & 0xFF) / 255f);
    }

private static Color GetTrackedBlipColor(MapTrackedBlipKind kind)
{
    switch (kind)
    {
        /*
         * Zona14; Fallout-specific faction and rank blips are intentionally
         * disabled, and i doubt we'll even need it but just in-case it's here
         *
         * case MapTrackedBlipKind.Elder:
         * case MapTrackedBlipKind.Paladin:
         * case MapTrackedBlipKind.Knight:
         * case MapTrackedBlipKind.Scribe:
         * case MapTrackedBlipKind.Squire:
         * case MapTrackedBlipKind.LegionCenturion:
         * case MapTrackedBlipKind.LegionDecanus:
         * case MapTrackedBlipKind.LegionWarrior:
         * case MapTrackedBlipKind.LegionRecruit:
         * case MapTrackedBlipKind.PipBoyContact:
         * case MapTrackedBlipKind.PipBoyGroupMember:
         * case MapTrackedBlipKind.TribalHuntTarget:
         */

        case MapTrackedBlipKind.DeadBody:
            return new Color(0.9f, 0.9f, 0.9f, 1f);

        default:
            return new Color(0.98f, 0.84f, 0.15f, 0.95f);
    }
}}