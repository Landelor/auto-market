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

    private enum MainTab
    {
        Dashboard,
        Search,
        Import,
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
    private bool resizeLocked;
    private MainTab activeTab = MainTab.Dashboard;
    private IDisposable? chrome;
    private Vector2? manualSize;

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
            // Size is a sticky property (not reset each frame): if the expanded branch forced it
            // last time this window was open, it must be cleared here or it keeps fighting the
            // collapsed-height constraint above every frame instead of letting it clamp freely.
            Size = null;
        }
        else
        {
            SizeConstraints = expandedConstraints;
            if (resizeLocked && manualSize is { } locked)
            {
                // Locking freezes the window at whatever size it was when the player clicked the
                // lock icon (captured onto `manualSize` there, see DrawHeaderButtons) by pinning
                // both the min and max constraint to it and continuing to force Size every frame -
                // the same ImGuiCond.Always mechanism the free-resize path already uses, just
                // with nowhere left for the user (or the resize grip, which is hidden while
                // locked) to push it.
                SizeConstraints = new WindowSizeConstraints { MinimumSize = locked, MaximumSize = locked };
                Size = locked;
                SizeCondition = ImGuiCond.Always;
            }
            else if (manualSize is { } wanted)
            {
                // Native border/corner resize-drag turned out to be unreliable here across three
                // separate fixes (chrome timing, padding, child insets) - most likely some other
                // ImGui ID/active-item interaction we could not pin down without a live debugger.
                // DrawResizeGrip() below now does the drag itself via a plain InvisibleButton and
                // writes the result here, so resizing no longer depends on ImGui's native corner
                // hit-test at all. ImGuiCond.Always every frame is deliberate: it is the only way
                // to keep asserting a size the user chose against ImGui's own layout/ini recall.
                Size = wanted;
                SizeCondition = ImGuiCond.Always;
            }
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
        if (!headerCollapsed && !resizeLocked)
            DrawResizeGrip();
    }

    /// <summary>
    /// A fully manual resize grip - not ImGui's native border/corner drag. Three releases in a
    /// row (chrome push timing, zero window padding, body-child insets up to 28px) all failed to
    /// make the native corner hit-test actually grab, even though it should have been clear by
    /// then; whatever the remaining interaction is, it could not be diagnosed further without a
    /// live ImGui debugger attached to the running game. A plain InvisibleButton + MouseDelta
    /// drag sidesteps the native mechanism entirely and is a standard, reliable pattern for
    /// custom-chrome ImGui windows. Must be called against the outer window (after any child
    /// Begin/End has returned), since it reads the *current* window's position/size/draw list.
    /// </summary>
    private void DrawResizeGrip()
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        const float gripSize = 14f;
        const float inset = 5f;
        const float hitSize = 22f;
        var grip = gripSize * scale;
        var padding = inset * scale;
        var hit = hitSize * scale;
        var corner = ImGui.GetWindowPos() + ImGui.GetWindowSize();

        ImGui.SetCursorScreenPos(corner - new Vector2(hit, hit));
        ImGui.InvisibleButton("##am_resize_grip", new Vector2(hit, hit));
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        if (active)
        {
            var current = manualSize ?? ImGui.GetWindowSize() / scale;
            var next = current + ImGui.GetIO().MouseDelta / scale;
            manualSize = Vector2.Clamp(next, expandedConstraints.MinimumSize, expandedConstraints.MaximumSize);
        }

        var color = ImGui.GetColorU32(Adt.AdtStyling.WithAlpha(Adt.AdtStyling.AccentTeal, active ? 1f : hovered ? 0.85f : 0.45f));
        var dl = ImGui.GetWindowDrawList();
        var anchor = corner - new Vector2(padding, padding);
        for (var line = 1; line <= 3; line++)
        {
            var offset = line * grip / 3f;
            dl.AddLine(anchor - new Vector2(offset, 0f), anchor - new Vector2(0f, offset), color, 1.6f * scale);
        }
    }

    /// <summary>
    /// Everything below the header, in its own child region - exactly ADT's structure (header
    /// drawn flush to the window edges with zero window padding, body in a separately-padded
    /// child). This is what makes the body's scrollbar start right under the header instead of
    /// running the full window height behind it, show only when content actually overflows (the
    /// child's own native scrollbar), and gives body content breathing room despite the window
    /// itself having zero padding.
    ///
    /// The child is deliberately sized a few pixels short of the window's own right/bottom edges
    /// (ADT's Layout.ContentRightInset does the same, at 6px) - a child window that reaches all
    /// the way to the physical edge takes mouse-hover priority over its parent there, which was
    /// silently swallowing clicks on the native resize-drag corner right underneath it.
    ///
    /// The body is further split into a narrow left icon rail (ADT-style section tabs: Dashboard/
    /// Search/Import) and the active tab's content, so a single window no longer has to scroll
    /// through all three workflows stacked on top of each other.
    /// </summary>
    private void DrawContent()
    {
        FinishSearch();
        FinishImportBuild();
        DrawHeaderBar();
        if (headerCollapsed)
            return;

        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        // ImGui's native resize-drag hit-test runs on whichever window is topmost under the
        // mouse; a child window always wins that over its own parent for the pixels it covers,
        // *regardless of what's drawn there* - so the body region must leave a real uncovered
        // strip along the window's own right/bottom edges, not just a cosmetically-sized gap.
        // ADT's AppWindow.cs gets this for free structurally (a 6px content-right-inset plus
        // its always-present bottom dock bar), so its resize-grip hint sits over genuinely bare
        // window, outside any child. 28px comfortably clears ImGui's actual corner hit-zone
        // (~18-22px at default font sizes) with margin for AutoMarket's larger custom fonts.
        var bodyInset = 28f * scale;
        var bodySize = ImGui.GetContentRegionAvail() - new Vector2(bodyInset, bodyInset);

        const float railWidth = 64f;
        const float railGap = 8f;
        var rail = railWidth * scale;
        var gap = railGap * scale;

        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8f, 14f) * scale))
        using (var nav = ImRaii.Child("##am_nav", new Vector2(rail, bodySize.Y), false))
        {
            if (nav)
                DrawNavRail();
        }

        ImGui.SameLine(0, gap);

        var contentSize = new Vector2(Math.Max(1f, bodySize.X - rail - gap), bodySize.Y);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f, 12f) * scale))
        using (var body = ImRaii.Child("##am_body", contentSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (body)
                DrawBodyContent();
        }
    }

    private void DrawBodyContent()
    {
        switch (activeTab)
        {
            case MainTab.Dashboard:
                DrawDashboardTab();
                break;
            case MainTab.Search:
                DrawSearchTab();
                break;
            case MainTab.Import:
                DrawImportTab();
                break;
        }
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
