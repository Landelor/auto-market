using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/IconButton.cs (AGPL-3.0):
/// a round hover-highlighted icon button, used for the header's close/collapse buttons.</summary>
internal static class AdtIconButton
{
    public static bool Draw(FontAwesomeIcon icon, string id, float size, Vector4? color = null, string? tooltip = null, bool enabled = true)
    {
        var origin = ImGui.GetCursorScreenPos();
        var box = new Vector2(size, size);
        var hit = AdtHit.Area(id, box, enabled);
        var hover = AdtMotion.Hover(AdtMotion.Key(id), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var center = origin + box * 0.5f;

        if (hover > 0.01f)
        {
            var alpha = hover * (hit.Held ? 1f : 0.85f);
            dl.AddCircleFilled(center, size * 0.5f, AdtPaint.Col(AdtStyling.WithAlpha(AdtStyling.Surface3, alpha)));
        }

        var tint = color ?? AdtStyling.TextSecondary;
        var glyphColor = enabled ? Vector4.Lerp(tint, AdtStyling.TextStrong, hover * 0.55f) : AdtStyling.TextMuted;
        AdtTextDraw.IconCentered(icon, center, glyphColor);

        if (hit.Hovered && tooltip is not null) AdtTooltip.Show(tooltip);
        return hit.Clicked;
    }
}
