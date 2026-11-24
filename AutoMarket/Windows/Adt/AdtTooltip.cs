using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/Tooltip.cs (AGPL-3.0).</summary>
internal static class AdtTooltip
{
    private const float PaddingX = 14f;
    private const float PaddingY = 11f;
    private const float WrapWidth = 320f;

    public static void Show(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var scale = ImGuiHelpers.GlobalScale;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(PaddingX, PaddingY) * scale)
            .Push(ImGuiStyleVar.WindowRounding, AdtStyling.CardRounding * scale)
            .Push(ImGuiStyleVar.PopupRounding, AdtStyling.CardRounding * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f);

        using var color = ImRaii.PushColor(ImGuiCol.PopupBg, AdtStyling.WithAlpha(AdtStyling.Surface2, 0.98f))
            .Push(ImGuiCol.Border, AdtStyling.WithAlpha(AdtStyling.BorderDim, 0.85f))
            .Push(ImGuiCol.Text, AdtStyling.TextSecondary);

        using var font = AdtFonts.PushBody();

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(WrapWidth * scale);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
