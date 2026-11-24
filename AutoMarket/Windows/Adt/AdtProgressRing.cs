using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/ProgressRing.cs (AGPL-3.0).</summary>
internal static class AdtProgressRing
{
    private const float Top = -MathF.PI / 2f;

    private static Vector2 Dir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    private static void Arc(Vector2 center, float radius, float thickness, float startAngle, float endAngle, uint color)
    {
        var dl = ImGui.GetWindowDrawList();
        var span = MathF.Abs(endAngle - startAngle);
        var segments = Math.Max(2, (int)MathF.Ceiling(span / (MathF.PI / 48f)));
        var previous = center + Dir(startAngle) * radius;
        for (var segment = 1; segment <= segments; segment++)
        {
            var angle = startAngle + (endAngle - startAngle) * (segment / (float)segments);
            var current = center + Dir(angle) * radius;
            dl.AddLine(previous, current, color, thickness);
            previous = current;
        }

        var cap = thickness * 0.5f;
        dl.AddCircleFilled(center + Dir(startAngle) * radius, cap, color);
        dl.AddCircleFilled(center + Dir(endAngle) * radius, cap, color);
    }

    public static void Glow(Vector2 center, float radius, Vector4 color, float intensity)
    {
        var dl = ImGui.GetWindowDrawList();
        for (var layer = 4; layer >= 1; layer--)
        {
            var layerRadius = radius * (0.72f + layer * 0.17f);
            var alpha = Math.Clamp(intensity * 0.05f * (5 - layer), 0f, 0.5f);
            dl.AddCircleFilled(center, layerRadius, AdtPaint.Col(AdtStyling.WithAlpha(color, alpha)));
        }
    }

    public static void Track(Vector2 center, float radius, float thickness, Vector4 color)
        => Arc(center, radius, thickness, Top, Top + MathF.PI * 2f, AdtPaint.Col(color));

    public static void Fill(Vector2 center, float radius, float thickness, float fraction, Vector4 color)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        if (fraction <= 0.0001f) return;

        Arc(center, radius, thickness, Top, Top + fraction * MathF.PI * 2f, AdtPaint.Col(color));
    }

    public static void Sweep(Vector2 center, float radius, float thickness, Vector4 color, double periodMs, float arcLength, float headAlpha)
    {
        var dl = ImGui.GetWindowDrawList();
        var head = Top + AdtStyling.Phase(periodMs) * MathF.PI * 2f;
        var tail = head - arcLength;
        var steps = Math.Max(10, (int)MathF.Ceiling(arcLength / (MathF.PI / 36f)));
        var previous = center + Dir(tail) * radius;
        for (var step = 1; step <= steps; step++)
        {
            var t = step / (float)steps;
            var angle = tail + (head - tail) * t;
            var current = center + Dir(angle) * radius;
            dl.AddLine(previous, current, AdtPaint.Col(AdtStyling.WithAlpha(color, headAlpha * t * t)), thickness);
            previous = current;
        }

        dl.AddCircleFilled(center + Dir(head) * radius, thickness * 0.62f, AdtPaint.Col(AdtStyling.WithAlpha(color, headAlpha)));
    }

    public static void CenterValue(Vector2 center, string big, string? small, Vector4 bigColor, Vector4 smallColor)
    {
        Vector2 bigSize;
        using (AdtFonts.PushTitle())
            bigSize = AdtTextDraw.Measure(big);

        var hasSmall = !string.IsNullOrEmpty(small);
        var smallSize = Vector2.Zero;
        if (hasSmall)
        {
            using (AdtFonts.PushCaption())
                smallSize = AdtTextDraw.Measure(small!);
        }

        var gap = hasSmall ? 1f : 0f;
        var top = center.Y - (bigSize.Y + gap + smallSize.Y) * 0.5f;

        using (AdtFonts.PushTitle())
            AdtTextDraw.At(big, new Vector2(center.X - bigSize.X * 0.5f, top), bigColor);

        if (!hasSmall) return;

        using (AdtFonts.PushCaption())
            AdtTextDraw.At(small!, new Vector2(center.X - smallSize.X * 0.5f, top + bigSize.Y + gap), smallColor);
    }
}
