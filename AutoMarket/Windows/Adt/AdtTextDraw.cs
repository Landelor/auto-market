using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoMarket.Windows.Adt;

/// <summary>
/// Prototype only. Port of Auto Daily Tribes' Windows/TextDraw.cs (AGPL-3.0): drawing text straight
/// through the draw list at explicit positions (so layout math is explicit instead of relying on
/// ImGui's cursor flow) plus a binary-search ellipsis truncator. Localization-aware helpers
/// (Loc.Upper, catalog-driven strings) are dropped since AutoMarket has no translation catalog.
/// </summary>
internal static class AdtTextDraw
{
    private const string Ellipsis = "…";

    public static Vector2 Measure(string text) => ImGui.CalcTextSize(text);

    public static void At(string text, Vector2 pos, Vector4 color)
        => ImGui.GetWindowDrawList().AddText(pos, AdtPaint.Col(color), text);

    public static void Right(string text, float rightX, float y, Vector4 color)
        => At(text, new Vector2(rightX - Measure(text).X, y), color);

    public static void Center(string text, float centerX, float y, Vector4 color)
        => At(text, new Vector2(centerX - Measure(text).X * 0.5f, y), color);

    public static Vector2 IconSize(FontAwesomeIcon icon)
    {
        using (AdtFonts.PushIcon())
            return Measure(icon.ToIconString());
    }

    public static void IconCentered(FontAwesomeIcon icon, Vector2 center, Vector4 color)
    {
        using (AdtFonts.PushIcon())
        {
            var glyph = icon.ToIconString();
            var size = Measure(glyph);
            At(glyph, center - size * 0.5f, color);
        }
    }

    // A line that overflows its slot overflows on every frame it is drawn, so truncation is cheap
    // to just recompute (no cross-frame cache - fine at AutoMarket's much smaller row counts).
    public static string Truncate(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            return string.Empty;

        if (Measure(text).X <= maxWidth)
            return text;

        var budget = maxWidth - Measure(Ellipsis).X;
        if (budget <= 0f)
            return Ellipsis;

        var low = 1;
        var high = text.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text.AsSpan(0, middle)).X <= budget)
                low = middle;
            else
                high = middle - 1;
        }

        return string.Concat(text.AsSpan(0, low), Ellipsis);
    }

    public static void SmallCaps(string label, Vector2 pos, Vector4 color)
    {
        using (AdtFonts.PushCaption())
            At(label.ToUpperInvariant(), pos, color);
    }

    public static Vector2 SmallCapsSize(string label)
    {
        using (AdtFonts.PushCaption())
            return Measure(label.ToUpperInvariant());
    }

    public static void SectionTitle(string label, Vector2 pos, Vector4 color)
    {
        using (AdtFonts.PushHeadline())
            At(label, pos, color);
    }
}
