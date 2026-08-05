using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AutoMarket;

internal sealed class MarketClient : IDisposable
{
    private readonly HttpClient client = new()
    {
        BaseAddress = new Uri("https://universalis.app/api/v2/"),
        Timeout = TimeSpan.FromSeconds(20),
    };

    public MarketClient(string version)
    {
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AutoMarket", version));
    }

    public async Task<List<MarketQuote>> FindListingsAsync(string scope, uint itemId, CancellationToken cancellationToken)
    {
        var path = $"{Uri.EscapeDataString(scope)}/{itemId}?entries=0";
        using var stream = await client.GetStreamAsync(path, cancellationToken).ConfigureAwait(false);
        var response = await JsonSerializer.DeserializeAsync<MarketResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return SortListings(response?.Listings ?? []);
    }

    public void Dispose() => client.Dispose();

    private static List<MarketQuote> SortListings(IEnumerable<MarketListing> listings) => listings
        .Where(listing => listing.PricePerUnit > 0
            && listing.Quantity > 0
            && !listing.OnMannequin
            && !string.IsNullOrWhiteSpace(listing.WorldName))
        .OrderBy(listing => listing.PricePerUnit)
        .ThenBy(listing => listing.Total)
        .Select(listing => new MarketQuote(
            listing.WorldName!,
            listing.PricePerUnit,
            listing.Quantity,
            listing.Total,
            listing.Hq,
            listing.RetainerName ?? "Unknown",
            listing.LastReviewTime,
            ParseId(listing.ListingId),
            ParseId(listing.RetainerId)))
        .ToList();

    private static ulong ParseId(string? value) => ulong.TryParse(value, out var id) ? id : 0;

#if DEBUG
    internal static void SelfTest()
    {
        var sorted = SortListings([
            new MarketListing { WorldName = "Alpha", PricePerUnit = 20, Quantity = 1, Total = 20 },
            new MarketListing { WorldName = "Beta", PricePerUnit = 10, Quantity = 2, Total = 20 },
            new MarketListing { WorldName = "Ignored", PricePerUnit = 1, Quantity = 1, Total = 1, OnMannequin = true },
        ]);

        if (sorted.Count != 2 || sorted[0].World != "Beta" || sorted[1].World != "Alpha")
            throw new InvalidOperationException("Listing sort self-check failed.");

        var quote = new MarketQuote("Alpha", 20, 2, 40, true, "Retainer", 0, 10, 11);
        if (!quote.MatchesLive(20, 2, true)
            || quote.MatchesLive(21, 2, true)
            || quote.MatchesLive(20, 2, false)
            || !quote.MatchesLive(20, 99, true, flexibleQuantity: true))
            throw new InvalidOperationException("Purchase guard self-check failed.");

        var plan = PurchasePlanner.PlanQuotes(sorted, 3, 20, 40);
        if (plan.Count != 2 || plan.Sum(listing => listing.Quantity) != 3)
            throw new InvalidOperationException("Cart planner self-check failed.");

        var exactPlan = PurchasePlanner.PlanQuotes([
            new MarketQuote("Alpha", 90, 2, 180, false, "A", 0, 1, 1),
            new MarketQuote("Alpha", 100, 1, 100, false, "B", 0, 2, 2),
        ], 1, 0, 0);
        if (exactPlan.Count != 1 || exactPlan[0].Quantity != 1 || exactPlan[0].Total != 100)
            throw new InvalidOperationException("Quantity-first planner self-check failed.");
    }
#endif

    private sealed class MarketResponse
    {
        [JsonPropertyName("listings")]
        public List<MarketListing> Listings { get; init; } = [];
    }

    private sealed class MarketListing
    {
        [JsonPropertyName("worldName")]
        public string? WorldName { get; init; }

        [JsonPropertyName("pricePerUnit")]
        public long PricePerUnit { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }

        [JsonPropertyName("total")]
        public long Total { get; init; }

        [JsonPropertyName("hq")]
        public bool Hq { get; init; }

        [JsonPropertyName("onMannequin")]
        public bool OnMannequin { get; init; }

        [JsonPropertyName("retainerName")]
        public string? RetainerName { get; init; }

        [JsonPropertyName("lastReviewTime")]
        public long LastReviewTime { get; init; }

        [JsonPropertyName("listingID")]
        public string? ListingId { get; init; }

        [JsonPropertyName("retainerID")]
        public string? RetainerId { get; init; }
    }
}
