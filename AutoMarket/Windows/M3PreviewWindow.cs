using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RebornMaterial;

namespace AutoMarket.Windows;

/// <summary>
/// Prototype only. Rebuilds <see cref="MainWindow"/>'s route-progress dashboard card on top of
/// RebornMaterial (the Material Design 3 ImGui library GatherBuddyReborn's UI is actually built
/// on - not ElliLib, which is a plain widget/utility grab-bag with no theme) instead of the
/// hand-rolled Nord <see cref="Theme"/>. Reads the exact same public Plugin fields the real
/// route card reads - nothing fabricated - so the two can be compared side by side with live
/// data. Toggle with the "M3" button in the main window's header. Not wired to any action:
/// the stop button here is visual only, so clicking it never cancels a real purchase.
/// </summary>
public sealed class M3PreviewWindow : Window, IDisposable
{
    private static readonly string[] RouteSteps = ["Travel", "Search", "Verify", "Buy"];
    private readonly Plugin plugin;

    public M3PreviewWindow(Plugin plugin) : base("AutoMarket - M3 Preview###AutoMarketM3Preview")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        M3.BeginFrame();
        ImGui.TextDisabled("Prototype - RebornMaterial (Material Design 3), not wired to any action.");
        ImGui.Separator();
        ImGui.Spacing();

        if (plugin.QueueTotal <= 0)
        {
            ImGui.TextWrapped("Start a route in the main AutoMarket window to see live data here. " +
                               "Showing a static example in the meantime.");
            DrawCard(stage: "Searching", completed: 3, total: 8, spent: 42_650, skipped: 1, pending: true);
            return;
        }

        DrawCard(plugin.ProgressStage, plugin.QueueCompleted, plugin.QueueTotal, plugin.RouteSpent,
            plugin.QueueSkipped, plugin.IsPurchasePending);
    }

    private static void DrawCard(string stage, int completed, int total, long spent, int skipped, bool pending)
    {
        var fraction = total > 0 ? (float)completed / total : 0f;
        var stepIndex = Math.Min(RouteStepIndex(stage), RouteSteps.Length - 1);
        var scheme = M3.Scheme;

        using var card = M3Card.Begin("##m3-preview-route", "Purchase route", FontAwesomeIcon.ShoppingCart,
            scheme.Primary, M3CardStyle.Elevated, $"Stage: {stage}");

        var ringDiameter = 72f;
        var cursor = ImGui.GetCursorScreenPos();
        M3Widgets.CircularProgress(ringDiameter, fraction, 6f);
        var ringCenter = cursor + new Vector2(ringDiameter / 2f, ringDiameter / 2f);
        var big = $"{completed}/{total}";
        using (ImRaii.PushFont(M3.TitleMedium))
        {
            var bigSize = ImGui.CalcTextSize(big);
            ImGui.GetWindowDrawList().AddText(ringCenter - bigSize / 2f, M3.U32(scheme.OnSurface), big);
        }

        ImGui.SameLine(0, 18f);
        ImGui.BeginGroup();
        M3Badge.Draw(stage.ToUpperInvariant(), scheme.Primary);
        ImGui.Spacing();
        DrawStepper(stepIndex);
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Spacing();

        var avail = ImGui.GetContentRegionAvail().X;
        var chipWidth = (avail - (M3.Space2 * 2f)) / 3f;
        DrawStatChip("Gil spent", $"{spent:N0}", scheme.Primary, chipWidth);
        ImGui.SameLine(0, M3.Space2);
        DrawStatChip("Skipped", $"{skipped:N0}", scheme.Warning, chipWidth);
        ImGui.SameLine(0, M3.Space2);
        DrawStatChip("Remaining", $"{Math.Max(0, total - completed):N0}", scheme.Primary, chipWidth);

        ImGui.Spacing();
        if (pending)
            _ = M3Widgets.Button("##m3-preview-stop", "Stop route", M3ButtonStyle.Danger,
                FontAwesomeIcon.StopCircle, width: avail, tooltip: "Preview only - not wired");
    }

    private static void DrawStepper(int currentIndex)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var scheme = M3.Scheme;
        const float gap = 6f;
        var count = RouteSteps.Length;
        var segWidth = (avail - (gap * (count - 1))) / count;
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        for (var i = 0; i < count; i++)
        {
            var x = pos.X + (i * (segWidth + gap));
            var barColor = i < currentIndex ? scheme.Primary : i == currentIndex ? scheme.Secondary : scheme.SurfaceContainerHighest;
            draw.AddRectFilled(new Vector2(x, pos.Y), new Vector2(x + segWidth, pos.Y + 4), M3.U32(barColor), 2f);
            var label = RouteSteps[i];
            using (ImRaii.PushFont(M3.LabelSmall))
            {
                var textSize = ImGui.CalcTextSize(label);
                draw.AddText(new Vector2(x + (segWidth / 2f) - (textSize.X / 2f), pos.Y + 8),
                    M3.U32(scheme.OnSurfaceVariant), label);
            }
        }

        ImGui.Dummy(new Vector2(avail, 24));
    }

    private static void DrawStatChip(string label, string value, Vector4 accent, float width)
    {
        var height = M3.FitText(44f, 6f);
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        draw.AddRectFilled(pos, pos + new Vector2(width, height), M3.U32(M3.Scheme.SurfaceContainerHigh), M3.ShapeSmall);
        draw.AddCircleFilled(pos + new Vector2(11, 13), 3f, M3.U32(accent));
        using (ImRaii.PushFont(M3.LabelSmall))
            draw.AddText(pos + new Vector2(19, 7), M3.U32(M3.Scheme.OnSurfaceVariant), label.ToUpperInvariant());
        using (ImRaii.PushFont(M3.TitleMedium))
            draw.AddText(pos + new Vector2(10, 23), M3.U32(M3.Scheme.OnSurface), value);
        ImGui.Dummy(new Vector2(width, height));
    }

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
