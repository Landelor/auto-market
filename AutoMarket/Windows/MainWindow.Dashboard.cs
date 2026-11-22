using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    private static readonly string[] RouteSteps = ["Travel", "Search", "Verify", "Buy"];

    /// <summary>
    /// The route-progress "dashboard" card: a progress ring (stacks done/total), the current
    /// status line, a 4-step tracker, three stat chips (all backed by Plugin.Purchases.cs'
    /// existing public counters - nothing fabricated), and a bottom action bar to cancel.
    /// Mirrors Auto Daily Tribes' single-task dashboard card. Shown whenever a route has been
    /// started this session (QueueTotal > 0), including right after it finishes.
    /// </summary>
    private void DrawRouteCard()
    {
        if (plugin.QueueTotal <= 0)
            return;

        ImGui.Spacing();
        var stepIndex = RouteStepIndex(plugin.ProgressStage);
        var fraction = plugin.QueueTotal > 0 ? (float)plugin.QueueCompleted / plugin.QueueTotal : 0f;

        var cardWidth = ImGui.GetContentRegionAvail().X;
        const float cardHeight = 110f;
        var cardPos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(cardPos, cardPos + new Vector2(cardWidth, cardHeight), ImGui.ColorConvertFloat4ToU32(Theme.Card), 10f);

        ImGui.SetCursorScreenPos(cardPos + new Vector2(16, 14));
        Theme.ProgressRing(80, fraction, $"{plugin.QueueCompleted}/{plugin.QueueTotal}", "stacks");

        ImGui.SetCursorScreenPos(cardPos + new Vector2(112, 14));
        ImGui.BeginGroup();
        Theme.Pill(plugin.ProgressStage.ToUpperInvariant(), Theme.Teal);
        ImGui.PushTextWrapPos(cardPos.X + cardWidth - 16);
        ImGui.TextWrapped(plugin.PurchaseStatus);
        ImGui.PopTextWrapPos();
        ImGui.Dummy(new Vector2(1, 6));
        Theme.Stepper(RouteSteps, Math.Min(stepIndex, RouteSteps.Length - 1));
        ImGui.EndGroup();

        ImGui.SetCursorScreenPos(cardPos + new Vector2(0, cardHeight + 10));

        var chipWidth = (cardWidth - 16f) / 3f;
        Theme.StatChip("Gil spent", $"{plugin.RouteSpent:N0}", Theme.Teal, chipWidth);
        ImGui.SameLine(0, 8);
        Theme.StatChip("Skipped", $"{plugin.QueueSkipped:N0}", Theme.Amber, chipWidth);
        ImGui.SameLine(0, 8);
        Theme.StatChip("Remaining", $"{Math.Max(0, plugin.QueueTotal - plugin.QueueCompleted):N0}", Theme.Teal, chipWidth);

        ImGui.Spacing();
        if (plugin.IsPurchasePending
            && Theme.ActionBar("STOP ROUTE", $"{plugin.QueueCompleted}/{plugin.QueueTotal} stacks", Theme.Rose))
            plugin.CancelPurchase();
    }

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
