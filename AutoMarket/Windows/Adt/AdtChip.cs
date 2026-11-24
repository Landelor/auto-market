using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows.Adt;

/// <summary>
/// Prototype only. A small non-interactive status pill, drawn the same way ADT draws its own
/// header status pill and phase chip (Windows/Shell/HeaderBar.cs DrawStatusPill, Windows/Sections/
/// RunningPanel.cs DrawPhaseChip) - plain AdtPaint.Pill + AdtTextDraw, no hit area, unlike
/// <see cref="AdtPillButton"/> which is a real (hover-animated, clickable) button. Call
/// ImGui.SameLine() yourself between pills, same calling convention as the old Theme.Pill.
/// </summary>
internal static class AdtChip
{
    public static void Draw(string label, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var padX = 9f * scale;
        var padY = 3f * scale;

        using (AdtFonts.PushCaption())
        {
            var textSize = AdtTextDraw.Measure(label);
            var min = ImGui.GetCursorScreenPos();
            var size = new Vector2(padX * 2f + textSize.X, textSize.Y + padY * 2f);
            AdtPaint.Pill(dl, min, min + size, AdtStyling.WithAlpha(accent, 0.22f), AdtStyling.WithAlpha(accent, 0.55f));
            AdtTextDraw.At(label, min + new Vector2(padX, padY), accent);
            ImGui.Dummy(size);
        }
    }
}
