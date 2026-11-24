using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/HeroButton.cs (AGPL-3.0):
/// the big primary call-to-action button (glow + gradient + lift-on-hover), used in ADT's
/// ActionDock for Start/Stop. AutoMarket uses this for "start route"/"stop route".</summary>
internal static class AdtHeroButton
{
    private const float Rounding = 14f;
    private const float PadX = 20f;
    private const float Height = 54f;

    public static bool Draw(FontAwesomeIcon icon, string title, string? sublabel, Vector4 accent, bool enabled, string? disabledReason = null, float width = 0f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = Height * scale;
        if (width <= 0f) width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, height);
        var origin = ImGui.GetCursorScreenPos();

        ImGui.PushID((int)icon);
        var hit = AdtHit.Area("##hero", size, enabled);
        var hover = AdtMotion.Hover(AdtMotion.Key("##hero"), hit.Hovered);
        var press = AdtMotion.Approach(AdtMotion.Key("##hero_press"), hit.Held ? 1f : 0f, 30f);
        ImGui.PopID();

        var lift = enabled ? (hover * 2f - press * 2f) * scale : 0f;
        var min = origin - new Vector2(0f, lift);
        var max = min + size;
        var rounding = Rounding * scale;
        var dl = ImGui.GetWindowDrawList();

        if (enabled)
        {
            var pulse = 0.5f + 0.5f * AdtStyling.Pulse(AdtStyling.PulseBreath);
            AdtGlow.Rect(dl, min, max, rounding, accent, 0.35f + 0.35f * pulse + hover * 0.6f);
            AdtPaint.Shadow(dl, min, max, rounding, 8f * scale, 0.5f);
            var top = Vector4.Lerp(AdtStyling.Lighten(accent, 0.18f), AdtStyling.Lighten(accent, 0.30f), hover);
            var bottom = Vector4.Lerp(AdtStyling.Darken(accent, 0.14f), accent, hover);
            AdtPaint.Gradient(dl, min, max, top, bottom, rounding);
            AdtPaint.TopLight(dl, min, max, rounding, 0.28f);
            AdtPaint.Stroke(dl, min, max, AdtStyling.WithAlpha(AdtStyling.Lighten(accent, 0.5f), 0.55f + hover * 0.3f), rounding);
        }
        else
        {
            AdtPaint.Fill(dl, min, max, AdtStyling.WithAlpha(AdtStyling.Surface1, 0.8f), rounding);
            AdtPaint.Stroke(dl, min, max, AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.7f), rounding);
        }

        var padX = PadX * scale;
        var midY = min.Y + height * 0.5f;
        var glyph = enabled ? icon : FontAwesomeIcon.Lock;
        var textColor = enabled ? AdtStyling.ForegroundOn(accent) : AdtStyling.TextMuted;

        var iconSize = AdtTextDraw.IconSize(glyph);
        using (AdtFonts.PushIcon())
            AdtTextDraw.At(glyph.ToIconString(), new Vector2(min.X + padX, midY - iconSize.Y * 0.5f), textColor);

        using (AdtFonts.PushHeadline())
        {
            var titleSize = AdtTextDraw.Measure(title);
            AdtTextDraw.At(title, new Vector2(min.X + padX + iconSize.X + 12f * scale, midY - titleSize.Y * 0.5f), textColor);
        }

        var sub = enabled ? sublabel : disabledReason ?? sublabel;
        if (!string.IsNullOrEmpty(sub))
        {
            var subSize = AdtTextDraw.Measure(sub);
            AdtTextDraw.At(sub, new Vector2(max.X - padX - subSize.X, midY - subSize.Y * 0.5f),
                enabled ? AdtStyling.WithAlpha(textColor, 0.8f) : AdtStyling.TextDim);
        }

        return hit.Clicked;
    }
}

/// <summary>Small helper split out of HeroButton.cs' inline Paint.Glow call (AdtPaint.Glass covers
/// cards; this is the plain glow-only halo HeroButton/ProgressRing use around a shape).</summary>
internal static class AdtGlow
{
    public static void Rect(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, Vector4 color, float intensity)
    {
        var scale = ImGuiHelpers.GlobalScale;
        for (var layer = 3; layer >= 1; layer--)
        {
            var grow = new Vector2(layer * 3f * scale, layer * 3f * scale);
            var alpha = 0.04f * (4 - layer) * intensity;
            dl.AddRectFilled(min - grow, max + grow, AdtPaint.Col(AdtStyling.WithAlpha(color, alpha)), rounding + grow.X);
        }
    }
}
