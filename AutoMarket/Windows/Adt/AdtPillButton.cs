using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/PillButton.cs (AGPL-3.0):
/// a rounded pill that can be a passive status badge (Ghost/Tinted) or a clickable button
/// (any emphasis - the caller just ignores the return value for a display-only pill).</summary>
internal static class AdtPillButton
{
    public enum Emphasis { Ghost, Tinted, Filled }

    private const float PadX = 13f;
    private const float IconGap = 6f;
    private const float DefaultHeight = 28f;

    public static float Width(string label, FontAwesomeIcon? icon = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconWidth = icon is { } glyph ? AdtTextDraw.IconSize(glyph).X + IconGap * scale : 0f;
        return PadX * 2f * scale + iconWidth + AdtTextDraw.Measure(label).X;
    }

    public static bool Draw(string id, string label, Vector4 accent, Emphasis emphasis = Emphasis.Tinted,
        FontAwesomeIcon? icon = null, bool enabled = true, float height = DefaultHeight, string? tooltip = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(Width(label, icon), height * scale);
        var origin = ImGui.GetCursorScreenPos();
        var hit = AdtHit.Area(id, size, enabled);
        var hover = AdtMotion.Hover(AdtMotion.Key(id), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var end = origin + size;

        var (fill, border, text) = Palette(accent, emphasis, hover, enabled);
        if (hit.Held) fill = AdtStyling.Darken(fill, 0.12f);

        AdtPaint.Pill(dl, origin, end, fill, border);
        if (emphasis == Emphasis.Filled && enabled) AdtPaint.TopLight(dl, origin, end, size.Y * 0.5f, 0.22f);

        var midY = origin.Y + size.Y * 0.5f;
        var x = origin.X + PadX * scale;
        if (icon is { } glyph)
        {
            var iconSize = AdtTextDraw.IconSize(glyph);
            using (AdtFonts.PushIcon())
                AdtTextDraw.At(glyph.ToIconString(), new Vector2(x, midY - iconSize.Y * 0.5f), text);
            x += iconSize.X + IconGap * scale;
        }

        var labelSize = AdtTextDraw.Measure(label);
        AdtTextDraw.At(label, new Vector2(x, midY - labelSize.Y * 0.5f), text);

        if (hit.Hovered && tooltip is not null) AdtTooltip.Show(tooltip);
        return hit.Clicked;
    }

    private static (Vector4 Fill, Vector4 Border, Vector4 Text) Palette(Vector4 accent, Emphasis emphasis, float hover, bool enabled)
    {
        if (!enabled)
            return (AdtStyling.WithAlpha(AdtStyling.Surface1, 0.6f), AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.5f), AdtStyling.TextMuted);

        return emphasis switch
        {
            Emphasis.Filled => (
                Vector4.Lerp(accent, AdtStyling.Lighten(accent, 0.15f), hover),
                AdtStyling.WithAlpha(AdtStyling.Lighten(accent, 0.5f), 0.6f),
                AdtStyling.TextStrong),
            Emphasis.Tinted => (
                AdtStyling.WithAlpha(accent, 0.16f + 0.12f * hover),
                AdtStyling.WithAlpha(accent, 0.45f + 0.30f * hover),
                Vector4.Lerp(AdtStyling.Lighten(accent, 0.25f), AdtStyling.TextStrong, hover * 0.5f)),
            _ => (
                AdtStyling.WithAlpha(AdtStyling.Surface2, 0.9f * hover),
                AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.5f + 0.3f * hover),
                Vector4.Lerp(AdtStyling.TextSecondary, AdtStyling.TextStrong, hover)),
        };
    }
}
