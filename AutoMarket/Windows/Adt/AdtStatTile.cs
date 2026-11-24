using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoMarket.Windows.Adt;

/// <summary>Prototype only. Port of Auto Daily Tribes' Windows/Components/StatTile.cs (AGPL-3.0).</summary>
internal static class AdtStatTile
{
    private const float PadX = 13f;
    private const float PadY = 10f;
    private const float Height = 66f;

    public static void Draw(string label, string value, string? sub, Vector4 accent, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, Height * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var dl = ImGui.GetWindowDrawList();

        AdtPaint.Glass(dl, origin, end, AdtStyling.CardRounding * scale, accent, 0.05f);

        var padX = PadX * scale;
        var padY = PadY * scale;
        var labelSize = AdtTextDraw.SmallCapsSize(label);
        var dotRadius = 3f * scale;
        dl.AddCircleFilled(new Vector2(origin.X + padX + dotRadius, origin.Y + padY + labelSize.Y * 0.5f), dotRadius, AdtPaint.Col(accent));
        AdtTextDraw.SmallCaps(label, new Vector2(origin.X + padX + dotRadius * 2f + 6f * scale, origin.Y + padY), AdtStyling.TextDim);

        var subWidth = 0f;
        if (!string.IsNullOrEmpty(sub))
        {
            using (AdtFonts.PushCaption())
            {
                var subSize = AdtTextDraw.Measure(sub);
                subWidth = subSize.X + 8f * scale;
                AdtTextDraw.At(sub, new Vector2(end.X - padX - subSize.X, end.Y - padY - subSize.Y - 1f * scale), AdtStyling.TextDim);
            }
        }

        using (AdtFonts.PushHeadline())
        {
            var text = AdtTextDraw.Truncate(value, size.X - padX * 2f - subWidth);
            var valueSize = AdtTextDraw.Measure(text);
            AdtTextDraw.At(text, new Vector2(origin.X + padX, end.Y - padY - valueSize.Y), AdtStyling.TextStrong);
        }

        ImGui.Dummy(size);
    }
}
