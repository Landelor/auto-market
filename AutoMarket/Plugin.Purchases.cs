using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoMarket;

public sealed partial class Plugin
{
    private sealed record PurchaseRequest(
        int CartId,
        uint ItemId,
        string ItemName,
        MarketQuote Quote,
        bool FlexibleStacks,
        DateTimeOffset Started);

    private sealed class CartProgress(int targetQuantity, long maxBudget)
    {
        internal int TargetQuantity { get; } = targetQuantity;
        internal long MaxBudget { get; } = maxBudget;
        internal int PurchasedQuantity { get; set; }
        internal long Spent { get; set; }
    }

    private readonly Queue<PurchaseRequest> purchaseQueue = new();
    private readonly Dictionary<int, CartProgress> cartProgress = [];
    private readonly List<PurchaseHistoryEntry> purchaseHistory;
    private readonly string historyPath;
    private PurchaseRequest? pendingPurchase;
    private DateTimeOffset marketBoardDetectedAt;
    private bool searchSubmitted;
    private int searchAttempts;
    private bool listingRequestStarted;
    private int listingAttempts;
    private DateTimeOffset listingRetryAt;
    private DateTimeOffset listingRequestedAt;
    private DateTimeOffset nextPurchaseCheck;
    private ulong liveListingId;
    private DateTimeOffset liveListingMatchedAt;
    private bool liveOfferingsReceived;
    private long activeBudgetRemaining;
    private bool awaitingPurchase;
    private DateTimeOffset purchaseSentAt;
    private bool purchaseConfirmed;
    private uint sentQuantity;
    private uint sentUnitPrice;
    private uint sentTax;

    internal bool IsPurchasePending => pendingPurchase is not null || purchaseQueue.Count > 0;
    internal string PurchaseStatus { get; private set; } = string.Empty;
    internal string ProgressStage { get; private set; } = "Idle";
    internal int QueueCompleted { get; private set; }
    internal int QueueTotal { get; private set; }
    internal IReadOnlyList<PurchaseHistoryEntry> PurchaseHistory => purchaseHistory;

    internal unsafe string? TravelAndBuy(uint itemId, string itemName, MarketQuote quote)
    {
        if (IsPurchasePending)
            return "A purchase is already in progress.";

        purchaseQueue.Clear();
        cartProgress.Clear();
        purchaseQueue.Enqueue(new PurchaseRequest(0, itemId, itemName, quote, false, default));
        QueueCompleted = 0;
        QueueTotal = 1;
        return BeginNextPurchase();
    }

    internal unsafe string? StartCart(IReadOnlyList<CartOrder> orders)
    {
        if (IsPurchasePending)
            return "A purchase is already in progress.";

        var route = orders
            .SelectMany(order => order.PlannedQuotes.Select(quote => new PurchaseRequest(order.Id, order.ItemId, order.ItemName, quote, true, default)))
            .GroupBy(request => request.Quote.World, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group)
            .ToList();
        if (route.Count == 0)
            return "The cart has no purchasable listings within its limits.";

        purchaseQueue.Clear();
        cartProgress.Clear();
        foreach (var order in orders)
            cartProgress[order.Id] = new CartProgress(order.TargetQuantity, order.MaxBudget);
        foreach (var request in route)
            purchaseQueue.Enqueue(request);

        QueueCompleted = 0;
        QueueTotal = route.Count;
        return BeginNextPurchase();
    }

