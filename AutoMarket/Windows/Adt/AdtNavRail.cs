using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows.Adt;

/// <summary>
/// Vertical icon rail for switching between a window's top-level sections - the ADT-style
/// left-hand tab rail (same role as Auto Daily Tribes' own section nav): one square,
/// hover-animated icon button per section, with the active one getting a tinted glass
/// background plus a thin accent bar on its leading edge so the current tab stays legible even
/// though the rail carries no text labels.
/// </summary>
internal static class AdtNavRail
{
    public static bool Item(FontAwesomeIcon icon, string id, float size, bool active, string tooltip)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var box = new Vector2(size, size);
        var hit = AdtHit.Area(id, box, true);
        var hover = AdtMotion.Hover(AdtMotion.Key(id), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var rounding = AdtStyling.CardRounding * scale;

        if (active)
        {
            AdtPaint.Fill(dl, origin, origin + box, AdtStyling.WithAlpha(AdtStyling.AccentTeal, 0.20f), rounding);
            AdtPaint.Stroke(dl, origin, origin + box, AdtStyling.WithAlpha(AdtStyling.AccentTeal, 0.45f), rounding);

            var barHeight = box.Y * 0.55f;
            var barMin = new Vector2(origin.X - 1f * scale, origin.Y + (box.Y - barHeight) * 0.5f);
            dl.AddRectFilled(barMin, barMin + new Vector2(2.5f * scale, barHeight),
                AdtPaint.Col(AdtStyling.AccentTealSoft), 2f * scale);
        }
        else if (hover > 0.01f)
        {
            AdtPaint.Fill(dl, origin, origin + box, AdtStyling.WithAlpha(AdtStyling.Surface3, hover * (hit.Held ? 1f : 0.7f)), rounding);
        }

        var center = origin + box * 0.5f;
        var tint = active
            ? AdtStyling.AccentTealSoft
            : Vector4.Lerp(AdtStyling.TextSecondary, AdtStyling.TextStrong, hover * 0.55f);
        AdtTextDraw.IconCentered(icon, center, tint);

        if (hit.Hovered && tooltip.Length > 0)
            AdtTooltip.Show(tooltip);

        return hit.Clicked;
    }
}
