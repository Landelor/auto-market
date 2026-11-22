using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows;

/// <summary>
/// Nord-palette dark theme (colors sampled from the "Auto Daily Tribes" plugin's dashboard UI)
/// plus small ImDrawList-based widgets (pills, stat chips, a progress ring, a step tracker, and a
/// full-width colored action bar) used to give AutoMarket a similar card-dashboard look.
/// </summary>
internal static class Theme
{
    internal static readonly Vector4 Bg = Rgb(0x2E, 0x34, 0x40);
    internal static readonly Vector4 Panel = Rgb(0x3B, 0x42, 0x52);
    internal static readonly Vector4 Card = Rgb(0x43, 0x4C, 0x5E);
    internal static readonly Vector4 Track = Rgb(0x4C, 0x56, 0x6A);
    internal static readonly Vector4 Text = Rgb(0xEC, 0xEF, 0xF4);
    internal static readonly Vector4 Muted = Rgb(0xD8, 0xDE, 0xE9);
    internal static readonly Vector4 Teal = Rgb(0x88, 0xC0, 0xD0);
    internal static readonly Vector4 Teal2 = Rgb(0x8F, 0xBC, 0xBB);
    internal static readonly Vector4 Rose = Rgb(0xBF, 0x61, 0x6A);
    internal static readonly Vector4 Green = Rgb(0xA3, 0xBE, 0x8C);
    internal static readonly Vector4 Amber = Rgb(0xEB, 0xCB, 0x8B);

    private static readonly (ImGuiCol, Vector4)[] Colors =
    [
        (ImGuiCol.WindowBg, Bg),
        (ImGuiCol.ChildBg, Bg),
        (ImGuiCol.PopupBg, Panel),
        (ImGuiCol.FrameBg, Panel),
        (ImGuiCol.FrameBgHovered, Card),
        (ImGuiCol.FrameBgActive, Card),
        (ImGuiCol.TitleBg, Panel),
        (ImGuiCol.TitleBgActive, Panel),
        (ImGuiCol.TitleBgCollapsed, Panel),
        (ImGuiCol.Header, Card),
        (ImGuiCol.HeaderHovered, WithAlpha(Teal, 0.35f)),
        (ImGuiCol.HeaderActive, WithAlpha(Teal, 0.45f)),
        (ImGuiCol.Button, Card),
        (ImGuiCol.ButtonHovered, WithAlpha(Teal, 0.5f)),
        (ImGuiCol.ButtonActive, Teal),
        (ImGuiCol.Separator, Track),
        (ImGuiCol.SeparatorHovered, Teal),
        (ImGuiCol.Border, Track),
        (ImGuiCol.Text, Text),
        (ImGuiCol.TextDisabled, Muted),
        (ImGuiCol.ScrollbarBg, Panel),
        (ImGuiCol.ScrollbarGrab, Track),
        (ImGuiCol.ScrollbarGrabHovered, Teal),
        (ImGuiCol.ScrollbarGrabActive, Teal),
        (ImGuiCol.TableHeaderBg, Panel),
        (ImGuiCol.TableBorderStrong, Track),
        (ImGuiCol.TableBorderLight, Track),
        (ImGuiCol.TableRowBg, Bg),
        (ImGuiCol.TableRowBgAlt, Panel),
        (ImGuiCol.CheckMark, Teal),
        (ImGuiCol.SliderGrab, Teal),
        (ImGuiCol.SliderGrabActive, Teal),
        (ImGuiCol.PlotHistogram, Teal),
    ];

    /// <summary>Pushes the Nord color/rounding theme. Must be paired with exactly one <see cref="Pop"/>.</summary>
    internal static void Push()
    {
        foreach (var (col, val) in Colors)
            ImGui.PushStyleColor(col, val);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
    }

    internal static void Pop()
    {
        ImGui.PopStyleVar(7);
        ImGui.PopStyleColor(Colors.Length);
    }

    /// <summary>A small rounded pill badge, e.g. status text. Call ImGui.SameLine() yourself between pills.</summary>
    internal static void Pill(string label, Vector4 accent)
    {
        var draw = ImGui.GetWindowDrawList();
        var pad = new Vector2(10, 3);
        var textSize = ImGui.CalcTextSize(label);
        var pos = ImGui.GetCursorScreenPos();
        var size = textSize + pad * 2;
        draw.AddRectFilled(pos, pos + size, ImGui.ColorConvertFloat4ToU32(WithAlpha(accent, 0.18f)), size.Y / 2f);
        draw.AddText(pos + pad, ImGui.ColorConvertFloat4ToU32(accent), label);
        ImGui.Dummy(size);
    }

