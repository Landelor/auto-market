using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    private void DrawSearchOptions()
    {
        var dataCenter = plugin.CurrentDataCenter ?? "not logged in";
        var region = plugin.CurrentRegion;

        ImGui.Text("Search scope");
        if (ImGui.RadioButton($"Current DC ({dataCenter})", searchScope == SearchScope.DataCenter))
        {
            searchScope = SearchScope.DataCenter;
            listings.Clear();
        }

        if (region is not null)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"Entire region ({region})", searchScope == SearchScope.Region))
            {
                searchScope = SearchScope.Region;
                listings.Clear();
            }
        }

        ImGui.Text("Quality");
        if (ImGui.RadioButton("Any##Quality", qualityFilter == QualityFilter.Any))
            qualityFilter = QualityFilter.Any;
        ImGui.SameLine();
        if (ImGui.RadioButton("HQ only##Quality", qualityFilter == QualityFilter.Hq))
            qualityFilter = QualityFilter.Hq;
        ImGui.SameLine();
        if (ImGui.RadioButton("NQ only##Quality", qualityFilter == QualityFilter.Nq))
            qualityFilter = QualityFilter.Nq;

        ImGui.SetNextItemWidth(180);
        if (ImGui.InputInt("Target quantity", ref targetQuantity, 1, 10))
            targetQuantity = Math.Max(1, targetQuantity);
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputInt("Maximum unit price (0 = none)", ref maxUnitPrice, 100, 1000))
            maxUnitPrice = Math.Max(0, maxUnitPrice);
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputInt("Maximum budget (0 = none)", ref maxBudget, 1000, 10000))
            maxBudget = Math.Max(0, maxBudget);
    }

    private void DrawListings(List<MarketQuote> visibleListings, bool dependenciesReady)
    {
        var flags = ImGuiTableFlags.Borders
            | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.SizingFixedFit
            | ImGuiTableFlags.NoSavedSettings;
        if (!ImGui.BeginTable("##AutoMarketListings", 8, flags, new Vector2(0, 250)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Price");
        ImGui.TableSetupColumn("Quality");
        ImGui.TableSetupColumn("Stack");
        ImGui.TableSetupColumn("Total");
        ImGui.TableSetupColumn("World");
        ImGui.TableSetupColumn("Data center");
        ImGui.TableSetupColumn("Updated");
        ImGui.TableSetupColumn("Action");
        ImGui.TableHeadersRow();

        for (var index = 0; index < visibleListings.Count; index++)
        {
            var listing = visibleListings[index];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{listing.PricePerUnit:N0}");
            ImGui.TableNextColumn();
            ImGui.Text(listing.Hq ? "HQ" : "NQ");
            ImGui.TableNextColumn();
            ImGui.Text($"{listing.Quantity:N0}");
            ImGui.TableNextColumn();
            ImGui.Text($"{listing.Total:N0}");
            ImGui.TableNextColumn();
            ImGui.Text(listing.World);
            ImGui.TableNextColumn();
            ImGui.Text(GetDataCenter(listing.World));
            ImGui.TableNextColumn();
            ImGui.Text(FormatAge(listing.LastReviewTime));
            ImGui.TableNextColumn();
            if (plugin.IsPurchasePending)
            {
                ImGui.Text("Working...");
            }
            else
            {
                if (!dependenciesReady)
                {
                    ImGui.Text("Needs Lifestream");
                }
                else if (ImGui.SmallButton($"Travel + buy##{index}-{listing.World}-{listing.PricePerUnit}"))
                {
                    var error = selectedItem is { } item
                        ? plugin.TravelAndBuy(item.Id, item.Name, listing)
                        : "Choose an item first.";
                    status = error ?? $"Travel and purchase started for {selectedItem?.Name}.";
                }

                ImGui.SameLine();
                if (ImGui.SmallButton($"Add to list##{index}-{listing.ListingId}"))
                {
                    if (selectedItem is { } item)
                        AddListingToCart(item, listing);
                }
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Retainer: {listing.RetainerName}");
        }

        ImGui.EndTable();
    }

    private void RefreshMatches()
    {
        matches.Clear();
        var term = searchText.Trim();
        if (term.Length < 2)
            return;

        matches.AddRange(items
            .Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.Name.Length)
            .Take(10));

        status = matches.Count == 0 ? "No marketable item matched that search." : "Choose an item.";
    }

    private void StartSearch(ItemChoice item)
    {
        var scope = searchScope == SearchScope.Region ? plugin.CurrentRegion : plugin.CurrentDataCenter;
        if (string.IsNullOrWhiteSpace(scope))
        {
            status = "Log in to a supported region before searching.";
            return;
        }

        searchCancellation = new CancellationTokenSource();
        searchItemId = item.Id;
        listings.Clear();
        status = $"Searching {scope} for {item.Name}...";
        searchTask = plugin.MarketClient.FindListingsAsync(scope, item.Id, searchCancellation.Token);
    }

    private void FinishSearch()
    {
        if (searchTask is not { IsCompleted: true } completed)
            return;

        try
        {
            var result = completed.GetAwaiter().GetResult();
            listings = selectedItem?.Id == searchItemId ? result : [];
            var dataCenterCount = listings
                .Select(listing => GetDataCenter(listing.World))
                .Where(dataCenter => dataCenter != "Unknown")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            status = listings.Count == 0
                ? "Universalis returned no purchasable listings for this scope."
                : $"Found {listings.Count:N0} listings across {dataCenterCount:N0} data centers.";
        }
        catch (OperationCanceledException)
        {
            status = "Search cancelled.";
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Universalis search failed");
            status = $"Universalis search failed: {ex.Message}";
        }
        finally
        {
            searchTask = null;
            searchItemId = null;
            searchCancellation?.Dispose();
            searchCancellation = null;
        }
    }

    private bool MatchesQuality(MarketQuote listing) => qualityFilter switch
    {
        QualityFilter.Hq => listing.Hq,
        QualityFilter.Nq => !listing.Hq,
        _ => true,
    };

    private string GetDataCenter(string world) => worldDataCenters.TryGetValue(world, out var dataCenter)
        ? dataCenter
        : "Unknown";

    private static string FormatAge(long unixSeconds)
    {
        if (unixSeconds <= 0)
            return "Unknown";

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return age.TotalMinutes < 60
            ? $"{Math.Max(0, age.TotalMinutes):N0}m"
            : $"{Math.Max(0, age.TotalHours):N1}h";
    }
}
