using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Direct port of Auto Daily Tribes' Windows/Paint.cs (AGPL-3.0).</summary>
internal static class AdtPaint
{
    private const int ShadowLayers = 5;

    public static uint Col(Vector4 color) => ImGui.GetColorU32(color);

    private static uint Opaque(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color with { W = 1f });

    public static void Fill(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, float rounding, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
        => dl.AddRectFilled(min, max, Col(color), rounding, flags);

    public static void Stroke(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, float rounding, float thickness = 1f, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
        => dl.AddRect(min, max, Col(color), rounding, flags, thickness);

    public static void Gradient(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 top, Vector4 bottom, float rounding, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
    {
        var start = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, Col(new Vector4(1f, 1f, 1f, top.W)), rounding, flags);
        var end = dl.VtxBuffer.Size;
        ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(dl, start, end, min, new Vector2(min.X, max.Y), Opaque(top), Opaque(bottom));
    }

    public static void Shadow(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float spread, float alpha)
    {
        var offset = new Vector2(0f, spread * 0.4f);
        for (var layer = ShadowLayers; layer >= 1; layer--)
        {
            var t = layer / (float)ShadowLayers;
            var grow = new Vector2(spread * t, spread * t);
            var layerAlpha = alpha / ShadowLayers * (1.25f - t);
            dl.AddRectFilled(min - grow + offset, max + grow + offset, Col(new Vector4(0f, 0f, 0f, layerAlpha)), rounding + grow.X);
        }
    }

    public static void TopLight(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float alpha = 0.075f)
        => dl.AddLine(new Vector2(min.X + rounding, min.Y + 1f), new Vector2(max.X - rounding, min.Y + 1f), Col(new Vector4(1f, 1f, 1f, alpha)), 1f);

    /// <summary>
    /// The card surface used everywhere in ADT instead of a flat filled rect: a subtle top-to-bottom
    /// gradient tinted toward the accent, a 1px top highlight, and an accent-tinted border. This -
    /// not the flat <see cref="Fill"/> AutoMarket's cards currently use - is most of why ADT's cards
    /// read as "glass" instead of "flat".
    /// </summary>
    public static void Glass(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, Vector4 accent, float tint, float hover = 0f, bool elevated = false)
    {
        if (elevated) Shadow(dl, min, max, rounding, 10f * ImGuiHelpers.GlobalScale, 0.5f);

        var top = Vector4.Lerp(AdtStyling.Surface1, accent, tint * 1.3f);
        var bottom = Vector4.Lerp(AdtStyling.Surface0, accent, tint * 0.8f);
        if (hover > 0f)
        {
            top = Vector4.Lerp(top, AdtStyling.Surface2, hover * 0.6f);
            bottom = Vector4.Lerp(bottom, AdtStyling.Surface1, hover * 0.6f);
        }

        Gradient(dl, min, max, top with { W = 0.97f }, bottom with { W = 0.97f }, rounding);
        TopLight(dl, min, max, rounding);

        var borderMix = Math.Clamp(tint * 2.2f + hover * 0.4f, 0f, 1f);
        var border = Vector4.Lerp(AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.75f), AdtStyling.WithAlpha(accent, 0.9f), borderMix);
        Stroke(dl, min, max, border, rounding);
    }

    public static void Pill(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 fill, Vector4 border)
    {
        var rounding = (max.Y - min.Y) * 0.5f;
        Fill(dl, min, max, fill, rounding);
        Stroke(dl, min, max, border, rounding);
    }

    public static void Bar(ImDrawListPtr dl, Vector2 origin, float width, float height, float fraction, Vector4 color)
    {
        var rounding = height * 0.5f;
        var end = origin + new Vector2(width, height);
        Fill(dl, origin, end, AdtStyling.WithAlpha(AdtStyling.Surface0, 0.9f), rounding);
        Stroke(dl, origin, end, AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.55f), rounding);

        fraction = Math.Clamp(fraction, 0f, 1f);
        if (fraction <= 0f) return;

        var fillWidth = MathF.Max(height, width * fraction);
        var fillEnd = new Vector2(origin.X + fillWidth, end.Y);
        Gradient(dl, origin, fillEnd, AdtStyling.Lighten(color, 0.22f), color, rounding);
        dl.AddCircleFilled(new Vector2(fillEnd.X - rounding, origin.Y + rounding), rounding * 1.7f, Col(AdtStyling.WithAlpha(color, 0.22f)));
    }

    public static void Dot(ImDrawListPtr dl, Vector2 center, float radius, Vector4 color, float haloAlpha = 0.22f)
    {
        dl.AddCircleFilled(center, radius * 2.3f, Col(AdtStyling.WithAlpha(color, haloAlpha)));
        dl.AddCircleFilled(center, radius, Col(color));
    }

    public static void Hairline(ImDrawListPtr dl, Vector2 from, Vector2 to)
        => dl.AddLine(from, to, Col(AdtStyling.Hairline), 1f);

    public static void Divider(float verticalPadding = 6f)
    {
        AdtStyling.VSpace(verticalPadding);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Hairline(ImGui.GetWindowDrawList(), origin, origin + new Vector2(width, 0f));
        ImGui.Dummy(new Vector2(width, 1f));
        AdtStyling.VSpace(verticalPadding);
    }

    public static void Check(ImDrawListPtr dl, Vector2 center, float size, Vector4 color, float thickness)
    {
        var a = center + new Vector2(-size * 0.42f, 0f);
        var b = center + new Vector2(-size * 0.10f, size * 0.32f);
        var c = center + new Vector2(size * 0.46f, -size * 0.34f);
        var col = Col(color);
        dl.AddLine(a, b, col, thickness);
        dl.AddLine(b, c, col, thickness);
    }
}