    private unsafe string? BeginNextPurchase()
    {
        while (purchaseQueue.TryDequeue(out var request))
        {
            ResetPurchaseState();
            if (request.CartId != 0 && cartProgress.TryGetValue(request.CartId, out var progress))
            {
                if (progress.PurchasedQuantity >= progress.TargetQuantity || (progress.MaxBudget > 0 && progress.Spent >= progress.MaxBudget))
                {
                    QueueCompleted++;
                    continue;
                }

                activeBudgetRemaining = progress.MaxBudget > 0 ? progress.MaxBudget - progress.Spent : 0;
            }
            else
            {
                activeBudgetRemaining = 0;
            }

            var currentWorld = CurrentLocation?.World;
            var marketAddon = (AddonItemSearch*)GameGui.GetAddonByName("ItemSearch").Address;
            var marketOpen = marketAddon != null && marketAddon->AtkUnitBase.IsVisible;
            if (!string.Equals(currentWorld, request.Quote.World, StringComparison.OrdinalIgnoreCase) || !marketOpen)
            {
                var error = TravelToMarketBoard(request.Quote.World);
                if (error is not null)
                {
                    FailPurchase(error);
                    return error;
                }
                ProgressStage = $"Traveling to {request.Quote.World}";
            }
            else
            {
                var resultAddon = (AtkUnitBase*)GameGui.GetAddonByName("ItemSearchResult").Address;
                if (resultAddon != null && resultAddon->IsVisible)
                    resultAddon->Close(true);
                ProgressStage = "Opening market board";
            }

            pendingPurchase = request with { Started = DateTimeOffset.UtcNow };
            PurchaseStatus = $"[{ProgressStage}] {request.ItemName}: {request.Quote.PricePerUnit:N0} gil each or less, {FormatQueueProgress()}.";
            Log.Information("Purchase started: {Item} on {World}, listing {ListingId}", request.ItemName, request.Quote.World, request.Quote.ListingId);
            return null;
        }

        pendingPurchase = null;
        cartProgress.Clear();
        ProgressStage = "Completed";
        PurchaseStatus = $"[Completed] Purchase route finished: {QueueCompleted:N0}/{QueueTotal:N0} planned stacks processed.";
        return null;
    }

    internal void CancelPurchase()
    {
        purchaseQueue.Clear();
        cartProgress.Clear();
        if (awaitingPurchase)
        {
            QueueTotal = QueueCompleted + 1;
            ProgressStage = "Waiting for purchase confirmation";
            PurchaseStatus = "The remaining route was cancelled. The purchase already sent to the server cannot be cancelled; waiting for confirmation.";
            return;
        }

        pendingPurchase = null;
        ResetPurchaseState();
        ProgressStage = "Cancelled";
        PurchaseStatus = "Purchase cancelled.";
    }

    private void FailPurchase(string message)
    {
        pendingPurchase = null;
        purchaseQueue.Clear();
        cartProgress.Clear();
        ResetPurchaseState();
        ProgressStage = "Stopped";
        PurchaseStatus = message;
        Log.Warning("Purchase stopped: {Message}", message);
    }

    private unsafe void CompletePurchase(DateTimeOffset now)
    {
        if (pendingPurchase is not { } request)
            return;

        var total = checked((long)sentUnitPrice * sentQuantity + sentTax);
        if (request.CartId != 0 && cartProgress.TryGetValue(request.CartId, out var progress))
        {
            progress.PurchasedQuantity += (int)sentQuantity;
            progress.Spent += total;
        }

        purchaseHistory.Insert(0, new PurchaseHistoryEntry(
            now,
            request.ItemId,
            request.ItemName,
            request.Quote.World,
            request.Quote.Hq,
            sentQuantity,
            sentUnitPrice,
            sentTax,
            total));
        if (purchaseHistory.Count > 200)
            purchaseHistory.RemoveRange(200, purchaseHistory.Count - 200);
        SaveHistory();

        QueueCompleted++;
        pendingPurchase = null;
        ResetPurchaseState();
        nextPurchaseCheck = now.AddMilliseconds(500);
        BeginNextPurchase();
    }

    private string FormatQueueProgress() => $"stack {Math.Min(QueueCompleted + 1, QueueTotal):N0}/{QueueTotal:N0}";

    private List<PurchaseHistoryEntry> LoadHistory()
    {
        try
        {
            return File.Exists(historyPath)
                ? JsonSerializer.Deserialize<List<PurchaseHistoryEntry>>(File.ReadAllText(historyPath)) ?? []
                : [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Purchase history could not be loaded");
            return [];
        }
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
            File.WriteAllText(historyPath, JsonSerializer.Serialize(purchaseHistory));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Purchase history could not be saved");
        }
    }

    private void ResetPurchaseState()
    {
        marketBoardDetectedAt = default;
        searchSubmitted = false;
        searchAttempts = 0;
        listingRequestStarted = false;
        listingAttempts = 0;
        listingRetryAt = default;
        listingRequestedAt = default;
        liveListingId = 0;
        liveListingMatchedAt = default;
        liveOfferingsReceived = false;
        awaitingPurchase = false;
        purchaseSentAt = default;
        purchaseConfirmed = false;
        sentQuantity = 0;
        sentUnitPrice = 0;
        sentTax = 0;
        activeBudgetRemaining = 0;
    }
}
