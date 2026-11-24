using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMarket.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    private void AddToCart(ItemChoice item, List<MarketQuote> visibleListings)
    {
        var owned = plugin.GetOwnedQuantity(item.Id);
        var remaining = (int)Math.Clamp((long)targetQuantity - owned, 0, int.MaxValue);
        if (remaining <= 0)
        {
            status = $"You already have {owned:N0}/{targetQuantity:N0} {item.Name} between your inventory and retainers; nothing to buy.";
            return;
        }

        var plan = PurchasePlanner.PlanQuotes(visibleListings, remaining, maxUnitPrice, maxBudget);
        if (plan.Count == 0)
        {
            status = owned > 0
                ? $"No listings fit the remaining {remaining:N0} needed (you already have {owned:N0}) within the unit-price and budget limits."
                : "No listings fit the quantity, unit-price, and budget limits.";
            return;
        }

        cart.Add(new CartOrder(nextCartId++, item.Id, item.Name, remaining, maxBudget, plan));
        status = owned > 0
            ? $"Added {item.Name}: {plan.Sum(listing => listing.Quantity):N0}/{remaining:N0} units still needed (you already have {owned:N0}), {plan.Sum(listing => listing.Total):N0} gil estimated."
            : $"Added {item.Name}: {plan.Sum(listing => listing.Quantity):N0}/{remaining:N0} units, {plan.Sum(listing => listing.Total):N0} gil estimated.";
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
        if (ImGui.BeginTable("##AutoMarketCart", 10, flags, new Vector2(0, 220)))
        {
            ImGui.TableSetupScrollFreeze(1, 1);
            ImGui.TableSetupColumn("Item");
            ImGui.TableSetupColumn("Quality");
            ImGui.TableSetupColumn("Target");
            ImGui.TableSetupColumn("Stack");
            ImGui.TableSetupColumn("Unit price");
            ImGui.TableSetupColumn("Stack total");
            ImGui.TableSetupColumn("World");
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
                    ImGui.Text(quote.RetainerName);
                    ImGui.TableNextColumn();
                    var (freshnessText, freshnessColor) = FormatFreshness(quote);
                    ImGui.TextColored(freshnessColor, freshnessText);
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
            ImGui.TextDisabled("Route in progress - see the dashboard card above to stop it.");
        }
        else
        {
            var totalGil = cart.SelectMany(order => order.PlannedQuotes).Sum(quote => quote.Total);
            if (AdtHeroButton.Draw(FontAwesomeIcon.Play, "Start grouped purchase route", $"{cart.Count:N0} items \u00b7 {totalGil:N0} gil", AdtStyling.AccentTeal, true))
            {
                var error = plugin.StartCart(cart);
                status = error ?? $"Started a {cart.SelectMany(order => order.PlannedQuotes).Select(quote => quote.World).Distinct(StringComparer.OrdinalIgnoreCase).Count():N0}-world route.";
            }
        }

        ImGui.Spacing();
        if (!plugin.IsPurchasePending && ImGui.Button("Clear cart"))
            cart.Clear();
    }
}
