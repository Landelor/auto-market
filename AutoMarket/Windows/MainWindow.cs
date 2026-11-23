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
    private readonly WindowSizeConstraints expandedConstraints;
    private static readonly WindowSizeConstraints CollapsedConstraints = new()
    {
        MinimumSize = new Vector2(320, 40),
        MaximumSize = new Vector2(float.MaxValue, 40),
    };
    private CancellationTokenSource? searchCancellation;
    private Task<List<MarketQuote>>? searchTask;
    private ItemChoice? selectedItem;
    private List<MarketQuote> listings = [];
    private QualityFilter qualityFilter;
    private uint? searchItemId;
    private string searchText = string.Empty;
    private string status = "Type at least two characters, then choose an item.";
    private int targetQuantity = 1;
    private int maxUnitPrice;
    private int maxBudget;
    private int nextCartId = 1;
    private bool headerCollapsed;

    public MainWindow(Plugin plugin) : base("AutoMarket###AutoMarketMain")
    {
        this.plugin = plugin;
        Flags |= ImGuiWindowFlags.NoTitleBar;
        expandedConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        SizeConstraints = expandedConstraints;

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

    public override void PreDraw()
    {
        base.PreDraw();
        SizeConstraints = headerCollapsed ? CollapsedConstraints : expandedConstraints;
    }

    public override void Draw()
    {
        Theme.Push();
        try
        {
            DrawContent();
        }
        finally
        {
            Theme.Pop();
        }
    }

    private void DrawContent()
    {
        FinishSearch();
        FinishImportBuild();
        DrawHeaderBar();
        if (headerCollapsed)
            return;

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
        DrawRouteCard();

        var visibleListings = listings.Where(MatchesQuality).ToList();
        if (listings.Count > 0)
        {
            ImGui.Text($"Showing {visibleListings.Count:N0} of {listings.Count:N0} listings, cheapest first.");
            if (selectedItem is { } selected && ImGui.Button("Add optimized plan to list"))
                AddToCart(selected, visibleListings);
            ImGui.SameLine();
            ImGui.TextDisabled("Cart purchases whole stacks and may exceed the target on the final stack.");
            ImGui.TextDisabled("Updated: green = verified live in your own client; gray/amber/red = how old the Universalis report is.");
            DrawListings(visibleListings, dependenciesReady);
        }

        DrawImport();
        DrawCart(dependenciesReady);
    }

    private bool DrawDependencyStatus()
    {
        var lifestream = plugin.GetPluginState("Lifestream");
        var allaganTools = plugin.GetPluginState("InventoryTools");

        ImGui.Text("Dependencies");
        DrawPluginStatus("Lifestream", lifestream, required: true);
        ImGui.SameLine();
        DrawPluginStatus("Allagan Tools", allaganTools, required: false);
        if (!lifestream.Loaded)
            ImGui.TextWrapped("Install or enable Lifestream, then return here. AutoMarket will detect it automatically.");
        if (!allaganTools.Loaded)
            ImGui.TextWrapped("Optional: install or enable Allagan Tools so target quantities subtract what you already hold in your inventory and your retainers.");
        ImGui.Separator();
        return lifestream.Loaded;
    }

    private static void DrawPluginStatus(string name, (bool Installed, bool Loaded) state, bool required)
    {
        var (color, text) = state switch
        {
            { Loaded: true } => (Theme.Green, "loaded"),
            { Installed: true } => (Theme.Amber, "installed, disabled"),
            _ when required => (Theme.Rose, "missing, required"),
            _ => (Theme.Muted, "not installed, optional"),
        };
        Theme.Pill($"{name}: {text}", color);
    }
}
