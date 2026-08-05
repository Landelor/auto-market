using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    private void AddToCart(ItemChoice item, List<MarketQuote> visibleListings)
    {
        var plan = PurchasePlanner.PlanQuotes(visibleListings, targetQuantity, maxUnitPrice, maxBudget);
        if (plan.Count == 0)
        {
            status = "No listings fit the quantity, unit-price, and budget limits.";
            return;
        }

        cart.Add(new CartOrder(nextCartId++, item.Id, item.Name, targetQuantity, maxBudget, plan));
        status = $"Added {item.Name}: {plan.Sum(listing => listing.Quantity):N0}/{targetQuantity:N0} units, {plan.Sum(listing => listing.Total):N0} gil estimated.";
    }

    private void AddListingToCart(ItemChoice item, MarketQuote listing)
    {
        cart.Add(new CartOrder(nextCartId++, item.Id, item.Name, listing.Quantity, 0, [listing]));
        status = $"Added {listing.Quantity:N0}x {item.Name} from {listing.World} to the shopping list.";
    }

    private void DrawCart(bool dependenciesReady)
    {
        ImGui.Separator();
        ImGui.Text("Shopping list and grouped route");
        if (cart.Count == 0)
        {
            ImGui.TextDisabled("The shopping list is empty.");
            return;
        }

        var remove = -1;
        ImGui.TextDisabled("Planned from Universalis; each listing is verified live before purchase.");
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit;
        if (ImGui.BeginTable("##AutoMarketCart", 11, flags, new Vector2(0, 220)))
        {
            ImGui.TableSetupScrollFreeze(1, 1);
            ImGui.TableSetupColumn("Item");
            ImGui.TableSetupColumn("Quality");
            ImGui.TableSetupColumn("Target");
            ImGui.TableSetupColumn("Stack");
            ImGui.TableSetupColumn("Unit price");
            ImGui.TableSetupColumn("Stack total");
            ImGui.TableSetupColumn("World");
            ImGui.TableSetupColumn("Data center");
            ImGui.TableSetupColumn("Retainer");
            ImGui.TableSetupColumn("Updated");
            ImGui.TableSetupColumn("Action");
            ImGui.TableHeadersRow();
            for (var index = 0; index < cart.Count; index++)
            {
                var order = cart[index];
                for (var quoteIndex = 0; quoteIndex < order.PlannedQuotes.Count; quoteIndex++)
                {
                    var quote = order.PlannedQuotes[quoteIndex];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(order.ItemName);
                    ImGui.TableNextColumn();
                    ImGui.Text(quote.Hq ? "HQ" : "NQ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{order.TargetQuantity:N0}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{quote.Quantity:N0}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{quote.PricePerUnit:N0}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{quote.Total:N0}");
                    ImGui.TableNextColumn();
                    ImGui.Text(quote.World);
                    ImGui.TableNextColumn();
                    ImGui.Text(GetDataCenter(quote.World));
                    ImGui.TableNextColumn();
                    ImGui.Text(quote.RetainerName);
                    ImGui.TableNextColumn();
                    ImGui.Text(FormatAge(quote.LastReviewTime));
                    ImGui.TableNextColumn();
                    if (quoteIndex == 0 && !plugin.IsPurchasePending && ImGui.SmallButton($"Remove plan##cart-{order.Id}"))
                        remove = index;
                }
            }
            ImGui.EndTable();
        }

        if (remove >= 0)
            cart.RemoveAt(remove);

        if (!dependenciesReady)
        {
            ImGui.TextDisabled("Enable Lifestream to start the route.");
        }
        else if (plugin.IsPurchasePending)
        {
            ImGui.TextDisabled("Route in progress...");
        }
        else if (ImGui.Button("Start grouped purchase route"))
        {
            var error = plugin.StartCart(cart);
            status = error ?? $"Started a {cart.SelectMany(order => order.PlannedQuotes).Select(quote => quote.World).Distinct(StringComparer.OrdinalIgnoreCase).Count():N0}-world route.";
        }

        ImGui.SameLine();
        if (!plugin.IsPurchasePending && ImGui.Button("Clear cart"))
            cart.Clear();
    }

    private void DrawHistory()
    {
        ImGui.Separator();
        if (!ImGui.CollapsingHeader($"Purchase history ({plugin.PurchaseHistory.Count:N0})"))
            return;

        if (plugin.PurchaseHistory.Count == 0)
        {
            ImGui.TextDisabled("No confirmed purchases yet.");
            return;
        }

        if (!ImGui.BeginTable("##AutoMarketHistory", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Time");
        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Quality");
        ImGui.TableSetupColumn("Quantity");
        ImGui.TableSetupColumn("Unit price");
        ImGui.TableSetupColumn("Total + tax");
        ImGui.TableSetupColumn("World");
        ImGui.TableHeadersRow();
        foreach (var entry in plugin.PurchaseHistory.Take(20))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(entry.PurchasedAt.ToLocalTime().ToString("g"));
            ImGui.TableNextColumn();
            ImGui.Text(entry.ItemName);
            ImGui.TableNextColumn();
            ImGui.Text(entry.Hq ? "HQ" : "NQ");
            ImGui.TableNextColumn();
            ImGui.Text($"{entry.Quantity:N0}");
            ImGui.TableNextColumn();
            ImGui.Text($"{entry.UnitPrice:N0}");
            ImGui.TableNextColumn();
            ImGui.Text($"{entry.Total:N0}");
            ImGui.TableNextColumn();
            ImGui.Text(entry.World);
        }
        ImGui.EndTable();
    }
}
