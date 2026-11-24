using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoMarket.Windows.Adt;

/// <summary>
/// Prototype only. Ported from Auto Daily Tribes' Windows/Styling.cs (AGPL-3.0, same author/
/// license as AutoMarket) - the actual source of the look AutoMarket's hand-rolled Nord
/// <see cref="Theme"/> was approximating from screenshots. Trimmed of tribe-specific bits
/// (TribeKind colors) and Loc/localization calls; everything else - palette, text-contrast math,
/// pulse animation, and the single window-wide style/color push - is a direct port.
/// </summary>
internal static class AdtStyling
{
    public static readonly Vector4 AccentTeal = new(0.32f, 0.78f, 0.78f, 1.00f);
    public static readonly Vector4 AccentTealSoft = new(0.52f, 0.91f, 0.91f, 1.00f);
    public static readonly Vector4 AccentMint = new(0.46f, 0.86f, 0.66f, 1.00f);
    public static readonly Vector4 AccentAmber = new(0.92f, 0.74f, 0.34f, 1.00f);
    public static readonly Vector4 AccentAmberSoft = new(1.00f, 0.86f, 0.52f, 1.00f);
    public static readonly Vector4 AccentRose = new(0.93f, 0.42f, 0.50f, 1.00f);
    public static readonly Vector4 AccentViolet = new(0.62f, 0.42f, 0.96f, 1.00f);

    /// <summary>
    /// A real (if cool/dim) hue for neutral/idle states. ADT's own ReadyState.cs never pairs a
    /// status pill with a flat grey like <see cref="TextMuted"/> - every state gets a saturated
    /// accent so the pill stays legible; this is that color for AutoMarket's "cart is empty" rest
    /// state, which ADT has no equivalent of (it always has a tribe-running pipeline to show).
    /// </summary>
    public static readonly Vector4 AccentSlate = new(0.56f, 0.64f, 0.78f, 1.00f);

    public static readonly Vector4 WindowBg = new(0.046f, 0.056f, 0.068f, 0.985f);
    public static readonly Vector4 Surface0 = new(0.076f, 0.092f, 0.110f, 1.00f);
    public static readonly Vector4 Surface1 = new(0.102f, 0.122f, 0.145f, 1.00f);
    public static readonly Vector4 Surface2 = new(0.136f, 0.160f, 0.188f, 1.00f);
    public static readonly Vector4 Surface3 = new(0.174f, 0.202f, 0.236f, 1.00f);

    public static readonly Vector4 SliderBg = new(0.154f, 0.180f, 0.212f, 1.00f);
    public static readonly Vector4 BorderDim = new(0.228f, 0.268f, 0.310f, 1.00f);

    public static readonly Vector4 TextStrong = new(0.965f, 0.968f, 0.975f, 1.00f);
    public static readonly Vector4 InkOnAccent = new(0.078f, 0.059f, 0.133f, 1.00f);
    public static readonly Vector4 TextSecondary = new(0.780f, 0.805f, 0.840f, 1.00f);
    public static readonly Vector4 TextDim = new(0.560f, 0.595f, 0.640f, 1.00f);
    public static readonly Vector4 TextMuted = new(0.400f, 0.425f, 0.470f, 1.00f);

    public static readonly Vector4 Hairline = new(1f, 1f, 1f, 0.055f);

    public const float WindowRounding = 14f;
    public const float PanelRounding = 12f;
    public const float CardRounding = 10f;
    public const float FrameRounding = 7f;

    public const double PulseMedium = 800.0;
    public const double PulseBreath = 2600.0;
    public const double PulseOrbit = 3400.0;

    public static float Pulse(double periodMs = PulseMedium)
    {
        var t = (Environment.TickCount % periodMs) / periodMs;
        return (float)((Math.Sin(t * Math.PI * 2.0) + 1.0) * 0.5);
    }

    public static Vector4 PulseColor(Vector4 a, Vector4 b, double periodMs = PulseMedium)
        => Vector4.Lerp(a, b, Pulse(periodMs));

    public static float Phase(double periodMs)
        => (float)((Environment.TickCount % periodMs) / periodMs);

    public static Vector4 WithAlpha(Vector4 c, float a) => c with { W = a };

