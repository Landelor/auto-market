using System.Linq;
using System.Numerics;
using AutoMarket.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    // Matches Auto Daily Tribes' real Windows/Shell/HeaderBar.cs constants exactly (height,
    // padding, button size/gap), not an approximation - the previous port guessed smaller numbers
    // (36px bar, 26px buttons, tight gaps) which read as cramped next to the real thing.
    private const float HeaderHeight = 52f;
    private const float HeaderPadX = 16f;
    private const float HeaderButtonSize = 30f;
    private const float HeaderButtonGap = 6f;

    /// <summary>
    /// Custom title bar built on the ported Auto Daily Tribes system (see Windows/Adt/*): a glass
    /// strip, a single status pill with its own pulsing dot inside it (ADT's HeaderBar
    /// DrawStatusPill - not a separate freestanding dot plus a plain pill, which the previous port
    /// had and didn't match the reference), a one-line summary, then real hover-animated icon
    /// buttons for collapse/close. Collapsing hides everything below it and shrinks the window to
    /// just this bar, so the summary line IS the "minimized" view - same as Auto Daily Tribes.
    /// </summary>
    private void DrawHeaderBar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = HeaderHeight * scale;
        var padX = HeaderPadX * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        AdtPaint.Fill(draw, pos, pos + new Vector2(avail, height), AdtStyling.WithAlpha(AdtStyling.Surface1, 0.55f),
            AdtStyling.WindowRounding * scale, ImDrawFlags.RoundCornersTop);
        AdtPaint.Hairline(draw, new Vector2(pos.X, pos.Y + height - 0.5f), new Vector2(pos.X + avail, pos.Y + height - 0.5f));

        var midY = pos.Y + height * 0.5f;
        var buttonsWidth = HeaderButtonSize * 2f * scale + HeaderButtonGap * scale;
        var buttonsLeft = pos.X + avail - padX - buttonsWidth;
        var x = pos.X + padX;

        const string title = "Auto Market";
        using (AdtFonts.PushHeadline())
        {
            var titleSize = AdtTextDraw.Measure(title);
            if (x + titleSize.X <= buttonsLeft)
            {
                AdtTextDraw.At(title, new Vector2(x, midY - titleSize.Y * 0.5f), AdtStyling.TextStrong);
                x += titleSize.X + 14f * scale;
            }
        }

        var (statusWord, statusColor, statusSoft, summary) = BuildHeaderSummary();
        var pillEnd = DrawStatusPill(draw, statusWord, statusColor, statusSoft, x, buttonsLeft, midY);
        if (pillEnd > x) x = pillEnd + 14f * scale;

        using (AdtFonts.PushCaption())
        {
            var summarySize = AdtTextDraw.Measure(summary);
            AdtTextDraw.At(AdtTextDraw.Truncate(summary, buttonsLeft - 14f * scale - x), new Vector2(x, midY - summarySize.Y * 0.5f), AdtStyling.TextDim);
        }

        DrawHeaderButtons(pos + new Vector2(avail, 0f), midY);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + height + 8f * scale));
    }

    private void DrawHeaderButtons(Vector2 topRight, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = HeaderPadX * scale;
        var buttonSize = HeaderButtonSize * scale;
        var stride = buttonSize + HeaderButtonGap * scale;
        var top = midY - buttonSize * 0.5f;

        ImGui.SetCursorScreenPos(new Vector2(topRight.X - padX - buttonSize, top));
        if (AdtIconButton.Draw(FontAwesomeIcon.Times, "##am_header_close", buttonSize, tooltip: "Close"))
            IsOpen = false;

        ImGui.SetCursorScreenPos(new Vector2(topRight.X - padX - buttonSize - stride, top));
        if (AdtIconButton.Draw(headerCollapsed ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp,
                "##am_header_collapse", buttonSize, tooltip: headerCollapsed ? "Restore" : "Minimize"))
            headerCollapsed = !headerCollapsed;
    }

    /// <summary>
    /// A pill with its own small colored dot inside it (pulsing while a route is running) - the
    /// exact structure of ADT's HeaderBar.DrawStatusPill, not a separate freestanding dot next to
    /// a plain-text pill.
    /// </summary>
    private static float DrawStatusPill(ImDrawListPtr dl, string label, Vector4 accent, Vector4 accentSoft, float x, float rightLimit, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = 10f * scale;
        var dotRadius = 3.5f * scale;

        using (AdtFonts.PushCaption())
        {
            var labelSize = AdtTextDraw.Measure(label);
            var pillHeight = labelSize.Y + 8f * scale;
            var pillMin = new Vector2(x, midY - pillHeight * 0.5f);
            var pillMax = pillMin + new Vector2(padX * 2f + dotRadius * 2f + 6f * scale + labelSize.X, pillHeight);
            if (pillMax.X > rightLimit) return x;

            AdtPaint.Pill(dl, pillMin, pillMax, AdtStyling.WithAlpha(accent, 0.16f), AdtStyling.WithAlpha(accent, 0.45f));

            var dotColor = label == "RUNNING" ? AdtStyling.PulseColor(accent, accentSoft, AdtStyling.PulseMedium) : accent;
            dl.AddCircleFilled(new Vector2(pillMin.X + padX + dotRadius, midY), dotRadius, AdtPaint.Col(dotColor));
            AdtTextDraw.At(label, new Vector2(pillMin.X + padX + dotRadius * 2f + 6f * scale, midY - labelSize.Y * 0.5f), accentSoft);
            return pillMax.X;
        }
    }

    /// <summary>
    /// Status word/colors/summary shown in the header, driven entirely by existing public plugin
    /// state (dependency readiness, route progress, cart contents) - nothing fabricated. Every
    /// state uses a real saturated accent (never a flat TextMuted grey, which reads as a near-
    /// invisible grey-on-grey pill - ADT's own ReadyState.cs never does that either).
    /// </summary>
    private (string Word, Vector4 Accent, Vector4 AccentSoft, string Summary) BuildHeaderSummary()
    {
        if (!plugin.GetPluginState("Lifestream").Loaded)
            return ("SETUP NEEDED", AdtStyling.AccentRose, AdtStyling.Lighten(AdtStyling.AccentRose, 0.2f), "Lifestream is required but not loaded");

        if (plugin.IsPurchasePending)
            return ("RUNNING", AdtStyling.AccentTeal, AdtStyling.AccentTealSoft, $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks \u00b7 {plugin.RouteSpent:N0} gil \u00b7 {plugin.ProgressStage}");

        if (plugin.QueueTotal > 0)
            return ("FINISHED", AdtStyling.AccentMint, AdtStyling.Lighten(AdtStyling.AccentMint, 0.2f), $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks \u00b7 {plugin.RouteSpent:N0} gil spent");

        if (cart.Count > 0)
        {
            var totalGil = cart.SelectMany(order => order.PlannedQuotes).Sum(quote => quote.Total);
            return ("READY", AdtStyling.AccentMint, AdtStyling.Lighten(AdtStyling.AccentMint, 0.2f), $"{cart.Count:N0} items queued \u00b7 {totalGil:N0} gil estimated");
        }

        return ("IDLE", AdtStyling.AccentSlate, AdtStyling.Lighten(AdtStyling.AccentSlate, 0.2f), "Cart is empty");
    }
}
