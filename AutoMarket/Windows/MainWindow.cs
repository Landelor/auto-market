using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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
    private IDisposable? chrome;

    public MainWindow(Plugin plugin) : base("AutoMarket###AutoMarketMain")
    {
        this.plugin = plugin;
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
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
        Flags &= ~ImGuiWindowFlags.NoResize;
        if (headerCollapsed)
        {
            // Matches DrawHeaderBar's actual (DPI-scaled) height + the gap it leaves below itself,
            // not a guessed fixed pixel value - a stale constant here would clip the real header.
            var collapsedHeight = (HeaderHeight + 8f) * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(320, collapsedHeight),
                MaximumSize = new Vector2(float.MaxValue, collapsedHeight),
            };
        }
        else
        {
            SizeConstraints = expandedConstraints;
        }

        // Dalamud calls PreDraw() before ImGui.Begin() paints the window's background/border, and
        // Draw() only runs after Begin() - so pushing chrome inside Draw() (the previous bug) is
        // too late to affect that background/border; ADT itself pushes in PreDraw/PostDraw for
        // exactly this reason (see AutoDailyTribes' Windows/Shell/AppWindow.cs). Zero window
        // padding, same as ADT's own PushChrome(Vector2.Zero) call, so the header can draw flush
        // to every edge instead of sitting in a padding gap that let the game show through at the
        // corners; the body below gets its own padding via a child region (see DrawContent).
        chrome = Adt.AdtStyling.PushChrome(Vector2.Zero);
    }

    public override void PostDraw()
    {
        chrome?.Dispose();
        chrome = null;
        base.PostDraw();
    }

    public override void Draw()
    {
        DrawContent();
        if (!headerCollapsed)
            DrawResizeGripHint();
    }

    /// <summary>
    /// A visual corner hint (ADT's AppWindow.cs DrawResizeGrip) so the window's built-in
    /// drag-to-resize corner - otherwise undiscoverable with no visible border - has an
    /// affordance. Must be called against the outer window (after any child Begin/End has
    /// returned), since it reads the *current* window's position/size/draw list.
    /// </summary>
    private static void DrawResizeGripHint()
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        const float gripSize = 14f;
        const float inset = 5f;
        var grip = gripSize * scale;
        var padding = inset * scale;
        var corner = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(padding, padding);
        var hovered = ImGui.IsMouseHoveringRect(corner - new Vector2(grip + padding, grip + padding), corner);
        var color = ImGui.GetColorU32(Adt.AdtStyling.WithAlpha(Adt.AdtStyling.AccentTeal, hovered ? 0.85f : 0.45f));
        var dl = ImGui.GetWindowDrawList();
        for (var line = 1; line <= 3; line++)
        {
            var offset = line * grip / 3f;
            dl.AddLine(corner - new Vector2(offset, 0f), corner - new Vector2(0f, offset), color, 1.6f * scale);
        }
    }

    /// <summary>
    /// Everything below the header, in its own child region - exactly ADT's structure (header
    /// drawn flush to the window edges with zero window padding, body in a separately-padded
    /// child). This is what makes the body's scrollbar start right under the header instead of
    /// running the full window height behind it, show only when content actually overflows (the
    /// child's own native scrollbar), and gives body content breathing room despite the window
    /// itself having zero padding.
    /// </summary>
    private void DrawContent()
    {
        FinishSearch();
        FinishImportBuild();
        DrawHeaderBar();
        if (headerCollapsed)
            return;

        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f, 12f) * scale))
        using (var body = ImRaii.Child("##am_body", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (body)
                DrawBodyContent();
        }
    }

    private void DrawBodyContent()
    {
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
            { Loaded: true } => (Adt.AdtStyling.AccentMint, "loaded"),
            { Installed: true } => (Adt.AdtStyling.AccentAmber, "installed, disabled"),
            _ when required => (Adt.AdtStyling.AccentRose, "missing, required"),
            _ => (Adt.AdtStyling.TextMuted, "not installed, optional"),
        };
        Adt.AdtChip.Draw($"{name}: {text}", color);
    }
}
