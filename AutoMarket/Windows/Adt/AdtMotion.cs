using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoMarket.Windows.Adt;

/// <summary>
/// Prototype only. Port of Auto Daily Tribes' Windows/Motion.cs (AGPL-3.0): frame-rate independent
/// exponential "approach" easing (so the progress ring eases toward its target instead of jumping)
/// plus the easing curves used elsewhere. Respects Dalamud's reduced-motion accessibility setting,
/// same as the original.
/// </summary>
internal static class AdtMotion
{
    private static readonly Dictionary<int, float> values = new();

    public static bool Reduced => Plugin.PluginInterface.UiBuilder.ShouldUseReducedMotion;

    public static float DeltaTime => MathF.Min(ImGui.GetIO().DeltaTime, 0.05f);

    public static int Key(string id) => unchecked((int)ImGui.GetID(id));

    public static float Approach(int key, float target, float speed = 14f)
    {
        if (Reduced || !values.TryGetValue(key, out var current))
        {
            values[key] = target;
            return target;
        }

        var next = current + (target - current) * (1f - MathF.Exp(-speed * DeltaTime));
        if (MathF.Abs(next - target) < 0.0005f)
            next = target;

        values[key] = next;
        return next;
    }

    public static float Hover(int key, bool hovered) => Approach(key, hovered ? 1f : 0f, 18f);

    public static ImRaii.StyleDisposable PushAlpha(float progress)
        => ImRaii.PushStyle(ImGuiStyleVar.Alpha, MathF.Max(0.001f, progress * ImGui.GetStyle().Alpha));

    public static float EaseOutCubic(float t)
    {
        var u = 1f - t;
        return 1f - u * u * u;
    }
}
