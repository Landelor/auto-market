using System.Linq;
using System.Numerics;
using AutoMarket.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    /// <summary>
    /// Custom title bar built on the ported Auto Daily Tribes system (see Windows/Adt/*): a glass
    /// strip, a pulsing status dot + pill (same drawing style as ADT's own HeaderBar
    /// DrawStatusPill), a one-line summary, then real hover-animated icon buttons for
    /// collapse/close instead of the old plain glyph-in-a-box. Collapsing hides everything below
    /// it and shrinks the window to just this bar, so the summary line IS the "minimized" view -
    /// same as Auto Daily Tribes.
    /// </summary>
    private void DrawHeaderBar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 36f * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        AdtPaint.Fill(draw, pos, pos + new Vector2(avail, height), AdtStyling.WithAlpha(AdtStyling.Surface1, 0.55f),
            AdtStyling.WindowRounding * scale, ImDrawFlags.RoundCornersTop);
        AdtPaint.Hairline(draw, new Vector2(pos.X, pos.Y + height - 0.5f), new Vector2(pos.X + avail, pos.Y + height - 0.5f));

        var (statusWord, statusColor, summary) = BuildHeaderSummary();
        var x = pos.X + 14f * scale;
        var midY = pos.Y + height / 2f;

        var dotColor = statusWord == "RUNNING"
            ? AdtStyling.PulseColor(statusColor, AdtStyling.Lighten(statusColor, 0.3f), AdtStyling.PulseMedium)
            : statusColor;
        AdtPaint.Dot(draw, new Vector2(x + 4f * scale, midY), 4f * scale, dotColor);
        x += 16f * scale;

        const string title = "Auto Market";
        using (AdtFonts.PushHeadline())
        {
            var titleSize = AdtTextDraw.Measure(title);
            AdtTextDraw.At(title, new Vector2(x, midY - titleSize.Y / 2f), AdtStyling.TextStrong);
            x += titleSize.X + 10f * scale;
        }

        var cursorBefore = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(x, midY - 10f * scale));
        AdtChip.Draw(statusWord, statusColor);
        var pillEnd = ImGui.GetItemRectMax().X;
        ImGui.SetCursorScreenPos(cursorBefore);
        x = pillEnd + 10f * scale;

        var buttonSize = 26f * scale;
        var closeX = pos.X + avail - 10f * scale - buttonSize;
        var chevronX = closeX - 4f * scale - buttonSize;

        using (AdtFonts.PushCaption())
        {
            var summarySize = AdtTextDraw.Measure(summary);
            var maxX = chevronX - 10f * scale;
            AdtTextDraw.At(AdtTextDraw.Truncate(summary, maxX - x), new Vector2(x, midY - summarySize.Y / 2f), AdtStyling.TextDim);
        }

        ImGui.SetCursorScreenPos(new Vector2(chevronX, midY - buttonSize / 2f));
        if (AdtIconButton.Draw(headerCollapsed ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp,
                "##am_header_collapse", buttonSize, tooltip: headerCollapsed ? "Restore" : "Minimize"))
            headerCollapsed = !headerCollapsed;

        ImGui.SetCursorScreenPos(new Vector2(closeX, midY - buttonSize / 2f));
        if (AdtIconButton.Draw(FontAwesomeIcon.Times, "##am_header_close", buttonSize, tooltip: "Close"))
            IsOpen = false;

        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + height + 8f * scale));
    }

    /// <summary>
    /// Status word/color/summary shown in the header, driven entirely by existing public plugin
    /// state (dependency readiness, route progress, cart contents) - nothing fabricated.
    /// </summary>
    private (string Word, Vector4 Color, string Summary) BuildHeaderSummary()
    {
        if (!plugin.GetPluginState("Lifestream").Loaded)
            return ("SETUP NEEDED", AdtStyling.AccentRose, "Lifestream is required but not loaded");

        if (plugin.IsPurchasePending)
            return ("RUNNING", AdtStyling.AccentTeal, $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks \u00b7 {plugin.RouteSpent:N0} gil \u00b7 {plugin.ProgressStage}");

        if (plugin.QueueTotal > 0)
            return ("FINISHED", AdtStyling.AccentMint, $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks \u00b7 {plugin.RouteSpent:N0} gil spent");

        if (cart.Count > 0)
        {
            var totalGil = cart.SelectMany(order => order.PlannedQuotes).Sum(quote => quote.Total);
            return ("READY", AdtStyling.AccentTeal, $"{cart.Count:N0} items queued \u00b7 {totalGil:N0} gil estimated");
        }

        return ("IDLE", AdtStyling.TextMuted, "Cart is empty");
    }
}
