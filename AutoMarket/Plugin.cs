using System;
using System.IO;
using System.Linq;
using AutoMarket.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace AutoMarket;

public sealed partial class Plugin : IDalamudPlugin
{
    internal const string CommandName = "/automarket";
    internal const string ShortCommandName = "/am";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IMarketBoard MarketBoard { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly ICallGateSubscriber<string, object> lifestreamCommand;
    private readonly WindowSystem windowSystem = new("AutoMarket");
    private readonly MainWindow mainWindow;

    internal MarketClient MarketClient { get; }

    public Plugin()
    {
        MarketClient = new MarketClient(PluginInterface.Manifest.AssemblyVersion?.ToString() ?? "dev");
        historyPath = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "history.json");
        purchaseHistory = LoadHistory();
        lifestreamCommand = PluginInterface.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand");
        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);

        if (!GetPluginState("Lifestream").Loaded)
            mainWindow.Open(string.Empty);

        var command = new CommandInfo(OnCommand) { HelpMessage = "Open AutoMarket. Optionally pass an item name." };
        CommandManager.AddHandler(CommandName, command);
        CommandManager.AddHandler(ShortCommandName, command);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenMainUi;
        Framework.Update += OnFrameworkUpdate;
        MarketBoard.OfferingsReceived += OnOfferingsReceived;
        MarketBoard.ItemPurchased += OnItemPurchased;
    }

    private (string World, string DataCenter, uint RegionId)? CurrentLocation
    {
        get
        {
            if (!PlayerState.IsLoaded)
                return null;

            var worldRef = PlayerState.CurrentWorld;
            if (!worldRef.IsValid)
                return null;

            var world = worldRef.Value;
            if (!world.DataCenter.IsValid)
                return null;

            var dataCenter = world.DataCenter.Value;
            return (world.Name.ToString(), dataCenter.Name.ToString(), dataCenter.Region.RowId);
        }
    }

    internal string? CurrentDataCenter => CurrentLocation?.DataCenter;

    internal string? CurrentRegion => CurrentLocation?.RegionId switch
    {
        1u => "Japan",
        2u => "North-America",
        3u => "Europe",
        4u => "Oceania",
        _ => null,
    };

    internal (bool Installed, bool Loaded) GetPluginState(string internalName)
    {
        var installedPlugin = PluginInterface.InstalledPlugins
            .FirstOrDefault(plugin => plugin.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase));
        return (installedPlugin is not null, installedPlugin?.IsLoaded == true);
    }

    internal string? TravelToMarketBoard(string world)
    {
        var state = GetPluginState("Lifestream");
        if (!state.Installed)
            return "Lifestream is not installed.";
        if (!state.Loaded)
            return "Lifestream is installed but not enabled.";

        try
        {
            lifestreamCommand.InvokeAction($"{world} mb");
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lifestream IPC call failed");
            return "Lifestream is not installed, loaded, or ready.";
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenMainUi;
        Framework.Update -= OnFrameworkUpdate;
        MarketBoard.OfferingsReceived -= OnOfferingsReceived;
        MarketBoard.ItemPurchased -= OnItemPurchased;
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShortCommandName);
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        MarketClient.Dispose();
    }

    private void OnCommand(string command, string args) => mainWindow.Open(args);

    private void OpenMainUi() => mainWindow.Open(string.Empty);
}
