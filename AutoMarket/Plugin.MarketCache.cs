using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.Network.Structures;

namespace AutoMarket;

public sealed partial class Plugin
{
    private sealed record CachedListing(
        uint ItemId,
        string World,
        ulong ListingId,
        ulong RetainerId,
        string RetainerName,
        long PricePerUnit,
        int Quantity,
        long Total,
        bool Hq,
        DateTimeOffset ObservedAt);

    /// <summary>
    /// How long a listing we personally saw live on a market board still counts as more trustworthy
    /// than whatever Universalis currently reports for that same world. Prices genuinely do change,
    /// so a stale local observation isn't preferred forever - just long enough to outrun a slow
    /// Universalis upload for the world/item we actually just looked at.
    /// </summary>
    private static readonly TimeSpan CacheFreshnessWindow = TimeSpan.FromMinutes(20);

    /// <summary>Entries older than this are dropped outright; by then they're no better than Universalis.</summary>
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(3);

    private const int CacheMaxEntries = 3000;

    private readonly Dictionary<string, CachedListing> marketCache;
    private readonly string marketCachePath;

    private static string CacheKey(uint itemId, string world, ulong listingId) =>
        $"{itemId}|{world.ToLowerInvariant()}|{listingId}";

    /// <summary>
    /// Records every live listing the player actually sees on a market board - whether browsing
    /// manually or mid AutoMarket-driven purchase - as ground truth, keyed by listing so repeat views
    /// just refresh the timestamp. This is what lets AutoMarket later prefer a listing it personally
    /// verified over a Universalis report for the same world that may not have caught up yet.
    /// </summary>
    private void CacheLiveOfferings(string world, IReadOnlyList<IMarketBoardItemListing> listings)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var listing in listings)
        {
            if (listing.PricePerUnit == 0 || listing.ItemQuantity == 0 || listing.OnMannequin)
                continue;

            var key = CacheKey(listing.ItemId, world, listing.ListingId);
            marketCache[key] = new CachedListing(
                listing.ItemId,
                world,
                listing.ListingId,
                listing.RetainerId,
                listing.RetainerName,
                listing.PricePerUnit,
                (int)listing.ItemQuantity,
                checked((long)listing.PricePerUnit * listing.ItemQuantity + listing.TotalTax),
                listing.IsHq,
                now);
        }

        TrimMarketCache(now);
        SaveMarketCache();
    }

    private void TrimMarketCache(DateTimeOffset now)
    {
        var cutoff = now - CacheMaxAge;
        foreach (var key in marketCache
                     .Where(entry => entry.Value.ObservedAt < cutoff)
                     .Select(entry => entry.Key)
                     .ToList())
            marketCache.Remove(key);

        if (marketCache.Count <= CacheMaxEntries)
            return;

        foreach (var key in marketCache
                     .OrderBy(entry => entry.Value.ObservedAt)
                     .Take(marketCache.Count - CacheMaxEntries)
                     .Select(entry => entry.Key)
                     .ToList())
            marketCache.Remove(key);
    }

    /// <summary>
    /// For each world where we hold a live-verified observation newer than Universalis' own freshest
    /// report for that item/world, substitutes our observation instead of trusting the (possibly
    /// stale) Universalis listings. Worlds with no recent personal observation are left exactly as
    /// Universalis reported them.
    /// </summary>
    internal List<MarketQuote> MergeWithCache(uint itemId, List<MarketQuote> universalisListings)
    {
        var cutoff = DateTimeOffset.UtcNow - CacheFreshnessWindow;
        var cachedForItem = marketCache.Values
            .Where(entry => entry.ItemId == itemId && entry.ObservedAt >= cutoff)
            .ToList();
        if (cachedForItem.Count == 0)
            return universalisListings;

        var universalisFreshnessByWorld = universalisListings
            .GroupBy(listing => listing.World, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Max(listing => listing.LastReviewTime), StringComparer.OrdinalIgnoreCase);

        var replacedWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<MarketQuote>();

        foreach (var worldGroup in cachedForItem.GroupBy(entry => entry.World, StringComparer.OrdinalIgnoreCase))
        {
            var cacheStamp = worldGroup.Max(entry => entry.ObservedAt.ToUnixTimeSeconds());
            var universalisStamp = universalisFreshnessByWorld.GetValueOrDefault(worldGroup.Key, 0L);
            if (cacheStamp <= universalisStamp)
                continue;

            replacedWorlds.Add(worldGroup.Key);
            merged.AddRange(worldGroup.Select(entry => new MarketQuote(
                entry.World,
                entry.PricePerUnit,
                entry.Quantity,
                entry.Total,
                entry.Hq,
                entry.RetainerName,
                entry.ObservedAt.ToUnixTimeSeconds(),
                entry.ListingId,
                entry.RetainerId,
                LiveVerified: true)));
        }

        merged.AddRange(universalisListings.Where(listing => !replacedWorlds.Contains(listing.World)));

        return merged
            .OrderBy(listing => listing.PricePerUnit)
            .ThenBy(listing => listing.Total)
            .ToList();
    }

    private Dictionary<string, CachedListing> LoadMarketCache()
    {
        try
        {
            if (!File.Exists(marketCachePath))
                return [];

            return JsonSerializer.Deserialize<Dictionary<string, CachedListing>>(File.ReadAllText(marketCachePath)) ?? [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Market cache could not be loaded");
            return [];
        }
    }

    private void SaveMarketCache()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(marketCachePath)!);
            File.WriteAllText(marketCachePath, JsonSerializer.Serialize(marketCache));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Market cache could not be saved");
        }
    }
}
