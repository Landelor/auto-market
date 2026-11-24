using System;
using System.Numerics;
using AutoMarket.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    private static readonly string[] RouteSteps = ["Travel", "Search", "Verify", "Buy"];

    /// <summary>
    /// The route-progress dashboard card, rebuilt on the ported Auto Daily Tribes hero-card layout
    /// (see Windows/Adt/* and AutoDailyTribes' Windows/Sections/RunningPanel.cs DrawHeroCard): a
    /// glowing/sweeping progress ring (stacks done/total), a pulsing phase chip, a live status
    /// line, an animated 4-step Travel/Search/Verify/Buy tracker, glass stat tiles (gil spent,
    /// skipped, remaining - all backed by existing Plugin.Purchases.cs counters), and a hero
    /// "stop route" button. Shown whenever a route has been started this session.
    /// </summary>
    private void DrawRouteCard()
    {
        if (plugin.QueueTotal <= 0)
            return;

        var fraction = plugin.QueueTotal > 0 ? (float)plugin.QueueCompleted / plugin.QueueTotal : 0f;
        var stepIndex = Math.Min(RouteStepIndex(plugin.ProgressStage), RouteSteps.Length - 1);
        var (accent, accentSoft) = PhasePalette(stepIndex);

        AdtStyling.VSpace(4f);
        DrawHeaderStrip(plugin.ProgressStage, plugin.RouteSpent, accent, accentSoft);
        AdtStyling.VSpace(6f);
        DrawHeroCard(fraction, stepIndex, accent, accentSoft);

        AdtStyling.VSpace(10f);
        DrawStatTiles();

        AdtStyling.VSpace(10f);
        if (plugin.IsPurchasePending
            && AdtHeroButton.Draw(FontAwesomeIcon.Stop, "Stop route", $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks", AdtStyling.AccentRose, true))
            plugin.CancelPurchase();
    }

    private static void DrawHeaderStrip(string stage, long spent, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var lineHeight = ImGui.GetTextLineHeight();
        var midY = origin.Y + lineHeight * 0.5f;

        var radius = 4f * scale;
        AdtPaint.Dot(dl, new Vector2(origin.X + radius + 3f * scale, midY), radius, AdtStyling.PulseColor(accent, accentSoft, AdtStyling.PulseMedium));

        var statusSize = AdtTextDraw.SmallCapsSize("Running");
        AdtTextDraw.SmallCaps("Running", new Vector2(origin.X + radius * 2f + 12f * scale, midY - statusSize.Y * 0.5f), AdtStyling.TextSecondary);

        var footer = $"{spent:N0} gil spent";
        using (AdtFonts.PushCaption())
        {
            var footerSize = AdtTextDraw.Measure(footer);
            AdtTextDraw.At(footer, new Vector2(origin.X + avail - footerSize.X, midY - footerSize.Y * 0.5f), AdtStyling.TextMuted);
        }

        ImGui.Dummy(new Vector2(avail, lineHeight));
    }

    private void DrawHeroCard(float fraction, int stepIndex, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        const float height = 176f;
        const float padX = 18f;
        const float ringInset = 22f;
        const float columnGap = 20f;

        var size = new Vector2(ImGui.GetContentRegionAvail().X, height * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var dl = ImGui.GetWindowDrawList();
        var rounding = AdtStyling.PanelRounding * scale;

        AdtPaint.Glass(dl, origin, end, rounding, accent, 0.10f, 0f, elevated: true);
        var border = AdtStyling.PulseColor(AdtStyling.WithAlpha(accent, 0.5f), accentSoft, AdtStyling.PulseMedium);
        AdtPaint.Stroke(dl, origin, end, border, rounding, 1.6f);

        var pad = padX * scale;
        var ringRadius = size.Y * 0.5f - ringInset * scale;
        var ringCenter = new Vector2(origin.X + pad + ringRadius, origin.Y + size.Y * 0.5f);
        DrawRing(ringCenter, ringRadius, fraction, accent, accentSoft);

        var columnX = ringCenter.X + ringRadius + columnGap * scale;
        var columnRight = end.X - pad;
        var columnWidth = columnRight - columnX;
        var y = origin.Y + 16f * scale;

        y += DrawPhaseChip(columnX, y, plugin.ProgressStage, accent, accentSoft) + 10f * scale;
        y += DrawStatusLine(dl, plugin.PurchaseStatus, accentSoft, new Vector2(columnX, y), columnWidth) + 10f * scale;

        float captionHeight;
        using (AdtFonts.PushCaption())
            captionHeight = ImGui.GetTextLineHeight();
        var stepperHeight = 3f * scale + 5f * scale + captionHeight;
        DrawStepper(dl, stepIndex, columnX, end.Y - 16f * scale - stepperHeight, columnWidth);

        ImGui.Dummy(size);
    }

    private static void DrawRing(Vector2 center, float radius, float fraction, Vector4 accent, Vector4 accentSoft)
    {
        var thickness = 6f * ImGuiHelpers.GlobalScale;
        AdtProgressRing.Glow(center, radius, accent, 0.35f + 0.30f * AdtStyling.Pulse(AdtStyling.PulseBreath));
        AdtProgressRing.Track(center, radius, thickness, AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.7f));
        var eased = AdtMotion.Approach(AdtMotion.Key("##am_route_ring"), fraction, 6f);
        AdtProgressRing.Fill(center, radius, thickness, eased, accent);
        AdtProgressRing.Sweep(center, radius, thickness * 0.72f, accentSoft, AdtStyling.PulseOrbit, MathF.PI * 0.5f, 1f);
    }

    private static float DrawPhaseChip(float x, float y, string text, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var padX = 9f * scale;
        var padY = 3f * scale;

        using (AdtFonts.PushCaption())
        {
            var label = text.ToUpperInvariant();
            var textSize = AdtTextDraw.Measure(label);
            var chipMin = new Vector2(x, y);
            var chipMax = chipMin + new Vector2(padX * 2f + textSize.X, textSize.Y + padY * 2f);
            AdtPaint.Pill(dl, chipMin, chipMax, AdtStyling.WithAlpha(accent, 0.28f), AdtStyling.WithAlpha(accent, 0.65f));
            AdtTextDraw.At(label, new Vector2(x + padX, y + padY), accentSoft);
            return chipMax.Y - chipMin.Y;
        }
    }

    private static float DrawStatusLine(ImDrawListPtr dl, string status, Vector4 accent, Vector2 position, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dotRadius = 3.5f * scale;
        var midY = position.Y + ImGui.GetTextLineHeight() * 0.5f;
        var alpha = 0.4f + 0.6f * AdtStyling.Pulse(AdtStyling.PulseBreath);
        dl.AddCircleFilled(new Vector2(position.X + dotRadius, midY), dotRadius, AdtPaint.Col(AdtStyling.WithAlpha(accent, alpha)));

        var textX = position.X + dotRadius * 2f + 8f * scale;
        var text = string.IsNullOrWhiteSpace(status) ? "Working..." : status;
        AdtTextDraw.At(AdtTextDraw.Truncate(text, width - (textX - position.X)), new Vector2(textX, position.Y), AdtStyling.TextSecondary);
        return ImGui.GetTextLineHeight();
    }

    private static void DrawStepper(ImDrawListPtr dl, int rank, float x, float y, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        const float gap = 8f;
        var g = gap * scale;
        var segmentWidth = (width - g * (RouteSteps.Length - 1)) / RouteSteps.Length;
        var barHeight = 3f * scale;
        var labelY = y + barHeight + 5f * scale;

        using var caption = AdtFonts.PushCaption();
        for (var index = 0; index < RouteSteps.Length; index++)
        {
            var segmentX = x + index * (segmentWidth + g);
            var bar = index < rank ? AdtStyling.AccentMint
                : index == rank ? AdtStyling.WithAlpha(AdtStyling.AccentTeal, 0.45f + 0.55f * AdtStyling.Pulse(AdtStyling.PulseMedium))
                : AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.6f);
            AdtPaint.Fill(dl, new Vector2(segmentX, y), new Vector2(segmentX + segmentWidth, y + barHeight), bar, barHeight * 0.5f);

            var text = index < rank ? AdtStyling.TextDim : index == rank ? AdtStyling.TextStrong : AdtStyling.TextMuted;
            AdtTextDraw.At(RouteSteps[index], new Vector2(segmentX, labelY), text);
        }
    }

    private void DrawStatTiles()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var avail = ImGui.GetContentRegionAvail().X;
        var gap = 8f * scale;
        var tileWidth = (avail - gap * 2f) / 3f;

        AdtStatTile.Draw("Gil spent", $"{plugin.RouteSpent:N0}", null, AdtStyling.AccentTeal, tileWidth);
        ImGui.SameLine(0, gap);
        AdtStatTile.Draw("Skipped", $"{plugin.QueueSkipped:N0}", null, AdtStyling.AccentAmber, tileWidth);
        ImGui.SameLine(0, gap);
        AdtStatTile.Draw("Remaining", $"{Math.Max(0, plugin.QueueTotal - plugin.QueueCompleted):N0}", null, AdtStyling.AccentTeal, tileWidth);
    }

    private static (Vector4 Accent, Vector4 AccentSoft) PhasePalette(int stepIndex) => stepIndex switch
    {
        0 => (AdtStyling.AccentViolet, AdtStyling.Lighten(AdtStyling.AccentViolet, 0.25f)),
        1 => (AdtStyling.AccentTeal, AdtStyling.AccentTealSoft),
        2 => (AdtStyling.AccentAmber, AdtStyling.AccentAmberSoft),
        _ => (AdtStyling.AccentMint, AdtStyling.Lighten(AdtStyling.AccentMint, 0.25f)),
    };

    /// <summary>
    /// Buckets Plugin.Purchases.cs' free-text ProgressStage into the 4-step tracker. This mapping
    /// is a UI simplification over the real stage strings, not a new tracked field.
    /// </summary>
    private static int RouteStepIndex(string stage) => stage switch
    {
        _ when stage.StartsWith("Traveling", StringComparison.OrdinalIgnoreCase) => 0,
        "Opening market board" or "Searching" or "Trying next search result" => 1,
        "Verifying live listings" or "Retrying live listings" => 2,
        "Waiting for purchase confirmation" => 3,
        "Completed" or "Cancelled" or "Stopped" or "Skipped" => 3,
        _ => 0,
    };
}
