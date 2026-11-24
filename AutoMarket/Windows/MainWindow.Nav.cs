using AutoMarket.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    /// <summary>
    /// The left-hand tab rail, ADT-style: one icon per top-level section instead of cramming
    /// dashboard/search/import into a single long scroll. Dashboard uses a gauge (route progress
    /// + cart), Search a magnifier (the single-item lookup), Import a "file in" glyph (the
    /// Artisan import flow) - all three already used elsewhere in Dalamud plugins for these
    /// exact concepts, so no custom art asset is needed.
    /// </summary>
    private void DrawNavRail()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = 40f * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size) * 0.5f);

        DrawNavItem(MainTab.Dashboard, FontAwesomeIcon.TachometerAlt, "Dashboard\nRoute progress, stats, and the shopping cart.", size);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size) * 0.5f);
        DrawNavItem(MainTab.Search, FontAwesomeIcon.Search, "Search\nLook up a single item and its listings.", size);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - size) * 0.5f);
        DrawNavItem(MainTab.Import, FontAwesomeIcon.FileImport, "Import\nQueue a whole ingredient list from Artisan.", size);
    }

    private void DrawNavItem(MainTab tab, FontAwesomeIcon icon, string tooltip, float size)
    {
        if (AdtNavRail.Item(icon, $"##am_nav_{tab}", size, activeTab == tab, tooltip))
            activeTab = tab;
        AdtStyling.VSpace(8f);
    }
}
