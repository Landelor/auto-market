using System;
using System.Collections.Generic;

namespace AutoMarket;

internal sealed record MarketQuote(
    string World,
    long PricePerUnit,
    int Quantity,
    long Total,
    bool Hq,
    string RetainerName,
    long LastReviewTime,
    ulong ListingId,
    ulong RetainerId)
{
    internal bool MatchesLive(uint unitPrice, uint quantity, bool hq, bool flexibleQuantity = false) =>
        unitPrice <= PricePerUnit
        && (flexibleQuantity || quantity == Quantity)
        && hq == Hq;
}

internal sealed record CartOrder(
    int Id,
    uint ItemId,
    string ItemName,
    int TargetQuantity,
    long MaxBudget,
    List<MarketQuote> PlannedQuotes);

internal sealed record PurchaseHistoryEntry(
    DateTimeOffset PurchasedAt,
    uint ItemId,
    string ItemName,
    string World,
    bool Hq,
    uint Quantity,
    uint UnitPrice,
    uint Tax,
    long Total);
