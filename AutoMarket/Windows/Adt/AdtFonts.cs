using System;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility.Raii;

namespace AutoMarket.Windows.Adt;

/// <summary>
/// Prototype only. Simplified port of Auto Daily Tribes' Windows/Fonts.cs: the same four size
/// tiers (caption/body/headline/title, each a fixed multiple of Dalamud's configured font size,
/// so the UI follows the user's Dalamud font-size setting instead of hard-coded pixels) built from
/// Dalamud's default font. The original also bundles a Latin ttf and merges CJK/localization glyph
/// ranges for full-game-text rendering; AutoMarket has neither a bundled font nor a translation
/// catalog, so this only keeps the scale-tier idea, not that machinery.
/// </summary>
internal static class AdtFonts
{
    private const float CaptionScale = 1.0f;
    private const float BodyScale = 1.125f;
    private const float HeadlineScale = 1.25f;
    private const float TitleScale = 1.625f;

    private static IUiBuilder? builder;
    private static float unitPx = UiBuilder.DefaultFontSizePx;

    private static IFontHandle? body;
    private static IFontHandle? title;
    private static IFontHandle? headline;
    private static IFontHandle? caption;

    public static void Initialize(IUiBuilder uiBuilder)
    {
        builder = uiBuilder;
        unitPx = uiBuilder.FontDefaultSizePx;

        var atlas = uiBuilder.FontAtlas;
        body = TextHandle(atlas, BodyScale);
        title = TextHandle(atlas, TitleScale);
        headline = TextHandle(atlas, HeadlineScale);
        caption = TextHandle(atlas, CaptionScale);
        uiBuilder.DefaultFontChanged += Rebuild;
    }

    public static void Dispose()
    {
        if (builder is not null)
            builder.DefaultFontChanged -= Rebuild;

        body?.Dispose();
        title?.Dispose();
        headline?.Dispose();
        caption?.Dispose();
        body = title = headline = caption = null;
        builder = null;
    }

    public static IDisposable PushBody() => body?.Push() ?? NoOpScope.Instance;

    public static IDisposable PushTitle() => title?.Push() ?? NoOpScope.Instance;

    public static IDisposable PushHeadline() => headline?.Push() ?? NoOpScope.Instance;

    public static IDisposable PushCaption() => caption?.Push() ?? NoOpScope.Instance;

    public static IDisposable PushIcon() => ImRaii.PushFont(UiBuilder.IconFont);

    private static IFontHandle TextHandle(IFontAtlas atlas, float scale)
        => atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(unitPx * scale)));

    private static void Rebuild()
    {
        unitPx = builder?.FontDefaultSizePx ?? UiBuilder.DefaultFontSizePx;
        builder?.FontAtlas.BuildFontsAsync();
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }
}
