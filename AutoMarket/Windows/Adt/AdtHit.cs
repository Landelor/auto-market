using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/Hit.cs (AGPL-3.0): a
/// clickable invisible-button hit area that reports hover/press state for hover-animated widgets.</summary>
internal static class AdtHit
{
    public readonly record struct Result(bool Clicked, bool Hovered, bool Held);

    public static Result Area(string id, Vector2 size, bool enabled = true, bool handCursor = true)
    {
        size = new Vector2(MathF.Max(1f, size.X), MathF.Max(1f, size.Y));
        if (!enabled)
        {
            ImGui.Dummy(size);
            return default;
        }

        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        if (hovered && handCursor) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return new Result(clicked, hovered, ImGui.IsItemActive());
    }
}
