using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    /// <summary>
    /// A custom title bar (the native ImGui title bar can't host a colored pill or avatar dot) that
    /// mirrors Auto Daily Tribes' look: icon dot, title, a status pill, a one-line summary, then a
    /// collapse chevron and a close button. Collapsing hides everything below it and shrinks the
    /// window to just this bar - so the summary line IS what you see when "minimized", same as
    /// Auto Daily Tribes.
    /// </summary>
    private void DrawHeaderBar()
    {
        const float height = 34f;
        var avail = ImGui.GetContentRegionAvail().X;
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        draw.AddRectFilled(pos, pos + new Vector2(avail, height), ImGui.ColorConvertFloat4ToU32(Theme.Panel), 10f, ImDrawFlags.RoundCornersTop);

        var (statusWord, statusColor, summary) = BuildHeaderSummary();
        var x = pos.X + 12f;
        const float midY = height / 2f;

        draw.AddCircleFilled(new Vector2(x + 4, pos.Y + midY), 4f, ImGui.ColorConvertFloat4ToU32(statusColor));
        x += 16f;

        const string title = "Auto Market";
        var titleSize = ImGui.CalcTextSize(title);
        draw.AddText(new Vector2(x, pos.Y + midY - titleSize.Y / 2f), ImGui.ColorConvertFloat4ToU32(Theme.Text), title);
        x += titleSize.X + 10f;

        var pillPad = new Vector2(9, 3);
        var pillTextSize = ImGui.CalcTextSize(statusWord);
        var pillSize = pillTextSize + pillPad * 2;
        var pillPos = new Vector2(x, pos.Y + midY - pillSize.Y / 2f);
        draw.AddRectFilled(pillPos, pillPos + pillSize, ImGui.ColorConvertFloat4ToU32(statusColor with { W = 0.2f }), pillSize.Y / 2f);
        draw.AddText(pillPos + pillPad, ImGui.ColorConvertFloat4ToU32(statusColor), statusWord);
        x += pillSize.X + 10f;

        var summarySize = ImGui.CalcTextSize(summary);
        draw.AddText(new Vector2(x, pos.Y + midY - summarySize.Y / 2f), ImGui.ColorConvertFloat4ToU32(Theme.Muted), summary);

        const float buttonSize = 20f;
        var closeX = pos.X + avail - 10f - buttonSize;
        var chevronX = closeX - 6f - buttonSize;
        DrawHeaderButton(chevronX, pos.Y + midY - buttonSize / 2f, buttonSize, headerCollapsed ? "v" : "^", () => headerCollapsed = !headerCollapsed);
        DrawHeaderButton(closeX, pos.Y + midY - buttonSize / 2f, buttonSize, "x", () => IsOpen = false);

        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + height + 8f));
    }

    private static void DrawHeaderButton(float x, float y, float size, string glyph, Action onClick)
    {
        var pos = new Vector2(x, y);
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton($"##automarket-header-{glyph}-{x}", new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList();
        if (hovered)
            draw.AddRectFilled(pos, pos + new Vector2(size, size), ImGui.ColorConvertFloat4ToU32(Theme.Card), 5f);

        var glyphSize = ImGui.CalcTextSize(glyph);
        draw.AddText(pos + (new Vector2(size, size) - glyphSize) / 2f, ImGui.ColorConvertFloat4ToU32(Theme.Muted), glyph);

        if (clicked)
            onClick();
    }

    /// <summary>
    /// Status word/color/summary shown in the header, driven entirely by existing public plugin
    /// state (dependency readiness, route progress, cart contents) - nothing fabricated.
    /// </summary>
    private (string Word, Vector4 Color, string Summary) BuildHeaderSummary()
    {
        if (!plugin.GetPluginState("Lifestream").Loaded)
            return ("SETUP NEEDED", Theme.Rose, "Lifestream is required but not loaded");

        if (plugin.IsPurchasePending)
            return ("RUNNING", Theme.Teal, $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks \u00b7 {plugin.RouteSpent:N0} gil \u00b7 {plugin.ProgressStage}");

        if (plugin.QueueTotal > 0)
            return ("FINISHED", Theme.Teal2, $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks \u00b7 {plugin.RouteSpent:N0} gil spent");

        if (cart.Count > 0)
        {
            var totalGil = cart.SelectMany(order => order.PlannedQuotes).Sum(quote => quote.Total);
            return ("READY", Theme.Teal, $"{cart.Count:N0} items queued \u00b7 {totalGil:N0} gil estimated");
        }

        return ("IDLE", Theme.Muted, "Cart is empty");
    }
}
