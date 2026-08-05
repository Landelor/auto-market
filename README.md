# AutoMarket

AutoMarket is a Dalamud plugin that lists current Universalis offers for an item across your data center or entire region, then asks Lifestream to take you to the chosen world's market board and purchase that listing.

## Requirements

- Dalamud API 15 / .NET 10
- [Lifestream](https://github.com/NightmareXIV/Lifestream)

AutoMarket checks these dependencies when it loads.

## Use

1. Run `/automarket` or `/am Item Name`.
2. Choose **Current DC** or **Entire region** and optionally filter to **HQ only** or **NQ only**.
3. Set a target quantity, maximum unit price, and optional total budget.
4. Add one or more item plans to the cart, then start the grouped world route; or use **Travel + buy** for one stack.
5. AutoMarket buys whole stacks, uses equal-or-cheaper live fallbacks with matching HQ/NQ quality, and stops when the target or budget is reached.
6. Every purchase checks live gil and free inventory space, shows progress, and is saved to purchase history after server confirmation.

Universalis data is crowdsourced and can be stale. AutoMarket never buys above the planned unit price or budget. Because market listings are sold as whole stacks, the final stack can exceed the requested target quantity.

## Source layout

- `Plugin.cs`: Dalamud services, plugin lifecycle, current location, and Lifestream travel.
- `Plugin.Purchases.cs`: purchase queue, budgets, progress, and history.
- `Plugin.MarketBoard.cs`: in-game market search, live validation, retries, and purchase requests.
- `MarketClient.cs`: Universalis HTTP client and response mapping.
- `MarketModels.cs` / `PurchasePlanner.cs`: shared records and quantity-first planning.
- `Windows/MainWindow*.cs`: main UI, search UI, and shopping/history UI.

## Installation

TBD
