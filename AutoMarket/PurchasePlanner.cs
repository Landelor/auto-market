using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoMarket;

internal static class PurchasePlanner
{
    internal static List<MarketQuote> PlanQuotes(IEnumerable<MarketQuote> listings, int targetQuantity, long maxUnitPrice, long maxBudget)
    {
        var candidates = listings
            .Where(listing => maxUnitPrice <= 0 || listing.PricePerUnit <= maxUnitPrice)
            .ToList();
        if (candidates.Count == 0)
            return [];

        var target = Math.Max(1, targetQuantity);
        var limit = (int)Math.Min(int.MaxValue, (long)target + candidates.Max(listing => listing.Quantity) - 1);
        var plans = new Dictionary<int, (long Total, List<MarketQuote> Listings)>
        {
            [0] = (0, []),
        };
        foreach (var listing in candidates)
        {
            foreach (var (quantity, plan) in plans.ToArray())
            {
                var nextQuantity = (long)quantity + listing.Quantity;
                var nextTotal = plan.Total + listing.Total;
                if (nextQuantity > limit || (maxBudget > 0 && nextTotal > maxBudget))
                    continue;
                if (plans.TryGetValue((int)nextQuantity, out var existing) && existing.Total <= nextTotal)
                    continue;

                plans[(int)nextQuantity] = (nextTotal, [.. plan.Listings, listing]);
            }

            // ponytail: keep quantity planning responsive; use the price-sorted fallback for unusually large carts.
            if (plans.Count > 20_000)
                return GreedyPlan(candidates, target, maxBudget);
        }

        var best = plans
            .Where(plan => plan.Key >= target)
            .OrderBy(plan => plan.Key)
            .ThenBy(plan => plan.Value.Total)
            .Select(plan => plan.Value.Listings)
            .FirstOrDefault();
        return best ?? plans
            .OrderByDescending(plan => plan.Key)
            .ThenBy(plan => plan.Value.Total)
            .First().Value.Listings;
    }

    private static List<MarketQuote> GreedyPlan(IEnumerable<MarketQuote> listings, int targetQuantity, long maxBudget)
    {
        var result = new List<MarketQuote>();
        long total = 0;
        var quantity = 0;
        foreach (var listing in listings.OrderBy(listing => listing.PricePerUnit).ThenBy(listing => listing.Total))
        {
            if (maxBudget > 0 && total + listing.Total > maxBudget)
                continue;

            result.Add(listing);
            total += listing.Total;
            quantity += listing.Quantity;
            if (quantity >= targetQuantity)
                break;
        }

        return result;
    }
}