    // Only a pale accent crosses 0.65; the brand accents keep white text, same threshold as ADT.
    public static Vector4 ForegroundOn(Vector4 fill) => Luminance(fill) > 0.65f ? InkOnAccent : TextStrong;

    private static float Luminance(Vector4 color) => 0.2126f * Linear(color.X) + 0.7152f * Linear(color.Y) + 0.0722f * Linear(color.Z);

    private static float Linear(float channel) => channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    public static Vector4 Lighten(Vector4 c, float t) => Vector4.Lerp(c, Vector4.One, t) with { W = c.W };

    public static Vector4 Darken(Vector4 c, float t) => Vector4.Lerp(c, Vector4.Zero, t) with { W = c.W };

    public static Vector4 Tint(Vector4 baseColor, Vector4 accent, float amount)
        => Vector4.Lerp(baseColor, accent, amount) with { W = baseColor.W };

    public static void VSpace(float pixels)
        => ImGui.Dummy(new Vector2(0, pixels * ImGuiHelpers.GlobalScale));

    public static void SectionLabel(string label)
    {
        using (AdtFonts.PushHeadline())
        using (ImRaii.PushColor(ImGuiCol.Text, TextStrong))
            ImGui.TextUnformatted(label);
    }

    public static IDisposable PushChrome()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var style = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, WindowRounding * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.ChildRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.ChildBorderSize, 0f)
            .Push(ImGuiStyleVar.PopupRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f)
            .Push(ImGuiStyleVar.FrameRounding, FrameRounding * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(10f, 6f) * scale)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 8f) * scale)
            .Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6f, 4f) * scale)
            .Push(ImGuiStyleVar.ScrollbarSize, 9f * scale)
            .Push(ImGuiStyleVar.ScrollbarRounding, 9f * scale)
            .Push(ImGuiStyleVar.GrabRounding, 6f * scale)
            .Push(ImGuiStyleVar.GrabMinSize, 12f * scale);

        var color = ImRaii.PushColor(ImGuiCol.WindowBg, WindowBg)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, WithAlpha(Surface1, 0.985f))
            .Push(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.09f))
            .Push(ImGuiCol.BorderShadow, Vector4.Zero)
            .Push(ImGuiCol.FrameBg, SliderBg)
            .Push(ImGuiCol.FrameBgHovered, Surface2)
            .Push(ImGuiCol.FrameBgActive, Surface3)
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, new Vector4(1f, 1f, 1f, 0.12f))
            .Push(ImGuiCol.ScrollbarGrabHovered, new Vector4(1f, 1f, 1f, 0.20f))
            .Push(ImGuiCol.ScrollbarGrabActive, new Vector4(1f, 1f, 1f, 0.28f))
            .Push(ImGuiCol.Button, Surface1)
            .Push(ImGuiCol.ButtonHovered, Surface2)
            .Push(ImGuiCol.ButtonActive, Tint(Surface2, AccentTeal, 0.35f))
            .Push(ImGuiCol.Header, Tint(Surface1, AccentTeal, 0.30f))
            .Push(ImGuiCol.HeaderHovered, Surface2)
            .Push(ImGuiCol.HeaderActive, Tint(Surface2, AccentTeal, 0.40f))
            .Push(ImGuiCol.CheckMark, AccentTealSoft)
            .Push(ImGuiCol.SliderGrab, AccentTeal)
            .Push(ImGuiCol.SliderGrabActive, AccentTealSoft)
            .Push(ImGuiCol.Text, TextStrong)
            .Push(ImGuiCol.TextDisabled, TextMuted)
            .Push(ImGuiCol.Separator, Hairline)
            // Hidden: AutoMarket draws its own corner resize-grip hint (MainWindow.DrawResizeGripHint)
            // so the native triangle - unthemed grey by default - never shows through underneath it.
            .Push(ImGuiCol.ResizeGrip, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripHovered, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripActive, Vector4.Zero)
            .Push(ImGuiCol.TextSelectedBg, WithAlpha(AccentTeal, 0.35f));

        return new ChromeScope(style, color);
    }

    private sealed class ChromeScope(IDisposable style, IDisposable color) : IDisposable
    {
        public void Dispose()
        {
            color.Dispose();
            style.Dispose();
        }
    }
}
