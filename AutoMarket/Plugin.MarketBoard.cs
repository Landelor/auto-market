using System;
using System.Text;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoMarket;

public sealed partial class Plugin
{
    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        if (pendingPurchase is not { } request)
            return;

        var now = DateTimeOffset.UtcNow;
        if (now < nextPurchaseCheck)
            return;

        nextPurchaseCheck = now.AddMilliseconds(250);
        if (awaitingPurchase)
        {
            if (purchaseConfirmed)
                CompletePurchase(now);
            else if (now - purchaseSentAt > TimeSpan.FromSeconds(15))
                FailPurchase("The server did not confirm the market purchase. The remaining route was stopped.");
            return;
        }

        if (now - request.Started > TimeSpan.FromMinutes(8))
        {
            FailPurchase("Timed out waiting for Lifestream or the market board.");
            return;
        }

        try
        {
            ProcessMarketBoard(request, now);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Automatic purchase failed");
            FailPurchase("Automatic purchase failed safely. Nothing else will be attempted; check /xllog.");
        }
    }

    private unsafe void ProcessMarketBoard(PurchaseRequest request, DateTimeOffset now)
    {
        var currentWorld = CurrentLocation?.World ?? string.Empty;
        if (!currentWorld.Equals(request.Quote.World, StringComparison.OrdinalIgnoreCase))
            return;

        var addon = (AddonItemSearch*)GameGui.GetAddonByName("ItemSearch").Address;
        if (!PrepareLiveListingSearch(addon, request, now))
            return;

        PurchaseLiveListing(addon, request, now);
    }

    private unsafe bool PrepareLiveListingSearch(AddonItemSearch* addon, PurchaseRequest request, DateTimeOffset now)
    {
        if (!listingRequestStarted && (addon == null || !addon->AtkUnitBase.IsVisible))
            return false;

        if (marketBoardDetectedAt == default)
        {
            marketBoardDetectedAt = now;
            ProgressStage = "Opening market board";
            PurchaseStatus = $"[{ProgressStage}] Waiting for the board to finish initializing, {FormatQueueProgress()}.";
            Log.Information("Market board detected for item {ItemId}", request.ItemId);
            return false;
        }

        if (now - marketBoardDetectedAt < TimeSpan.FromSeconds(1))
            return false;

        if (!searchSubmitted)
        {
            if (!RunTextSearch(addon, request.ItemName))
                return false;

            searchSubmitted = true;
            searchAttempts = 1;
            listingRequestedAt = now;
            ProgressStage = "Searching";
            PurchaseStatus = $"[{ProgressStage}] Live {request.Quote.World} market for {request.ItemName}, {FormatQueueProgress()}.";
            Log.Information("Market text search submitted for {ItemId}", request.ItemId);
            return false;
        }

        if (listingRequestStarted)
            return true;

        var resultIndex = FindSearchResultIndex(addon, request.ItemId);
        if (resultIndex >= 0)
        {
            addon->ResultsList->DispatchItemEvent(resultIndex, AtkEventType.ListItemClick);
            listingRequestStarted = true;
            listingAttempts = 1;
            listingRequestedAt = now;
            ProgressStage = "Verifying live listings";
            PurchaseStatus = $"[{ProgressStage}] Checking price, quality, budget, gil, and inventory, {FormatQueueProgress()}.";
            Log.Information("Market result {Index} opened for {ItemId}", resultIndex, request.ItemId);
            return false;
        }

        var searchTimeout = searchAttempts == 1 ? TimeSpan.FromSeconds(3.5) : TimeSpan.FromSeconds(6);
        if (now - listingRequestedAt < searchTimeout)
            return false;

        if (searchAttempts == 1 && RunTextSearch(addon, request.ItemName))
        {
            searchAttempts = 2;
            listingRequestedAt = now;
            Log.Information("Market text search retried for {ItemId}", request.ItemId);
            return false;
        }

        FailPurchase("The item did not appear in the live market search. Nothing was purchased.");
        return false;
    }

    private unsafe void PurchaseLiveListing(AddonItemSearch* addon, PurchaseRequest request, DateTimeOffset now)
    {
        var agent = AgentItemSearch.Instance();
        var proxy = agent == null ? null : agent->InfoProxyItemSearch;
        if (proxy == null || liveListingId == 0)
        {
            WaitForLiveListings(addon, request, now);
            return;
        }

        var listings = proxy->Listings;
        var count = Math.Min((int)proxy->ListingCount, listings.Length);
        if (count == 0 && now - listingRequestedAt < TimeSpan.FromSeconds(10))
            return;

        var match = -1;
        for (var index = 0; index < count; index++)
        {
            ref var listing = ref listings[index];
            if (listing.ListingId == liveListingId
                && listing.ItemId == request.ItemId
                && request.Quote.MatchesLive(listing.UnitPrice, listing.Quantity, listing.IsHqItem, request.FlexibleStacks))
            {
                match = index;
                break;
            }
        }

        if (match < 0)
        {
            if (now - liveListingMatchedAt < TimeSpan.FromSeconds(5))
                return;

            FailPurchase("No safe live listing matches this purchase plan. Nothing was purchased; search again.");
            return;
        }

        ref var selected = ref listings[match];
        var totalCost = checked((long)selected.UnitPrice * selected.Quantity + selected.TotalTax);
        if (activeBudgetRemaining > 0 && totalCost > activeBudgetRemaining)
        {
            FailPurchase($"The live stack costs {totalCost:N0} gil including tax, above the remaining {activeBudgetRemaining:N0} gil budget.");
            return;
        }

        var inventory = InventoryManager.Instance();
        if (inventory == null)
        {
            FailPurchase("Inventory information is unavailable. Nothing was purchased.");
            return;
        }
        if (inventory->GetGil() < totalCost)
        {
            FailPurchase($"Not enough gil: the live stack costs {totalCost:N0} gil including tax.");
            return;
        }
        if (inventory->GetEmptySlotsInBag() == 0)
        {
            FailPurchase("No free inventory slots are available. Nothing was purchased.");
            return;
        }

        fixed (MarketBoardListing* listing = &selected)
        {
            sentQuantity = selected.Quantity;
            sentUnitPrice = selected.UnitPrice;
            sentTax = selected.TotalTax;
            awaitingPurchase = true;
            purchaseSentAt = now;
            if (!proxy->SetLastPurchasedItem(listing) || !proxy->SendPurchaseRequestPacket())
            {
                FailPurchase("The game rejected the purchase request. Nothing was purchased.");
                return;
            }

            ProgressStage = "Waiting for purchase confirmation";
            PurchaseStatus = $"[{ProgressStage}] {selected.Quantity:N0}x {request.ItemName} at {selected.UnitPrice:N0} gil each plus {selected.TotalTax:N0} tax, {FormatQueueProgress()}.";
        }
    }

    private unsafe void WaitForLiveListings(AddonItemSearch* addon, PurchaseRequest request, DateTimeOffset now)
    {
        if (listingRetryAt != default)
        {
            if (now < listingRetryAt || addon == null || !addon->AtkUnitBase.IsVisible)
                return;

            var resultIndex = FindSearchResultIndex(addon, request.ItemId);
            if (resultIndex < 0)
            {
                FailPurchase("The market result disappeared before listings could be refreshed. Nothing was purchased.");
                return;
            }

            liveListingId = 0;
            liveListingMatchedAt = default;
            liveOfferingsReceived = false;
            addon->ResultsList->DispatchItemEvent(resultIndex, AtkEventType.ListItemClick);
            listingRetryAt = default;
            listingRequestedAt = now;
            Log.Information("Live listing request retried for {ItemId}", request.ItemId);
            return;
        }

        var listingTimeout = listingAttempts == 1 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(10);
        if (now - listingRequestedAt < listingTimeout)
            return;

        if (listingAttempts == 1)
        {
            var resultAddon = (AtkUnitBase*)GameGui.GetAddonByName("ItemSearchResult").Address;
            if (resultAddon != null && resultAddon->IsVisible)
                resultAddon->Close(true);

            listingAttempts = 2;
            listingRetryAt = now.AddMilliseconds(300);
            ProgressStage = "Retrying live listings";
            PurchaseStatus = $"[{ProgressStage}] The first request timed out, {FormatQueueProgress()}.";
            return;
        }

        FailPurchase(liveOfferingsReceived
            ? "No safe live listing matches this purchase plan. Nothing was purchased; search again."
            : "The market board did not return live listings. Nothing was purchased.");
    }

    private void OnOfferingsReceived(IMarketBoardCurrentOfferings offerings)
    {
        if (pendingPurchase is not { } request || !listingRequestStarted)
            return;

        foreach (var listing in offerings.ItemListings)
        {
            if (listing.ItemId != request.ItemId)
                continue;

            liveOfferingsReceived = true;
            var totalCost = checked((long)listing.PricePerUnit * listing.ItemQuantity + listing.TotalTax);
            if (liveListingId == 0
                && (activeBudgetRemaining <= 0 || totalCost <= activeBudgetRemaining)
                && request.Quote.MatchesLive(listing.PricePerUnit, listing.ItemQuantity, listing.IsHq, request.FlexibleStacks))
            {
                liveListingId = listing.ListingId;
                liveListingMatchedAt = DateTimeOffset.UtcNow;
                Log.Information("Live market response matched listing {ListingId} for {ItemId}", liveListingId, request.ItemId);
            }
        }
    }

    private void OnItemPurchased(IMarketBoardPurchase purchase)
    {
        if (pendingPurchase is { } request
            && awaitingPurchase
            && purchase.CatalogId == request.ItemId
            && purchase.ItemQuantity == sentQuantity)
            purchaseConfirmed = true;
    }

    private static unsafe bool RunTextSearch(AddonItemSearch* addon, string itemName)
    {
        if (addon->SearchTextInput == null)
            return false;

        addon->SearchText.SetString(itemName);
        var encoded = Encoding.UTF8.GetBytes(itemName + '\0');
        fixed (byte* text = encoded)
            addon->SearchTextInput->SetText(text);
        addon->RunSearch(false);
        return true;
    }

    private static unsafe int FindSearchResultIndex(AddonItemSearch* addon, uint itemId)
    {
        if (addon->ResultsList == null || addon->ResultsList->ListLength == 0)
            return -1;

        var agent = AgentItemSearch.Instance();
        if (agent != null && agent->ItemBuffer != null)
        {
            var count = Math.Min((int)agent->ItemCount, addon->ResultsList->ListLength);
            for (var index = 0; index < count; index++)
            {
                if (agent->ItemBuffer[index] == itemId)
                    return index;
            }
        }

        return 0;
    }
}
