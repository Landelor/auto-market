using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace AutoMarket.Windows;

public sealed partial class MainWindow : Window, IDisposable
{
    private enum SearchScope
    {
        DataCenter,
        Region,
    }

    private enum QualityFilter
    {
        Any,
        Hq,
        Nq,
    }

    private readonly record struct ItemChoice(uint Id, string Name);

    private readonly Plugin plugin;
    private readonly List<ItemChoice> items = [];
    private readonly List<ItemChoice> matches = [];
    private readonly List<CartOrder> cart = [];
    private readonly Dictionary<string, string> worldDataCenters = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? searchCancellation;
    private Task<List<MarketQuote>>? searchTask;
    private ItemChoice? selectedItem;
    private List<MarketQuote> listings = [];
    private SearchScope searchScope;
    private QualityFilter qualityFilter;
    private uint? searchItemId;
    private string searchText = string.Empty;
    private string status = "Type at least two characters, then choose an item.";
    private int targetQuantity = 1;
    private int maxUnitPrice;
    private int maxBudget;
    private int nextCartId = 1;

    public MainWindow(Plugin plugin) : base("AutoMarket###AutoMarketMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        foreach (var item in Plugin.DataManager.GetExcelSheet<Item>())
        {
            var name = item.Name.ToString();
            if (item.ItemSearchCategory.RowId != 0 && !string.IsNullOrWhiteSpace(name))
                items.Add(new ItemChoice(item.RowId, name));
        }

        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            var name = world.Name.ToString();
            if (world.DataCenter.IsValid && !string.IsNullOrWhiteSpace(name))
                worldDataCenters[name] = world.DataCenter.Value.Name.ToString();
        }

        items.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
#if DEBUG
        MarketClient.SelfTest();
#endif
    }

    public void Open(string query)
    {
        IsOpen = true;
        if (string.IsNullOrWhiteSpace(query))
            return;

        searchText = query.Trim();
        RefreshMatches();
    }

    public void Dispose()
    {
        searchCancellation?.Cancel();
        searchCancellation?.Dispose();
    }

    public override void Draw()
    {
        FinishSearch();
        var dependenciesReady = DrawDependencyStatus();

        ImGui.Text("Item");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##AutoMarketItem", ref searchText, 128))
        {
            searchCancellation?.Cancel();
            selectedItem = null;
            listings.Clear();
            RefreshMatches();
        }

        ItemChoice? chosenItem = null;
        foreach (var match in matches)
        {
            if (ImGui.Selectable($"{match.Name}##{match.Id}"))
            {
                chosenItem = match;
                break;
            }
        }

        if (chosenItem is { } chosen)
        {
            selectedItem = chosen;
            searchText = chosen.Name;
            matches.Clear();
            listings.Clear();
            status = $"Selected {chosen.Name}.";
        }

        DrawSearchOptions();

        if (selectedItem is { } item && searchTask is null && ImGui.Button("Search all listings"))
            StartSearch(item);

        if (searchTask is not null)
            ImGui.Text("Checking Universalis...");

        ImGui.Separator();
        ImGui.TextWrapped(status);
        if (!string.IsNullOrWhiteSpace(plugin.PurchaseStatus))
            ImGui.TextWrapped(plugin.PurchaseStatus);
        if (plugin.QueueTotal > 0)
        {
            ImGui.Text($"Stage: {plugin.ProgressStage}");
            ImGui.ProgressBar(Math.Clamp((float)plugin.QueueCompleted / plugin.QueueTotal, 0, 1), new Vector2(-1, 0), $"{plugin.QueueCompleted}/{plugin.QueueTotal}");
        }
        if (plugin.IsPurchasePending && ImGui.SmallButton("Cancel purchase"))
            plugin.CancelPurchase();

        var visibleListings = listings.Where(MatchesQuality).ToList();
        if (listings.Count > 0)
        {
            ImGui.Text($"Showing {visibleListings.Count:N0} of {listings.Count:N0} listings, cheapest first.");
            if (selectedItem is { } selected && ImGui.Button("Add optimized plan to list"))
                AddToCart(selected, visibleListings);
            ImGui.SameLine();
            ImGui.TextDisabled("Cart purchases whole stacks and may exceed the target on the final stack.");
            DrawListings(visibleListings, dependenciesReady);
        }

        DrawCart(dependenciesReady);
        DrawHistory();
    }

    private bool DrawDependencyStatus()
    {
        var lifestream = plugin.GetPluginState("Lifestream");

        ImGui.Text("Dependencies");
        DrawPluginStatus("Lifestream", lifestream, required: true);
        if (!lifestream.Loaded)
            ImGui.TextWrapped("Install or enable Lifestream, then return here. AutoMarket will detect it automatically.");
        ImGui.Separator();
        return lifestream.Loaded;
    }

    private static void DrawPluginStatus(string name, (bool Installed, bool Loaded) state, bool required)
    {
        var (color, text) = state switch
        {
            { Loaded: true } => (new Vector4(0.3f, 1f, 0.4f, 1f), "loaded"),
            { Installed: true } => (new Vector4(1f, 0.7f, 0.2f, 1f), "installed, disabled"),
            _ when required => (new Vector4(1f, 0.3f, 0.3f, 1f), "missing, required"),
            _ => (new Vector4(0.7f, 0.7f, 0.7f, 1f), "not installed, optional"),
        };
        ImGui.TextColored(color, $"{name}: {text}");
    }
}