    /// <summary>A small card: colored dot + uppercase label + bold value, sized to fit <paramref name="width"/>.</summary>
    internal static void StatChip(string label, string value, Vector4 dotColor, float width)
    {
        const float height = 44f;
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        draw.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(Card), 8f);
        draw.AddCircleFilled(pos + new Vector2(11, 13), 3f, ImGui.ColorConvertFloat4ToU32(dotColor));
        draw.AddText(pos + new Vector2(19, 7), ImGui.ColorConvertFloat4ToU32(Muted), label.ToUpperInvariant());
        draw.AddText(pos + new Vector2(10, 23), ImGui.ColorConvertFloat4ToU32(Text), value);
        ImGui.Dummy(new Vector2(width, height));
    }

    /// <summary>A circular progress ring with a big centered number and a small caption under it.</summary>
    internal static void ProgressRing(float diameter, float fraction, string bigText, string smallText)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var center = pos + new Vector2(diameter / 2f, diameter / 2f);
        var radius = diameter / 2f - 6f;
        const float thickness = 8f;
        const int segments = 64;

        draw.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(Track), segments, thickness);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped > 0.001f)
        {
            const float startAngle = -MathF.PI / 2f;
            var endAngle = startAngle + 2f * MathF.PI * clamped;
            draw.PathArcTo(center, radius, startAngle, endAngle, segments);
            draw.PathStroke(ImGui.ColorConvertFloat4ToU32(Teal), ImDrawFlags.None, thickness);
        }

        var bigSize = ImGui.CalcTextSize(bigText);
        draw.AddText(center - bigSize / 2f - new Vector2(0, 8), ImGui.ColorConvertFloat4ToU32(Text), bigText);
        var smallSize = ImGui.CalcTextSize(smallText);
        draw.AddText(center - smallSize / 2f + new Vector2(0, 10), ImGui.ColorConvertFloat4ToU32(Muted), smallText);
        ImGui.Dummy(new Vector2(diameter, diameter));
    }

    /// <summary>A horizontal row of segment bars with a label under each, highlighting steps before/at <paramref name="currentIndex"/>.</summary>
    internal static void Stepper(string[] labels, int currentIndex)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        const float gap = 6f;
        var count = labels.Length;
        var segWidth = (avail - gap * (count - 1)) / count;
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        for (var i = 0; i < count; i++)
        {
            var x = pos.X + i * (segWidth + gap);
            var barColor = i < currentIndex ? Teal : i == currentIndex ? Teal2 : Track;
            draw.AddRectFilled(new Vector2(x, pos.Y), new Vector2(x + segWidth, pos.Y + 4), ImGui.ColorConvertFloat4ToU32(barColor), 2f);
            var label = labels[i];
            var textSize = ImGui.CalcTextSize(label);
            draw.AddText(new Vector2(x + segWidth / 2f - textSize.X / 2f, pos.Y + 8), ImGui.ColorConvertFloat4ToU32(Muted), label);
        }

        ImGui.Dummy(new Vector2(avail, 24));
    }

    /// <summary>
    /// A full-width colored bar with left/right text, acting as a single clickable action (like
    /// Auto Daily Tribes' bottom "Stop" bar). Returns true the frame it is clicked.
    /// </summary>
    internal static bool ActionBar(string leftLabel, string rightLabel, Vector4 color)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        const float height = 36f;
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("##automarket-actionbar", new Vector2(avail, height));
        var bg = ImGui.IsItemHovered() ? Lighten(color, 0.08f) : color;

        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(pos, pos + new Vector2(avail, height), ImGui.ColorConvertFloat4ToU32(bg), 8f);
        var textColor = ImGui.ColorConvertFloat4ToU32(Bg);
        var leftSize = ImGui.CalcTextSize(leftLabel);
        draw.AddText(pos + new Vector2(14, (height - leftSize.Y) / 2f), textColor, leftLabel);
        var rightSize = ImGui.CalcTextSize(rightLabel);
        draw.AddText(pos + new Vector2(avail - 14 - rightSize.X, (height - rightSize.Y) / 2f), textColor, rightLabel);
        return clicked;
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };

    private static Vector4 Lighten(Vector4 color, float amount) => new(
        Math.Clamp(color.X + amount, 0, 1),
        Math.Clamp(color.Y + amount, 0, 1),
        Math.Clamp(color.Z + amount, 0, 1),
        color.W);

    private static Vector4 Rgb(byte r, byte g, byte b, byte a = 255) => new(r / 255f, g / 255f, b / 255f, a / 255f);
}
