using System;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    /// <summary>
    /// Persisted across game sessions in the plugin's own config directory - same pattern
    /// Plugin.MarketCache.cs/Plugin.Purchases.cs already use (a plain JSON file via
    /// System.Text.Json, not Dalamud's Configuration system, which this plugin doesn't use
    /// elsewhere). Only the resize-lock state and the size it was locked at are saved; an
    /// unlocked window still relies on ImGui's own layout recall (imgui.ini) like before.
    /// </summary>
    private sealed record WindowSettings(bool ResizeLocked, float Width, float Height);

    private static string SettingsPath => Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "windowSettings.json");

    private void LoadWindowSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var settings = JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(SettingsPath));
            if (settings is null)
                return;

            resizeLocked = settings.ResizeLocked;
            if (settings.Width > 0f && settings.Height > 0f)
                manualSize = Vector2.Clamp(new Vector2(settings.Width, settings.Height), expandedConstraints.MinimumSize, expandedConstraints.MaximumSize);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to load AutoMarket window settings");
        }
    }

    private void SaveWindowSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var size = manualSize ?? Vector2.Zero;
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new WindowSettings(resizeLocked, size.X, size.Y)));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to save AutoMarket window settings");
        }
    }
}
